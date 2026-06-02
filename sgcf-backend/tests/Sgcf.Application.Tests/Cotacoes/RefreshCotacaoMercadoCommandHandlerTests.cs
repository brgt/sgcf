using FluentAssertions;
using NodaTime;
using NSubstitute;
using Sgcf.Application.Cambio;
using Sgcf.Application.Cotacoes;
using Sgcf.Application.Cotacoes.Commands;
using Sgcf.Domain.Cambio;
using Sgcf.Domain.Common;
using Sgcf.Domain.Cotacoes;
using Xunit;

namespace Sgcf.Application.Tests.Cotacoes;

[Trait("Category", "Unit")]
public sealed class RefreshCotacaoMercadoCommandHandlerTests
{
    [Fact]
    public async Task Handle_ComSpotIntraday_UsaSpotEAtualizaSnapshot()
    {
        // Arrange — spot intraday disponível: deve preferir o spot e não consultar PtaxD0.
        Cotacao cotacao = TestHelpers.CriarCotacaoEmCaptacao();

        ICotacaoRepository repo = Substitute.For<ICotacaoRepository>();
        ICotacaoSpotCache spotCache = Substitute.For<ICotacaoSpotCache>();
        IResolveTipoCotacaoService resolver = Substitute.For<IResolveTipoCotacaoService>();
        IClock clock = TestHelpers.CriarClock();

        repo.GetByIdWithPropostasAsync(cotacao.Id, default).Returns(cotacao);
        spotCache.GetSpotAsync(Moeda.Usd, default).Returns(new Money(5.55m, Moeda.Brl));

        RefreshCotacaoMercadoCommandHandler handler = new(repo, spotCache, resolver, clock);

        // Act
        CotacaoDto resultado = await handler.Handle(new(cotacao.Id), default);

        // Assert
        resultado.Should().NotBeNull();
        await repo.Received(1).SaveChangesAsync(default);
        await resolver.DidNotReceiveWithAnyArgs().ResolverFxAsync(default, default, default, default);
    }

    [Fact]
    public async Task Handle_SemSpot_UsaFechamentoPtaxD0EAtualizaSnapshot()
    {
        // Arrange — sem spot: cai para o fechamento PtaxD0 do dia corrente.
        Cotacao cotacao = TestHelpers.CriarCotacaoEmCaptacao();
        CotacaoFx d0 = TestHelpers.CriarCotacaoFxUsd(venda: 5.50m);

        ICotacaoRepository repo = Substitute.For<ICotacaoRepository>();
        ICotacaoSpotCache spotCache = Substitute.For<ICotacaoSpotCache>();
        IResolveTipoCotacaoService resolver = Substitute.For<IResolveTipoCotacaoService>();
        IClock clock = TestHelpers.CriarClock();

        repo.GetByIdWithPropostasAsync(cotacao.Id, default).Returns(cotacao);
        spotCache.GetSpotAsync(Moeda.Usd, default).Returns((Money?)null);
        resolver.ResolverFxAsync(Moeda.Usd, TipoCotacao.PtaxD0, Arg.Any<LocalDate>(), default)
            .Returns(d0);

        RefreshCotacaoMercadoCommandHandler handler = new(repo, spotCache, resolver, clock);

        // Act
        CotacaoDto resultado = await handler.Handle(new(cotacao.Id), default);

        // Assert
        resultado.Should().NotBeNull();
        await repo.Received(1).SaveChangesAsync(default);
        await resolver.Received(1).ResolverFxAsync(Moeda.Usd, TipoCotacao.PtaxD0, Arg.Any<LocalDate>(), default);
    }

    [Fact]
    public async Task Handle_SemSpotNemPtaxD0_LancaInvalidOperationException()
    {
        // Arrange — nem spot nem fechamento PtaxD0 do dia: erro claro.
        Cotacao cotacao = TestHelpers.CriarCotacaoEmCaptacao();

        ICotacaoRepository repo = Substitute.For<ICotacaoRepository>();
        ICotacaoSpotCache spotCache = Substitute.For<ICotacaoSpotCache>();
        IResolveTipoCotacaoService resolver = Substitute.For<IResolveTipoCotacaoService>();
        IClock clock = TestHelpers.CriarClock();

        repo.GetByIdWithPropostasAsync(cotacao.Id, default).Returns(cotacao);
        spotCache.GetSpotAsync(Moeda.Usd, default).Returns((Money?)null);
        resolver.ResolverFxAsync(Moeda.Usd, TipoCotacao.PtaxD0, Arg.Any<LocalDate>(), default)
            .Returns((CotacaoFx?)null);

        RefreshCotacaoMercadoCommandHandler handler = new(repo, spotCache, resolver, clock);

        Func<Task> act = () => handler.Handle(new(cotacao.Id), default);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*USD/BRL*");
    }

    [Fact]
    public async Task Handle_CotacaoNaoEncontrada_LancaKeyNotFoundException()
    {
        ICotacaoRepository repo = Substitute.For<ICotacaoRepository>();
        ICotacaoSpotCache spotCache = Substitute.For<ICotacaoSpotCache>();
        IResolveTipoCotacaoService resolver = Substitute.For<IResolveTipoCotacaoService>();
        IClock clock = TestHelpers.CriarClock();

        repo.GetByIdWithPropostasAsync(Arg.Any<Guid>(), default).Returns((Cotacao?)null);

        RefreshCotacaoMercadoCommandHandler handler = new(repo, spotCache, resolver, clock);

        Func<Task> act = () => handler.Handle(new(Guid.NewGuid()), default);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
