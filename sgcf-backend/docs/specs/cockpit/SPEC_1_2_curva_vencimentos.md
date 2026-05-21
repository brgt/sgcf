# SPEC — Task 1.2 — GAP-CKP-03 — Curva de Vencimentos Multi-Ano

> **Master:** `SPEC.md`
> **Plano:** `tasks/plan_cockpit_backend_gaps.md` Task 1.2
> **Status:** Draft
> **Versão:** v1.0
> **Escopo:** S
> **Persona:** CFO
> **Dependências:** Task 0.1 (envelope)

---

## 1. Objetivo

Entregar o card "Curva de Vencimentos (Maturity Profile)" do cockpit CFO (UX §13.1) — horizonte configurável de 12 a 60 meses com granularidade `mes`, `trimestre` ou `ano`. Substitui o uso atual de `GET /painel/vencimentos?ano=YYYY`, que cobre apenas 12 meses.

---

## 2. Endpoint

```
GET /api/v1/painel/vencimentos/horizonte
```

**Auth:** `Policies.Leitura`.

**Query params:**

| Param | Tipo | Default | Valores aceitos |
|-------|------|---------|------------------|
| `meses` | int | 36 | `12`, `24`, `36`, `60` |
| `granularidade` | string | `trimestre` | `mes`, `trimestre`, `ano` |
| `bancoId` | guid | — | filtro opcional |
| `modalidade` | string | — | enum `ModalidadeContrato` |
| `moeda` | string | — | enum `Moeda` |

Combinações inválidas retornam 400.

### 2.1 DTO

```csharp
public sealed record CurvaVencimentosDto(
    int HorizonteMeses,
    string Granularidade,
    decimal TotalHorizonteBrl,
    IReadOnlyList<BucketVencimentoDto> Buckets);

public sealed record BucketVencimentoDto(
    string Label,
    LocalDate DataInicio,
    LocalDate DataFim,
    decimal TotalPrincipalBrl,
    decimal TotalJurosBrl,
    decimal TotalBrl,
    int QuantidadeParcelas,
    IReadOnlyList<BreakdownModalidadeBucketDto> BreakdownPorModalidade);

public sealed record BreakdownModalidadeBucketDto(
    string Modalidade,
    decimal ValorBrl);
```

### 2.2 Exemplo

```json
{
  "data": {
    "horizonteMeses": 36,
    "granularidade": "trimestre",
    "totalHorizonteBrl": 142000000.00,
    "buckets": [
      {
        "label": "2026-Q3",
        "dataInicio": "2026-07-01",
        "dataFim": "2026-09-30",
        "totalPrincipalBrl": 18500000.00,
        "totalJurosBrl": 2100000.00,
        "totalBrl": 20600000.00,
        "quantidadeParcelas": 14,
        "breakdownPorModalidade": [
          { "modalidade": "FINIMP", "valorBrl": 12000000.00 },
          { "modalidade": "LEI4131", "valorBrl": 8600000.00 }
        ]
      }
    ]
  },
  "meta": { "dataHoraCalculo": "...", "fontesConsultadas": [...], "completude": "COMPLETO" }
}
```

---

## 3. Regras de Cálculo

### 3.1 Período de varredura

`hoje = clock.GetCurrentInstant().InZone(BRT).Date`
`dataFim = hoje.PlusMonths(meses)`

Lê todos os `EventoCronograma` (tabela `cronograma_pagamento`) pendentes nesse intervalo, agrupados pelos buckets calculados.

### 3.2 Construção de buckets

```csharp
private IEnumerable<(string label, LocalDate inicio, LocalDate fim)> GerarBuckets(
    LocalDate hoje, int meses, Granularidade gran) => gran switch
{
    Granularidade.Mes        => GerarMensais(hoje, meses),
    Granularidade.Trimestre  => GerarTrimestrais(hoje, meses),
    Granularidade.Ano        => GerarAnuais(hoje, meses),
    _ => throw new ArgumentException(nameof(gran)),
};
```

**Buckets mensais:** `2026-05`, `2026-06`, ... — `label = $"{ano:D4}-{mes:D2}"`.
**Buckets trimestrais:** `2026-Q3`, `2026-Q4`, ... — `label = $"{ano:D4}-Q{trimestre}"`.
**Buckets anuais:** `2026`, `2027`, ... — `label = ano.ToString()`.

O primeiro bucket começa em `hoje` (não no início do trimestre/ano), garantindo que todas as parcelas do horizonte sejam contadas exatamente uma vez.

### 3.3 Conversão BRL

Estratégia idêntica a `GetCalendarioVencimentosQueryHandler`:

- Spot via cache.
- Fallback PTAX D-1 mid-rate.
- Eventos em moeda sem cotação contribuem 0 e marcam `completude = PARCIAL`.

### 3.4 Filtros opcionais

`bancoId`, `modalidade`, `moeda` aplicados no SQL via join com `contrato`. Combinação é AND.

---

## 4. Handler

```csharp
public sealed record GetCurvaVencimentosQuery(
    int Meses,
    Granularidade Granularidade,
    Guid? BancoId,
    ModalidadeContrato? Modalidade,
    Moeda? Moeda) : IRequest<EnvelopeResponse<CurvaVencimentosDto>>;
```

Validação no controller:

