# SPEC — Task −1.8 — Suite de Testes de Isolação Cross-Tenant

> **Master:** `SPEC.md`
> **Plano:** `tasks/plan_multi_tenancy.md` Task −1.8
> **Status:** Draft
> **Versão:** v1.0
> **Escopo:** L
> **Dependências:** Tasks −1.6, −1.7

---

## 1. Objetivo

Bateria de testes que **garante zero vazamento** entre tenants em todos os controllers existentes do SGCF. Falha em qualquer teste **bloqueia merge** (CI gate).

A suite serve como rede de segurança contra:

- Handler novo esquecendo de respeitar EF filter.
- RLS policy faltando em tabela nova.
- Bug em `TenantSaveInterceptor` aceitando `TenantId` errado.
- Endpoint admin aceitando `X-Tenant-Id` indevidamente.

---

## 2. Estrutura

```
tests/Sgcf.Api.IntegrationTests/CrossTenantIsolation/
├── Fixtures/
│   ├── MultiTenantFixture.cs           ← Cria 2 tenants + dados em cada
│   ├── MultiTenantTestBase.cs          ← Helpers de assertion
│   └── TenantTestData.cs               ← Builders/Factories
├── ContratosCrossTenantTests.cs
├── CotacoesCrossTenantTests.cs
├── HedgesCrossTenantTests.cs
├── BancosCrossTenantTests.cs           ← Caso especial (catálogo global compartilhado)
├── LimitesBancoCrossTenantTests.cs
├── PainelCrossTenantTests.cs
├── AlertasCrossTenantTests.cs
├── SimulacoesCrossTenantTests.cs
├── AuditoriaCrossTenantTests.cs
├── PlanoContasCrossTenantTests.cs
├── ParametrosSistemaCrossTenantTests.cs
├── FeriadosCrossTenantTests.cs         ← Catálogo global (validar acesso compartilhado)
├── CdiSnapshotsCrossTenantTests.cs     ← Catálogo global
└── AdminTenantsCrossTenantTests.cs     ← Super-admin only
```

Total: **14 arquivos**, um por controller.

---

## 3. Fixture compartilhada

```csharp
namespace Sgcf.Api.IntegrationTests.CrossTenantIsolation.Fixtures;

[Trait("Category", "CrossTenantIsolation")]
public sealed class MultiTenantFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    public Guid TenantAId { get; } = Guid.NewGuid();
    public Guid TenantBId { get; } = Guid.NewGuid();
    public string TenantAToken { get; private set; } = default!;
    public string TenantBToken { get; private set; } = default!;
    public string SuperAdminToken { get; private set; } = default!;

    private PostgreSqlContainer _pg = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    public async Task InitializeAsync()
    {
        await _pg.StartAsync();
        await AplicarMigrations();
        await CriarTenants();
        await ProvisionarAmbos();
        await SeedDadosBasicos();
        TenantAToken = GerarToken(TenantAId, role: "admin");
        TenantBToken = GerarToken(TenantBId, role: "admin");
        SuperAdminToken = GerarTokenSuperAdmin();
    }

    public new async Task DisposeAsync() => await _pg.DisposeAsync();

    // Helpers
    public HttpClient ClientFor(Guid tenantId, string role = "admin")
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", TokenFor(tenantId, role));
        return client;
    }

    public HttpClient SuperAdminClient(Guid? impersonateTenant = null)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", SuperAdminToken);
        if (impersonateTenant.HasValue)
            client.DefaultRequestHeaders.Add("X-Tenant-Id", impersonateTenant.Value.ToString());
        return client;
    }
}
```

---

## 4. Padrão de teste por controller

Cada controller recebe 4 testes-padrão (mínimo), além de testes específicos:

### 4.1 `GET` não retorna dados do outro tenant

```csharp
public sealed class ContratosCrossTenantTests(MultiTenantFixture fx) : IClassFixture<MultiTenantFixture>
{
    [Fact]
    public async Task Lista_no_tenant_A_nao_retorna_contratos_do_tenant_B()
    {
        await fx.CriarContrato(fx.TenantAId, "CT-A-001");
        await fx.CriarContrato(fx.TenantBId, "CT-B-001");

        using var client = fx.ClientFor(fx.TenantAId);
        var response = await client.GetAsync("/api/v1/contratos");
        response.EnsureSuccessStatusCode();

        var page = await response.Content.ReadFromJsonAsync<PagedResult<ContratoDto>>();
        page!.Items.Should().HaveCount(1);
        page.Items[0].NumeroExterno.Should().Be("CT-A-001");
    }
}
```

