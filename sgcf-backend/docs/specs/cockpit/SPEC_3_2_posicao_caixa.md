# SPEC — Task 3.2 — GAP-CKP-08 — Posição de Caixa Consolidada

> **Master:** `SPEC.md`
> **Plano:** `tasks/plan_cockpit_backend_gaps.md` Task 3.2
> **Status:** Draft
> **Versão:** v1.0
> **Escopo:** L
> **Persona:** Gerente de Tesouraria
> **Dependências:** Tasks 0.1, 3.1

---

## 1. Objetivo

Entregar o card "Posição de Caixa Consolidada" do cockpit Tesouraria (UX §13.3) — saldo em D+0 por banco/moeda/conta, com **input manual editável por data** (decisão sponsor 2026-05-20). Integração OFX/CNAB fica para Fase 2.

---

## 2. Modelo de Domínio

### 2.1 Agregado `SaldoCaixa`

```csharp
namespace Sgcf.Domain.Tesouraria;

public sealed class SaldoCaixa : Entity, IAuditable
{
    public Guid ContaId { get; private set; }
    public LocalDate DataReferencia { get; private set; }
    internal decimal ValorDecimal { get; private set; }
    public Moeda Moeda { get; private set; }
    public Money Valor => new(ValorDecimal, Moeda);

    public string RegistradoPor { get; private set; } = default!;
    public Instant RegistradoEm { get; private set; }
    public Instant CreatedAt { get; private set; }
    public Instant UpdatedAt { get; private set; }

    private SaldoCaixa() { }

    public static SaldoCaixa Criar(
        Guid contaId, LocalDate dataReferencia,
        Money valor, string usuario, IClock clock)
    {
        if (contaId == Guid.Empty)
            throw new ArgumentException(nameof(contaId));
        if (string.IsNullOrWhiteSpace(usuario))
            throw new ArgumentException(nameof(usuario));

        Instant agora = clock.GetCurrentInstant();
        return new SaldoCaixa
        {
            ContaId = contaId,
            DataReferencia = dataReferencia,
            ValorDecimal = Math.Round(valor.Valor, 6, MidpointRounding.AwayFromZero),
            Moeda = valor.Moeda,
            RegistradoPor = usuario,
            RegistradoEm = agora,
            CreatedAt = agora,
            UpdatedAt = agora,
        };
    }

    public void Atualizar(Money novoValor, string usuario, IClock clock)
    {
        if (novoValor.Moeda != Moeda)
            throw new InvalidOperationException("Moeda do saldo não pode ser alterada.");

        ValorDecimal = Math.Round(novoValor.Valor, 6, MidpointRounding.AwayFromZero);
        RegistradoPor = usuario;
        RegistradoEm = clock.GetCurrentInstant();
        UpdatedAt = RegistradoEm;
    }
}
```

**Edição retroativa:** atualizar `(contaId, dataReferencia)` existente substitui o valor. `AuditLog` registra `valorAntes` e `valorDepois`.

### 2.2 Repositório

```csharp
public interface ISaldoCaixaRepository
{
    Task<SaldoCaixa?> GetAsync(Guid contaId, LocalDate dataRef, CancellationToken ct);
    Task<IReadOnlyList<SaldoCaixa>> ListPorDataAsync(LocalDate dataRef, CancellationToken ct);
    Task<IReadOnlyList<SaldoCaixa>> ListPorContaPeriodoAsync(
        Guid contaId, LocalDate de, LocalDate ate, CancellationToken ct);
    Task UpsertAsync(SaldoCaixa saldo, CancellationToken ct);
}
```

`UpsertAsync` faz `INSERT ... ON CONFLICT (conta_id, data_referencia) DO UPDATE` ou equivalente EF Core (`AddOrUpdate` manual).

---

## 3. Schema PostgreSQL

```sql
CREATE TABLE saldo_caixa (
    id              UUID PRIMARY KEY,
    conta_id        UUID NOT NULL REFERENCES conta_bancaria(id),
    data_referencia DATE NOT NULL,
    valor_decimal   NUMERIC(20, 6) NOT NULL,
    moeda           SMALLINT NOT NULL,
    registrado_por  TEXT NOT NULL,
    registrado_em   TIMESTAMPTZ NOT NULL,
    created_at      TIMESTAMPTZ NOT NULL,
    updated_at      TIMESTAMPTZ NOT NULL,
    CONSTRAINT uq_saldo_caixa_conta_data UNIQUE (conta_id, data_referencia)
);

CREATE INDEX ix_saldo_caixa_data ON saldo_caixa (data_referencia);
CREATE INDEX ix_saldo_caixa_conta_data ON saldo_caixa (conta_id, data_referencia DESC);
```

Limite de edição retroativa: **refinamento aberto** (`SPEC.md` §10). Default proposto enquanto não há decisão: **sem limite** — qualquer data passada pode ser editada. Caso a decisão final seja D-30 ou D-90, validação será adicionada no command.

---

## 4. Endpoints