```csharp
private static readonly int[] HorizonteValido = [12, 24, 36, 60];

if (!HorizonteValido.Contains(meses)) return BadRequest(/* detail */);
if (!Enum.TryParse<Granularidade>(granularidade, true, out var gran)) return BadRequest(/* detail */);
```

---

## 5. Endpoint Controller

```csharp
[HttpGet("vencimentos/horizonte")]
[Authorize(Policy = Policies.Leitura)]
[ProducesResponseType<EnvelopeResponse<CurvaVencimentosDto>>(StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
public async Task<IActionResult> GetCurvaVencimentos(
    [FromQuery] int meses = 36,
    [FromQuery] string granularidade = "trimestre",
    [FromQuery] Guid? bancoId = null,
    [FromQuery] string? modalidade = null,
    [FromQuery] string? moeda = null,
    CancellationToken ct = default)
{
    // validação
    // ...
    var resultado = await mediator.Send(new GetCurvaVencimentosQuery(meses, gran, bancoId, mod, moe), ct);
    return Ok(resultado);
}
```

---

## 6. Casos de Borda

| Cenário | Comportamento |
|---------|---------------|
| Sem parcelas no horizonte | `totalHorizonteBrl: 0, buckets: []`, `completude: COMPLETO` |
| Parcela com `DataVencimento = hoje` | Conta no primeiro bucket |
| Parcela em moeda sem PTAX | Conta 0 BRL; `completude: PARCIAL` |
| Granularidade `ano` com `meses = 12` | Retorna 1 bucket (ano corrente truncado) |
| Granularidade `mes` com `meses = 60` | 60 buckets — payload pode passar de 200 KB; ativar gzip |
| `meses = 36, granularidade = mes` | 36 buckets — sem problema de payload |
| Bucket sem parcelas (vazio) | Incluído com totais zero e array vazio em `breakdownPorModalidade` |

---

## 7. Performance

- Query única em `cronograma_pagamento` com join em `contrato`, ordenada por `data_vencimento`.
- Agregação em memória após query (cacheável por 60 s via ETag).
- `Cache-Control: max-age=60, private`, ETag com hash dos filtros + `dataHoraCalculo`.

P95 esperado: < 500 ms para horizonte 60 meses + 1 000 contratos.

---

## 8. Critérios de Aceite

- [ ] Endpoint `GET /api/v1/painel/vencimentos/horizonte` operacional.
- [ ] Validação dos parâmetros funciona (400 para `meses = 17`, `granularidade = decada`).
- [ ] Buckets gerados na granularidade correta.
- [ ] Soma `Σ buckets.totalBrl == totalHorizonteBrl` (consistência aritmética).
- [ ] Soma `Σ buckets.breakdownPorModalidade.valorBrl == bucket.totalBrl`.
- [ ] Filtros opcionais aplicados em SQL.
- [ ] ETag + Cache-Control de 60 s.

---

## 9. Verificação

```bash
dotnet test --filter "FullyQualifiedName~CurvaVencimentos"
```

**Teste-chave (consistência):**

```csharp
[Theory]
[InlineData(12, "mes", 12)]
[InlineData(24, "mes", 24)]
[InlineData(36, "trimestre", 12)]
[InlineData(60, "ano", 6)]
public async Task GetCurvaVencimentos_gera_quantidade_correta_de_buckets(
    int meses, string gran, int bucketsEsperados)
{
    var result = await _mediator.Send(new GetCurvaVencimentosQuery(meses, Enum.Parse<Granularidade>(gran, true), null, null, null));

    result.Data.Buckets.Should().HaveCount(bucketsEsperados);
}
```

**Teste de consistência aritmética:**

```csharp
[Fact]
public async Task GetCurvaVencimentos_soma_buckets_bate_com_total()
{
    var result = await _mediator.Send(new GetCurvaVencimentosQuery(36, Granularidade.Trimestre, null, null, null));

    decimal soma = result.Data.Buckets.Sum(b => b.TotalBrl);
    soma.Should().BeApproximately(result.Data.TotalHorizonteBrl, precision: 0.05m);
}
```

---

## 10. Boundaries específicas

### 10.1 Always do
- Conversão BRL idêntica ao `GetCalendarioVencimentosQueryHandler` (consistência).
- Buckets vazios incluídos no array (não ocultar).
- Validação no controller, antes do MediatR.

### 10.2 Ask first
- Adicionar `meses = 120` (10 anos) — exigiria análise de payload + projeção de juros futura.
- Adicionar granularidade `semana`.

### 10.3 Never do
- Calcular juros projetados sem CDI conhecido — usar `JurosBrl` dos eventos como está (`JurosBrlProjetado` é feature do endpoint legado).
- Filtrar parcelas com `Status = Paga` no resultado (já filtra pendentes).

---

## 11. Arquivos esperados

- `src/Sgcf.Application/Painel/CurvaVencimentosDto.cs`
- `src/Sgcf.Application/Painel/Queries/GetCurvaVencimentosQuery.cs` + Handler
- `src/Sgcf.Application/Painel/Granularidade.cs`
- `src/Sgcf.Api/Controllers/PainelController.cs` (endpoint novo)
- `tests/Sgcf.Application.Tests/Painel/CurvaVencimentosTests.cs`
- `tests/Sgcf.Api.IntegrationTests/PainelControllerCurvaVencimentosTests.cs`
