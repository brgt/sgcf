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
}
