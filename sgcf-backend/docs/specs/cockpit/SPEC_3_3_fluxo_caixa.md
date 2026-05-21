# SPEC — Task 3.3 — GAP-CKP-09 — Fluxo de Caixa Projetado Diário

> **Master:** `SPEC.md`
> **Plano:** `tasks/plan_cockpit_backend_gaps.md` Task 3.3
> **Status:** Draft
> **Versão:** v1.0
> **Escopo:** M
> **Persona:** Gerente de Tesouraria
> **Dependências:** Tasks 0.1, 3.1, 3.2

---

## 1. Objetivo

Entregar o card "Fluxo de Caixa Projetado (D+1 a D+90)" do cockpit Tesouraria (UX §13.3) — projeção diária consolidando saídas de cronograma de contratos com eventos manuais (recebíveis previstos, aportes, despesas extras).

`GET /painel/vencimentos?ano=YYYY` cobre granularidade mensal; este endpoint entrega **diário**.

---

## 2. Modelo de Domínio

### 2.1 Enum `TipoEventoFluxoCaixa`

```csharp
namespace Sgcf.Domain.Tesouraria;

public enum TipoEventoFluxoCaixa : byte
{
    // Entradas
    RecebivelPrevisto    = 1,
    AporteSocio          = 2,
    Desembolso           = 3,  // novo contrato/cotação aprovada
    Outra_Entrada        = 4,

    // Saídas
    AmortizacaoContrato  = 5,  // gerado automaticamente do EventoCronograma
    JurosContrato        = 6,  // gerado automaticamente do EventoCronograma
    DespesaOperacional   = 7,
    PagamentoFornecedor  = 8,
    Outra_Saida          = 9,
}
```

### 2.2 Agregado `EventoFluxoCaixa`

```csharp
public sealed class EventoFluxoCaixa : Entity, IAuditable
{
    public LocalDate Data { get; private set; }
    public TipoEventoFluxoCaixa Tipo { get; private set; }
    public string Descricao { get; private set; } = default!;
    internal decimal ValorDecimal { get; private set; }  // assinado: positivo entrada, negativo saída
    public Moeda Moeda { get; private set; }
    public Money Valor => new(ValorDecimal, Moeda);

    public Guid? ContaId { get; private set; }            // opcional: conta destino/origem
    public Guid? ContratoId { get; private set; }         // opcional: vínculo a contrato
    public bool Manual { get; private set; }              // true = input usuário; false = derivado de cronograma
    public string CriadoPor { get; private set; } = default!;
    public Instant CreatedAt { get; private set; }
    public Instant UpdatedAt { get; private set; }
    public Instant? DeletedAt { get; private set; }

    public static EventoFluxoCaixa CriarManual(
        LocalDate data, TipoEventoFluxoCaixa tipo, string descricao,
        Money valor, Guid? contaId, Guid? contratoId,
        string usuario, IClock clock)
    {
        if (string.IsNullOrWhiteSpace(descricao))
            throw new ArgumentException(nameof(descricao));
        if (valor.Valor == 0)
            throw new ArgumentException("Valor não pode ser zero.");
        if (IsSaida(tipo) && valor.Valor > 0)
            throw new ArgumentException("Saída deve ter valor negativo.");
        if (IsEntrada(tipo) && valor.Valor < 0)
            throw new ArgumentException("Entrada deve ter valor positivo.");

        // ... construtor
    }
}
```

`AmortizacaoContrato` e `JurosContrato` **não são persistidos** em `EventoFluxoCaixa` — são **derivados em runtime** do `EventoCronograma` existente. O agregado serve para eventos **extra-cronograma**.

### 2.3 Repositório

```csharp
public interface IEventoFluxoCaixaRepository
{
    Task<IReadOnlyList<EventoFluxoCaixa>> ListEntrePeriodoAsync(
        LocalDate de, LocalDate ate, CancellationToken ct);
    Task AddAsync(EventoFluxoCaixa evento, CancellationToken ct);
    Task RemoveAsync(Guid eventoId, IClock clock, CancellationToken ct); // soft delete
}
```

---

## 3. Schema PostgreSQL

```sql
CREATE TABLE evento_fluxo_caixa (
    id            UUID PRIMARY KEY,
    data          DATE NOT NULL,
    tipo          SMALLINT NOT NULL,
    descricao     TEXT NOT NULL,
    valor_decimal NUMERIC(20, 6) NOT NULL,
    moeda         SMALLINT NOT NULL,
    conta_id      UUID NULL REFERENCES conta_bancaria(id),
    contrato_id   UUID NULL REFERENCES contrato(id),
    manual        BOOLEAN NOT NULL DEFAULT TRUE,
    criado_por    TEXT NOT NULL,
    created_at    TIMESTAMPTZ NOT NULL,
    updated_at    TIMESTAMPTZ NOT NULL,
    deleted_at    TIMESTAMPTZ NULL
);

CREATE INDEX ix_evento_fluxo_data ON evento_fluxo_caixa (data) WHERE deleted_at IS NULL;
```

