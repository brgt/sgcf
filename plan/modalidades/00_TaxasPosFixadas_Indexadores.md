# 00 — Taxas Pós-Fixadas e Indexadores (CDI, SELIC, IPCA, SOFR)

**Pergunta do PO atendida**: Q7 — *"Para contratos FGI pós-fixada a Taxa de Juros é a CDI + o Spread. Como o sistema prevê este tipo de situação?"*

**Resposta direta**: **Não prevê.** Hoje o cálculo de juros usa apenas um `Percentual` fixo e `BaseCalculo` (Du252, etc.). Não existem entidades `Indexador` nem `CotacaoIndexador`. Este documento descreve o modelo necessário.

---

## 1. Resumo executivo

Contratos pós-fixados (FGI, parte de 4131, alguns FINIMP) usam uma taxa **composta de duas partes**: (a) percentual sobre um indexador de mercado (CDI, SELIC, IPCA, SOFR) e (b) um spread fixo anual. A taxa efetiva varia dia a dia conforme a cotação do indexador. O sistema precisa:

1. Modelar `Indexador` (catálogo) e `CotacaoIndexador` (série temporal diária).
2. Adicionar ao `Contrato` os campos `TipoTaxa`, `IndexadorId`, `PercentualIndexador`, `SpreadAnual`.
3. Atualizar `CalculadorJuros` para resolver a taxa do dia em função do indexador.
4. Integrar com a API BACEN-SGS para alimentação automática diária.

---

## 2. Cenário de negócio

### 2.1 Estrutura típica de uma taxa pós-fixada brasileira

```
taxa_efetiva = percentual_indexador × taxa_indexador + spread_aa
```

Exemplos reais:

| Contrato | Estrutura nominal | Componentes |
|----------|-------------------|-------------|
| FGI BV | "100% CDI + 5,24% a.a." | indexador=CDI, percentual=1,00, spread=5,24% |
| FGI BB | "120% CDI" | indexador=CDI, percentual=1,20, spread=0 |
| 4131 Santander | "SOFR + 3,5% a.a." | indexador=SOFR, percentual=1,00, spread=3,5% |
| Capital giro BNDES | "TLP + 4% a.a." | indexador=TLP, percentual=1,00, spread=4,0% |
| Imobiliário | "IPCA + 9% a.a." | indexador=IPCA, percentual=1,00, spread=9,0% (estrutura híbrida) |

### 2.2 Indexadores comuns no Brasil

| Indexador | Fonte oficial | Série BACEN SGS | Composição | Base anual |
|-----------|---------------|-----------------|------------|------------|
| **CDI** | CETIP/B3 | 12 | Geométrica diária | 252 dias úteis |
| **SELIC** (meta) | COPOM | 4189 | Anual nominal | 252 |
| **SELIC** (efetiva diária) | BACEN | 1178 | Geométrica diária | 252 |
| **IPCA** | IBGE | 433 | Acumulada mensal | 365 |
| **IGP-M** | FGV | 189 | Acumulada mensal | 365 |
| **TLP** | BNDES/BACEN | 27572 | Combina IPCA + juros real | 365 |
| **SOFR** | Fed NY | externa (NY Fed API) | Geométrica diária | 360 |
| **LIBOR USD** | descontinuada 2023 | — | — | 360 (legado) |

### 2.3 Capitalização

- **CDI/SELIC**: capitalização geométrica diária em dias úteis → usar **base 252**.
- **IPCA/IGP-M**: aplicação mensal (variação acumulada do mês) → base 365 / aniversário mensal.
- **SOFR**: capitalização diária em dias corridos → base 360.

A fórmula de juros para um dia útil em base 252 com CDI:

```
fator_dia = (1 + percentual_indexador × cdi_dia)^(1/252) − 1
fator_spread_dia = (1 + spread_aa)^(1/252) − 1
taxa_efetiva_dia = (1 + fator_dia)(1 + fator_spread_dia) − 1
```

Para período de N dias úteis: produto dos fatores diários.

### 2.4 Histórico e atualização

- BACEN publica CDI/SELIC com defasagem de 1 dia útil (D+1).
- IPCA é publicado mensalmente (em torno do dia 10 do mês seguinte).
- Sistema precisa lidar com "indexador ainda não publicado" → usar último valor disponível como provisão, recalcular quando publicado.

---

## 3. Estado atual no sistema

### 3.1 O que existe

- `Sgcf.Domain/Calculos/CalculadorJuros.cs` (inferido) — recebe `taxaAnual: Percentual`, `baseCalculo: BaseCalculo { Du252, Dias360, Dias365 }`, `dias: int` e calcula juros.
- Campo `BaseCalculo` no contrato.

### 3.2 O que NÃO existe

- Nenhuma entidade `Indexador`.
- Nenhuma tabela `CotacaoIndexador` / série temporal.
- Nenhum campo `TipoTaxa`, `PercentualIndexador`, `SpreadAnual` no `Contrato`.
- Nenhuma integração com BACEN-SGS.
- Nenhum mecanismo de provisão (juros provisionados com cotação parcial / projetada).

