using System.Text.Json;

using MediatR;

using NodaTime;

using Sgcf.Application.Common;
using Sgcf.Application.Cotacoes;

using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Painel.EconomiaTributaria;

/// <summary>
/// Agrega a economia tributária acumulada (benefício estimado IRPJ + CSLL)
/// para o período e banco informados. GAP-CKP-21.
///
/// <para>
/// Alíquota efetiva combinada: IRPJ 15% + adicional IRPJ 10% + CSLL 9% = 34%.
/// O benefício é calculado sobre a economia equalizada por CDI
/// (<c>EconomiaAjustadaCdiBrl</c>), não sobre a economia bruta.
/// </para>
/// </summary>
public sealed record GetEconomiaTributariaQuery(
    int DeAno,
    int DeMes,
    int AteAno,
    int AteMes,
    Guid? BancoId) : IRequest<EnvelopeResponse<EconomiaTributariaDto>>;

/// <summary>
/// Handler para <see cref="GetEconomiaTributariaQuery"/>.
/// </summary>
public sealed class GetEconomiaTributariaQueryHandler(
    IEconomiaRepository repository,
    IClock clock)
    : IRequestHandler<GetEconomiaTributariaQuery, EnvelopeResponse<EconomiaTributariaDto>>
{
    /// <summary>
    /// Alíquota efetiva combinada: IRPJ (15% + adicional 10%) + CSLL (9%) = 34%.
    /// Definida como constante nomeada para tornar explícita a origem do coeficiente.
    /// </summary>
    private const decimal AliquotaEfetivaCombinada = 0.34m;

    public async Task<EnvelopeResponse<EconomiaTributariaDto>> Handle(
        GetEconomiaTributariaQuery query,
        CancellationToken cancellationToken)
    {
        YearMonth de = new(query.DeAno, query.DeMes);
        YearMonth ate = new(query.AteAno, query.AteMes);

        IReadOnlyList<EconomiaNegociacao> economias = await repository.ListByPeriodoAsync(
            de,
            ate,
            query.BancoId,
            cancellationToken);

        decimal totalEconomiaBrl = Math.Round(
            economias.Sum(e => e.EconomiaBrl.Valor),
            2,
            MidpointRounding.AwayFromZero);

        decimal totalEconomiaAjustadaCdiBrl = Math.Round(
            economias.Sum(e => e.EconomiaAjustadaCdiBrl.Valor),
            2,
            MidpointRounding.AwayFromZero);

        decimal beneficioTributarioEstimadoBrl = Math.Round(
            totalEconomiaAjustadaCdiBrl * AliquotaEfetivaCombinada,
            2,
            MidpointRounding.AwayFromZero);

        List<EconomiaTributariaPorBancoDto> porBanco = BuildPorBanco(economias, query.BancoId);

        EconomiaTributariaDto data = new(
            DeAno: query.DeAno,
            DeMes: query.DeMes,
            AteAno: query.AteAno,
            AteMes: query.AteMes,
            TotalEconomiaBrl: totalEconomiaBrl,
            TotalEconomiaAjustadaCdiBrl: totalEconomiaAjustadaCdiBrl,
            BeneficioTributarioEstimadoBrl: beneficioTributarioEstimadoBrl,
            TotalOperacoes: economias.Count,
            PorBanco: porBanco.AsReadOnly());

        return new EnvelopeResponse<EconomiaTributariaDto>(
            Data: data,
            Meta: new EnvelopeMeta(
                DataHoraCalculo: clock.GetCurrentInstant(),
                FontesConsultadas: [new FonteConsultada("economia_negociacao", "ok", economias.Count)],
                Completude: Completude.Completo));
    }

    // ── helpers ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Agrupa as economias por banco extraindo o <c>BancoId</c> do snapshot JSON da proposta.
    /// Registros sem BancoId identificável no snapshot são agrupados sob <c>null</c>.
    /// Quando um filtro <paramref name="filteredBancoId"/> está ativo e o snapshot não contém
    /// BancoId válido, o banco do filtro é usado como chave do grupo — evitando perda de dados.
    /// </summary>
    /// <summary>Sentinela textual para operações sem BancoId identificável no snapshot.</summary>
    private const string ChaveSemBanco = "";

    private static List<EconomiaTributariaPorBancoDto> BuildPorBanco(
        IReadOnlyList<EconomiaNegociacao> economias,
        Guid? filteredBancoId)
    {
        // Guid? não satisfaz a restrição notnull de Dictionary<TKey,TValue>.
        // Usamos string como chave: representação canônica do Guid ou string vazia
        // (ChaveSemBanco) para o grupo de operações sem banco identificável.
        Dictionary<string, (Guid? BancoId, List<EconomiaNegociacao> Itens)> porBanco = [];

        foreach (EconomiaNegociacao e in economias)
        {
            Guid? bancoId = ExtrairBancoIdDoSnapshot(e.SnapshotPropostaJson) ?? filteredBancoId;
            string chave = bancoId.HasValue ? bancoId.Value.ToString() : ChaveSemBanco;

            if (!porBanco.TryGetValue(chave, out (Guid? BancoId, List<EconomiaNegociacao> Itens) entrada))
            {
                entrada = (bancoId, []);
                porBanco[chave] = entrada;
            }

            entrada.Itens.Add(e);
        }

        List<EconomiaTributariaPorBancoDto> resultado = new(porBanco.Count);

        foreach ((string _, (Guid? bancoId, List<EconomiaNegociacao> grupo)) in porBanco.OrderBy(x => x.Key))
        {
            decimal economiaBrl = Math.Round(
                grupo.Sum(e => e.EconomiaBrl.Valor),
                2,
                MidpointRounding.AwayFromZero);

            decimal economiaAjustadaCdiBrl = Math.Round(
                grupo.Sum(e => e.EconomiaAjustadaCdiBrl.Valor),
                2,
                MidpointRounding.AwayFromZero);

            decimal beneficio = Math.Round(
                economiaAjustadaCdiBrl * AliquotaEfetivaCombinada,
                2,
                MidpointRounding.AwayFromZero);

            resultado.Add(new EconomiaTributariaPorBancoDto(
                BancoId: bancoId,
                EconomiaBrl: economiaBrl,
                EconomiaAjustadaCdiBrl: economiaAjustadaCdiBrl,
                BeneficioTributarioEstimadoBrl: beneficio,
                Operacoes: grupo.Count));
        }

        return resultado;
    }

    /// <summary>
    /// Extrai o campo <c>BancoId</c> do snapshot JSON da proposta aceita.
    /// Retorna <c>null</c> quando o snapshot está ausente, malformado ou não contém o campo.
    /// </summary>
    private static Guid? ExtrairBancoIdDoSnapshot(string snapshotJson)
    {
        if (string.IsNullOrWhiteSpace(snapshotJson))
        {
            return null;
        }

        try
        {
            using JsonDocument doc = JsonDocument.Parse(snapshotJson);

            if (doc.RootElement.TryGetProperty("BancoId", out JsonElement bancoIdEl)
                && bancoIdEl.ValueKind == JsonValueKind.String
                && Guid.TryParse(bancoIdEl.GetString(), out Guid bancoId))
            {
                return bancoId;
            }
        }
        catch (JsonException)
        {
            // Snapshot malformado: ignora silenciosamente.
            // O registro ficará agrupado sob null ou sob filteredBancoId.
        }

        return null;
    }
}
