# SGCF API — Changelog

**Formato:** Semver + ISO 8601. Seções por versão, ordem decrescente.
**Destinatários:** Sistemas de IA (agentes MCP/A2A/LLM), integrações máquina-a-máquina, CI/CD.

> **Convenções de impacto:**
> - `BREAKING` — quebra compatibilidade com clientes existentes; migração obrigatória.
> - `ADDITIVE` — novo endpoint ou campo opcional; clientes existentes não quebram.
> - `FIX` — correção de comportamento incorreto; pode alterar resposta sem quebrar contrato.
> - `INTERNAL` — mudança interna sem impacto na interface pública.

---

## [0.9.0] — 2026-05-18

### Resumo executivo

Entrega conjunta da **Onda 3 — modalidades Capital de Giro e FGI** no módulo de Cotações. Duas modalidades BRL independentes mergeadas no mesmo release:

- **Onda 3b — Capital de Giro:** linha BRL universal para fluxo de caixa empresarial (qualquer banco comercial). Inclui remoção de `TipoProduto` e `TemFgi` de `CapitalDeGiroDetail` (migration S8 — BREAKING), `CalcularCetCapitalDeGiro` como função pura, `ConversorCapitalDeGiro` real, guards (USD → 400, NDF=true → 400), e golden dataset validado (CET 15,904828% com IOF 0,38%).
- **Onda 3a — FGI:** linha BNDES via banco repassador (Caixa, BV, BB, Sicoob). Inclui guards específicos (BRL obrigatório, sem NDF, apenas Bullet no MVP), `CalcularCetFgi` com fluxo manual de tarifa anual, `ConversorFgi` real, campos `numeroOperacaoFgi` e `fgi` em `POST /converter-em-contrato`, e golden dataset (CET 12,912788%).

> **Distinção de domínio (correção 2026-05-18):** FGI-modalidade é a linha BNDES direta. FGI como garantia em outras modalidades (NCE, Capital de Giro) já estava implementado em v0.6.0 via `GarantiaExigidaLimite.Tipo = Fgi`. Não há flag `TemFgi`; o tipo de garantia é declarado no limite, não na proposta.

---

### BREAKING — Cotações — CapitalDeGiroDetail (migration S8)

**Colunas removidas da tabela `balcao_caixa_detail` (schema `sgcf`):**

- `tipo_produto` (`text`, nullable) — removida; Capital de Giro não tem tipologia de produto no MVP.
- `tem_fgi` (`boolean`, not null, default false) — removida; FGI é modalidade independente (`ModalidadeContrato.Fgi`), não uma flag em Capital de Giro.

Clientes que liam esses campos receberão erro de desserialização após a migration. Atualizar parsers antes de aplicar `S8_DropTipoProdutoTemFgi`.

**Migration aplicada:** `20260518170758_S8_DropTipoProdutoTemFgi`.

---

### ADDITIVE — Cotações — Modalidade Capital de Giro

**Validação nova em `POST /api/v1/cotacoes/{id}/propostas`:**

- Para cotações `CapitalDeGiro`, `moedaOriginal` deve ser `Brl`. Proposta em moeda estrangeira retorna `400 Bad Request`.
- Para cotações `CapitalDeGiro`, `exigeNdf` deve ser `false`. Proposta com NDF retorna `400 Bad Request`.
- `custoNdfAaPercentual` não nulo em cotação `CapitalDeGiro` retorna `400 Bad Request`.

**Campo novo (opcional) em `POST /api/v1/cotacoes/{id}/converter-em-contrato`:**

- `capitalDeGiro` (objeto, opcional quando `modalidade = "CapitalDeGiro"`). Quando ausente, `NumeroOperacao` fica `null`.

Estrutura do objeto `capitalDeGiro`:

```json
{
  "numeroOperacao": "OP-2026-CDG-001"
}
```

**Campo novo em `ContratoDto` (resposta de `POST converter-em-contrato` e `GET /api/v1/contratos/{id}`):**

```json
{
  "capitalDeGiroDetail": {
    "numeroOperacao": "OP-2026-CDG-001"
  }
}
```

`numeroOperacao` é `null` quando não informado na conversão (SPEC EC-10).

**Fórmula CET (SPEC §7):**

Capital de Giro usa a mesma TIR Newton-Raphson base 360 do NCE. O CET é sempre maior que a taxa nominal quando IOF > 0, pois o IOF incide sobre o principal em t=0.

```
NetoRecebido = ValorPrincipal - IOF
IOF          = round(ValorPrincipal × iofPct/100, 2, AwayFromZero)
FluxoT180    = ValorPrincipal × (1 + TaxaAa/100 × Prazo/360)
CET          = TIR([NetoRecebido, -FluxoT180], base=360) — Newton-Raphson
```

**Golden case Capital de Giro validado (SPEC §7 e Cenário 8):**

| Parâmetro | Valor |
|---|---|
| Moeda | BRL |
| Valor principal | R$ 1.500.000,00 |
| Taxa | 14,5% a.a. |
| IOF | 0,38% |
| Prazo | 180 dias (Bullet) |
| CET esperado | **15,904828% a.a.** (±0,01 p.p.) |
| IOF t=0 | R$ 5.700,00 |
| Juros Bullet | R$ 108.750,00 |
| Pagamento t180 | R$ 1.608.750,00 |

**Migration Capital de Giro:** `S8_DropTipoProdutoTemFgi` — remove `tipo_produto` e `tem_fgi` de `balcao_caixa_detail`.

**Cobertura de testes Capital de Giro:**

- 7 testes de domínio: `CalcularCetCapitalDeGiro` (golden BRL 180d, IOF zero ≈ nominal, IOF eleva CET, fachada null, override, rejeita USD, rejeita NDF).
- 5 testes de domínio: `CapitalDeGiroDetail.Criar` (válido, NumeroOperacao null, contratoId empty, sem TipoProduto, sem TemFgi).
- 6 testes de aplicação: `ConversorCapitalDeGiro` (Modalidade, retorna (CapitalDeGiroDetail, null), NumeroOperacao propagado, null sem inputs, ContratoId correto, timestamps preenchidos).
- 3 testes de aplicação: `RegistrarPropostaCommand` guard (rejeita USD, rejeita NDF, aceita BRL válido).
- 1 golden dataset (Cenário 8): CET 15,904828%.
- 4 testes E2E (Slow): fluxo completo, proposta USD → 400, proposta NDF → 400, converter sem capitalDeGiroInputs → NumeroOperacao null.

