using FluentAssertions;
using NSubstitute;
using Sgcf.Application.Common;
using Sgcf.Application.Cotacoes;
using Sgcf.Application.Cotacoes.Queries;
using Sgcf.Domain.Cotacoes;
using Xunit;

namespace Sgcf.Application.Tests.Cotacoes;

[Trait("Category", "Unit")]
public sealed class ListCotacoesQueryHandlerTests
{
    [Fact]
    public async Task Handle_SemFiltros_RetornaPagedResultComTodosItens()
    {
        // Arrange
        Cotacao c1 = TestHelpers.CriarCotacaoRascunho();
        Cotacao c2 = TestHelpers.CriarCotacaoRascunho();
        IReadOnlyList<Cotacao> items = [c1, c2];

        ICotacaoRepository repo = Substitute.For<ICotacaoRepository>();
        repo.ListPagedAsync(Arg.Any<CotacaoFilter>(), Arg.Any<int>(), Arg.Any<int>(), default)
            .Returns((items, 2));

        ListCotacoesQueryHandler handler = new(repo);
        ListCotacoesQuery query = new(Page: 1, PageSize: 20);

        // Act
        PagedResult<CotacaoDto> resultado = await handler.Handle(query, default);

        // Assert
        resultado.Items.Should().HaveCount(2);
        resultado.Total.Should().Be(2);
        resultado.Page.Should().Be(1);
        resultado.PageSize.Should().Be(20);
    }

    [Fact]
    public async Task Handle_ListaVazia_RetornaPagedResultVazio()
    {
        // Arrange
        ICotacaoRepository repo = Substitute.For<ICotacaoRepository>();
        repo.ListPagedAsync(Arg.Any<CotacaoFilter>(), Arg.Any<int>(), Arg.Any<int>(), default)
            .Returns((Array.Empty<Cotacao>() as IReadOnlyList<Cotacao>, 0));

        ListCotacoesQueryHandler handler = new(repo);
        ListCotacoesQuery query = new(Page: 1, PageSize: 20);

        // Act
        PagedResult<CotacaoDto> resultado = await handler.Handle(query, default);

        // Assert
        resultado.Items.Should().BeEmpty();
        resultado.Total.Should().Be(0);
    }

    [Fact]
    public async Task Handle_FiltroStatusValido_PassaFilterCorretoParaRepositorio()
    {
        // Arrange
        ICotacaoRepository repo = Substitute.For<ICotacaoRepository>();
        repo.ListPagedAsync(Arg.Any<CotacaoFilter>(), Arg.Any<int>(), Arg.Any<int>(), default)
            .Returns((Array.Empty<Cotacao>() as IReadOnlyList<Cotacao>, 0));

        ListCotacoesQueryHandler handler = new(repo);
        ListCotacoesQuery query = new(Page: 1, PageSize: 10, Status: "Rascunho");

        // Act
        await handler.Handle(query, default);

        // Assert — filter com status deve ter sido passado ao repositório
        await repo.Received(1).ListPagedAsync(
            Arg.Is<CotacaoFilter>(f => f.Status == StatusCotacao.Rascunho),
            1, 10, default);
    }

    [Fact]
    public async Task Handle_FiltroStatusInvalido_PassaNullParaRepositorio()
    {
        // Arrange — status inválido é ignorado silenciosamente (null)
        ICotacaoRepository repo = Substitute.For<ICotacaoRepository>();
        repo.ListPagedAsync(Arg.Any<CotacaoFilter>(), Arg.Any<int>(), Arg.Any<int>(), default)
            .Returns((Array.Empty<Cotacao>() as IReadOnlyList<Cotacao>, 0));

        ListCotacoesQueryHandler handler = new(repo);
        ListCotacoesQuery query = new(Page: 1, PageSize: 10, Status: "StatusInexistente");

        await handler.Handle(query, default);

        await repo.Received(1).ListPagedAsync(
            Arg.Is<CotacaoFilter>(f => f.Status == null),
            1, 10, default);
    }

    [Fact]
    public async Task Handle_PageSizeAcimaDoMaximo_ClampaPara100()
    {
        // Arrange — pageSize=500 deve ser clamped para 100
        ICotacaoRepository repo = Substitute.For<ICotacaoRepository>();
        repo.ListPagedAsync(Arg.Any<CotacaoFilter>(), Arg.Any<int>(), Arg.Any<int>(), default)
            .Returns((Array.Empty<Cotacao>() as IReadOnlyList<Cotacao>, 0));

        ListCotacoesQueryHandler handler = new(repo);
        ListCotacoesQuery query = new(Page: 1, PageSize: 500);

        await handler.Handle(query, default);

        await repo.Received(1).ListPagedAsync(
            Arg.Any<CotacaoFilter>(),
            1, 100, default);
    }
}
