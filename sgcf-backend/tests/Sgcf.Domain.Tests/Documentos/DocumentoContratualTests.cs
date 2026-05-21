using FluentAssertions;
using NodaTime;
using Sgcf.Domain.Documentos;
using Xunit;

namespace Sgcf.Domain.Tests.Documentos;

/// <summary>
/// Testes unitários para DocumentoContratual. GAP-CKP-16.
/// Cobre criação, atualização de campos e máquina de estados de status.
/// </summary>
public sealed class DocumentoContratualTests
{
    private static readonly Instant AgoraFixo = Instant.FromUtc(2025, 6, 1, 0, 0, 0);
    private static readonly Guid ContratoIdFixo = Guid.NewGuid();
    private static readonly LocalDate DataEmissaoFixa = new(2025, 1, 15);
    private static readonly LocalDate DataVencimentoFixa = new(2026, 1, 15);

    private static DocumentoContratual CriarDocumentoPadrao() =>
        DocumentoContratual.Criar(
            ContratoIdFixo,
            TipoDocumento.Contrato,
            "Contrato de Financiamento",
            DataEmissaoFixa,
            DataVencimentoFixa,
            "https://storage.example.com/doc.pdf",
            null,
            AgoraFixo);

    [Fact]
    public void Criar_ComParametrosValidos_InicializaComStatusPendente()
    {
        DocumentoContratual doc = CriarDocumentoPadrao();

        doc.ContratoId.Should().Be(ContratoIdFixo);
        doc.Tipo.Should().Be(TipoDocumento.Contrato);
        doc.Status.Should().Be(StatusDocumento.Pendente);
        doc.Nome.Should().Be("Contrato de Financiamento");
        doc.DataEmissao.Should().Be(DataEmissaoFixa);
        doc.DataVencimento.Should().Be(DataVencimentoFixa);
        doc.CriadoEm.Should().Be(AgoraFixo);
        doc.AtualizadoEm.Should().Be(AgoraFixo);
    }

    [Fact]
    public void Atualizar_ComNovoNome_AlteraCamposEAtualizaTimestamp()
    {
        DocumentoContratual doc = CriarDocumentoPadrao();
        Instant depois = AgoraFixo.Plus(Duration.FromHours(1));
        LocalDate novaEmissao = new(2025, 3, 1);

        doc.Atualizar(
            "Aditivo de Financiamento",
            novaEmissao,
            null,
            "https://storage.example.com/aditivo.pdf",
            "Aditivo ao contrato original",
            depois);

        doc.Nome.Should().Be("Aditivo de Financiamento");
        doc.DataEmissao.Should().Be(novaEmissao);
        doc.DataVencimento.Should().BeNull();
        doc.UrlArmazenamento.Should().Be("https://storage.example.com/aditivo.pdf");
        doc.Observacao.Should().Be("Aditivo ao contrato original");
        doc.AtualizadoEm.Should().Be(depois);
        // CriadoEm não deve ser alterado
        doc.CriadoEm.Should().Be(AgoraFixo);
    }

    [Fact]
    public void AtualizarStatus_PendenteParaEmRevisao_TransicaoValida()
    {
        DocumentoContratual doc = CriarDocumentoPadrao();
        Instant depois = AgoraFixo.Plus(Duration.FromHours(1));

        doc.AtualizarStatus(StatusDocumento.EmRevisao, "Em análise jurídica", depois);

        doc.Status.Should().Be(StatusDocumento.EmRevisao);
        doc.Observacao.Should().Be("Em análise jurídica");
        doc.AtualizadoEm.Should().Be(depois);
    }

    [Fact]
    public void AtualizarStatus_EmRevisaoParaAprovado_TransicaoValida()
    {
        DocumentoContratual doc = CriarDocumentoPadrao();
        Instant t1 = AgoraFixo.Plus(Duration.FromHours(1));
        Instant t2 = AgoraFixo.Plus(Duration.FromHours(2));

        doc.AtualizarStatus(StatusDocumento.EmRevisao, null, t1);
        doc.AtualizarStatus(StatusDocumento.Aprovado, "Aprovado pela diretoria", t2);

        doc.Status.Should().Be(StatusDocumento.Aprovado);
        doc.Observacao.Should().Be("Aprovado pela diretoria");
        doc.AtualizadoEm.Should().Be(t2);
    }

    [Fact]
    public void AtualizarStatus_EmRevisaoParaRejeitado_TransicaoValida()
    {
        DocumentoContratual doc = CriarDocumentoPadrao();
        Instant t1 = AgoraFixo.Plus(Duration.FromHours(1));
        Instant t2 = AgoraFixo.Plus(Duration.FromHours(2));

        doc.AtualizarStatus(StatusDocumento.EmRevisao, null, t1);
        doc.AtualizarStatus(StatusDocumento.Rejeitado, "Documento incompleto", t2);

        doc.Status.Should().Be(StatusDocumento.Rejeitado);
        doc.Observacao.Should().Be("Documento incompleto");
    }

    [Theory]
    [InlineData(StatusDocumento.Pendente)]
    [InlineData(StatusDocumento.EmRevisao)]
    [InlineData(StatusDocumento.Aprovado)]
    [InlineData(StatusDocumento.Rejeitado)]
    public void AtualizarStatus_QualquerEstadoParaExpirado_TransicaoValida(StatusDocumento statusOrigem)
    {
        DocumentoContratual doc = CriarDocumentoPadrao();
        Instant t1 = AgoraFixo.Plus(Duration.FromHours(1));

        // Avança para o estado de origem (exceto Pendente que já é o inicial)
        if (statusOrigem != StatusDocumento.Pendente)
        {
            doc.AtualizarStatus(statusOrigem, null, t1);
        }

        Instant t2 = AgoraFixo.Plus(Duration.FromHours(2));
        doc.AtualizarStatus(StatusDocumento.Expirado, "Documento vencido", t2);

        doc.Status.Should().Be(StatusDocumento.Expirado);
    }

    [Fact]
    public void AtualizarStatus_AprovadoParaPendente_LancaInvalidOperationException()
    {
        DocumentoContratual doc = CriarDocumentoPadrao();
        Instant t1 = AgoraFixo.Plus(Duration.FromHours(1));
        Instant t2 = AgoraFixo.Plus(Duration.FromHours(2));

        doc.AtualizarStatus(StatusDocumento.EmRevisao, null, t1);
        doc.AtualizarStatus(StatusDocumento.Aprovado, null, t2);

        Instant t3 = AgoraFixo.Plus(Duration.FromHours(3));
        Action act = () => doc.AtualizarStatus(StatusDocumento.Pendente, null, t3);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Aprovado*Pendente*");
    }
}