---

### ADDITIVE — Cotações — Modalidade FGI

**Validações novas em `POST /api/v1/cotacoes/{id}/propostas` (quando `modalidade = "Fgi"`):**

- `moedaOriginal` deve ser `Brl` — FGI é operação doméstica BNDES sem conversão cambial. Violação retorna `400 Bad Request`.
- `exigeNdf` deve ser `false` — operação em BRL sem exposição cambial. Violação retorna `400 Bad Request`.
- `estruturaAmortizacao` deve ser `Bullet` — Price/SAC com FGI exigem cronograma intermediário (out of scope MVP §1.1). Violação retorna `400 Bad Request`.

**Campos novos em `POST /api/v1/cotacoes/{id}/converter-em-contrato` (quando `modalidade = "Fgi"`):**

- `numeroOperacaoFgi` (string?, opcional) — número da operação no sistema BNDES.
- `fgi` (objeto, **obrigatório** quando `modalidade = "Fgi"`) — ausência retorna `400 Bad Request`.

Estrutura do objeto `fgi`:

```json
{
  "taxaFgiAaPercentual": 0.5,
  "percentualCoberto": 80.0
}
```

| Campo | Tipo | Obrigatório | Descrição |
|---|---|---|---|
| `taxaFgiAaPercentual` | decimal | Sim | Taxa anual da tarifa FGI em % (ex.: `0.5` = 0,5% a.a.). Deve ser > 0. |
| `percentualCoberto` | decimal? | Não | Cobertura BNDES via FGI em % (ex.: `80.0`). Informativo — **não entra no CET** (MD-3, SPEC §7.2). |

**Fórmula CET FGI (SPEC §7.3):**

```
IOF         = Principal × IofPct/100                          (em t=0, reduz desembolso líquido)
Juros       = Principal × TaxaAa/100 × PrazoDias/360          (em t=vencimento)
TarifaFgi   = Principal × TaxaFgiAa/100 × PrazoDias/360       (em t=vencimento, AwayFromZero 2 casas)
```

Fluxo de caixa para TIR:

| t | Descrição | Sinal |
|---|---|---|
| 0 | Desembolso líquido `Principal - IOF` | − |
| PrazoDias | `Principal + Juros + TarifaFgi` | + |

TIR resolvida por Newton-Raphson (máx. 100 iterações, tolerância 1e-12), anualizada por `(1 + r_dia)^360 - 1`, base 360 dias.

**Golden case FGI validado pela equipe financeira (2026-05-18):**

| Parâmetro | Valor |
|---|---|
| Moeda | BRL |
| Principal | R$ 500.000,00 |
| Prazo | 365 dias |
| Taxa nominal | 12,0% a.a. |
| IOF | 0,38% (R$ 1.900,00 em t=0) |
| TaxaFgi | 0,5% a.a. (R$ 2.534,72 em t=365) |
| PercentualCoberto | 80% (informativo — não altera CET) |
| CET esperado | **12,912788% a.a.** (±0,01 p.p.) |

**Invariante FGI:** `PercentualCoberto` não altera o CET — alterar de 80% para qualquer valor produz CET idêntico (SPEC §7.2).

**Migration FGI:** nenhuma. `FgiDetail` e `fgi_detail` já existem desde a migração Onda 0 (`S6_FgiDetail`).

**Cobertura de testes FGI:**

- 9 testes unitários: `CalcularCetFgi` (golden ≈12,9%; invariante PercentualCoberto; TaxaFgi=0 → exceção; tarifa positiva; 180d proporcional; Price → NotSupportedException; BRL direto; taxaAaOverride; proporcionalidade).
- 12 testes de aplicação: `ConversorFgi` (modalidade=Fgi; FgiDetail populado; Fgi=null → IOE; TaxaFgi=0 → IOE; PercentualCoberto>100 → IOE; PercentualCoberto=0 → IOE; PercentualCoberto=null → válido; Secundario=null; NumeroOperacaoFgi=null → válido; timestamps).
- 3 testes de aplicação: `RegistrarPropostaCommand` guards FGI (BRL guard; NDF guard; Bullet guard).
- 1 golden dataset (Cenário 9): CET + invariante PercentualCoberto.

---

## [0.8.0] — 2026-05-18

### Resumo executivo

Entrega da **Onda 4 — modalidade Lei 4131/62** no módulo de Cotações. Uma empresa pode agora registrar cotações de empréstimo direto do exterior (Lei 4.131/62), comparar propostas com estimativa de IRRF por alíquota de bitributação, e converter em contrato com detalhamento de SBLC, market flex e break funding fee. Inclui: guard de moeda estrangeira em propostas Lei4131, `CalculadoraIrrfEstimado` como função pura, `irrfEstimadoBrl` informativo no comparativo (sem entrar no CET — decisão MD-3), `ConversorLei4131` real (substitui stub Onda 0), `CalcularCetLei4131` delegando a FINIMP com guard BRL, e golden dataset validado pela tesouraria (CET 6.504008%).

---

### ADDITIVE — Cotações — Modalidade Lei 4131/62

**Campo novo em `GET /api/v1/cotacoes/{id}/comparativo`:**

- `aliquotaIrrfPercentual` (decimal?, query string) — alíquota IRRF para estimar retenção na fonte. Valores comuns: `15` (regra geral), `12.5` (acordo Japão), `25` (jurisdição favorecida). Quando ausente, `irrfEstimadoBrl = 0`.

**Campo novo em `ComparativoDto` (resposta do comparativo):**

```json
{
  "irrfEstimadoBrl": 488699.25
}
```

Valor **informativo** — não compõe o CET (decisão PO 2026-05-18, MD-3/AD-3). Calculado on-demand; não persistido na `Proposta`.

**Validação nova em `POST /api/v1/cotacoes/{id}/propostas`:**

