using MediatR;
using NodaTime;
using NodaTime.Text;
using Sgcf.Domain.Covenants;

namespace Sgcf.Application.Covenants.Commands;

public sealed record UpdateCovenantCommand(
    Guid Id,
    string Descricao,
    int PeriodicidadeVerificacaoMeses,
    string? ProximaVerificacaoEm,
    decimal? LimiteNumerico) : IRequest<CovenantDto>;

public sealed class UpdateCovenantCommandHandler(
    ICovenantRepository repository,
    IClock clock)
    : IRequestHandler<UpdateCovenantCommand, CovenantDto>
{
    private static readonly LocalDatePattern DatePattern =
        LocalDatePattern.CreateWithInvariantCulture("yyyy-MM-dd");

    public async Task<CovenantDto> Handle(UpdateCovenantCommand command, CancellationToken cancellationToken)
    {
        Covenant? covenant = await repository.GetAsync(command.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Covenant {command.Id} não encontrado.");

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
        covenant.Atualizar(command.Descricao, command.PeriodicidadeVerificacaoMeses, proxima, command.LimiteNumerico, agora);

        await repository.SaveChangesAsync(cancellationToken);
        return CreateCovenantCommandHandler.ToDto(covenant);
    }
}
