using FluentAssertions;

using NodaTime;

using NSubstitute;

using Sgcf.Application.Calendario;
using Sgcf.Application.Calendario.Queries;
using Sgcf.Domain.Calendario;

using Xunit;

namespace Sgcf.Application.Tests.Calendario;

[Trait("Category", "Domain")]
public sealed class ListFeriadosQueryHandlerTests
{
    private static IClock CriarClock() =>
        Substitute.For<IClock>().With(c => c.GetCurrentInstant().Returns(Instant.FromUtc(2026, 5, 14, 12, 0)));

    private static Feriado CriarFeriado(LocalDate data, string descricao) =>
        Feriado.Criar(data, TipoFeriado.Nacional, EscopoFeriado.Brasil,
            descricao, FonteFeriado.Anbima, data.Year, CriarClock());

    [Fact]
    public async Task Handle_FiltroPorAno_RetornaFeriadosMapeados()
    {
        IFeriadoRepository repo = Substitute.For<IFeriadoRepository>();
        Feriado natal = CriarFeriado(new LocalDate(2026, 12, 25), "Natal");
        Feriado confraternizacao = CriarFeriado(new LocalDate(2026, 1, 1), "Confraternização Universal");

        repo.ListByYearAsync(2026, null, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Feriado>>([confraternizacao, natal]));

        ListFeriadosQueryHandler handler = new(repo);
        IReadOnlyList<FeriadoDto> result = await handler.Handle(
            new ListFeriadosQuery(2026), CancellationToken.None);

        result.Should().HaveCount(2);
        result.Should().Contain(f => f.Descricao == "Natal"
            && f.Data == new DateOnly(2026, 12, 25)
            && f.Tipo == "Nacional"
            && f.Escopo == "Brasil"
            && f.Fonte == "Anbima"
            && f.AnoReferencia == 2026);
    }

    [Fact]
    public async Task Handle_ComEscopo_PassaEscopoAoRepositorio()
    {
        IFeriadoRepository repo = Substitute.For<IFeriadoRepository>();
        repo.ListByYearAsync(Arg.Any<int>(), Arg.Any<EscopoFeriado?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Feriado>>([]));

        ListFeriadosQueryHandler handler = new(repo);
        _ = await handler.Handle(
            new ListFeriadosQuery(2026, EscopoFeriado.Brasil), CancellationToken.None);

        await repo.Received(1).ListByYearAsync(
            2026, EscopoFeriado.Brasil, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_RepositorioVazio_RetornaListaVazia()
    {
        IFeriadoRepository repo = Substitute.For<IFeriadoRepository>();
        repo.ListByYearAsync(Arg.Any<int>(), Arg.Any<EscopoFeriado?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Feriado>>([]));

        ListFeriadosQueryHandler handler = new(repo);
        IReadOnlyList<FeriadoDto> result = await handler.Handle(
            new ListFeriadosQuery(1999), CancellationToken.None);

        result.Should().BeEmpty();
    }
}

file static class TestExtensions
{
    public static T With<T>(this T target, Action<T> setup)
    {
        setup(target);
        return target;
    }
}
