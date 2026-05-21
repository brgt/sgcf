using FluentAssertions;
using NodaTime;
using NSubstitute;
using Sgcf.Domain.Alertas;
using Xunit;

namespace Sgcf.Domain.Tests.Alertas;

public sealed class AlertaTests
{
    private static IClock Clock
    {
        get
        {
            IClock clock = Substitute.For<IClock>();
            clock.GetCurrentInstant().Returns(Instant.FromUtc(2026, 5, 21, 10, 0));
            return clock;
        }
    }

    private static Alerta CriarAlertaValido(IClock? clock = null) =>
        Alerta.Criar(
            categoria: CategoriaAlerta.Vencimento,
            severidade: SeveridadeAlerta.Atencao,
            titulo: "Contrato vence em 7 dias",
            descricao: "O contrato FINAN-001 vence em 7 dias úteis.",
            origemTipo: "Contrato",
            origemId: Guid.NewGuid(),
            perfisVisiveis: [PerfilCockpit.Cfo, PerfilCockpit.Tesouraria],
            chaveIdempotencia: "vencimento:contrato-001:D-7:2026-05-28",
            clock: clock ?? Clock);

    // ── Criar ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Criar_ComDadosValidos_RetornaAlertaAberto()
    {
        Alerta alerta = CriarAlertaValido();

        alerta.Status.Should().Be(StatusAlerta.Aberto);
        alerta.Id.Should().NotBe(Guid.Empty);
        alerta.Titulo.Should().Be("Contrato vence em 7 dias");
        alerta.Categoria.Should().Be(CategoriaAlerta.Vencimento);
        alerta.Severidade.Should().Be(SeveridadeAlerta.Atencao);
    }

    [Fact]
    public void Criar_ComPerfisVisiveis_PersistePerfisCorretamente()
    {
        Alerta alerta = CriarAlertaValido();

        alerta.PerfisVisiveis.Should().HaveCount(2);
        alerta.PerfisVisiveis.Select(p => p.Perfil).Should()
            .BeEquivalentTo([PerfilCockpit.Cfo, PerfilCockpit.Tesouraria]);
    }

    [Fact]
    public void Criar_PerfisVisiveis_ReferenciaIdDoAlerta()
    {
        Alerta alerta = CriarAlertaValido();

        alerta.PerfisVisiveis.Should().AllSatisfy(p =>
            p.AlertaId.Should().Be(alerta.Id));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_ComChaveIdempotenciaVazia_LancaArgumentException(string chave)
    {
        Action acao = () => Alerta.Criar(
            CategoriaAlerta.Vencimento,
            SeveridadeAlerta.Atencao,
            "Título válido",
            "Descrição válida.",
            "Contrato",
            null,
            [PerfilCockpit.Cfo],
            chave,
            Clock);

        acao.Should().Throw<ArgumentException>()
            .WithMessage("*ChaveIdempotencia*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_ComTituloVazio_LancaArgumentException(string titulo)
    {
        Action acao = () => Alerta.Criar(
            CategoriaAlerta.Vencimento,
            SeveridadeAlerta.Atencao,
            titulo,
            "Descrição válida.",
            "Contrato",
            null,
            [PerfilCockpit.Cfo],
            "chave-valida",
            Clock);

        acao.Should().Throw<ArgumentException>()
            .WithMessage("*Título*");
    }

    [Fact]
    public void Criar_ComTituloAcima200Chars_LancaArgumentException()
    {
        string tituloLongo = new('x', 201);

        Action acao = () => Alerta.Criar(
            CategoriaAlerta.Vencimento,
            SeveridadeAlerta.Atencao,
            tituloLongo,
            "Descrição válida.",
            "Contrato",
            null,
            [PerfilCockpit.Cfo],
            "chave-valida",
            Clock);

        acao.Should().Throw<ArgumentException>()
            .WithMessage("*200*");
    }

    // ── MarcarComoLido ────────────────────────────────────────────────────────

    [Fact]
    public void MarcarComoLido_AlertaAberto_MudaStatusParaLido()
    {
        Alerta alerta = CriarAlertaValido();

        alerta.MarcarComoLido(Clock);

        alerta.Status.Should().Be(StatusAlerta.Lido);
    }

    [Fact]
    public void MarcarComoLido_AlertaJaLido_EIdempotente()
    {
        Alerta alerta = CriarAlertaValido();
        alerta.MarcarComoLido(Clock);

        // Segunda chamada não deve lançar exceção.
        Action acao = () => alerta.MarcarComoLido(Clock);

        acao.Should().NotThrow();
        alerta.Status.Should().Be(StatusAlerta.Lido);
    }

    [Fact]
    public void MarcarComoLido_AlertaDispensado_LancaInvalidOperationException()
    {
        Alerta alerta = CriarAlertaValido();
        alerta.Dispensar(Clock);

        Action acao = () => alerta.MarcarComoLido(Clock);

        acao.Should().Throw<InvalidOperationException>()
            .WithMessage("*dispensado*");
    }

    // ── Dispensar ─────────────────────────────────────────────────────────────

    [Fact]
    public void Dispensar_AlertaAberto_MudaStatusParaDispensado()
    {
        Alerta alerta = CriarAlertaValido();

        alerta.Dispensar(Clock);

        alerta.Status.Should().Be(StatusAlerta.Dispensado);
    }

    [Fact]
    public void Dispensar_AlertaLido_MudaStatusParaDispensado()
    {
        Alerta alerta = CriarAlertaValido();
        alerta.MarcarComoLido(Clock);

        alerta.Dispensar(Clock);

        alerta.Status.Should().Be(StatusAlerta.Dispensado);
    }

    [Fact]
    public void Dispensar_AlertaJaDispensado_EIdempotente()
    {
        Alerta alerta = CriarAlertaValido();
        alerta.Dispensar(Clock);

        // Segunda chamada não deve lançar exceção.
        Action acao = () => alerta.Dispensar(Clock);

        acao.Should().NotThrow();
        alerta.Status.Should().Be(StatusAlerta.Dispensado);
    }
}