---

## 4. GAPs identificados

| # | GAP | Severidade | Quem afeta |
|---|-----|-----------|-----------|
| G1 | Não existe catálogo de indexadores | Alta | FGI, 4131 SOFR, contratos longos |
| G2 | Não existe série histórica de cotações | Alta | FGI, todos pós-fixados |
| G3 | `Contrato` não distingue taxa fixa de pós-fixada | Alta | FGI |
| G4 | `CalculadorJuros` não compõe percentual × indexador + spread | Alta | FGI |
| G5 | Sem integração BACEN-SGS para alimentação diária | Alta | Operação |
| G6 | Sem mecanismo de provisão para período com cotação não publicada | Média | Relatórios em tempo real |
| G7 | Sem suporte a indexadores estrangeiros (SOFR) | Média | 4131 |
| G8 | Sem versionamento de fonte (cotação revisada pelo BACEN) | Baixa | Auditoria |

---

## 5. Proposta de estrutura

### 5.1 Catálogo de indexadores

**Nova tabela `indexador`**

| Campo | Tipo | Descrição |
|-------|------|-----------|
| `Id` | `Guid` | PK |
| `Codigo` | `string(20)` único | CDI, SELIC, IPCA, IGPM, TLP, SOFR, LIBOR |
| `Nome` | `string(120)` | "Certificado de Depósito Interbancário" |
| `Moeda` | `enum` | BRL, USD, EUR |
| `Fonte` | `enum FonteIndexador` | BACEN_SGS, IBGE, FED_NY, FGV, BNDES, MANUAL |
| `CodigoFonte` | `string(40)` | Ex.: "12" para BACEN-SGS CDI |
| `Composicao` | `enum` | GeometricaDiariaDU, GeometricaDiariaDC, AcumuladaMensal |
| `BaseAnual` | `int` | 252, 360, 365 |
| `Ativo` | `bool` | Permite descontinuar (LIBOR) |

### 5.2 Série temporal

**Nova tabela `cotacao_indexador`**

| Campo | Tipo | Descrição |
|-------|------|-----------|
| `Id` | `Guid` | PK |
| `IndexadorId` | `Guid` | FK |
| `Data` | `LocalDate` | Data de referência |
| `Taxa` | `decimal(10,8)` | Taxa anualizada (% a.a.) — ex.: 11.65 |
| `FatorDiario` | `decimal(18,12)?` | Pré-calculado para evitar recálculo ((1+taxa)^(1/base) − 1) |
| `Origem` | `enum` | IMPORT_API, IMPORT_MANUAL, PROJECAO |
| `Revisado` | `bool` | True se a cotação foi alterada após publicação inicial |
| `ImportadoEm` | `Instant` | Auditoria |

Índice único: `(IndexadorId, Data)`.

### 5.3 Novos campos no `Contrato`

| Campo | Tipo | Obrigatório | Descrição |
|-------|------|-------------|-----------|
| `TipoTaxa` | `enum TipoTaxa { Fixa, PosFixada, Hibrida }` | Sim | — |
| `IndexadorId` | `Guid?` | Condicional (não-fixa) | FK para `indexador` |
| `PercentualIndexador` | `decimal(8,6)` | Condicional | Ex.: 1.0 (100%), 1.2 (120%) |
| `SpreadAnual` | `decimal(8,6)` | Condicional | % a.a. (ex.: 5.24) |
| `BaseCalculo` (já existe) | `enum` | Sim | Mantém atual; default vem do `Indexador.BaseAnual` |

Quando `TipoTaxa = Fixa`: `IndexadorId = null`, `PercentualIndexador = 0`, `SpreadAnual` armazena a taxa fixa contratada.

Quando `TipoTaxa = Hibrida`: tratamento especial (IPCA + spread real) — ver §5.7.

### 5.4 CalculadorJuros refatorado

```csharp
public class CalculadorJuros(IIndexadorRepository indexadores, IBusinessDayCalendar calendar)
{
    public Money CalcularProvisao(Contrato contrato, LocalDate desde, LocalDate ate)
    {
        if (contrato.TipoTaxa == TipoTaxa.Fixa)
            return CalcularJurosFixos(contrato, desde, ate);

        var cotacoes = indexadores.GetCotacoes(contrato.IndexadorId, desde, ate);
        return AcumularFatoresDiarios(cotacoes, contrato.PercentualIndexador, contrato.SpreadAnual);
    }
}
```

Função pura (testável sem DB):

```csharp
public static decimal ComporFatorDia(decimal taxaIndexadorAA, decimal percentualIndexador, decimal spreadAA, int baseAnual)
{
    var fatorIndexador = Math.Pow(1 + percentualIndexador * taxaIndexadorAA, 1.0 / baseAnual) - 1;
    var fatorSpread   = Math.Pow(1 + spreadAA, 1.0 / baseAnual) - 1;
    return (decimal)((1 + fatorIndexador) * (1 + fatorSpread) - 1);
}
```

