using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;
using Sgcf.Infrastructure.Persistence;
using Sgcf.Infrastructure.Persistence.Repositories;
using Xunit;

namespace Sgcf.Application.Tests.Cotacoes.Infrastructure;

/// <summary>
/// Testes de integração que verificam o fluxo completo de
/// PATCH em garantias exigidas via repositório, comprovando que:
/// - cada PATCH com política diferente fecha a revisão vigente e abre nova;
/// - PATCH sem campo garantiasExigidas preserva a revisão existente;
/// - PATCH com lista equivalente não cria revisão duplicada (SLB-04).
/// Testcontainers — marcado [Slow].
/// </summary>
[Trait("Category", "Slow")]
[Collection("CotacoesDb")]
public sealed class LimiteBancoPatchAbreRevisaoTests(CotacoesDbFixture fixture)
{
    // ── helpers ──────────────────────────────────────────────────────────────────

    private LimiteBancoRepository CreateRepo() => new(fixture.Context);

    private async Task SeedBancoAsync(Guid bancoId, string codigoCompe, string apelido)
    {
        string razaoSocial = "Banco Patch " + apelido;
        await fixture.Context.Database.ExecuteSqlAsync(
            $"""
             INSERT INTO sgcf.banco_config (id, codigo_compe, razao_social, apelido,
               aceita_liquidacao_total, aceita_liquidacao_parcial, exige_anuencia_expressa,
               exige_parcela_inteira, aceita_refinimp, aviso_previo_min_dias_uteis,
               padrao_antecipacao, created_at, updated_at)
             VALUES ({bancoId}, {codigoCompe}, {razaoSocial}, {apelido},
               true, true, false, false, true, 0, 0,
               '2026-01-01 00:00:00+00', '2026-01-01 00:00:00+00')
             ON CONFLICT DO NOTHING
             """);
    }

    private async Task<LimiteBanco> CriarEPersistirLimiteAsync(
        Guid bancoId,
        IEnumerable<GarantiaExigidaItemSpec>? garantias = null)
    {
        LimiteBancoRepository repo = CreateRepo();
        LimiteBanco limite = LimiteBanco.Criar(
            bancoId: bancoId,
            modalidade: ModalidadeContrato.Finimp,
            valorLimiteBrl: new Money(1_000_000m, Moeda.Brl),
            dataVigenciaInicio: new LocalDate(2026, 1, 1),
            clock: fixture.Clock,
            garantiasExigidas: garantias);
        repo.Add(limite);
        await repo.SaveChangesAsync();
        return limite;
    }

    private async Task<LimiteBanco> RecarregarComRevisoesAsync(Guid limiteId)
    {
        await using SgcfDbContext ctx2 = fixture.CreateFreshContext();
        return await ctx2.LimitesBanco
            .Include(l => l.RevisoesGarantiasExigidas)
                .ThenInclude(r => r.Itens)
            .SingleAsync(l => l.Id == limiteId);
    }

    // ── Cenário R01 ───────────────────────────────────────────────────────────────
    // PATCH altera política: Aval → Cdb 50%
    // Espera: 2 revisões, continuidade temporal, itens corretos.

    [Fact]
    public async Task PatchGarantias_ComPoliticaDiferente_FechaRevisaoAntigaEAbreNova()
    {
        // Arrange
        Guid bancoId = Guid.NewGuid();
        await SeedBancoAsync(bancoId, "P01", "BPA");

        LimiteBanco limite = await CriarEPersistirLimiteAsync(bancoId, new[]
        {
            new GarantiaExigidaItemSpec(TipoGarantia.Aval, null, null, true, null),
        });

        // Act — recarrega com tracking e aplica PATCH
        await using SgcfDbContext ctxPatch = fixture.CreateFreshContext();
        LimiteBanco limitePatch = await ctxPatch.LimitesBanco
            .Include(l => l.RevisoesGarantiasExigidas)
                .ThenInclude(r => r.Itens)
            .SingleAsync(l => l.Id == limite.Id);

        limitePatch.SubstituirGarantiasExigidas(
            new[] { new GarantiaExigidaItemSpec(TipoGarantia.CdbCativo, 50m, null, true, null) },
            fixture.Clock);

        await ctxPatch.SaveChangesAsync();

        // Assert
        LimiteBanco recuperado = await RecarregarComRevisoesAsync(limite.Id);

        recuperado.RevisoesGarantiasExigidas.Should().HaveCount(2,
            "cada PATCH com política diferente deve criar nova revisão (append-only)");

        GarantiaExigidaRevisao revisaoAntiga = recuperado.RevisoesGarantiasExigidas
            .OrderBy(r => r.VigenciaInicio)
            .First();

        GarantiaExigidaRevisao revisaoNova = recuperado.RevisoesGarantiasExigidas
            .OrderBy(r => r.VigenciaInicio)
            .Last();

        revisaoAntiga.VigenciaFim.Should().NotBeNull("a revisão anterior deve estar encerrada");
        revisaoAntiga.Itens.Should().ContainSingle(i => i.Tipo == TipoGarantia.Aval,
            "a revisão antiga deve conter o Aval original");

        revisaoNova.VigenciaFim.Should().BeNull("a revisão nova deve estar vigente");
        revisaoNova.Itens.Should().ContainSingle(i => i.Tipo == TipoGarantia.CdbCativo,
            "a revisão nova deve conter o CdbCativo");
        revisaoNova.Itens.Single().PercentualSobreLimite.Should().Be(50m);

        // SLB-03: continuidade temporal sem gap
        revisaoAntiga.VigenciaFim.Should().Be(revisaoNova.VigenciaInicio,
            "VigenciaFim da anterior deve ser exatamente igual ao VigenciaInicio da nova (SLB-03)");
    }

