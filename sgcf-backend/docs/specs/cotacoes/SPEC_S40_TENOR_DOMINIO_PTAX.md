# SPEC S40 — Cotação: Prazo como Tenor, Campos de Domínio, PTAX Multimoeda e Erros RFC 7807

> **Status:** Draft para aprovação
> **Data:** 2026-06-06
> **Autor:** Análise técnica (back-end SGCF) a partir da especificação do time de front-end
> **Versão:** v1.0
> **Origem:** Adaptação de `nordware-landing/SPEC_S40_COTACAO_API_BACKEND.md` (contract-first) ao código real do SGCF.
> **Módulos afetados:** Cotações (`Sgcf.Domain.Cotacoes`, `Sgcf.Application.Cotacoes`, `Sgcf.Infrastructure.Persistence`, `Sgcf.Api`).
> **Estratégia:** "Opção B" — preservar campos canônicos (`prazoMaximoDias`, `ptaxUsadaUsdBrl`) e adicionar camadas de intenção/generalização sem quebra de contrato.

---

## 0. Nota de adaptação (premissas corrigidas)

A especificação de origem foi escrita de forma agnóstica e assumiu **multi-tenant por schema** (`search_path` por tenant, DDL bruto por schema). O back-end real adota **schema único `sgcf` + coluna `tenant_id` + PostgreSQL Row-Level Security (RLS)**. Esta spec corrige tais premissas:

- A tabela alvo é `sgcf.cotacao` (singular); a coluna canônica é `prazo_maximo_dias`.
- A migração é **uma única migração EF Core code-first**, não um loop por schema. O DDL bruto da origem é substituído por `migrationBuilder`.
- Não há `search_path` por tenant. O isolamento ocorre via `TenantConnectionInterceptor` (`set_config('app.tenant_id', ...)`) + políticas RLS e _global query filter_ do EF.

Decisões do solicitante incorporadas (2026-06-06):

1. **URI de erro:** novos `type` em `https://sgcf.nordware.io/errors/` (o módulo será hospedado sob `nordware.io`).
2. **ProblemDetails:** padronizar todos os erros conforme boas práticas de API (RFC 7807), não apenas o de PTAX.
3. **PTAX:** generalizar a resolução para absorver outras moedas além de USD.
4. **Alertas:** validação suave via array de alertas estruturado, alinhada a boas práticas de UI/UX.
5. **Banco/API de produção:** fora de preocupação imediata (ambiente de teste); migração escrita corretamente, com ressalvas de produção registradas como _follow-up_.

---

## 1. Objetivo

Permitir que o operador registre o prazo máximo de uma cotação na unidade natural de cada modalidade (dias ou meses), preservando `prazoMaximoDias` como campo canônico comparável; enriquecer a cotação com campos de domínio opcionais (indexador, moeda alvo, carência e estruturantes do FGI); generalizar a resolução de PTAX para qualquer par moeda-estrangeira/BRL; e padronizar o tratamento de erros em RFC 7807 com `type` URIs estáveis.

### 1.1. Personas

| Persona                    | Necessidade atendida por esta spec                                                                 |
| -------------------------- | -------------------------------------------------------------------------------------------------- |
| **Operador de Tesouraria** | Cotar prazo em meses para instrumentos de médio/longo prazo; registrar indexador, moeda e carência |
| **Banco-alvo (captação)**  | Visualizar a intenção do operador (indexador, cobertura FGI, carência) antes de propor             |
| **Front-end**              | Detectar erro de PTAX por `type` estável; renderizar alertas suaves; exibir prazo na unidade certa |

### 1.2. Problema

`prazoMaximoDias` em dias inteiros é inadequado para modalidades cotadas em meses (Lei4131, NCE, Capital de Giro, FGI). O erro de PTAX é hoje um objeto anônimo `{ "error": "..." }` (não RFC 7807), detectado por texto frágil. A cotação não possui moeda alvo nem indexador, e a resolução de PTAX está fixada em USD.

### 1.3. Princípio crítico: tenor não é day-count