- Para cotações `Lei4131`, `moedaOriginal` deve ser diferente de `Brl`. Proposta em BRL retorna `400 Bad Request` com mensagem descritiva.

**Campo novo em `POST /api/v1/cotacoes/{id}/converter-em-contrato`:**

- `lei4131` (objeto, obrigatório quando `modalidade = "Lei4131"`). Ausência retorna `400 Bad Request`.

Estrutura do objeto `lei4131`:

```json
{
  "garantiaTipo": "SBLC",
  "garantiaPercentual": 100.0,
  "valorGarantiaContratadaBrl": 25061500.00,
  "garantiaEhCdbCativo": false
}
```

Campos de `Lei4131Detail` persistidos no contrato:

| Campo | Tipo | Notas |
|---|---|---|
| `SblcNumero` | `string?` | Identificador externo da SBLC |
| `SblcBancoEmissor` | `string?` | Razão social do banco emissor |
| `SblcValorUsd` | `decimal?` | Valor de face em USD (informativo) |
| `TemMarketFlex` | `bool` | Flag de cláusula market flex |
| `BreakFundingFeePercentual` | `decimal?` | Fee de liquidação antecipada (fração, ex: 0.015 = 1.5%) |

Campos capturados no command mas **não persistidos** (MD-5):

- `paisCredor` — país do credor (ISO 3166-1 alpha-3). Informativo; sem FK.
- `aliquotaIrrfPercentual` — alíquota IRRF usada na estimativa; sem coluna em `lei4131_detail`.

**Fórmula IRRF (SPEC §8.1):**

```
JurosProjetadosMoedaOriginal = ValorOferecidoMoedaOriginal × (TaxaAa + Spread)/100 × Prazo/360
JurosProjetadosBrl           = JurosProjetadosMoedaOriginal × PtaxUsadaUsdBrl
IrrfEstimadoBrl              = round(JurosProjetadosBrl × Alíquota/100, 2, AwayFromZero)
```

**Golden case validado (SPEC §7.4):**

| Parâmetro | Valor |
|---|---|
| Moeda | USD |
| Valor ofertado | 5.000.000,00 |
| Taxa + Spread | 6,0% + 0,5% = 6,5% a.a. |
| Prazo | 720 dias |
| PTAX | 5,0123 |
| CET esperado | **6,504008% a.a.** (±0,01 p.p.) |
| IRRF 15% | **R$ 488.699,25** |
| IRRF 12,5% | **R$ 407.249,38** |
| IRRF 25% | **R$ 814.498,75** |

**Migration:** nenhuma. Todos os campos persistidos já existem em `lei4131_detail` (migrado na Onda 0).

**Bruno collection:** 3 novos requests (19, 20, 21) em `docs/api/collections/sgcf-api/10-Cotacoes/`.

**Cobertura de testes:**

- 8 testes unitários: `CalculadoraIrrfEstimado` (null, zero, negativo → 0; golden cases 15%/12.5%/25%; prazo proporcional; arredondamento AwayFromZero).
- 6 testes de aplicação: `ConversorLei4131` (SBLC completo, clean, ContratoId, campos não persistidos, inputs nulos → InvalidOperationException).
- 2 testes de aplicação: `RegistrarPropostaCommand` guard BRL (Lei4131 BRL → ArgumentException; Lei4131 USD → sucesso).
- 2 testes de domínio: `CalcularCetLei4131` (retorna CET idêntico ao FINIMP; BRL → ArgumentException).
- 1 golden dataset (Cenário 7): CET + IRRF 3 alíquotas.
- 3 testes E2E (Slow): fluxo completo, BRL guard, lei4131Detail ausente.

---

## [0.7.0] — 2026-05-18

### Resumo executivo

Entrega da **Onda 1 — modalidade REFINIMP** no módulo de Cotações. Uma empresa pode agora registrar cotações de refinanciamento de FINIMP existente, comparar propostas e converter em contrato via fluxo estruturado, com rastreabilidade completa da cadeia mãe → filho. Inclui: campo `contratoMaeId` na cotação, validação de status do mãe, regra 70% Banco do Brasil, navegação até o ancestral FINIMP raiz, marcação automática do mãe como `RefinanciadoParcial` ou `RefinanciadoTotal`, e retorno de `RefinimpDetail` no `ContratoDto`.

---

### ADDITIVE — Cotações — Modalidade REFINIMP

**Campo novo em `POST /api/v1/cotacoes`:**

- `contratoMaeId` (Guid?, obrigatório quando `modalidade = "Refinimp"`, proibido nas demais).

**Validações de negócio aplicadas na criação:**

- `contratoMaeId` deve referenciar um contrato existente no status `Ativo`, `RefinanciadoParcial`, `Inadimplente` ou `Vencido`. Status `Cancelado`, `Liquidado` e `RefinanciadoTotal` são rejeitados com `400 Bad Request`.

**Validação de proposta (`POST /api/v1/cotacoes/{id}/propostas`):**

- Para cotações REFINIMP, a `moedaOriginal` da proposta deve coincidir com a moeda do contrato mãe. Divergência retorna `400 Bad Request`.

**Conversão (`POST /api/v1/cotacoes/{id}/converter-em-contrato`):**

- Campo novo opcional: `refinimp.percentualRefinanciado` (decimal) — auditoria de intenção; o valor armazenado no `RefinimpDetail` é sempre calculado como `valorPrincipal / ancestral.ValorPrincipal`.
- **Regra 70% Banco do Brasil:** para banco com `codigoCompe = "001"`, o `valorPrincipal` do REFINIMP não pode exceder 70% do `valorPrincipal` do ancestral FINIMP raiz. Violação retorna `500 Internal Server Error` com mensagem descritiva (mapeamento para 422 previsto na Onda 2).
- O ancestral é determinado percorrendo a cadeia REFINIMP até o contrato de modalidade não-REFINIMP mais antigo.

**Campo novo em `RefinimpDetail` (resposta de `ContratoDto`):**

```json
{
  "refinimpDetail": {
    "contratoMaeId": "uuid",
    "percentualRefinanciado": 0.75,
    "valorQuitadoNoRefi": 150000.00,
    "moeda": "Usd"
  }
}
```