### 4.2 `GET /{id}` de outro tenant retorna 404

```csharp
[Fact]
public async Task Get_contrato_de_outro_tenant_retorna_404()
{
    Guid ctIdB = await fx.CriarContrato(fx.TenantBId, "CT-B-002");

    using var client = fx.ClientFor(fx.TenantAId);
    var response = await client.GetAsync($"/api/v1/contratos/{ctIdB}");

    response.StatusCode.Should().Be(HttpStatusCode.NotFound);
}
```

### 4.3 `POST` com referência a entidade de outro tenant falha

```csharp
[Fact]
public async Task Post_contrato_referenciando_banco_global_funciona_para_ambos_tenants()
{
    // Banco é catálogo global — ambos tenants podem referenciar
    Guid bancoItau = await fx.GetOrCreateBancoGlobal("ITAU");

    using var clientA = fx.ClientFor(fx.TenantAId);
    using var clientB = fx.ClientFor(fx.TenantBId);

    var ra = await clientA.PostAsJsonAsync("/api/v1/contratos",
        new CreateContratoCommand(BancoId: bancoItau, /* ... */));
    var rb = await clientB.PostAsJsonAsync("/api/v1/contratos",
        new CreateContratoCommand(BancoId: bancoItau, /* ... */));

    ra.StatusCode.Should().Be(HttpStatusCode.Created);
    rb.StatusCode.Should().Be(HttpStatusCode.Created);
}
```

### 4.4 `DELETE` em entidade de outro tenant retorna 404

```csharp
[Fact]
public async Task Delete_contrato_de_outro_tenant_retorna_404()
{
    Guid ctIdB = await fx.CriarContrato(fx.TenantBId, "CT-B-003");

    using var client = fx.ClientFor(fx.TenantAId);
    var response = await client.DeleteAsync($"/api/v1/contratos/{ctIdB}");

    response.StatusCode.Should().Be(HttpStatusCode.NotFound);
}
```

### 4.5 Paginação não cruza tenants

```csharp
[Fact]
public async Task Paginacao_grande_no_tenant_A_nao_inclui_dados_do_B()
{
    for (int i = 0; i < 50; i++)
        await fx.CriarContrato(fx.TenantAId, $"CT-A-{i:D3}");
    for (int i = 0; i < 50; i++)
        await fx.CriarContrato(fx.TenantBId, $"CT-B-{i:D3}");

    using var client = fx.ClientFor(fx.TenantAId);
    var response = await client.GetAsync("/api/v1/contratos?pageSize=100");
    var page = await response.Content.ReadFromJsonAsync<PagedResult<ContratoDto>>();

    page!.Items.Should().HaveCount(50);
    page.Items.Should().AllSatisfy(c => c.NumeroExterno.Should().StartWith("CT-A-"));
    page.Total.Should().Be(50);
}
```

---

## 5. Testes específicos por controller

| Controller | Casos específicos |
|------------|---------------------|
| `BancosController` | `GET` retorna bancos globais para ambos os tenants (não isola). |
| `FeriadosController` | Idem (calendário universal). |
| `CdiSnapshotsController` | Idem (CDI universal). |
| `TenantsController` | Sem `super-admin`, retorna 403 para qualquer endpoint. Com `super-admin`, lista todos. |
| `AuditoriaController` | `GET /auditoria` retorna apenas eventos do tenant atual. `GET /admin/auditoria?tenantId=X` exige `super-admin`. |
| `PainelController` | KPIs do tenant A não incluem contratos do B. |
| `AlertasController` | (Após Task 0.3 cockpit) Listagem filtra por `tenant_id` e `perfil`. |
| `ParametrosSistemaController` | (Após Task −1.9) Tetão de A diferente do tetão de B. |

---

## 6. Cenários de Impersonação (super-admin)

```csharp
[Fact]
public async Task SuperAdmin_com_X_Tenant_Id_acessa_dados_do_tenant_alvo()
{
    Guid ctIdB = await fx.CriarContrato(fx.TenantBId, "CT-B-IMP-001");

    using var client = fx.SuperAdminClient(impersonateTenant: fx.TenantBId);
    var response = await client.GetAsync("/api/v1/contratos");
    var page = await response.Content.ReadFromJsonAsync<PagedResult<ContratoDto>>();

    page!.Items.Should().Contain(c => c.NumeroExterno == "CT-B-IMP-001");
}

[Fact]
public async Task SuperAdmin_impersonando_audita_com_flag()
{
    using var client = fx.SuperAdminClient(impersonateTenant: fx.TenantBId);
    await client.PatchAsJsonAsync($"/api/v1/contratos/{_someId}",
        new { observacoes = "atualizado por suporte" });

    var audit = await fx.GetUltimoAuditLog(fx.TenantBId, "Contrato");
    audit.Detalhes.Should().Contain("\"impersonating\": true");
}
```

