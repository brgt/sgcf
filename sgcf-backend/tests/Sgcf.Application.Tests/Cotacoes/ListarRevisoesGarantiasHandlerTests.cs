using FluentAssertions;
using NodaTime;
using NSubstitute;
using Sgcf.Application.Cotacoes;
using Sgcf.Application.Cotacoes.Queries;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;
using Xunit;

namespace Sgcf.Application.Tests.Cotacoes;

/// <summary>
/// Testes unitários do handler <see cref="ListarRevisoesGarantiasQueryHandler"/>.
/// Usa NSubstitute para o repositório — sem banco de dados.
/// SPEC §5.1, SLB-05.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ListarRevisoesGarantiasHandlerTests
{
    private static readonly Guid LimiteId = Guid.NewGuid();
    private static readonly Guid BancoId = Guid.NewGuid();

    private static IClock CriarClock(int hora = 10) =>
        CriarClockComInstant(Instant.FromUtc(2026, 5, 25, hora, 0));

    private static IClock CriarClockComInstant(Instant instante)
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(instante);
        return clock;
    }

    /// <summary>
    /// Cria um LimiteBanco com garantias e retorna a revisão vigente para ser
    /// usada nos mocks do repositório.
    /// </summary>
    private static (LimiteBanco Limite, GarantiaExigidaRevisao Revisao) CriarLimiteComRevisao(
        IClock clock,
        params GarantiaExigidaItemSpec[] specs)
    {
        LimiteBanco limite = LimiteBanco.Criar(
            bancoId: BancoId,
            modalidade: ModalidadeContrato.Finimp,
            valorLimiteBrl: new Money(1_000_000m, Moeda.Brl),
            dataVigenciaInicio: new LocalDate(2026, 1, 1),
            clock: clock,
            garantiasExigidas: specs.Length > 0 ? specs : null);

        GarantiaExigidaRevisao? revisao = limite.RevisaoGarantiasVigente;
        return (limite, revisao!);
    }

    // ── Cenário 1: limite sem revisões retorna lista vazia ────────────────────

    [Fact]
    public async Task Handle_LimiteSemRevisoes_RetornaListaVazia()
    {
        // Arrange
        IClock clock = CriarClock();
        LimiteBanco limite = LimiteBanco.Criar(
            bancoId: BancoId,
            modalidade: ModalidadeContrato.Finimp,
            valorLimiteBrl: new Money(1_000_000m, Moeda.Brl),
            dataVigenciaInicio: new LocalDate(2026, 1, 1),
            clock: clock);

        ILimiteBancoRepository repo = Substitute.For<ILimiteBancoRepository>();
        repo.GetByIdAsync(LimiteId, default).Returns(limite);
        repo.GetRevisoesGarantiasAsync(LimiteId, default)
            .Returns(Array.Empty<GarantiaExigidaRevisao>() as IReadOnlyList<GarantiaExigidaRevisao>);

        var handler = new ListarRevisoesGarantiasQueryHandler(repo);

        // Act
        ListarRevisoesGarantiasResponse resultado =
            await handler.Handle(new ListarRevisoesGarantiasQuery(LimiteId), default);

        // Assert
        resultado.LimiteBancoId.Should().Be(LimiteId);
        resultado.Revisoes.Should().BeEmpty();
    }

    // ── Cenário 2: limite com 1 revisão vigente ───────────────────────────────

    [Fact]
    public async Task Handle_LimiteComUmaRevisaoVigente_RetornaUmItemComVigenciaFimNull()
    {
        // Arrange
        IClock clock = CriarClock();
        var (limite, revisao) = CriarLimiteComRevisao(clock,
            new GarantiaExigidaItemSpec(TipoGarantia.CdbCativo, 20m, null, true, null));

        ILimiteBancoRepository repo = Substitute.For<ILimiteBancoRepository>();
        repo.GetByIdAsync(LimiteId, default).Returns(limite);
        repo.GetRevisoesGarantiasAsync(LimiteId, default)
            .Returns(new[] { revisao } as IReadOnlyList<GarantiaExigidaRevisao>);

        var handler = new ListarRevisoesGarantiasQueryHandler(repo);

        // Act
        ListarRevisoesGarantiasResponse resultado =
            await handler.Handle(new ListarRevisoesGarantiasQuery(LimiteId), default);

        // Assert
        resultado.Revisoes.Should().HaveCount(1);

        GarantiaExigidaRevisaoDto dto = resultado.Revisoes[0];
        dto.VigenciaFim.Should().BeNull("a revisão vigente não possui VigenciaFim");
        dto.Itens.Should().ContainSingle(i => i.Tipo == "CdbCativo");
    }

    // ── Cenário 3: limite com 3 revisões em sequência — ordem ascendente ──────

    [Fact]
    public async Task Handle_LimiteComTresRevisoes_RetornaEmOrdemAscendente()
    {
        // Arrange
        // Simula 3 instantes distintos para as 3 revisões.
        Instant t1 = Instant.FromUtc(2026, 1, 1, 10, 0);
        Instant t2 = Instant.FromUtc(2026, 3, 1, 10, 0);
        Instant t3 = Instant.FromUtc(2026, 5, 1, 10, 0);

        IClock clock1 = CriarClockComInstant(t1);
        IClock clock2 = CriarClockComInstant(t2);
        IClock clock3 = CriarClockComInstant(t3);

        // Revisão 1 — criada em t1 e encerrada em t2
        GarantiaExigidaRevisao rev1 = GarantiaExigidaRevisao.CriarComInstant(
            limiteBancoId: BancoId,
            itens: [new GarantiaExigidaItemSpec(TipoGarantia.Aval, null, null, true, null)],
            momento: t1);
        rev1.EncerrarVigencia(t2);

        // Revisão 2 — criada em t2 e encerrada em t3
        GarantiaExigidaRevisao rev2 = GarantiaExigidaRevisao.CriarComInstant(
            limiteBancoId: BancoId,
            itens: [new GarantiaExigidaItemSpec(TipoGarantia.CdbCativo, 20m, null, true, null)],
            momento: t2);
        rev2.EncerrarVigencia(t3);

        // Revisão 3 — criada em t3, vigente (sem encerramento)
        GarantiaExigidaRevisao rev3 = GarantiaExigidaRevisao.CriarComInstant(
            limiteBancoId: BancoId,
            itens: [new GarantiaExigidaItemSpec(TipoGarantia.Sblc, 50m, null, true, null)],
            momento: t3);

        // Mock retorna na ordem certa (repositório ordenado por VigenciaInicio ASC)
        IReadOnlyList<GarantiaExigidaRevisao> listaOrdenada = [rev1, rev2, rev3];

        LimiteBanco limite = LimiteBanco.Criar(
            bancoId: BancoId,
            modalidade: ModalidadeContrato.Finimp,
            valorLimiteBrl: new Money(1_000_000m, Moeda.Brl),
            dataVigenciaInicio: new LocalDate(2026, 1, 1),
            clock: clock1);

        ILimiteBancoRepository repo = Substitute.For<ILimiteBancoRepository>();
        repo.GetByIdAsync(LimiteId, default).Returns(limite);
        repo.GetRevisoesGarantiasAsync(LimiteId, default).Returns(listaOrdenada);

        var handler = new ListarRevisoesGarantiasQueryHandler(repo);

        // Act
        ListarRevisoesGarantiasResponse resultado =
            await handler.Handle(new ListarRevisoesGarantiasQuery(LimiteId), default);

        // Assert
        resultado.Revisoes.Should().HaveCount(3);

        // Verifica ordem ascendente
        resultado.Revisoes[0].VigenciaInicio.Should().Be(t1.ToDateTimeOffset());
        resultado.Revisoes[1].VigenciaInicio.Should().Be(t2.ToDateTimeOffset());
        resultado.Revisoes[2].VigenciaInicio.Should().Be(t3.ToDateTimeOffset());

        // Rev1 e rev2 são fechadas; rev3 é vigente
        resultado.Revisoes[0].VigenciaFim.Should().NotBeNull();
        resultado.Revisoes[1].VigenciaFim.Should().NotBeNull();
        resultado.Revisoes[2].VigenciaFim.Should().BeNull("a última revisão está vigente");

        // Itens
        resultado.Revisoes[0].Itens.Should().ContainSingle(i => i.Tipo == "Aval");
        resultado.Revisoes[1].Itens.Should().ContainSingle(i => i.Tipo == "CdbCativo");
        resultado.Revisoes[2].Itens.Should().ContainSingle(i => i.Tipo == "Sblc");
    }

    // ── Cenário 4: LimiteId inexistente lança KeyNotFoundException ────────────

    [Fact]
    public async Task Handle_LimiteIdInexistente_LancaKeyNotFoundException()
    {
        // Arrange
        ILimiteBancoRepository repo = Substitute.For<ILimiteBancoRepository>();
        repo.GetByIdAsync(LimiteId, default).Returns((LimiteBanco?)null);

        var handler = new ListarRevisoesGarantiasQueryHandler(repo);

        // Act
        Func<Task> act = () => handler.Handle(new ListarRevisoesGarantiasQuery(LimiteId), default);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage($"*{LimiteId}*");
    }
}
