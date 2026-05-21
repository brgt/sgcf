using FluentAssertions;
using NodaTime;
using Sgcf.Domain.Exportacao;
using Xunit;

namespace Sgcf.Domain.Tests.Exportacao;

[Trait("Category", "Domain")]
public sealed class ExportacaoJobTests
{
    private static readonly Instant AgoraFixo = Instant.FromUtc(2026, 5, 21, 12, 0);

    // ── Criar ────────────────────────────────────────────────────────────────

    [Fact]
    public void Criar_ComDadosValidos_IniciaComStatusPendente()
    {
        // Act
        ExportacaoJob job = ExportacaoJob.Criar(
            TipoExportacao.Contratos,
            parametrosJson: null,
            solicitadoPor: "user-sub-123",
            agora: AgoraFixo);

        // Assert
        job.Id.Should().NotBeEmpty();
        job.Status.Should().Be(StatusExportacao.Pendente);
        job.Tipo.Should().Be(TipoExportacao.Contratos);
        job.SolicitadoPor.Should().Be("user-sub-123");
        job.CriadoEm.Should().Be(AgoraFixo);
        job.IniciadoEm.Should().BeNull();
        job.ConcluidoEm.Should().BeNull();
        job.ResultadoJson.Should().BeNull();
        job.MensagemErro.Should().BeNull();
    }

    // ── IniciarProcessamento ─────────────────────────────────────────────────

    [Fact]
    public void IniciarProcessamento_JobPendente_TransicionaParaProcessando()
    {
        // Arrange
        ExportacaoJob job = ExportacaoJob.Criar(
            TipoExportacao.FluxoCaixa,
            parametrosJson: null,
            solicitadoPor: "user-sub-123",
            agora: AgoraFixo);

        Instant iniciado = AgoraFixo.Plus(Duration.FromSeconds(5));

        // Act
        job.IniciarProcessamento(iniciado);

        // Assert
        job.Status.Should().Be(StatusExportacao.Processando);
        job.IniciadoEm.Should().Be(iniciado);
    }

    [Fact]
    public void IniciarProcessamento_JobJaProcessando_LancaInvalidOperationException()
    {
        // Arrange
        ExportacaoJob job = ExportacaoJob.Criar(
            TipoExportacao.Contratos,
            parametrosJson: null,
            solicitadoPor: "user-sub-123",
            agora: AgoraFixo);

        job.IniciarProcessamento(AgoraFixo.Plus(Duration.FromSeconds(1)));

        // Act
        Action act = () => job.IniciarProcessamento(AgoraFixo.Plus(Duration.FromSeconds(2)));

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Job já foi iniciado.");
    }

    // ── Concluir ─────────────────────────────────────────────────────────────

    [Fact]
    public void Concluir_JobProcessando_SetaStatusResultadoEConcluidoEm()
    {
        // Arrange
        ExportacaoJob job = ExportacaoJob.Criar(
            TipoExportacao.Covenants,
            parametrosJson: null,
            solicitadoPor: "user-sub-456",
            agora: AgoraFixo);

        job.IniciarProcessamento(AgoraFixo.Plus(Duration.FromSeconds(1)));
        Instant concluido = AgoraFixo.Plus(Duration.FromSeconds(10));
        const string payload = "{\"items\":[]}";

        // Act
        job.Concluir(payload, concluido);

        // Assert
        job.Status.Should().Be(StatusExportacao.Concluido);
        job.ResultadoJson.Should().Be(payload);
        job.ConcluidoEm.Should().Be(concluido);
        job.MensagemErro.Should().BeNull();
    }

    // ── Falhar ───────────────────────────────────────────────────────────────

    [Fact]
    public void Falhar_JobProcessando_SetaStatusMensagemErroEConcluidoEm()
    {
        // Arrange
        ExportacaoJob job = ExportacaoJob.Criar(
            TipoExportacao.AuditLog,
            parametrosJson: null,
            solicitadoPor: "user-sub-789",
            agora: AgoraFixo);

        job.IniciarProcessamento(AgoraFixo.Plus(Duration.FromSeconds(1)));
        Instant falhadoEm = AgoraFixo.Plus(Duration.FromSeconds(5));

        // Act
        job.Falhar("Timeout ao consultar banco de dados.", falhadoEm);

        // Assert
        job.Status.Should().Be(StatusExportacao.Falhou);
        job.MensagemErro.Should().Be("Timeout ao consultar banco de dados.");
        job.ConcluidoEm.Should().Be(falhadoEm);
        job.ResultadoJson.Should().BeNull();
    }
}