---

## 7. Cenários de catálogo global

```csharp
[Fact]
public async Task Banco_eh_visivel_para_ambos_tenants()
{
    using var clientA = fx.ClientFor(fx.TenantAId);
    using var clientB = fx.ClientFor(fx.TenantBId);

    var ra = await clientA.GetAsync("/api/v1/bancos");
    var rb = await clientB.GetAsync("/api/v1/bancos");

    var bancosA = await ra.Content.ReadFromJsonAsync<List<BancoDto>>();
    var bancosB = await rb.Content.ReadFromJsonAsync<List<BancoDto>>();

    bancosA.Should().NotBeEmpty();
    bancosB.Should().BeEquivalentTo(bancosA);
}
```

---

## 8. Idempotency Keys

```csharp
[Fact]
public async Task IdempotencyKey_compartilhada_entre_tenants_nao_colide()
{
    string key = "shared-key-001";

    using var clientA = fx.ClientFor(fx.TenantAId);
    using var clientB = fx.ClientFor(fx.TenantBId);

    clientA.DefaultRequestHeaders.Add("Idempotency-Key", key);
    clientB.DefaultRequestHeaders.Add("Idempotency-Key", key);

    var ra = await clientA.PostAsJsonAsync("/api/v1/contratos", FixtureContrato("A"));
    var rb = await clientB.PostAsJsonAsync("/api/v1/contratos", FixtureContrato("B"));

    ra.StatusCode.Should().Be(HttpStatusCode.Created);
    rb.StatusCode.Should().Be(HttpStatusCode.Created); // não retorna a resposta cached do A
}
```

---

## 9. CI Gate

**Decisão sponsor 2026-05-20:** branch protection ativa bloqueando merge em `main` quando suite cross-tenant estiver vermelha.

### 9.1 Workflow CI completo

`.github/workflows/ci-cross-tenant.yml`:

```yaml
name: Cross-Tenant Isolation Tests

on:
  pull_request:
    branches: [main]
    paths:
      - 'sgcf-backend/**'
  push:
    branches: [main]

jobs:
  cross-tenant-isolation:
    name: Cross-Tenant Isolation Tests
    runs-on: ubuntu-latest
    timeout-minutes: 30

    services:
      postgres:
        image: postgres:16-alpine
        env:
          POSTGRES_USER: sgcf
          POSTGRES_PASSWORD: sgcf_test
          POSTGRES_DB: sgcf_test
        ports:
          - 5432:5432
        options: >-
          --health-cmd pg_isready
          --health-interval 10s
          --health-timeout 5s
          --health-retries 5

      redis:
        image: redis:7-alpine
        ports:
          - 6379:6379
        options: >-
          --health-cmd "redis-cli ping"
          --health-interval 10s

    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '11.0.x'

      - name: Restore
        working-directory: sgcf-backend
        run: dotnet restore

      - name: Build
        working-directory: sgcf-backend
        run: dotnet build --no-restore --configuration Release

      - name: Run cross-tenant isolation suite
        working-directory: sgcf-backend
        env:
          ConnectionStrings__Default: "Host=localhost;Database=sgcf_test;Username=sgcf;Password=sgcf_test"
          Redis__ConnectionString: "localhost:6379"
        run: |
          dotnet test \
            --filter "Category=CrossTenantIsolation" \
            --no-build \
            --configuration Release \
            --logger "trx;LogFileName=cross-tenant-results.trx" \
            --logger "console;verbosity=normal" \
            --results-directory ./TestResults

      - name: Upload test results
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: cross-tenant-test-results
          path: sgcf-backend/TestResults/

      - name: Block merge on failure
        if: failure()
        run: |
          echo "::error::Cross-tenant isolation tests failed. Merge blocked — vazamento entre tenants é risco LGPD."
          exit 1
```

### 9.2 Branch protection (GitHub)

Configuração feita pelo administrador do repositório em `Settings → Branches → Add branch protection rule`:

- **Branch name pattern:** `main`
- **Require a pull request before merging:** ✅
  - Require approvals: 1
  - Dismiss stale pull request approvals when new commits are pushed: ✅
