using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;
using Sgcf.Infrastructure.Persistence.Repositories;
using Xunit;

namespace Sgcf.Application.Tests.Cotacoes.Infrastructure;

/// <summary>
/// Testes de integração para <see cref="ConsultaSaldoBancoService"/>.
/// Verifica que as quatro queries SQL retornam valores corretos com dados reais no PostgreSQL.
/// Usa a fixture compartilhada <see cref="CotacoesDbFixture"/> (um único container para a coleção).
/// </summary>
[Trait("Category", "Slow")]
[Collection("CotacoesDb")]
public sealed class ConsultaSaldoBancoServiceTests(CotacoesDbFixture fixture)
{
    private static readonly Guid TenantId = CotacoesDbFixture.TestTenantId;

    private ConsultaSaldoBancoService CreateService() =>
        new(fixture.Context);

    // ─── Helpers de seed ──────────────────────────────────────────────────────

    /// <summary>
    /// Insere uma linha em sgcf.banco_config diretamente via SQL para satisfazer a FK
    /// sem depender do BancoRepository. Usa ON CONFLICT DO NOTHING para idempotência.
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

    /// <summary>
    /// Cria e persiste um Contrato ativo para o banco/tenant fixo com o valor informado.
    /// </summary>
    private async Task SeedContratoAtivoAsync(
        Guid bancoId,
        decimal valorPrincipalBrl,
        string numeroExterno)
    {
        Contrato contrato = Contrato.Criar(
            numeroExterno: numeroExterno,
            bancoId: bancoId,
            modalidade: ModalidadeContrato.Finimp,
            valorPrincipal: new Money(valorPrincipalBrl, Moeda.Brl),
            dataContratacao: new LocalDate(2026, 1, 1),
            dataVencimento: new LocalDate(2027, 1, 1),
            taxaAa: Percentual.De(10m),
            baseCalculo: BaseCalculo.Dias360,
            clock: fixture.Clock);

        fixture.Context.Contratos.Add(contrato);
        await fixture.Context.SaveChangesAsync();
    }

    /// <summary>
    /// Cria e persiste um LimiteBanco vigente (DataVigenciaFim = null) para o banco/tenant fixo.
    /// </summary>
    private async Task<LimiteBanco> SeedLimiteBancoAsync(
        Guid bancoId,
        decimal valorLimiteBrl,
        decimal valorUtilizadoBrl = 0m,
        ModalidadeContrato modalidade = ModalidadeContrato.Finimp)
    {
        LimiteBanco limite = LimiteBanco.Criar(
            bancoId: bancoId,
            modalidade: modalidade,
            valorLimiteBrl: new Money(valorLimiteBrl, Moeda.Brl),
            dataVigenciaInicio: new LocalDate(2026, 1, 1),
            clock: fixture.Clock);

        if (valorUtilizadoBrl > 0m)
        {
            limite.RegistrarUso(new Money(valorUtilizadoBrl, Moeda.Brl), fixture.Clock);
        }

        fixture.Context.LimitesBanco.Add(limite);
        await fixture.Context.SaveChangesAsync();
        return limite;
    }

    // ─── CalcularSaldoDevedorBancoAsync ───────────────────────────────────────

    /// <summary>
    /// TC-01: 2 contratos ativos para BancoA com valores 100k e 50k
    /// → soma retornada deve ser 150k BRL.
    /// </summary>
    [Fact]
    public async Task CalcularSaldoDevedorBancoAsync_DoisContratosAtivos_RetornaSoma()
    {
        // Arrange
        Guid bancoId = Guid.NewGuid();
        await SeedBancoAsync(bancoId, "S01", "BS01");
        await SeedContratoAtivoAsync(bancoId, 100_000m, $"CTR-S01-A-{bancoId:N}");
        await SeedContratoAtivoAsync(bancoId, 50_000m, $"CTR-S01-B-{bancoId:N}");

        ConsultaSaldoBancoService sut = CreateService();

        // Act
        Money resultado = await sut.CalcularSaldoDevedorBancoAsync(bancoId, TenantId);

        // Assert
        resultado.Moeda.Should().Be(Moeda.Brl);
        resultado.Valor.Should().Be(150_000m);
    }

