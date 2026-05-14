# FGI — Fundo Garantidor para Investimentos (BNDES)

**Perguntas do PO consolidadas neste documento**:
- Q1 — Vencimento em data fixa por mês (cronograma mensal)
- Q7 — Taxa pós-fixada CDI + spread
- Q8 — Política de liberação de garantia (FGI: somente na quitação total)

> Para detalhes transversais, consultar:
> - `00_DiasUteis_Calendario.md` — ajuste de data fixa quando recai em feriado/fim-de-semana
> - `00_Cronograma_Estrutura.md` — Strategy PRICE/SAC com data fixa do mês
> - `00_TaxasPosFixadas_Indexadores.md` — CDI + spread, importação BACEN-SGS
> - `00_LiberacaoGarantia.md` — política `SomenteNaQuitacaoTotal`

---

## 1. Resumo executivo

O FGI (Fundo Garantidor para Investimentos) é um fundo do BNDES que oferece garantia complementar a operações de crédito a micro, pequenas e médias empresas, reduzindo o risco para o banco financiador. Operacionalmente para o SGCF, um contrato FGI:

- É denominado em BRL.
- Tem prazos longos (24 a 96 meses, comumente 60).
- Periodicidade **mensal** com **data fixa do mês** (ex.: todo dia 15).
- Taxa quase sempre **pós-fixada**: percentual do CDI + spread fixo, plus taxa FGI (cobertura).
- Garantias compostas: aval FGI (até 80% do principal) + aval pessoal dos sócios (20%) + eventuais outras (alienação, fiança).
- Liberação de garantia: **não há liberação parcial proporcional**; ocorre na quitação total ou execução de aval.

A combinação cronograma-mensal + taxa-pós-fixada + garantia-composta é o que torna o FGI distinto de FINIMP e 4131.

---

## 2. Cenário de negócio

### 2.1 Estrutura típica

| Item | Valor / Comportamento |
|------|------------------------|
| Moeda | BRL |
| Prazos | 24, 36, 48, 60, 84, 96 meses (60 é o mais frequente em PME) |
| Periodicidade | Mensal, **data fixa do mês** (5, 10, 15, 20 ou 25 comuns) |
| Estrutura | Price (mais frequente) ou SAC |
| Taxa | Pós-fixada: percentual do CDI (100% a 130%) + spread (2% a 8% a.a.); pode ter taxa FGI (0,1% a 1% a.a. paga ao Fundo) |
| Carência | 0 a 12 meses (durante a carência paga só juros, principal entra após) |
| Garantia FGI | Cobertura de 40%, 60% ou 80% do principal (configurável) |
| Garantia complementar | Aval dos sócios, alienação fiduciária de bens, recebíveis |
| Liberação | Só na quitação total ou execução |

### 2.2 Pergunta 1 — Cronograma mensal com data fixa

**Cenário PO**: FGI com vencimento dia 15 de cada mês.

- Contratação 14/05/2026 (quinta-feira).
- Primeira parcela: 15/06/2026 (segunda-feira).
- 60 parcelas mensais, todas no dia 15.
- Quando 15 cai em sábado/domingo/feriado: aplica `Following` → próximo dia útil.

Mapeamento para `Contrato`:
- `Periodicidade = Mensal`
- `EstruturaAmortizacao = Price` (ou `Sac` conforme contratação)
- `DataPrimeiroVencimento = 15/06/2026`
- `QuantidadeParcelas = 60`
- `AnchorDiaMes = DiaFixo`
- `AnchorDiaFixo = 15`
- `ConvencaoDataNaoUtil = Following`

**Regra de capping em meses curtos**: se `AnchorDiaFixo = 31`, em fevereiro usa dia 28 (ou 29 em ano bissexto). Decisão em `00_Cronograma_Estrutura.md` §9 D1.

### 2.3 Pergunta 7 — Taxa pós-fixada CDI + spread

**Exemplo concreto** (`CONTRATOS_MODELOS/CONTRATO_FGI_BANCO_BV.pdf`):
- Indexador: CDI
- Percentual: 100%
- Spread: 5,24% a.a.
- Taxa FGI (cobrada pelo BNDES): 0,30% a.a. (incide adicionalmente)

