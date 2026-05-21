# SPEC — Task 3.1 — Domínio `ContaBancaria` (CRUD)

> **Master:** `SPEC.md`
> **Plano:** `tasks/plan_cockpit_backend_gaps.md` Task 3.1
> **Status:** Draft
> **Versão:** v1.0
> **Escopo:** M
> **Persona:** Gerente de Tesouraria
> **Dependências:** Nenhuma; bloqueia Tasks 3.2 e 3.3

---

## 1. Objetivo

Introduzir o domínio `ContaBancaria` em `Sgcf.Domain.Tesouraria` — pré-requisito para posição de caixa (Task 3.2) e fluxo de caixa (Task 3.3). Sem integração OFX/CNAB no MVP: input manual.

---

## 2. Modelo de Domínio

### 2.1 Enum `TipoContaBancaria`

```csharp
namespace Sgcf.Domain.Tesouraria;

public enum TipoContaBancaria : byte
{
    Corrente   = 1,
    Poupanca   = 2,
    Investimento = 3,
    Garantia   = 4,
    Vinculada  = 5,
}
```

### 2.2 Agregado `ContaBancaria`

```csharp
public sealed class ContaBancaria : Entity, IAuditable
{
    public Guid BancoId { get; private set; }
    public string Agencia { get; private set; } = default!;
    public string Numero { get; private set; } = default!;
    public TipoContaBancaria Tipo { get; private set; }
    public Moeda Moeda { get; private set; }
    public string Apelido { get; private set; } = default!;
    public bool Ativa { get; private set; }

    public Instant CreatedAt { get; private set; }
    public Instant UpdatedAt { get; private set; }
    public Instant? DeletedAt { get; private set; }

    private ContaBancaria() { }

    public static ContaBancaria Criar(
        Guid bancoId, string agencia, string numero,
        TipoContaBancaria tipo, Moeda moeda, string apelido,
        IClock clock)
    {
        if (bancoId == Guid.Empty)
            throw new ArgumentException("BancoId obrigatório.", nameof(bancoId));
        if (string.IsNullOrWhiteSpace(agencia))
            throw new ArgumentException("Agência obrigatória.", nameof(agencia));
        if (string.IsNullOrWhiteSpace(numero))
            throw new ArgumentException("Número obrigatório.", nameof(numero));
        if (string.IsNullOrWhiteSpace(apelido))
            throw new ArgumentException("Apelido obrigatório.", nameof(apelido));

        Instant agora = clock.GetCurrentInstant();

        return new ContaBancaria
        {
            BancoId = bancoId,
            Agencia = agencia.Trim(),
            Numero = numero.Trim(),
            Tipo = tipo,
            Moeda = moeda,
            Apelido = apelido.Trim(),
            Ativa = true,
            CreatedAt = agora,
            UpdatedAt = agora,
        };
    }

    public void Atualizar(TipoContaBancaria tipo, string apelido, IClock clock)
    {
        if (string.IsNullOrWhiteSpace(apelido))
            throw new ArgumentException(nameof(apelido));

        Tipo = tipo;
        Apelido = apelido.Trim();
        UpdatedAt = clock.GetCurrentInstant();
    }

    public void Desativar(IClock clock)
    {
        if (!Ativa) return;
        Ativa = false;
        DeletedAt = clock.GetCurrentInstant();
        UpdatedAt = DeletedAt.Value;
    }

    public void Reativar(IClock clock)
    {
        if (Ativa) return;
        Ativa = true;
        DeletedAt = null;
        UpdatedAt = clock.GetCurrentInstant();
    }
}
```

**Observação:** `BancoId`, `Agencia`, `Numero`, `Moeda` são imutáveis após criação. Mudar conta implica criar nova e desativar antiga.

### 2.3 Repositório

