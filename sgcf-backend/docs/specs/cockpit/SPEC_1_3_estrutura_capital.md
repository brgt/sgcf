# SPEC — Task 1.3 — GAP-CKP-04 — Estrutura de Capital

> **Master:** `SPEC.md`
> **Plano:** `tasks/plan_cockpit_backend_gaps.md` Task 1.3
> **Status:** Draft
> **Versão:** v1.0
> **Escopo:** M
> **Persona:** CFO
> **Dependências:** Tasks 0.1, 0.2, 0.3

---

## 1. Objetivo

Entregar o card "Estrutura de Capital" do cockpit CFO (UX §13.1) — indicadores Dívida/EBITDA, Dívida/Patrimônio Líquido e ICR (Interest Coverage Ratio). O endpoint atual `POST /painel/ebitda` cobre apenas EBITDA; faltam cadastros mensais de Patrimônio Líquido e Despesa Financeira.

---

## 2. Modelo de Domínio

### 2.1 Entidade `DadosContabeisMensal`

```csharp
namespace Sgcf.Domain.Contabilidade;

public sealed class DadosContabeisMensal : Entity, IAuditable
{
    public int Ano { get; private set; }
    public int Mes { get; private set; }
    public Money PatrimonioLiquidoBrl { get; private set; } = default!;
    public Money DespesaFinanceiraBrl { get; private set; } = default!;
    public Money EbitdaBrl { get; private set; } = default!;
    public Instant CreatedAt { get; private set; }
    public Instant UpdatedAt { get; private set; }
    public string AtualizadoPor { get; private set; } = default!;

    private DadosContabeisMensal() { }

    public static DadosContabeisMensal Criar(
        int ano, int mes,
        Money patrimonioLiquido, Money despesaFinanceira, Money ebitda,
        string usuario, IClock clock)
    {
        ValidarPeriodo(ano, mes);
        ValidarMoedasBrl(patrimonioLiquido, despesaFinanceira, ebitda);
        if (patrimonioLiquido.Valor <= 0)
            throw new ArgumentException("Patrimônio Líquido deve ser positivo.");
        // Despesa Financeira pode ser zero; EBITDA pode ser negativo.

        Instant agora = clock.GetCurrentInstant();
        return new DadosContabeisMensal
        {
            Ano = ano, Mes = mes,
            PatrimonioLiquidoBrl = patrimonioLiquido,
            DespesaFinanceiraBrl = despesaFinanceira,
            EbitdaBrl = ebitda,
            CreatedAt = agora, UpdatedAt = agora,
            AtualizadoPor = usuario,
        };
    }

    public void Atualizar(
        Money patrimonioLiquido, Money despesaFinanceira, Money ebitda,
        string usuario, IClock clock)
    {
        ValidarMoedasBrl(patrimonioLiquido, despesaFinanceira, ebitda);
        PatrimonioLiquidoBrl = patrimonioLiquido;
        DespesaFinanceiraBrl = despesaFinanceira;
        EbitdaBrl = ebitda;
        UpdatedAt = clock.GetCurrentInstant();
        AtualizadoPor = usuario;
    }
}
```

### 2.2 Repositório

```csharp
public interface IDadosContabeisMensalRepository
{
    Task<DadosContabeisMensal?> GetAsync(int ano, int mes, CancellationToken ct);
    Task UpsertAsync(DadosContabeisMensal dados, CancellationToken ct);
    Task<IReadOnlyList<DadosContabeisMensal>> ListUltimos12Async(LocalDate ate, CancellationToken ct);
}
```

### 2.3 Migração da tabela existente `ebitda_mensal`

A tabela atual `ebitda_mensal` permanece como **fonte primária de EBITDA** (legado). A nova entidade lê EBITDA dela e adiciona PL e Despesa Financeira em coluna nova ou tabela paralela. **Decisão:** evoluir a tabela existente:

```sql
ALTER TABLE ebitda_mensal RENAME TO dados_contabeis_mensal;

ALTER TABLE dados_contabeis_mensal
    ADD COLUMN patrimonio_liquido_brl NUMERIC(18, 2) NOT NULL DEFAULT 0,
    ADD COLUMN despesa_financeira_brl NUMERIC(18, 2) NOT NULL DEFAULT 0;

-- Renomear coluna para consistência
ALTER TABLE dados_contabeis_mensal RENAME COLUMN valor_brl TO ebitda_brl;
```

`POST /painel/ebitda` é mantido (compatibilidade) e atualiza apenas `EbitdaBrl`. Novo endpoint `POST /painel/dados-contabeis` aceita os três valores juntos.

---

## 3. Endpoints

