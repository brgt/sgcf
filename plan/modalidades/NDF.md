# NDF — Non-Deliverable Forward (Hedge Cambial)

**Pergunta do PO consolidada neste documento**:
- Q5 — *"Em um contrato 4131 o valor do dólar da NDF pode variar em um mesmo contrato. Por exemplo, as vezes eles utilizam o dólar strike de 5,2 para os próximos 6 meses e 5,5 para o dólar do vencimento em 12 meses. Como isso está previsto?"*

> Para detalhes transversais, consultar:
> - `00_DiasUteis_Calendario.md` — convenção `ModifiedFollowing` típica de derivativos
> - `00_Cronograma_Estrutura.md` — vinculação opcional do strike a um `EventoCronograma`
> - `4131.md` — onde o cenário do PO ocorre na prática

---

## 1. Resumo executivo

NDF (Non-Deliverable Forward) é um derivativo cambial **liquidado financeiramente em BRL** (sem entrega física da moeda), usado para travar o resultado cambial de um contrato em moeda estrangeira (FINIMP, 4131, importação direta). No SGCF é cadastrado 1:1 com o contrato hedgeado e pode assumir duas formas:

1. **Forward simples**: 1 strike único, 1 data de vencimento.
2. **Collar (Forward com bandas)**: 2 strikes (Put e Call), 1 data de vencimento.

O modelo atual `InstrumentoHedge.cs:1-111` cobre as duas formas com **um único strike por hedge**. A pergunta do PO revela um cenário comum em 4131 plurianuais: **múltiplos strikes ao longo do tempo**, escalonados conforme a curva forward USD/BRL. Hoje o sistema **não suporta** esse cenário.

Propõe-se introduzir a entidade `StrikePorVencimento` em relação 1:N com `InstrumentoHedge`, mantendo compatibilidade com o caso single-strike (uma única linha na sub-tabela).

---

## 2. Cenário de negócio

### 2.1 Forward simples (estado atual cobre)

- Contrato 4131 de 6 meses, USD 1.000.000.
- NDF: liquida 14/11/2026, strike R$ 5,15.
- Na liquidação: se PTAX médio dos últimos 4 dias úteis = R$ 5,25 → tomador recebe `(5,25 − 5,15) × 1.000.000 = R$ 100.000`. Se PTAX = R$ 5,05 → paga `R$ 100.000`.

### 2.2 Collar (estado atual cobre)

- Contrato 4131 de 12 meses, USD 1.000.000.
- NDF: liquida 14/05/2027, strike Put = R$ 4,80, strike Call = R$ 5,40.
- Liquidação:
  - PTAX abaixo de 4,80 → paga `(4,80 − PTAX) × notional`.
  - PTAX acima de 5,40 → recebe `(PTAX − 5,40) × notional`.
  - PTAX entre 4,80 e 5,40 → sem fluxo (zona neutra).

### 2.3 NDF com múltiplos strikes por vencimento (cenário PO)

**Caso do PO**: contrato 4131 de 12 meses com pagamentos semestrais (USD 500.000 cada), tomador contrata NDF para hedgear ambas as pernas:

| Perna | Vencimento | Notional | Strike contratado |
|-------|-----------|----------|-------------------|
| 1 | 14/11/2026 | USD 500.000 | R$ 5,20 |
| 2 | 14/05/2027 | USD 500.000 | R$ 5,50 |

Cada perna é liquidada independentemente, usando o strike específico. Operacionalmente, no banco isso pode ser:

- **(a) Um único contrato NDF** com tabela de strikes por vencimento.
- **(b) Dois NDFs independentes** sob o mesmo número-mãe.

Mercado brasileiro pratica ambos. No SGCF, propõe-se modelar como **(a)** — um `InstrumentoHedge` com N strikes — para refletir a forma como o tomador percebe o produto (um único hedge escalonado).

### 2.4 Diferença entre "NDF múltiplos vencimentos" e "Target Forward / Strip"

- **NDF strip** (o que o PO descreveu): cada perna é independente; resultado é a soma dos resultados individuais.
- **Target forward / Target redemption forward**: produto exótico em que ganhos acumulados acima de um teto encerram antecipadamente o hedge. Fora do escopo inicial; modelo extensível para acomodar via campo `EstruturaExotica` no futuro.

---

## 3. Estado atual no sistema

### 3.1 Modelo

`Sgcf.Domain/Hedge/InstrumentoHedge.cs:1-111`:

| Campo | Tipo | Observação |
|-------|------|------------|
| `ContratoId` | `Guid` | Relação 1:1 com contrato hedgeado |
| `Tipo` | `TipoHedge` | `Forward` ou `Collar` |
| `ContraparteId` | `Guid` | Banco contraparte do hedge |
| `StrikeForward` | `decimal?` | Único strike no Forward |
| `StrikePut` | `decimal?` | Limite inferior no Collar |
| `StrikeCall` | `decimal?` | Limite superior no Collar |
| `Status` | `StatusHedge` | `Ativo`, `Cancelado`, `Liquidado` |

