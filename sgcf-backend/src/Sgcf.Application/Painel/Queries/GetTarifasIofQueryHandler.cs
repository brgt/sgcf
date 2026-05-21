using MediatR;
using NodaTime;
using Sgcf.Application.Bancos;
using Sgcf.Application.Common;
using Sgcf.Application.Contratos;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cronograma;

namespace Sgcf.Application.Painel.Queries;

/// <summary>
/// Agrega eventos de IOF e tarifas do cronograma por banco e por modalidade.
/// Realiza uma única consulta ao repositório de eventos e junta com os dados de contrato em memória.
/// Quando um evento em moeda estrangeira não possui ValorBrlEstimado, o valor é tratado como zero
/// e a completude do resultado é marcada como Parcial.
/// </summary>
public sealed class GetTarifasIofQueryHandler(
    IEventoCronogramaRepository eventoRepo,
    IContratoRepository contratoRepo,
    IBancoRepository bancoRepo,
    IClock clock)
    : IRequestHandler<GetTarifasIofQuery, EnvelopeResponse<TarifasIofDto>>
{
    // Tipos que representam IOF cambial.
    private static readonly IReadOnlyCollection<TipoEventoCronograma> TiposIof =
    [
        TipoEventoCronograma.IofCambio,
    ];

    // Tipos que representam tarifas e comissões diversas.
    private static readonly IReadOnlyCollection<TipoEventoCronograma> TiposTarifas =
    [
        TipoEventoCronograma.TarifaRof,
        TipoEventoCronograma.TarifaCademp,
        TipoEventoCronograma.TarifaCartorio,
        TipoEventoCronograma.TarifaFgi,
        TipoEventoCronograma.ComissaoSblc,
        TipoEventoCronograma.ComissaoCpg,
        TipoEventoCronograma.ComissaoGarantiaFgi,
        TipoEventoCronograma.BreakFundingFee,
        TipoEventoCronograma.MultaMoratoria,
    ];

    public async Task<EnvelopeResponse<TarifasIofDto>> Handle(
        GetTarifasIofQuery query,
        CancellationToken cancellationToken)
    {
        // Reúne todos os tipos numa única coleção para fazer apenas 1 round-trip ao banco.
        TipoEventoCronograma[] todosTipos = [.. TiposIof, .. TiposTarifas];

        IReadOnlyList<EventoCronograma> eventos =
            await eventoRepo.ListPorTiposAsync(todosTipos, cancellationToken);

        IReadOnlyList<Contrato> contratos =
            await contratoRepo.ListAsync(cancellationToken);

        // Índice rápido: ContratoId → Contrato. Evita O(n²) no loop de eventos.
        Dictionary<Guid, Contrato> contratosPorId = contratos
            .ToDictionary(c => c.Id);

        // Resolve nomes de bancos. Coleta ids únicos primeiro para minimizar chamadas ao repo.
        HashSet<Guid> bancoIds = contratos
            .Select(c => c.BancoId)
            .ToHashSet();

        Dictionary<Guid, string> nomesPorBancoId = await ResolverNomesBancosAsync(bancoIds, cancellationToken);

        // Acumuladores de agregação.
        Dictionary<Guid, (string Nome, decimal Iof, decimal Tarifas)> porBanco = new();
        Dictionary<string, (decimal Iof, decimal Tarifas)> porModalidade = new();

        decimal totalIof = 0m;
        decimal totalTarifas = 0m;
        bool algumEventoSemBrl = false;

        foreach (EventoCronograma evento in eventos)
        {
            if (!contratosPorId.TryGetValue(evento.ContratoId, out Contrato? contrato))
            {
                // Evento órfão (contrato deletado ou de outro tenant): ignora.
                continue;
            }

            decimal valorBrl = ResolverValorBrl(evento, ref algumEventoSemBrl);
            bool ehIof = TiposIof.Contains(evento.Tipo);

            // --- por banco ---
            string nomeBanco = nomesPorBancoId.TryGetValue(contrato.BancoId, out string? nome)
                ? nome
                : $"Banco {contrato.BancoId}";

            if (!porBanco.TryGetValue(contrato.BancoId, out (string Nome, decimal Iof, decimal Tarifas) entradaBanco))
            {
                entradaBanco = (nomeBanco, 0m, 0m);
            }

            porBanco[contrato.BancoId] = ehIof
                ? entradaBanco with { Iof = entradaBanco.Iof + valorBrl }
                : entradaBanco with { Tarifas = entradaBanco.Tarifas + valorBrl };

            // --- por modalidade ---
            string modalidadeKey = contrato.Modalidade.ToString();

            if (!porModalidade.TryGetValue(modalidadeKey, out (decimal Iof, decimal Tarifas) entradaModalidade))
            {
                entradaModalidade = (0m, 0m);
            }

            porModalidade[modalidadeKey] = ehIof
                ? entradaModalidade with { Iof = entradaModalidade.Iof + valorBrl }
                : entradaModalidade with { Tarifas = entradaModalidade.Tarifas + valorBrl };

            // --- totais globais ---
            if (ehIof)
            {
                totalIof += valorBrl;
            }
            else
            {
                totalTarifas += valorBrl;
            }
        }

        List<TarifasIofPorBancoDto> listaPorBanco = porBanco
            .Select(kv => new TarifasIofPorBancoDto(
                BancoId: kv.Key,
                NomeBanco: kv.Value.Nome,
                TotalIofBrl: Math.Round(kv.Value.Iof, 2, MidpointRounding.AwayFromZero),
                TotalTarifasBrl: Math.Round(kv.Value.Tarifas, 2, MidpointRounding.AwayFromZero),
                TotalBrl: Math.Round(kv.Value.Iof + kv.Value.Tarifas, 2, MidpointRounding.AwayFromZero)))
            .ToList();

        List<TarifasIofPorModalidadeDto> listaPorModalidade = porModalidade
            .Select(kv => new TarifasIofPorModalidadeDto(
                Modalidade: kv.Key,
                TotalIofBrl: Math.Round(kv.Value.Iof, 2, MidpointRounding.AwayFromZero),
                TotalTarifasBrl: Math.Round(kv.Value.Tarifas, 2, MidpointRounding.AwayFromZero),
                TotalBrl: Math.Round(kv.Value.Iof + kv.Value.Tarifas, 2, MidpointRounding.AwayFromZero)))
            .ToList();

        TarifasIofDto dados = new(
            TotalIofBrl: Math.Round(totalIof, 2, MidpointRounding.AwayFromZero),
            TotalTarifasBrl: Math.Round(totalTarifas, 2, MidpointRounding.AwayFromZero),
            TotalGeralBrl: Math.Round(totalIof + totalTarifas, 2, MidpointRounding.AwayFromZero),
            PorBanco: listaPorBanco.AsReadOnly(),
            PorModalidade: listaPorModalidade.AsReadOnly());

        EnvelopeMeta meta = new(
            DataHoraCalculo: clock.GetCurrentInstant(),
            FontesConsultadas:
            [
                new FonteConsultada("cronograma", "ok", eventos.Count),
            ],
            Completude: algumEventoSemBrl ? Completude.Parcial : Completude.Completo);

        return new EnvelopeResponse<TarifasIofDto>(dados, meta);
    }

    /// <summary>
    /// Retorna o valor BRL do evento.
    /// Prioriza ValorBrlEstimado. Quando ausente e a moeda já é BRL, usa ValorMoedaOriginal.
    /// Quando ausente e moeda é estrangeira, retorna 0 e sinaliza completude parcial.
    /// </summary>
    private static decimal ResolverValorBrl(EventoCronograma evento, ref bool algumEventoSemBrl)
    {
        if (evento.ValorBrlEstimado.HasValue)
        {
            return evento.ValorBrlEstimado.Value.Valor;
        }

        if (evento.Moeda == Moeda.Brl)
        {
            return evento.ValorMoedaOriginal.Valor;
        }

        // Moeda estrangeira sem estimativa BRL: dados incompletos.
        algumEventoSemBrl = true;
        return 0m;
    }

    /// <summary>
    /// Resolve nomes de bancos via IBancoRepository.
    /// Itera sequencialmente sobre os ids únicos — a coleção é pequena (tipicamente &lt; 20 bancos por tenant).
    /// </summary>
    private async Task<Dictionary<Guid, string>> ResolverNomesBancosAsync(
        IEnumerable<Guid> bancoIds,
        CancellationToken cancellationToken)
    {
        Dictionary<Guid, string> resultado = new();

        foreach (Guid bancoId in bancoIds)
        {
            Domain.Bancos.Banco? banco = await bancoRepo.GetByIdAsync(bancoId, cancellationToken);
            if (banco is not null)
            {
                resultado[bancoId] = banco.Apelido;
            }
        }

        return resultado;
    }
}