**Migration aplicada:** `S7_CotacaoContratoMaeId`

- Adiciona coluna `contrato_mae_id uuid NULL` na tabela `cotacao`.
- FK para `contrato.id` com `ON DELETE RESTRICT`.
- Índice parcial `ix_cotacao_contrato_mae_id` filtrado por `IS NOT NULL`.

**Cobertura de testes:**

- 7 testes unitários: invariantes de domínio `Cotacao.ContratoMaeId`.
- 6 testes de aplicação: `CriarCotacaoCommand` com mãe em diferentes status.
- 3 testes de aplicação: `RegistrarPropostaCommand` validação de moeda REFINIMP.
- 8 testes unitários: `ConversorRefinimp` (regra 70% BB, cadeia 3 níveis, marcação de status).
- 3 testes E2E: fluxo completo, 70% BB, mãe RefinanciadoTotal.
- 1 golden dataset: CET e economia REFINIMP USD banco não-BB.

---

## [0.6.0] — 2026-05-16

### Resumo executivo

Extensão do módulo Limites de Banco com duas novas capacidades analíticas e operacionais: **Garantias Exigidas** (modelo de colateral por tipo, com validações XOR e unicidade por tipo) e **Histórico de Valor Concedido** (versionamento automático do `valorLimiteBrl` para análise de tendência). Novo endpoint `GET /{id}` expõe o DTO completo incluindo ambas as coleções. Acompanha migration `S5_GarantiasExigidasLimite`, 9 testes de integração e coleção Bruno atualizada.

---

### ADDITIVE — Limites de Banco — Garantias Exigidas

Cada `LimiteBanco` passa a suportar zero ou mais garantias exigidas pelo banco para liberar a linha de crédito.

**Entidade nova:** `GarantiaExigidaLimite`

- Propriedades: `Tipo` (enum `TipoGarantia`), `PercentualSobreLimite` (decimal?), `ValorFixoBrl` (Money?), `Obrigatoria` (bool), `Observacoes` (string?), `CreatedAt`, `UpdatedAt`.
- `Obrigatoria = true` indica que o banco exige a garantia; `false` indica que negocia.

**Validações de domínio:**

- `PercentualSobreLimite` e `ValorFixoBrl` são mutuamente exclusivos. Ambos preenchidos lança `ArgumentException` (→ 400).
- Para todos os tipos exceto `Aval`, ao menos um dos dois deve ser informado. Nenhum dos dois lança `ArgumentException` (→ 400).
- Para `Aval`, ambos podem ser nulos (representa exigência implícita de aval dos sócios cobrindo 100% da exposição).
- `PercentualSobreLimite` deve estar no intervalo (0, 100].
- Não podem existir duas garantias com o mesmo `TipoGarantia` no mesmo limite. Duplicata lança `InvalidOperationException` (→ 409).

**Endpoints estendidos:**

| Endpoint | Mudança |
|----------|---------|
| `POST /api/v1/limites-banco` | Campo opcional `garantiasExigidas: CriarGarantiaExigidaLimiteRequest[]` |
| `PATCH /api/v1/limites-banco/{id}` | Campo opcional `garantiasExigidas`: `null` = preservar; `[]` = remover todas; itens = replace-all |

**DTOs novos:** `GarantiaExigidaLimiteDto`, `CriarGarantiaExigidaLimiteRequest`.

**DTO estendido:** `LimiteBancoDto` agora inclui `garantiasExigidas: GarantiaExigidaLimiteDto[]`.

---

### ADDITIVE — Limites de Banco — Histórico de Valor Concedido

O campo `valorLimiteBrl` passa a ser versionado automaticamente. Toda alteração de valor via `PATCH` registra uma entrada em `LimiteBancoHistorico` com o valor anterior e o novo. A criação do limite registra a entrada inicial com `valorAnteriorBrl = null` e `observacoes = "Criação do limite"`.

O histórico serve à análise de tendência: permite identificar bancos que aumentam ou reduzem o limite ao longo do tempo, apoiando decisões de diversificação de captação.

**Entidade nova:** `LimiteBancoHistorico`

- Propriedades: `LimiteBancoId`, `ValorAnteriorBrl` (Money?), `ValorNovoBrl` (Money), `RegistradoEm` (Instant), `Observacoes` (string?).
- `ValorAnteriorBrl` é `null` exclusivamente na entrada de criação.

**DTO novo:** `LimiteBancoHistoricoDto`.

**DTO estendido:** `LimiteBancoDto` agora inclui `historico: LimiteBancoHistoricoDto[]`, ordenado por `registradoEm` crescente.

---

### ADDITIVE — Limites de Banco — Endpoint GET /{id}

Novo endpoint para busca individual de limite operacional pelo identificador.

```
GET /api/v1/limites-banco/{id}
Autorização: Leitura
```

**Resposta:** `LimiteBancoDto` completo com `garantiasExigidas` e `historico` populados.

**Erros:**
- `404 Not Found` — limite não encontrado.

O endpoint `GET /api/v1/limites-banco` (listagem) também inclui `garantiasExigidas` e `historico` no DTO retornado, pois o mapper `LimiteBancoDto.From` é compartilhado.

---

### INTERNAL — Migration `S5_GarantiasExigidasLimite`

**Tabelas criadas:**

| Tabela | Schema | Descrição |
|--------|--------|-----------|
| `limite_banco_garantia_exigida` | `sgcf` | Garantias exigidas por limite |
| `limite_banco_historico` | `sgcf` | Histórico de valores concedidos |

**Colunas principais — `limite_banco_garantia_exigida`:**

| Coluna | Tipo PG | Notas |
|--------|---------|-------|
| `id` | `uuid` | PK |
| `limite_banco_id` | `uuid` | FK → `limite_banco.id` (CASCADE DELETE) |
| `tipo` | `integer` | Enum `TipoGarantia` como inteiro |
| `percentual_sobre_limite` | `numeric(7,4)` | Nullable |
| `valor_fixo_brl` | `numeric(20,6)` | Nullable |
| `obrigatoria` | `boolean` | — |
| `observacoes` | `text` | Nullable |
| `created_at` / `updated_at` | `timestamptz` | — |

