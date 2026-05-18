# SPEC — Modalidade FGI no Módulo de Cotações

**Versão alvo:** v0.9.0 (paralelo com Capital de Giro)
**Status:** Entregue — v0.9.0 (2026-05-18)
**Pré-requisito:** Onda 0 (PTAX nullable, `CalculadoraCet` por modalidade, `IConversorModalidade` dispatcher)
**Plano:** `tasks/cotacoes-modalidades/fgi/plan.md`
**Plano mestre:** `tasks/cotacoes-modalidades/plan.md`
**SPEC base:** `docs/specs/cotacoes/SPEC.md`
**Decisões travadas:** MD-3, MD-5, MD-8 do plano mestre; base de cálculo 360 dias; PTAX não se aplica; correção de domínio 2026-05-18 (FGI é produto BNDES ofertado via bancos — não é garantia genérica)

---

## 1. Objetivo

Habilitar o módulo de Cotações a tratar a modalidade **FGI** (Fundo Garantidor para Investimentos — programa BNDES) como **produto BNDES ofertado por bancos repassadores** (Caixa, BV, Banco do Brasil, Sicoob, e demais credenciados). A modalidade FGI cobre o caso específico em que a operação contratada é estruturada **como uma linha BNDES com cobertura FGI obrigatória** — não trata FGI como garantia genérica anexada a outras modalidades.

Características operacionais que tornam FGI uma modalidade distinta (e não uma flag em Capital de Giro):

- **Origem do funding:** BNDES, não o próprio banco. O banco é repassador, não tomador de risco direto sobre o principal.
- **Tarifa periódica obrigatória:** `TaxaFgiAa` aplicada sobre saldo devedor — distinta da taxa nominal repassada pelo BNDES.
- **Cobertura informativa:** `PercentualCoberto` indica fração que o BNDES via FGI cobre em caso de default; alimenta análise de risco, não o CET.
- **Documentação específica BNDES:** `NumeroOperacaoFgi` rastreia a operação dentro do sistema BNDES, separadamente do contrato bancário.

Esta SPEC formaliza o fluxo "Criar cotação FGI → registrar propostas → comparar → aceitar → converter em contrato FGI" reaproveitando os agregados existentes (`Cotacao`, `Proposta`, `FgiDetail`) e exigindo extensões pontuais em `CalculadoraCet` e nos comandos de criação e conversão.

### 1.1. Não-objetivos (Out of Scope desta SPEC)

- **Price/SAC com FGI.** A cobrança correta da tarifa anual sobre saldo médio em estruturas amortizáveis exige cronograma intermediário. MVP só suporta `EstruturaAmortizacao.Bullet` para FGI-modalidade. Evolução pode endereçar Price/SAC.
- **FGI como garantia anexa.** Caso `TipoGarantia.Fgi` em `GarantiaExigidaLimite` aplicado a outra modalidade (ex.: NCE, Capital de Giro, FINIMP) já está implementado em v0.6.0 — fora desta SPEC. Ver §2.1 e §13 (glossário).
- **Flag `TemFgi` em outras modalidades.** O design original previa `BalcaoCaixaDetail.TemFgi`, removido na correção de 2026-05-18. Capital de Giro com exigência de FGI usa `GarantiaExigidaLimite.Tipo=Fgi` no cadastro do limite; não há flag duplicada.
- **Validação regulatória de cobertura máxima do FGI.** O sistema aceita `PercentualCoberto` em `(0, 1]`; a verificação de limites por programa (PEAC, NOVO_EMPREENDEDOR, etc.) é responsabilidade do operador.
- **Captura do subtipo `TipoFgi`** (PEAC/NOVO_EMPREENDEDOR). `GarantiaFgiDetail` carrega esse campo; `FgiDetail` (modalidade) não o expõe no MVP.
- **Banco intermediário separado.** O `BancoId` da `Proposta` já identifica o banco que operacionaliza a linha; não há campo extra.

---

## 2. Conceito de Negócio

### 2.1. FGI: modalidade e garantia — dois conceitos distintos

O domínio do SGCF reconhece **dois usos distintos** do termo "FGI" após a correção de 2026-05-18. Esta SPEC trata exclusivamente do primeiro.

| Conceito | Onde aparece no domínio | Significado |
|----------|--------------------------|-------------|
| **FGI-modalidade** | `ModalidadeContrato.Fgi`; `Sgcf.Domain.Contratos.FgiDetail` | Produto BNDES ofertado via banco repassador. O contrato é estruturado como linha BNDES-FGI. Tarifa FGI anual sobre saldo devedor. **Escopo desta SPEC.** |
| **FGI-garantia** | `TipoGarantia.Fgi`; `GarantiaExigidaLimite` no `LimiteBanco`; `Sgcf.Domain.Contratos.GarantiaFgiDetail` | Cobertura FGI exigida como garantia em **qualquer** modalidade (FINIMP, NCE, Capital de Giro, etc.). **Já implementado em v0.6.0** — fora desta SPEC. |

**Nota histórica:** o design original previa um terceiro conceito ("FGI-anexo" via `BalcaoCaixaDetail.TemFgi`), removido na correção de 2026-05-18. Hoje, quando um Capital de Giro tem FGI exigida pelo banco, modela-se via `GarantiaExigidaLimite.Tipo=Fgi` no limite do banco — não há flag duplicada no detail.