```csharp
public interface IContaBancariaRepository
{
    Task<ContaBancaria?> GetAsync(Guid id, CancellationToken ct);
    Task<ContaBancaria?> GetByBancoAgenciaNumeroAsync(Guid bancoId, string agencia, string numero, CancellationToken ct);
    Task<IReadOnlyList<ContaBancaria>> ListAsync(Guid? bancoId, bool incluirInativas, CancellationToken ct);
    Task AddAsync(ContaBancaria conta, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
```

---

## 3. Schema PostgreSQL

```sql
CREATE TABLE conta_bancaria (
    id          UUID PRIMARY KEY,
    banco_id    UUID NOT NULL REFERENCES banco(id),
    agencia     TEXT NOT NULL,
    numero      TEXT NOT NULL,
    tipo        SMALLINT NOT NULL,
    moeda       SMALLINT NOT NULL,
    apelido     TEXT NOT NULL,
    ativa       BOOLEAN NOT NULL DEFAULT TRUE,
    created_at  TIMESTAMPTZ NOT NULL,
    updated_at  TIMESTAMPTZ NOT NULL,
    deleted_at  TIMESTAMPTZ NULL,
    CONSTRAINT uq_conta_bancaria_natural UNIQUE (banco_id, agencia, numero)
);

CREATE INDEX ix_conta_bancaria_banco_ativa ON conta_bancaria (banco_id, ativa);
```

`Apelido` não tem unique — usuário pode repetir "Conta principal" entre bancos.

---

## 4. Endpoints

| Método | Path | Auth Policy |
|--------|------|-------------|
| GET | `/api/v1/contas-bancarias` | `Policies.Leitura` |
| GET | `/api/v1/contas-bancarias/{id}` | `Policies.Leitura` |
| POST | `/api/v1/contas-bancarias` | `Policies.Escrita` |
| PUT | `/api/v1/contas-bancarias/{id}` | `Policies.Escrita` |
| DELETE | `/api/v1/contas-bancarias/{id}` | `Policies.Gerencial` |

### 4.1 DTOs

```csharp
public sealed record ContaBancariaDto(
    Guid Id,
    Guid BancoId,
    string BancoApelido,
    string Agencia,
    string Numero,
    string Tipo,
    string Moeda,
    string Apelido,
    bool Ativa,
    Instant CreatedAt,
    Instant UpdatedAt);

public sealed record CreateContaBancariaRequest(
    Guid BancoId,
    string Agencia,
    string Numero,
    string Tipo,
    string Moeda,
    string Apelido);

public sealed record UpdateContaBancariaRequest(
    string Tipo,
    string Apelido);
```

### 4.2 `GET /api/v1/contas-bancarias`

Query params: `bancoId` (opcional), `incluirInativas` (bool, default `false`).
Response: `EnvelopeResponse<IReadOnlyList<ContaBancariaDto>>`.

### 4.3 `POST /api/v1/contas-bancarias`

Header `Idempotency-Key` recomendado. Retorna 201 com `ContaBancariaDto`. 400 se faltar campos; 409 se já existe `(bancoId, agencia, numero)`.

### 4.4 `PUT /api/v1/contas-bancarias/{id}`

Atualiza apenas `Tipo` e `Apelido`. Demais campos imutáveis. 404 se não existe.

### 4.5 `DELETE /api/v1/contas-bancarias/{id}`

**Soft delete** — chama `Desativar()`. Idempotente: deletar conta já desativada retorna 204. 404 se nunca existiu.

---

## 5. Validações

- `Agencia` e `Numero` aceitam dígitos, hífens e barras — sem máscara enforced; FE deve normalizar.
- `Apelido` máx 100 caracteres.
- `BancoId` deve referenciar `banco` ativo.
- Moeda BRL/USD/EUR/JPY/CNY (mesma enum existente).
- Tentativa de criar com `bancoId + agencia + numero` duplicado → 409 com `detail: "Conta já cadastrada"`.

---

## 6. Casos de Borda