- **Require status checks to pass before merging:** ✅
  - Require branches to be up to date before merging: ✅
  - **Required status checks:**
    - `Cross-Tenant Isolation Tests`
    - `dotnet test (existente)`
    - `build (existente)`
- **Require conversation resolution before merging:** ✅
- **Do not allow bypassing the above settings:** ✅
- **Restrict who can push to matching branches:** apenas líderes (configurar lista).

Documentação detalhada do procedimento em `docs/operacao/ci-branch-protection.md` (a criar nesta task).

### 9.3 Configuração via `gh` CLI

Comando para aplicar via terminal (operador com permissão admin no repositório):

```bash
gh api -X PUT \
  -H "Accept: application/vnd.github+json" \
  /repos/<org>/<repo>/branches/main/protection \
  -F required_status_checks[strict]=true \
  -F required_status_checks[contexts][]='Cross-Tenant Isolation Tests' \
  -F required_status_checks[contexts][]='dotnet test' \
  -F enforce_admins=true \
  -F required_pull_request_reviews[required_approving_review_count]=1 \
  -F required_pull_request_reviews[dismiss_stale_reviews]=true \
  -F restrictions=null
```

### 9.4 CODEOWNERS

`.github/CODEOWNERS` (adicionar para mudanças em RLS/tenancy):

```
# Multi-tenancy critical paths — require security review
/sgcf-backend/src/Sgcf.Domain/Tenancy/                  @<security-team>
/sgcf-backend/src/Sgcf.Infrastructure/Tenancy/          @<security-team>
/sgcf-backend/src/Sgcf.Infrastructure/Persistence/SgcfDbContext.cs  @<security-team>
/sgcf-backend/src/Sgcf.Infrastructure/Migrations/*RowLevelSecurity*  @<security-team>
/sgcf-backend/tests/Sgcf.Api.IntegrationTests/CrossTenantIsolation/  @<security-team>
```

---

## 10. Critérios de Aceite

- [ ] 14 arquivos de teste (um por controller existente) implementados.
- [ ] Cada controller tenant-scoped tem mínimo 4 testes padrão (§4).
- [ ] Bancos, Feriados, CdiSnapshots têm testes confirmando visibilidade global.
- [ ] Cenários de impersonação super-admin cobertos.
- [ ] Idempotency Keys testadas cross-tenant.
- [ ] Suite roda em CI com tag `Category=CrossTenantIsolation`.
- [ ] Branch protection bloqueia merge se suite vermelha.

---

## 11. Verificação

```bash
dotnet test --filter "Category=CrossTenantIsolation" --logger "console;verbosity=normal"

# Quantidade esperada
dotnet test --filter "Category=CrossTenantIsolation" --list-tests | wc -l
# → ~80 testes (14 controllers × ~5-6 testes cada)
```

**Critério de pronto da fixture:**

- Fixture inicializa em < 30 s (PostgreSqlContainer + migrations + seed).
- Cada teste roda em < 5 s.

---

## 12. Boundaries específicas

### 12.1 Always do
- Cada controller novo recebe arquivo de teste cross-tenant antes de merge.
- Fixtures isolam dados por tenant via builders explícitos.
- Asserts comparam tanto contagem quanto conteúdo (`numero_externo` etc.).

### 12.2 Ask first
- Reduzir cobertura (skip de algum controller) — exige justificativa.
- Adicionar tag nova (afeta CI gate).

### 12.3 Never do
- Compartilhar `HttpClient` entre tenants no mesmo teste (fica difícil rastrear).
- Confiar apenas na contagem total — sempre validar identificadores.
- Pular suite localmente sem rodar em CI.

---

## 13. Arquivos esperados

- `tests/Sgcf.Api.IntegrationTests/CrossTenantIsolation/Fixtures/MultiTenantFixture.cs`
- `tests/Sgcf.Api.IntegrationTests/CrossTenantIsolation/Fixtures/MultiTenantTestBase.cs`
- `tests/Sgcf.Api.IntegrationTests/CrossTenantIsolation/Fixtures/TenantTestData.cs`
- 14 arquivos `*CrossTenantTests.cs` (lista §2)
- `.github/workflows/ci-cross-tenant.yml` (gate cross-tenant)
- `.github/CODEOWNERS` (paths críticos de tenancy)
- `docs/operacao/ci-branch-protection.md` (procedimento de configuração)
- `docs/operacao/multi-tenancy.md` (seção sobre como rodar a suite localmente)
