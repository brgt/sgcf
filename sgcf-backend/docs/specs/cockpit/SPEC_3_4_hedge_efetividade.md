# SPEC — Task 3.4 — GAP-CKP-10 — Efetividade de Hedge

> **Master:** `SPEC.md`
> **Plano:** `tasks/plan_cockpit_backend_gaps.md` Task 3.4
> **Status:** Draft
> **Versão:** v1.0
> **Escopo:** M
> **Persona:** Gerente de Tesouraria
> **Dependências:** Task 0.1 (envelope)

---

## 1. Objetivo

Entregar o card "Efetividade do Hedge" do cockpit Tesouraria (UX §13.3) — relação exposição × cobertura × MtM × VaR consolidada por moeda. Centraliza no backend o cálculo que hoje é feito client-side (`FgiTarifaSensitivityControls`, `Lei4131IrrfSensitivityControls`) e expõe via endpoint único.

---

## 2. Endpoint

```
GET /api/v1/tesouraria/hedge-efetividade
```

**Auth:** `Policies.Leitura`.
**Query params:** Nenhum no MVP.

### 2.1 DTO

```csharp
public sealed record HedgeEfetividadeDto(
    decimal CoberturaConsolidadaPct,
    IReadOnlyList<HedgePorMoedaDto> PorMoeda);

public sealed record HedgePorMoedaDto(
    string Moeda,
    decimal ExposicaoLiquidaOriginal,
    decimal ExposicaoLiquidaBrl,
    decimal HedgeContratadoOriginal,
    decimal HedgeContratadoBrl,
    decimal CoberturaPct,
    decimal GapOriginal,
    decimal GapBrl,
    decimal MtmAtualBrl,
    decimal Var1diaBrl);
```

### 2.2 Exemplo

```json
{
  "data": {
    "coberturaConsolidadaPct": 68.40,
    "porMoeda": [
      {
        "moeda": "USD",
        "exposicaoLiquidaOriginal": 12500000.00,
        "exposicaoLiquidaBrl": 65800000.00,
        "hedgeContratadoOriginal": 9000000.00,
        "hedgeContratadoBrl": 47400000.00,
        "coberturaPct": 72.00,
        "gapOriginal": 3500000.00,
        "gapBrl": 18400000.00,
        "mtmAtualBrl": -420000.00,
        "var1diaBrl": 850000.00
      }
    ]
  },
  "meta": {...}
}
```

---

## 3. Regras de Cálculo

### 3.1 Exposição líquida por moeda

```
exposicaoLiquidaOriginal_M = Σ (contratos ativos com moeda = M).ValorPrincipal
```

Contratos com `StatusContrato = Ativo` em moeda estrangeira (≠ BRL). BRL é excluído (não há exposição cambial).

### 3.2 Hedge contratado por moeda

```
hedgeContratadoOriginal_M = Σ (hedges com Status = Ativo, MoedaBase = M).Notional
```

### 3.3 Cobertura

```
coberturaPct_M = (hedgeContratadoOriginal_M / exposicaoLiquidaOriginal_M) * 100
```

Casos especiais:

- `exposicaoLiquidaOriginal = 0` e `hedgeContratado > 0` → cobertura `null` + alerta `OPERACIONAL` (hedge sem exposição, anomalia).
- `exposicaoLiquidaOriginal = 0` e `hedgeContratado = 0` → não inclui a moeda no payload.

Cobertura **não é capada em 100%** — over-hedge é informação relevante (alerta separado pode ser disparado pelo rules engine futuramente).

### 3.4 Gap

```
gapOriginal_M = exposicaoLiquidaOriginal_M - hedgeContratadoOriginal_M
```

Positivo = exposição não coberta. Negativo = over-hedge.

### 3.5 Conversão BRL

Mesma estratégia spot/PTAX dos outros painéis. `cotacaoAplicada` exposta indiretamente via `exposicaoBrl / exposicaoOriginal`.

### 3.6 MtM atual por moeda

```
mtmAtualBrl_M = Σ NdfMtmCalculador.Calcular(hedge_i, spot_M) para hedges em M
```

