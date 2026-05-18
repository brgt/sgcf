# SPEC — Modalidade REFINIMP no Módulo de Cotações

**Versão alvo:** v0.7.0
**Status:** Implementado — Onda 1 entregue em 2026-05-18
**Pré-requisito:** Onda 0 (foundation) entregue — mas REFINIMP **pode ser desenvolvido em PARALELO** com a Onda 0 (não depende de PTAX nullable nem dos branches BRL da `CalculadoraCet`; herda apenas a interface `IConversorModalidade` quando esta estiver disponível, caso contrário usa o `if (... == Refinimp)` análogo ao FINIMP).
**Plano de execução:** `tasks/cotacoes-modalidades/refinimp/plan.md`
**Spec MVP que estende:** `docs/specs/cotacoes/SPEC.md`
**Spec foundation referenciada:** `docs/specs/cotacoes/modalidades/onda-0.md`
**Decisões mestre referenciadas:** `tasks/cotacoes-modalidades/plan.md` §2 (MD-1..MD-10)

---

## 1. Objetivo

Habilitar o módulo de Cotações a registrar, comparar e converter propostas de **refinanciamento de FINIMP (REFINIMP)** — operação na qual uma empresa toma novo crédito, nas mesmas moeda e condições gerais, para quitar (parcial ou totalmente) um FINIMP existente (chamado contrato mãe). Após a conversão, o contrato mãe é marcado como `RefinanciadoParcial` ou `RefinanciadoTotal` (estados já existentes no domínio de Contratos) e o novo contrato carrega um `RefinimpDetail` com a rastreabilidade da cadeia.

A entrega desta SPEC permite que toda operação REFINIMP — hoje gerada apenas via criação direta de contrato — passe a nascer no fluxo de cotações com captura de propostas concorrentes, comparação estruturada e medição de economia negociada, alinhada ao MVP FINIMP.

### 1.1. Não-objetivos

1. **Suporte cross-currency** (refinanciar mãe em USD com proposta em BRL). REFINIMP nasce na mesma moeda do mãe; alteração de moeda é evolução fora desta SPEC. Ver §10.
2. **Reconciliação automática de `LimiteBanco.Finimp`** quando o mãe é marcado `RefinanciadoTotal`. O REFINIMP consome limite próprio (`Modalidade = Refinimp`); a quitação efetiva do mãe não decrementa automaticamente o limite FINIMP. Decisão registrada em `tasks/cotacoes-modalidades/refinimp/plan.md` §6 Q3.
3. **Imposição de prazo da proposta ≤ prazo restante do mãe.** O sistema atual não impõe; introduzir essa regra é evolução. Ver §10.
4. **Reabertura de cotação REFINIMP recusada.** Permanece regra global do MVP (criar nova cotação).
5. **Renumeração de `ModalidadeContrato`.** Proibido por MD-2 (`tasks/cotacoes-modalidades/plan.md`). `Refinimp = 2` permanece imutável.

---

## 2. Conceito de Negócio

### 2.1. Casos reais e exemplos

**Caso A — Refinanciamento parcial de FINIMP USD com BB:**

> A empresa tem um FINIMP USD de 1.000.000 com banco X vencendo em 90 dias. Recebe oferta do BB para refinanciar 500.000 (50%) à taxa menor. Cria cotação REFINIMP USD apontando para o FINIMP mãe, captura propostas de BB e Itaú, escolhe a melhor e converte. O FINIMP mãe passa para `RefinanciadoParcial`; um novo contrato REFINIMP USD 500.000 nasce com `RefinimpDetail.PercentualRefinanciado = 0.50` e `ContratoMaeId = <id do mãe>`.

**Caso B — Refinanciamento total de FINIMP CNY:**

> FINIMP CNY 5.000.000 com banco Y. Empresa fecha REFINIMP CNY 5.000.000 com banco Z para liquidar 100%. Mãe passa para `RefinanciadoTotal`; novo REFINIMP carrega `PercentualRefinanciado = 1.00`.

**Caso C — Cadeia recursiva (refi de refi):**

```
FINIMP USD 1.000.000  (ancestral original)
    └── REFINIMP USD 700.000  (refi 70% do mãe)
            └── REFINIMP USD 400.000  (refi 57,14% do imediato; 40% do ancestral)
                    └── REFINIMP USD ???  (regra 70% BB sempre sobre ancestral original)
```

O `ContratoMaeId` aponta para o pai **imediato** da cadeia. Para qualquer validação cumulativa contra o principal original, usa-se `IContratoRepository.GetAncestraNaoRefinimpAsync` que percorre a cadeia até encontrar o FINIMP raiz.

### 2.2. Diferenças em relação a FINIMP

| Dimensão                         | FINIMP                                                      | REFINIMP                                                                                                                       |
| -------------------------------- | ----------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------ |
| Origem                           | Captação nova vinculada a importação                        | Refinanciamento de FINIMP (ou outro REFINIMP em cadeia) existente                                                              |
| Moeda                            | Definida pela proposta (USD, EUR, CNY...)                   | **Obrigatoriamente igual** à do contrato mãe imediato                                                                          |
| `Cotacao.ContratoMaeId`          | Sempre `null`                                               | Obrigatório, aponta para o pai imediato                                                                                        |
| Validação `Banco.AceitaRefinimp` | N/A                                                         | Validada fail-fast em `AdicionarBancoNaCotacaoCommand`                                                                         |
| Regra 70% BB                     | N/A                                                         | Aplicada na conversão; teto = `0.70 × principal do ancestral original`                                                         |
| Cálculo de CET                   | `CalculadoraCet.CalcularCetFinimp` (Onda 0)                 | `CalculadoraCet.CalcularCetRefinimp` que delega para `CalcularCetFinimp` — mesma fórmula                                       |
| Conversor                        | `ConversorFinimp` (Onda 0) cria `FinimpDetail`              | `ConversorRefinimp` cria `RefinimpDetail` **e** marca o contrato mãe como `RefinanciadoParcial`/`RefinanciadoTotal`            |
| PTAX D-1                         | Obrigatória (`Cotacao.ExigeMoedaEstrangeira(Refinimp)`)     | Obrigatória (mesma helper retorna `true` para Refinimp em `onda-0.md` §3.2)                                                    |
| `LimiteBanco`                    | `Modalidade = Finimp`                                       | `Modalidade = Refinimp` — linha separada por banco (MD-3)                                                                      |
| Status final do mãe              | N/A                                                         | Mãe transita para `RefinanciadoTotal` (se 100%) ou `RefinanciadoParcial` (se < 100%)                                           |