### 3.1 `POST /api/v1/painel/dados-contabeis`

**Auth:** `Policies.Auditoria` (mesma política do `POST /ebitda`).

**Body:**

```json
{
  "ano": 2026,
  "mes": 4,
  "patrimonioLiquidoBrl": 89000000.00,
  "despesaFinanceiraBrl": 8200000.00,
  "ebitdaBrl": 33400000.00
}
```

**Response:** 204 No Content. 400 em validação.

Operação é **upsert** por `(ano, mes)`.

### 3.2 `POST /api/v1/painel/ebitda` (existente, mantido)

Atualiza apenas EBITDA. Preserva PL e Despesa Financeira já cadastradas.

### 3.3 `GET /api/v1/painel/estrutura-capital`

**Auth:** `Policies.Leitura`.
**Query params:**

| Param | Tipo | Default |
|-------|------|---------|
| `dataReferencia` | date | hoje BRT |

**Response 200 (envelope):**

```json
{
  "data": {
    "dataReferencia": "2026-04-30",
    "dividaTotalBrl": 124000000.00,
    "patrimonioLiquidoBrl": 89000000.00,
    "dividaSobrePatrimonio": 1.39,
    "ebitdaUltimos12mBrl": 33400000.00,
    "despesaFinanceira12mBrl": 8200000.00,
    "icr": 4.07,
    "dividaEbitda": 3.71
  },
  "meta": {
    "dataHoraCalculo": "...",
    "fontesConsultadas": [
      { "fonte": "contratos", "status": "OK", "registros": 142 },
      { "fonte": "dados_contabeis_mensal", "status": "OK", "registros": 12 }
    ],
    "completude": "COMPLETO"
  }
}
```

---

## 4. Regras de Cálculo

### 4.1 `DividaTotalBrl`

Reaproveita `PainelDividaDto.DividaBrutaBrl` (mesma estratégia spot/PTAX).

### 4.2 `EbitdaUltimos12mBrl` e `DespesaFinanceira12mBrl`

Soma dos últimos 12 meses fechados a partir de `dataReferencia`:

```csharp
LocalDate fim = new LocalDate(dataRef.Year, dataRef.Month, 1).PlusMonths(-1).PlusDays(-1)?.PlusDays(1); 
// = último dia do mês anterior ao dataRef se dataRef cai no meio do mês, ou
// = último dia do próprio mês de dataRef se cai no último dia.
// Simplificação: usar Mes/Ano do mês imediatamente anterior se dia < último dia.
```

Regra prática: considera mês cheio apenas se `dataReferencia` cai no último dia do mês ou em mês posterior.

### 4.3 `DividaSobrePatrimonio`

```
dividaTotalBrl / patrimonioLiquidoBrl
```

Arredondado a 2 casas `AwayFromZero`. `PatrimonioLiquido = 0` → retorna `null` + alerta.

### 4.4 `ICR` (Interest Coverage Ratio)

```
ebitdaUltimos12mBrl / despesaFinanceira12mBrl
```

Arredondado a 2 casas. `DespesaFinanceira = 0` → retorna `null` + alerta.

### 4.5 `DividaEbitda`

```
dividaTotalBrl / ebitdaUltimos12mBrl
```

Arredondado a 2 casas. `Ebitda12m ≤ 0` → retorna `null` + alerta.

### 4.6 Dados ausentes

| Situação | Comportamento |
|----------|---------------|
| Nenhum mês de PL/DF/EBITDA cadastrado para os últimos 12 meses | `data` campos nulos; `completude: PARCIAL`; gera `Alerta(categoria=OPERACIONAL, severidade=ATENCAO)` "Dados contábeis ausentes" |
| 6 meses de 12 cadastrados | Calcula com os 6 (anualiza?) — **não anualizar**, retorna soma direta + flag em `meta.fontesConsultadas[].status = DEGRADADO` + completude `PARCIAL` |
| PL cadastrado para dataReferencia mas mês corrente vazio | Usa o mais recente disponível anterior à dataRef |

---

## 5. Handler

```csharp
public sealed record GetEstruturaCapitalQuery(LocalDate? DataReferencia)
    : IRequest<EnvelopeResponse<EstruturaCapitalDto>>;

public sealed record EstruturaCapitalDto(
    LocalDate DataReferencia,
    decimal DividaTotalBrl,
    decimal? PatrimonioLiquidoBrl,
    decimal? DividaSobrePatrimonio,
    decimal? EbitdaUltimos12mBrl,
    decimal? DespesaFinanceira12mBrl,
    decimal? Icr,
    decimal? DividaEbitda);
```

