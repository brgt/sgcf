using FluentAssertions;
using NodaTime;
using Sgcf.Domain.Conformidade;
using Xunit;

namespace Sgcf.Domain.Tests.Conformidade;

/// <summary>
/// Testes unitários para RegistroRegulatorio. GAP-CKP-17.
/// </summary>
public sealed class RegistroRegulatorioTests
{
    private static readonly Instant AgoraFixo = Instant.FromUtc(2025, 6, 1, 0, 0, 0);
    private static readonly Guid ContratoIdFixo = Guid.NewGuid();
    private static readonly LocalDate DataFixa = new(2025, 12, 31);

    private static RegistroRegulatorio CriarPadrao(
        TipoRegistroRegulatorio tipo = TipoRegistroRegulatorio.RdeRof,
        LocalDate? dataVencimento = null,
        string? observacao = null)
        => RegistroRegulatorio.Criar(ContratoIdFixo, tipo, dataVencimento, observacao, AgoraFixo);

    [Fact]
    public void Criar_ComParametrosValidos_RetornaPendente()
    {
        RegistroRegulatorio r = CriarPadrao(
            TipoRegistroRegulatorio.RdeRof,
            DataFixa,
            "Operação de empréstimo externo");

        r.ContratoId.Should().Be(ContratoIdFixo);
        r.Tipo.Should().Be(TipoRegistroRegulatorio.RdeRof);
        r.Status.Should().Be(StatusRegistroRegulatorio.Pendente);
        r.DataVencimento.Should().Be(DataFixa);
        r.Observacao.Should().Be("Operação de empréstimo externo");
        r.NumeroRegistro.Should().BeNull();
        r.DataRegistro.Should().BeNull();
        r.CriadoEm.Should().Be(AgoraFixo);
        r.AtualizadoEm.Should().Be(AgoraFixo);
    }

    [Fact]
    public void Criar_ComContratoIdVazio_LancaArgumentException()
    {
        Action act = () => RegistroRegulatorio.Criar(
            Guid.Empty, TipoRegistroRegulatorio.Def, null, null, AgoraFixo);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("contratoId");
    }

    [Fact]
    public void Atualizar_ComNovosValores_AtualizaCamposETimestamp()
    {
        RegistroRegulatorio r = CriarPadrao(dataVencimento: DataFixa, observacao: "original");

        Instant depois = AgoraFixo.Plus(Duration.FromHours(1));
        LocalDate novaData = new(2026, 6, 30);
        r.Atualizar(novaData, "atualizado", depois);

        r.DataVencimento.Should().Be(novaData);
        r.Observacao.Should().Be("atualizado");
        r.AtualizadoEm.Should().Be(depois);
        r.Status.Should().Be(StatusRegistroRegulatorio.Pendente);
    }

    [Fact]
    public void Registrar_ComNumeroValido_DefineNumeroDataEStatusRegistrado()
    {
        RegistroRegulatorio r = CriarPadrao();
        LocalDate dataRegistro = new(2025, 7, 15);
        Instant depois = AgoraFixo.Plus(Duration.FromDays(1));

        r.Registrar("RDE-2025/00123", dataRegistro, "protocolo recebido", depois);

        r.NumeroRegistro.Should().Be("RDE-2025/00123");
        r.DataRegistro.Should().Be(dataRegistro);
        r.Status.Should().Be(StatusRegistroRegulatorio.Registrado);
        r.Observacao.Should().Be("protocolo recebido");
        r.AtualizadoEm.Should().Be(depois);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Registrar_ComNumeroVazio_LancaArgumentException(string numero)
    {
        RegistroRegulatorio r = CriarPadrao();

        Action act = () => r.Registrar(numero, new LocalDate(2025, 7, 1), null, AgoraFixo);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("numeroRegistro");
    }

    [Fact]
    public void AtualizarStatus_PendenteParaEmAnalise_Permitido()
    {
        RegistroRegulatorio r = CriarPadrao();
        Instant depois = AgoraFixo.Plus(Duration.FromDays(1));

        r.AtualizarStatus(StatusRegistroRegulatorio.EmAnalise, "em análise pelo BACEN", depois);

        r.Status.Should().Be(StatusRegistroRegulatorio.EmAnalise);
        r.Observacao.Should().Be("em análise pelo BACEN");
        r.AtualizadoEm.Should().Be(depois);
    }

    [Fact]
    public void AtualizarStatus_EmAnaliseParaDispensado_Permitido()
    {
        RegistroRegulatorio r = CriarPadrao();
        r.AtualizarStatus(StatusRegistroRegulatorio.EmAnalise, null, AgoraFixo);

        Instant depois = AgoraFixo.Plus(Duration.FromDays(5));
        r.AtualizarStatus(StatusRegistroRegulatorio.Dispensado, "dispensado por norma circular", depois);

        r.Status.Should().Be(StatusRegistroRegulatorio.Dispensado);
    }

    [Fact]
    public void AtualizarStatus_ParaExpirado_Permitido()
    {
        RegistroRegulatorio r = CriarPadrao();
        r.AtualizarStatus(StatusRegistroRegulatorio.EmAnalise, null, AgoraFixo);

        Instant depois = AgoraFixo.Plus(Duration.FromDays(365));
        r.AtualizarStatus(StatusRegistroRegulatorio.Expirado, "prazo encerrado", depois);

        r.Status.Should().Be(StatusRegistroRegulatorio.Expirado);
    }

    [Fact]
    public void AtualizarStatus_RegistradoParaPendente_LancaInvalidOperationException()
    {
        RegistroRegulatorio r = CriarPadrao();
        r.Registrar("RDE-2025/00123", new LocalDate(2025, 7, 1), null, AgoraFixo);

        Instant depois = AgoraFixo.Plus(Duration.FromDays(1));
        Action act = () => r.AtualizarStatus(StatusRegistroRegulatorio.Pendente, null, depois);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Pendente*");
    }

    [Fact]
    public void AtualizarStatus_DispensadoParaPendente_LancaInvalidOperationException()
    {
        RegistroRegulatorio r = CriarPadrao();
        r.AtualizarStatus(StatusRegistroRegulatorio.Dispensado, null, AgoraFixo);

        Instant depois = AgoraFixo.Plus(Duration.FromDays(1));
        Action act = () => r.AtualizarStatus(StatusRegistroRegulatorio.Pendente, null, depois);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Pendente*");
    }
}