Reaproveita `NdfMtmCalculador` existente (`Sgcf.Domain.Hedge.NdfMtmCalculador`).

### 3.7 VaR 1 dia

**Fórmula simplificada para MVP:**

```
var1diaBrl_M = exposicaoLiquidaBrl_M * volatilidadeDiaria_M * z_95
```

Onde:
- `volatilidadeDiaria_M` = desvio-padrão dos retornos diários da PTAX dos últimos 30 dias úteis.
- `z_95` = 1,645 (95% confidence).

Se faltam dados de PTAX para 30 dias, retorna `null` + `FonteConsultada.Status = DEGRADADO`.

**Decisão de produto:** versão MVP usa este modelo paramétrico simples. Versão mais sofisticada (Monte Carlo) entra na Fase 4.

### 3.8 Cobertura consolidada

```
coberturaConsolidadaPct = (Σ hedgeContratadoBrl) / (Σ exposicaoLiquidaBrl) * 100
```

Ponderada por BRL — moeda com maior exposição domina.

---

## 4. Handler

```csharp
public sealed record GetHedgeEfetividadeQuery()
    : IRequest<EnvelopeResponse<HedgeEfetividadeDto>>;

public sealed class GetHedgeEfetividadeQueryHandler(
    IContratoRepository contratoRepo,
    IHedgeRepository hedgeRepo,
    ICotacaoSpotCache spotCache,
    ICotacaoFxRepository cotacaoFxRepo,
    IClock clock)
    : IRequestHandler<GetHedgeEfetividadeQuery, EnvelopeResponse<HedgeEfetividadeDto>>
{
    public async Task<EnvelopeResponse<HedgeEfetividadeDto>> Handle(GetHedgeEfetividadeQuery _, CancellationToken ct)
    {
        Instant agora = clock.GetCurrentInstant();
        LocalDate hoje = agora.InZone(FusoBrasilia).Date;

        var contratos = await contratoRepo.ListByStatusAsync(StatusContrato.Ativo, ct);
        var hedges = await hedgeRepo.ListAtivosAsync(ct);

        var moedas = contratos.Where(c => c.Moeda != Moeda.Brl).Select(c => c.Moeda)
            .Union(hedges.Select(h => h.MoedaBase).Where(m => m != Moeda.Brl))
            .ToHashSet();

        var (cotacoes, fontes) = await ResolverCotacoesAsync(moedas, hoje, ct);
        var voltatilidades = await CarregarVolatilidades30dAsync(moedas, hoje, ct);

        var porMoeda = moedas
            .Select(m => CalcularHedgePorMoeda(m, contratos, hedges, cotacoes, voltatilidades))
            .Where(linha => linha is not null)
            .Select(linha => linha!)
            .OrderByDescending(l => l.ExposicaoLiquidaBrl)
            .ToList()
            .AsReadOnly();

        decimal cobConsol = porMoeda.Sum(l => l.ExposicaoLiquidaBrl) is > 0
            ? Math.Round(porMoeda.Sum(l => l.HedgeContratadoBrl) / porMoeda.Sum(l => l.ExposicaoLiquidaBrl) * 100m,
                2, MidpointRounding.AwayFromZero)
            : 0m;

        return EnvelopeResponse.Ok(new HedgeEfetividadeDto(cobConsol, porMoeda), agora, fontes);
    }
}
```

---

## 5. Casos de Borda

| Cenário | Comportamento |
|---------|---------------|
| Sem contratos em moeda estrangeira | `porMoeda: []`, `coberturaConsolidadaPct: 0`, `completude: COMPLETO` |
| Moeda com exposição mas sem hedge | Incluída com `coberturaPct: 0`, `gapOriginal = exposicao` |
| Moeda com hedge mas sem exposição | Não incluída no payload; gera `Alerta OPERACIONAL` (anomalia) |
| Sem PTAX para 30 dias úteis | `var1diaBrl: null` + `meta.completude: PARCIAL` |
| Hedge tipo `NdfCollar` | MtM calculado por `NdfMtmCalculador.CalcularMtmCollar` |
| Múltiplos hedges para mesma moeda | Notional somado; MtM somado linearmente |
| Over-hedge (cobertura > 100%) | Exibido normalmente; FE deve sinalizar visualmente |