---

## 4. Endpoints

| Método | Path | Auth Policy |
|--------|------|-------------|
| GET | `/api/v1/tesouraria/fluxo-caixa` | `Policies.Leitura` |
| POST | `/api/v1/tesouraria/eventos-fluxo` | `Policies.Escrita` |
| DELETE | `/api/v1/tesouraria/eventos-fluxo/{id}` | `Policies.Escrita` |

### 4.1 `GET /api/v1/tesouraria/fluxo-caixa`

Query params:

| Param | Tipo | Default |
|-------|------|---------|
| `dataDe` | date | hoje BRT |
| `dataAte` | date | hoje + 90d |
| `granularidade` | string | `dia` (única opção MVP) |

Validação: `dataAte - dataDe ≤ 90 dias`.

DTO:

```csharp
public sealed record FluxoCaixaDto(
    decimal SaldoInicialBrl,
    IReadOnlyList<DiaFluxoCaixaDto> Dias,
    decimal? GapLiquidezBrl);

public sealed record DiaFluxoCaixaDto(
    LocalDate Data,
    decimal EntradasBrl,
    decimal SaidasBrl,
    decimal SaldoProjetadoBrl,
    IReadOnlyList<EventoFluxoCaixaDto> Eventos,
    IReadOnlyList<string> Alertas);

public sealed record EventoFluxoCaixaDto(
    string Tipo,
    string Descricao,
    decimal ValorBrl,
    Guid? ContratoId);
```

`GapLiquidezBrl` é o menor `SaldoProjetadoBrl` observado no período — null se sempre positivo.

### 4.2 `POST /api/v1/tesouraria/eventos-fluxo`

Body single ou batch:

```json
{
  "data": "2026-05-25",
  "tipo": "RecebivelPrevisto",
  "descricao": "Recebimento cliente ABC",
  "valor": 1200000.00,
  "moeda": "BRL",
  "contaId": null,
  "contratoId": null
}
```

Idempotência via `Idempotency-Key`. Retorna 201.

### 4.3 `DELETE /api/v1/tesouraria/eventos-fluxo/{id}`

Soft delete. Idempotente. 404 se nunca existiu.

---

## 5. Regras de Cálculo

### 5.1 Saldo inicial

`saldoInicialBrl` = `GetPosicaoCaixaQuery(dataDe.PlusDays(-1)).SaldoConsolidadoBrl`. Se não há posição registrada, usa `dataDe` e marca `meta.completude = PARCIAL`.

### 5.2 Eventos por dia

Para cada dia entre `dataDe` e `dataAte`:

1. **Eventos automáticos:** lê `EventoCronograma` da `cronograma_pagamento` com `DataVencimento = dia` e `Status = Pendente`. Converte cada um em `EventoFluxoCaixaDto` com `Tipo = AmortizacaoContrato` ou `JurosContrato`, valor negativo (saída).
2. **Eventos manuais:** lê `EventoFluxoCaixa` ativos com `Data = dia`.
3. Soma `entradasBrl` (valores positivos) e `saidasBrl` (valores negativos em módulo).
4. `saldoProjetadoBrl = saldoProjetadoBrl_anterior + entradasBrl - saidasBrl` (anterior = saldoInicialBrl no primeiro dia).

### 5.3 Conversão para BRL

Eventos em moeda estrangeira: usa cotação **da data do evento** via PTAX (não spot — projeção). Eventos com `data > hoje` usam PTAX D-1 do dia atual como aproximação (não há PTAX futura).

### 5.4 Alertas no dia

Gerados em runtime no DTO (não persistidos):

- `SALDO_NEGATIVO` se `saldoProjetadoBrl < 0`.
- `SALDO_BAIXO` se `0 ≤ saldoProjetadoBrl < 0.1 * saldoInicialBrl`.

Esses são **distintos** dos `Alerta` persistidos pelo rules engine — são informativos in-payload.

---

## 6. Casos de Borda

| Cenário | Comportamento |
|---------|---------------|
| `dataDe > dataAte` | 400 |
| `dataAte - dataDe > 90` | 400 |
| Sem posição de caixa registrada | `saldoInicialBrl = 0`, `completude: PARCIAL` |
| Dia sem nenhum evento | Incluído com `entradasBrl: 0, saidasBrl: 0`, `saldoProjetadoBrl` herdado do anterior |
| Evento manual em data passada | Aceito — usuário pode registrar histórico (raro, mas permitido) |
| Cronograma com parcela em moeda estrangeira sem PTAX | Conta com BRL = 0; `completude: PARCIAL` |
| Evento criado e deletado no mesmo dia | Não aparece no fluxo (soft delete) |
| Granularidade ≠ `dia` | 400 (MVP só suporta diária) |

