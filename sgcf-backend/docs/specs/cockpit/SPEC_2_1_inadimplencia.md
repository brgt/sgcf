# SPEC — Task 2.1 — GAP-CKP-07 — Visão de Inadimplência Agregada

> **Master:** `SPEC.md`
> **Plano:** `tasks/plan_cockpit_backend_gaps.md` Task 2.1
> **Status:** Draft
> **Versão:** v1.0
> **Escopo:** S
> **Persona:** Gerente Financeiro
> **Dependências:** Task 0.1 (envelope)

---

## 1. Objetivo

Entregar o card "Inadimplência e Atrasos" do cockpit Financeiro (UX §13.2) — visão agregada de contratos em mora com dias de atraso médio e bucketização por faixa de dias.

Os status `StatusContrato.Vencido/Inadimplente` e `StatusParcela.Vencida` já existem; o que falta é a agregação.

---

## 2. Endpoint

```
GET /api/v1/painel/inadimplencia
```

**Auth:** `Policies.Leitura`.
**Query params:** Nenhum no MVP. (Filtros por banco/modalidade entram em Fase 4.)

### 2.1 DTO

```csharp
public sealed record InadimplenciaDto(
    decimal TotalEmMoraBrl,
    decimal DiasAtrasoMedio,
    int QuantidadeContratos,
    IReadOnlyList<BucketInadimplenciaDto> Buckets,
    IReadOnlyList<ContratoEmMoraDto> ContratosTop);

public sealed record BucketInadimplenciaDto(
    string FaixaDias,        // "1-15", "16-30", "31-60", "60+"
    int Quantidade,
    decimal ValorBrl);

public sealed record ContratoEmMoraDto(
    Guid ContratoId,
    string NumeroExterno,
    Guid BancoId,
    string Modalidade,
    decimal ValorEmMoraBrl,
    int DiasAtraso);
```

### 2.2 Exemplo

```json
{
  "data": {
    "totalEmMoraBrl": 1240000.00,
    "diasAtrasoMedio": 12.4,
    "quantidadeContratos": 3,
    "buckets": [
      { "faixaDias": "1-15",  "quantidade": 2, "valorBrl":  480000.00 },
      { "faixaDias": "16-30", "quantidade": 1, "valorBrl":  760000.00 },
      { "faixaDias": "31-60", "quantidade": 0, "valorBrl":       0.00 },
      { "faixaDias": "60+",   "quantidade": 0, "valorBrl":       0.00 }
    ],
    "contratosTop": [
      { "contratoId": "...", "numeroExterno": "FINIMP-2026-018", "bancoId": "...",
        "modalidade": "FINIMP", "valorEmMoraBrl": 760000.00, "diasAtraso": 18 }
    ]
  },
  "meta": { "...": "..." }
}
```

`ContratosTop` lista os 5 contratos com maior `valorEmMoraBrl` (não é endpoint paginado — apenas resumo). Drill-down completo usa `GET /contratos?status=Vencido,Inadimplente`.

---

## 3. Regras de Cálculo

### 3.1 Universo

Parcelas com `Status = Vencida` **ou** com `DataVencimento < hoje BRT` e `Status = Pendente`. Considera todas as parcelas de contratos `Ativo`, `Vencido`, `Inadimplente`.

### 3.2 Valor em mora

Por parcela: `ValorPrincipal + ValorJuros - ValorPago` (parcial conta diferença). Convertido para BRL via spot/PTAX D-1 (mesma estratégia).

### 3.3 Dias de atraso

```
diasAtraso_i = (hoje - parcela.DataVencimento).Days
```

Mínimo 1 (parcela vencendo hoje já entra em `1-15`).

### 3.4 Bucketização

| Faixa | Critério |
|-------|----------|
| `1-15`  | `1 ≤ dias ≤ 15` |
| `16-30` | `16 ≤ dias ≤ 30` |
| `31-60` | `31 ≤ dias ≤ 60` |
| `60+`   | `dias > 60` |

Bucket atribuído **por contrato**, usando a **maior** `diasAtraso` entre as parcelas vencidas do contrato.

### 3.5 Dias de atraso médio

Média **ponderada por valor**:

```
diasAtrasoMedio = Σ (diasAtraso_contrato * valorEmMora_contrato) / Σ valorEmMora_contrato
```

Arredondado a 1 casa.

### 3.6 Top 5

`contratosTop = contratos.OrderByDescending(valorEmMoraBrl).Take(5)`.

---

## 4. Handler