A conversão meses → dias (convenção fixa 30/360, `meses × 30`) serve **apenas** como teto comparável e base de exibição. **Não** é o day-count do CET. O CET permanece calculado na proposta e no contrato, sobre datas reais (dias úteis/252 para CDI, ACT/360 para moeda estrangeira). O campo `PrazoDias` de `PropostaDto` permanece inalterado.

---

## 2. Modelo de dados — campos novos

Todos os campos são **opcionais e retrocompatíveis**, exceto a regra de coexistência do tenor (§4.1).

### 2.1. Tenor de prazo (núcleo — MUST)

| Campo                 | Tipo                       | Obrigatoriedade        | Descrição                                              |
| --------------------- | -------------------------- | ---------------------- | ------------------------------------------------------ |
| `prazoMaximoValor`    | `int >= 1`                 | Condicional (§4.1)     | Valor do prazo na unidade indicada (intenção)          |
| `prazoMaximoUnidade`  | enum `UnidadePrazo`        | Opcional (default §4.2)| `Dias` \| `Meses`                                      |
| `prazoMaximoDias`     | `int >= 1`                 | Derivado/canônico      | Campo canônico; derivado pelo back-end; sempre na saída|

Novo enum de domínio `UnidadePrazo { Dias = 1, Meses = 2 }` em `Sgcf.Domain.Cotacoes`. Persistido como `text` (`'Dias'`/`'Meses'`) via converter, consistente com o padrão de `Modalidade`.

### 2.2. Moeda alvo (MUST — viabiliza PTAX multimoeda)

| Campo       | Tipo               | Modalidades        | Comportamento                                                                   |
| ----------- | ------------------ | ------------------ | ------------------------------------------------------------------------------- |
| `moedaAlvo` | enum `Moeda`       | Finimp, Lei4131    | Editável (FX). Herdada do contrato mãe e somente leitura em Refinimp. Fixa `Brl` em Nce/CapitalDeGiro/Fgi |

Reutiliza o enum existente `Sgcf.Domain.Common.Moeda` (`Brl, Usd, Eur, Jpy, Cny`). **Não** se cria `MoedaCotacao`.

### 2.3. Carência (SHOULD)

| Campo           | Tipo        | Modalidades                          | Comportamento                                              |
| --------------- | ----------- | ------------------------------------ | ---------------------------------------------------------- |
| `carenciaMeses` | `int >= 0`  | Lei4131, Nce, CapitalDeGiro, Fgi     | Carência pretendida em meses. Default `0` quando ausente nas aplicáveis. Em modalidade não aplicável: ignorada + alerta suave (decisão §3) |

### 2.4. Indexador base (SHOULD — modelagem em colunas planas)

Novo VO/owned `IndexadorBase` serializado como objeto na API e decomposto em colunas planas na persistência (decisão de modelagem §3).

- `indexadorBase.tipo`: enum `TipoIndexador { CdiPercentual, CdiMaisSpread, Prefixado, Tlp, Ipca, Selic, Sofr, Euribor }`. Opcional.
- `indexadorBase.percentualCdi`: `decimal?` — aplicável a `CdiPercentual` (ex.: `112.5`).
- `indexadorBase.spreadAa`: `decimal?` em p.p. ao ano — aplicável a `CdiMaisSpread`, `Sofr`, `Euribor`, `Tlp`, `Ipca`.
- `indexadorBase.taxaPrefixadaAa`: `decimal?` em p.p. ao ano — aplicável a `Prefixado`.

Coerência tipo↔campo é **validação suave** (alerta, sem bloqueio). Default: ausente.

### 2.5. Estruturantes do FGI (SHOULD — apenas modalidade Fgi)

| Campo                     | Tipo                | Comportamento                                                              |
| ------------------------- | ------------------- | ------------------------------------------------------------------------- |
| `finalidadeBndes`         | `string`            | String livre + validação suave (enum oficial posterior — §8)              |
| `bancoRepassadorPretendido` | `string`          | String livre + validação suave (enum oficial posterior — §8)              |
| `percentualCoberturaFgi`  | `decimal 0..100`    | Intenção na cotação. Coexiste com `FgiInputs.PercentualCoberto` (conversão, efetivo). Informativo; não compõe o CET |

### 2.6. PTAX multimoeda — campos de saída

