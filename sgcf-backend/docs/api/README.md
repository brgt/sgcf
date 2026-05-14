# SGCF API — Guia de Conexão e Integração

Sistema de Gestão de Captações e Financiamentos — API REST v1.

---

## Índice

1. [URLs por Ambiente](#1-urls-por-ambiente)
2. [Autenticação](#2-autenticação)
   - [Ambiente de Desenvolvimento (token fixo)](#21-ambiente-de-desenvolvimento)
   - [Produção (OAuth 2.0)](#22-produção)
3. [Headers Obrigatórios](#3-headers-obrigatórios)
4. [Políticas de Autorização e Roles](#4-políticas-de-autorização-e-roles)
5. [Idempotência](#5-idempotência)
6. [Tratamento de Erros](#6-tratamento-de-erros)
7. [Convenções de Formato](#7-convenções-de-formato)
8. [Paginação](#8-paginação)
9. [Exemplos Prontos — curl](#9-exemplos-prontos--curl)
10. [Coleção Bruno](#10-coleção-bruno)
11. [Endpoints Disponíveis](#11-endpoints-disponíveis)
12. [Health Check](#12-health-check)

---

## 1. URLs por Ambiente

| Ambiente | Base URL | Swagger |
|----------|----------|---------|
| **Local (dev)** | `http://localhost:5000` | `http://localhost:5000/swagger` |
| **Produção** | `https://api.sgcf.proxysgroup.com.br` | não disponível |

Todos os endpoints são prefixados com `/api/v1/`.

> **Subir a API localmente:**
> ```bash
> cd sgcf-backend
> ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/Sgcf.Api -c Release
> ```

---

## 2. Autenticação

A API usa **JWT Bearer Token**. O header deve ser enviado em todas as requisições (exceto `/health`):

```
Authorization: Bearer <token>
```

### 2.1 Ambiente de Desenvolvimento

Em desenvolvimento (`ASPNETCORE_ENVIRONMENT=Development`), **qualquer string** no header Bearer é aceita. O servidor cria automaticamente um principal com todas as roles (`admin`, `tesouraria`, `gerente`, `diretor`, `contabilidade`, `auditor`).

**Token de desenvolvimento:**
```
dev-token
```

**Header completo:**
```
Authorization: Bearer dev-token
```

Não é necessário gerar, validar nem renovar esse token. Qualquer valor funciona.

### 2.2 Produção

Em produção, a API valida JWT emitido pelo servidor de identidade da Proxys Group.

| Parâmetro | Valor |
|-----------|-------|
| **Authority** | `https://auth.proxysgroup.com.br` |
| **Audience** | `sgcf-api` |
| **Algoritmo** | RS256 |
| **Expiração padrão** | 3600 segundos (1 hora) |

**Obter token (Client Credentials — integrações máquina-a-máquina):**

```http
POST https://auth.proxysgroup.com.br/connect/token
Content-Type: application/x-www-form-urlencoded

grant_type=client_credentials
&client_id=SEU_CLIENT_ID
&client_secret=SEU_CLIENT_SECRET
&scope=sgcf-api
```

**Resposta:**
```json
{
  "access_token": "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9...",
  "token_type": "Bearer",
  "expires_in": 3600
}
```

**Obter token (Authorization Code + PKCE — front-ends SPA):**

```http
POST https://auth.proxysgroup.com.br/connect/token
Content-Type: application/x-www-form-urlencoded

grant_type=authorization_code
&client_id=SEU_CLIENT_ID
&code=AUTHORIZATION_CODE
&redirect_uri=SUA_REDIRECT_URI
&code_verifier=SEU_PKCE_VERIFIER
```

**Renovar token (refresh):**

```http
POST https://auth.proxysgroup.com.br/connect/token
Content-Type: application/x-www-form-urlencoded

grant_type=refresh_token
&client_id=SEU_CLIENT_ID
&refresh_token=SEU_REFRESH_TOKEN
```

> Credenciais de produção (`client_id`, `client_secret`) são fornecidas pelo time de infraestrutura da Proxys Group. Não commitar em repositórios.

---

## 3. Headers Obrigatórios

| Header | Valor | Quando |
|--------|-------|--------|
| `Authorization` | `Bearer dev-token` (dev) ou `Bearer <jwt>` (prod) | Todas as requisições |
| `Content-Type` | `application/json` | POST e PUT |
| `Accept` | `application/json` | Recomendado sempre |
| `Idempotency-Key` | UUID v4 único por operação | POST de criação (opcional, mas recomendado) |

**Exemplo completo de headers:**
```http
Authorization: Bearer dev-token
Content-Type: application/json
Accept: application/json
Idempotency-Key: 550e8400-e29b-41d4-a716-446655440000
```

---

## 4. Políticas de Autorização e Roles

Cada endpoint exige uma política. Em desenvolvimento, o token `dev-token` já possui todas as roles.

| Política | Roles aceitas | Endpoints típicos |
|----------|--------------|-------------------|
| `Leitura` | qualquer autenticado | GET em todos os recursos |
| `Escrita` | `tesouraria`, `admin` | POST/PUT em contratos, garantias, hedges |
| `Gerencial` | `gerente`, `diretor`, `admin` | DELETE, simulações executivas |
| `Executivo` | `tesouraria`, `gerente`, `diretor`, `admin` | Painel, simulador |
| `Auditoria` | `contabilidade`, `auditor`, `admin` | Plano de contas, lançamentos |
| `Admin` | `admin` | Bancos (criação/config), parâmetros de cotação |

Em produção, o JWT deve conter as roles como claims no campo `role` (array de strings). Exemplo de payload JWT:

```json
{
  "sub": "user-id",
  "name": "Welysson Soares",
  "role": ["tesouraria", "gerente"],
  "aud": "sgcf-api",
  "iss": "https://auth.proxysgroup.com.br",
  "exp": 1768000000
}
```

---

## 5. Idempotência

Endpoints de criação aceitam o header `Idempotency-Key` para evitar duplicação em retentativas:

```
Idempotency-Key: <uuid-v4-único-por-operação>
```

- O mesmo `Idempotency-Key` na mesma operação retorna a resposta original sem reprocessar.
- Gere um UUID v4 diferente para cada nova operação.
- Se omitido, a requisição é processada normalmente (sem proteção contra duplicatas).

**Endpoints com idempotência habilitada:**
- `POST /api/v1/contratos`
- `POST /api/v1/contratos/{id}/garantias`

---

## 6. Tratamento de Erros

Todos os erros seguem **Problem Details (RFC 7807)**:

```json
{
  "type": "https://tools.ietf.org/html/rfc7807",
  "title": "Validation failed",
  "status": 422,
  "detail": "QtdDuplicatasCedidas deve ser maior que zero.",
  "traceId": "00-abc123..."
}
```

| Status | Significado | Ação |
|--------|-------------|------|
| `200 OK` | Sucesso | Processar corpo da resposta |
| `201 Created` | Recurso criado | Ler `id` no corpo |
| `400 Bad Request` | Dados inválidos (formato, campos faltando) | Ver campo `errors` |
| `401 Unauthorized` | Token ausente ou inválido | Verificar header `Authorization` |
| `403 Forbidden` | Role insuficiente | Verificar roles do token |
| `404 Not Found` | Recurso não existe | Verificar UUID na URL |
| `409 Conflict` | Estado conflitante (ex: contrato já possui cronograma) | Ler `detail` |
| `422 Unprocessable Entity` | Regra de negócio violada | Ler `detail` |
| `500 Internal Server Error` | Erro inesperado | Logar `traceId` e reportar |

---

## 7. Convenções de Formato

### Datas

| Tipo | Formato | Exemplo |
|------|---------|---------|
| Data simples (`DateOnly`) | `YYYY-MM-DD` | `2026-03-15` |
| Data+hora UTC (`DateTimeOffset`) | ISO 8601 | `2026-03-15T14:30:00Z` |

### Números

- Separador decimal: **ponto** (`.`) — nunca vírgula
- Percentuais: valor humano — `2.5` significa 2,5%
- Dinheiro: decimal com 2 casas — `1234567.89`

### GUIDs

Formato padrão com hífens: `3fa85f64-5717-4562-b3fc-2c963f66afa6`

### Enums relevantes

| Enum | Valores |
|------|---------|
| `Modalidade` | `Finimp`, `Lei4131`, `Refinimp`, `Nce`, `BalcaoCaixa`, `Fgi` |
| `Moeda` | `Brl`, `Usd`, `Eur`, `Jpy`, `Cny` |
| `StatusContrato` | `Ativo`, `Liquidado`, `Inadimplente`, `Cancelado` |
| `TipoGarantia` | `CdbCativo`, `Sblc`, `Aval`, `AlienacaoFiduciaria`, `Duplicatas`, `RecebiveisCartao`, `BoletoBancario`, `Fgi` |
| `TipoCotacao` | `PtaxD0`, `PtaxD1`, `SpotIntraday`, `Fixing` |
| `BaseCalculo` | `Dias252`, `Dias360`, `Dias365` |

---

## 8. Paginação

Listagens retornam objetos paginados:

**Request:**
```
GET /api/v1/contratos?page=1&pageSize=25
```

**Response:**
```json
{
  "items": [ ... ],
  "total": 150,
  "page": 1,
  "pageSize": 25
}
```

| Parâmetro | Padrão | Máximo |
|-----------|--------|--------|
| `page` | `1` | — |
| `pageSize` | `25` | `100` |

---

## 9. Exemplos Prontos — curl

Todos os exemplos abaixo funcionam **sem alteração** contra a API local em modo desenvolvimento.

### Health Check (sem token)
```bash
curl http://localhost:5000/health
```

### Listar contratos
```bash
curl -s \
  -H "Authorization: Bearer dev-token" \
  -H "Accept: application/json" \
  "http://localhost:5000/api/v1/contratos?page=1&pageSize=10" \
  | python3 -m json.tool
```

### Buscar contrato por ID
```bash
curl -s \
  -H "Authorization: Bearer dev-token" \
  "http://localhost:5000/api/v1/contratos/019e21cc-102f-79c0-b2c1-48ad8fef9d86" \
  | python3 -m json.tool
```

### Listar bancos
```bash
curl -s \
  -H "Authorization: Bearer dev-token" \
  "http://localhost:5000/api/v1/bancos" \
  | python3 -m json.tool
```

### Buscar banco por nome/código (flexible lookup)
```bash
# Por codigoCompe
curl -s -H "Authorization: Bearer dev-token" "http://localhost:5000/api/v1/bancos/033"

# Por apelido (case-insensitive)
curl -s -H "Authorization: Bearer dev-token" "http://localhost:5000/api/v1/bancos/Santander"

# Por texto parcial
curl -s -H "Authorization: Bearer dev-token" "http://localhost:5000/api/v1/bancos/Caixa"
```

### Criar contrato FINIMP
```bash
curl -s -X POST "http://localhost:5000/api/v1/contratos" \
  -H "Authorization: Bearer dev-token" \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: $(uuidgen)" \
  -d '{
    "bancoId": "BANCO_ID_AQUI",
    "numeroExterno": "FINIMP-2026-001",
    "modalidade": "Finimp",
    "moeda": "Usd",
    "valorPrincipal": 500000.00,
    "dataContratacao": "2026-01-10",
    "dataVencimento": "2027-01-10",
    "taxaAa": 6.5,
    "baseCalculo": "Dias360",
    "createdBy": "sistema",
    "finimpDetail": {
      "numeroCcex": "FINIMP-123",
      "bancoCorrespondenteSwift": "CITIUS33",
      "bancoCorrespondenteNome": "Citibank NY",
      "paisOrigemRecursos": "US"
    }
  }' | python3 -m json.tool
```

### Listar garantias de um contrato
```bash
curl -s \
  -H "Authorization: Bearer dev-token" \
  "http://localhost:5000/api/v1/contratos/019e21cc-102f-79c0-b2c1-48ad8fef9d86/garantias" \
  | python3 -m json.tool
```

### Indicadores de garantias
```bash
curl -s \
  -H "Authorization: Bearer dev-token" \
  "http://localhost:5000/api/v1/contratos/019e21cc-102f-79c0-b2c1-48ad8fef9d86/garantias/indicadores" \
  | python3 -m json.tool
```

### Adicionar garantia (CDB cativo)
```bash
curl -s -X POST \
  "http://localhost:5000/api/v1/contratos/CONTRATO_ID/garantias" \
  -H "Authorization: Bearer dev-token" \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: $(uuidgen)" \
  -d '{
    "tipo": "CdbCativo",
    "valorBrl": 1000000.00,
    "dataConstituicao": "2026-01-15",
    "createdBy": "sistema",
    "cdb": {
      "bancoCustodia": "Banco do Brasil",
      "numeroCdb": "CDB-12345",
      "dataEmissaoCdb": "2026-01-15",
      "dataVencimentoCdb": "2028-01-15",
      "percentualCdiPct": 100.0,
      "taxaIrrfAplicacaoPct": 15.0
    }
  }' | python3 -m json.tool
```

### Resolver tipo de cotação
```bash
curl -s \
  -H "Authorization: Bearer dev-token" \
  "http://localhost:5000/api/v1/parametros-cotacao/resolve?modalidade=Finimp" \
  | python3 -m json.tool
```

### Painel executivo — dívida consolidada
```bash
curl -s \
  -H "Authorization: Bearer dev-token" \
  "http://localhost:5000/api/v1/painel/divida" \
  | python3 -m json.tool
```

### Calendário de vencimentos (sem projeção)
```bash
curl -s \
  -H "Authorization: Bearer dev-token" \
  "http://localhost:5000/api/v1/painel/vencimentos?ano=2026" \
  | python3 -m json.tool
```

### Calendário de vencimentos com projeção de juros CDI
```bash
curl -s \
  -H "Authorization: Bearer dev-token" \
  "http://localhost:5000/api/v1/painel/vencimentos?ano=2026&cdiAnualPct=14.75" \
  | python3 -m json.tool
```

> Passe `cdiAnualPct` com o CDI anual atual (ex.: `14.75` = 14,75% a.a.). O campo `jurosBrlProjetado`
> é calculado para cada parcela de contrato indexado ao CDI. `taxaCdiUsadaPct` no topo da resposta
> confirma o valor aplicado.

---

## 10. Coleção Bruno

A coleção Bruno com todos os requests está em:

```
docs/api/collections/sgcf-api/
```

**Para usar:**

1. Instale o [Bruno](https://www.usebruno.com/) (gratuito, open-source).
2. Abra o Bruno → **Open Collection** → selecione a pasta `sgcf-api/`.
3. No canto superior direito, selecione o ambiente **Dev**.
4. O ambiente **Dev** já tem `token: dev-token` e `baseUrl: http://localhost:5000` configurados.
5. Execute qualquer request diretamente.

**Estrutura da coleção:**

| Pasta | Conteúdo |
|-------|----------|
| `00-Health` | Health check |
| `01-Bancos` | CRUD de bancos + config antecipação + busca flexível |
| `02-Plano-Contas` | Plano de contas contábil |
| `03-Parametros-Cotacao` | Parâmetros de cotação + resolver |
| `04-Contratos` | CRUD completo + cronograma + tabela completa + antecipação |
| `05-Garantias` | Adicionar/cancelar garantias + indicadores |
| `06-Hedges` | Hedges forward, MTM, cancelamento |
| `07-Painel` | Dívida, KPIs, vencimentos, EBITDA |
| `08-Simulador` | Cenário cambial + antecipação de portfólio |

**Variáveis do ambiente Dev:**

| Variável | Valor padrão | Descrição |
|----------|-------------|-----------|
| `baseUrl` | `http://localhost:5000` | URL base da API |
| `token` | `dev-token` | Token Bearer (qualquer string funciona em dev) |
| `bancoId` | *(preenchido pelos scripts)* | ID do banco atual |
| `contratoId` | *(preenchido pelos scripts)* | ID do contrato atual |
| `garantiaId` | *(preenchido pelos scripts)* | ID da garantia atual |
| `hedgeId` | *(preenchido pelos scripts)* | ID do hedge atual |
| `planoContaId` | *(preenchido pelos scripts)* | ID da conta contábil atual |
| `parametroCotacaoId` | *(preenchido pelos scripts)* | ID do parâmetro atual |

---

## 11. Endpoints Disponíveis

### Bancos — `/api/v1/bancos`

| Método | Path | Política | Descrição |
|--------|------|----------|-----------|
| `GET` | `/api/v1/bancos` | Leitura | Listar bancos (`?search=`) |
| `GET` | `/api/v1/bancos/{id:guid}` | Leitura | Buscar por UUID |
| `GET` | `/api/v1/bancos/{identifier}` | Leitura | Buscar por codigoCompe, apelido ou texto parcial |
| `POST` | `/api/v1/bancos` | Admin | Criar banco |
| `PUT` | `/api/v1/bancos/{id}/config-antecipacao` | Admin | Atualizar regras de antecipação |

### Contratos — `/api/v1/contratos`

| Método | Path | Política | Descrição |
|--------|------|----------|-----------|
| `GET` | `/api/v1/contratos` | Leitura | Listar contratos (paginado, multi-filtro) |
| `GET` | `/api/v1/contratos/{id}` | Leitura | Buscar contrato |
| `POST` | `/api/v1/contratos` | Escrita | Criar contrato (FINIMP/Lei4131/Refinimp/NCE/BalcaoCaixa/FGI) |
| `DELETE` | `/api/v1/contratos/{id}` | Gerencial | Excluir contrato (somente Rascunho) |
| `POST` | `/api/v1/contratos/{id}/gerar-cronograma` | Escrita | Gerar cronograma automaticamente |
| `POST` | `/api/v1/contratos/{id}/importar-cronograma` | Escrita | Importar parcelas manualmente (BalcaoCaixa) |
| `GET` | `/api/v1/contratos/{id}/tabela-completa` | Executivo | Demonstrativo completo (JSON, PDF, Excel) |
| `POST` | `/api/v1/contratos/{id}/simular-antecipacao-total` | Executivo | Simular liquidação total |
| `POST` | `/api/v1/contratos/{id}/simular-antecipacao-parcial` | Executivo | Simular amortização parcial |

### Garantias — `/api/v1/contratos/{id}/garantias`

| Método | Path | Política | Descrição |
|--------|------|----------|-----------|
| `GET` | `/{id}/garantias` | Leitura | Listar garantias do contrato |
| `POST` | `/{id}/garantias` | Escrita | Adicionar garantia |
| `GET` | `/{id}/garantias/indicadores` | Leitura | Cobertura total, alertas |
| `DELETE` | `/{id}/garantias/{garantiaId}` | Gerencial | Cancelar garantia |

### Hedges — `/api/v1/contratos/{id}/hedges` e `/api/v1/hedges`

| Método | Path | Política | Descrição |
|--------|------|----------|-----------|
| `GET` | `/contratos/{id}/hedges` | Leitura | Listar hedges do contrato |
| `POST` | `/contratos/{id}/hedges` | Escrita | Adicionar hedge forward |
| `GET` | `/hedges/{hedgeId}/mtm` | Executivo | Calcular MTM |
| `DELETE` | `/hedges/{hedgeId}` | Gerencial | Cancelar hedge |

### Painel — `/api/v1/painel`

| Método | Path | Política | Descrição |
|--------|------|----------|-----------|
| `GET` | `/painel/divida` | Leitura | Dívida total por moeda, banco, modalidade |
| `GET` | `/painel/garantias` | Leitura | Cobertura de garantias |
| `GET` | `/painel/vencimentos` | Leitura | Agenda de vencimentos (`?ano=` obrigatório; `?cdiAnualPct=` para projeção CDI) |
| `GET` | `/painel/kpis` | Executivo | KPIs executivos |
| `POST` | `/painel/ebitda` | Auditoria | Registrar EBITDA mensal |

### Simulador — `/api/v1/simulador`

| Método | Path | Política | Descrição |
|--------|------|----------|-----------|
| `POST` | `/simulador/cenario-cambial` | Executivo | Impacto de variação cambial no portfólio |
| `POST` | `/simulador/antecipacao-portfolio` | Executivo | Custo de antecipar múltiplos contratos |

### Plano de Contas — `/api/v1/plano-contas`

| Método | Path | Política | Descrição |
|--------|------|----------|-----------|
| `GET` | `/plano-contas` | Leitura | Listar contas |
| `GET` | `/plano-contas/{id}` | Leitura | Buscar conta |
| `POST` | `/plano-contas` | Auditoria | Criar conta |
| `PUT` | `/plano-contas/{id}` | Auditoria | Atualizar conta |

### Parâmetros de Cotação — `/api/v1/parametros-cotacao`

| Método | Path | Política | Descrição |
|--------|------|----------|-----------|
| `GET` | `/parametros-cotacao` | Leitura | Listar parâmetros |
| `GET` | `/parametros-cotacao/{id}` | Leitura | Buscar por ID |
| `POST` | `/parametros-cotacao` | Admin | Criar parâmetro |
| `PUT` | `/parametros-cotacao/{id}` | Admin | Atualizar parâmetro |
| `DELETE` | `/parametros-cotacao/{id}` | Admin | Excluir parâmetro |
| `GET` | `/parametros-cotacao/resolve` | Leitura | Resolver tipo de cotação para banco/modalidade |

---

## 12. Health Check

```bash
curl http://localhost:5000/health
```

**Resposta:**
```json
{ "status": "healthy" }
```

Não requer autenticação. Use para verificar disponibilidade antes de inicializar o sistema.

---

## Documentação por Recurso

| Documento | Recurso |
|-----------|---------|
| [contratos.md](./contratos.md) | Contratos, cronograma, garantias, antecipação |
| [bancos.md](./bancos.md) | Bancos e configurações de antecipação |
| [hedges.md](./hedges.md) | Hedges NDF e MTM |
| [painel.md](./painel.md) | Painel executivo e KPIs |
| [simulador.md](./simulador.md) | Simulações de cenário e portfólio |
| [plano-contas.md](./plano-contas.md) | Plano de contas contábil |
| [parametros-cotacao.md](./parametros-cotacao.md) | Cotação PTAX/Spot e hierarquia de resolução |
| [schemas.md](./schemas.md) | Todos os DTOs, enums e tipos compartilhados |
| [getting-started.md](./getting-started.md) | Fluxo OAuth completo passo a passo |