### 2.3. Regras de banco

1. **`Banco.AceitaRefinimp`** (`src/Sgcf.Domain/Bancos/Banco.cs:17`): flag booleana persistida no agregado `Banco`. Quando `false`, o banco não pode ser adicionado a uma cotação REFINIMP. Validação fail-fast em `AdicionarBancoNaCotacaoCommand` (não esperar até a conversão — `tasks/cotacoes-modalidades/refinimp/plan.md` AD-4).

2. **Regra 70% do Banco do Brasil (CodigoCompe `001`)**: o `ValorPrincipal` do REFINIMP não pode exceder `0.70 × valorPrincipalAncestral`, onde `ancestral` é o resultado de `GetAncestraNaoRefinimpAsync` (o FINIMP raiz da cadeia). Aplicada na conversão; espelha a regra existente em `CreateContratoCommand.ProcessarRefinimpAsync` linhas 388–396.

   - Exemplo: ancestral FINIMP USD 1.000.000 → REFINIMP BB pode no máximo cobrir USD 700.000.
   - Em **cadeia recursiva**, o teto é sempre relativo ao ancestral original, não ao pai imediato. Se já existem REFINIMP BB anteriores na cadeia que somam 50% do ancestral, este SPEC **não impõe** soma cumulativa: cada operação isolada respeita 70%; soma cumulativa fica fora de escopo (questão aberta — `tasks/cotacoes-modalidades/refinimp/plan.md` §6 Q5).
   - Bancos diferentes de BB não têm essa restrição.

3. **`LimiteBanco.Refinimp`**: cadastro de limite separado. Adicionar banco à cotação REFINIMP exige `LimiteDisponivelBRL >= ValorAlvoBRL` na **modalidade Refinimp**, não na FINIMP do mesmo banco.

---

## 3. Modelo de Dados

### 3.1. Cotacao — extensão

**Arquivo:** `src/Sgcf.Domain/Cotacoes/Cotacao.cs`

Adicionar propriedade:

```csharp
public Guid? ContratoMaeId { get; private set; }
```

**Captura no momento da criação** (rascunho), não diferida para conversão. Justificativa:

- A escolha do contrato mãe é a decisão de negócio que origina a cotação — define moeda obrigatória da proposta, ancestral para a regra 70%, e contexto para o comparativo.
- Permite validar moeda da proposta logo no `RegistrarPropostaCommand` (não esperar conversão).
- Permite pré-cálculo informativo da regra 70% BB no `CompararPropostasQuery` (mitigação de risco — `tasks/cotacoes-modalidades/refinimp/plan.md` §5).

**Factory `Cotacao.Criar`** ganha parâmetro opcional `Guid? contratoMaeId = null`. Invariantes novos (combinados com os da Onda 0):

```csharp
// AD-1: REFINIMP exige ContratoMaeId
if (modalidade == ModalidadeContrato.Refinimp &&
    (contratoMaeId is null || contratoMaeId == Guid.Empty))
{
    throw new ArgumentException(
        "ContratoMaeId é obrigatório para cotação da modalidade Refinimp.",
        nameof(contratoMaeId));
}

// AD-1 (defesa): outras modalidades rejeitam ContratoMaeId
if (modalidade != ModalidadeContrato.Refinimp && contratoMaeId is not null)
{
    throw new ArgumentException(
        $"ContratoMaeId não se aplica à modalidade {modalidade}.",
        nameof(contratoMaeId));
}
```

Onda 0 já garante que `Refinimp` está em `ExigeMoedaEstrangeira` (`onda-0.md` §3.2), portanto PTAX D-1 continua obrigatória — não há conflito com a invariante de PTAX nullable.

### 3.2. Proposta — sem novos campos

**Decisão MD-5 (plano mestre):** `Proposta` permanece pequena e estável. Nenhum campo específico de REFINIMP é adicionado ao agregado. Campos contextuais (por exemplo, "qual contrato mãe está sendo refinanciado") são derivados da `Cotacao` parent quando necessário em queries ou cálculos.

Para REFINIMP, a validação `MoedaOriginal == contratoMae.Moeda` ocorre no handler de `RegistrarPropostaCommand` (carrega o mãe via `IContratoRepository.GetByIdAsync`), **não** como invariante do agregado `Proposta`, pois o agregado não conhece o mãe.

### 3.3. RefinimpDetail (já existente — sem mudanças)

**Arquivo:** `src/Sgcf.Domain/Contratos/RefinimpDetail.cs`

| Campo                              | Tipo            | Notas                                                                       |
| ---------------------------------- | --------------- | --------------------------------------------------------------------------- |
| `ContratoId`                       | `Guid`          | Id do contrato REFINIMP recém-criado (1:1 com `Contrato`)                   |
| `ContratoMaeId`                    | `Guid`          | Id do contrato pai **imediato** (pode ser outro REFINIMP)                   |
| `PercentualRefinanciadoDecimal`    | `decimal`       | Fração (0..1] do principal **do ancestral original** coberto por este refi  |
| `ValorQuitadoNoRefiValor` + `Moeda`| `Money` derivado| Igual ao `ValorPrincipal` do contrato REFINIMP                              |
| `CreatedAt`, `UpdatedAt`           | `Instant`       | Auditoria padrão                                                            |

**Importante:** `PercentualRefinanciado` no `RefinimpDetail` representa a **fração sobre o ancestral original**, conforme calculado em `ProcessarRefinimpAsync` linha 399 (`valorPrincipal.Valor / valorPrincipalAncestral.Valor`). Não é a fração sobre o pai imediato. Esse contrato é preservado na nova spec.

### 3.4. EconomiaNegociacao — campos novos no snapshot

`SnapshotContratoJson` (campo `jsonb` imutável) ganha **três campos opcionais** quando a cotação é REFINIMP:

| Campo no JSON                       | Origem                                              |
| ----------------------------------- | --------------------------------------------------- |
| `RefinimpContratoMaeId`             | `cotacao.ContratoMaeId`                             |
| `RefinimpPercentualRefinanciado`    | `refinimpDetail.PercentualRefinanciado.AsDecimal`   |
| `RefinimpAncestralId`               | `ancestral.Id` (de `GetAncestraNaoRefinimpAsync`)   |

Para cotações não-REFINIMP os campos são ausentes (não-`null` para evitar ruído). Round-trip JSON com snapshots antigos (FINIMP) continua válido — campos novos são aditivos.

