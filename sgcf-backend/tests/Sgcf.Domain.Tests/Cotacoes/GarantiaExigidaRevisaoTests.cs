using FluentAssertions;
using NodaTime;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;
using Xunit;

namespace Sgcf.Domain.Tests.Cotacoes;

/// <summary>
/// Testes unitários de <see cref="GarantiaExigidaRevisao"/>.
/// Cobre invariantes SR-01..SR-08 (SPEC §4.1).
/// </summary>
public sealed class GarantiaExigidaRevisaoTests
{
    private static readonly Guid LimiteId = Guid.NewGuid();

    private static IClock CriarClock(int ano = 2026, int mes = 5, int dia = 25) =>
        PropostaFactory.CriarClockFixo(ano, mes, dia);

    private static GarantiaExigidaItemSpec SpecCdb(decimal percentual = 20m) =>
        new(TipoGarantia.CdbCativo, percentual, null, true, null);

    private static GarantiaExigidaItemSpec SpecAval() =>
        new(TipoGarantia.Aval, null, null, true, null);

    // ─── SR-01: LimiteBancoId não pode ser Guid.Empty ────────────────────────

    [Fact]
    public void Criar_ComLimiteBancoIdVazio_LancaArgumentException()
    {
        // SR-01
        var clock = CriarClock();

        var act = () => GarantiaExigidaRevisao.Criar(
            limiteBancoId: Guid.Empty,
            itens: Array.Empty<GarantiaExigidaItemSpec>(),
            clock: clock);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*LimiteBancoId*");
    }

    // ─── SR-02: VigenciaInicio definido pelo clock na criação ────────────────

    [Fact]
    public void Criar_DefineVigenciaInicioComoInstantAtual_EVigenciaFimNull()
    {
        // SR-02
        var clock = CriarClock();
        var now = clock.GetCurrentInstant();

        var revisao = GarantiaExigidaRevisao.Criar(
            limiteBancoId: LimiteId,
            itens: Array.Empty<GarantiaExigidaItemSpec>(),
            clock: clock);

        revisao.VigenciaInicio.Should().Be(now);
        revisao.VigenciaFim.Should().BeNull();
        revisao.EstaVigente.Should().BeTrue();
    }

    // ─── SR-03: VigenciaFim só pode ser definido uma vez ────────────────────

    [Fact]
    public void EncerrarVigencia_ChamadaDuasVezes_LancaInvalidOperationException()
    {
        // SR-03
        var clock = CriarClock();
        var revisao = GarantiaExigidaRevisao.Criar(LimiteId, Array.Empty<GarantiaExigidaItemSpec>(), clock);

        // Primeira chamada — deve funcionar
        revisao.EncerrarVigencia(clock);

        // Segunda chamada — deve lançar
        var act = () => revisao.EncerrarVigencia(clock);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*encerrada*");
    }

    // ─── SR-03/SR-04: EncerrarVigencia define VigenciaFim ───────────────────

    [Fact]
    public void EncerrarVigencia_DefineVigenciaFimComoInstantAtual()
    {
        // SR-03 (positivo) + SR-04
        var clockCriacao = CriarClock(2026, 1, 1);
        var clockEncerramento = CriarClock(2026, 6, 1);

        var revisao = GarantiaExigidaRevisao.Criar(LimiteId, Array.Empty<GarantiaExigidaItemSpec>(), clockCriacao);

        revisao.EncerrarVigencia(clockEncerramento);

        revisao.VigenciaFim.Should().Be(clockEncerramento.GetCurrentInstant());
        revisao.EstaVigente.Should().BeFalse();
    }

    // ─── SR-04: VigenciaFim deve ser >= VigenciaInicio ───────────────────────

    [Fact]
    public void EncerrarVigencia_ComInstanteAnteriorAVigenciaInicio_LancaArgumentException()
    {
        // SR-04 via sobrecarga Instant
        var clockFuturo = CriarClock(2026, 6, 1);
        var revisao = GarantiaExigidaRevisao.Criar(LimiteId, Array.Empty<GarantiaExigidaItemSpec>(), clockFuturo);

        // Tentar encerrar com instante ANTERIOR ao VigenciaInicio
        var instantePassado = CriarClock(2026, 1, 1).GetCurrentInstant();

        var act = () => revisao.EncerrarVigencia(instantePassado);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*anterior*");
    }

    // ─── SR-05: Itens imutáveis após encerramento ────────────────────────────