Composição diária (base 252):
```
taxa_efetiva_dia(d) = ((1 + 1.00 × cdi_aa(d))^(1/252) − 1)
                    + ((1 + 0.0524)^(1/252) − 1)
                    + ((1 + 0.0030)^(1/252) − 1)
```

Com CDI corrente de 11,65% a.a. (2026 hipotético), taxa efetiva nominal ≈ 17,38% a.a. — bate com a referência informal "16% a 17% para FGI BV".

**Implementação**: ver `00_TaxasPosFixadas_Indexadores.md`. Para FGI, sugere-se modelar a `TaxaFGI` como **componente separado** do `SpreadAnual`, para fins de relatórios (BNDES audita a parcela cobrada pelo Fundo).

### 2.4 Pergunta 8 — Política de liberação

Para FGI, recomenda-se `Garantia.PoliticaLiberacao = SomenteNaQuitacaoTotal` para **todas as garantias** vinculadas (aval FGI, aval pessoal, alienações):

| Garantia | `PercentualExigido` | `PoliticaLiberacao` |
|----------|----------------------|----------------------|
| Aval FGI | 0.80 (ou 0.40 / 0.60) | `SomenteNaQuitacaoTotal` |
| Aval pessoal sócios | 0.20 (complementar) | `SomenteNaQuitacaoTotal` |
| Alienação fiduciária (se houver) | conforme | `SomenteNaQuitacaoTotal` |

Quando o último pagamento zera o saldo, o sistema gera N `EventoLiberacaoGarantia` (1 por garantia), todos com `Origem = QuitacaoTotal`. Operador valida e marca `Executado` conforme retorno do banco/BNDES.

**Exceção**: em alguns produtos FGI Bovespa há "amortização extraordinária" que reduz cobertura — modelo deve permitir override pontual para `AutomaticaProporcional` se necessário.

### 2.5 Carência

Algumas operações FGI têm carência (geralmente 6 ou 12 meses). Durante a carência:
- Eventos JUROS são gerados mensalmente (igual ao período pós-carência).
- Eventos PRINCIPAL **não são gerados** durante carência; primeiro PRINCIPAL ocorre no mês N+1 da carência.

Mapeamento sugerido:
- Campo novo no `FgiDetail`: `CarenciaMeses` (default 0).
- Strategy `Price`/`Sac` recebe `CarenciaMeses` e ajusta cálculo da parcela e cronograma.

---

## 3. Estado atual no sistema

| Aspecto | Estado |
|---------|--------|
| Modalidade `FGI` cadastrada | Esperada |
| Entidade `FgiDetail` polimórfica | Esperada (Anexo_B §3 + 8.x para garantia FGI) |
| `EventoCronograma` mensal | Estruturalmente cobre |
| `AnchorDiaMes` / `AnchorDiaFixo` | **NÃO existe** (gap transversal — `00_Cronograma`) |
| Taxa pós-fixada CDI + spread | **NÃO existe** entidade `Indexador` (gap transversal — `00_TaxasPosFixadas`) |
| `TaxaFGI` separada | Esperada como campo de `FgiDetail`, valor único; não há vínculo conceitual com `Indexador` |
| Política `SomenteNaQuitacaoTotal` | **NÃO existe** (gap transversal — `00_LiberacaoGarantia`) |
| Carência | **NÃO existe** campo |
| Cobertura BNDES (40/60/80%) | Campo esperado em `FgiDetail` (`PercentualCobertura`) |

---

## 4. GAPs específicos da modalidade

| # | GAP FGI | Severidade |
|---|---------|-----------|
| F1 | `CarenciaMeses` em `FgiDetail` | Alta |
| F2 | Strategy `Price`/`Sac` com carência | Alta |
| F3 | Cálculo CDI + spread + taxa FGI (composição tripla) | Alta |
| F4 | Tratamento de "aval FGI 80%" + "aval pessoal 20%" como duas `Garantia` que somam 100% | Média |
| F5 | Relatórios para BNDES com `TaxaFGI` separada | Média |
| F6 | Identificador único da garantia FGI (número do contrato no BNDES) | Média |
| F7 | Recálculo de cronograma quando CDI sobe e parcela passa de teto contratual | Média |
| F8 | Suporte a amortização extraordinária com recálculo das parcelas restantes | Média |
| F9 | Multa por inadimplemento típica do FGI (2% sobre saldo + juros mora) | Baixa |