Factories:
- `CriarForward(strikeForward)` → grava em `StrikeForward`.
- `CriarCollar(strikePut, strikeCall)` → grava em `StrikePut` e `StrikeCall`.

### 3.2 Limitações

- Strikes são campos escalares — **não suportam múltiplas datas de vencimento**.
- Não há entidade que represente "perna do hedge".
- O cálculo de MTM (`plan/Anexo_A_Valoracao_Divida_NDF.md`) assume strike único.
- Vinculação a `EventoCronograma` do contrato hedgeado não existe (relevante quando o tomador quer mostrar que cada perna do hedge cobre uma parcela específica).

---

## 4. GAPs identificados

| # | GAP | Severidade |
|---|-----|-----------|
| N1 | Não existe `StrikePorVencimento` (entidade 1:N) | Alta |
| N2 | MTM assume strike único | Alta |
| N3 | Liquidação assume 1 evento; precisa N eventos | Alta |
| N4 | Vinculação opcional `StrikePorVencimento.EventoCronogramaId` | Média |
| N5 | UI não permite adicionar múltiplos strikes | Alta |
| N6 | API de criação de NDF aceita apenas escalar | Alta |
| N7 | `Anexo_A_Valoracao_Divida_NDF.md` precisa atualização para multi-strike | Média |

---

## 5. Proposta de estrutura

### 5.1 Nova entidade `StrikePorVencimento`

| Campo | Tipo | Descrição |
|-------|------|-----------|
| `Id` | `Guid` | PK |
| `InstrumentoHedgeId` | `Guid` | FK |
| `OrdemPerna` | `int` (1..N) | Ordenação cronológica |
| `DataVencimento` | `LocalDate` | Data de liquidação da perna |
| `NotionalMoedaOriginal` | `Money` | Volume da perna (ex.: USD 500.000) |
| `StrikeForward` | `decimal?` | Forward simples |
| `StrikePut` | `decimal?` | Collar |
| `StrikeCall` | `decimal?` | Collar |
| `EventoCronogramaId` | `Guid?` | Vínculo opcional com parcela do contrato hedgeado |
| `Status` | `StatusPerna` | `Ativa`, `Liquidada`, `Cancelada` |
| `DataLiquidacaoEfetiva` | `LocalDate?` | Quando efetivamente liquidou |
| `PtaxApurada` | `decimal?` | PTAX média dos 4 dias úteis anteriores |
| `ResultadoFinanceiroBrl` | `Money?` | Resultado da perna (positivo = recebível) |

Índice: `(InstrumentoHedgeId, OrdemPerna)` único; `(InstrumentoHedgeId, DataVencimento)` para consulta.

### 5.2 Refatoração de `InstrumentoHedge`

Os campos `StrikeForward`, `StrikePut`, `StrikeCall`, `DataVencimento` (se existir) migram para `StrikePorVencimento`. O `InstrumentoHedge` passa a ter:

| Campo | Status |
|-------|--------|
| `Id`, `ContratoId`, `Tipo`, `ContraparteId`, `Status` | mantidos |
| `Notional` (total agregado) | adicionado (soma dos notionals das pernas) |
| `DataInicioVigencia` | adicionado |
| `DataUltimoVencimento` | computado |
| Campos `Strike*` antigos | **deprecados**. Mantidos no schema temporariamente para migração; novos cadastros vão direto para `StrikePorVencimento`. |

### 5.3 Migração de dados existentes

Para hedges legados com strikes escalares:
- Cria uma única `StrikePorVencimento` com `OrdemPerna = 1`, copiando os campos.
- `DataVencimento` legada (se existir em outra coluna) migra; senão usa `Contrato.DataVencimento` como fallback.
- Após validação, schema antigo de `Strike*` em `InstrumentoHedge` pode ser removido em migração subsequente.

### 5.4 UI no Step 2

Sub-formulário `DetalheNdf.vue`:

```
+-----------------------------------------------------------------+
| NDF — Hedge cambial                                             |
+-----------------------------------------------------------------+
| Tipo:          [ Forward ▾]                                    |
| Contraparte:   [ Banco BB ▾]                                   |
| Múltiplos strikes? [✓] Sim                                    |
|                                                                 |
| Pernas:                                                         |
| ┌───┬──────────────┬──────────────┬──────────┬───────────────┐ |
| │ # │ Vencimento   │ Notional USD │ Strike   │ Parcela vinc. │ |
| ├───┼──────────────┼──────────────┼──────────┼───────────────┤ |
| │ 1 │ 14/11/2026   │ 500.000,00   │ 5,20     │ Parc. #1 ▾    │ |
| │ 2 │ 14/05/2027   │ 500.000,00   │ 5,50     │ Parc. #2 ▾    │ |
| └───┴──────────────┴──────────────┴──────────┴───────────────┘ |
| [+ Adicionar perna]                                             |
|                                                                 |
| Total notional: USD 1.000.000,00                                |
+-----------------------------------------------------------------+
```