| Cenário | Comportamento |
|---------|---------------|
| Criar conta com banco desativado | 400 `detail: "Banco inativo"` |
| Soft-delete + recriar com mesma natural key | 409 (a conta inativa ainda ocupa a chave). Operação correta: reativar via `PUT` |
| Listar com `incluirInativas=true` | Retorna todas as contas inclusive `Ativa = false` |
| `PUT` alterando `BancoId` ou `Numero` | Campo ignorado (não está no request); para mudar, soft-delete + criar nova |
| Conta com `SaldoCaixa` registrado e tenta DELETE | Permitido (soft delete); saldos antigos permanecem para histórico |

---

## 7. Critérios de Aceite

- [ ] Agregado `ContaBancaria` com fábrica, `Atualizar`, `Desativar`, `Reativar`.
- [ ] Repositório `IContaBancariaRepository` + impl EF Core.
- [ ] Migration cria tabela com unique constraint.
- [ ] CRUD HTTP funcional.
- [ ] Soft delete preserva chave natural (não pode recriar duplicata).
- [ ] AuditLog em criação, alteração, desativação.

---

## 8. Verificação

```bash
dotnet test --filter "FullyQualifiedName~ContaBancaria"

# Smoke
curl -X POST http://localhost:5000/api/v1/contas-bancarias \
  -H "Authorization: Bearer ..." \
  -H "Content-Type: application/json" \
  -d '{"bancoId":"...","agencia":"0001","numero":"12345-6","tipo":"Corrente","moeda":"BRL","apelido":"Conta principal Itaú"}'
```

**Teste-chave:**

```csharp
[Fact]
public async Task Create_quando_natural_key_duplicada_retorna_409()
{
    await PostContaBancaria(bancoId: _itau, agencia: "0001", numero: "12345-6");
    var response = await PostContaBancaria(bancoId: _itau, agencia: "0001", numero: "12345-6");

    response.StatusCode.Should().Be(HttpStatusCode.Conflict);
}
```

---

## 9. Boundaries específicas

### 9.1 Always do
- Soft delete (nunca DELETE físico).
- Trim de strings antes de persistir.
- AuditLog em mutações.

### 9.2 Ask first
- Adicionar campo de "saldo em tempo real" — vai contra a separação de Conta vs SaldoCaixa.
- Permitir mudança de moeda — exige migração de saldos históricos.

### 9.3 Never do
- Hard delete: dados de `SaldoCaixa` apontariam para FK órfã.
- Permitir 2 contas com mesma `(banco, agencia, numero)`.

---

## 10. Arquivos esperados

- `src/Sgcf.Domain/Tesouraria/ContaBancaria.cs`
- `src/Sgcf.Domain/Tesouraria/TipoContaBancaria.cs`
- `src/Sgcf.Application/Tesouraria/IContaBancariaRepository.cs`
- `src/Sgcf.Application/Tesouraria/ContaBancariaDto.cs`
- `src/Sgcf.Application/Tesouraria/Queries/ListContasBancariasQuery.cs` + Handler
- `src/Sgcf.Application/Tesouraria/Queries/GetContaBancariaQuery.cs` + Handler
- `src/Sgcf.Application/Tesouraria/Commands/CreateContaBancariaCommand.cs` + Handler
- `src/Sgcf.Application/Tesouraria/Commands/UpdateContaBancariaCommand.cs` + Handler
- `src/Sgcf.Application/Tesouraria/Commands/DesativarContaBancariaCommand.cs` + Handler
- `src/Sgcf.Api/Controllers/ContasBancariasController.cs`
- `src/Sgcf.Infrastructure/Persistence/Configurations/ContaBancariaConfiguration.cs`
- `src/Sgcf.Infrastructure/Persistence/Repositories/ContaBancariaRepository.cs`
- `src/Sgcf.Infrastructure/Migrations/<ts>_AddContasBancarias.cs`
- `tests/Sgcf.Domain.Tests/Tesouraria/ContaBancariaTests.cs`
- `tests/Sgcf.Api.IntegrationTests/ContasBancariasControllerTests.cs`
