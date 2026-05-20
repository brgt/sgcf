using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Sgcf.Api.IntegrationTests.TestAuth;
using Sgcf.Application.Painel.Queries;
using Sgcf.Application.Tenancy;
using Xunit;

namespace Sgcf.Api.IntegrationTests.Painel;

/// <summary>
/// Testa que a soma do saldo por banco produzida por <see cref="GetSaldoPorBancoAtualQuery"/>
/// é igual ao <c>dividaBrutaBrl</c> retornado pelo endpoint <c>/api/v1/painel/divida</c>.
///
/// Este teste valida a invariante fundamental do plano (AD-8):
/// Σ SaldoPorBanco == PainelDivida.DividaBrutaBrl (sem filtros).
/// </summary>
[Collection("PainelVencimentosApi")]
[Trait("Category", "Slow")]
public sealed class SaldoPorBancoAtualApiTests(PainelVencimentosApiFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    // ── helpers de seed ──────────────────────────────────────────────────────

    private static async Task<Guid> CriarBancoAsync(HttpClient client, string codigo, string apelido)
    {
        HttpResponseMessage res = await client.PostAsJsonAsync("/api/v1/bancos", new
        {
            codigoCompe = codigo,
            razaoSocial = $"Banco {apelido} S.A.",
            apelido,
            padraoAntecipacao = "A"
        });
        res.IsSuccessStatusCode.Should().BeTrue($"seed banco falhou: {await res.Content.ReadAsStringAsync()}");
        return (await res.Content.ReadFromJsonAsync<JsonElement>(JsonOpts)).GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CriarContratoBrlAsync(HttpClient client, Guid bancoId, decimal valor)
    {
        HttpResponseMessage res = await client.PostAsJsonAsync("/api/v1/contratos", new
        {
            numeroExterno = $"BRL-E2E-{Guid.NewGuid():N}",
            bancoId,
            modalidade = "CapitalDeGiro",
            moeda = "Brl",
            valorPrincipal = valor,
            dataContratacao = "2025-01-01",
            dataVencimento = "2027-01-01",
            taxaAa = 10.0m,
            baseCalculo = "Dias252",
            contratoPaiId = (Guid?)null,
            periodicidade = "Bullet",
            estruturaAmortizacao = "Bullet",
            quantidadeParcelas = 1,
            dataPrimeiroVencimento = "2027-01-01",
            capitalDeGiroDetail = new { numeroOperacao = (string?)null, tipoProduto = "CCB", temFgi = false }
        });
        res.IsSuccessStatusCode.Should().BeTrue($"seed contrato BRL falhou: {await res.Content.ReadAsStringAsync()}");
        return (await res.Content.ReadFromJsonAsync<JsonElement>(JsonOpts)).GetProperty("id").GetGuid();
    }

    // ── teste principal ───────────────────────────────────────────────────────

    /// <summary>
    /// Cria 2 bancos com 2 contratos BRL cada, chama a query via MediatR e o endpoint
    /// /painel/divida via HTTP e verifica que os totais coincidem.
    /// </summary>
    [Fact]
    public async Task GetSaldoPorBancoAtual_RetornaSomaIgualAoPainelDivida()
    {
        // Arrange — seed 2 bancos × 2 contratos BRL
        using HttpClient client = fixture.CreateAuthenticatedClient();

        Guid banco1Id = await CriarBancoAsync(client, "100", "BancoSaldoE2E1");
        Guid banco2Id = await CriarBancoAsync(client, "101", "BancoSaldoE2E2");

        await CriarContratoBrlAsync(client, banco1Id, 1_000_000m);
        await CriarContratoBrlAsync(client, banco1Id, 500_000m);
        await CriarContratoBrlAsync(client, banco2Id, 2_000_000m);
        await CriarContratoBrlAsync(client, banco2Id, 750_000m);

        // Act A — chama a query via IMediator (sem endpoint REST próprio).
        // Resolve o TenantContext manualmente porque não há pipeline HTTP neste escopo.
        using IServiceScope scope = fixture.Factory.Services.CreateScope();
        ITenantContext tenantCtx = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantCtx.Resolve(ProxysDevTenant.Id, ProxysDevTenant.Slug, false, false);
        IMediator mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        SaldoPorBancoAtualDto saldoPorBanco = await mediator.Send(
            new GetSaldoPorBancoAtualQuery(),
            CancellationToken.None);

        // Act B — chama /painel/divida via HTTP para comparar o total BRL
        HttpResponseMessage painelResponse = await client.GetAsync("/api/v1/painel/divida");
        painelResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        JsonElement painel = await painelResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        decimal dividaBrutaPainel = painel.GetProperty("dividaBrutaBrl").GetDecimal();

        // Assert 1 — a query retorna os bancos esperados
        saldoPorBanco.Bancos.Should().Contain(b => b.BancoId == banco1Id,
            "banco1 deve aparecer no resultado");
        saldoPorBanco.Bancos.Should().Contain(b => b.BancoId == banco2Id,
            "banco2 deve aparecer no resultado");

        // Assert 2 — soma por banco coincide internamente
        decimal somaInterna = saldoPorBanco.Bancos.Sum(b => b.SaldoBrl);
        saldoPorBanco.SaldoTotalBrl.Should().Be(somaInterna,
            "SaldoTotalBrl deve ser a soma exata dos saldos individuais");

        // Assert 3 — total da query >= os contratos que sedeamos
        // (outros testes da fixture podem ter criado contratos adicionais)
        decimal saldoContratosSeeded = 1_000_000m + 500_000m + 2_000_000m + 750_000m;
        saldoPorBanco.SaldoTotalBrl.Should().BeGreaterThanOrEqualTo(saldoContratosSeeded,
            "o total deve incluir pelo menos os contratos sedeados neste teste");

        // Assert 4 — SaldoTotalBrl bate com dividaBrutaBrl do painel (mesma lógica de conversão)
        saldoPorBanco.SaldoTotalBrl.Should().Be(dividaBrutaPainel,
            "GetSaldoPorBancoAtualQuery deve usar exatamente a mesma lógica de conversão " +
            "que GetPainelDividaQueryHandler, garantindo Σ SaldoPorBanco == DividaBrutaBrl");
    }
}
