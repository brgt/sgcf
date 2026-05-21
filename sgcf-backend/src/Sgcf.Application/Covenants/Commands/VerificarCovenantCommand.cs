using MediatR;
using NodaTime;
using NodaTime.Text;
using Sgcf.Domain.Covenants;

namespace Sgcf.Application.Covenants.Commands;

public sealed record VerificarCovenantCommand(
    Guid Id,
    string DataVerificacao,
    StatusCovenant NovoStatus,
    string? ProximaVerificacaoEm,
    decimal? ValorApurado,
    string? Observacao) : IRequest<CovenantDto>;

public sealed class VerificarCovenantCommandHandler(
    ICovenantRepository repository,
    IClock clock)
    : IRequestHandler<VerificarCovenantCommand, CovenantDto>
{
    private static readonly LocalDatePattern DatePattern =
        LocalDatePattern.CreateWithInvariantCulture("yyyy-MM-dd");

    public async Task<CovenantDto> Handle(VerificarCovenantCommand command, CancellationToken cancellationToken)
    {
        Covenant? covenant = await repository.GetAsync(command.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Covenant {command.Id} não encontrado.");

        ParseResult<LocalDate> dateResult = DatePattern.Parse(command.DataVerificacao);
        if (!dateResult.Success)
        {
            throw new ArgumentException(
                $"DataVerificacao '{command.DataVerificacao}' inválida. Use formato yyyy-MM-dd.");
        }

        LocalDate? proxima = null;
        if (!string.IsNullOrWhiteSpace(command.ProximaVerificacaoEm))
        {
            ParseResult<LocalDate> r = DatePattern.Parse(command.ProximaVerificacaoEm);
            if (!r.Success)
            {
                throw new ArgumentException(
                    $"ProximaVerificacaoEm '{command.ProximaVerificacaoEm}' inválida. Use formato yyyy-MM-dd.");
            }

            proxima = r.Value;
        }

        Instant agora = clock.GetCurrentInstant();
        covenant.RegistrarVerificacao(
            command.NovoStatus,
            dateResult.Value,
            proxima,
            command.ValorApurado,
            command.Observacao,
            agora);

        await repository.SaveChangesAsync(cancellationToken);
        return CreateCovenantCommandHandler.ToDto(covenant);
    }
}