---

## 6. Performance

- Uma query para contratos ativos, uma para hedges ativos, uma para cotações por moeda.
- Volatilidade pré-computada em background (futuro). MVP: query SQL agregada sobre `cotacao_fx` filtrando últimos 30 dias úteis.
- Cache de 60 s via ETag (saldo de moeda muda pouco intraday).

P95 esperado: < 500 ms.

---

## 7. Critérios de Aceite

- [ ] Endpoint `GET /api/v1/tesouraria/hedge-efetividade` operacional.
- [ ] Cálculo de exposição reproduz `Σ contratos.ValorPrincipalBrl` por moeda (testado contra `PainelDividaDto`).
- [ ] Cálculo de hedge reproduz `Σ hedge.NotionalBrl` por moeda.
- [ ] Cobertura calculada por moeda + consolidada.
- [ ] MtM atual usa `NdfMtmCalculador` existente.
- [ ] VaR 1d calculado com fórmula paramétrica + z_95.
- [ ] `completude: PARCIAL` quando dados de volatilidade insuficientes.
- [ ] ETag + Cache-Control 60 s.

---

## 8. Verificação

```bash
dotnet test --filter "FullyQualifiedName~HedgeEfetividade"
```

**Teste-chave (consistência com PainelDivida):**

```csharp
[Fact]
public async Task HedgeEfetividade_exposicao_por_moeda_bate_com_PainelDivida()
{
    var painel = await _mediator.Send(new GetPainelDividaQuery(null, null));
    var hedge = await _mediator.Send(new GetHedgeEfetividadeQuery());

    foreach (var moeda in hedge.Data.PorMoeda)
    {
        var moedaPainel = painel.BreakdownPorMoeda.FirstOrDefault(m => m.Moeda == moeda.Moeda);
        if (moedaPainel is null) continue;

        moeda.ExposicaoLiquidaBrl.Should().BeApproximately(moedaPainel.SaldoBrl, 0.05m);
    }
}

[Fact]
public async Task HedgeEfetividade_cobertura_consolidada_eh_ponderada_por_brl()
{
    // 1 moeda 100mi exposição, 50mi hedge → 50%
    // 1 moeda 10mi exposição, 9mi hedge → 90%
    // Esperado consolidado: 59/110 = ~53,6%

    SetupExposicoes();
    var result = await _mediator.Send(new GetHedgeEfetividadeQuery());

    result.Data.CoberturaConsolidadaPct.Should().BeApproximately(53.6m, 0.1m);
}
```

---

## 9. Boundaries específicas

### 9.1 Always do
- Reaproveitar `NdfMtmCalculador` para MtM.
- Filtrar `StatusContrato = Ativo` e `StatusHedge = Ativo`.
- Documentar fórmula do VaR em XMLDoc do handler.

### 9.2 Ask first
- Trocar modelo paramétrico de VaR por simulação Monte Carlo (impacta tempo de resposta).
- Adicionar parâmetro `confianca` (90, 95, 99) — válido mas exige decisão de produto.

### 9.3 Never do
- Capar cobertura em 100% no servidor (esconder over-hedge).
- Calcular MtM com fórmula nova quando `NdfMtmCalculador` existe.
- Considerar BRL como moeda exposta (não tem risco cambial).

---

## 10. Arquivos esperados

- `src/Sgcf.Application/Tesouraria/HedgeEfetividadeDto.cs`
- `src/Sgcf.Application/Tesouraria/Queries/GetHedgeEfetividadeQuery.cs` + Handler
- `src/Sgcf.Application/Tesouraria/Services/VolatilidadeCambialService.cs` (cálculo de volatilidade)
- `src/Sgcf.Api/Controllers/TesourariaController.cs` (endpoint novo)
- `tests/Sgcf.Application.Tests/Tesouraria/HedgeEfetividadeTests.cs`
- `tests/Sgcf.GoldenDataset/data/tesouraria/hedge_efetividade_baseline.json`
