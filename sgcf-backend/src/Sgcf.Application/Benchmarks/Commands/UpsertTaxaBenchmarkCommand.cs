using MediatR;
using NodaTime;
using NodaTime.Text;
using Sgcf.Domain.Benchmarks;

namespace Sgcf.Application.Benchmarks.Commands;

public sealed record UpsertTaxaBenchmarkCommand(
    string Tipo,
    string DataReferencia,
    decimal TaxaAa,
    string Fonte) : IRequest<TaxaBenchmarkDto>;

public sealed class UpsertTaxaBenchmarkCommandHandler(
    ITaxaBenchmarkRepository repository,
    IClock clock)
    : IRequestHandler<UpsertTaxaBenchmarkCommand, TaxaBenchmarkDto>
{
    private static readonly LocalDatePattern DatePattern =
        LocalDatePattern.CreateWithInvariantCulture("yyyy-MM-dd");

    public async Task<TaxaBenchmarkDto> Handle(
        UpsertTaxaBenchmarkCommand command,
        CancellationToken cancellationToken)
    {
        ParseResult<LocalDate> parseResult = DatePattern.Parse(command.DataReferencia);
        if (!parseResult.Success)
        {
            throw new ArgumentException(
                $"DataReferencia '{command.DataReferencia}' inválida. Use formato yyyy-MM-dd.");
        }

        LocalDate data = parseResult.Value;
        Instant agora = clock.GetCurrentInstant();

        TaxaBenchmark? existente = await repository.GetAsync(command.Tipo, data, cancellationToken);

        if (existente is not null)
        {
            existente.Atualizar(command.TaxaAa, command.Fonte, agora);
        }
        else
        {
            TaxaBenchmark nova = TaxaBenchmark.Criar(command.Tipo, data, command.TaxaAa, command.Fonte, agora);
            repository.Add(nova);
        }

        await repository.SaveChangesAsync(cancellationToken);

        return new TaxaBenchmarkDto(command.Tipo, command.DataReferencia, command.TaxaAa, command.Fonte);
    }
}