**CHECK constraints — `limite_banco_garantia_exigida`:**

| Nome | Regra |
|------|-------|
| `ck_garantia_exigida_percentual_intervalo` | `percentual_sobre_limite IS NULL OR (percentual_sobre_limite > 0 AND percentual_sobre_limite <= 100)` |
| `ck_garantia_exigida_percentual_xor_valor` | XOR entre percentual e valor fixo, com exceção para `tipo = 3` (Aval) |
| `ck_garantia_exigida_valor_fixo_positivo` | `valor_fixo_brl IS NULL OR valor_fixo_brl > 0` |

**Colunas principais — `limite_banco_historico`:**

| Coluna | Tipo PG | Notas |
|--------|---------|-------|
| `id` | `uuid` | PK |
| `limite_banco_id` | `uuid` | FK → `limite_banco.id` (CASCADE DELETE) |
| `valor_anterior_brl` | `numeric(20,6)` | Nullable (null = entrada de criação) |
| `valor_novo_brl` | `numeric(20,6)` | NOT NULL |
| `registrado_em` | `timestamptz` | — |
| `observacoes` | `text` | Nullable |

**Índices:**

| Nome | Tabela | Colunas | Tipo |
|------|--------|---------|------|
| `ix_garantia_exigida_limite_banco` | `limite_banco_garantia_exigida` | `limite_banco_id` | B-tree |
| `ux_garantia_exigida_limite_tipo` | `limite_banco_garantia_exigida` | `(limite_banco_id, tipo)` | Unique |
| `ix_limite_banco_historico_limite` | `limite_banco_historico` | `limite_banco_id` | B-tree |
| `ix_limite_banco_historico_limite_registrado_em` | `limite_banco_historico` | `(limite_banco_id, registrado_em)` | B-tree |

O índice único `ux_garantia_exigida_limite_tipo` reforça em nível de banco de dados a invariante de não duplicação por tipo, complementando a validação do domínio.

Ambas as tabelas usam `CASCADE DELETE` referenciando `limite_banco.id`.

---

## [0.5.0] — 2026-05-16

### Resumo executivo

Lançamento do módulo de **Cotações de Captação** (MVP, modalidade FINIMP). Três novos controllers entram em produção (`CotacoesController`, `LimitesBancoController`, `CdiSnapshotsController`), totalizando 24 novos endpoints. O módulo cobre o ciclo completo: registro de propostas multi-banco, cálculo automático de CET, comparação lado a lado, aceitação rastreada e conversão em contrato com mensuração de economia ajustada por CDI. Acompanha 5 cenários de golden dataset, coleção Bruno completa e nova migration `S3Cotacoes`.

Detalhes da especificação em [`docs/specs/cotacoes/SPEC.md`](../specs/cotacoes/SPEC.md). Documentação operacional em [`docs/api/cotacoes.md`](../api/cotacoes.md), [`docs/api/limites-banco.md`](../api/limites-banco.md) e [`docs/api/cdi-snapshots.md`](../api/cdi-snapshots.md).

---

### ADDITIVE — Cotações (`/api/v1/cotacoes`)

**Novo controller:** `CotacoesController`

**Endpoints adicionados (19):**

| Método | Path | Política |
|--------|------|----------|
| `GET` | `/api/v1/cotacoes` | Leitura |
| `POST` | `/api/v1/cotacoes` | Escrita |
| `GET` | `/api/v1/cotacoes/{id}` | Leitura |
| `PATCH` | `/api/v1/cotacoes/{id}` | Escrita |
| `DELETE` | `/api/v1/cotacoes/{id}` | Escrita |
| `POST` | `/api/v1/cotacoes/{id}/bancos` | Escrita |
| `DELETE` | `/api/v1/cotacoes/{id}/bancos/{bancoId}` | Escrita |
| `POST` | `/api/v1/cotacoes/{id}/enviar` | Escrita |
| `POST` | `/api/v1/cotacoes/{id}/encerrar-captacao` | Escrita |
| `POST` | `/api/v1/cotacoes/{id}/cancelar` | Escrita |
| `POST` | `/api/v1/cotacoes/{id}/refresh-mercado` | Escrita |
| `GET` | `/api/v1/cotacoes/{id}/comparativo` | Leitura |
| `GET` | `/api/v1/cotacoes/{id}/auditoria` | Auditoria |
| `GET` | `/api/v1/cotacoes/economia` | Leitura |
| `POST` | `/api/v1/cotacoes/{id}/propostas` | Escrita |
| `PATCH` | `/api/v1/cotacoes/{id}/propostas/{propostaId}` | Escrita |
| `POST` | `/api/v1/cotacoes/{id}/propostas/{propostaId}/aceitar` | Escrita |
| `POST` | `/api/v1/cotacoes/{id}/propostas/{propostaId}/desfazer-aceitacao` | Escrita |
| `POST` | `/api/v1/cotacoes/{id}/converter-em-contrato` | Escrita |

**DTOs adicionados:** `CotacaoDto`, `PropostaDto`, `ComparativoDto`, `EconomiaNegociacaoDto`, `EconomiaPeriodoDto` (+ `EconomiaMesDto`, `EconomiaPorBancoDto`).

**Enums adicionados:** `StatusCotacao` (`Rascunho`, `EmCaptacao`, `Comparada`, `Aceita`, `Convertida`, `Recusada`), `StatusProposta` (`Recebida`, `Aceita`, `Recusada`, `Expirada`).

**Regras críticas:**
- Apenas uma proposta por cotação pode estar `Aceita`.
- `DataPtaxReferencia` deve ser dia útil anterior à `DataAbertura`; falha se PTAX D-1 ausente.
- Adicionar banco-alvo valida `valorAlvoBrl ≤ valorDisponivelBrl` do `LimiteBanco` do par banco/modalidade.
- Conversão em contrato cria `Contrato` + `EconomiaNegociacao` atomicamente e incrementa `ValorUtilizadoBrl` do limite.
- CET é recalculado automaticamente em `RegistrarPropostaCommand`, `AtualizarPropostaCommand` e `RefreshCotacaoMercadoCommand`.