### 3.5. Tabela `cotacao` — coluna nova

Migration aditiva **S7_CotacaoContratoMae** (consecutiva a S6_PtaxNullable da Onda 0):

```sql
ALTER TABLE sgcf.cotacao
    ADD COLUMN contrato_mae_id uuid NULL;

ALTER TABLE sgcf.cotacao
    ADD CONSTRAINT fk_cotacao_contrato_mae
        FOREIGN KEY (contrato_mae_id) REFERENCES sgcf.contrato(id)
        ON DELETE RESTRICT;

CREATE INDEX ix_cotacao_contrato_mae_id ON sgcf.cotacao(contrato_mae_id);
```

`ON DELETE RESTRICT`: não permite apagar um contrato que tenha cotação REFINIMP filha — preserva integridade da cadeia para auditoria. EF mapeamento via `CotacaoConfiguration` com `HasOne<Contrato>().WithMany().HasForeignKey(c => c.ContratoMaeId).IsRequired(false).OnDelete(DeleteBehavior.Restrict)` (sem navegação inversa — id puro, AD-10).

---

## 4. Fluxo Funcional

### 4.1. Criar cotação REFINIMP

```
Cliente HTTP                CotacoesController     CriarCotacaoCommandHandler        IContratoRepository
    │                            │                          │                              │
    │ POST /cotacoes (Refinimp)  │                          │                              │
    ├───────────────────────────►│                          │                              │
    │ {modalidade:Refinimp,      │ Validate                 │                              │
    │  contratoMaeId:<uuid>,     │ (FluentValidation)       │                              │
    │  valorAlvo, prazo, ...}    │                          │                              │
    │                            ├─ MediatR.Send ──────────►│                              │
    │                            │                          │ GetByIdAsync(contratoMaeId) │
    │                            │                          ├─────────────────────────────►│
    │                            │                          │ ◄────────────  Contrato/null │
    │                            │                          │                              │
    │                            │  Valida:                  │                              │
    │                            │   - mae não-null          │                              │
    │                            │   - mae.Status ∉          │                              │
    │                            │     {Cancelado, Quitado}  │                              │
    │                            │   - busca PTAX D-1        │                              │
    │                            │   - Cotacao.Criar(...)    │                              │
    │                            │   - codigoInterno seq     │                              │
    │                            │                          │                              │
    │ 201 Created (CotacaoDto)   │ ◄────────────────────────│                              │
    │ ◄──────────────────────────│                          │                              │
```

**Validações específicas REFINIMP no handler:**

1. `contratoMae = await contratoRepo.GetByIdAsync(cmd.ContratoMaeId)` — `KeyNotFoundException` se ausente (HTTP 404).
2. `contratoMae.Status ∉ {Cancelado, Quitado}` — `InvalidOperationException` (HTTP 409) com mensagem específica.
3. `contratoMae.Status` pode ser `Ativo`, `RefinanciadoParcial` (cadeias permitidas), ou (decisão de PO) qualquer status que indique vivo. `RefinanciadoTotal` é rejeitado porque o saldo já foi totalmente refinanciado.
4. PTAX D-1: obrigatória, igual a FINIMP. Onda 0 confirma `ExigeMoedaEstrangeira(Refinimp) = true`.

### 4.2. Registrar proposta

Sem novos campos no payload (MD-5). Handler de `RegistrarPropostaCommand` ganha branch condicional:

```csharp
if (cotacao.Modalidade == ModalidadeContrato.Refinimp)
{
    Contrato contratoMae = await contratoRepo.GetByIdAsync(cotacao.ContratoMaeId!.Value, ct)
        ?? throw new KeyNotFoundException($"Contrato mãe '{cotacao.ContratoMaeId}' não encontrado.");

    if (cmd.MoedaOriginal != contratoMae.Moeda.ToString())
    {
        throw new InvalidOperationException(
            $"Proposta REFINIMP deve ser na mesma moeda do contrato mãe " +
            $"({contratoMae.Moeda}); recebida {cmd.MoedaOriginal}.");
    }
}
```

Outras modalidades não atingem o branch — regressão FINIMP inalterada.

### 4.3. Comparar propostas — pré-cálculo informativo BB 70%

`CompararPropostasQuery` (já existente para FINIMP) ganha, para cotação REFINIMP, **três colunas informativas adicionais por proposta**:

| Coluna                            | Valor                                                                              |
| --------------------------------- | ---------------------------------------------------------------------------------- |
| `ValorPrincipalAncestral`         | `ancestral.ValorPrincipal` (na moeda do ancestral)                                 |
| `PercentualSobreAncestral`        | `proposta.ValorOferecido / valorPrincipalAncestral` (fração)                       |
| `ExcedeRegraBb70Pct`              | `true` se `banco.CodigoCompe == "001"` **e** `PercentualSobreAncestral > 0.70`     |

Informativo, **não bloqueia**. O operador vê o sinal de alerta antes da aceitação e pode ajustar com o banco. Bloqueio definitivo é apenas na conversão (AD-5 do plano).

### 4.4. Aceitar proposta

Sem mudança no comando `AceitarPropostaCommand`. A aceitação de uma proposta REFINIMP segue exatamente o mesmo fluxo de FINIMP (`AceitaPor`, `DataAceitacao`, transição `Comparada → Aceita`). A regra 70% BB **não** é validada aqui — sua aplicação fica na conversão para evitar bloqueio prematuro e duplicação de lógica (AD-5 — plano refinimp/).

### 4.5. Converter em contrato

```
ConverterEmContratoCommandHandler                IConversorModalidade        IContratoRepository
    │                                                      │                          │
    │ Carrega Cotacao + PropostaAceita                     │                          │
    │ Cria Contrato (Contrato.Criar)                       │                          │
    │ contratoRepo.Add(contrato)                           │                          │
    │                                                      │                          │
    │ Despacha por modalidade:                             │                          │
    │   _conversoresMap[Refinimp] → ConversorRefinimp      │                          │
    │ ──────────────────────────────────────────────────► │                          │
    │                                                      │ contratoMae =            │
    │                                                      │   GetByIdAsync(maeId)    │
    │                                                      ├─────────────────────────►│
    │                                                      │ ancestral =              │
    │                                                      │ GetAncestraNaoRefinimp() │
    │                                                      ├─────────────────────────►│
    │                                                      │                          │
    │                                                      │ Valida moeda             │
    │                                                      │ (defesa em profundidade) │
    │                                                      │ Valida regra 70% BB      │
    │                                                      │ percentual =             │
    │                                                      │   valorPrincipal /       │
    │                                                      │   ancestral.ValorPrinc   │
    │                                                      │ Cria RefinimpDetail      │
    │                                                      │ Marca mãe:               │
    │                                                      │   if %>=1.0 → Total      │
    │                                                      │   else → Parcial         │
    │ ◄────────── (RefinimpDetail, null) ─────────────────│                          │
    │                                                      │                          │
    │ repo.AddDetail(refinimpDetail)                       │                          │
    │ CalculadoraCet.CalcularCet → fachada → ...Refinimp   │                          │
    │ EconomiaNegociacao.Criar (snapshot c/ 3 campos novos)│                          │
    │ LimiteBanco.Refinimp.RegistrarUtilizacao             │                          │
    │ cotacao.MarcarConvertida()                           │                          │
    │ SaveChangesAsync (UoW único, atômico)                │                          │
```