| Campo                 | Tipo        | Comportamento                                                                              |
| --------------------- | ----------- | ------------------------------------------------------------------------------------------ |
| `ptaxUsada`           | `decimal?`  | **Canônico (novo).** PTAX D-1 de `moedaAlvo`/BRL usada como referência. Null em modalidade BRL pura |
| `dataPtaxReferencia`  | `date?`     | Inalterado. Data de referência da PTAX                                                     |
| `ptaxUsadaUsdBrl`     | `decimal?`  | **Depreciado.** Mantido por retrocompat: espelha `ptaxUsada` quando `moedaAlvo = Usd`; `null` caso contrário |

### 2.7. Matriz de aplicabilidade

| Modalidade    | Unidade default | `moedaAlvo`                  | `carenciaMeses` | `indexadorBase` | Estruturantes FGI |
| ------------- | --------------- | ---------------------------- | --------------- | --------------- | ----------------- |
| Finimp        | Dias            | Editável (FX)                | n/a             | Aplicável       | n/a               |
| Refinimp      | Dias            | Herdada do mãe (read-only)   | n/a             | Aplicável       | n/a               |
| Lei4131       | Meses           | Editável (FX)                | Aplicável       | Aplicável       | n/a               |
| Nce           | Meses           | Fixa `Brl`                   | Aplicável       | Aplicável       | n/a               |
| CapitalDeGiro | Meses           | Fixa `Brl`                   | Aplicável       | Aplicável       | n/a               |
| Fgi           | Meses           | Fixa `Brl`                   | Aplicável       | Aplicável       | Aplicável         |

> **Atenção de implementação:** chavear mapas por `ModalidadeContrato` (enum), nunca pela string persistida — `CapitalDeGiro` grava `"BALCAO_CAIXA"` no banco (mapeamento legado em `SgcfConverters.Modalidade`).

---

## 3. Decisões de engenharia (perguntas abertas resolvidas)

| Tema (origem §10)                         | Decisão para S40                                                                                       |
| ----------------------------------------- | ------------------------------------------------------------------------------------------------------ |
| Modelagem de `indexadorBase`              | **Colunas planas** (`indexador_tipo`, `indexador_percentual_cdi`, `indexador_spread_aa`, `indexador_taxa_prefixada_aa`). Serialização monta/decompõe o objeto |
| Listas FGI/BNDES (enum vs string)         | **String livre + validação suave** agora; promover a enum quando a lista oficial existir               |
| Tetos de prazo por modalidade             | **Sem teto rígido.** Faixas "esperadas" provisórias geram **alerta suave** (§4.4)                       |
| `carenciaMeses` em modalidade não aplicável | **Ignorar silenciosamente + alerta suave** (não bloquear) — melhor UX para FE em evolução             |
| Erros                                     | **RFC 7807 ProblemDetails padronizado** (§5), `type` em `sgcf.nordware.io`                              |
| Alertas                                   | **Array estruturado** `alertas[]` na resposta de escrita (§4.5)                                         |

---

## 4. Regras de validação e defaults

### 4.1. Tenor: precedência e coexistência (POST e PATCH)

1. Se `prazoMaximoValor` presente: resolver `prazoMaximoUnidade` (enviada ou default da modalidade §4.2); derivar `prazoMaximoDias`; persistir os três.
2. Se `prazoMaximoValor` ausente e `prazoMaximoDias` presente (legado): persistir `prazoMaximoUnidade = 'Dias'`, `prazoMaximoValor = prazoMaximoDias`, `prazoMaximoDias` como enviado.
3. POST sem nenhum dos dois: **HTTP 400** (prazo obrigatório na criação).
4. PATCH sem nenhum dos dois: não alterar o prazo (atualização parcial).
5. Ambos presentes e **inconsistentes**: adotar `{valor, unidade}` como fonte de verdade, recalcular `prazoMaximoDias` e emitir **alerta suave** (`prazo-recalculado`).

Derivação (função pura no domínio):

```
Dias:  prazoMaximoDias = prazoMaximoValor
Meses: prazoMaximoDias = prazoMaximoValor * 30   // 30/360 fixo; NÃO usar NodaTime Period
```

Depreciação: `prazoMaximoDias` como **entrada** é depreciado (aceito por retrocompat); como **saída** permanece canônico.