Handler combina `IContratoRepository` + `IDadosContabeisMensalRepository` + helpers de conversão BRL já existentes. Emite alertas via `IAlertaRepository` quando há dados ausentes.

---

## 6. Casos de Borda

| Cenário | Comportamento |
|---------|---------------|
| `dataReferencia` futura | 400 com `detail: "dataReferencia não pode ser futura"` |
| Mês 13 ou 0 no POST | 400 |
| EBITDA negativo nos 12 meses | `DividaEbitda = null`, alerta `OPERACIONAL ATENCAO` |
| Despesa Financeira = 0 | `ICR = null`, alerta `OPERACIONAL INFORMATIVO` |
| Recadastrar mesmo `(ano, mes)` | Upsert atualiza `UpdatedAt` e `AtualizadoPor`; registra `AuditLog` |
| Patrimônio Líquido negativo (empresa insolvente) | Aceito; `DividaSobrePatrimonio` retorna negativo — exibido no FE com aviso |

---

## 7. Critérios de Aceite

- [ ] Migration evolui `ebitda_mensal` para `dados_contabeis_mensal` sem perda de dados.
- [ ] `POST /painel/dados-contabeis` faz upsert idempotente.
- [ ] `POST /painel/ebitda` legado continua funcional.
- [ ] `GET /painel/estrutura-capital` retorna envelope com todos os campos.
- [ ] Cálculos batem com fixture conhecida (golden dataset).
- [ ] Alerta `OPERACIONAL` é criado quando dados contábeis estão ausentes.
- [ ] AuditLog registra cada upsert.

---

## 8. Verificação

```bash
dotnet test --filter "FullyQualifiedName~EstruturaCapital"
dotnet test --filter "FullyQualifiedName~DadosContabeis"

# Migration
dotnet ef migrations add EvoluirEbitdaParaDadosContabeis --project src/Sgcf.Infrastructure --startup-project src/Sgcf.Api
dotnet ef database update --project src/Sgcf.Infrastructure --startup-project src/Sgcf.Api
```

**Teste-chave (cálculo):**

```csharp
[Fact]
public async Task GetEstruturaCapital_calcula_indicadores_corretamente()
{
    // Arrange: divida 124m, PL 89m, EBITDA12m 33.4m, DespFin12m 8.2m
    SetupContratos(totalBrl: 124_000_000m);
    SetupDadosContabeis(ano: 2026, mes: 4,
        pl: 89_000_000m, despFin: 8_200_000m, ebitda: 33_400_000m);

    var result = await _mediator.Send(new GetEstruturaCapitalQuery(new LocalDate(2026, 4, 30)));

    result.Data.DividaSobrePatrimonio.Should().Be(1.39m);
    result.Data.Icr.Should().Be(4.07m);
    result.Data.DividaEbitda.Should().Be(3.71m);
}
```

---

## 9. Boundaries específicas

### 9.1 Always do
- Money `Brl` em todos os campos contábeis.
- Audit log em cada upsert.
- Alerta quando dados ausentes (sem mascarar a falha).

### 9.2 Ask first
- Trocar a estratégia de janela 12m por trailing 4 trimestres (impacta interpretação do CFO).
- Permitir `PatrimonioLiquido = 0` para empresas em pré-operacional.

### 9.3 Never do
- Anualizar parcial (`ebitda_6m * 2`) — gera viés.
- Calcular ICR com EBIT em vez de EBITDA sem ajuste explícito.
- Apagar histórico ao fazer upsert — sempre `UPDATE` + `AuditLog`.

---

## 10. Arquivos esperados

- `src/Sgcf.Domain/Contabilidade/DadosContabeisMensal.cs` (substitui `EbitdaMensal`)
- `src/Sgcf.Application/Contabilidade/IDadosContabeisMensalRepository.cs`
- `src/Sgcf.Application/Contabilidade/Commands/UpsertDadosContabeisCommand.cs` + Handler
- `src/Sgcf.Application/Painel/EstruturaCapitalDto.cs`
- `src/Sgcf.Application/Painel/Queries/GetEstruturaCapitalQuery.cs` + Handler
- `src/Sgcf.Api/Controllers/PainelController.cs` (endpoint novo + upsert)
- `src/Sgcf.Infrastructure/Persistence/Configurations/DadosContabeisMensalConfiguration.cs`
- `src/Sgcf.Infrastructure/Migrations/<ts>_EvoluirEbitdaParaDadosContabeis.cs`
- `tests/Sgcf.Application.Tests/Painel/EstruturaCapitalTests.cs`
- `tests/Sgcf.GoldenDataset/data/painel/estrutura_capital_baseline.json`