    /// <summary>
    /// TC-02: Contratos de BancoA e BancoB presentes.
    /// Query para BancoA não deve incluir contratos de BancoB.
    /// </summary>
    [Fact]
    public async Task CalcularSaldoDevedorBancoAsync_ComContratosDeOutroBanco_NaoIncluiOutroBanco()
    {
        // Arrange
        Guid bancoA = Guid.NewGuid();
        Guid bancoB = Guid.NewGuid();
        await SeedBancoAsync(bancoA, "S02", "BS02A");
        await SeedBancoAsync(bancoB, "S03", "BS02B");

        await SeedContratoAtivoAsync(bancoA, 200_000m, $"CTR-S02-A-{bancoA:N}");
        await SeedContratoAtivoAsync(bancoB, 999_000m, $"CTR-S02-B-{bancoB:N}");

        ConsultaSaldoBancoService sut = CreateService();

        // Act
        Money resultado = await sut.CalcularSaldoDevedorBancoAsync(bancoA, TenantId);

        // Assert
        resultado.Valor.Should().Be(200_000m,
            because: "apenas os contratos de BancoA devem ser somados");
    }

    /// <summary>
    /// TC-03: Nenhum contrato cadastrado para BancoA
    /// → deve retornar Money(0, BRL) sem lançar exceção.
    /// </summary>
    [Fact]
    public async Task CalcularSaldoDevedorBancoAsync_SemContratos_RetornaZero()
    {
        // Arrange — banco sem contratos
        Guid bancoId = Guid.NewGuid();
        await SeedBancoAsync(bancoId, "S04", "BS04");

        ConsultaSaldoBancoService sut = CreateService();

        // Act
        Money resultado = await sut.CalcularSaldoDevedorBancoAsync(bancoId, TenantId);

        // Assert
        resultado.Moeda.Should().Be(Moeda.Brl);
        resultado.Valor.Should().Be(0m);
    }

    // ─── CalcularUtilizadoAgregadoModalidadesAsync ────────────────────────────

    /// <summary>
    /// TC-04: 2 LimiteBanco vigentes para BancoA com ValorUtilizado 200k e 100k
    /// → soma deve ser 300k BRL.
    /// </summary>
    [Fact]
    public async Task CalcularUtilizadoAgregadoModalidadesAsync_DoisLimites_RetornaSomaDosUtilizados()
    {
        // Arrange
        Guid bancoId = Guid.NewGuid();
        await SeedBancoAsync(bancoId, "S05", "BS05");

        await SeedLimiteBancoAsync(bancoId, 1_000_000m, valorUtilizadoBrl: 200_000m, ModalidadeContrato.Finimp);
        await SeedLimiteBancoAsync(bancoId, 1_000_000m, valorUtilizadoBrl: 100_000m, ModalidadeContrato.Nce);

        ConsultaSaldoBancoService sut = CreateService();

        // Act
        Money resultado = await sut.CalcularUtilizadoAgregadoModalidadesAsync(bancoId, TenantId);

        // Assert
        resultado.Moeda.Should().Be(Moeda.Brl);
        resultado.Valor.Should().Be(300_000m);
    }

    /// <summary>
    /// TC-05: Nenhum LimiteBanco para BancoA
    /// → deve retornar Money(0, BRL) sem lançar exceção.
    /// </summary>
    [Fact]
    public async Task CalcularUtilizadoAgregadoModalidadesAsync_SemLimites_RetornaZero()
    {
        // Arrange — banco sem LimiteBanco
        Guid bancoId = Guid.NewGuid();
        await SeedBancoAsync(bancoId, "S06", "BS06");

        ConsultaSaldoBancoService sut = CreateService();

        // Act
        Money resultado = await sut.CalcularUtilizadoAgregadoModalidadesAsync(bancoId, TenantId);

        // Assert
        resultado.Moeda.Should().Be(Moeda.Brl);
        resultado.Valor.Should().Be(0m);
    }

    // ─── CalcularSomaLimitesModalidadesAsync ─────────────────────────────────

