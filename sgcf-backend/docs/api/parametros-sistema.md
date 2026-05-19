# Parâmetros do Sistema API

**Base route:** `/api/v1/parametros-sistema`

Gerencia os parâmetros globais de configuração do sistema. No MVP, expõe o **tetão mensal de capacidade** — limite de movimentação financeira mensal (captações + amortizações) que, quando ultrapassado, dispara alertas não-bloqueantes no Quadro da Dívida.

> **Implementação:** Fase 3 Task 3.4 — D-11 / Q8. Migration `S10_ParametroSistemaTetao`.

---

## Endpoints

### Obter Parâmetros do Sistema

```
GET /api/v1/parametros-sistema
Autorização: Leitura
```

Retorna os parâmetros globais configurados. O registro é um singleton criado automaticamente (get-or-create) na primeira consulta.

**Response 200 OK:** `ParametrosSistemaDto`

```json
{
  "tetaoMensalCapacidadeBrl": 50000000.00
}
```

Quando o tetão não está configurado:

```json
{
  "tetaoMensalCapacidadeBrl": null
}
```

---

### Atualizar Tetão Mensal

```
PATCH /api/v1/parametros-sistema/tetao-mensal
Autorização: Admin
```

Configura ou remove o tetão mensal de movimentação. O valor representa o limite em BRL da soma de captações e amortizações de principal em qualquer mês do Quadro da Dívida.

Quando o tetão está configurado e a soma `(totalCaptacaoMes + totalAmortizacaoMes)` de um mês excede o valor, o `QuadroDividaDto` inclui um alerta textual no array `alertas[]` identificando o mês ultrapassado. O alerta é não-bloqueante.

**Request Body:**

```json
{
  "valor": 50000000.00
}
```

Para **remover** o limite (sem restrição):

```json
{
  "valor": null
}
```

| Campo | Tipo | Obrigatório | Validação |
|-------|------|-------------|-----------|
| `valor` | decimal? | Sim | Deve ser positivo quando informado. `null` remove o limite |

**Response 200 OK:** `ParametrosSistemaDto`

```json
{
  "tetaoMensalCapacidadeBrl": 50000000.00
}
```

**Erros:**
- `400 Bad Request` — Valor negativo ou zero

---

## Schema

### ParametrosSistemaDto

```json
{
  "tetaoMensalCapacidadeBrl": "decimal | null"
}
```

| Campo | Tipo | Descrição |
|-------|------|-----------|
| `tetaoMensalCapacidadeBrl` | decimal? | Tetão mensal de movimentação em BRL. `null` quando não configurado (sem limite) |

---

## Relação com o Quadro da Dívida

Quando `tetaoMensalCapacidadeBrl` está configurado, o handler `GetQuadroDividaQueryHandler` invoca `ValidadorTetaoMensal` (função pura) para cada mês da projeção. Para cada mês em que:

```
totalCaptacaoMes + totalAmortizacaoMes > tetaoMensalCapacidadeBrl
```

o sistema adiciona uma entrada no array `alertas[]` do `QuadroDividaDto` com o formato:

```
"Mês {M}/{YYYY}: movimentação prevista de R$ {valor} excede o tetão mensal de R$ {tetao}."
```

O alerta aplica-se tanto ao quadro com dados reais quanto ao quadro com cenário de simulação aplicado.