**Inputs do command** ganham o tipo `RefinimpInputs` opcional:

```csharp
public sealed record RefinimpInputs(
    decimal PercentualRefinanciado);  // 0..1 (fração)

// ConverterEmContratoCommand record:
public sealed record ConverterEmContratoCommand(
    // ... campos FINIMP existentes ...
    FinimpInputs? Finimp = null,
    RefinimpInputs? Refinimp = null)
    : IRequest<ContratoDto>;
```

**Por que `PercentualRefinanciado` vem do command e não é só derivado?**

`ProcessarRefinimpAsync` hoje (`CreateContratoCommand` linha 399) **calcula** o percentual via `valorPrincipal / valorPrincipalAncestral`. Esta spec preserva esse comportamento — o `PercentualRefinanciado` armazenado em `RefinimpDetail` continua sendo o resultado dessa divisão (fração sobre o ancestral).

Porém, o **operador** precisa, no momento da conversão, declarar a **intenção** de cobertura (ex.: "este refi cobre 50% do mãe imediato"). Esse percentual de intenção valida o valor do contrato e ajusta-o se houver pequena diferença entre o valor da proposta aceita e o efetivamente desembolsado. Casos:

- Se o operador fornece `RefinimpInputs.PercentualRefinanciado = 0.50` mas o `valorPrincipal` derivado da proposta aceita resulta em fração diferente sobre o ancestral, o conversor **prioriza o valor monetário** (`Money` é a fonte da verdade) e armazena `percentualFracao = valorPrincipal / ancestral.ValorPrincipal`. O `PercentualRefinanciado` declarado entra no `audit_log` para divergência.

Decisão preserva a lógica existente em `ProcessarRefinimpAsync` linhas 398–400. O campo no command serve para auditoria de intenção e para futura validação de cobertura (ex.: "se você declarou 100% e o valor não fecha, alerta").

---

## 5. API

### 5.1. `POST /api/v1/cotacoes`

**Payload (REFINIMP):**

```json
{
  "modalidade": "Refinimp",
  "contratoMaeId": "0192a4c3-1234-7000-8000-abcdef012345",
  "valorAlvoBRL": 2700000.00,
  "prazoMaximoDias": 180,
  "dataAbertura": "2026-06-01",
  "observacoes": "Refi 50% do FIN-2025-0007 com proposta BB"
}
```

**Campos:**

| Campo            | Tipo     | Obrigatório                                | Notas                                                                                       |
| ---------------- | -------- | ------------------------------------------ | ------------------------------------------------------------------------------------------- |
| `modalidade`     | string   | sim                                        | `"Refinimp"` (case-insensitive)                                                             |
| `contratoMaeId`  | `Guid?`  | **sim** quando `modalidade=Refinimp`       | FluentValidation: `RuleFor(c => c.ContratoMaeId).NotNull().NotEqual(Guid.Empty).When(...)`  |
| `valorAlvoBRL`   | decimal  | sim                                        | Valor desejado em BRL (cross-rate via PTAX D-1)                                             |
| `prazoMaximoDias`| int      | sim                                        | ≥ 1                                                                                         |
| `dataAbertura`   | date     | sim                                        | PTAX D-1 será o dia útil anterior                                                           |
| `observacoes`    | string?  | não                                        |                                                                                             |

**Validação adicional no handler:** `contratoMae` deve existir e seu `Status` não pode ser `Cancelado` nem `Quitado` nem `RefinanciadoTotal`.

### 5.2. `POST /api/v1/cotacoes/{id}/propostas`

Sem campos novos no payload (MD-5). O handler infere o contexto REFINIMP via `cotacao.Modalidade` e valida `MoedaOriginal == contratoMae.Moeda`.

**Payload (exemplo USD REFINIMP, sem NDF):**

```json
{
  "bancoId": "0192a4c3-aaaa-7000-8000-000000000bb1",
  "moedaOriginal": "Usd",
  "valorOferecidoMoedaOriginal": 500000.00,
  "taxaAaPercentual": 5.20,
  "iofPercentual": 0.38,
  "spreadAaPercentual": 0.50,
  "prazoDias": 180,
  "estruturaAmortizacao": "Bullet",
  "periodicidadeJuros": "Mensal",
  "exigeNdf": false,
  "garantiaExigida": "Aval pessoa-física",
  "valorGarantiaExigidaBRL": 0,
  "garantiaEhCdbCativo": false
}
```

### 5.3. `POST /api/v1/cotacoes/{id}/converter-em-contrato`

**Payload (REFINIMP):**

```json
{
  "numeroExternoContrato": "REFI-BB-2026-0042",
  "dataContratacao": "2026-06-10",
  "dataVencimento": "2026-12-10",
  "baseCalculo": "Dias360",
  "refinimp": {
    "percentualRefinanciado": 0.50
  }
}
```

**Campos específicos:**

| Campo                          | Tipo          | Obrigatório quando             | Notas                                                                              |
| ------------------------------ | ------------- | ------------------------------ | ---------------------------------------------------------------------------------- |
| `refinimp.percentualRefinanciado` | decimal (0..1] | `cotacao.Modalidade=Refinimp`  | Fração declarada de cobertura sobre o mãe **imediato**; auditoria de intenção     |
| `finimp.*`                     | objeto        | `cotacao.Modalidade=Finimp`    | Inalterado (Onda 0)                                                                |

**Defaults para REFINIMP:**

- `bancoId` vem da `propostaAceita.BancoId`.
- `valorPrincipal` é `propostaAceita.ValorOferecidoMoedaOriginal` (não vem do command).
- `moeda` é `propostaAceita.MoedaOriginal` (== moeda do mãe pela validação 5.2).
- `taxaAa` é `propostaAceita.TaxaAaPercentual` (+ `SpreadAaPercentual` se aplicável).