---

## 5. Proposta consolidada

### 5.1 Campos novos em `FgiDetail`

| Campo | Tipo | Descrição |
|-------|------|-----------|
| `NumeroFgiBndes` | `string(40)` | ID da garantia no BNDES |
| `PercentualCobertura` | `decimal(5,4)` | 0.40, 0.60, 0.80 |
| `TaxaFgiAnual` | `decimal(7,6)` | Taxa cobrada pelo Fundo (% a.a.) |
| `CarenciaMeses` | `int` (0..24) | Período sem amortização de principal |
| `AvalPessoalObrigatorio` | `bool` | Se há aval dos sócios |
| `LimiteCoberturaBRL` | `Money?` | Teto da cobertura quando há |
| `LinhaProduto` | `enum` | `FGI_PEAC`, `FGI_PROGER`, `FGI_BNDES_MPME`, `OUTRO` |

### 5.2 Composição da taxa

Reaproveita `00_TaxasPosFixadas_Indexadores.md` com adendo:

| Campo no `Contrato` | Para FGI |
|----------------------|----------|
| `TipoTaxa` | `PosFixada` |
| `IndexadorId` | CDI |
| `PercentualIndexador` | ex.: 1.00 (100% do CDI) |
| `SpreadAnual` | ex.: 0.0524 |
| `FgiDetail.TaxaFgiAnual` | ex.: 0.0030 |

`CalculadorJuros` para FGI compõe três fatores diários (indexador, spread, taxa FGI) e multiplica.

### 5.3 Cronograma com carência (Strategy `PriceComCarencia`)

Pseudocódigo da geração:

```
input: principal, taxaEfetivaMensal, qtdParcelas, carenciaMeses,
       dataPrimeiroVencimento, anchorDiaFixo, convencao

para m de 1 até carenciaMeses:
    dataJ = dataPrimeiroVencimento + (m-1) meses anchorada em anchorDiaFixo
    dataJ = AjustarPorConvencao(dataJ, convencao)
    cria EventoCronograma(tipo=JUROS, dataPrevista=dataJ, valor=principal*taxaEfetivaMensal)

parcelaPrice = PriceFormula(principal, taxaEfetivaMensal, qtdParcelas - carenciaMeses)
para m de carenciaMeses+1 até qtdParcelas:
    dataP = dataPrimeiroVencimento + (m-1) meses anchorada
    dataP = AjustarPorConvencao(dataP, convencao)
    cria EventoCronograma(tipo=PRINCIPAL_E_JUROS, dataPrevista=dataP, valor=parcelaPrice)
    // OU dois eventos separados (PRINCIPAL, JUROS) para auditoria fina
```

Recomendação: gerar **dois eventos separados** (PRINCIPAL + JUROS) no mesmo `NumeroEvento` para conciliar auditoria com pagamentos do banco.

### 5.4 Garantias FGI

No Step 2 do wizard, ao escolher FGI:

```
Garantias da operação
  ┌────────────────────────────────────────────────────────┐
  │ Aval FGI                                                │
  │   Número FGI BNDES: [..........]                        │
  │   % cobertura:      [80%]                              │
  │   Taxa FGI a.a.:    [0,30%]                            │
  │   Política liberação: [SomenteNaQuitacaoTotal] (fixo)  │
  └────────────────────────────────────────────────────────┘
  ┌────────────────────────────────────────────────────────┐
  │ Aval pessoal (sócios) — opcional                       │
  │   % cobertura:      [20%]                              │
  │   Avalistas: [+ Adicionar]                             │
  │   Política liberação: [SomenteNaQuitacaoTotal] (fixo)  │
  └────────────────────────────────────────────────────────┘
```

Sistema valida que `% cobertura aval FGI + % cobertura aval pessoal + outras garantias = 100%`. Se < 100%, alerta.

### 5.5 Recálculo quando CDI excede teto

Alguns contratos têm cláusula de "taxa máxima" (ex.: 25% a.a.). Se CDI + spread excederem, taxa fica travada no teto. Modelar como:

