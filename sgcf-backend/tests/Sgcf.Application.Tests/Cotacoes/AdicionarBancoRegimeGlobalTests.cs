using FluentAssertions;
using NodaTime;
using NSubstitute;
using Sgcf.Application.Bancos;
using Sgcf.Application.Cotacoes;
using Sgcf.Application.Cotacoes.Commands;
using Sgcf.Application.Tenancy;
using Sgcf.Domain.Bancos;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;
using Xunit;

namespace Sgcf.Application.Tests.Cotacoes;

/// <summary>
/// Testes da ramificação por regime em <see cref="AdicionarBancoNaCotacaoCommandHandler"/>.
/// Cobre GlobalPuro (Cenário A) e regressão PerModalidade. SPEC_REGIME_LIMITE_EXPLICITO §4.3.
/// </summary>
[Trait("Category", "Unit")]
public sealed class AdicionarBancoRegimeGlobalTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid BancoId = Guid.NewGuid();
    private static readonly LocalDate DataInicio = new(2026, 1, 1);

    private static IClock CriarClock()
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(Instant.FromUtc(2026, 5, 23, 13, 0));
        return clock;
    }

    private static LimiteGlobalBanco CriarLimiteGlobal(decimal valorBrl)
    {
        return LimiteGlobalBanco.Criar(
            BancoId,
            new Money(valorBrl, Moeda.Brl),
            DataInicio,
            CriarClock());
    }

    private static AdicionarBancoNaCotacaoCommandHandler CriarHandler(
        ICotacaoRepository cotacaoRepo,
        ILimiteBancoRepository limiteRepo,
        ILimiteGlobalBancoRepository limiteGlobalRepo,
        IConsultaSaldoBanco saldo,
        bool isPerModalidade)
    {
        saldo.BancoEmRegimePerModalityAsync(BancoId, TenantId, Arg.Any<CancellationToken>())
             .Returns(isPerModalidade);

        // Banco real (carregado para o Apelido nas mensagens).
        var banco = Banco.Criar("341", "Itaú Unibanco S.A.", "Itaú", CriarClock());
        var bancoRepo = Substitute.For<IBancoRepository>();
        bancoRepo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(banco);

        ITenantContext tenant = Substitute.For<ITenantContext>();
        tenant.TenantId.Returns(TenantId);

        return new AdicionarBancoNaCotacaoCommandHandler(
            cotacaoRepo, limiteRepo, limiteGlobalRepo, saldo, bancoRepo, tenant, CriarClock());
    }

    // ── GlobalPuro ────────────────────────────────────────────────────────────

    [Fact]
    public async Task RegimeGlobal_ComGlobalSuficiente_PermiteAdicionar()
    {
        Cotacao cotacao = TestHelpers.CriarCotacaoRascunho(valorAlvoBrl: 500_000m);
        LimiteGlobalBanco limiteGlobal = CriarLimiteGlobal(1_000_000m);

        var cotacaoRepo = Substitute.For<ICotacaoRepository>();
        var limiteRepo = Substitute.For<ILimiteBancoRepository>();
        var limiteGlobalRepo = Substitute.For<ILimiteGlobalBancoRepository>();
        var saldo = Substitute.For<IConsultaSaldoBanco>();

        cotacaoRepo.GetByIdAsync(cotacao.Id, Arg.Any<CancellationToken>()).Returns(cotacao);
        limiteGlobalRepo.GetVigenteByBancoAsync(BancoId, Arg.Any<LocalDate>(), Arg.Any<CancellationToken>())
                        .Returns(limiteGlobal);
        saldo.CalcularSaldoDevedorBancoAsync(BancoId, TenantId, Arg.Any<CancellationToken>())
             .Returns(new Money(200_000m, Moeda.Brl));

        var handler = CriarHandler(cotacaoRepo, limiteRepo, limiteGlobalRepo, saldo, isPerModalidade: false);
        var cmd = new AdicionarBancoNaCotacaoCommand(cotacao.Id, BancoId);

        AdicionarBancoNaCotacaoResponse resultado = await handler.Handle(cmd, default);

        cotacao.BancosAlvo.Should().Contain(BancoId);
        await cotacaoRepo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        resultado.Proposta.Should().BeNull("GlobalPuro não tem LimiteBanco por modalidade");
        resultado.Alertas.Should().BeEmpty();
    }

    [Fact]
    public async Task RegimeGlobal_ComGlobalInsuficiente_Bloqueia()
    {
        Cotacao cotacao = TestHelpers.CriarCotacaoRascunho(valorAlvoBrl: 500_000m);
        LimiteGlobalBanco limiteGlobal = CriarLimiteGlobal(1_000_000m);

        var cotacaoRepo = Substitute.For<ICotacaoRepository>();
        var limiteRepo = Substitute.For<ILimiteBancoRepository>();
        var limiteGlobalRepo = Substitute.For<ILimiteGlobalBancoRepository>();
        var saldo = Substitute.For<IConsultaSaldoBanco>();

        cotacaoRepo.GetByIdAsync(cotacao.Id, Arg.Any<CancellationToken>()).Returns(cotacao);
        limiteGlobalRepo.GetVigenteByBancoAsync(BancoId, Arg.Any<LocalDate>(), Arg.Any<CancellationToken>())
                        .Returns(limiteGlobal);
        saldo.CalcularSaldoDevedorBancoAsync(BancoId, TenantId, Arg.Any<CancellationToken>())
             .Returns(new Money(600_000m, Moeda.Brl)); // disponível 400k < 500k

        var handler = CriarHandler(cotacaoRepo, limiteRepo, limiteGlobalRepo, saldo, isPerModalidade: false);
        var cmd = new AdicionarBancoNaCotacaoCommand(cotacao.Id, BancoId);

        Func<Task> act = () => handler.Handle(cmd, default);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*limite global disponível suficiente*");

        cotacao.BancosAlvo.Should().NotContain(BancoId);
        await cotacaoRepo.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegimeGlobal_SemLimiteGlobalVigente_Bloqueia()
    {
        Cotacao cotacao = TestHelpers.CriarCotacaoRascunho(valorAlvoBrl: 500_000m);

        var cotacaoRepo = Substitute.For<ICotacaoRepository>();
        var limiteRepo = Substitute.For<ILimiteBancoRepository>();
        var limiteGlobalRepo = Substitute.For<ILimiteGlobalBancoRepository>();
        var saldo = Substitute.For<IConsultaSaldoBanco>();

        cotacaoRepo.GetByIdAsync(cotacao.Id, Arg.Any<CancellationToken>()).Returns(cotacao);
        limiteGlobalRepo.GetVigenteByBancoAsync(BancoId, Arg.Any<LocalDate>(), Arg.Any<CancellationToken>())
                        .Returns((LimiteGlobalBanco?)null);

        var handler = CriarHandler(cotacaoRepo, limiteRepo, limiteGlobalRepo, saldo, isPerModalidade: false);
        var cmd = new AdicionarBancoNaCotacaoCommand(cotacao.Id, BancoId);

        Func<Task> act = () => handler.Handle(cmd, default);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*[REG-03]*");

        cotacao.BancosAlvo.Should().NotContain(BancoId);
        await cotacaoRepo.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ── PerModalidade (regressão) ───────────────────────────────────────────────

    [Fact]
    public async Task RegimePerModalidade_SemLimiteBanco_ContinuaBloqueando()
    {
        Cotacao cotacao = TestHelpers.CriarCotacaoRascunho(valorAlvoBrl: 500_000m);

        var cotacaoRepo = Substitute.For<ICotacaoRepository>();
        var limiteRepo = Substitute.For<ILimiteBancoRepository>();
        var limiteGlobalRepo = Substitute.For<ILimiteGlobalBancoRepository>();
        var saldo = Substitute.For<IConsultaSaldoBanco>();

        cotacaoRepo.GetByIdAsync(cotacao.Id, Arg.Any<CancellationToken>()).Returns(cotacao);
        limiteRepo.GetByBancoModalidadeAsync(BancoId, ModalidadeContrato.Finimp, Arg.Any<CancellationToken>())
                  .Returns((LimiteBanco?)null);

        var handler = CriarHandler(cotacaoRepo, limiteRepo, limiteGlobalRepo, saldo, isPerModalidade: true);
        var cmd = new AdicionarBancoNaCotacaoCommand(cotacao.Id, BancoId);

        Func<Task> act = () => handler.Handle(cmd, default);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*não possui limite cadastrado*");

        cotacao.BancosAlvo.Should().NotContain(BancoId);
        await cotacaoRepo.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
