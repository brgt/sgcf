using NodaTime;
using Sgcf.Application.Contratos;
using Sgcf.Domain.Alertas;
using Sgcf.Domain.Cronograma;

namespace Sgcf.Application.Alertas.Rules;

/// <summary>
/// Gera alertas de vencimento iminente para eventos de cronograma nos horizontes D-0, D-3 e D-7.
/// Idempotente: a <c>ChaveIdempotencia</c> inclui o eventId e a data de avaliação,
/// garantindo que rodar duas vezes no mesmo dia não duplique alertas.
/// </summary>
public sealed class RegraVencimentoIminente(
    IEventoCronogramaRepository cronogramaRepo,
    IAlertaRepository alertaRepo,
    IClock clock) : IAlertaRule
{
    /// <inheritdoc />
    public string Nome => "vencimento";

    // Pares (diasFuturos, severidade): D-0 é crítico, D-3 é atenção, D-7 é informativo.
    private static readonly (int Dias, SeveridadeAlerta Severidade)[] Horizontes =
    [
        (0, SeveridadeAlerta.Critico),
        (3, SeveridadeAlerta.Atencao),
        (7, SeveridadeAlerta.Informativo),
    ];

    private static readonly PerfilCockpit[] PerfisVisiveis =
        [PerfilCockpit.Tesouraria, PerfilCockpit.GerenteFinanceiro];

    /// <inheritdoc />
    public async Task AvaliarAsync(LocalDate hoje, CancellationToken ct)
    {
        foreach ((int dias, SeveridadeAlerta severidade) in Horizontes)
        {
            LocalDate dataAlvo = hoje.PlusDays(dias);

            IReadOnlyList<EventoCronograma> eventos =
                await cronogramaRepo.ListPendentesVencendoEmAsync(dataAlvo, ct);

            foreach (EventoCronograma evento in eventos)
            {
                // Chave garante: uma vez por evento por dia de avaliação.
                string chave = $"{Nome}:{evento.Id}:{hoje:yyyy-MM-dd}";

                string titulo = dias switch
                {
                    0 => $"Vencimento hoje — {evento.ContratoId:D}",
                    3 => $"Vencimento em 3 dias — {evento.ContratoId:D}",
                    _ => $"Vencimento em 7 dias — {evento.ContratoId:D}",
                };

                string descricao = $"Evento de cronograma vencendo em {dataAlvo:yyyy-MM-dd} " +
                    $"(contrato {evento.ContratoId:D}, valor {evento.ValorMoedaOriginal.Valor:N2} " +
                    $"{evento.ValorMoedaOriginal.Moeda}).";

                // ExpiraEm: fim do dia do vencimento (meia-noite do dia seguinte).
                // Alertas de vencimento deixam de ser relevantes após a data alvo.
                Instant expiraEm = dataAlvo.PlusDays(1)
                    .AtMidnight()
                    .InUtc()
                    .ToInstant();

                Alerta alerta = Alerta.Criar(
                    categoria: CategoriaAlerta.Vencimento,
                    severidade: severidade,
                    titulo: titulo,
                    descricao: descricao,
                    origemTipo: "EventoCronograma",
                    origemId: evento.Id,
                    perfisVisiveis: PerfisVisiveis,
                    chaveIdempotencia: chave,
                    clock: clock,
                    acaoRotulo: "Ver contrato",
                    acaoRota: $"/contratos/{evento.ContratoId}",
                    expiraEm: expiraEm);

                await alertaRepo.TryAddIdempotentAsync(alerta, ct);
            }
        }
    }
}