### 4.2. Unidade default por modalidade

`Finimp → Dias`, `Refinimp → Dias`, `Lei4131 → Meses`, `Nce → Meses`, `CapitalDeGiro → Meses`, `Fgi → Meses`.

### 4.3. Validação dura de prazo (HTTP 400)

- `prazoMaximoValor < 1`.
- `prazoMaximoValor` não inteiro.
- `prazoMaximoUnidade` fora de `{Dias, Meses}`.

### 4.4. Validação suave de prazo (alerta, sem bloqueio)

Faixas "esperadas" provisórias por modalidade (não rígidas); exceder gera alerta `prazo-fora-da-faixa-esperada`:

- Finimp: até ~3650 dias (bens de capital ~10 anos).
- Lei4131/Nce/CapitalDeGiro: médio/longo prazo comuns; faixa ampla.
- Fgi: 24–84 meses típico; 120 meses exceção legítima.

### 4.5. Validação dos campos de domínio

- `indexadorBase`: coerência tipo↔campo numérico = **alerta suave** (`indexador-incoerente`).
- `moedaAlvo`:
  - Nce/CapitalDeGiro/Fgi: forçar `Brl`; outro valor → **HTTP 400**.
  - Refinimp: **ignorar** valor enviado, adotar o herdado do contrato mãe (read-only). Divergência enviada → alerta suave `moeda-herdada-do-contrato-mae`.
  - Finimp/Lei4131: aceitar qualquer valor do enum `Moeda` que possua PTAX D-1 disponível (§6).
- `carenciaMeses`: `< 0` → **HTTP 400**; modalidade não aplicável → ignorar + alerta `carencia-ignorada`.
- `percentualCoberturaFgi`: faixa `0..100`; fora → **HTTP 400**; apenas Fgi.
- `finalidadeBndes` / `bancoRepassadorPretendido`: string livre; validação suave contra lista futura.

### 4.6. Modelo de alerta (UI/UX)

`alertas: AlertaDto[]`, sempre presente nas respostas de escrita (vazio quando não há alertas), ausente/transiente em leitura.

```jsonc
{ "codigo": "prazo-fora-da-faixa-esperada", "campo": "prazoMaximoValor", "severidade": "Aviso", "mensagem": "..." }
```

- `codigo`: string estável (machine-readable) — base do tratamento no FE.
- `campo`: caminho do campo de origem (para realce inline).
- `severidade`: enum `{ Info, Aviso }` (nunca bloqueante).
- `mensagem`: texto legível.

---

## 5. Tratamento de erros — RFC 7807 padronizado

### 5.1. Estratégia

Centralizar a tradução de erros em `GlobalExceptionHandler` (`IExceptionHandler`). Introduzir hierarquia de exceções tipadas e **remover os `catch (InvalidOperationException) → Conflict(new { error })` por endpoint** do `CotacoesController` (≈20 ocorrências), que hoje sombreiam o handler global. Todos os 409 passam a ser `ProblemDetails`.

### 5.2. Catálogo de `type` URIs (base `https://sgcf.nordware.io/errors/`)

| Exceção                         | HTTP | `type`                                                  | Extensões RFC 7807                          |
| ------------------------------- | ---- | ------------------------------------------------------- | ------------------------------------------- |
| `PtaxIndisponivelException`     | 409  | `…/errors/ptax-indisponivel`                            | `dataPtaxReferencia`, `moedaAlvo`           |
| `ConflitoDeEstadoException`     | 409  | `…/errors/conflito-de-estado`                           | (contextual)                                |
| `GarantiaExigidaNaoCobertaException` | 409 | `…/errors/garantia-exigida-nao-coberta` (alinhar de `sgcf.io`) | `limiteBancoId`, `garantiasExigidasRevisaoId`, `lacunas[]` |
| `ValidationException`           | 400  | `…/errors/validacao`                                    | `errors{}`                                   |
| `KeyNotFoundException`          | 404  | `…/errors/nao-encontrado`                               | —                                            |

`PtaxIndisponivelException : InvalidOperationException` (especialização) e `ConflitoDeEstadoException : InvalidOperationException` (genérica). O handler testa o subtipo específico primeiro; o genérico cobre as demais transições de estado do domínio.