Veja §13 para glossário completo com exemplos.

### 2.2. Tarifa FGI anual sobre saldo devedor

A linha FGI cobra uma **tarifa anual** (`TaxaFgiAa`) que incide sobre o saldo devedor da operação, separada da taxa de juros nominal (`TaxaAa`). No MVP, com FGI restrito a Bullet, o saldo devedor é constante e igual ao principal durante todo o prazo — a fórmula da tarifa é:

```
ValorTarifaFgi = Principal × TaxaFgiAa × PrazoDias / 360
```

Esta fórmula é **idêntica** à empregada por `GerarCronogramaCommand.AdicionarTarifaFgiAsync` (`src/Sgcf.Application/Contratos/Commands/GerarCronogramaCommand.cs` linhas 180–183), o que garante coerência entre o CET projetado na cotação e o CET observado após geração do cronograma do contrato.

A tarifa entra no CET porque é **custo direto recorrente do tomador**. Aparece no fluxo de caixa como saída em `t = prazoDias` (vencimento), junto com a amortização principal + juros.

### 2.3. PercentualCoberto e implicação em risco/CET

`PercentualCoberto` representa a fração do principal coberta pelo FGI em caso de inadimplência (ex.: 80%). É **informativa**:

- **Não há fluxo de caixa associado** durante a operação normal. O FGI só desembolsa se houver default — evento que está fora do modelo CET.
- **Não entra no cálculo do CET.** Alterar `PercentualCoberto` mantendo `TaxaFgiAa` constante **não altera o CET**.
- **Entra na proposta** como conteúdo informativo para o operador comparar diferentes propostas (banco A oferece 80%, banco B oferece 70% por taxa mais baixa).

### 2.4. Moeda

FGI é **BRL puro**. Não há conversão cambial; PTAX D-1 **não se aplica** (consistente com NCE e Capital de Giro). `Cotacao.PtaxUsadaUsdBrl = null` quando `Modalidade == Fgi` (invariante introduzido na Onda 0, F0.1).

---

## 3. Modelo de Dados

### 3.1. `Cotacao` — sem PTAX

`Cotacao` permanece como definido na Onda 0:

- `Modalidade = ModalidadeContrato.Fgi`
- `PtaxUsadaUsdBrl = null` (invariante: rejeitado se não-nulo para FGI)
- `DataPtaxReferencia = null`
- `ValorAlvoBRL`, `PrazoMaximoDias`, `Status` seguem regras do agregado (SPEC §3.2).

**Nenhum campo novo** em `Cotacao`.

### 3.2. `Proposta` — sem campos novos

Decisão **MD-5** travada: campos específicos de modalidade **não** entram no agregado `Proposta`. Os dados específicos do FGI (TaxaFgiAa, PercentualCoberto, NumeroOperacaoFgi) são capturados **no `ConverterEmContratoCommand`**, no momento de virar contrato. Isso preserva a estabilidade do agregado `Proposta` e evita explosão de campos opcionais.

A `Proposta` para FGI continua tendo:
- `TaxaAaPercentual` — taxa nominal de juros
- `IofPercentual` — IOF crédito
- `SpreadAaPercentual` — eventual spread
- `MoedaOriginal = Brl`
- `PrazoDias`, `EstruturaAmortizacao = Bullet`, `PeriodicidadeJuros`
- `ExigeNdf = false` (FGI é BRL puro)
- `GarantiaEhCdbCativo = false` (FGI já tem cobertura própria)
- `CetCalculadoAaPercentual` (cache; recalculado quando `TaxaFgiAaPercentual` é informada)

**Como o CET FGI pode ser projetado sem `TaxaFgiAa` na `Proposta`?** A `CalculadoraCet.CalcularCetFgi` recebe `FgiInputs` como parâmetro separado (ver §7). Durante a etapa de proposta/comparação, o operador pode informar a `TaxaFgiAa` esperada/oferecida pelo banco como input do query de comparação (`CompararPropostasQuery`). O valor definitivo é gravado no command de conversão.

### 3.3. `FgiDetail` (já existe) — modalidade

`FgiDetail` (`src/Sgcf.Domain/Contratos/FgiDetail.cs`) existe e é estável. Esta SPEC **não modifica** a entidade. Campos relevantes:

- `ContratoId` — FK 1:1 com `Contrato`
- `NumeroOperacaoFgi : string?` — código de identificação no sistema FGI
- `TaxaFgiAaDecimal : decimal?` (fração 0..1); exposto como `TaxaFgiAa : Percentual?`
- `PercentualCobertoBacking : decimal?` (fração 0..1); exposto como `PercentualCoberto : Percentual?`
- Auditoria: `CreatedAt`, `UpdatedAt`

Factory `FgiDetail.Criar` aceita valores em **percentual humano** (ex.: `0.5m` para 0,5%) e converte para fração internamente.

### 3.4. Disambiguação com `GarantiaFgiDetail`

| Entidade | Pasta | Quando usar |
|----------|-------|-------------|
| `Sgcf.Domain.Contratos.FgiDetail` | `Contratos/FgiDetail.cs` | **Esta SPEC.** Contrato cuja `Modalidade == Fgi` (produto BNDES via banco). |
| `Sgcf.Domain.Contratos.GarantiaFgiDetail` | `Contratos/GarantiaFgiDetail.cs` | Garantia FGI anexada a contrato de outra modalidade (FINIMP, NCE, Capital de Giro, etc.) cuja `GarantiaExigidaLimite.Tipo=Fgi` foi resolvida no contrato. |

