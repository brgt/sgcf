using System.Net.Http.Json;
using System.Text.Json;

namespace Sgcf.Api.IntegrationTests.CrossTenantIsolation.Fixtures;

/// <summary>
/// Base class que fornece utilitários comuns a todos os testes de isolamento cross-tenant.
///
/// Encapsula clientes pré-configurados para TenantA e TenantB, e helpers de seed
/// que criam recursos via HTTP (realista: exercita o pipeline completo).
/// </summary>
public abstract class MultiTenantTestBase(MultiTenantFixture Fixture)
{
    protected static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    /// <summary>Client autenticado como TenantA.</summary>
    protected HttpClient ClientA => Fixture.ClientFor(TenantTestData.TenantAId);

    /// <summary>Client autenticado como TenantB.</summary>
    protected HttpClient ClientB => Fixture.ClientFor(TenantTestData.TenantBId);

    /// <summary>Client super-admin (sem tenant_id fixo).</summary>
    protected HttpClient AdminClient => Fixture.SuperAdminClient();

    // ── Banco seed (global — visível para ambos os tenants) ───────────────────

    /// <summary>
    /// Garante que existe um banco com o <paramref name="codigoCompe"/> informado
    /// e retorna seu ID. Idempotente: se o banco já existir, retorna o ID existente.
    /// </summary>
    protected async Task<Guid> GarantirBancoAsync(
        string codigoCompe,
        string apelido = "Banco Teste")
    {
        // Tenta localizar pelo código COMPE antes de criar.
        HttpResponseMessage getRes = await AdminClient.GetAsync(
            $"/api/v1/bancos/{codigoCompe}");

        if (getRes.IsSuccessStatusCode)
        {
            JsonElement body = JsonSerializer.Deserialize<JsonElement>(
                await getRes.Content.ReadAsStringAsync(), JsonOpts);
            return body.GetProperty("id").GetGuid();
        }

        HttpResponseMessage createRes = await AdminClient.PostAsJsonAsync(
            "/api/v1/bancos",
            new
            {
                codigoCompe,
                razaoSocial    = $"{apelido} S/A",
                apelido,
                padraoAntecipacao = "A",   // PadraoAntecipacao.A — padrão BB FINIMP
            });

        createRes.EnsureSuccessStatusCode();

        JsonElement created = JsonSerializer.Deserialize<JsonElement>(
            await createRes.Content.ReadAsStringAsync(), JsonOpts);
        return created.GetProperty("id").GetGuid();
    }

    // ── Contrato seed ─────────────────────────────────────────────────────────

    /// <summary>
    /// Cria um contrato NCE (modalidade mais simples — sem detail obrigatório) para
    /// o tenant representado por <paramref name="client"/> e retorna o ID do contrato.
    /// </summary>
    protected static async Task<Guid> CriarContratoAsync(
        HttpClient client,
        Guid bancoId,
        string numeroExterno)
    {
        object payload = new
        {
            numeroExterno,
            bancoId,
            modalidade             = "Nce",
            moeda                  = "Brl",
            valorPrincipal         = 1_000_000m,
            dataContratacao        = "2026-01-15",
            dataVencimento         = "2027-01-15",
            taxaAa                 = 12m,
            baseCalculo            = "Dias360",
            contratoPaiId          = (Guid?)null,
            observacoes            = (string?)null,
            finimpDetail           = (object?)null,
            lei4131Detail          = (object?)null,
            refinimpDetail         = (object?)null,
        };

        HttpResponseMessage res = await client.PostAsJsonAsync("/api/v1/contratos", payload);
        res.EnsureSuccessStatusCode();

        JsonElement body = JsonSerializer.Deserialize<JsonElement>(
            await res.Content.ReadAsStringAsync(), JsonOpts);
        return body.GetProperty("id").GetGuid();
    }

    // ── Cenário de simulação seed ─────────────────────────────────────────────

    /// <summary>
    /// Cria um cenário de simulação para o tenant representado por <paramref name="client"/>
    /// e retorna o ID do cenário.
    /// </summary>
    protected static async Task<Guid> CriarCenarioAsync(
        HttpClient client,
        string nome,
        int anoBase = 2026)
    {
        HttpResponseMessage res = await client.PostAsJsonAsync(
            "/api/v1/simulacoes/cenarios",
            new { nome, anoBase });

        res.EnsureSuccessStatusCode();

        JsonElement body = JsonSerializer.Deserialize<JsonElement>(
            await res.Content.ReadAsStringAsync(), JsonOpts);
        return body.GetProperty("id").GetGuid();
    }

    // ── Cotação seed ──────────────────────────────────────────────────────────

    /// <summary>
    /// Cria uma cotação Nce em rascunho para o tenant representado por
    /// <paramref name="client"/> e retorna o ID da cotação.
    /// </summary>
    protected static async Task<Guid> CriarCotacaoAsync(
        HttpClient client,
        string? codigoInterno = null)
    {
        HttpResponseMessage res = await client.PostAsJsonAsync(
            "/api/v1/cotacoes",
            new
            {
                codigoInterno,
                modalidade       = "Nce",
                valorAlvoBrl     = 500_000m,
                prazoMaximoDias  = 365,
                dataAbertura     = "2026-05-20",
            });

        res.EnsureSuccessStatusCode();

        JsonElement body = JsonSerializer.Deserialize<JsonElement>(
            await res.Content.ReadAsStringAsync(), JsonOpts);
        return body.GetProperty("id").GetGuid();
    }
}
