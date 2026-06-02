# Guia do Front-end — Cotações e Parâmetros de Cotação

> **Público:** time de front-end do SGCF.
> **Versão:** 2026-06-02
> **Objetivo:** esclarecer o erro 400 em `parametros-cotacao`, a diferença entre **configurar o tipo** de cotação e **cadastrar a taxa**, e como o front deve consultar/alimentar o câmbio.

---

## TL;DR

- `parametros-cotacao` **NÃO** cadastra a taxa USD/BRL. Ele só configura **qual tipo** de cotação (PtaxD0, PtaxD1, SpotIntraday, Fixing) o sistema usa por banco/modalidade.
- A **taxa** vem da ingestão automática do BCB (job) ou do endpoint admin `POST /cotacoes-fx`.
- O 400 que o time vê ao "cadastrar cotação" em `parametros-cotacao` quase sempre é o campo `tipoCotacao` ausente/inválido — porque esse endpoint espera um **tipo do enum**, não um valor de taxa.

---

## 1. `parametros-cotacao` configura o TIPO, não a taxa

O recurso `parametros-cotacao` define **qual regra de tipo de cotação** aplicar ao converter moeda estrangeira para BRL, por **banco** e/ou **modalidade**. Ele responde à pergunta *"para o banco X na modalidade Y, uso PTAX D0, D-1, spot intraday ou fixing?"* — e **não** *"quanto vale o dólar hoje?"*.

A resolução de tipo é hierárquica (do mais específico para o mais genérico):

1. (banco + modalidade) específico
2. banco (qualquer modalidade)
3. modalidade (qualquer banco)
4. global (fallback)

### Contrato — `POST /api/v1/parametros-cotacao` (policy **Admin**)

```json
{
  "bancoId": "00000000-0000-0000-0000-000000000000",
  "modalidade": "Finimp",
  "tipoCotacao": "PtaxD1"
}
```

- `bancoId` — opcional (`null` = aplica a qualquer banco).
- `modalidade` — opcional (`null` = aplica a qualquer modalidade). Valores: `Finimp, Refinimp, Lei4131, Nce, CapitalDeGiro, Fgi`.
- `tipoCotacao` — **obrigatório**, deve ser um valor do enum (ver §2).

### Causa do 400 relatado

O endpoint valida `tipoCotacao` contra o enum. Se o front enviar:

- `tipoCotacao` ausente, vazio, ou um valor fora do enum (por exemplo, mandando uma **taxa numérica** ou um texto livre), **ou**
- `modalidade` com um valor que não está no enum,

o backend responde **400 Bad Request** com mensagem explícita, por exemplo:

```
TipoCotacao deve ser um dos valores: PtaxD0, PtaxD1, SpotIntraday, Fixing.
```

> **Importante:** o backend está **correto** — a validação não será afrouxada para aceitar valores fora do enum. O front deve enviar um dos tipos válidos. Se a intenção era cadastrar a **taxa**, use o §3.

---

## 2. Valores válidos de `TipoCotacao`

| Valor          | Significado |
|----------------|-------------|
| `PtaxD0`       | Boletim de **fechamento** do dia (o que o ingestor do BCB realmente grava). |
| `PtaxD1`       | Tipo **lógico**: fechamento de **D-1**. O sistema traduz internamente para o `PtaxD0` do dia anterior à referência. |
| `SpotIntraday` | Cotação spot intradiária (cache Redis), quando disponível. |
| `Fixing`       | Cotação de fixing. |

Envie sempre uma destas strings (case-insensitive) no campo `tipoCotacao`. Qualquer outro valor → **400**.

> **Nota técnica:** `PtaxD1` é um tipo lógico. Como o ingestor do BCB grava apenas `PtaxD0`, o backend resolve `PtaxD1` consultando o `PtaxD0` do dia útil anterior à data de referência. O front não precisa se preocupar com isso — basta escolher o tipo desejado.

---