- `Contrato.TaxaMaximaAnual` (nullable).
- `CalculadorJuros` aplica `min(taxa_calculada, TaxaMaximaAnual)`.
- Quando hit do teto, evento `LimiteTaxaAtingido` é registrado para alerta.

### 5.6 Amortização extraordinária

PME pode antecipar parcelas. Conforme `plan/Anexo_C_Regras_Antecipacao_Pagamento.md`:

- Parcelas inteiras apenas (Anexo_C:76).
- Antecipação reduz `QuantidadeParcelas` futuras (mantém valor PRICE) **ou** mantém qtd parcelas e reduz valor (recalcula PRICE) — **escolha do tomador**.
- Sistema gera nova versão de cronograma; versão anterior fica para auditoria.

---

## 6. Impacto no wizard

- Step 0: card `FGI`.
- Step 1: comuns + Periodicidade `Mensal` (default) + `AnchorDiaMes = DiaFixo` (default) + `AnchorDiaFixo` editável + `TipoTaxa = PosFixada` (default).
- Step 2 (DetalheFgi): número FGI BNDES, % cobertura, taxa FGI, linha do produto, carência, avalistas, política de garantia (read-only `SomenteNaQuitacaoTotal`), taxa máxima opcional.
- Step 3 (preview): mostra parcela PRICE estimada + composição (CDI corrente projetado + spread + taxa FGI) — operador valida.

---

## 7. Critérios de aceite

- [ ] FGI 60 meses, PRICE, data fixa dia 15: cronograma de 60 parcelas, datas ajustadas (ex.: 15/11/2026 é domingo → 16/11/2026)
- [ ] FGI com carência de 6 meses: 6 JUROS sem PRINCIPAL + 54 PRINCIPAL+JUROS
- [ ] Cálculo bate com exemplo FGI BV §2.3 (100% CDI + 5,24% + 0,30% taxa FGI)
- [ ] Aval FGI 80% + aval pessoal 20% gerados como 2 garantias, ambas `SomenteNaQuitacaoTotal`
- [ ] Quitação total dispara `EventoLiberacaoGarantia` para ambas
- [ ] Recálculo automático quando CDI atualiza diariamente; juros provisionados refletem na próxima parcela
- [ ] Taxa máxima trava a composição quando atingida; evento de alerta registrado
- [ ] Antecipação de parcela: usuário escolhe "reduzir qtd" ou "reduzir valor"; nova versão de cronograma criada
- [ ] Relatório BNDES mensal lista taxa FGI cobrada e cobertura vigente

---

## 8. Pontos em aberto

| # | Decisão | Recomendação |
|---|---------|--------------|
| D1 | TaxaFGI é cobrada por dentro da parcela ou em boleto separado? | Configurável por contrato; default = "por dentro" (compõe a parcela) |
| D2 | Multi-aval pessoal: tratar cada avalista como `Garantia` ou agrupar? | Agrupar em uma `Garantia.Tipo = AVAL` com sub-lista de `Avalista` no detalhe |
| D3 | Linha de produto BNDES afeta cálculo? | Apenas para relatórios; cálculo é o mesmo |
| D4 | Capping em mês com 28/29/30 dias quando `AnchorDiaFixo = 31` | Capping no último dia útil do mês (recomendado) |
| D5 | Mora em atraso: 2% multa + 1%/mês juros + correção; modelar agora? | Fora do escopo deste doc — tratar em "Cobrança" futuramente |

---

## 9. Referências

- `CONTRATOS_MODELOS/CONTRATO_FGI_BANCO_BV.pdf`
- `plan/Anexo_B_Modalidades_e_Modelo_Dados.md:18, 742-766`
- `plan/Anexo_C_Regras_Antecipacao_Pagamento.md:72-76`
- `00_Cronograma_Estrutura.md`, `00_TaxasPosFixadas_Indexadores.md`, `00_LiberacaoGarantia.md`, `00_DiasUteis_Calendario.md`
- BNDES — Regulamento Geral FGI
- BNDES — Linhas FGI PEAC, FGI PROGER, FGI BNDES MPME
- Resolução BNDES 4.881/2020