Para Collar, a coluna `Strike` desdobra em `Strike Put` e `Strike Call`.

### 5.5 Cálculo de MTM multi-perna

`Anexo_A_Valoracao_Divida_NDF.md` será revisado. Resumo do novo cálculo:

```
mtm_total = Σ_perna mtm_perna

mtm_perna(t) =
    se Tipo == Forward:
        (Forward(t, DataVencimento) − Strike) × Notional × DF(t, DataVencimento)
    se Tipo == Collar:
        max(0, Forward(t, DataVencimento) − StrikeCall) × Notional × DF
        − max(0, StrikePut − Forward(t, DataVencimento)) × Notional × DF
```

Onde `Forward(t, T)` é a cotação forward USD/BRL para vencimento `T` na data `t`, e `DF` é o fator de desconto até `T`.

A função de MTM consome a curva forward externa (B3 ou outra). Curva é um dado de entrada (`ICurvaForwardProvider`).

### 5.6 Liquidação

Quando `DataVencimento` chega:
1. Job ou tela manual consulta PTAX média dos últimos 4 dias úteis.
2. Calcula `ResultadoFinanceiroBrl`.
3. Marca `StrikePorVencimento.Status = Liquidada` e preenche `DataLiquidacaoEfetiva`, `PtaxApurada`, `ResultadoFinanceiroBrl`.
4. Quando todas as pernas liquidaram, `InstrumentoHedge.Status = Liquidado` automaticamente.

### 5.7 Vinculação com cronograma do contrato hedgeado

Campo `EventoCronogramaId` em `StrikePorVencimento` permite ao usuário (e a relatórios) responder "qual perna do hedge cobre qual parcela do contrato". A vinculação é **informativa**, não restringe operações.

---

## 6. Impacto em APIs

- `POST /api/instrumentos-hedge` — payload aceita lista `strikes: StrikePorVencimentoDto[]`. Single-strike vira lista de 1 item.
- `GET /api/instrumentos-hedge/{id}` — retorna o hedge com sub-coleção `strikes`.
- `POST /api/instrumentos-hedge/{id}/strikes/{strikeId}/liquidar` — registra liquidação de uma perna.
- `GET /api/instrumentos-hedge/{id}/mtm?dataReferencia=...` — devolve MTM agregado + breakdown por perna.

---

## 7. Critérios de aceite

- [ ] Entidade `StrikePorVencimento` migrada com índices
- [ ] Forward simples antigo (1 strike) migra automaticamente para 1 linha em `StrikePorVencimento`
- [ ] Hedge multi-perna (cenário PO §2.3) criado com 2 strikes e 2 datas distintas
- [ ] MTM calculado por perna e somado corretamente
- [ ] Liquidação de uma perna não fecha o hedge enquanto outras estiverem ativas
- [ ] Liquidação da última perna fecha o `InstrumentoHedge`
- [ ] UI exibe tabela de pernas com totais; permite editar antes de salvar
- [ ] Vínculo com `EventoCronograma` é opcional e mostrado quando preenchido
- [ ] Anexo_A_Valoracao_Divida_NDF.md atualizado com fórmulas multi-perna
- [ ] Cobertura de teste para: Forward 1 perna, Forward N pernas, Collar 1 perna, Collar N pernas

---

## 8. Pontos em aberto

| # | Decisão | Recomendação |
|---|---------|--------------|
| D1 | Permitir Collar e Forward misturados no mesmo hedge? | Não — todas as pernas do mesmo hedge têm o mesmo `Tipo` (campo do `InstrumentoHedge`) |
| D2 | Permitir editar uma perna após liquidação? | Não; somente cancelar via novo evento |
| D3 | Cancelamento parcial (uma perna) com penalidade | Sim — modelar `PenalidadeCancelamento` por perna |
| D4 | Curva forward — onde obter? | (a) B3 (oficial, prazo curto); (b) cotação interna do banco contraparte (preferido); (c) modelo paramétrico (Nelson-Siegel) — manter `ICurvaForwardProvider` plugável |
| D5 | Target forward / KO / KI no futuro | Modelo já permite via `EstruturaExotica` em `InstrumentoHedge` (campo nullable adicional) |

---

## 9. Referências

- `Sgcf.Domain/Hedge/InstrumentoHedge.cs:1-111`
- `plan/Anexo_A_Valoracao_Divida_NDF.md` (precisa de atualização)
- `plan/Anexo_B_Modalidades_e_Modelo_Dados.md:19, 159-160`
- `CONTRATOS_MODELOS/CONTRATO_NDF_ITAU.pdf`
- `4131.md` §2.4 (este pacote)
- ISDA — "FX and Currency Option Definitions" (2005, suplementos 2017)
- BACEN Resolução 4.527/2016 (registro de derivativos)
- B3 — Curva de cupom cambial e forward USD/BRL