## 3. Como cadastrar a TAXA USD/BRL

A taxa cambial não é cadastrada via `parametros-cotacao`. Há duas formas de alimentá-la:

### 3.1 Ingestão automática (padrão em produção)

O job `IngestaoPtaxJob` consome a API PTAX do BCB a cada 15 minutos e grava `PtaxD0` e `SpotIntraday` para USD, EUR, JPY e CNY. Em operação normal, **o front não precisa fazer nada** — a taxa já estará disponível.

### 3.2 Cadastro manual — `POST /api/v1/cotacoes-fx` (policy **Admin**)

Para contingência (BCB indisponível) ou correção, use o endpoint admin de cadastro manual. Ele grava preferencialmente `PtaxD0` (coerente com o ingestor) e é **idempotente** pela chave `(moedaBase, moedaQuote, momento, tipo)`.

**Request:**

```json
{
  "moedaBase": "Usd",
  "moedaQuote": "Brl",
  "momento": "2026-05-15T20:00:00Z",
  "tipo": "PtaxD0",
  "valorCompra": 5.15,
  "valorVenda": 5.20,
  "fonte": "MANUAL"
}
```

Campos com **default** (podem ser omitidos): `moedaQuote` = `Brl`, `tipo` = `PtaxD0`, `fonte` = `MANUAL`.

**Validação (400 Bad Request) se:**

- `valorCompra` ou `valorVenda` ≤ 0;
- `moedaQuote` ≠ `Brl`;
- `moedaBase` = `Brl` ou fora do enum de moedas;
- `tipo` fora do enum de `TipoCotacao`;
- `momento` no **futuro**.

**Respostas:**

- `201 Created` — cotação registrada (corpo = a cotação cadastrada).
- `403 Forbidden` — usuário sem role `admin`.
- `400 Bad Request` — payload inválido (ver acima).

**Conferência — `GET /api/v1/cotacoes-fx?moeda=Usd&tipo=PtaxD0&ate=2026-05-15` (policy Admin):**

- `200 OK` com a cotação mais recente para (moeda, tipo) até a data informada.
- `404 Not Found` se não houver cotação.

> Após cadastrar a taxa (3.1 ou 3.2), os fluxos cambiais (criar cotação, registrar proposta, painéis, refresh) funcionam normalmente — não é necessário inserir dados via SQL.

---

## 4. Consultar qual tipo está configurado

Para descobrir **qual tipo** de cotação o sistema aplicará a um banco/modalidade:

### `GET /api/v1/parametros-cotacao/resolve?bancoId={guid}&modalidade={modalidade}` (policy Leitura)

**Resposta `200 OK`:**

```json
{ "tipoCotacao": "PtaxD1" }
```

**`400 Bad Request`** se `modalidade` for inválida (fora do enum).

Endpoints auxiliares de leitura (policy Leitura):

- `GET /api/v1/parametros-cotacao` — lista todos os parâmetros configurados.
- `GET /api/v1/parametros-cotacao/{id}` — detalha um parâmetro.

---

## 5. Fluxo recomendado para o front

1. **Garantir a taxa:** confirmar que a ingestão automática está populando o câmbio (ou cadastrar manualmente via `POST /cotacoes-fx` em contingência — perfil admin).
2. **Configurar o tipo (uma vez, admin):** `POST /parametros-cotacao` com o `tipoCotacao` desejado por banco/modalidade.
3. **Conferir a configuração:** `GET /parametros-cotacao/resolve?bancoId=…&modalidade=…`.
4. **Operar:** criar cotação, registrar propostas e abrir painéis — o backend resolve a taxa automaticamente a partir do tipo configurado.

> **Erro comum a evitar:** tentar enviar a **taxa** (ex.: `5.20`) no campo `tipoCotacao` de `parametros-cotacao`. Isso retorna 400. A taxa vai no `POST /cotacoes-fx` (§3.2); o `parametros-cotacao` recebe apenas o **tipo** (§2).