---

### ADDITIVE — Limites de Banco (`/api/v1/limites-banco`)

**Novo controller:** `LimitesBancoController`

**Endpoints adicionados (3):**

| Método | Path | Política |
|--------|------|----------|
| `GET` | `/api/v1/limites-banco` | Leitura |
| `POST` | `/api/v1/limites-banco` | Admin |
| `PATCH` | `/api/v1/limites-banco/{id}` | Admin |

**DTO adicionado:** `LimiteBancoDto` com `valorLimiteBrl`, `valorUtilizadoBrl`, `valorDisponivelBrl` (computed), vigência por período.

**Regras críticas:**
- Não permite sobreposição de vigência para o mesmo par banco/modalidade.
- `valorUtilizadoBrl` é mantido pela API ao converter cotações em contratos.

---

### ADDITIVE — CDI Snapshots (`/api/v1/cdi-snapshots`)

**Novo controller:** `CdiSnapshotsController`

**Endpoints adicionados (2):**

| Método | Path | Política |
|--------|------|----------|
| `GET` | `/api/v1/cdi-snapshots` | Leitura |
| `POST` | `/api/v1/cdi-snapshots` | Admin |

**DTO adicionado:** `CdiSnapshotDto` com `data`, `cdiAaPercentual`, `createdAt`.

**Notas:**
- Cadastro manual no MVP (integração ANBIMA prevista para ondas futuras).
- Snapshot é consultado pelo cálculo de `EconomiaAjustadaCdiBrl` na conversão da cotação em contrato.
- `GET` sem parâmetros retorna últimos 30 dias (timezone `America/Sao_Paulo`).

---

### INTERNAL — Migration `S3Cotacoes`

Nova migration EF Core cria as tabelas `cotacoes`, `cotacao_propostas`, `cotacao_bancos_alvo`, `limites_banco`, `economia_negociacao`, `cdi_snapshots`. Soft delete habilitado para `cotacoes` via coluna `deleted_at` e filtro global. Indexes em `(bancoId, modalidade, dataVigenciaInicio, dataVigenciaFim)` para resolução do limite vigente; em `(data)` único para `cdi_snapshots`; em `(cotacaoId, status)` para `cotacao_propostas`.

---

### INTERNAL — Refactor `Cotacoes` → `Cambio`

O módulo anterior chamado `Cotacoes` (que tratava de PTAX/Spot) foi renomeado para `Cambio` (`Sgcf.Domain.Cambio`, `Sgcf.Application.Cambio`) para liberar o nome ao novo módulo de cotações de captação. Os endpoints `/api/v1/parametros-cotacao` permanecem inalterados na URL pública.

---

### Golden dataset

Cinco cenários de regressão adicionados em `tests/Sgcf.GoldenDataset/data/cotacoes/`:

1. FINIMP simples USD/BRL — CET sem NDF.
2. FINIMP com NDF — verificação de custo do hedge.
3. FINIMP com CDB cativo — verificação de rendimento subtraindo do CET.
4. Comparativo de 3 propostas com prazos diferentes — coluna 3 (custo total equivalente).
5. Economia negociada ajustada por CDI — VPL de fluxos com prazos distintos.

---

## [0.4.0] — 2026-05-14

### Resumo executivo

Sprint 3 completo. Quatro novos grupos de funcionalidades entram em produção: Audit Log automático em todas as entidades, edição de contratos via PATCH, CRUD de feriados do calendário e lançamentos contábeis por conta. Dois novos controllers adicionados (`AuditoriaController`, extensão do `FeriadosController`).

---

### ADDITIVE — Audit Log (`GET /audit/eventos`)

**Novo controller:** `AuditoriaController`

**Endpoint adicionado:**

```
GET /audit/eventos
Política: Auditoria (roles: auditor, admin)
```

**Query params disponíveis:**

| Parâmetro | Tipo | Obrigatório |
|-----------|------|-------------|
| `entity` | string | Não |
| `entityId` | guid | Não |
| `actorSub` | string | Não |
| `source` | `rest\|mcp\|a2a\|job` | Não |
| `operation` | `CREATE\|UPDATE\|DELETE` | Não |
| `de` | DateTimeOffset | Não |
| `ate` | DateTimeOffset | Não |
| `page` | int (≥1) | Não (padrão: 1) |
| `pageSize` | int (1–200) | Não (padrão: 50) |

**Resposta:**

```json
{
  "items": [AuditEventoDto],
  "total": 840,
  "page": 1,
  "pageSize": 50
}
```

**AuditEventoDto (novo schema):**

```json
{
  "id": "long",
  "occurredAt": "DateTimeOffset",
  "actorSub": "string",
  "actorRole": "string",
  "source": "rest|mcp|a2a|job",
  "entity": "string",
  "entityId": "guid|null",
  "operation": "CREATE|UPDATE|DELETE",
  "diffJson": "string|null",
  "requestId": "guid"
}
```

**Infra:** Tabela `sgcf.audit_log` (bigserial PK) criada via migration `20260514184348_AuditLog`. Interceptor `AuditInterceptor` (EF Core `SaveChangesInterceptor`) registra automaticamente toda entidade `IAuditable` em qualquer SaveChanges.

**Entidades auditadas automaticamente:** `Contrato`, `Banco`, `Feriado`, `InstrumentoHedge`, `Garantia`, `EventoCronograma`, `EbitdaMensal`, `ParametroCotacao`, `PlanoContas`, `LancamentoContabil`.

---

### ADDITIVE — Atualização de Contrato (`PATCH /api/v1/contratos/{id}`)

**Endpoint adicionado ao `ContratosController`:**

```
PATCH /api/v1/contratos/{id}
Política: Escrita (roles: tesouraria, admin)
```

Atualização parcial: apenas os campos enviados com valor não-nulo são aplicados.

**Request body (todos os campos opcionais):**

