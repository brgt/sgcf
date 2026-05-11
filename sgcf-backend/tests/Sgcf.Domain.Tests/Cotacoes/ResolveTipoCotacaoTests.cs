using FluentAssertions;

using NodaTime;

using NSubstitute;

using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;

using Xunit;

namespace Sgcf.Domain.Tests.Cotacoes;

[Trait("Category", "Domain")]
public sealed class ResolveTipoCotacaoTests
{
    private static IClock CriarClock(Instant instant)
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(instant);
        return clock;
    }

    private static IClock ClockPadrao() =>
        CriarClock(Instant.FromUtc(2026, 5, 11, 10, 0));

    private static ParametroCotacao CriarParametro(
        Guid? bancoId,
        ModalidadeContrato? modalidade,
        TipoCotacao tipoCotacao,
        bool ativo = true)
    {
        ParametroCotacao parametro = ParametroCotacao.Criar(bancoId, modalidade, tipoCotacao, ClockPadrao());
        if (!ativo)
        {
            // Desativar via Atualizar para testar parametros inativos
            parametro.Atualizar(tipoCotacao, false, ClockPadrao());
        }
        return parametro;
    }

    // ── Teste 1: Exact match (bancoId + modalidade) ────────────────────────

    [Fact]
    public void Resolve_ExactMatch_RetornaResultadoExato()
    {
        // Arrange
        Guid bancoId = Guid.NewGuid();
        ModalidadeContrato modalidade = ModalidadeContrato.Finimp;

        List<ParametroCotacao> parametros = new()
        {
            CriarParametro(bancoId, modalidade, TipoCotacao.PtaxD0),
            CriarParametro(bancoId, null, TipoCotacao.SpotIntraday),
            CriarParametro(null, null, TipoCotacao.Fixing),
        };

        // Act
        TipoCotacao resultado = ResolveTipoCotacaoService.Resolve(parametros, bancoId, modalidade);

        // Assert
        resultado.Should().Be(TipoCotacao.PtaxD0);
    }

    // ── Teste 2: Bank default ganha sobre modality default ─────────────────

    [Fact]
    public void Resolve_BankDefaultGanhaSobreModalityDefault()
    {
        // Arrange
        Guid bancoId = Guid.NewGuid();
        ModalidadeContrato modalidade = ModalidadeContrato.Lei4131;

        // Sem match exato para (bancoId + modalidade)
        // Mas há: banco default e modality default
        List<ParametroCotacao> parametros = new()
        {
            CriarParametro(bancoId, null, TipoCotacao.PtaxD0),                   // banco default
            CriarParametro(null, modalidade, TipoCotacao.SpotIntraday),           // modality default
            CriarParametro(null, null, TipoCotacao.Fixing),                       // global default
        };

        // Act
        TipoCotacao resultado = ResolveTipoCotacaoService.Resolve(parametros, bancoId, modalidade);

        // Assert — banco default tem prioridade 2, modality default tem prioridade 3
        resultado.Should().Be(TipoCotacao.PtaxD0);
    }

    // ── Teste 3: Modality default quando não há banco match ───────────────

    [Fact]
    public void Resolve_ModalityDefault_QuandoNaoHaBancoMatch()
    {
        // Arrange
        Guid bancoId = Guid.NewGuid();
        ModalidadeContrato modalidade = ModalidadeContrato.Nce;

        List<ParametroCotacao> parametros = new()
        {
            CriarParametro(null, modalidade, TipoCotacao.SpotIntraday),   // modality default
            CriarParametro(null, null, TipoCotacao.Fixing),               // global default
        };

        // Act
        TipoCotacao resultado = ResolveTipoCotacaoService.Resolve(parametros, bancoId, modalidade);

        // Assert
        resultado.Should().Be(TipoCotacao.SpotIntraday);
    }

    // ── Teste 4: Global default quando não há match específico ─────────────

    [Fact]
    public void Resolve_GlobalDefault_QuandoNaoHaMatchEspecifico()
    {
        // Arrange
        Guid bancoId = Guid.NewGuid();
        ModalidadeContrato modalidade = ModalidadeContrato.Refinimp;

        List<ParametroCotacao> parametros = new()
        {
            CriarParametro(null, null, TipoCotacao.Fixing),   // apenas global default
        };

        // Act
        TipoCotacao resultado = ResolveTipoCotacaoService.Resolve(parametros, bancoId, modalidade);

        // Assert
        resultado.Should().Be(TipoCotacao.Fixing);
    }

    // ── Teste 5: System fallback PtaxD1 quando nenhum parâmetro bate ───────

    [Fact]
    public void Resolve_SystemFallback_QuandoNenhumParametroCorresponde()
    {
        // Arrange
        Guid bancoId = Guid.NewGuid();
        ModalidadeContrato modalidade = ModalidadeContrato.Fgi;

        List<ParametroCotacao> parametros = new();

        // Act
        TipoCotacao resultado = ResolveTipoCotacaoService.Resolve(parametros, bancoId, modalidade);

        // Assert
        resultado.Should().Be(TipoCotacao.PtaxD1);
    }

    // ── Teste 6: Parâmetros inativos são ignorados ──────────────────────────

    [Fact]
    public void Resolve_IgnoraParametrosInativos()
    {
        // Arrange
        Guid bancoId = Guid.NewGuid();
        ModalidadeContrato modalidade = ModalidadeContrato.BalcaoCaixa;

        List<ParametroCotacao> parametros = new()
        {
            CriarParametro(bancoId, modalidade, TipoCotacao.PtaxD0, ativo: false),  // exact, mas inativo
            CriarParametro(bancoId, null, TipoCotacao.SpotIntraday, ativo: false),  // banco default, inativo
            CriarParametro(null, modalidade, TipoCotacao.Fixing, ativo: true),      // modality default, ativo
        };

        // Act
        TipoCotacao resultado = ResolveTipoCotacaoService.Resolve(parametros, bancoId, modalidade);

        // Assert — inativos ignorados; modality default ativo é retornado
        resultado.Should().Be(TipoCotacao.Fixing);
    }

    // ── Teste 7: Múltiplos matches em níveis diferentes → prioridade ────────

    [Fact]
    public void Resolve_MultiplosPossiveisMatches_RetornaMaisEspecifico()
    {
        // Arrange
        Guid bancoId = Guid.NewGuid();
        ModalidadeContrato modalidade = ModalidadeContrato.Finimp;

        List<ParametroCotacao> parametros = new()
        {
            CriarParametro(null, null, TipoCotacao.Fixing),                    // prioridade 4
            CriarParametro(null, modalidade, TipoCotacao.SpotIntraday),        // prioridade 3
            CriarParametro(bancoId, null, TipoCotacao.PtaxD0),                 // prioridade 2
            CriarParametro(bancoId, modalidade, TipoCotacao.PtaxD1),           // prioridade 1 (mais específico)
        };

        // Act
        TipoCotacao resultado = ResolveTipoCotacaoService.Resolve(parametros, bancoId, modalidade);

        // Assert — exact match (prioridade 1) deve vencer
        resultado.Should().Be(TipoCotacao.PtaxD1);
    }

    // ── Teste extra: Lista vazia retorna fallback ──────────────────────────

    [Fact]
    public void Resolve_ListaVazia_RetornaPtaxD1()
    {
        // Arrange
        Guid bancoId = Guid.NewGuid();
        ModalidadeContrato modalidade = ModalidadeContrato.Lei4131;

        IReadOnlyList<ParametroCotacao> parametros = Array.Empty<ParametroCotacao>();

        // Act
        TipoCotacao resultado = ResolveTipoCotacaoService.Resolve(parametros, bancoId, modalidade);

        // Assert
        resultado.Should().Be(TipoCotacao.PtaxD1);
    }
}