**Removido em 2026-05-18:** `CapitalDeGiroDetail.TemFgi` flag (antes `BalcaoCaixaDetail.TemFgi`). Capital de Giro com FGI exigida agora modela-se exclusivamente via `GarantiaExigidaLimite.Tipo=Fgi` no cadastro do limite (v0.6.0).

---

## 4. Fluxo Funcional

### 4.1. Criar cotação FGI

```http
POST /api/v1/cotacoes
{
  "modalidade": "Fgi",
  "valorAlvoBrl": 500000.00,
  "prazoMaximoDias": 365,
  "observacoes": "Capital de giro com cobertura FGI 80%"
}
```

Comportamento:

- `CriarCotacaoCommandHandler` reconhece `modalidade == Fgi` e **pula** a busca de PTAX (Onda 0, §3.3).
- `Cotacao.PtaxUsadaUsdBrl` é `null`; `DataPtaxReferencia` é `null`.
- Status inicial: `Rascunho`.

### 4.2. Registrar proposta

```http
POST /api/v1/cotacoes/{id}/propostas
{
  "bancoId": "guid-banco-caixa",
  "moedaOriginal": "Brl",
  "valorOferecidoMoedaOriginal": 500000.00,
  "taxaAaPercentual": 12.0,
  "iofPercentual": 0.38,
  "spreadAaPercentual": 0,
  "prazoDias": 365,
  "estruturaAmortizacao": "Bullet",
  "periodicidadeJuros": "AoVencimento",
  "exigeNdf": false,
  "garantiaExigida": "FGI 80%",
  "valorGarantiaExigidaBrl": 0,
  "garantiaEhCdbCativo": false
}
```

Comportamento:

- A proposta é registrada **sem** os campos específicos do FGI (TaxaFgiAa, PercentualCoberto). A `Proposta` é "neutra de modalidade" (MD-5).
- O CET inicial pode ser calculado com `TaxaFgiAaPercentual` informada via query parameter ou cabeçalho, ou — preferencialmente — recalculado quando o operador chamar o comparativo informando as condições FGI esperadas (ver §5.4).
- Validador rejeita `EstruturaAmortizacao != Bullet` quando `cotacao.Modalidade == Fgi` (MVP).

### 4.3. Comparar propostas

```http
GET /api/v1/cotacoes/{id}/comparativo?taxaFgiAaPercentual=0.5&percentualCoberto=80
```

Comportamento:

- `CompararPropostasQuery` recebe `taxaFgiAaPercentual` e (opcionalmente) `percentualCoberto`.
- Para cada `Proposta`, recalcula CET via `CalculadoraCet.CalcularCetFgi(...)` com a tarifa anual informada.
- Retorna breakdown opcional: `cetBaseAaPct` (sem tarifa FGI) e `cetTotalAaPct` (com tarifa FGI).
- Operador pode iterar diferentes `taxaFgiAaPercentual` para sensibilidade — sem persistir até a conversão.

### 4.4. Aceitar proposta

Idêntico ao fluxo padrão da SPEC base. Não há especificidades FGI nesta etapa — campos específicos só serão capturados no próximo passo.

### 4.5. Converter em contrato

```http
POST /api/v1/cotacoes/{id}/converter-em-contrato
{
  "dataContratacao": "2026-06-01",
  "dataPrimeiroVencimento": "2027-06-01",
  "convencaoDataNaoUtil": "UtilSeguinte",
  "anchorDiaMes": null,
  "anchorDiaFixo": null,
  "numeroOperacaoFgi": "FGI-2026-CAIXA-001234",
  "taxaFgiAaPercentual": 0.5,
  "percentualCoberto": 80.0
}
```

Comportamento:

- `ConverterEmContratoCommandHandler` resolve o `IConversorModalidade` para `ModalidadeContrato.Fgi` — `ConversorFgi`.
- `ConversorFgi.CriarDetailAsync` invoca `FgiDetail.Criar(...)` passando os 3 inputs do command:
  - `numeroOperacaoFgi` (string?)
  - `taxaFgiAaPercentual` (decimal — **obrigatório** para FGI)
  - `percentualCoberto` (decimal? — opcional)
- O contrato é criado com `Modalidade = Fgi`. Em seguida, a chamada de `GerarCronogramaCommand` (manual ou via flow) gera o evento `TipoEventoCronograma.TarifaFgi` automaticamente.
- `EconomiaNegociacao` registra o snapshot da proposta e do contrato; CET do contrato é recalculado com `TaxaFgiAa` definitiva.

---

## 5. API

### 5.1. `POST /api/v1/cotacoes` com `modalidade=Fgi`

| Campo | Tipo | Obrigatório | Observação |
|-------|------|-------------|------------|
| `modalidade` | string | sim | `"Fgi"` |
| `valorAlvoBrl` | decimal | sim | Em BRL |
| `prazoMaximoDias` | int | sim | ≥ 1 |
| `dataPtaxReferencia` | date | **não** | Rejeitado se informado (FGI é BRL) |
| `ptaxUsadaUsdBrl` | decimal | **não** | Rejeitado se informado |
| `observacoes` | string | não | |