### 5.5 Importação BACEN-SGS

- Endpoint público: `https://api.bcb.gov.br/dados/serie/bcdata.sgs.{codigoSerie}/dados?formato=json`.
- Job diário (após 09h00 BRT, quando BACEN publica D-1).
- Retorna lista de `{data, valor}`. Sistema persiste em `cotacao_indexador` com `Origem = IMPORT_API`.
- Falha de API → alerta operacional; fallback é usar última cotação como provisão.

### 5.6 Provisão e revisão

Quando consulta de juros é feita para período onde a cotação D não foi publicada:
- Usa `Origem = PROJECAO` com valor da última cotação publicada.
- Quando cotação real chega, atualiza `Origem = IMPORT_API` e dispara evento `CotacaoRevisada`.
- Relatórios marcam linhas com cotação projetada.

### 5.7 Suporte a Híbridas (IPCA + spread)

Diferente de CDI+spread, na híbrida IPCA atua **sobre o saldo principal** (correção monetária) e o spread incide sobre o saldo corrigido. Modelar:
- `TipoTaxa = Hibrida` aciona Strategy distinta.
- Saldo é "corrigido" mensalmente pelo IPCA do mês anterior.
- Juros do mês = saldo_corrigido × ((1+spread)^(1/365) − 1)^dias_corridos.

Esta strategy é necessária para 4131 atrelados a IPCA (raros mas existem) e para futuro suporte a financiamento imobiliário.

---

## 6. Impacto no wizard

- Step 1: ao escolher `TipoTaxa`, UI revela campos:
  - `Fixa`: pede só `SpreadAnual` (renomeado para "Taxa a.a." na UI).
  - `PosFixada`: pede `Indexador` (dropdown do catálogo), `Percentual do indexador` (default 100%), `Spread a.a.`.
  - `Hibrida`: idem pós-fixada, com aviso explicativo.
- Step 3 (revisão): mostrar **taxa efetiva projetada** com cotação corrente do indexador, para o usuário validar.

---

## 7. Impacto em APIs

- `GET /api/indexadores` — catálogo.
- `GET /api/indexadores/{codigo}/cotacoes?desde=...&ate=...` — série temporal.
- `POST /api/indexadores/{codigo}/cotacoes/importar` — admin, importa CSV ou dispara job BACEN.
- `GET /api/contratos/{id}/taxa-efetiva?data=...` — devolve taxa efetiva do dia.
- Payload de criação de contrato muda conforme §5.3.

---

## 8. Critérios de aceite

- [ ] Tabelas `indexador` e `cotacao_indexador` criadas com seed inicial (CDI, SELIC, IPCA, SOFR)
- [ ] Job BACEN-SGS importa CDI e SELIC diariamente (com retry e alerta em falha)
- [ ] `Contrato.TipoTaxa` exposto e validado conforme regras de §5.3
- [ ] `CalculadorJuros` cobre os 3 tipos (fixa, pós-fixada, híbrida) com testes
- [ ] Caso real validado: FGI BV "100% CDI + 5,24%" com 30 dias úteis bate cálculo manual em planilha
- [ ] Cotações marcadas como `PROJECAO` viram `IMPORT_API` quando real chega
- [ ] UI mostra taxa efetiva projetada no Step 3 do wizard
- [ ] Endpoint público de cotações com paginação

---

## 9. Pontos em aberto

| # | Decisão | Recomendação |
|---|---------|--------------|
| D1 | Importar SOFR (sem API pública BACEN) | Manual via planilha mensal ou integração com Fed NY API (gratuita) — recomenda-se Fed NY API |
| D2 | Tratar revisão retroativa de CDI? | Sim, com recálculo do cronograma e nota de auditoria |
| D3 | Permitir contrato com indexador customizado | Não inicialmente; manter catálogo fechado e curado |
| D4 | TLP e TJLP no catálogo | Sim, ambos são relevantes para BNDES e financiamentos longos |

---

## 10. Referências

- BACEN-SGS — `https://www3.bcb.gov.br/sgspub/`
- IBGE IPCA — `https://www.ibge.gov.br/estatisticas/economicas/precos-e-custos.html`
- Resolução BACEN 4.553/2017 (composição de taxas em operações de crédito)
- ISDA — "Rate Calculation Conventions"
- `plan/Anexo_B_Modalidades_e_Modelo_Dados.md` — campos `tipo_taxa`, `indice_referencia`, `spread_aa`
- `plan/Anexo_C_Regras_Antecipacao_Pagamento.md` §7.3 — exemplo FGI BV "100% CDI + 5,24%"
- `00_Cronograma_Estrutura.md` (este pacote) — recálculo de cronograma em reajuste de taxa
