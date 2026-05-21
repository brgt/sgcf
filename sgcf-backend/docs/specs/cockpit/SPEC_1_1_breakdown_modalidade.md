# SPEC — Task 1.1 — GAP-CKP-01 — Breakdown da Dívida por Modalidade

> **Master:** `SPEC.md`
> **Plano:** `tasks/plan_cockpit_backend_gaps.md` Task 1.1
> **Status:** Draft
> **Versão:** v1.0
> **Escopo:** S
> **Persona:** CFO
> **Dependências:** Task 0.1 (envelope)

---

## 1. Objetivo

Entregar o card "Mix de Funding" do cockpit CFO (UX §13.1) — distribuição da dívida ativa por `ModalidadeContrato`. O endpoint atual `GET /painel/divida` só retorna `BreakdownPorMoeda`; este task adiciona a visão complementar por modalidade.

---

## 2. Endpoint

```
GET /api/v1/painel/divida/breakdown-modalidade
```

**Auth:** `Policies.Leitura`.
**Query params:** Nenhum (default: contratos `Ativo`).
**Response 200:** envelope com `BreakdownModalidadeDto`.

### 2.1 DTO

```csharp
namespace Sgcf.Application.Painel;

public sealed record BreakdownModalidadeDto(
    decimal TotalBrl,
    IReadOnlyList<LinhaBreakdownModalidadeDto> Itens);

public sealed record LinhaBreakdownModalidadeDto(
    string Modalidade,
    decimal ValorBrl,
    decimal PercentualPct,
    int QuantidadeContratos,
    decimal TaxaMediaPonderadaAaPct,
    int PrazoMedioRemanescenteDias);
```

### 2.2 Exemplo de resposta

```json
{
  "data": {
    "totalBrl": 124000000.00,
    "itens": [
      {
        "modalidade": "FINIMP",
        "valorBrl": 45000000.00,
        "percentualPct": 36.29,
        "quantidadeContratos": 18,
        "taxaMediaPonderadaAaPct": 6.82,
        "prazoMedioRemanescenteDias": 187
      },
      {
        "modalidade": "LEI4131",
        "valorBrl": 32000000.00,
        "percentualPct": 25.81,
        "quantidadeContratos": 7,
        "taxaMediaPonderadaAaPct": 5.94,
        "prazoMedioRemanescenteDias": 412
      }
    ]
  },
  "meta": {
    "dataHoraCalculo": "2026-05-19T14:32:00Z",
    "fontesConsultadas": [
      { "fonte": "contratos", "status": "OK", "registros": 142 },
      { "fonte": "cotacao_spot_cache", "status": "OK" }
    ],
    "completude": "COMPLETO"
  }
}
```

---

## 3. Regras de Cálculo

### 3.1 Filtro de contratos

- `StatusContrato = Ativo` (única — exclui `Vencido`, `Inadimplente`, `Liquidado`, etc.).
- Saldo considerado: `ValorPrincipalBrl` no momento do cálculo (mesma fonte que `PainelDividaDto.DividaBrutaBrl`).

### 3.2 Conversão para BRL

Mesma estratégia já usada em `GetPainelDividaQueryHandler`:

1. Para cada moeda estrangeira presente, tenta cotação spot via `ICotacaoSpotCache`.
2. Falha em spot cai para PTAX D-1 (`ICotacaoFxRepository.GetMaisRecenteAsync`, `TipoCotacao.PtaxD1`, mid-rate).
3. Sem PTAX, o contrato contribui com `0` para o BRL — registra `FonteConsultada.Status = DEGRADADO`.

### 3.3 Agrupamento

```csharp
var grupos = contratos
    .GroupBy(c => c.Modalidade)
    .Select(g => new LinhaBreakdownModalidadeDto(
        Modalidade: g.Key.ToString().ToUpperInvariant(),
        ValorBrl: g.Sum(c => ConverterParaBrl(c)),
        PercentualPct: 0m, // preenchido depois
        QuantidadeContratos: g.Count(),
        TaxaMediaPonderadaAaPct: MediaPonderadaTaxa(g),
        PrazoMedioRemanescenteDias: MediaPonderadaPrazo(g, hoje)))
    .ToList();
```

### 3.4 Cálculos derivados

**Percentual:** `valorBrl / totalBrl * 100`, arredondado a 2 casas `AwayFromZero`. Soma dos percentuais pode dar 99.99 ou 100.01 — não normalizar (transparência).

**Taxa média ponderada (a.a.):**

```
Σ (taxaAa_i * valorBrl_i) / Σ valorBrl_i
```

Arredondado a 4 casas (precisão de basis points), exibido como percentual.

**Prazo médio remanescente (dias):**

```
Σ ((dataVencimento_i - hoje) * valorBrl_i) / Σ valorBrl_i
```

Ponderado por valor. Retorna `int` arredondado.

### 3.5 Ordenação

Default: `valorBrl DESC`. Não há flag para reordenar nesta versão.

### 3.6 Consistência

Invariante testável: `Σ itens.valorBrl == PainelDividaDto.DividaBrutaBrl` (mesma data/hora). Diferença permitida até 0.05 BRL (acumulação de arredondamento).

---

## 4. Handler MediatR

