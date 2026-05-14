using System.Text.Json;

using FluentAssertions;

using Sgcf.Mcp.Tools;

using Xunit;

namespace Sgcf.Mcp.Tests.Tools;

[Trait("Category", "Mcp")]
public sealed class RagToolsTests
{
    // ── BuscarClausulaContratual ────────────────────────────────────────────

    [Fact]
    public void BuscarClausulaContratual_RetornaStubComDisponivelFalse()
    {
        // Arrange
        RagTools tools = new();

        // Act
        string resultado = tools.BuscarClausulaContratual("vencimento antecipado");

        // Assert
        using JsonDocument doc = JsonDocument.Parse(resultado);
        doc.RootElement.GetProperty("disponivel").GetBoolean().Should().BeFalse();
    }

    // ── CompararClausulas ──────────────────────────────────────────────────

    [Fact]
    public void CompararClausulas_RetornaStubComDisponivelFalse()
    {
        // Arrange
        RagTools tools = new();
        string contratoA = Guid.NewGuid().ToString();
        string contratoB = Guid.NewGuid().ToString();

        // Act
        string resultado = tools.CompararClausulas(contratoA, contratoB, "garantias");

        // Assert
        using JsonDocument doc = JsonDocument.Parse(resultado);
        doc.RootElement.GetProperty("disponivel").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public void BuscarClausulaContratual_RetornaJsonValido()
    {
        // Arrange
        RagTools tools = new();

        // Act
        string resultado = tools.BuscarClausulaContratual("inadimplência");

        // Assert — deve ser JSON válido e conter mensagem informativa
        JsonDocument doc = JsonDocument.Parse(resultado);
        doc.RootElement.TryGetProperty("mensagem", out JsonElement mensagem).Should().BeTrue();
        mensagem.GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void CompararClausulas_CategoriaNull_RetornaStubComDisponivelFalse()
    {
        // Arrange
        RagTools tools = new();

        // Act
        string resultado = tools.CompararClausulas(
            Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), null);

        // Assert
        using JsonDocument doc = JsonDocument.Parse(resultado);
        doc.RootElement.GetProperty("disponivel").GetBoolean().Should().BeFalse();
    }
}
