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

[Trait("Category", "Slow")]
[Collection("CotacoesDb")]
public sealed class LimiteBancoRepositoryTests(CotacoesDbFixture fixture)
{
    private LimiteBancoRepository CreateRepo() => new(fixture.Context);

    private LimiteBanco CriarLimite(Guid bancoId, ModalidadeContrato modalidade = ModalidadeContrato.Finimp)
    {
        return LimiteBanco.Criar(
            bancoId: bancoId,
            modalidade: modalidade,
            valorLimiteBrl: new Money(1_000_000m, Moeda.Brl),
            dataVigenciaInicio: new LocalDate(2026, 1, 1),
            clock: fixture.Clock);
    }

    /// <summary>
    /// Insere um registro em banco_config para satisfazer a FK sem depender do BancoRepository.
    /// ExecuteSqlAsync aceita FormattableString e parametriza automaticamente (EF9, sem SQL injection).
    /// </summary>
    private async Task SeedBancoAsync(Guid bancoId, string codigoCompe, string apelido)
    {
        string razaoSocial = "Banco Seed " + apelido;
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

    [Fact]
    public async Task Add_E_GetByBancoModalidade_RetornaLimiteVigente()
    {
        Guid bancoId = Guid.NewGuid();
        await SeedBancoAsync(bancoId, "997", "BTL");

        LimiteBancoRepository repo = CreateRepo();
        LimiteBanco limite = CriarLimite(bancoId);
        repo.Add(limite);
        await repo.SaveChangesAsync();

        await using SgcfDbContext ctx2 = fixture.CreateFreshContext();
        LimiteBancoRepository repo2 = new(ctx2);
        LimiteBanco? encontrado = await repo2.GetByBancoModalidadeAsync(bancoId, ModalidadeContrato.Finimp);

        encontrado.Should().NotBeNull();
        encontrado!.ValorLimiteBrl.Valor.Should().Be(1_000_000m);
        encontrado.ValorUtilizadoBrl.Valor.Should().Be(0m);
    }

    [Fact]
    public async Task RegistrarUso_PersistidoCorretamente()
    {
        Guid bancoId = Guid.NewGuid();
        await SeedBancoAsync(bancoId, "996", "BU");

        LimiteBancoRepository repo = CreateRepo();
        LimiteBanco limite = CriarLimite(bancoId, ModalidadeContrato.Nce);
        repo.Add(limite);
        await repo.SaveChangesAsync();

        // Registra uso no domínio e persiste
        limite.RegistrarUso(new Money(300_000m, Moeda.Brl), fixture.Clock);
        repo.Update(limite);
        await repo.SaveChangesAsync();

        await using SgcfDbContext ctx2 = fixture.CreateFreshContext();
        LimiteBancoRepository repo2 = new(ctx2);
        LimiteBanco? atualizado = await repo2.GetByIdAsync(limite.Id);

        atualizado.Should().NotBeNull();
        atualizado!.ValorUtilizadoBrl.Valor.Should().Be(300_000m);
        atualizado.ValorDisponivelBrl.Valor.Should().Be(700_000m);
    }

    [Fact]
    public async Task GarantiasExigidas_RoundTrip_PreservaColecao()
    {
        Guid bancoId = Guid.NewGuid();
        await SeedBancoAsync(bancoId, "995", "BGE");

        LimiteBancoRepository repo = CreateRepo();
        LimiteBanco limite = LimiteBanco.Criar(
            bancoId: bancoId,
            modalidade: ModalidadeContrato.Finimp,
            valorLimiteBrl: new Money(5_000_000m, Moeda.Brl),
            dataVigenciaInicio: new LocalDate(2026, 1, 1),
            clock: fixture.Clock,
            garantiasExigidas: new[]
            {
                new GarantiaExigidaLimiteSpec(
                    TipoGarantia.CdbCativo, 20m, null, true, "20% do limite em CDB"),
                new GarantiaExigidaLimiteSpec(
                    TipoGarantia.Aval, null, null, true, "Aval dos sócios"),
            });

        repo.Add(limite);
        await repo.SaveChangesAsync();

        await using SgcfDbContext ctx2 = fixture.CreateFreshContext();
        LimiteBancoRepository repo2 = new(ctx2);
        LimiteBanco? recuperado = await repo2.GetByIdAsync(limite.Id);

        recuperado.Should().NotBeNull();
        recuperado!.GarantiasExigidas.Should().HaveCount(2);

        var cdb = recuperado.GarantiasExigidas.Single(g => g.Tipo == TipoGarantia.CdbCativo);
        cdb.PercentualSobreLimite.Should().Be(20m);
        cdb.ValorFixoBrl.Should().BeNull();
        cdb.Obrigatoria.Should().BeTrue();
        cdb.Observacoes.Should().Be("20% do limite em CDB");

        var aval = recuperado.GarantiasExigidas.Single(g => g.Tipo == TipoGarantia.Aval);
        aval.PercentualSobreLimite.Should().BeNull();
        aval.ValorFixoBrl.Should().BeNull();
    }

    [Fact]
    public async Task Historico_RoundTrip_RegistraEntradaInicialEAlteracoes()
    {
        Guid bancoId = Guid.NewGuid();
        await SeedBancoAsync(bancoId, "994", "BH");

        LimiteBancoRepository repo = CreateRepo();
        LimiteBanco limite = CriarLimite(bancoId, ModalidadeContrato.Refinimp);
        repo.Add(limite);
        await repo.SaveChangesAsync();

        // Aumenta o limite — deve adicionar entrada no histórico.
        limite.Atualizar(fixture.Clock, novoLimiteBrl: new Money(2_000_000m, Moeda.Brl));
        repo.Update(limite);
        await repo.SaveChangesAsync();

        await using SgcfDbContext ctx2 = fixture.CreateFreshContext();
        LimiteBancoRepository repo2 = new(ctx2);
        LimiteBanco? recuperado = await repo2.GetByIdAsync(limite.Id);

        recuperado.Should().NotBeNull();
        recuperado!.Historico.Should().HaveCount(2);

        var inicial = recuperado.Historico.OrderBy(h => h.RegistradoEm).First();
        inicial.ValorAnteriorBrl.Should().BeNull();
        inicial.ValorNovoBrl.Valor.Should().Be(1_000_000m);

        var aumento = recuperado.Historico.OrderBy(h => h.RegistradoEm).Last();
        aumento.ValorAnteriorBrl!.Value.Valor.Should().Be(1_000_000m);
        aumento.ValorNovoBrl.Valor.Should().Be(2_000_000m);
    }

    [Fact]
    public async Task CascadeDelete_RemoveGarantiasEHistoricoJuntoComLimite()
    {
        Guid bancoId = Guid.NewGuid();
        await SeedBancoAsync(bancoId, "993", "BCD");

        LimiteBancoRepository repo = CreateRepo();
        LimiteBanco limite = LimiteBanco.Criar(
            bancoId: bancoId,
            modalidade: ModalidadeContrato.Nce,
            valorLimiteBrl: new Money(3_000_000m, Moeda.Brl),
            dataVigenciaInicio: new LocalDate(2026, 1, 1),
            clock: fixture.Clock,
            garantiasExigidas: new[]
            {
                new GarantiaExigidaLimiteSpec(TipoGarantia.Sblc, 50m, null, true, null),
            });

        repo.Add(limite);
        await repo.SaveChangesAsync();

        Guid limiteId = limite.Id;

        await using SgcfDbContext ctxDelete = fixture.CreateFreshContext();
        LimiteBanco aRemover = await ctxDelete.LimitesBanco
            .Include(l => l.GarantiasExigidas)
            .Include(l => l.Historico)
            .SingleAsync(l => l.Id == limiteId);
        ctxDelete.LimitesBanco.Remove(aRemover);
        await ctxDelete.SaveChangesAsync();

        await using SgcfDbContext ctxVerify = fixture.CreateFreshContext();
        bool garantiasOrfas = await ctxVerify.Set<GarantiaExigidaLimite>()
            .AnyAsync(g => g.LimiteBancoId == limiteId);
        bool historicoOrfao = await ctxVerify.Set<LimiteBancoHistorico>()
            .AnyAsync(h => h.LimiteBancoId == limiteId);

        garantiasOrfas.Should().BeFalse();
        historicoOrfao.Should().BeFalse();
    }
}
