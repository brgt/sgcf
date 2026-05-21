using System.Globalization;
using MediatR;
using NodaTime;
using Sgcf.Domain.Hedge;

namespace Sgcf.Application.Hedge.Commands;

/// <summary>
/// Registra ou atualiza (upsert) o snapshot diário de MtM de um instrumento de hedge.
///
/// <para>Quando <see cref="DataReferencia"/> é nulo ou vazio, a data corrente em BRT é usada.</para>
/// <para>O handler lança <see cref="KeyNotFoundException"/> quando o hedge não existe no tenant.</para>
/// </summary>
public sealed record RegistrarHistoricoMtmCommand(
    Guid HedgeId,
    string? DataReferencia,
    decimal PayoffBrl,
    decimal SpotUtilizado,
    string TipoCotacao = "SPOT_INTRADAY") : IRequest<HistoricoMtmDiarioDto>;

/// <summary>
/// Processa <see cref="RegistrarHistoricoMtmCommand"/> fazendo upsert do snapshot de MtM.
/// Caminho de criação: <see cref="IHistoricoMtmRepository.GetAsync"/> retorna null → Criar + Add.
/// Caminho de atualização: registro já existe → Atualizar (sem novo Add).
/// </summary>
public sealed class RegistrarHistoricoMtmCommandHandler(
    IHedgeRepository hedgeRepo,
    IHistoricoMtmRepository historicoRepo,
    IClock clock)
    : IRequestHandler<RegistrarHistoricoMtmCommand, HistoricoMtmDiarioDto>
{
    private static readonly DateTimeZone FusoBrasilia =
        DateTimeZoneProviders.Tzdb["America/Sao_Paulo"];

    public async Task<HistoricoMtmDiarioDto> Handle(
        RegistrarHistoricoMtmCommand command,
        CancellationToken cancellationToken)
    {
        // Garante que o hedge existe no tenant corrente antes de prosseguir.
        InstrumentoHedge hedge = await hedgeRepo.GetByIdAsync(command.HedgeId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Instrumento de hedge com Id '{command.HedgeId}' não encontrado.");

        Instant agora = clock.GetCurrentInstant();

        LocalDate dataReferencia = string.IsNullOrWhiteSpace(command.DataReferencia)
            ? agora.InZone(FusoBrasilia).Date
            : LocalDate.FromDateTime(DateTime.Parse(command.DataReferencia, CultureInfo.InvariantCulture));

        HistoricoMtmDiario? existente = await historicoRepo.GetAsync(
            command.HedgeId,
            dataReferencia,
            cancellationToken);

        HistoricoMtmDiario historico;

        if (existente is null)
        {
            historico = HistoricoMtmDiario.Criar(
                command.HedgeId,
                dataReferencia,
                command.PayoffBrl,
                command.SpotUtilizado,
                command.TipoCotacao,
                agora);

            historicoRepo.Add(historico);
        }
        else
        {
            existente.Atualizar(command.PayoffBrl, command.SpotUtilizado, command.TipoCotacao, agora);
            historico = existente;
        }

        await historicoRepo.SaveChangesAsync(cancellationToken);

        return ToDto(historico);
    }

    /// <summary>
    /// Posicao é derivado em tempo de mapeamento — não é armazenado no banco.
    /// </summary>
    private static HistoricoMtmDiarioDto ToDto(HistoricoMtmDiario h)
    {
        decimal payoff = h.PayoffBrl.Valor;
        return new(
            DataReferencia: h.DataReferencia.ToString("yyyy-MM-dd", null),
            PayoffBrl:      payoff,
            Posicao:        payoff > 0 ? "RECEBER" : payoff < 0 ? "PAGAR" : "NEUTRO",
            SpotUtilizado:  h.SpotUtilizado,
            TipoCotacao:    h.TipoCotacao);
    }
}