**Resposta (201 Created):**

```json
{
  "id": "01927b4c-...",
  "codigoInterno": "COT-2026-00042",
  "modalidade": "Fgi",
  "valorAlvoBrl": 500000.00,
  "prazoMaximoDias": 365,
  "dataAbertura": "2026-05-18",
  "dataPtaxReferencia": null,
  "ptaxUsadaUsdBrl": null,
  "status": "Rascunho"
}
```

### 5.2. `POST /api/v1/cotacoes/{id}/propostas` — sem campos novos

Schema idêntico ao do MVP FINIMP (SPEC §7.2), respeitando:

- `moedaOriginal = "Brl"` (qualquer outro valor é rejeitado para cotação FGI)
- `estruturaAmortizacao = "Bullet"` (Price/Sac rejeitados — limitação MVP)
- `exigeNdf = false` (NDF não se aplica a BRL)
- `garantiaEhCdbCativo = false` (FGI já é cobertura)

### 5.3. `POST /api/v1/cotacoes/{id}/converter-em-contrato`

Campos específicos para FGI:

| Campo | Tipo | Obrigatório | Faixa | Observação |
|-------|------|-------------|-------|------------|
| `numeroOperacaoFgi` | string? | não | livre | Código da operação no sistema FGI |
| `taxaFgiAaPercentual` | decimal | **sim** | `> 0` | Em percentual humano (ex.: `0.5` = 0,5% a.a.) |
| `percentualCoberto` | decimal? | não | `(0, 100]` | Em percentual humano (ex.: `80.0` = 80%) |

Campos não-aplicáveis a FGI ficam nulos/ignorados: `rofNumero`, `exportadorNome`, `exportadorPais`, `produtoImportado` (FINIMP); `numeroOperacaoLei4131`, `sblcCusto`, `aliqIrrf` (Lei 4131); etc.

**Validações no handler:**

1. `taxaFgiAaPercentual` ausente ou ≤ 0 → 400 `"TaxaFgiAaPercentual é obrigatória para modalidade FGI."`
2. `percentualCoberto > 100` → 400 `"PercentualCoberto deve ser ≤ 100%."`
3. `percentualCoberto ≤ 0` (quando informado) → 400 `"PercentualCoberto deve ser maior que zero quando informado."`

### 5.4. Comparativo

```http
GET /api/v1/cotacoes/{id}/comparativo?taxaFgiAaPercentual=0.5
```

**Resposta:**

```json
{
  "cotacaoId": "...",
  "modalidade": "Fgi",
  "propostas": [
    {
      "propostaId": "...",
      "bancoNome": "Caixa Econômica Federal",
      "taxaNominalAaPct": 12.0,
      "cetBaseAaPct": 12.45,
      "tarifaFgiAaPct": 0.5,
      "cetTotalAaPct": 12.97,
      "garantiaExigida": "FGI 80%"
    }
  ]
}
```

A coluna `cetTotalAaPct` é o ranking principal. `cetBaseAaPct` é breakdown opcional para o operador conversar com o banco ("baixe a taxa nominal e eu aceito a tarifa FGI mais alta").

---

## 6. Conversor (`IConversorModalidade`)

### 6.1. `ConversorFgi`

**Arquivo (novo):** `src/Sgcf.Application/Cotacoes/Conversores/ConversorFgi.cs`

Responsável por criar `FgiDetail` na conversão. Estrutura:

```csharp
public sealed class ConversorFgi : IConversorModalidade
{
    public ModalidadeContrato Modalidade => ModalidadeContrato.Fgi;

    public Task<(Entity Principal, Entity? Secundario)> CriarDetailAsync(
        ConverterEmContratoContext ctx,
        CancellationToken cancellationToken)
    {
        ConverterEmContratoCommand cmd = ctx.Command;

        if (!cmd.TaxaFgiAaPercentual.HasValue || cmd.TaxaFgiAaPercentual.Value <= 0m)
        {
            throw new InvalidOperationException(
                "TaxaFgiAaPercentual é obrigatória e deve ser > 0 para modalidade FGI.");
        }

        if (cmd.PercentualCoberto is decimal pc && (pc <= 0m || pc > 100m))
        {
            throw new InvalidOperationException(
                "PercentualCoberto deve estar em (0, 100] quando informado.");
        }

        FgiDetail detail = FgiDetail.Criar(
            ctx.ContratoCriado.Id,
            cmd.NumeroOperacaoFgi,
            cmd.TaxaFgiAaPercentual.Value,
            cmd.PercentualCoberto,
            ctx.Clock);

        return Task.FromResult<(Entity, Entity?)>((detail, null));
    }
}
```

Retorna **(`FgiDetail`, `null`)** — não há detail secundário. A dualidade `(Principal, Secundario)` da interface existe para acomodar Capital de Giro + Garantia FGI; FGI-modalidade não a utiliza.

### 6.2. Registro DI

Já previsto na Onda 0 (§5.6 da SPEC Onda 0):

```csharp
services.AddScoped<IConversorModalidade, ConversorFgi>();
```

Substituir o stub `NotImplementedException` do `ConversorFgi` (Onda 0) pela implementação completa nesta SPEC.

---

## 7. CET