### 5.4. Respostas HTTP

| Status | Quando                                                                                                              |
| ------ | ------------------------------------------------------------------------------------------------------------------- |
| `201`  | Cotação ou conversão criada com sucesso                                                                             |
| `400`  | Payload inválido (FluentValidation): `contratoMaeId` ausente em REFINIMP, `percentualRefinanciado ∉ (0..1]`         |
| `404`  | `contratoMaeId` não encontrado; `ancestral` não localizado                                                          |
| `409`  | Status do mãe inválido (`Cancelado`/`Quitado`/`RefinanciadoTotal`)                                                  |
| `409`  | `Banco.AceitaRefinimp = false`                                                                                      |
| `409`  | Regra 70% BB violada                                                                                                |
| `409`  | Moeda da proposta ≠ moeda do mãe                                                                                    |
| `409`  | `LimiteBanco.Refinimp.ValorDisponivel < ValorAlvoBRL` ao adicionar banco                                            |

**Mensagens canônicas** (espelham `ProcessarRefinimpAsync`):

- `"O banco '{apelido}' não aceita contratos Refinimp."`
- `"Banco do Brasil (001): o valor do REFINIMP ({valor}) excede 70% do principal do contrato ancestral ({limite})."`
- `"A moeda do REFINIMP ({moedaRefi}) deve ser igual à moeda do contrato mãe ({moedaMae})."`
- `"Contrato mãe '{id}' está em status '{status}' e não pode ser refinanciado."`

---

## 6. Conversor (IConversorModalidade)

### 6.1. Implementação `ConversorRefinimp`

**Arquivo:** `src/Sgcf.Application/Cotacoes/Conversores/ConversorRefinimp.cs`

```csharp
public sealed class ConversorRefinimp(IContratoRepository contratoRepo, IBancoRepository bancoRepo) : IConversorModalidade
{
    public ModalidadeContrato Modalidade => ModalidadeContrato.Refinimp;

    private static readonly string CodigoCompeBancoDoBrasil = "001";

    public async Task<(Entity Principal, Entity? Secundario)> CriarDetailAsync(
        ConverterEmContratoContext ctx, CancellationToken ct)
    {
        Guid maeId = ctx.Cotacao.ContratoMaeId
            ?? throw new InvalidOperationException(
                "Cotacao REFINIMP sem ContratoMaeId — invariante violada.");

        Contrato contratoMae = await contratoRepo.GetByIdAsync(maeId, ct)
            ?? throw new KeyNotFoundException($"Contrato mãe '{maeId}' não encontrado.");

        // Defesa em profundidade (regra já validada em RegistrarPropostaCommand)
        if (ctx.ContratoCriado.Moeda != contratoMae.Moeda)
        {
            throw new InvalidOperationException(
                $"A moeda do REFINIMP ({ctx.ContratoCriado.Moeda}) deve ser igual à moeda do contrato mãe ({contratoMae.Moeda}).");
        }

        Contrato ancestral = await contratoRepo.GetAncestraNaoRefinimpAsync(maeId, ct)
            ?? throw new InvalidOperationException(
                $"Não foi possível localizar o ancestral original do contrato mãe '{maeId}'.");

        Money valorPrincipal = ctx.ContratoCriado.ValorPrincipal;
        Money valorPrincipalAncestral = ancestral.ValorPrincipal;

        Banco banco = await bancoRepo.GetByIdAsync(ctx.ContratoCriado.BancoId, ct)
            ?? throw new KeyNotFoundException($"Banco '{ctx.ContratoCriado.BancoId}' não encontrado.");

        if (banco.CodigoCompe == CodigoCompeBancoDoBrasil)
        {
            Money limite70 = valorPrincipalAncestral.Multiplicar(0.70m);
            if (valorPrincipal.MaiorQue(limite70))
            {
                throw new InvalidOperationException(
                    $"Banco do Brasil (001): o valor do REFINIMP ({valorPrincipal}) excede 70% do principal do contrato ancestral ({limite70}).");
            }
        }

        decimal percentualFracao = valorPrincipal.Valor / valorPrincipalAncestral.Valor;
        Percentual percentual = Percentual.DeFracao(percentualFracao);

        RefinimpDetail detail = RefinimpDetail.Criar(
            ctx.ContratoCriado.Id,
            maeId,
            percentual,
            valorPrincipal,
            ctx.Clock);

        if (percentualFracao >= 1.0m)
        {
            contratoMae.MarcarRefinanciadoTotal(ctx.Clock);
        }
        else
        {
            contratoMae.MarcarRefinanciadoParcial(ctx.Clock);
        }

        return (detail, null);  // sem detail secundário (Balcão+FGI é caso à parte)
    }
}
```

**Retorno:** `(RefinimpDetail, null)` — secundário sempre nulo para REFINIMP.

**Side effect aceito:** o conversor **muta o agregado `Contrato` mãe** (chamada `MarcarRefinanciado{Total,Parcial}`). Justificado porque o mãe foi recém-carregado pelo próprio conversor e ficará marcado para `SaveChanges` na mesma UoW do dispatcher (`ConverterEmContratoCommand`). Espelha a lógica existente em `ProcessarRefinimpAsync` linhas 412–420, sem nova invariante.

### 6.2. Registro DI

Em `Sgcf.Application/DependencyInjection.cs` (preparado pela Onda 0 §5.6):

```csharp
services.AddScoped<IConversorModalidade, ConversorRefinimp>();
```

Substitui o stub `ConversorRefinimp` `NotImplementedException` deixado pela Onda 0 §5.4.

---

## 7. CET

### 7.1. Fórmula

REFINIMP usa **a mesma fórmula de CET de FINIMP**. A fachada `CalculadoraCet.CalcularCet` (Onda 0 §4.2) dispatcheia por modalidade; o branch REFINIMP é apenas:

```csharp
internal static decimal CalcularCetRefinimp(
    Proposta p, decimal ptax, LocalDate dt, decimal? taxaAaOverride = null)
    => CalcularCetFinimp(p, ptax, dt, taxaAaOverride);
```

Justificativa (AD-7 plano refinimp/): a estrutura de proposta (taxa + spread + IOF + NDF + garantia CDB + BreakFunding) é idêntica entre FINIMP e REFINIMP. REFINIMP é diferenciação documental/contratual, não fórmula.