### 5.3. Exemplo — PTAX indisponível (HTTP 409)

```json
{
  "status": 409,
  "title": "PTAX indisponível",
  "detail": "PTAX D-1 não disponível para a moeda e a data de referência informadas.",
  "type": "https://sgcf.nordware.io/errors/ptax-indisponivel",
  "dataPtaxReferencia": "2026-06-05",
  "moedaAlvo": "Eur"
}
```

O FE detecta por `err.type === 'https://sgcf.nordware.io/errors/ptax-indisponivel'`.

> **Follow-up não bloqueante:** migrar o `type` existente `https://sgcf.io/errors/garantia-exigida-nao-coberta` para a base `sgcf.nordware.io`. Coordenar com o FE por ser consumido. Fora do caminho crítico de S40.

---

## 6. PTAX multimoeda

### 6.1. Generalização da resolução

Hoje `CriarCotacaoCommandHandler` chama `cotacaoResolver.ResolverFxAsync(Moeda.Usd, TipoCotacao.PtaxD1, dataAbertura)` (USD fixo). Passa a usar `moedaAlvo`:

- `ResolverFxAsync(moedaAlvo, TipoCotacao.PtaxD1, dataAbertura)` para modalidades FX (`Cotacao.ExigeMoedaEstrangeira`).
- `moedaAlvo` default: para Finimp/Lei4131 é obrigatório quando FX; para Refinimp herda do contrato mãe (`mae.Moeda`, já carregado no handler).
- A indisponibilidade lança `PtaxIndisponivelException(moedaAlvo, dataReferencia)` em vez de `InvalidOperationException` genérica.

### 6.2. Persistência e saída

- `ptaxUsada` (canônico) recebe a venda da PTAX de `moedaAlvo`/BRL.
- `ptaxUsadaUsdBrl` (depreciado) recebe o mesmo valor **somente** quando `moedaAlvo = Usd`; `null` caso contrário.
- `dataPtaxReferencia` inalterado.
- `RefreshCotacaoMercadoCommand` atualiza `ptaxUsada` (e `ptaxUsadaUsdBrl` quando USD) usando `moedaAlvo` persistida.

### 6.3. Modalidades BRL puras

Nce/CapitalDeGiro/Fgi: `moedaAlvo = Brl`, sem resolução de PTAX; `ptaxUsada`, `ptaxUsadaUsdBrl` e `dataPtaxReferencia` permanecem `null` (invariante já existente em `Cotacao.Criar`).

---

## 7. Contratos de API (recortes)

### 7.1. `POST /api/v1/cotacoes`

Request (novo formato):

```json
{
  "modalidade": "Lei4131",
  "valorAlvoBrl": 5000000,
  "prazoMaximoValor": 60,
  "prazoMaximoUnidade": "Meses",
  "dataAbertura": "2026-06-06",
  "moedaAlvo": "Eur",
  "carenciaMeses": 12,
  "indexadorBase": { "tipo": "Euribor", "spreadAa": 2.75 }
}
```

Response 201 (recorte): inclui `prazoMaximoDias: 1800`, `prazoMaximoValor: 60`, `prazoMaximoUnidade: "Meses"`, `moedaAlvo: "Eur"`, `ptaxUsada`, `ptaxUsadaUsdBrl: null`, `indexadorBase`, `carenciaMeses`, e `alertas: []`.

Request legado (aceito): apenas `prazoMaximoDias` → back-end infere `unidade='Dias'`, `valor=prazoMaximoDias`.

### 7.2. `PATCH /api/v1/cotacoes/{id}`

Aceita `{ prazoMaximoValor, prazoMaximoUnidade, carenciaMeses, observacoes, ... }`; continua aceitando `prazoMaximoDias` direto (retrocompat). Restrição de estado: **somente `Rascunho`** (invariante de `EditarCamposBasicos`). Resposta inclui o `CotacaoDto` completo + `alertas[]`.

### 7.3. `GET /api/v1/cotacoes` e `GET /api/v1/cotacoes/{id}`

