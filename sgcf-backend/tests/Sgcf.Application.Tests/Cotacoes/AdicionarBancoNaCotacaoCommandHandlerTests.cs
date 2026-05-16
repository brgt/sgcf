using FluentAssertions;
using MediatR;
using NSubstitute;
using Sgcf.Application.Cotacoes;
using Sgcf.Application.Cotacoes.Commands;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;
using Xunit;

namespace Sgcf.Application.Tests.Cotacoes;

[Trait("Category", "Unit")]
public sealed class AdicionarBancoNaCotacaoCommandHandlerTests
{
    [Fact]
    public async Task Handle_ComLimiteSuficiente_AdicionaBanco()
    {
        // Arrange
        Guid bancoId = Guid.NewGuid();
        Cotacao cotacao = TestHelpers.CriarCotacaoRascunho(valorAlvoBrl: 500_000m);
        LimiteBanco limite = TestHelpers.CriarLimiteBanco(bancoId, valorLimiteBrl: 1_000_000m);

        ICotacaoRepository cotacaoRepo = Substitute.For<ICotacaoRepository>();
        ILimiteBancoRepository limiteRepo = Substitute.For<ILimiteBancoRepository>();

        cotacaoRepo.GetByIdAsync(cotacao.Id, default).Returns(cotacao);
        limiteRepo.GetByBancoModalidadeAsync(bancoId, ModalidadeContrato.Finimp, default).Returns(limite);

        AdicionarBancoNaCotacaoCommandHandler handler = new(cotacaoRepo, limiteRepo);
        AdicionarBancoNaCotacaoCommand cmd = new(cotacao.Id, bancoId);

        // Act
        Unit resultado = await handler.Handle(cmd, default);

        // Assert
        cotacao.BancosAlvo.Should().Contain(bancoId);
        await cotacaoRepo.Received(1).SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_SemLimiteCadastrado_LancaInvalidOperationException()
    {
        Guid bancoId = Guid.NewGuid();
        Cotacao cotacao = TestHelpers.CriarCotacaoRascunho();

        ICotacaoRepository cotacaoRepo = Substitute.For<ICotacaoRepository>();
        ILimiteBancoRepository limiteRepo = Substitute.For<ILimiteBancoRepository>();

        cotacaoRepo.GetByIdAsync(cotacao.Id, default).Returns(cotacao);
        limiteRepo.GetByBancoModalidadeAsync(bancoId, ModalidadeContrato.Finimp, default)
            .Returns((LimiteBanco?)null);

        AdicionarBancoNaCotacaoCommandHandler handler = new(cotacaoRepo, limiteRepo);
        AdicionarBancoNaCotacaoCommand cmd = new(cotacao.Id, bancoId);

        Func<Task> act = () => handler.Handle(cmd, default);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*não possui limite cadastrado*");
    }

    [Fact]
    public async Task Handle_ComLimiteInsuficiente_LancaInvalidOperationException()
    {
        Guid bancoId = Guid.NewGuid();
        // ValorAlvo = 500k, Limite = 300k → insuficiente
        Cotacao cotacao = TestHelpers.CriarCotacaoRascunho(valorAlvoBrl: 500_000m);
        LimiteBanco limite = TestHelpers.CriarLimiteBanco(bancoId, valorLimiteBrl: 300_000m);

        ICotacaoRepository cotacaoRepo = Substitute.For<ICotacaoRepository>();
        ILimiteBancoRepository limiteRepo = Substitute.For<ILimiteBancoRepository>();

        cotacaoRepo.GetByIdAsync(cotacao.Id, default).Returns(cotacao);
        limiteRepo.GetByBancoModalidadeAsync(bancoId, ModalidadeContrato.Finimp, default).Returns(limite);

        AdicionarBancoNaCotacaoCommandHandler handler = new(cotacaoRepo, limiteRepo);
        AdicionarBancoNaCotacaoCommand cmd = new(cotacao.Id, bancoId);

        Func<Task> act = () => handler.Handle(cmd, default);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*limite disponível suficiente*");
    }

    [Fact]
    public async Task Handle_CotacaoNaoEncontrada_LancaKeyNotFoundException()
    {
        ICotacaoRepository cotacaoRepo = Substitute.For<ICotacaoRepository>();
        ILimiteBancoRepository limiteRepo = Substitute.For<ILimiteBancoRepository>();

        cotacaoRepo.GetByIdAsync(Arg.Any<Guid>(), default).Returns((Cotacao?)null);

        AdicionarBancoNaCotacaoCommandHandler handler = new(cotacaoRepo, limiteRepo);
        AdicionarBancoNaCotacaoCommand cmd = new(Guid.NewGuid(), Guid.NewGuid());

        Func<Task> act = () => handler.Handle(cmd, default);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