### 7.1. Fórmula `CalcularCetFgi`

**Arquivo:** `src/Sgcf.Application/Cotacoes/CalculadoraCet.cs` (método já declarado na Onda 0; implementação concreta nesta SPEC).

**Assinatura:**

```csharp
internal static decimal CalcularCetFgi(
    Proposta proposta,
    LocalDate dataReferencia,
    FgiInputs fgi,
    decimal? taxaAaPercentualOverride = null);

public sealed record FgiInputs(decimal TaxaFgiAaPercentual, decimal? PercentualCoberto);
```

**Inputs efetivamente consumidos:**

- `proposta.TaxaAaPercentual` (override `taxaAaPercentualOverride` aplicado se presente)
- `proposta.IofPercentual` (IOF crédito sobre principal, em `t = 0`)
- `proposta.PrazoDias`
- `proposta.ValorOferecidoMoedaOriginal` (em BRL)
- `proposta.EstruturaAmortizacao` (deve ser `Bullet` — caso contrário lança)
- `fgi.TaxaFgiAaPercentual` (em percentual humano, ex.: `0.5` = 0,5% a.a.)
- `fgi.PercentualCoberto` (**ignorado** — informativo)
- `dataReferencia` (para cálculo de TIR data-base)

**Fluxo de caixa modelado (BRL):**

| Instante | Componente | Valor |
|----------|------------|-------|
| `t = 0` | Desembolso (entrada) | `+ Principal` |
| `t = 0` | IOF crédito (saída) | `- Principal × IofPercentual / 100` |
| `t = prazoDias` | Amortização do principal (saída) | `- Principal` |
| `t = prazoDias` | Juros (saída) | `- Principal × TaxaAa × PrazoDias / 360` |
| `t = prazoDias` | **Tarifa FGI** (saída) | `- Principal × TaxaFgiAa × PrazoDias / 360` |

**Base de cálculo:** 360 dias (decisão travada — consistente com FINIMP/NCE/Capital de Giro). Helper `BaseCalculo.Dias360`.

**TIR → CET anualizado:** o motor existente (`Sgcf.Domain.Cronograma` + utilitários de TIR usados em `CalcularCetFinimp`) calcula a TIR do fluxo. CET = TIR anualizada (base 360).

**Coerência com `GerarCronogramaCommand.AdicionarTarifaFgiAsync`:**

A linha 180 de `GerarCronogramaCommand.cs` aplica:

```csharp
valorTarifaFgi = principal × taxaFgiAa × prazo / baseCalculo
```

A `CalcularCetFgi` deve usar **exatamente a mesma expressão** com `baseCalculo = 360`. Qualquer refatoração futura deve extrair um helper compartilhado para garantir consistência.

### 7.2. `PercentualCoberto` não entra no CET

Reforçando: `fgi.PercentualCoberto` aparece na assinatura para documentar o input mas é **descartado** no cálculo. Teste de regressão valida essa invariante (§9.2).

### 7.3. Golden case

**Cenário:** FGI BRL Bullet, principal R$ 500.000,00, prazo 365 dias, `TaxaAa = 12,0%`, `IofPercentual = 0,38%`, `TaxaFgiAa = 0,5%`, `PercentualCoberto = 80%`.

| Componente | Valor |
|------------|-------|
| Desembolso (t=0) | + 500.000,00 |
| IOF (t=0) | − 1.900,00 |
| Juros (t=365) | − 60.000,00 |
| Principal (t=365) | − 500.000,00 |
| Tarifa FGI (t=365) | − 2.500,00 |

CET esperado: aproximadamente **13,00–13,02% a.a.** (valor exato calculado por TIR e validado pelo time financeiro — registrado em `tests/Sgcf.GoldenDataset/data/cotacoes/cotacao-fgi-bullet-12m.json`).

---

## 8. Edge Cases