---

## 7. Critérios de Aceite

- [ ] Agregado `EventoFluxoCaixa` + repositório + EF Core config + migration.
- [ ] `GET /fluxo-caixa` retorna envelope com `saldoInicial`, lista de dias, `gapLiquidez`.
- [ ] `POST /eventos-fluxo` cria evento manual (single ou batch).
- [ ] `DELETE /eventos-fluxo/{id}` é soft delete idempotente.
- [ ] Cronograma de contratos é incluído automaticamente nas saídas.
- [ ] Cálculo do saldo projetado é cumulativo dia-a-dia.
- [ ] Alertas `SALDO_NEGATIVO` e `SALDO_BAIXO` emitidos no DTO.
- [ ] `gapLiquidezBrl` = menor `saldoProjetadoBrl` do período (null se positivo).

---

## 8. Verificação

```bash
dotnet test --filter "FullyQualifiedName~FluxoCaixa"
```

**Teste-chave (cálculo cumulativo):**

```csharp
[Fact]
public async Task FluxoCaixa_calcula_saldo_cumulativo()
{
    // Saldo inicial 10mi
    // D+3: -8mi (amortização)
    // D+5: +5mi (recebível)
    // Esperado D+5: 10 - 8 + 5 = 7mi

    SetupPosicaoCaixa(brl: 10_000_000m);
    SetupCronogramaSaida(diasAhead: 3, valor: 8_000_000m);
    await PostEventoManual(diasAhead: 5, tipo: "RecebivelPrevisto", valor: 5_000_000m);

    var result = await _mediator.Send(new GetFluxoCaixaQuery(_hoje, _hoje.PlusDays(7)));

    result.Data.Dias.First(d => d.Data == _hoje.PlusDays(5))
        .SaldoProjetadoBrl.Should().Be(7_000_000m);
}

[Fact]
public async Task FluxoCaixa_emite_alerta_SALDO_NEGATIVO()
{
    SetupPosicaoCaixa(brl: 1_000_000m);
    SetupCronogramaSaida(diasAhead: 2, valor: 2_000_000m);

    var result = await _mediator.Send(new GetFluxoCaixaQuery(_hoje, _hoje.PlusDays(7)));

    var diaNegativo = result.Data.Dias.First(d => d.Data == _hoje.PlusDays(2));
    diaNegativo.Alertas.Should().Contain("SALDO_NEGATIVO");
    result.Data.GapLiquidezBrl.Should().Be(-1_000_000m);
}
```

---

## 9. Boundaries específicas

### 9.1 Always do
- Manter assinatura de `valor` consistente com tipo (entrada positiva, saída negativa).
- Soft delete em `EventoFluxoCaixa`.
- AuditLog em criação/deleção.

### 9.2 Ask first
- Granularidade semanal/mensal.
- Horizonte > 90 dias (impacta payload e precisão).
- Inferir entradas a partir de receitas operacionais sem cadastro manual.

### 9.3 Never do
- Persistir eventos derivados de `EventoCronograma` (gera duplicidade).
- Permitir evento com valor zero.
- Permitir mudança de tipo após criação (cria viés histórico).

---

## 10. Arquivos esperados

- `src/Sgcf.Domain/Tesouraria/EventoFluxoCaixa.cs`
- `src/Sgcf.Domain/Tesouraria/TipoEventoFluxoCaixa.cs`
- `src/Sgcf.Application/Tesouraria/IEventoFluxoCaixaRepository.cs`
- `src/Sgcf.Application/Tesouraria/Commands/CriarEventoFluxoCaixaCommand.cs` + Handler
- `src/Sgcf.Application/Tesouraria/Commands/RemoverEventoFluxoCaixaCommand.cs` + Handler
- `src/Sgcf.Application/Tesouraria/Queries/GetFluxoCaixaQuery.cs` + Handler
- `src/Sgcf.Application/Tesouraria/FluxoCaixaDto.cs`
- `src/Sgcf.Api/Controllers/TesourariaController.cs` (endpoints fluxo)
- `src/Sgcf.Infrastructure/Persistence/Configurations/EventoFluxoCaixaConfiguration.cs`
- `src/Sgcf.Infrastructure/Persistence/Repositories/EventoFluxoCaixaRepository.cs`
- `src/Sgcf.Infrastructure/Migrations/<ts>_AddEventoFluxoCaixa.cs`
- `tests/Sgcf.Application.Tests/Tesouraria/FluxoCaixaTests.cs`
- `tests/Sgcf.Api.IntegrationTests/TesourariaControllerFluxoCaixaTests.cs`
