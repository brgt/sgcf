# Getting Started — Integração com a SGCF API

Este documento descreve tudo que um sistema externo (front-end, integração, agente) precisa para se conectar e operar na SGCF API.

---

## 1. Pré-requisitos

- URL base da API (ver tabela em [README.md](./README.md))
- Token JWT obtido junto ao provedor de identidade (`dev-auth.proxysgroup.com.br`)
- Role adequada para os recursos que serão consumidos

---

## 2. Autenticação

### 2.1 Obtendo o Token

A API delega autenticação ao servidor OAuth 2.0 da Proxys Group.

**Fluxo recomendado:** Authorization Code + PKCE (front-ends SPA/Headless)

```
POST https://dev-auth.proxysgroup.com.br/connect/token
Content-Type: application/x-www-form-urlencoded

grant_type=authorization_code
&client_id=<client_id>
&code=<authorization_code>
&redirect_uri=<redirect_uri>
&code_verifier=<pkce_verifier>
```

**Resposta:**
```json
{
  "access_token": "eyJhbGciOiJSUzI1NiIs...",
  "token_type": "Bearer",
  "expires_in": 3600,
  "refresh_token": "..."
}
```

### 2.2 Usando o Token

Inclua o token em **todas** as requisições:

```
Authorization: Bearer eyJhbGciOiJSUzI1NiIs...
```

### 2.3 Renovando o Token

Use o `refresh_token` antes da expiração:

```
POST https://dev-auth.proxysgroup.com.br/connect/token
Content-Type: application/x-www-form-urlencoded

grant_type=refresh_token
&client_id=<client_id>
&refresh_token=<refresh_token>
```

### 2.4 Ambiente de Desenvolvimento

Em desenvolvimento, qualquer token Bearer válido é aceito. Um token de teste pode ser gerado diretamente no endpoint `/swagger` da API.

---

## 3. Headers Padrão

| Header | Valor | Obrigatório |
|--------|-------|-------------|
| `Authorization` | `Bearer <token>` | Sim |
| `Content-Type` | `application/json` | Sim (para POST/PUT) |
| `Accept` | `application/json` | Recomendado |

---

## 4. Idempotência

Endpoints de criação e simulação aceitam o header de idempotência para evitar processamento duplicado em caso de retry:

```
Idempotency-Key: <uuid-v4-unico-por-operacao>
```

Endpoints com idempotência habilitada estão marcados nos documentos de cada recurso.

---

## 5. Tratamento de Erros

Todos os erros seguem o padrão **Problem Details (RFC 7807)**:

```json
{
  "type": "https://tools.ietf.org/html/rfc7807",
  "title": "Validation failed",
  "status": 400,
  "detail": "O campo 'valorPrincipal' deve ser maior que zero.",
  "errors": {
    "valorPrincipal": ["Deve ser maior que zero."]
  }
}
```

### Códigos de Status

| Status | Significado | Ação recomendada |
|--------|-------------|-----------------|
| `200 OK` | Sucesso com corpo | Processar resposta |
| `201 Created` | Recurso criado | Ler `Location` header |
| `204 No Content` | Sucesso sem corpo | Sem ação |
| `400 Bad Request` | Dados inválidos | Exibir `errors` ao usuário |
| `401 Unauthorized` | Token ausente/inválido | Redirecionar para login |
| `403 Forbidden` | Role insuficiente | Exibir mensagem de permissão |
| `404 Not Found` | Recurso não existe | Tratar na UI |
| `409 Conflict` | Conflito de estado | Verificar regra de negócio |
| `422 Unprocessable Entity` | Regra de negócio violada | Exibir `detail` ao usuário |
| `500 Internal Server Error` | Erro inesperado | Logar e notificar |

---

## 6. Paginação

Endpoints de listagem seguem o padrão:

**Request:**
```
GET /api/v1/contratos?page=1&pageSize=25
```

**Response:**
```json
{
  "items": [...],
  "total": 150,
  "page": 1,
  "pageSize": 25
}
```

| Parâmetro | Padrão | Máximo | Descrição |
|-----------|--------|--------|-----------|
| `page` | `1` | — | Página (base 1) |
| `pageSize` | `25` | `100` | Itens por página |

---

## 7. Convenções de Formato

### Datas

| Tipo .NET | Formato | Exemplo |
|-----------|---------|---------|
| `DateOnly` | `YYYY-MM-DD` | `2026-03-15` |
| `DateTimeOffset` | ISO 8601 UTC | `2026-03-15T14:30:00Z` |

### Números

- Decimais usam ponto (`.`) como separador: `1234567.89`
- Percentuais são representados como valor real: `2.5` = 2,5%

### GUIDs

Formato padrão com hífens: `3fa85f64-5717-4562-b3fc-2c963f66afa6`

---

## 8. CORS

As origens liberadas por padrão em desenvolvimento:

- `http://localhost:5173`
- `http://localhost:4173`

Para produção, as origens são configuradas por ambiente no `appsettings.json`.

---

## 9. Health Check

Verifique a disponibilidade da API antes de inicializar o sistema:

```
GET /health
```

```json
{ "status": "healthy" }
```

Não requer token.

---

## 10. Exemplo Completo — Listar Contratos

```http
GET /api/v1/contratos?page=1&pageSize=10&status=ATIVO&moeda=USD
Authorization: Bearer eyJhbGciOiJSUzI1NiIs...
Accept: application/json
```

**Resposta:**
```json
{
  "items": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "numeroExterno": "FINIMP-2025-001",
      "codigoInterno": "SGCF-0001",
      "bancoId": "...",
      "modalidade": "FINIMP",
      "moeda": "USD",
      "valorPrincipal": 1500000.00,
      "dataContratacao": "2025-01-10",
      "dataVencimento": "2027-01-10",
      "taxaAa": 5.25,
      "status": "ATIVO"
    }
  ],
  "total": 1,
  "page": 1,
  "pageSize": 10
}
```