| # | Cenário | Comportamento esperado |
|---|---------|------------------------|
| EC-1 | Operador escolhe `modalidade = Fgi` mas `estruturaAmortizacao = Price` ou `Sac` na proposta | **Rejeição** no `RegistrarPropostaCommandValidator`: 400 `"Modalidade FGI suporta apenas EstruturaAmortizacao.Bullet no MVP."` |
| EC-2 | `taxaFgiAaPercentual` ausente no command de conversão | **Rejeição** no `ConversorFgi` (e antes no validator do command): 400 `"TaxaFgiAaPercentual é obrigatória para modalidade FGI."` |
| EC-3 | `percentualCoberto > 100` | **Rejeição:** 400 `"PercentualCoberto deve ser ≤ 100%."` |
| EC-4 | `percentualCoberto = 0` (positivo, mas degenerado) | **Rejeição:** 400 `"PercentualCoberto deve ser maior que zero quando informado."` (cobertura zero não faz sentido prático — usar `null` se cobertura indefinida) |
| EC-5 | `percentualCoberto` não informado (`null`) | **Permitido.** `FgiDetail.PercentualCoberto` fica `null`; CET inalterado. |
| EC-6 | Prazo < 360 dias (ex.: 180 dias) | **Permitido.** Tarifa anual é rateada pelo prazo: `ValorTarifa = principal × taxaFgi × 180 / 360`. Mesma regra do cronograma. |
| EC-7 | Prazo > 360 dias com FGI Bullet (ex.: 720 dias) | **Permitido no MVP** com tarifa única no vencimento (`taxa × prazo / 360`). Limitação documentada: bancos podem cobrar tarifa por janela anual — neste caso o operador registra a tarifa equivalente acumulada como `TaxaFgiAaPercentual` ajustada, ou a evolução pós-MVP implementa Price/SAC com cobrança anual. |
| EC-8 | Banco cadastrado como **não-Caixa/BV** sendo usado em cotação FGI | **Permitido.** O modelo não restringe banco operador; outros bancos credenciados pelo BNDES podem operar FGI. Operador é responsável pela credenciamento. |
| EC-9 | Cotação FGI com `LimiteBanco` cuja `GarantiasExigidas` contém `TipoGarantia.Fgi` | **Permitido — porém alerta informativo.** O limite FGI tipicamente não exige outra garantia FGI sobreposta (seria redundância conceitual). Sistema não bloqueia; apenas registra alerta na resposta do `AdicionarBancoNaCotacao`. |
| EC-10 | `moedaOriginal != Brl` em proposta de cotação FGI | **Rejeição:** 400 `"Modalidade FGI exige propostas em BRL."` |
| EC-11 | Tentativa de informar PTAX na criação da cotação FGI | **Rejeição** via invariante do agregado (Onda 0 §3.2): 400 `"PTAX não se aplica à modalidade Fgi (operação em BRL)."` |
| EC-12 | Conversão chamada sem `taxaFgiAaPercentual` mas com `EstruturaAmortizacao = Bullet` correta | **Rejeição** no command validator antes do `ConversorFgi` ser invocado. |
| EC-13 | `taxaFgiAaPercentual = 0` no command de conversão | **Rejeição:** 400 `"TaxaFgiAaPercentual deve ser > 0."` (tarifa zero implica que não há FGI — registrar como outra modalidade). |
| EC-14 | Recálculo de CET após conversão diverge do CET projetado na cotação | **Tolerado se diferença < 1 bp.** Diferença maior deve ser investigada — provável drift entre `CalcularCetFgi` e `GerarCronogramaCommand`. Ver §10. |

---

## 9. Testes

### 9.1. Domain

**Arquivo:** `tests/Sgcf.Domain.Tests/Cotacoes/CotacaoFgiTests.cs`

- `Criar_cotacao_Fgi_sem_PTAX_sucesso` (regressão Onda 0)
- `Criar_cotacao_Fgi_com_PTAX_rejeitado`
- `AdicionarProposta_Fgi_com_estrutura_Bullet_sucesso`
- `AdicionarProposta_Fgi_com_estrutura_Price_rejeitado`
- `AdicionarProposta_Fgi_com_moeda_USD_rejeitado`

**Arquivo:** `tests/Sgcf.Domain.Tests/Contratos/FgiDetailTests.cs` (regressão — já existente)

### 9.2. Application

**Arquivo:** `tests/Sgcf.Application.Tests/Cotacoes/CalculadoraCetFgiTests.cs`

- `CalcularCetFgi_bullet_12m_BRL_valor_consistente_com_golden`
- `CalcularCetFgi_com_PercentualCoberto_50pct_resultado_igual_PercentualCoberto_100pct` (regressão: cobertura não entra no CET)
- `CalcularCetFgi_com_TaxaFgi_zero_lanca_excecao` (degenerado)
- `CalcularCetFgi_com_TaxaFgi_positiva_resultado_maior_que_sem_tarifa` (property)
- `CalcularCetFgi_com_prazo_180_dias_tarifa_rateada` (50% da tarifa anual)
- `CalcularCetFgi_com_EstruturaAmortizacao_Price_lanca_NotSupportedException` (MVP só Bullet)
- `Fachada_CalcularCet_modalidade_Fgi_dispatcheia_para_CalcularCetFgi`

**Arquivo:** `tests/Sgcf.Application.Tests/Cotacoes/Conversores/ConversorFgiTests.cs`

- `CriarDetailAsync_com_inputs_completos_retorna_FgiDetail_populado`
- `CriarDetailAsync_sem_TaxaFgiAa_lanca`
- `CriarDetailAsync_com_PercentualCoberto_acima_de_100_lanca`
- `CriarDetailAsync_com_PercentualCoberto_zero_lanca`
- `CriarDetailAsync_sem_PercentualCoberto_persiste_null`
- `CriarDetailAsync_Secundario_eh_sempre_null`

### 9.3. Integration (E2E HTTP)

**Arquivo:** `tests/Sgcf.Api.IntegrationTests/Cotacoes/CotacaoFgiE2ETests.cs`

- `Fluxo_completo_Fgi_cria_proposta_aceita_converte_contrato_com_FgiDetail`
- `Cotacao_Fgi_aceita_proposta_BRL_Bullet`
- `Cotacao_Fgi_rejeita_proposta_USD` (400)
- `Cotacao_Fgi_rejeita_proposta_Price` (400)
- `Converter_Fgi_sem_TaxaFgi_retorna_400`
- `Converter_Fgi_com_PercentualCoberto_invalido_retorna_400`

### 9.4. Golden dataset

**Arquivo:** `tests/Sgcf.GoldenDataset/data/cotacoes/cotacao-fgi-bullet-12m.json`