```csharp
public sealed record GetBreakdownModalidadeQuery()
    : IRequest<EnvelopeResponse<BreakdownModalidadeDto>>;

public sealed class GetBreakdownModalidadeQueryHandler(
    IContratoRepository contratoRepo,
    ICotacaoSpotCache spotCache,
    ICotacaoFxRepository cotacaoFxRepo,
    IClock clock)
    : IRequestHandler<GetBreakdownModalidadeQuery, EnvelopeResponse<BreakdownModalidadeDto>>
{
    private static readonly DateTimeZone FusoBrasilia = DateTimeZoneProviders.Tzdb["America/Sao_Paulo"];

    public async Task<EnvelopeResponse<BreakdownModalidadeDto>> Handle(
        GetBreakdownModalidadeQuery _,
        CancellationToken ct)
    {
        Instant agora = clock.GetCurrentInstant();
        LocalDate hoje = agora.InZone(FusoBrasilia).Date;

        IReadOnlyList<Contrato> ativos = await contratoRepo
            .ListByStatusAsync(StatusContrato.Ativo, ct);

        IReadOnlySet<Moeda> moedasEstrangeiras = ativos
            .Where(c => c.Moeda != Moeda.Brl)
            .Select(c => c.Moeda).ToHashSet();

        var (cotacoes, fontes) = await ResolverCotacoesAsync(moedasEstrangeiras, hoje, ct);

        BreakdownModalidadeDto data = MontarBreakdown(ativos, cotacoes, hoje);

        return EnvelopeResponse.Ok(data, agora, fontes);
    }

    // helpers privados
}
```

---

## 5. Endpoint Controller

```csharp
[HttpGet("divida/breakdown-modalidade")]
[Authorize(Policy = Policies.Leitura)]
[ProducesResponseType<EnvelopeResponse<BreakdownModalidadeDto>>(StatusCodes.Status200OK)]
public async Task<IActionResult> GetBreakdownModalidade(CancellationToken ct)
{
    var resultado = await mediator.Send(new GetBreakdownModalidadeQuery(), ct);
    return Ok(resultado);
}
```

---

## 6. Casos de Borda

| Cenário | Comportamento |
|---------|---------------|
| Sem contratos ativos | `totalBrl: 0, itens: []`, `completude: COMPLETO` |
| Todas as 6 modalidades presentes | Retorna 6 linhas; ordenação por `valorBrl DESC` |
| Modalidade FGI com `taxaAa = 0` | Inclui na média ponderada normalmente; `taxaMediaPonderadaAaPct` pode ser baixa para a linha |
| Contrato em moeda sem cotação spot nem PTAX | Inclui na linha com `valorBrl = 0`; `FonteConsultada.Status = DEGRADADO`; `completude: PARCIAL` |
| Apenas contratos BRL | Sem chamada de cotação; `fontesConsultadas` lista apenas `contratos` |
| Contrato com `DataVencimento < hoje` (atrasado mas `Status = Ativo`) | Prazo remanescente vira negativo — usar `Math.Max(0, dias)` na média |

---

## 7. Performance

- Filtragem `WHERE status = 1` no banco — não em memória.
- `cotacao_spot_cache` evita N chamadas a PTAX repository.
- Resposta inteira em < 500 ms para até 1 000 contratos ativos.

---

## 8. Critérios de Aceite

- [ ] Endpoint `GET /api/v1/painel/divida/breakdown-modalidade` operacional.
- [ ] Resposta segue envelope `{ data, meta }`.
- [ ] Filtro `StatusContrato = Ativo` aplicado em SQL.
- [ ] Ordenação default por `valorBrl DESC`.
- [ ] Soma dos `valorBrl` por modalidade igual à `PainelDividaDto.DividaBrutaBrl` (delta ≤ 0,05 BRL).
- [ ] `completude` reflete falha de cotação.
- [ ] Cobertura ≥ 80% no handler.

---

## 9. Verificação

```bash
dotnet test --filter "FullyQualifiedName~BreakdownModalidade"
```

**Teste-chave (consistência):**

```csharp
[Fact]
public async Task BreakdownModalidade_soma_bate_com_PainelDivida()
{
    var painelDivida = await _mediator.Send(new GetPainelDividaQuery(null, null));
    var breakdown = await _mediator.Send(new GetBreakdownModalidadeQuery());

    decimal somaBreakdown = breakdown.Data.Itens.Sum(i => i.ValorBrl);

    somaBreakdown.Should().BeApproximately(painelDivida.DividaBrutaBrl, precision: 0.05m);
}
```

**Golden dataset:** adicionar caso em `tests/Sgcf.GoldenDataset/data/painel/breakdown_modalidade_5_contratos.json` cobrindo 3 modalidades distintas + 1 moeda estrangeira.

---

## 10. Boundaries específicas

### 10.1 Always do
- Filtrar `StatusContrato = Ativo` no banco.
- Usar mesmo `IClock` e cache spot do `GetPainelDividaQueryHandler` para garantir consistência.
- Arredondamento `AwayFromZero`.

### 10.2 Ask first
- Adicionar parâmetros de filtro (`bancoId`, `moeda`) — não estão no MVP do cockpit.
- Mudar ordenação default.

### 10.3 Never do
- Calcular percentual normalizado para somar exatamente 100% (mascara erros de arredondamento).
- Recriar lógica de conversão BRL — reusar helper compartilhado.

---

## 11. Arquivos esperados

- `src/Sgcf.Application/Painel/BreakdownModalidadeDto.cs`
- `src/Sgcf.Application/Painel/Queries/GetBreakdownModalidadeQuery.cs` + Handler
- `src/Sgcf.Api/Controllers/PainelController.cs` (novo endpoint)
- `tests/Sgcf.Application.Tests/Painel/BreakdownModalidadeTests.cs`
- `tests/Sgcf.Api.IntegrationTests/PainelControllerBreakdownTests.cs`
- `tests/Sgcf.GoldenDataset/data/painel/breakdown_modalidade_5_contratos.json`
