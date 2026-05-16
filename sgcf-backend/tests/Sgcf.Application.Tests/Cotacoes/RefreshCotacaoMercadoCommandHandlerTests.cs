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
    public async Task Handle_PtaxDisponivel_AtualizaSnapshotERetornaCotacaoDto()
    {
        // Arrange
        Cotacao cotacao = TestHelpers.CriarCotacaoEmCaptacao();
        CotacaoFx novaFx = TestHelpers.CriarCotacaoFxUsd(venda: 5.50m); // PTAX atualizada

        ICotacaoRepository repo = Substitute.For<ICotacaoRepository>();
        ICotacaoFxRepository fxRepo = Substitute.For<ICotacaoFxRepository>();
        IClock clock = TestHelpers.CriarClock();

        repo.GetByIdWithPropostasAsync(cotacao.Id, default).Returns(cotacao);
        fxRepo.GetMaisRecenteAsync(Moeda.Usd, TipoCotacao.PtaxD1, Arg.Any<LocalDate>(), default)
            .Returns(novaFx);

        RefreshCotacaoMercadoCommandHandler handler = new(repo, fxRepo, clock);
        RefreshCotacaoMercadoCommand cmd = new(cotacao.Id);

        // Act
        CotacaoDto resultado = await handler.Handle(cmd, default);

        // Assert
        resultado.Should().NotBeNull();
        await repo.Received(1).SaveChangesAsync(default);
        await fxRepo.Received(1).GetMaisRecenteAsync(Moeda.Usd, TipoCotacao.PtaxD1, Arg.Any<LocalDate>(), default);
    }

    [Fact]
    public async Task Handle_PtaxIndisponivel_LancaInvalidOperationException()
    {
        // Arrange — PTAX não cadastrada para o dia de hoje
        Cotacao cotacao = TestHelpers.CriarCotacaoEmCaptacao();

        ICotacaoRepository repo = Substitute.For<ICotacaoRepository>();
        ICotacaoFxRepository fxRepo = Substitute.For<ICotacaoFxRepository>();
        IClock clock = TestHelpers.CriarClock();

        repo.GetByIdWithPropostasAsync(cotacao.Id, default).Returns(cotacao);
        fxRepo.GetMaisRecenteAsync(Moeda.Usd, TipoCotacao.PtaxD1, Arg.Any<LocalDate>(), default)
            .Returns((CotacaoFx?)null);

        RefreshCotacaoMercadoCommandHandler handler = new(repo, fxRepo, clock);
        RefreshCotacaoMercadoCommand cmd = new(cotacao.Id);

        Func<Task> act = () => handler.Handle(cmd, default);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*PTAX*");
    }

    [Fact]
    public async Task Handle_CotacaoNaoEncontrada_LancaKeyNotFoundException()
    {
        ICotacaoRepository repo = Substitute.For<ICotacaoRepository>();
        ICotacaoFxRepository fxRepo = Substitute.For<ICotacaoFxRepository>();
        IClock clock = TestHelpers.CriarClock();

        repo.GetByIdWithPropostasAsync(Arg.Any<Guid>(), default).Returns((Cotacao?)null);

        RefreshCotacaoMercadoCommandHandler handler = new(repo, fxRepo, clock);
        RefreshCotacaoMercadoCommand cmd = new(Guid.NewGuid());

        Func<Task> act = () => handler.Handle(cmd, default);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