- `input`: principal R$ 500.000, prazo 365d, TaxaAa 12%, IOF 0,38%, TaxaFgiAa 0,5%, PercentualCoberto 80%.
- `expectedOutput.cetAaPct`: valor calculado e validado pelo time financeiro com precisão de 6 casas decimais.

### 9.5. Teste comparativo CET cotação ↔ contrato

**Arquivo:** `tests/Sgcf.Api.IntegrationTests/Cotacoes/CetFgiCoerenciaTests.cs`

Cenário: criar cotação FGI, registrar proposta, converter em contrato, chamar `GerarCronogramaCommand`. Asseverar:

1. `EconomiaNegociacao.CetContratoAaPercentual` (calculado pós-cronograma com tarifa FGI inclusa) ≈ `EconomiaNegociacao.CetPropostaAaPercentual` dentro de 1 bp.
2. Soma dos eventos `TarifaFgi` no cronograma = `principal × taxaFgi × prazo / 360`.
3. Drift > 1 bp falha o teste (sinaliza divergência entre `CalcularCetFgi` e `AdicionarTarifaFgiAsync`).

---

## 10. Boundaries

### 10.1. Always

- **Validar coerência da fórmula tarifa FGI entre `CalcularCetFgi` e `GerarCronogramaCommand.AdicionarTarifaFgiAsync`.** Idealmente extrair helper compartilhado em refatoração pós-MVP. No MVP, testes comparativos (§9.5) protegem.
- **Tarifa FGI sempre em t = prazoDias** (vencimento) no modelo CET enquanto a estrutura é Bullet.
- **`PercentualCoberto` apenas informativo** — não pode jamais alterar o CET.
- **Registrar `TaxaFgiAa` no snapshot `EconomiaNegociacao.SnapshotContratoJson`** — auditoria depende de fixar o valor usado na conversão.
- **Rejeitar `EstruturaAmortizacao != Bullet`** no validator antes de chegar ao `CalcularCetFgi`.

### 10.2. Ask first

- Implementar Price/SAC com FGI (tarifa anual sobre saldo médio) — exige revisão do `AdicionarTarifaFgiAsync` para gerar evento por janela anual.
- Adicionar `TipoFgi` (PEAC/NOVO_EMPREENDEDOR) à `FgiDetail` modalidade — hoje só `GarantiaFgiDetail` carrega.
- Validar cobertura máxima regulatória por subprograma (ex.: PEAC 80%).
- Permitir cotação FGI com moeda diferente de BRL (não há precedente regulatório, mas se programa internacionalizar).

### 10.3. Never

- **Confundir modalidade FGI com garantia FGI.** Glossário (§13) é obrigatório em qualquer revisão.
- Adicionar `TaxaFgiAa` ao agregado `Proposta` (viola MD-5).
- Permitir conversão de cotação FGI sem `taxaFgiAaPercentual` no command — sentinela 0 ou null não é aceito.
- Incluir `PercentualCoberto` no cálculo do CET — é informativo.
- Sobrescrever `FgiDetail` existente após conversão (snapshot imutável; ver MD-7).
- Aplicar PTAX D-1 a cotação FGI (Onda 0 invariante).

---

## 11. Documentação a atualizar

| Documento | Mudança |
|-----------|---------|
| `docs/specs/cotacoes/SPEC.md` | §2 (glossário): incluir entrada "FGI (modalidade)" distinta de "FGI (garantia)" e "FGI (anexo Capital de Giro)" — referenciar §13 desta SPEC. §11.2: remover FGI da lista "out of scope". |
| `docs/api/cotacoes.md` | Documentar campos novos no `POST /api/v1/cotacoes/{id}/converter-em-contrato` (taxaFgiAaPercentual, percentualCoberto, numeroOperacaoFgi). Documentar parâmetro opcional `taxaFgiAaPercentual` no `GET /api/v1/cotacoes/{id}/comparativo`. |
| `docs/api/limites-banco.md` | Referência cruzada: `GarantiaExigidaLimite.Tipo = Fgi` é **garantia anexa** a outra modalidade — não confundir com modalidade FGI desta SPEC. |
| `docs/api/collections/sgcf-api/.../Cotacoes/` (Bruno) | Novos requests: "POST cotacao FGI", "POST proposta FGI", "GET comparativo FGI", "POST converter em contrato FGI". |
| `docs/changelog/CHANGELOG.md` | `[0.9.0]` — bloco `ADDITIVE — Cotações — Modalidade FGI`. |

---

## 12. Plano de Implementação

Referência detalhada: `tasks/cotacoes-modalidades/fgi/plan.md`. Tarefas principais, na ordem do caminho crítico:

1. **(F0 já entregue)** Stub `ConversorFgi` lançando `NotImplementedException`; `CalcularCetFgi` lançando `NotImplementedException`.
2. **Domain:** estender validações em `Cotacao.AdicionarProposta` para `Modalidade == Fgi` rejeitar Price/SAC e moeda não-BRL.
3. **Application — `CalcularCetFgi`:** implementar fluxo BRL Bullet com tarifa FGI no vencimento.
4. **Application — `ConversorFgi`:** substituir stub por implementação que cria `FgiDetail`.
5. **Application — `ConverterEmContratoCommand`:** adicionar campos `numeroOperacaoFgi`, `taxaFgiAaPercentual`, `percentualCoberto` ao record do command. Validar no validator.
6. **Application — `CompararPropostasQuery`:** aceitar `taxaFgiAaPercentual` opcional; calcular CET via `CalcularCetFgi` para propostas de cotações FGI.
7. **API — Controller:** mapear novos campos.
8. **Testes:** Domain, Application, IntegrationTests, Golden dataset (§9).
9. **Documentação:** SPEC, API docs, Bruno, CHANGELOG.
10. **Code review humano** focado em §2.1 (disambiguação) e §10 (boundaries).