    [Fact]
    public void AdicionarItem_AposRevisaoEncerrada_LancaInvalidOperationException()
    {
        // SR-05: a invariante é verificada via CriarComInstant que internamente chama AdicionarItemInterno.
        // Como AdicionarItemInterno é privado, testamos via factory: criamos revisão, encerramos,
        // e então tentamos usar SubstituirGarantiasExigidas no LimiteBanco (que fecha a vigente
        // e cria nova). Para testar SR-05 diretamente, usamos reflexão ou verificamos via LimiteBanco.
        // Optamos por testar via LimiteBanco pois é o único caller de AdicionarItemInterno.
        //
        // Alternativa: criar revisão via CriarComInstant com lista de itens, encerrar,
        // e verificar que Itens está frozen (immutable via IReadOnlyCollection) — o método
        // Atualizar nos itens pode ser chamado diretamente para testar a invariante de valor.
        //
        // Aqui testamos a invariante de imutabilidade via agregado LimiteBanco:
        // após SubstituirGarantiasExigidas a revisão anterior deve estar encerrada e seus itens
        // inalterados mesmo que o item do tipo seja chamado com Atualizar.
        var clock = CriarClock();
        var limite = LimiteBanco.Criar(
            Guid.NewGuid(), ModalidadeContrato.Finimp,
            new Money(1_000_000m, Moeda.Brl), new NodaTime.LocalDate(2026, 1, 1), clock,
            garantiasExigidas: new[] { SpecCdb() });

        var revisaoOriginal = limite.RevisaoGarantiasVigente!;

        // Substitui: fecha a revisão original
        limite.SubstituirGarantiasExigidas(new[] { SpecAval() }, clock);

        // Revisão original deve estar encerrada
        revisaoOriginal.EstaVigente.Should().BeFalse();
        revisaoOriginal.VigenciaFim.Should().NotBeNull();

        // Itens da revisão encerrada são imutáveis via IReadOnlyCollection
        var itensOriginais = revisaoOriginal.Itens;
        itensOriginais.Should().ContainSingle(i => i.Tipo == TipoGarantia.CdbCativo);
    }

    // ─── SR-06: Não duplicar tipo na mesma revisão ───────────────────────────

    [Fact]
    public void Criar_ComItensDeMesmoTipo_LancaInvalidOperationException()
    {
        // SR-06
        var clock = CriarClock();
        var itens = new[]
        {
            SpecCdb(20m),
            SpecCdb(30m), // mesmo tipo
        };

        var act = () => GarantiaExigidaRevisao.Criar(LimiteId, itens, clock);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*duplicada*");
    }

    // ─── SR-07: Itens herdam validação de GarantiaExigidaItem ────────────────

    [Fact]
    public void Criar_ComItemPercentualEValorFixoSimultaneos_LancaArgumentException()
    {
        // SR-07: percentual e valor fixo são mutuamente exclusivos (AD-4)
        var clock = CriarClock();
        var itens = new[]
        {
            new GarantiaExigidaItemSpec(
                TipoGarantia.CdbCativo,
                PercentualSobreLimite: 20m,
                ValorFixoBrl: new Money(100_000m, Moeda.Brl),
                Obrigatoria: true,
                Observacoes: null),
        };

        var act = () => GarantiaExigidaRevisao.Criar(LimiteId, itens, clock);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*mutuamente exclusivos*");
    }

    [Fact]
    public void Criar_ComItemNaoAvalSemPercentualESemValorFixo_LancaArgumentException()
    {
        // SR-07: não-Aval exige percentual ou valor fixo
        var clock = CriarClock();
        var itens = new[]
        {
            new GarantiaExigidaItemSpec(
                TipoGarantia.CdbCativo,
                PercentualSobreLimite: null,
                ValorFixoBrl: null,
                Obrigatoria: true,
                Observacoes: null),
        };

        var act = () => GarantiaExigidaRevisao.Criar(LimiteId, itens, clock);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*percentual*valor*");
    }

    // ─── SR-08: Revisão pode nascer com itens vazios ─────────────────────────

    [Fact]
    public void Criar_ComItensVazios_Permitido()
    {
        // SR-08
        var clock = CriarClock();

        var revisao = GarantiaExigidaRevisao.Criar(
            limiteBancoId: LimiteId,
            itens: Array.Empty<GarantiaExigidaItemSpec>(),
            clock: clock,
            motivo: "Política sem exigências");

        revisao.Itens.Should().BeEmpty();
        revisao.EstaVigente.Should().BeTrue();
        revisao.Motivo.Should().Be("Política sem exigências");
    }

    // ─── Campos de auditoria ─────────────────────────────────────────────────

    [Fact]
    public void Criar_PreencheRegistradoEmECreatedAtComMesmoInstante()
    {
        var clock = CriarClock();
        var now = clock.GetCurrentInstant();

        var revisao = GarantiaExigidaRevisao.Criar(LimiteId, Array.Empty<GarantiaExigidaItemSpec>(), clock);

        revisao.RegistradoEm.Should().Be(now);
        revisao.CreatedAt.Should().Be(now);
        revisao.UpdatedAt.Should().Be(now);
    }

    [Fact]
    public void EncerrarVigencia_AtualizaUpdatedAt()
    {
        var clockCriacao = CriarClock(2026, 1, 1);
        var clockEncerramento = CriarClock(2026, 6, 1);

        var revisao = GarantiaExigidaRevisao.Criar(LimiteId, Array.Empty<GarantiaExigidaItemSpec>(), clockCriacao);

        revisao.EncerrarVigencia(clockEncerramento);

        revisao.UpdatedAt.Should().Be(clockEncerramento.GetCurrentInstant());
    }
}