Sem mudança de request. Cada `CotacaoDto` passa a incluir os três campos de prazo, os campos de domínio (quando presentes) e `ptaxUsada`. Linhas legadas (pós-backfill) retornam `unidade='Dias'`, `valor=prazoMaximoDias`.

---

## 8. Estrutura do projeto — arquivos afetados

### 8.1. `Sgcf.Domain`

- `Cotacoes/UnidadePrazo.cs` — **novo** enum.
- `Cotacoes/TipoIndexador.cs` — **novo** enum.
- `Cotacoes/IndexadorBase.cs` — **novo** VO/owned (record).
- `Cotacoes/Cotacao.cs` — adicionar `PrazoMaximoValor`, `PrazoMaximoUnidade`, `MoedaAlvo`, `CarenciaMeses`, `IndexadorBase`, `FinalidadeBndes`, `BancoRepassadorPretendido`, `PercentualCoberturaFgi`, `PtaxUsada`. Estender `Criar()` e `EditarCamposBasicos()` com derivação 30/360 e invariantes por modalidade. Setters privados; derivação pura.
- `Cotacoes/Exceptions/PtaxIndisponivelException.cs`, `ConflitoDeEstadoException.cs` — **novos** (ou em `Sgcf.Application` se preferível à camada).

### 8.2. `Sgcf.Application`