    /// <summary>
    /// TC-06: 3 LimiteBanco vigentes para BancoA com ValorLimite 100k, 200k e 300k
    /// → soma total deve ser 600k BRL.
    /// </summary>
    [Fact]
    public async Task CalcularSomaLimitesModalidadesAsync_TresLimites_RetornaSomaTotal()
    {
        // Arrange
        Guid bancoId = Guid.NewGuid();
        await SeedBancoAsync(bancoId, "S07", "BS07");

        await SeedLimiteBancoAsync(bancoId, 100_000m, modalidade: ModalidadeContrato.Finimp);
        await SeedLimiteBancoAsync(bancoId, 200_000m, modalidade: ModalidadeContrato.Nce);
        await SeedLimiteBancoAsync(bancoId, 300_000m, modalidade: ModalidadeContrato.Lei4131);

        ConsultaSaldoBancoService sut = CreateService();

        // Act
        Money resultado = await sut.CalcularSomaLimitesModalidadesAsync(bancoId, TenantId);

        // Assert
        resultado.Moeda.Should().Be(Moeda.Brl);
        resultado.Valor.Should().Be(600_000m);
    }

    /// <summary>
    /// TC-07: 3 LimiteBanco com valores 100k, 200k e 300k.
    /// Ao passar excluirLimiteBancoId = o limite de 100k, a soma deve ser 500k.
    /// </summary>
    [Fact]
    public async Task CalcularSomaLimitesModalidadesAsync_ComExclusao_RetornaSomaSemExcluido()
    {
        // Arrange
        Guid bancoId = Guid.NewGuid();
        await SeedBancoAsync(bancoId, "S08", "BS08");

        LimiteBanco limiteExcluir = await SeedLimiteBancoAsync(bancoId, 100_000m, modalidade: ModalidadeContrato.Finimp);
        await SeedLimiteBancoAsync(bancoId, 200_000m, modalidade: ModalidadeContrato.Nce);
        await SeedLimiteBancoAsync(bancoId, 300_000m, modalidade: ModalidadeContrato.Refinimp);

        ConsultaSaldoBancoService sut = CreateService();

        // Act
        Money resultado = await sut.CalcularSomaLimitesModalidadesAsync(
            bancoId, TenantId, excluirLimiteBancoId: limiteExcluir.Id);

        // Assert
        resultado.Moeda.Should().Be(Moeda.Brl);
        resultado.Valor.Should().Be(500_000m,
            because: "o limite de 100k deve ser excluído da soma");
    }

    // ─── BancoEmRegimePerModalityAsync ────────────────────────────────────────

    /// <summary>
    /// TC-08: BancoA com ao menos 1 LimiteBanco vigente (DataVigenciaFim = null)
    /// → deve retornar true.
    /// </summary>
    [Fact]
    public async Task BancoEmRegimePerModalityAsync_ComLimiteVigente_RetornaTrue()
    {
        // Arrange
        Guid bancoId = Guid.NewGuid();
        await SeedBancoAsync(bancoId, "S09", "BS09");
        await SeedLimiteBancoAsync(bancoId, 500_000m);

        ConsultaSaldoBancoService sut = CreateService();

        // Act
        bool resultado = await sut.BancoEmRegimePerModalityAsync(bancoId, TenantId);

        // Assert
        resultado.Should().BeTrue(
            because: "banco com LimiteBanco vigente opera em regime per-modality");
    }

    /// <summary>
    /// TC-09: BancoA sem nenhum LimiteBanco cadastrado
    /// → deve retornar false.
    /// </summary>
    [Fact]
    public async Task BancoEmRegimePerModalityAsync_SemLimites_RetornaFalse()
    {
        // Arrange — banco sem LimiteBanco
        Guid bancoId = Guid.NewGuid();
        await SeedBancoAsync(bancoId, "S10", "BS10");

        ConsultaSaldoBancoService sut = CreateService();

        // Act
        bool resultado = await sut.BancoEmRegimePerModalityAsync(bancoId, TenantId);

        // Assert
        resultado.Should().BeFalse(
            because: "banco sem LimiteBanco não opera em regime per-modality");
    }
}
