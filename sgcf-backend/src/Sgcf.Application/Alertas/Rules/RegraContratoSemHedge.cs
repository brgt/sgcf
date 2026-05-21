using NodaTime;
using Sgcf.Application.Contratos;
using Sgcf.Application.Hedge;
using Sgcf.Domain.Alertas;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Hedge;

namespace Sgcf.Application.Alertas.Rules;

/// <summary>
/// Gera alertas para contratos ativos em moeda estrangeira que não possuem
/// um instrumento de hedge ativo vinculado.
/// Lógica extraída de <c>GetPainelDividaQueryHandler.GerarAlertasSemHedge</c>.
///
/// Chave idempotente mensal (ano-mês) para evitar spam diário —
/// um contrato sem hedge é uma condição estrutural, não um evento pontual.
/// </summary>
public sealed class RegraContratoSemHedge(
    IContratoRepository contratoRepo,
    IHedgeRepository hedgeRepo,
    IAlertaRepository alertaRepo,
    IClock clock) : IAlertaRule
{
    /// <inheritdoc />
    public string Nome => "sem-hedge";

    private static readonly PerfilCockpit[] PerfisVisiveis =
        [PerfilCockpit.Tesouraria, PerfilCockpit.GerenteFinanceiro, PerfilCockpit.Cfo];

    /// <inheritdoc />
    public async Task AvaliarAsync(LocalDate hoje, CancellationToken ct)
    {
        IReadOnlyList<Contrato> contratos = await contratoRepo.ListAsync(ct);
        IReadOnlyList<InstrumentoHedge> hedgesAtivos = await hedgeRepo.ListAtivosAsync(ct);

        // Apenas contratos ativos em moeda estrangeira são relevantes para hedging.
        IEnumerable<Contrato> candidatos = contratos
            .Where(c => c.Status == StatusContrato.Ativo && c.Moeda != Moeda.Brl);

        // Lookup rápido: quais contratos possuem hedge ativo.
        HashSet<Guid> contratoIdsComHedge = hedgesAtivos
            .Select(h => h.ContratoId)
            .ToHashSet();

        foreach (Contrato contrato in candidatos)
        {
            if (contratoIdsComHedge.Contains(contrato.Id))
            {
                continue;
            }

            // Chave mensal: evita gerar um alerta por dia para condição estrutural.
            string chave = $"{Nome}:{contrato.Id}:{hoje:yyyy-MM}";

            string titulo = $"Contrato sem hedge — {contrato.NumeroExterno}";
            string descricao = $"Contrato {contrato.NumeroExterno} ({contrato.Id:D}) " +
                $"em {contrato.Moeda} não possui instrumento de hedge ativo vinculado.";

            Alerta alerta = Alerta.Criar(
                categoria: CategoriaAlerta.Hedge,
                severidade: SeveridadeAlerta.Atencao,
                titulo: titulo,
                descricao: descricao,
                origemTipo: "Contrato",
                origemId: contrato.Id,
                perfisVisiveis: PerfisVisiveis,
                chaveIdempotencia: chave,
                clock: clock,
                acaoRotulo: "Ver contrato",
                acaoRota: $"/contratos/{contrato.Id}");

            await alertaRepo.TryAddIdempotentAsync(alerta, ct);
        }
    }
}
