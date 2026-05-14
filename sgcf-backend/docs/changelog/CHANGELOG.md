# SGCF API — Changelog

**Formato:** Semver + ISO 8601. Seções por versão, ordem decrescente.
**Destinatários:** Sistemas de IA (agentes MCP/A2A/LLM), integrações máquina-a-máquina, CI/CD.

> **Convenções de impacto:**
> - `BREAKING` — quebra compatibilidade com clientes existentes; migração obrigatória.
> - `ADDITIVE` — novo endpoint ou campo opcional; clientes existentes não quebram.
> - `FIX` — correção de comportamento incorreto; pode alterar resposta sem quebrar contrato.
> - `INTERNAL` — mudança interna sem impacto na interface pública.

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
