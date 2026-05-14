# Bancos API

**Base route:** `/api/v1/bancos`

Gerencia o cadastro de bancos e suas configurações de antecipação (regras comerciais para liquidação antecipada de contratos).

---

## Endpoints

### Listar Bancos

```
GET /api/v1/bancos
Autorização: Leitura
```

**Query Parameters:**

| Parâmetro | Tipo | Descrição |
|-----------|------|-----------|
| `search` | string | Busca por nome ou código COMPE |

**Response 200 OK:** `BancoDto[]`

```json
[
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "codigoCompe": "033",
    "razaoSocial": "Banco Santander (Brasil) S.A.",
    "apelido": "Santander",
    "aceitaLiquidacaoTotal": true,
    "aceitaLiquidacaoParcial": true,
    "exigeAnuenciaExpressa": false,
    "exigeParcelaInteira": false,
    "avisoPrevioMinDiasUteis": 3,
    "padraoAntecipacao": "BREAKFUNDING",
    "valorMinimoParcialPct": 10.0,
    "breakFundingFeePct": 0.5,
    "tlaPctSobreSaldo": null,
    "tlaPctPorMesRemanescente": null,
    "observacoesAntecipacao": null,
    "createdAt": "2025-01-01T00:00:00Z",
    "updatedAt": "2026-01-15T10:30:00Z"
  }
]
```

---

### Buscar Banco por ID

```
GET /api/v1/bancos/{id}
Autorização: Leitura
```

**Path Parameters:**

| Parâmetro | Tipo | Descrição |
|-----------|------|-----------|
| `id` | guid | UUID do banco |

**Responses:**
- `200 OK` — [BancoDto](./schemas.md#bancodto)
- `404 Not Found` — Banco não encontrado

---

### Buscar Banco por Identificador Flexível

```
GET /api/v1/bancos/{identifier}
Autorização: Leitura
```

Aceita qualquer identificador textual. Resolve na seguinte ordem de prioridade:

1. **codigoCompe exato** — e.g. `033`, `341`
2. **apelido exato** (case-insensitive) — e.g. `Santander`, `itaú`
3. **busca parcial** em codigoCompe, apelido e razaoSocial (retorna o primeiro resultado)

> Se o segmento for um UUID válido, a rota `GET /api/v1/bancos/{id:guid}` tem precedência.

**Exemplos:**
```
GET /api/v1/bancos/033         → Santander
GET /api/v1/bancos/Santander   → Santander
GET /api/v1/bancos/Banco+do    → Banco do Brasil (primeiro match parcial)
```

**Path Parameters:**

| Parâmetro | Tipo | Descrição |
|-----------|------|-----------|
| `identifier` | string | codigoCompe, apelido, ou texto parcial |

**Responses:**
- `200 OK` — [BancoDto](./schemas.md#bancodto)
- `404 Not Found` — Nenhum banco encontrado com o identificador fornecido

---

### Criar Banco

```
POST /api/v1/bancos
Autorização: Admin
```

> **Importante:** o `POST` aceita apenas os 4 campos básicos abaixo. As demais configurações de antecipação (aceita liquidação total/parcial, fees, TLA, etc.) são definidas após a criação via `PUT /api/v1/bancos/{id}/config-antecipacao`.

**Request Body:**
```json
{
  "codigoCompe": "341",
  "razaoSocial": "Itaú Unibanco S.A.",
  "apelido": "Itaú",
  "padraoAntecipacao": "D"
}
```

| Campo | Tipo | Obrigatório | Descrição |
|-------|------|-------------|-----------|
| `codigoCompe` | string | Sim | Código COMPE/BACEN (exatamente 3 caracteres) |
| `razaoSocial` | string | Sim | Razão social completa |
| `apelido` | string | Sim | Nome curto para exibição |
| `padraoAntecipacao` | string | Sim | `A` \| `B` \| `C` \| `D` \| `E` (ver tabela abaixo) |

#### Padrões de Antecipação

Os padrões refletem as cinco metodologias reais observadas nos contratos da Proxys:

| Padrão | Metodologia | Banco de referência |
|--------|-------------|---------------------|
| `A` | Pro rata + break funding fee fixo + indenização | BB FINIMP |
| `B` | Cobra juros do período **total** contratado, sem desconto de juros futuros — antecipar **não** gera economia | Sicredi |
| `C` | Desconto a taxa de mercado (MTM) | FGI BV (PEAC) |
| `D` | Fórmula TLA BACEN — Resoluções 3401/06 e 3516/07 | Caixa Balcão |
| `E` | Pagamento ordinário com abatimento proporcional de juros futuros | Caixa prefixado |

**Responses:**
- `201 Created` — [BancoDto](./schemas.md#bancodto)
- `400 Bad Request` — Validação falhou (códigos COMPE inválidos, padrão fora do enum, etc.)
- `403 Forbidden` — Role insuficiente

---

### Atualizar Configuração de Antecipação

```
PUT /api/v1/bancos/{id}/config-antecipacao
Autorização: Admin
```

Atualiza exclusivamente as regras comerciais de antecipação do banco. Não altera razão social ou código COMPE.

**Path Parameters:**

| Parâmetro | Tipo | Descrição |
|-----------|------|-----------|
| `id` | guid | ID do banco |

**Request Body:**
```json
{
  "aceitaLiquidacaoTotal": true,
  "aceitaLiquidacaoParcial": true,
  "exigeAnuenciaExpressa": false,
  "exigeParcelaInteira": false,
  "avisoPrevioMinDiasUteis": 3,
  "padraoAntecipacao": "BREAKFUNDING",
  "valorMinimoParcialPct": 10.0,
  "breakFundingFeePct": 0.5,
  "tlaPctSobreSaldo": null,
  "tlaPctPorMesRemanescente": null,
  "observacoesAntecipacao": "Novo acordo a partir de 2026."
}
```

**Responses:**
- `200 OK` — [BancoDto](./schemas.md#bancodto) atualizado
- `400 Bad Request` — Validação falhou
- `404 Not Found` — Banco não encontrado
- `403 Forbidden` — Role insuficiente

---

## Campos de Configuração de Antecipação

| Campo | Descrição |
|-------|-----------|
| `aceitaLiquidacaoTotal` | O banco permite liquidação total antes do vencimento |
| `aceitaLiquidacaoParcial` | O banco permite amortização parcial |
| `exigeAnuenciaExpressa` | Exige confirmação formal por escrito do banco |
| `exigeParcelaInteira` | A amortização parcial deve ser exatamente uma parcela do cronograma |
| `avisoPrevioMinDiasUteis` | Dias úteis de antecedência exigidos para comunicar a antecipação |
| `padraoAntecipacao` | Metodologia padrão: `BREAKFUNDING`, `TLA`, ou `CUSTOM` |
| `valorMinimoParcialPct` | Valor mínimo de uma antecipação parcial, em % do saldo devedor |
| `breakFundingFeePct` | Fee de break funding cobrado pelo banco, em % |
| `tlaPctSobreSaldo` | Taxa de liquidação antecipada em % sobre o saldo devedor |
| `tlaPctPorMesRemanescente` | Taxa de liquidação antecipada em % por mês remanescente |
| `observacoesAntecipacao` | Observações livres sobre condições negociadas |