```json
{
  "numeroExterno": "string|null",
  "taxaAa": "decimal|null",
  "dataVencimento": "YYYY-MM-DD|null",
  "observacoes": "string|null",
  "baseCalculo": "Dias252|Dias360|Dias365|null",
  "periodicidade": "Bullet|Mensal|Trimestral|Semestral|Anual|null",
  "estruturaAmortizacao": "Bullet|Price|Sac|Customizada|null",
  "quantidadeParcelas": "int|null",
  "dataPrimeiroVencimento": "YYYY-MM-DD|null",
  "convencaoDataNaoUtil": "Following|ModifiedFollowing|Preceding|NoAdjustment|null"
}
```

**Resposta:** `200 OK` com `ContratoDto` atualizado, ou `404` se não encontrado.

**Validações:**
- `dataVencimento` deve ser posterior a `dataContratacao` original.
- `dataPrimeiroVencimento` deve ser posterior a `dataContratacao` original.
- `quantidadeParcelas` deve ser ≥ 1.
- Enums são validados por nome (case-insensitive).

---

### ADDITIVE — Feriados CRUD (`POST /DELETE /api/v1/feriados`)

**Endpoints adicionados ao `FeriadosController`:**

```
POST   /api/v1/feriados          Política: Admin
DELETE /api/v1/feriados/{id}     Política: Admin
```

O endpoint `GET /api/v1/feriados` já existia e não foi alterado.

**POST request body:**

```json
{
  "data": "YYYY-MM-DD",
  "descricao": "string",
  "abrangencia": "Nacional|Estadual|Municipal",
  "tipo": "FixoCalendario|MovelCalendario|Pontual"
}
```

**Resposta POST:** `201 Created` com `FeriadoDto`.

**FeriadoDto (novo schema):**

```json
{
  "id": "guid",
  "data": "YYYY-MM-DD",
  "descricao": "string",
  "abrangencia": "Nacional|Estadual|Municipal",
  "tipo": "FixoCalendario|MovelCalendario|Pontual",
  "fonte": "Manual|Anbima",
  "createdAt": "DateTimeOffset"
}
```

Feriados criados via `POST` recebem `fonte = Manual`. A exclusão via `DELETE` retorna `204 No Content`.

---

### ADDITIVE — Lançamentos Contábeis (`POST /GET /api/v1/plano-contas/{id}/lancamentos`)

**Endpoints adicionados ao `PlanoContasController`:**

```
POST /api/v1/plano-contas/{contaId}/lancamentos    Política: Escrita
GET  /api/v1/plano-contas/{contaId}/lancamentos    Política: Auditoria
```

**POST request body:**

```json
{
  "contratoId": "guid",
  "data": "YYYY-MM-DD",
  "origem": "string (máx. 50 chars)",
  "valorDecimal": "decimal > 0",
  "moeda": "Brl|Usd|Eur|Jpy|Cny",
  "descricao": "string"
}
```

**Resposta POST:** `201 Created` com `LancamentoContabilDto`.

**LancamentoContabilDto (novo schema):**

```json
{
  "id": "guid",
  "contratoId": "guid",
  "planoContaId": "guid",
  "data": "YYYY-MM-DD",
  "origem": "string",
  "valor": "decimal",
  "moeda": "string",
  "descricao": "string",
  "createdAt": "DateTimeOffset"
}
```

**GET resposta:** `IReadOnlyList<LancamentoContabilDto>`, ordenado por `data` decrescente.

**Nota de interface:** O `contaId` no path de ambos os endpoints refere-se ao ID da `PlanoContas`. O campo `contratoId` no body associa o lançamento a um contrato específico. O mesmo `contaId` aparece como `planoContaId` no DTO de resposta.

---

### ADDITIVE — Campos de amortização em Contrato

Os seguintes campos foram adicionados ao `ContratoDto` e ao `POST /api/v1/contratos` (todos opcionais com defaults):

| Campo | Tipo | Default | Descrição |
|-------|------|---------|-----------|
| `periodicidade` | string | `Bullet` | Frequência de pagamento |
| `estruturaAmortizacao` | string | `Bullet` | Tabela de amortização |
| `quantidadeParcelas` | int | `1` | Número de parcelas |
| `dataPrimeiroVencimento` | date | `dataVencimento` | Data da primeira parcela |
| `anchorDiaMes` | string | `DiaContratacao` | Âncora do dia de vencimento |
| `anchorDiaFixo` | int\|null | `null` | Dia fixo (1–31) quando `anchorDiaMes = DiaFixo` |
| `periodicidadeJuros` | string\|null | `null` | Periodicidade dos juros quando diferente do principal |
| `convencaoDataNaoUtil` | string | `Following` | Convenção para datas em fins de semana/feriados |

**Enums novos:**

`Periodicidade`: `Bullet | Mensal | Bimestral | Trimestral | Semestral | Anual`

`EstruturaAmortizacao`: `Bullet | Price | Sac | Customizada`

`AnchorDiaMes`: `DiaContratacao | DiaFixo | UltimoDiaMes`

`ConvencaoDataNaoUtil`: `Following | ModifiedFollowing | Preceding | NoAdjustment`

---

### INTERNAL — ICurrentUserService / IRequestContextService

Cada host agora injeta sua própria implementação de contexto:

| Host | `source` | Implementação |
|------|----------|---------------|
| `Sgcf.Api` | `rest` | `HttpCurrentUserService` + `HttpRequestContextService` |
| `Sgcf.Mcp` | `mcp` | `HttpCurrentUserService` + `McpRequestContextService` |
| `Sgcf.A2a` | `a2a` | `HttpCurrentUserService` + `A2aRequestContextService` |
| `Sgcf.Jobs` | `job` | `SystemCurrentUserService` + `SystemRequestContextService` |

O `actorSub` do Audit Log reflete o claim `sub` do JWT em requisições autenticadas e `"system"` em jobs.

---

## [0.3.0] — 2026-05-14 (Sprint 2 — Cronograma + Strategies)

### Resumo executivo

Motor de cronograma completo com 4 estratégias de amortização. Integração BCB/PTAX. Jobs de background. Domínio de hedges NDF e MTM.

### ADDITIVE — Estratégias de amortização