```csharp
public sealed record GetInadimplenciaQuery()
    : IRequest<EnvelopeResponse<InadimplenciaDto>>;

public sealed class GetInadimplenciaQueryHandler(
    IContratoRepository contratoRepo,
    IParcelaRepository parcelaRepo,
    ICotacaoSpotCache spotCache,
    ICotacaoFxRepository cotacaoFxRepo,
    IClock clock)
    : IRequestHandler<GetInadimplenciaQuery, EnvelopeResponse<InadimplenciaDto>>
{
    public async Task<EnvelopeResponse<InadimplenciaDto>> Handle(GetInadimplenciaQuery _, CancellationToken ct)
    {
        Instant agora = clock.GetCurrentInstant();
        LocalDate hoje = agora.InZone(FusoBrasilia).Date;

        IReadOnlyList<Parcela> vencidas = await parcelaRepo.ListVencidasAteAsync(hoje, ct);
        IReadOnlySet<Guid> contratoIds = vencidas.Select(p => p.ContratoId).ToHashSet();
        IReadOnlyList<Contrato> contratos = await contratoRepo.ListByIdsAsync(contratoIds, ct);

        // ... agregação e conversão BRL

        return EnvelopeResponse.Ok(dto, agora, fontes);
    }
}
```

`IParcelaRepository.ListVencidasAteAsync` é nova:

```csharp
public Task<IReadOnlyList<Parcela>> ListVencidasAteAsync(LocalDate ate, CancellationToken ct);
// SELECT * FROM parcela
// WHERE (status = 3 /* Vencida */)
//    OR (status = 1 /* Pendente */ AND data_vencimento < @ate)
```

---

## 5. Casos de Borda

| Cenário | Comportamento |
|---------|---------------|
| Sem parcelas vencidas | `totalEmMoraBrl: 0, quantidadeContratos: 0`, todos os buckets com zero, `contratosTop: []`, `completude: COMPLETO` |
| Parcela parcialmente paga | `valorEmMora = valor - valorPago` (resto) |
| Contrato com várias parcelas em buckets diferentes | Contrato vai no bucket com a **maior** `diasAtraso` (não duplica) |
| Parcela em moeda sem PTAX | Conta com BRL = 0; `completude: PARCIAL` |
| Contrato com `Status = Liquidado` mas com parcela vencida (anomalia) | Inclui no resultado; gera `Alerta OPERACIONAL` (inconsistência) |
| `diasAtraso = 0` (parcela vencendo hoje) | Conta no bucket `1-15` |

---

## 6. Critérios de Aceite

- [ ] Endpoint `GET /api/v1/painel/inadimplencia` operacional.
- [ ] Bucketização funciona conforme tabela §3.4.
- [ ] `diasAtrasoMedio` ponderado por valor (testado com fixture).
- [ ] `contratosTop` ordenado por valor desc, máx 5.
- [ ] Soma `Σ buckets.valorBrl == totalEmMoraBrl`.
- [ ] Soma `Σ buckets.quantidade == quantidadeContratos`.
- [ ] Envelope com `meta.fontesConsultadas`.

---

## 7. Verificação

```bash
dotnet test --filter "FullyQualifiedName~Inadimplencia"
```

**Teste-chave (consistência):**

```csharp
[Fact]
public async Task Inadimplencia_consistencia_de_somas()
{
    SetupContratosComParcelasVencidas();

    var result = await _mediator.Send(new GetInadimplenciaQuery());

    result.Data.Buckets.Sum(b => b.ValorBrl).Should().BeApproximately(result.Data.TotalEmMoraBrl, 0.05m);
    result.Data.Buckets.Sum(b => b.Quantidade).Should().Be(result.Data.QuantidadeContratos);
}

[Fact]
public async Task DiasAtrasoMedio_ponderado_por_valor()
{
    // 1 contrato 1000 BRL, 30 dias atraso
    // 1 contrato 4000 BRL, 5 dias atraso
    // Esperado: (30*1000 + 5*4000) / 5000 = 10

    SetupTwoContratos();

    var result = await _mediator.Send(new GetInadimplenciaQuery());

    result.Data.DiasAtrasoMedio.Should().Be(10.0m);
}
```

---

## 8. Boundaries específicas

### 8.1 Always do
- Usar `IClock` + `BRT` para `hoje`.
- Money/conversão BRL conforme padrão dos outros painéis.

### 8.2 Ask first
- Adicionar filtro por banco/modalidade — fora do MVP, entra em refinamento Fase 4.
- Mudar critério de mora (incluir prazo de carência?) — afeta indicador.

### 8.3 Never do
- Atribuir mesmo contrato a dois buckets.
- Ignorar parcelas `Status = Pendente` com vencimento passado (a regra cobre).
- Disparar processo de cobrança automatizado a partir deste endpoint (read-only).

---

## 9. Arquivos esperados

- `src/Sgcf.Application/Painel/InadimplenciaDto.cs`
- `src/Sgcf.Application/Painel/Queries/GetInadimplenciaQuery.cs` + Handler
- `src/Sgcf.Application/Contratos/IParcelaRepository.cs` (método novo)
- `src/Sgcf.Infrastructure/Persistence/Repositories/ParcelaRepository.cs` (impl)
- `src/Sgcf.Api/Controllers/PainelController.cs` (endpoint novo)
- `tests/Sgcf.Application.Tests/Painel/InadimplenciaTests.cs`
- `tests/Sgcf.GoldenDataset/data/painel/inadimplencia_baseline.json`