### 7.2. Inputs

- `MoedaOriginal` ∈ {USD, EUR, CNY, JPY} (igual ao mãe; nunca BRL).
- `PTAX D-1` obrigatória (Onda 0 §3.2 confirma `ExigeMoedaEstrangeira(Refinimp) = true`).
- Estrutura de amortização, periodicidade, garantia, NDF — idênticos a FINIMP.

### 7.3. Caso golden dedicado

`tests/Sgcf.GoldenDataset/data/cotacoes/refinimp-001-usd-50pct-ndf.json`:

- **Setup:** FINIMP mãe USD 1.000.000 já existente, ativo, banco X.
- **Cotação REFINIMP:** USD com BB, valor alvo 500.000 USD (50% sobre ancestral; respeita regra 70% BB).
- **Proposta vencedora:** taxa 5,20% a.a., spread 0,50%, IOF 0,38%, NDF obrigatório a 1,80% a.a., prazo 180 dias, bullet, garantia "Aval".
- **PTAX D-1 fixada** no JSON para determinismo.
- **CET esperado:** calculado pelo mesmo motor de FINIMP; valor armazenado bit a bit.
- **Conversão:** gera REFINIMP USD 500.000, mãe vai para `RefinanciadoParcial`, `EconomiaNegociacao` registra os 3 campos novos do snapshot.

---

## 8. Edge Cases

### 8.1. Status do contrato mãe ≠ `Ativo`

| Status do mãe na criação da cotação | Comportamento esperado                                                                                                                |
| ----------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------- |
| `Ativo`                             | OK — fluxo normal.                                                                                                                     |
| `RefinanciadoParcial`               | OK — cadeia recursiva permitida; novo REFINIMP cobre saldo remanescente.                                                               |
| `RefinanciadoTotal`                 | **Rejeita 409.** Saldo já totalmente refinanciado — não há o que cobrir.                                                                |
| `Quitado`                           | **Rejeita 409.** Mãe pago e fechado.                                                                                                   |
| `Cancelado`                         | **Rejeita 409.** Mãe nunca foi operacionalizado.                                                                                       |
| Outros (futuros)                    | Rejeita 409 com mensagem "status não suportado para refinanciamento". Default conservador.                                             |

**Defesa em profundidade:** mesma validação re-executada em `ConverterEmContratoCommand` (status pode ter mudado entre criação e conversão, ex.: outro REFINIMP marcou o mãe como `RefinanciadoTotal`).

### 8.2. Cadeia recursiva REFINIMP → REFINIMP → REFINIMP → FINIMP

**Garantido por:** `IContratoRepository.GetAncestraNaoRefinimpAsync` (`IContratoRepository.cs:22`) já percorre a cadeia até encontrar o FINIMP raiz. Comportamento existente, sem mudança.

**Profundidade máxima:** não imposta nesta SPEC. Em prática, cadeias > 3 níveis são raras. Caso a profundidade vire problema operacional, introduzir limite é evolução (`tasks/cotacoes-modalidades/refinimp/plan.md` §6 Q5).

**Teste obrigatório:** integration test com cadeia de 3 níveis valida que `GetAncestraNaoRefinimpAsync` retorna o FINIMP original e a regra 70% BB usa o principal correto.

### 8.3. Banco do REFINIMP ≠ banco do FINIMP mãe

**Permitido sem restrição adicional.** A empresa pode refinanciar um FINIMP do banco X tomando crédito no banco Y. Validações aplicáveis:

- `Banco.AceitaRefinimp` do banco Y deve ser `true` (sempre).
- Se Y for BB, regra 70% sobre principal do ancestral.
- `LimiteBanco.Refinimp` do banco Y deve cobrir o `ValorAlvoBRL`.

Não há requisito de "mesmo banco da cadeia". Caso real frequente: cliente migra dívida de banco caro para banco barato.

### 8.4. `PercentualRefinanciado = 100%` vs parcial

| `percentualFracao = valorPrincipal / ancestral.ValorPrincipal` | Marcação do mãe imediato                             |
| -------------------------------------------------------------- | ---------------------------------------------------- |
| `< 1.0`                                                        | `contratoMae.MarcarRefinanciadoParcial(clock)`       |
| `>= 1.0`                                                       | `contratoMae.MarcarRefinanciadoTotal(clock)`         |

**Atenção:** a divisão é contra `ancestral.ValorPrincipal`, **não** contra `contratoMae.ValorPrincipal` quando o mãe é ele próprio um REFINIMP. Implicação: em cadeias, um único REFINIMP de USD 500.000 sobre um mãe REFINIMP de USD 500.000 que descende de um ancestral FINIMP USD 1.000.000 dá `percentualFracao = 0.50` (sobre o ancestral), e o mãe **imediato** (REFINIMP intermediário) é marcado `RefinanciadoTotal` (porque cobre 100% **do mãe imediato**, mesmo sendo 50% do ancestral).

> **⚠️ Inconsistência preservada do código existente** (`ProcessarRefinimpAsync` linha 413):
> A condição `if (percentualFracao >= 1.0m)` usa a fração sobre o **ancestral**, mas a marcação se aplica ao **mãe imediato**. Em cadeias, isso pode marcar o mãe imediato como `RefinanciadoTotal` apenas quando o REFINIMP cobre 100% do ancestral, mesmo que cubra muito mais que 100% do mãe imediato (impossível pela validação 70% BB, mas teoricamente).
>
> Esta SPEC preserva o comportamento atual sem corrigir, porque (a) corrigir seria mudar contrato em código já em produção e (b) o caso prático onde cadeia > 1 nível ocorre é raríssimo. Levantado como questão aberta em `tasks/cotacoes-modalidades/refinimp/plan.md` §6 Q5 / Q6 para discussão futura com PO.

### 8.5. Moeda do REFINIMP ≠ moeda do contrato mãe

**Proibido.** Validação tripla:

1. `RegistrarPropostaCommand` — fail-fast quando registra proposta com moeda diferente.
2. `ConversorRefinimp` — defesa em profundidade.
3. Documentação da API (`docs/api/cotacoes.md`) — alerta explícito.

Cross-currency (refi BRL de FINIMP USD para liquidar exposição cambial) é caso de uso real mencionado por tesouraria, mas **fora deste escopo** (questão aberta — `tasks/cotacoes-modalidades/refinimp/plan.md` §6 Q4).

### 8.6. `LimiteBanco.Refinimp` ausente ou insuficiente

