# Frontend Handover — S40 Cotação: Tenor, Campos de Domínio, PTAX Multimoeda e Erros RFC 7807

**Versão backend:** `0.12.0`
**Status:** Entregue e integrado em `main` (as-built) — PR [#7](https://github.com/brgt/sgcf/pull/7).
**Data de entrega:** 2026-06-06
**Contato backend:** Equipe SGCF
**Spec de referência:** `docs/specs/cotacoes/SPEC_S40_TENOR_DOMINIO_PTAX.md`

> **As-built (confirmado contra o código mergeado):**
> - Respostas JSON em **camelCase**; enums serializados como **string** (ex.: `prazoMaximoUnidade: "Meses"`, `severidade: "Aviso"`).
> - `alertas` está **sempre presente** nas respostas (vazio `[]` em leituras; preenchido em POST/PATCH).
> - Em modalidades FX (Finimp/Lei4131), **omitir `moedaAlvo` assume `Usd`** (retrocompatível); envie `moedaAlvo` explicitamente para EUR/JPY/CNY.
> - Suíte completa verde (1697 testes); CET inalterado (GoldenDataset intacto).

---

## Resumo executivo

S40 implementa as mudanças propostas pelo time de front-end, com três adaptações relevantes ao back-end real. O contrato JSON que vocês desenharam foi mantido quase integralmente; as mudanças abaixo refinam pontos que dependiam de premissas internas do back-end que diferiam da implementação atual.

- **Prazo como tenor:** `{ prazoMaximoValor, prazoMaximoUnidade }` com `prazoMaximoDias` canônico derivado.
- **Campos de domínio:** `moedaAlvo`, `carenciaMeses`, `indexadorBase` e estruturantes do FGI.
- **PTAX multimoeda:** novo campo canônico `ptaxUsada`; `ptaxUsadaUsdBrl` torna-se depreciado.
- **Erros RFC 7807:** todos os `409` da área de cotações passam a ser `ProblemDetails`. Esta é a única **breaking change** do pacote.
- **Alertas:** validação suave via array `alertas[]` estruturado na resposta de escrita.

---

## 1. Pontos a corrigir no entendimento

Esta seção esclarece premissas da spec original que não correspondiam ao back-end atual. Nenhuma delas decorre de erro de modelagem de contrato — todas se referem a detalhes internos do back-end que não eram visíveis a partir do front-end.

### 1.1. Multi-tenant não é "por schema"

A spec original (Seção 7) assumiu isolamento multi-tenant por schema, com `search_path` por tenant, DDL bruto executado por schema e tabela `cotacoes` (plural).

- O back-end real usa **schema único `sgcf` + coluna `tenant_id` + Row-Level Security (RLS)**.
- A tabela é `sgcf.cotacao` (singular); a coluna canônica é `prazo_maximo_dias`.
- A migração é **uma única migração EF Core**, não um laço por schema.

**Impacto no front-end:** nenhum. O contrato de API não muda por causa disto. O esclarecimento serve apenas para alinhar o modelo mental e qualquer coordenação de _deploy_/migração.

### 1.2. Base de URI de erro e erros pré-existentes

A spec referenciou erros existentes `garantia-nao-coberta` e `lacuna-grupo-garantia` sob `https://sgcf.nordware.io/errors/`.

- No back-end existia **apenas um** `type` customizado: `https://sgcf.io/errors/garantia-exigida-nao-coberta` (domínio `sgcf.io`, _slug_ diferente).
- `lacuna-grupo-garantia` não existia como `type` próprio.
- Decisão adotada: **novos** `type` URIs usam a base `https://sgcf.nordware.io/errors/` (o módulo será hospedado sob `nordware.io`).

**Impacto no front-end:** usar exatamente os `type` finais listados na Seção 4. O `type` de garantia existente permanece em `sgcf.io` por ora (alinhamento futuro, será comunicado).

### 1.3. O `409` de PTAX atual não é detectável por `detail`/`title`

A spec afirmou que o front-end detecta o erro de PTAX por _substring_ `"ptax"` em `detail` ou `title`.

- O back-end atual **não** retorna `ProblemDetails` nesse caso. Ele retorna um objeto anônimo:

```json
{ "error": "PTAX D-1 não disponível (fechamento 2026-05-15). Cadastre a cotação USD/BRL antes de criar a cotação." }
```

- Ou seja, não há `type`, `title` nem `detail` hoje. Qualquer detecção baseada em `detail`/`title` não estava casando com o corpo real.
- S40 corrige isto definitivamente: o erro passa a ser `ProblemDetails` com `type` estável (Seção 4).

**Impacto no front-end:** migrar a detecção para `err.type === 'https://sgcf.nordware.io/errors/ptax-indisponivel'`.

### 1.4. `moedaAlvo` exigia generalização da PTAX

A spec permite `moedaAlvo ∈ { Usd, Eur, Jpy, Cny }` para modalidades FX, mas o único campo de saída de PTAX era `ptaxUsadaUsdBrl` (específico de USD), e a resolução interna estava fixada em USD.

- S40 generaliza a resolução de PTAX por `moedaAlvo` (decisão aprovada).
- Introduz o campo canônico **`ptaxUsada`** (PTAX D-1 de `moedaAlvo`/BRL).
- **`ptaxUsadaUsdBrl`** passa a ser **depreciado**: preenchido apenas quando `moedaAlvo = "Usd"`, `null` caso contrário.

**Impacto no front-end:** ler `ptaxUsada`, não `ptaxUsadaUsdBrl`.

### 1.5. `alertas` passa a ser estruturado (não texto solto)

A spec previa um array `alertas` para validação suave, sem definir o formato dos itens.

- Decisão adotada (boas práticas de UI/UX): cada alerta é um **objeto estruturado** com `codigo` estável, `campo`, `severidade` e `mensagem`.
- O front-end deve ramificar a lógica por `codigo` (estável), e usar `mensagem` apenas para exibição.

### 1.6. Observações menores

- O enum de moeda reutiliza o tipo existente `Moeda` (`Brl, Usd, Eur, Jpy, Cny`); não há tipo `MoedaCotacao` separado. Os valores são idênticos aos da spec.
- As rotas reais são prefixadas: `POST /api/v1/cotacoes`, `PATCH /api/v1/cotacoes/{id}`, etc.

---

## 2. Breaking change — formato de erro `409` em cotações

### O que mudou

Todos os endpoints de cotações que antes retornavam `409` com corpo `{ "error": "..." }` passam a retornar **`ProblemDetails` (RFC 7807)**.

Antes:

```json
{ "error": "Mensagem do conflito." }
```

Depois:

```json
{
  "type": "https://sgcf.nordware.io/errors/conflito-de-estado",
  "title": "Conflito de estado",
  "status": 409,
  "detail": "Mensagem do conflito."
}
```

### Impacto

| Código atual                     | Código atualizado                          |
| -------------------------------- | ------------------------------------------ |
| `error.response.data.error`      | `error.response.data.detail`               |
| (sem discriminador)              | `error.response.data.type` (estável)       |

Recomenda-se um interceptador único que normalize `ProblemDetails` e ramifique por `type`.

---

## 3. Mudanças de contrato (campos)

### 3.1. Prazo como tenor

Request (`POST`/`PATCH`):

```json
{
  "prazoMaximoValor": 60,
  "prazoMaximoUnidade": "Meses"
}
```

- `prazoMaximoDias` continua aceito como **entrada legada** (depreciado).
- A resposta sempre traz os três campos: `prazoMaximoDias` (canônico, derivado), `prazoMaximoValor`, `prazoMaximoUnidade`.
- Conversão de exibição/teto: `Meses → Dias = valor × 30` (30/360). Não é o day-count do CET.
- Unidade default quando omitida: Finimp/Refinimp = `Dias`; Lei4131/Nce/CapitalDeGiro/Fgi = `Meses`.
- Linhas legadas retornam `prazoMaximoUnidade = "Dias"`, `prazoMaximoValor = prazoMaximoDias`.

### 3.2. `moedaAlvo` e PTAX multimoeda

- `moedaAlvo`: editável em Finimp e Lei4131; herdada do contrato mãe e somente leitura em Refinimp; fixa `"Brl"` em Nce/CapitalDeGiro/Fgi.
- Saída de PTAX: usar **`ptaxUsada`**; `ptaxUsadaUsdBrl` é depreciado (espelha `ptaxUsada` somente quando `moedaAlvo = "Usd"`).
- `dataPtaxReferencia` permanece inalterado.

### 3.3. Campos de domínio opcionais

```json
{
  "carenciaMeses": 12,
  "indexadorBase": { "tipo": "Euribor", "spreadAa": 2.75 },
  "percentualCoberturaFgi": 80,
  "finalidadeBndes": "...",
  "bancoRepassadorPretendido": "..."
}
```

- `indexadorBase.tipo` ∈ `{ CdiPercentual, CdiMaisSpread, Prefixado, Tlp, Ipca, Selic, Sofr, Euribor }`.
- `carenciaMeses`: aplicável a Lei4131/Nce/CapitalDeGiro/Fgi; enviado a modalidade não aplicável é ignorado (com alerta suave).
- Estruturantes (`finalidadeBndes`, `bancoRepassadorPretendido`, `percentualCoberturaFgi`): apenas Fgi.

### 3.4. Array de alertas

A resposta de `POST` e `PATCH` traz `alertas: AlertaDto[]` (vazio quando não há alertas).

```json
{
  "alertas": [
    { "codigo": "prazo-fora-da-faixa-esperada", "campo": "prazoMaximoValor", "severidade": "Aviso", "mensagem": "..." }
  ]
}
```

Códigos de alerta inicialmente previstos:

- `prazo-recalculado` — `valor`/`unidade` e `prazoMaximoDias` divergentes; o par estruturado prevaleceu.
- `prazo-fora-da-faixa-esperada` — prazo acima da faixa típica da modalidade (não bloqueante).
- `indexador-incoerente` — `tipo` informado sem o campo numérico coerente.
- `carencia-ignorada` — `carenciaMeses` enviado a modalidade não aplicável.
- `moeda-herdada-do-contrato-mae` — `moedaAlvo` divergente enviada em Refinimp; valor do contrato mãe prevaleceu.

---

## 4. Erros para tratar

| `type`                                                  | HTTP | Quando ocorre                                  | Extensões úteis                          |
| ------------------------------------------------------- | ---- | ---------------------------------------------- | ---------------------------------------- |
| `https://sgcf.nordware.io/errors/ptax-indisponivel`     | 409  | PTAX D-1 ausente para `moedaAlvo`/data         | `dataPtaxReferencia`, `moedaAlvo`        |
| `https://sgcf.nordware.io/errors/conflito-de-estado`    | 409  | Transição/edição inválida (ex.: fora de Rascunho) | —                                     |
| `https://sgcf.nordware.io/errors/validacao`             | 400  | Validação de entrada                           | `errors` (mapa campo → mensagens)        |
| `https://sgcf.nordware.io/errors/nao-encontrado`        | 404  | Cotação inexistente                            | —                                        |

Exemplo do `409` de PTAX:

```json
{
  "type": "https://sgcf.nordware.io/errors/ptax-indisponivel",
  "title": "PTAX indisponível",
  "status": 409,
  "detail": "PTAX D-1 não disponível para a moeda e a data de referência informadas.",
  "dataPtaxReferencia": "2026-06-05",
  "moedaAlvo": "Eur"
}
```

Validações que retornam **`400`** (bloqueantes, não alertas):

- `prazoMaximoValor < 1`, não inteiro, ou `prazoMaximoUnidade` fora de `{Dias, Meses}`.
- `POST` sem nenhum campo de prazo.
- `moedaAlvo` diferente de `"Brl"` em Nce/CapitalDeGiro/Fgi.
- `carenciaMeses < 0`.
- `percentualCoberturaFgi` fora de `0..100`.

---

## 5. Tipos TypeScript (sugestão)

```ts
type UnidadePrazo = 'Dias' | 'Meses';
type Moeda = 'Brl' | 'Usd' | 'Eur' | 'Jpy' | 'Cny';
type TipoIndexador =
  | 'CdiPercentual' | 'CdiMaisSpread' | 'Prefixado' | 'Tlp'
  | 'Ipca' | 'Selic' | 'Sofr' | 'Euribor';
type SeveridadeAlerta = 'Info' | 'Aviso';

interface IndexadorBase {
  tipo?: TipoIndexador;
  percentualCdi?: number;
  spreadAa?: number;
  taxaPrefixadaAa?: number;
}

interface AlertaDto {
  codigo: string;
  campo: string;
  severidade: SeveridadeAlerta;
  mensagem: string;
}

interface CotacaoDto {
  // ...campos existentes...
  prazoMaximoDias: number;          // canônico (derivado)
  prazoMaximoValor: number;         // novo
  prazoMaximoUnidade: UnidadePrazo; // novo
  moedaAlvo: Moeda;                 // novo
  carenciaMeses?: number | null;    // novo
  indexadorBase?: IndexadorBase | null; // novo
  finalidadeBndes?: string | null;       // novo (Fgi)
  bancoRepassadorPretendido?: string | null; // novo (Fgi)
  percentualCoberturaFgi?: number | null;    // novo (Fgi)
  ptaxUsada?: number | null;        // novo (canônico)
  /** @deprecated usar ptaxUsada */
  ptaxUsadaUsdBrl?: number | null;
  alertas: AlertaDto[];             // presente em respostas de escrita; [] quando vazio
}

// Erro de PTAX (extensões RFC 7807)
interface PtaxIndisponivelProblem {
  type: 'https://sgcf.nordware.io/errors/ptax-indisponivel';
  title: string;
  status: 409;
  detail: string;
  dataPtaxReferencia: string; // YYYY-MM-DD
  moedaAlvo: Moeda;
}
```

---

## 6. Decisões sobre as perguntas em aberto (Seção 10 da spec original)

- **Modelagem de `indexadorBase`:** colunas planas no back-end; o contrato JSON permanece o objeto aninhado. Sem impacto no front-end.
- **Listas FGI/BNDES:** `finalidadeBndes` e `bancoRepassadorPretendido` aceitos como **string livre** com validação suave. Promoção a enum será comunicada quando a lista oficial existir.
- **Tetos de prazo:** sem teto rígido; apenas alerta suave (`prazo-fora-da-faixa-esperada`).
- **`carenciaMeses` em modalidade não aplicável:** ignorado, com alerta `carencia-ignorada` (não bloqueia).
- **Campos de fase posterior** (`percentualRefinanciado`, `nceNumero`, `bancoMandatario`, `paisCredor` na cotação): permanecem fora de escopo de S40 (coletados na conversão).

---

## 7. Retrocompatibilidade

- Clientes que enviam apenas `prazoMaximoDias` continuam funcionando (back-end infere `unidade='Dias'`).
- Clientes que ainda leem `ptaxUsadaUsdBrl` continuam funcionando para `moedaAlvo = "Usd"`.
- Campos novos ausentes não quebram a desserialização.
- A única mudança que exige ação imediata é o formato dos erros `409` (Seção 2).

---

## 8. Checklist de integração (front-end)

- [ ] Migrar leitura de erro `409` de `data.error` para `data.detail` + `data.type`.
- [ ] Detectar PTAX por `type === 'https://sgcf.nordware.io/errors/ptax-indisponivel'` (usar `moedaAlvo`/`dataPtaxReferencia` na mensagem ao operador).
- [ ] Enviar `{ prazoMaximoValor, prazoMaximoUnidade }`; manter exibição na unidade da modalidade.
- [ ] Ler `prazoMaximoDias` apenas como valor canônico/comparável (não para day-count do CET).
- [ ] Substituir leitura de `ptaxUsadaUsdBrl` por `ptaxUsada`.
- [ ] Enviar `moedaAlvo` nas modalidades FX; tratar Refinimp como somente leitura (herdada do mãe).
- [ ] Renderizar `alertas[]` de forma não bloqueante, ramificando por `codigo`.
- [ ] Tratar os `400` da Seção 4 com mensagens de campo.

---

## 9. Dúvidas

Encaminhar dúvidas de contrato à Equipe SGCF, referenciando a spec `SPEC_S40_TENOR_DOMINIO_PTAX.md`. Para questões de segurança ou conformidade de dados, contatar `security@nordware.io`.