Sequenciamento e paralelização: ver §7 do plano de tarefas.

---

## 13. Glossário FGI (disambiguação obrigatória)

| Conceito | Onde no domínio | Significado | Exemplo |
|----------|-----------------|-------------|---------|
| **FGI-modalidade** | `ModalidadeContrato.Fgi`; agregado `Sgcf.Domain.Contratos.FgiDetail` (1:1 com `Contrato`). Tabela `fgi_detail`. | Produto BNDES ofertado via banco repassador. A operação inteira é estruturada como linha BNDES-FGI. Tarifa anual (`TaxaFgiAa`) cobrada sobre saldo devedor, gerando eventos `TarifaFgi` no cronograma (`GerarCronogramaCommand.cs` linha 116). | Banco repassador concede operação BNDES-FGI de R$ 500k para PME; o contrato é o instrumento da própria linha FGI. **Esta SPEC.** |
| **FGI-garantia** | `TipoGarantia.Fgi` em `GarantiaExigidaLimite` (no `LimiteBanco`); agregado `Sgcf.Domain.Contratos.GarantiaFgiDetail` (1:1 com `Garantia`). Tabela `garantia_fgi_detail`. | Cobertura FGI exigida como garantia em uma modalidade **diferente** de FGI. O FGI funciona como mitigador de risco; a operação é, por exemplo, NCE, Capital de Giro ou FINIMP. Carrega `TipoFgi` (PEAC, NOVO_EMPREENDEDOR), `PercentualCobertura`, `TaxaFgiAa`, `BancoIntermediario`, `CodigoOperacaoBndes`. | Cotação Capital de Giro no banco X exige garantia FGI 80% PEAC. Banco X cadastra `GarantiaExigidaLimite{Tipo=Fgi}` no `LimiteBanco` da modalidade Capital de Giro. **Já implementado em v0.6.0 — fora desta SPEC.** |
| ~~FGI-anexo~~ | ~~`BalcaoCaixaDetail.TemFgi`~~ | **Removido em 2026-05-18.** O conceito original duplicava `FGI-garantia`. Capital de Giro com FGI hoje modela-se exclusivamente via `GarantiaExigidaLimite.Tipo=Fgi` no limite do banco. | n/a |
| **TaxaFgiAa** | Percentual anual cobrado sobre saldo devedor. Armazenado como fração (0..1) em `decimal?`; exposto como `Percentual?`. | Aparece em **três** lugares: `FgiDetail.TaxaFgiAa` (modalidade), `GarantiaFgiDetail.TaxaFgiAa` (garantia), e como input do command `ConverterEmContratoCommand.TaxaFgiAaPercentual` (modalidade) ou `CreateContratoCommand.FgiDetailRequest.TaxaFgiAaPct` (modalidade). | 0,5% a.a. é valor típico. |
| **PercentualCoberto / PercentualCobertura** | Fração do principal coberta pelo FGI em default. `FgiDetail.PercentualCoberto` (modalidade) e `GarantiaFgiDetail.PercentualCobertura` (garantia) — nomes ligeiramente diferentes por razões históricas. | **Não entra no CET**; informativo apenas. | 80% típico para PEAC. |
| **TarifaFgi (evento)** | `TipoEventoCronograma.TarifaFgi`. Evento gerado por `GerarCronogramaCommand.AdicionarTarifaFgiAsync` (linhas 160–195). | Cobrança da tarifa anual FGI projetada no cronograma; valor = `principal × TaxaFgiAa × prazoDias / 360`. Único no MVP (Bullet). | Evento `TarifaFgi` no vencimento. |
| **NumeroOperacaoFgi** | `FgiDetail.NumeroOperacaoFgi : string?`. | Código identificador da operação no sistema FGI / BNDES. Opcional na cotação; preenchido quando o banco repassa o número. | `"FGI-2026-CAIXA-001234"`. |
| **TipoFgi (subprograma)** | `GarantiaFgiDetail.TipoFgi : string` (apenas garantia). | Subprograma do FGI: PEAC (Programa Emergencial de Acesso a Crédito), NOVO_EMPREENDEDOR, etc. **Não modelado em `FgiDetail`** (modalidade) no MVP. | `"FGI_PEAC"`. |
| **BancoIntermediario** | `GarantiaFgiDetail.BancoIntermediario : string?` (apenas garantia). | Banco operador do FGI via BNDES quando difere do banco da operação principal. **Não modelado em `FgiDetail`** (modalidade) — `BancoId` da `Proposta` cumpre o papel. | `"Caixa Econômica Federal"`. |

---

## 14. Histórico

| Data       | Versão | Mudança                                            |
| ---------- | ------ | -------------------------------------------------- |
| 2026-05-18 | v1.0   | Draft inicial. Baseado no plano fgi/ e decisões MD-3, MD-5, MD-8. |