| Cenário                                                             | Comportamento                                                                                                          |
| ------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------- |
| Banco sem `LimiteBanco` para `Modalidade=Refinimp`                  | `AdicionarBancoNaCotacaoCommand` rejeita 409: "Banco '{apelido}' não possui limite cadastrado para modalidade Refinimp." |
| `LimiteDisponivelBRL < ValorAlvoBRL`                                | Rejeita 409 (regra MVP existente para FINIMP, reaplicada).                                                             |
| Banco com limite Finimp suficiente mas Refinimp ausente             | Rejeita — não há fallback automático entre modalidades (AD-3 plano refinimp/).                                          |

### 8.7. Múltiplos REFINIMP simultâneos do mesmo FINIMP

**Permitido.** Vários REFINIMP-filhos do mesmo mãe podem coexistir, somando ou não > 100% (cada um valida regra 70% BB isoladamente). Estado do mãe é atualizado pelo último que converte (idempotente — `MarcarRefinanciadoParcial` é não-destrutivo).

**Exemplo:**

```
FINIMP mãe USD 1.000.000  (banco X)
    ├── REFINIMP USD 400.000 (BB, 40% do ancestral, válido)
    ├── REFINIMP USD 500.000 (Itaú, 50% do ancestral, válido)
    └── REFINIMP USD 700.000 (Bradesco, 70% do ancestral, válido)
                        ────────────────────────────────────────
                        Soma = 1.600.000 USD (160% do mãe!)
```

Soma > 100% não é bloqueada hoje porque cada operação é independente em termos contratuais (cada uma toma dinheiro novo e quita parte do mãe). A regra de "soma cumulativa contra ancestral original" não está codificada — questão aberta em `tasks/cotacoes-modalidades/refinimp/plan.md` §6 Q5.

A SPEC **não introduz** essa regra; preserva o comportamento atual de `ProcessarRefinimpAsync`.

### 8.8. Contrato mãe muda de status entre criação da cotação e conversão

Cenário: operador cria cotação REFINIMP em D-5; outro REFINIMP do mesmo mãe é convertido em D-2 e marca o mãe como `RefinanciadoTotal`; operador tenta converter sua cotação em D-0.

**Comportamento:** `ConversorRefinimp` revalida `contratoMae.Status` antes de criar o detail. Rejeita 409 com mensagem clara, e a cotação permanece em `Aceita` (operador pode cancelá-la ou criar nova cotação contra outro mãe).

### 8.9. Concorrência: dois operadores convertem simultaneamente o mesmo mãe

UoW + transação Postgres garantem atomicidade. O segundo conversor a `SaveChanges` pode encontrar:

- `contratoMae` com status alterado (re-leitura via tracking ou conflict de version) — rejeita por status inválido.
- FK constraint não trava (mãe ainda existe).

Trata como qualquer outra contenção: o segundo operador recebe 409 com mensagem "Status do contrato mãe foi alterado por outra operação; recarregue a cotação."

---

## 9. Testes

### 9.1. Domain

**Arquivo:** `tests/Sgcf.Domain.Tests/Cotacoes/CotacaoRefinimpTests.cs`

- `Criar_Refinimp_sem_ContratoMaeId_lanca_excecao`
- `Criar_Refinimp_com_ContratoMaeId_Empty_lanca_excecao`
- `Criar_Refinimp_com_ContratoMaeId_valido_sucesso`
- `Criar_Finimp_com_ContratoMaeId_lanca_excecao` (defesa)
- `Criar_Refinimp_sem_PTAX_lanca_excecao` (herda invariante Onda 0)

### 9.2. Application (handlers)

**Arquivo:** `tests/Sgcf.Application.Tests/Cotacoes/CriarCotacaoRefinimpTests.cs`

- `Handle_Refinimp_mae_inexistente_lanca_KeyNotFound`
- `Handle_Refinimp_mae_Cancelado_lanca_InvalidOperation_409`
- `Handle_Refinimp_mae_Quitado_lanca_InvalidOperation_409`
- `Handle_Refinimp_mae_RefinanciadoTotal_lanca_InvalidOperation_409`
- `Handle_Refinimp_mae_Ativo_sucesso`
- `Handle_Refinimp_mae_RefinanciadoParcial_sucesso` (cadeia recursiva)

**Arquivo:** `tests/Sgcf.Application.Tests/Cotacoes/AdicionarBancoRefinimpTests.cs`

- `Handle_Refinimp_banco_sem_AceitaRefinimp_rejeita_409`
- `Handle_Refinimp_banco_sem_LimiteRefinimp_rejeita_409`
- `Handle_Refinimp_banco_AceitaRefinimp_true_sucesso`

**Arquivo:** `tests/Sgcf.Application.Tests/Cotacoes/RegistrarPropostaRefinimpTests.cs`

- `Handle_Refinimp_moeda_USD_mae_USD_sucesso`
- `Handle_Refinimp_moeda_EUR_mae_USD_lanca_InvalidOperation_409`
- `Handle_Finimp_nao_executa_branch_Refinimp` (regressão)

**Arquivo:** `tests/Sgcf.Application.Tests/Cotacoes/Conversores/ConversorRefinimpTests.cs`

- `CriarDetail_BB_valor_igual_70pct_ancestral_sucesso`
- `CriarDetail_BB_valor_acima_70pct_ancestral_rejeita_InvalidOperation`
- `CriarDetail_BancoNaoBB_acima_70pct_sucesso`
- `CriarDetail_percentual_100pct_marca_mae_RefinanciadoTotal`
- `CriarDetail_percentual_50pct_marca_mae_RefinanciadoParcial`
- `CriarDetail_cadeia_3_niveis_usa_ancestral_correto`
- `CriarDetail_moeda_divergente_lanca_InvalidOperation` (defesa)
- `CriarDetail_retorno_Secundario_eh_null`

### 9.3. Integration (E2E HTTP)

**Arquivo:** `tests/Sgcf.Api.IntegrationTests/Cotacoes/ConverterRefinimpTests.cs`

Setup compartilhado: cria FINIMP mãe USD 1.000.000 ativo via API.

- `POST_cotacao_Refinimp_USD_50pct_BB_converte_201` — fluxo completo: criar → adicionar BB → registrar proposta USD 500K → aceitar → converter → mãe `RefinanciadoParcial`.
- `POST_converter_Refinimp_100pct_marca_mae_RefinanciadoTotal`
- `POST_converter_Refinimp_BB_75pct_ancestral_rejeita_409`
- `POST_converter_Refinimp_moeda_divergente_rejeita_409`
- `POST_cotacao_Refinimp_sem_contratoMaeId_rejeita_400`
- `POST_cotacao_Refinimp_mae_Quitado_rejeita_409`
- `GET_comparativo_Refinimp_inclui_ExcedeRegraBb70Pct` (informativo)
- `Audit_log_Refinimp_inclui_contrato_mae_id_no_payload`
- `EconomiaNegociacao_snapshot_inclui_RefinimpContratoMaeId`