| Estratégia | Classe | Descrição |
|------------|--------|-----------|
| `Bullet` | `BulletStrategy` | Pagamento único no vencimento |
| `BulletComJurosPeriodicos` | `BulletComJurosPeriodicosStrategy` | Principal bullet + juros periódicos |
| `Price` | `PriceStrategy` | Parcelas iguais (sistema francês) |
| `Customizada` | `CustomizadaStrategy` | Parcelas manuais via importação |

### ADDITIVE — `POST /api/v1/contratos/{id}/importar-cronograma`

Importa cronograma manual parcela a parcela. Body: `ParcelaManualRequest[]`.

### ADDITIVE — `POST /api/v1/contratos/{id}/gerar-cronograma`

Gera cronograma automaticamente com base nos parâmetros de amortização do contrato.

### ADDITIVE — Hedges e MTM

```
GET  /api/v1/contratos/{id}/hedges
POST /api/v1/contratos/{id}/hedges
GET  /api/v1/hedges/{hedgeId}/mtm
DELETE /api/v1/hedges/{hedgeId}
```

### ADDITIVE — Jobs de background

| Job | Schedule | Descrição |
|-----|----------|-----------|
| `IngestaoPtaxJob` | Diário 13h20 | Ingere PTAX do BCB |
| `RecalcularMtmJob` | A cada 5min (horário de mercado) | Recalcula MTM dos NDFs |
| `AlertaVencimentoJob` | Diário 8h | Dispara alertas D-7/D-3/D-0 |
| `ProvisaoJurosDiariaJob` | Diário 18h | Provisiona juros acumulados |
| `SnapshotMensalJob` | Dia 1 de cada mês | Gera snapshot posicional imutável |

---

## [0.2.0] — 2026-05-13 (Sprint 1 — Base)

### Resumo executivo

Estrutura inicial completa: 8 controllers, MCP tools, A2A skill, domain model com todas as entidades, EF Core + PostgreSQL, autenticação JWT, FluentValidation, MediatR.

### Endpoints disponíveis na v0.2.0

| Grupo | Endpoints |
|-------|-----------|
| Bancos | `GET /bancos`, `GET /bancos/{id}`, `GET /bancos/{identifier}`, `POST /bancos`, `PUT /bancos/{id}/config-antecipacao` |
| Contratos | `GET /contratos`, `GET /contratos/{id}`, `POST /contratos`, `DELETE /contratos/{id}`, `GET /contratos/{id}/tabela-completa`, `POST /contratos/{id}/simular-antecipacao` |
| Garantias | `GET /contratos/{id}/garantias`, `GET /contratos/{id}/garantias/indicadores`, `POST /contratos/{id}/garantias`, `DELETE /contratos/{id}/garantias/{gId}` |
| Painel | `GET /painel/divida`, `GET /painel/garantias`, `GET /painel/vencimentos`, `GET /painel/kpis`, `POST /painel/ebitda` |
| Simulador | `POST /simulador/cenario-cambial`, `POST /simulador/antecipacao-portfolio` |
| Plano de Contas | `GET /plano-contas`, `GET /plano-contas/{id}`, `POST /plano-contas`, `PUT /plano-contas/{id}` |
| Parâmetros Cotação | `GET /parametros-cotacao`, `GET /parametros-cotacao/{id}`, `POST /parametros-cotacao`, `PUT /parametros-cotacao/{id}`, `DELETE /parametros-cotacao/{id}`, `GET /parametros-cotacao/resolve` |
| Feriados | `GET /feriados` |
| MCP | `list_contratos`, `get_contrato`, `get_tabela_completa`, `get_posicao_divida`, `get_calendario_vencimentos`, `get_cotacao_fx`, `get_mtm_hedge`, `simular_cenario_cambial`, `simular_antecipacao` |
| A2A | skill `consulta_posicao_divida` |

---

## [0.1.0] — 2026-05-08 (Sprint 0 — Commit inicial)

Estrutura de projeto criada. Domain model base. Migrations iniciais. Sem endpoints funcionais.

---

## Guia de migração para agentes de IA

### Ao consumir v0.4.0

1. **Audit Log:** `GET /audit/eventos` está disponível. Use `source=mcp` ou `source=a2a` para filtrar eventos gerados por agentes.

2. **PATCH Contrato:** Para editar um contrato existente, use `PATCH /api/v1/contratos/{id}` com apenas os campos que precisam mudar. Não é necessário enviar o contrato inteiro.

3. **ContratoDto expandido:** A resposta de `GET /contratos/{id}` e `POST /contratos` agora inclui 8 campos novos de amortização (`periodicidade`, `estruturaAmortizacao`, `quantidadeParcelas`, `dataPrimeiroVencimento`, `anchorDiaMes`, `anchorDiaFixo`, `periodicidadeJuros`, `convencaoDataNaoUtil`). Parsers que ignoram campos desconhecidos não são impactados.

4. **Lançamentos contábeis:** Para registrar um lançamento, faça `POST /api/v1/plano-contas/{contaId}/lancamentos` com `contratoId`, `data`, `origem`, `valorDecimal`, `moeda`, `descricao`.

5. **Feriados:** Para verificar se uma data é feriado, consulte `GET /api/v1/feriados?ano={ano}`. O retorno é uma lista de `FeriadoDto`.

### Enums com valores novos em v0.4.0

| Enum | Valores novos |
|------|---------------|
| `Periodicidade` | `Bullet`, `Mensal`, `Bimestral`, `Trimestral`, `Semestral`, `Anual` |
| `EstruturaAmortizacao` | `Bullet`, `Price`, `Sac`, `Customizada` |
| `AnchorDiaMes` | `DiaContratacao`, `DiaFixo`, `UltimoDiaMes` |
| `ConvencaoDataNaoUtil` | `Following`, `ModifiedFollowing`, `Preceding`, `NoAdjustment` |
| `StatusContrato` | + `RefinanciadoParcial`, `RefinanciadoTotal` (adicionados sobre os 5 existentes) |
| `EscopoFeriado` | `Nacional`, `Estadual`, `Municipal` |
| `TipoFeriado` | `FixoCalendario`, `MovelCalendario`, `Pontual` |
| `FonteFeriado` | `Manual`, `Anbima` |
