using MediatR;
using NodaTime;
using NodaTime.Text;
using Sgcf.Domain.Covenants;

namespace Sgcf.Application.Covenants.Commands;

public sealed record CreateCovenantCommand(
    Guid ContratoId,
    string Descricao,
    TipoCovenant Tipo,
    int PeriodicidadeVerificacaoMeses,
    string? ProximaVerificacaoEm,
    decimal? LimiteNumerico) : IRequest<CovenantDto>;

public sealed class CreateCovenantCommandHandler(
    ICovenantRepository repository,
    IClock clock)
    : IRequestHandler<CreateCovenantCommand, CovenantDto>
{
    private static readonly LocalDatePattern DatePattern =
        LocalDatePattern.CreateWithInvariantCulture("yyyy-MM-dd");

    public async Task<CovenantDto> Handle(CreateCovenantCommand command, CancellationToken cancellationToken)
    {
        Instant agora = clock.GetCurrentInstant();

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

        Covenant covenant = Covenant.Criar(
            command.ContratoId,
            command.Descricao,
            command.Tipo,
            command.PeriodicidadeVerificacaoMeses,
            proxima,
            command.LimiteNumerico,
            agora);

        repository.Add(covenant);
        await repository.SaveChangesAsync(cancellationToken);

        return ToDto(covenant);
    }

    internal static CovenantDto ToDto(Covenant c) =>
        new(c.Id,
            c.ContratoId,
            c.Descricao,
            c.Tipo,
            c.Status,
            c.PeriodicidadeVerificacaoMeses,
            c.ProximaVerificacaoEm?.ToString("yyyy-MM-dd", null),
            c.UltimaVerificacaoEm?.ToString("yyyy-MM-dd", null),
            c.ObservacaoVerificacao,
            c.LimiteNumerico,
            c.ValorApurado);
}