**Regressão obrigatória:** suite FINIMP existente passa sem ajuste.

### 9.4. Golden Dataset

**Arquivo:** `tests/Sgcf.GoldenDataset/data/cotacoes/refinimp-001-usd-50pct-ndf.json`

Cenário canônico descrito em §7.3. Aprovação humana do CET esperado é parte do checkpoint E do plano refinimp/.

---

## 10. Boundaries — Always / Ask First / Never

### 10.1. Always

- Validar `Banco.AceitaRefinimp` fail-fast em `AdicionarBancoNaCotacaoCommand` (AD-4 plano refinimp/).
- Validar moeda da proposta == moeda do mãe em `RegistrarPropostaCommand` (AD-8).
- Aplicar regra 70% BB na conversão sobre `ancestral.ValorPrincipal` (AD-5 + espelho `ProcessarRefinimpAsync`).
- Marcar `contratoMae` como `RefinanciadoParcial` ou `RefinanciadoTotal` na mesma UoW da conversão.
- Reutilizar `CalculadoraCet.CalcularCetFinimp` para REFINIMP (AD-7).
- Reutilizar `IContratoRepository.GetAncestraNaoRefinimpAsync` — não duplicar walk em código novo.
- Persistir `RefinimpDetail.PercentualRefinanciado` como fração sobre o **ancestral original** (preservar contrato de `ProcessarRefinimpAsync` linha 399).

### 10.2. Ask First

- Imposição de prazo máximo da proposta = prazo restante do mãe (questão aberta Q1 plano refinimp/).
- Suporte cross-currency (refi BRL de mãe USD — Q4).
- Soma cumulativa de múltiplos REFINIMP contra ancestral original (Q5).
- Reconciliação automática de `LimiteBanco.Finimp` quando mãe vira `RefinanciadoTotal` (Q3).
- Profundidade máxima da cadeia REFINIMP (Q5).
- Corrigir a inconsistência da marcação do mãe imediato em cadeias (§8.4 — Q6).

### 10.3. Never

- Alterar `RefinimpDetail` (já é entidade estável do domínio de Contratos; mudanças exigiriam migration e revisão de `ProcessarRefinimpAsync`).
- Adicionar campos REFINIMP em `Proposta` (MD-5).
- Calcular CET REFINIMP com fórmula diferente de FINIMP sem assinatura do PO + atualização do Golden Dataset.
- Pular validação `Banco.AceitaRefinimp` "porque o banco aparece no autocomplete" (UI não substitui regra de domínio).
- Permitir REFINIMP de mãe `Cancelado`, `Quitado` ou `RefinanciadoTotal`.
- Pular `IContratoRepository.GetAncestraNaoRefinimpAsync` e usar `ContratoMaeId` diretamente para regra 70% BB.

---

## 11. Documentação a atualizar

| Arquivo                                                       | Mudança                                                                                                                              |
| ------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------ |
| `docs/specs/cotacoes/SPEC.md`                                 | §1 modalidades MVP → "FINIMP + REFINIMP"; remover REFINIMP de §11 (boundaries); adicionar nova §14 sumarizando esta SPEC modal-específica |
| `docs/api/cotacoes.md`                                        | Payload `POST /cotacoes` com `contratoMaeId`; payload `POST /converter-em-contrato` com `refinimp.percentualRefinanciado`; tabela de erros 409 |
| `docs/api/schemas.md`                                         | Novos schemas: `RefinimpInputs`, `ComparativoPropostaRefinimpInfo`, campos novos do `EconomiaNegociacao.SnapshotContratoJson`         |
| `docs/api/collections/sgcf-api/Cotacoes-Refinimp/`            | Bruno: sequência completa (setup mãe → criar → adicionar BB → registrar → aceitar → converter); caso de erro `AceitaRefinimp=false`  |
| `docs/changelog/CHANGELOG.md` v0.7.0                          | Bloco `ADDITIVE — Cotações — Modalidade REFINIMP`; bloco `INTERNAL — Migration S7_CotacaoContratoMae`; nota de compatibilidade        |

---

## 12. Plano de Implementação

Referência: `tasks/cotacoes-modalidades/refinimp/plan.md` (já aprovado em estrutura; aguarda execução).

**Caminho crítico (6 tasks):**

```
1.1 Cotacao.ContratoMaeId
 │
 ▼
2.1 Migration S7_CotacaoContratoMae
 │
 ▼
2.2 EF Configuration
 │
 ▼
3.1 CriarCotacaoCommand (contratoMaeId + valida status mãe)
 │
 ├──► 3.2 AdicionarBancoNaCotacaoCommand (valida AceitaRefinimp) [paralelo]
 ├──► 3.3 RegistrarPropostaCommand (valida moeda mãe) [paralelo]
 │
 ▼
4.1 ConverterEmContratoCommand + ConversorRefinimp
 │
 ▼
5.3 Golden Dataset refinimp-001-usd-50pct-ndf.json
```

**Paralelizável após 3.1:** 3.2 e 3.3 (handlers independentes).
**Paralelizável após Checkpoint D:** Fase 5 (Bruno + Golden) e Fase 6 (docs).

**Checkpoints:** A (Domínio), B (Persistência), C (Captação REFINIMP), D (Conversão), E (Bruno + Golden), Final.

**Escopo total:** 13 tasks (4 M, 6 S, 3 XS).

**Pré-requisito Onda 0:** consumir `IConversorModalidade` da Onda 0 quando disponível. Se REFINIMP precisar começar antes da Onda 0 finalizar, implementação inicial usa `if (cotacao.Modalidade == Refinimp)` no `ConverterEmContratoCommand` análogo ao FINIMP — refatoração para Strategy ocorre na integração com a Onda 0 (esforço pequeno: extrair o branch para `ConversorRefinimp.CriarDetailAsync`).

---

## 13. Histórico

| Data       | Versão | Mudança                                                          |
| ---------- | ------ | ---------------------------------------------------------------- |
| 2026-05-18 | v1.0   | Draft inicial — consolidação do plano refinimp/ em SPEC formal   |