    // ── Cenário R02 ───────────────────────────────────────────────────────────────
    // PATCH sem campo garantiasExigidas (somente outro campo) preserva revisão.
    // Espera: ainda 1 revisão vigente, inalterada.

    [Fact]
    public async Task PatchSemCampoGarantias_PreservaRevisaoExistente()
    {
        // Arrange
        Guid bancoId = Guid.NewGuid();
        await SeedBancoAsync(bancoId, "P02", "BPB");

        LimiteBanco limite = await CriarEPersistirLimiteAsync(bancoId, new[]
        {
            new GarantiaExigidaItemSpec(TipoGarantia.Sblc, 25m, null, true, "SBLC 25%"),
        });

        // Act — altera apenas valor limite, sem tocar garantias
        await using SgcfDbContext ctxPatch = fixture.CreateFreshContext();
        LimiteBanco limitePatch = await ctxPatch.LimitesBanco
            .Include(l => l.RevisoesGarantiasExigidas)
                .ThenInclude(r => r.Itens)
            .Include(l => l.Historico)
            .SingleAsync(l => l.Id == limite.Id);

        limitePatch.Atualizar(fixture.Clock, novoLimiteBrl: new Money(2_000_000m, Moeda.Brl));
        // Deliberadamente não chama SubstituirGarantiasExigidas
        await ctxPatch.SaveChangesAsync();

        // Assert
        LimiteBanco recuperado = await RecarregarComRevisoesAsync(limite.Id);

        recuperado.RevisoesGarantiasExigidas.Should().HaveCount(1,
            "nenhuma nova revisão deve ser criada quando garantias não foram alteradas");

        GarantiaExigidaRevisao revisaoVigente = recuperado.RevisoesGarantiasExigidas.Single();
        revisaoVigente.VigenciaFim.Should().BeNull("a única revisão deve continuar vigente");
        revisaoVigente.Itens.Should().ContainSingle(i => i.Tipo == TipoGarantia.Sblc);
    }

    // ── Cenário R03 ───────────────────────────────────────────────────────────────
    // PATCH com lista equivalente: SLB-04 idempotência por valor.
    // Espera: ainda 1 revisão (não cria duplicata).

    [Fact]
    public async Task PatchGarantias_ComListaEquivalente_NaoCriaNovaRevisao()
    {
        // Arrange
        Guid bancoId = Guid.NewGuid();
        await SeedBancoAsync(bancoId, "P03", "BPC");

        LimiteBanco limite = await CriarEPersistirLimiteAsync(bancoId, new[]
        {
            new GarantiaExigidaItemSpec(TipoGarantia.Aval, null, null, true, null),
        });

        // Act — PATCH com a mesma lista (tipo, percentual, valor, obrigatoria, observacoes idênticos)
        await using SgcfDbContext ctxPatch = fixture.CreateFreshContext();
        LimiteBanco limitePatch = await ctxPatch.LimitesBanco
            .Include(l => l.RevisoesGarantiasExigidas)
                .ThenInclude(r => r.Itens)
            .SingleAsync(l => l.Id == limite.Id);

        limitePatch.SubstituirGarantiasExigidas(
            new[] { new GarantiaExigidaItemSpec(TipoGarantia.Aval, null, null, true, null) },
            fixture.Clock);

        await ctxPatch.SaveChangesAsync();

        // Assert
        LimiteBanco recuperado = await RecarregarComRevisoesAsync(limite.Id);

        recuperado.RevisoesGarantiasExigidas.Should().HaveCount(1,
            "lista idêntica à vigente não deve criar nova revisão (SLB-04)");

        recuperado.RevisoesGarantiasExigidas.Single().VigenciaFim.Should().BeNull(
            "a revisão original deve permanecer vigente");
    }
}