- `Cotacoes/CotacaoDto.cs` — novos campos + `alertas` + `ptaxUsada`; `From()` atualizado.
- `Cotacoes/AlertaDto.cs` + `SeveridadeAlerta` — **novos**.
- `Cotacoes/Commands/CriarCotacaoCommand.cs` — novos campos opcionais (ao fim do record para compat C#); validador (dura/suave); handler (precedência tenor, PTAX multimoeda, herança de moeda Refinimp, coleta de alertas).
- `Cotacoes/Commands/AtualizarCotacaoCommand.cs` — idem para PATCH.
- `Cotacoes/Services/` — _service_ puro `ResolvedorTenor` e `GeradorAlertasCotacao` (funções puras testáveis).
- Generalização do uso de `IResolveTipoCotacaoService`/`CotacaoResolverService` por `moedaAlvo`.

### 8.3. `Sgcf.Infrastructure`

- `Persistence/Configurations/CotacaoConfiguration.cs` — mapear novas colunas; `IndexadorBase` como owned/colunas planas; `UnidadePrazo`/`Moeda`/`TipoIndexador` via converters string.
- `Migrations/<timestamp>_S40_CotacaoTenorEDominio.cs` — **nova** migração aditiva: colunas nullable → backfill (`unidade='Dias'`, `valor=prazo_maximo_dias`, `ptax_usada=ptax_usada_usd_brl`) → constraints (`unidade IN (...)`, `valor >= 1`, `carencia >= 0`, `cobertura 0..100`) → `NOT NULL` com default seguro nas colunas de tenor.

### 8.4. `Sgcf.Api`

- `Middleware/GlobalExceptionHandler.cs` — adicionar `PtaxIndisponivelException` e `ConflitoDeEstadoException`; alinhar `type` à base `sgcf.nordware.io`; catálogo de §5.2.
- `Controllers/CotacoesController.cs` — **remover** os `catch (InvalidOperationException) → Conflict(new { error })`; deixar o handler global responder.
- Versão de contrato OpenAPI: `0.11.0 → 0.12.0` (bump menor; aditivo).

### 8.5. Adaptadores e jobs

- `Sgcf.Mcp` / `Sgcf.A2a` — verificar se expõem criação/edição de cotação; se sim, propagar campos via Application (sem acoplar a Infrastructure).
- `Sgcf.Jobs` — `RefreshCotacaoMercadoCommand` (se agendado) já coberto pela generalização de PTAX.

---

## 9. Estilo de código (conforme `CLAUDE.md`)

- **Money:** valores monetários via `Money` (nunca `decimal` cru). `ValorAlvoBrl` permanece `Money` com `Moeda.Brl`.
- **Datas:** NodaTime no domínio (`LocalDate`, `Instant`); proibido `DateTime.Now`. Injetar `IClock`.
- **Tenor:** `prazoMaximoDias` é `int`; a conversão 30/360 é aritmética pura (`valor * 30`) — **não** usar `Period`, pois não representa duração de calendário real (§1.3).
- **Cálculos financeiros:** funções puras, sem I/O. `ResolvedorTenor` e `GeradorAlertasCotacao` puros.
- **Arredondamento:** `MidpointRounding.AwayFromZero` (HalfUp) — encapsulado em `Money`.
- **Camadas:** Domain sem dependências; Application só Domain; EF só em Infrastructure; Mcp/A2a nunca acessam Infrastructure.
- **Nomes:** conceitos de domínio em português; técnicos em inglês.

---

## 10. Estratégia de testes

### 10.1. Unitários de domínio (`Sgcf.Domain.Tests`)

- Derivação tenor: `(60, Meses) → 1800`; `(180, Dias) → 180`.
- Default de unidade por modalidade quando `unidade` ausente.
- Invariantes por modalidade: `moedaAlvo` forçada `Brl` em BRL puras; carência ignorada fora das aplicáveis.
- Recálculo em inconsistência valor↔dias.
- Validações duras (`valor < 1`, não inteiro, unidade inválida, carência negativa, cobertura fora de `0..100`).

### 10.2. Aplicação (`Sgcf.Application.Tests`)

- Precedência tenor em POST e PATCH; caminho legado (`prazoMaximoDias` só).
- Geração de alertas suaves (faixa de prazo, indexador incoerente, carência ignorada, moeda herdada).
- PTAX multimoeda: resolução por `moedaAlvo`; herança de moeda em Refinimp; `PtaxIndisponivelException` quando ausente.

### 10.3. Integração HTTP (`Sgcf.Api.IntegrationTests`)

Mapeia os critérios de aceite §11. Verificar shape `ProblemDetails` do 409 de PTAX (`type` + `dataPtaxReferencia` + `moedaAlvo`) e ausência do antigo `{ error }`.

### 10.4. Golden dataset

Sem alteração de CET esperado (tenor não afeta day-count). Não modificar `expectedOutput` sem _sign-off_.

---

## 11. Critérios de aceite (testáveis)

- POST `{prazoMaximoValor:60, prazoMaximoUnidade:"Meses"}` persiste `prazoMaximoDias=1800` e retorna os três campos.
- POST `{prazoMaximoValor:180, prazoMaximoUnidade:"Dias"}` persiste `prazoMaximoDias=180`.
- POST Lei4131 sem `prazoMaximoUnidade` aplica default `Meses`.
- POST legado só com `prazoMaximoDias=180` retorna `unidade='Dias'`, `valor=180`.
- POST `prazoMaximoValor < 1` → 400; `prazoMaximoUnidade` inválida → 400; POST sem prazo → 400.
- PATCH `{prazoMaximoValor:24, prazoMaximoUnidade:"Meses"}` atualiza `prazoMaximoDias=720`; PATCH sem prazo não altera.
- POST/PATCH com valor↔dias inconsistentes adota o par e recalcula, com alerta `prazo-recalculado`.
- Linha legada (pré-migração) retorna em GET `unidade='Dias'`, `valor=prazoMaximoDias`.
- POST Nce com `moedaAlvo ≠ Brl` → 400; POST Refinimp ignora `moedaAlvo` e herda do mãe.
- POST `carenciaMeses < 0` → 400; POST Fgi `percentualCoberturaFgi` fora de `0..100` → 400.
- POST Lei4131 `moedaAlvo:"Eur"` resolve PTAX EUR/BRL; `ptaxUsada` preenchido, `ptaxUsadaUsdBrl: null`.
- 409 de PTAX traz `type=…/ptax-indisponivel` + `dataPtaxReferencia` + `moedaAlvo`; corpo é `ProblemDetails`, não `{ error }`.
- GET (lista e por id) retornam os três campos de prazo, `ptaxUsada` e campos de domínio quando presentes; ausentes não quebram clientes legados.
- Migração roda em base com linhas preexistentes; backfill cobre todas antes do `NOT NULL`.

---

## 12. Migração e retrocompatibilidade

- Migração **aditiva e não destrutiva** (uma migração EF Core sobre `sgcf.cotacao`).
- Colunas novas nullable → backfill → constraints → `NOT NULL` (apenas tenor, com default `'Dias'`).
- Backfill: `prazo_maximo_unidade='Dias'`, `prazo_maximo_valor=prazo_maximo_dias`, `ptax_usada=ptax_usada_usd_brl`.
- Clientes legados que enviam só `prazoMaximoDias` continuam funcionando; que leem `CotacaoDto` ignoram campos novos.

> **Follow-up de produção (decisão §5 do solicitante — ambiente de teste por ora):** confirmar o papel de conexão das migrações vs. `FORCE ROW LEVEL SECURITY`. Em produção, `UPDATE` de backfill sob papel sujeito a RLS sem `app.tenant_id` resolvido pode afetar zero linhas. Rodar a migração sob papel `BYPASSRLS`/superusuário ou definir tenant explicitamente. Em dev, o papel `sgcf` é superusuário (RLS ignorada).

---

## 13. Versionamento

- Contrato OpenAPI: `0.11.0 → 0.12.0` (aditivo, retrocompatível).
- Rota HTTP permanece `api/v1/cotacoes` (versão de URL inalterada).

### 13.1. Changelog (sugerido)

```
## 0.12.0
### Adicionado
- Cotação: prazo como tenor { prazoMaximoValor, prazoMaximoUnidade }; prazoMaximoDias canônico (30/360).
- Campos de domínio: indexadorBase, moedaAlvo, carenciaMeses, estruturantes FGI.
- PTAX multimoeda: ptaxUsada (canônico) + resolução por moedaAlvo.
- Erros RFC 7807 ProblemDetails padronizados; type https://sgcf.nordware.io/errors/ptax-indisponivel (+ dataPtaxReferencia, moedaAlvo).
- Resposta de escrita: array alertas[] (validação suave, não bloqueante).
### Depreciado
- prazoMaximoDias como ENTRADA (aceito por retrocompat; preferir {valor, unidade}).
- ptaxUsadaUsdBrl como SAÍDA (preferir ptaxUsada; mantido por retrocompat).
### Migração
- Aditiva/não destrutiva sobre sgcf.cotacao; backfill de tenor e ptaxUsada.
```

---

## 14. Fronteiras (boundaries)

### 14.1. Sempre fazer

- Derivar e persistir `prazoMaximoDias` como canônico.
- Manter `Money`, NodaTime e HalfUp conforme `CLAUDE.md`.
- Centralizar erros em `ProblemDetails` com `type` estável.
- Emitir alertas suaves em vez de bloquear, exceto nos 400 explícitos da §4.

### 14.2. Perguntar antes

- Promover `finalidadeBndes`/`bancoRepassadorPretendido` a enum (depende de lista oficial — §8 origem).
- Fixar tetos rígidos de prazo por modalidade.
- Migrar o `type` de garantia de `sgcf.io` para `sgcf.nordware.io` (consumido pelo FE).
- Antecipar campos de fase posterior (§14.3).

### 14.3. Nunca fazer (fora de escopo S40)

- Alterar o day-count financeiro do CET na proposta/contrato (tenor é só teto comparável).
- Migração destrutiva ou mudança de unidade canônica de armazenamento.
- Implementar campos de fase posterior: `percentualRefinanciado` (cotação), `nceNumero`/`bancoMandatario` (cotação), `paisCredor` (cotação) — permanecem na conversão.
- Acoplar `Sgcf.Mcp`/`Sgcf.A2a` à `Sgcf.Infrastructure`.
- Alterar `expectedOutput` de golden datasets sem _sign-off_.

---

## 15. Sequenciamento sugerido

1. Domínio: enums + VO `IndexadorBase` + extensão de `Cotacao` (derivação tenor, invariantes) + testes unitários.
2. PTAX multimoeda: generalizar resolução + `PtaxIndisponivelException` + herança Refinimp.
3. Erros: hierarquia de exceções + `GlobalExceptionHandler` + remoção dos `catch` do controller.
4. Application: comandos/validadores/handlers + `AlertaDto` + geração de alertas + `CotacaoDto`.
5. Infrastructure: `CotacaoConfiguration` + migração aditiva + backfill.
6. Integração: critérios de aceite §11 + ajuste de testes existentes quebrados por assinatura.
7. Versão/changelog + handover ao FE.
```