| Método | Path | Auth Policy |
|--------|------|-------------|
| GET | `/api/v1/tesouraria/saldos` | `Policies.Leitura` |
| POST | `/api/v1/tesouraria/saldos` | `Policies.Escrita` |
| GET | `/api/v1/tesouraria/posicao-caixa` | `Policies.Leitura` |

### 4.1 DTOs

```csharp
public sealed record SaldoCaixaDto(
    Guid Id,
    Guid ContaId,
    LocalDate DataReferencia,
    decimal Valor,
    string Moeda,
    string RegistradoPor,
    Instant RegistradoEm);

public sealed record UpsertSaldoCaixaItem(
    Guid ContaId,
    LocalDate DataReferencia,
    decimal Valor,
    string Moeda);

public sealed record UpsertSaldosCaixaRequest(
    IReadOnlyList<UpsertSaldoCaixaItem> Itens);

public sealed record PosicaoCaixaDto(
    LocalDate DataReferencia,
    decimal SaldoConsolidadoBrl,
    IReadOnlyList<SaldoPorMoedaDto> PorMoeda,
    IReadOnlyList<SaldoPorBancoDto> PorBanco);

public sealed record SaldoPorMoedaDto(
    string Moeda,
    decimal Saldo,
    decimal SaldoBrl,
    decimal CotacaoAplicada);

public sealed record SaldoPorBancoDto(
    Guid BancoId,
    string BancoApelido,
    decimal SaldoBrl,
    IReadOnlyList<SaldoPorContaDto> Contas);

public sealed record SaldoPorContaDto(
    Guid ContaId,
    string Agencia,
    string Numero,
    string Apelido,
    string Tipo,
    string Moeda,
    decimal Saldo,
    decimal SaldoBrl,
    LocalDate DataUltimoRegistro);
```

### 4.2 `GET /api/v1/tesouraria/saldos`

Query params:

| Param | Tipo | Obrigatório |
|-------|------|--------------|
| `contaId` | guid | sim |
| `dataDe` | date | sim |
| `dataAte` | date | sim |

Retorna série histórica de saldos para a conta, ordenada por `dataReferencia DESC`. Limite: `(dataAte - dataDe) ≤ 365 dias`, senão 400.

### 4.3 `POST /api/v1/tesouraria/saldos`

Body com **batch** de itens. Header `Idempotency-Key` recomendado.

Cada item:

- Faz upsert por `(contaId, dataReferencia)`.
- Valida que `moeda` bate com `ContaBancaria.Moeda` da conta.
- Registra `AuditLog` com `valorAntes`/`valorDepois`.

Response 200:

```json
{
  "data": {
    "criados": 3,
    "atualizados": 2,
    "rejeitados": [{"contaId": "...", "dataReferencia": "2026-04-01", "motivo": "Conta inativa"}]
  },
  "meta": {...}
}
```

Rejeições não interrompem o lote — operação parcial é aceitável.

### 4.4 `GET /api/v1/tesouraria/posicao-caixa`

Query params:

| Param | Tipo | Default |
|-------|------|---------|
| `dataReferencia` | date | hoje BRT |

**Regra de busca:** para cada conta ativa, busca o **último saldo registrado ≤ dataReferencia**. Se nenhum existe, exclui a conta e adiciona `FonteConsultada` com status `DEGRADADO`.

Conversão para BRL via spot/PTAX (mesma estratégia). Caso a `dataReferencia` seja passada, usa PTAX da data específica (`ICotacaoFxRepository.GetMaisRecenteAsync(moeda, PtaxD1, dataReferencia, ct)`).

---

## 5. Regras de Cálculo

### 5.1 `saldoConsolidadoBrl`

`Σ SaldoBrl` de todas as contas com saldo encontrado.

### 5.2 `porMoeda`

Agrupa por `Moeda` da conta. `saldo` é soma na moeda original; `saldoBrl` é soma após conversão.

### 5.3 `porBanco`

Agrupa por `BancoId`. Para cada banco, lista as contas com último saldo. `bancoApelido` lido de `Banco`.

### 5.4 Completude

| Situação | `meta.completude` |
|----------|--------------------|
| Todas as contas ativas têm saldo em `dataReferencia` | `COMPLETO` |
| Pelo menos uma conta com saldo mais antigo (> 24 h antes da `dataReferencia`) | `PARCIAL` (com `FonteConsultada` indicando contas defasadas) |
| Conta ativa sem nenhum saldo registrado | `PARCIAL` (não inclui a conta no payload mas registra em fontes) |

---

## 6. Casos de Borda

| Cenário | Comportamento |
|---------|---------------|
| `dataReferencia` futura | 400 `detail: "dataReferencia não pode ser futura"` |
| Conta inativa (`Ativa = false`) com saldo registrado | Excluída do `posicao-caixa`; ainda visível em `GET /saldos?contaId=...` |
| Saldo zerado (valor 0) | Incluído normalmente |
| Saldo negativo (cheque especial) | Aceito; FE exibe em vermelho |
| Conta em USD sem cotação na `dataReferencia` | Excluída do `porMoeda`; `FonteConsultada DEGRADADO`; `completude: PARCIAL` |
| Upsert com mesma `(conta, data)` em dois itens do batch | Aplica o último (LIFO), registra warning em log |
| Conta criada após `dataReferencia` (futuro: 2026-06, query 2026-05) | Excluída do payload |

---

## 7. Performance

- `GET /posicao-caixa` faz uma query com `DISTINCT ON (conta_id) ... ORDER BY conta_id, data_referencia DESC`.
- `GET /saldos` paginação via limit 365 dias.
- `Cache-Control: max-age=30, private` no `/posicao-caixa` (cache curto pois saldos podem mudar manualmente a qualquer momento).

---

## 8. Critérios de Aceite

- [ ] Agregado `SaldoCaixa` com `Criar` e `Atualizar`.
- [ ] Migration `<ts>_AddSaldoCaixa.cs` com unique constraint.
- [ ] `POST /tesouraria/saldos` aceita batch com upsert idempotente.
- [ ] `POST` valida moeda contra `ContaBancaria.Moeda`.
- [ ] `GET /tesouraria/saldos` retorna série histórica.
- [ ] `GET /posicao-caixa` aceita `dataReferencia` opcional.
- [ ] Soma `Σ porBanco.saldoBrl == saldoConsolidadoBrl`.
- [ ] Soma `Σ porMoeda.saldoBrl == saldoConsolidadoBrl`.
- [ ] AuditLog registra edições com `valorAntes`/`valorDepois`.
- [ ] `completude: PARCIAL` quando há contas defasadas.

---

## 9. Verificação

```bash
dotnet test --filter "FullyQualifiedName~PosicaoCaixa"
dotnet test --filter "FullyQualifiedName~SaldoCaixa"

# Smoke: registrar saldo + consultar posição
curl -X POST .../tesouraria/saldos -d '{"itens":[{"contaId":"...","dataReferencia":"2026-05-19","valor":15000000,"moeda":"BRL"}]}'
curl .../tesouraria/posicao-caixa
```

**Teste-chave (edição retroativa):**

```csharp
[Fact]
public async Task PostSaldo_quando_data_existente_atualiza_e_audita()
{
    Guid contaId = await CriarConta();
    await PostSaldo(contaId, "2026-05-19", 10_000_000m);

    await PostSaldo(contaId, "2026-05-19", 12_500_000m);

    var saldo = await GetSaldo(contaId, "2026-05-19");
    saldo.Valor.Should().Be(12_500_000m);

    var auditEvents = await GetAuditLog("SaldoCaixa", saldo.Id);
    auditEvents.Should().Contain(e => e.Detalhes.Contains("valorAntes") && e.Detalhes.Contains("10000000"));
}
```

**Teste de consistência:**

```csharp
[Fact]
public async Task PosicaoCaixa_soma_por_banco_bate_com_consolidado()
{
    Setup3ContasEmBancosDistintos();
    var result = await _mediator.Send(new GetPosicaoCaixaQuery(null));

    result.Data.PorBanco.Sum(b => b.SaldoBrl).Should().BeApproximately(result.Data.SaldoConsolidadoBrl, 0.05m);
}
```

---

## 10. Boundaries específicas

### 10.1 Always do
- Validar moeda do saldo contra `ContaBancaria.Moeda`.
- `AuditLog` com `valorAntes`/`valorDepois` em edições.
- Usar `IClock` para `RegistradoEm`.

### 10.2 Ask first
- Impor limite de edição retroativa (D-30, D-90 etc.) — refinar com Tesouraria.
- Adicionar campo "fonte do saldo" (manual, OFX, CNAB) — entra na Fase 2.
- Permitir saldo em moeda diferente da conta.

### 10.3 Never do
- Apagar `SaldoCaixa` (DELETE). Sobrescrever via upsert.
- Calcular cotação spot quando `dataReferencia` é passada — sempre PTAX da data.
- Persistir saldo de conta inativa criada após a data.

---

## 11. Arquivos esperados

- `src/Sgcf.Domain/Tesouraria/SaldoCaixa.cs`
- `src/Sgcf.Application/Tesouraria/ISaldoCaixaRepository.cs`
- `src/Sgcf.Application/Tesouraria/Commands/UpsertSaldosCaixaCommand.cs` + Handler
- `src/Sgcf.Application/Tesouraria/Queries/ListSaldosCaixaQuery.cs` + Handler
- `src/Sgcf.Application/Tesouraria/Queries/GetPosicaoCaixaQuery.cs` + Handler
- `src/Sgcf.Application/Tesouraria/PosicaoCaixaDto.cs`
- `src/Sgcf.Api/Controllers/TesourariaController.cs`
- `src/Sgcf.Infrastructure/Persistence/Configurations/SaldoCaixaConfiguration.cs`
- `src/Sgcf.Infrastructure/Persistence/Repositories/SaldoCaixaRepository.cs`
- `src/Sgcf.Infrastructure/Migrations/<ts>_AddSaldoCaixa.cs`
- `tests/Sgcf.Domain.Tests/Tesouraria/SaldoCaixaTests.cs`
- `tests/Sgcf.Application.Tests/Tesouraria/PosicaoCaixaTests.cs`
- `tests/Sgcf.Api.IntegrationTests/TesourariaControllerSaldosTests.cs`
