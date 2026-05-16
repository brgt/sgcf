# Cotações API

**Base route:** `/api/v1/cotacoes`

Gerencia cotações de captação financeira: registro de propostas recebidas de múltiplos bancos, comparação lado a lado (taxa nominal, CET e custo total equivalente em BRL), aceitação de proposta vencedora e conversão em contrato com mensuração de economia negociada.

> **MVP:** modalidade `Finimp`. Demais modalidades serão habilitadas em ondas futuras conforme [SPEC §1](../specs/cotacoes/SPEC.md).

---

## Ciclo de Vida

```
Rascunho → EmCaptacao → Comparada → Aceita → Convertida
                            ↘ Recusada
```

| De | Para | Comando |
|----|------|---------|
| `Rascunho` | `EmCaptacao` | `POST /{id}/enviar` |
| `Rascunho` | `Recusada` | `POST /{id}/cancelar` |
| `EmCaptacao` | `Comparada` | `POST /{id}/encerrar-captacao` |
| `EmCaptacao` | `Recusada` | `POST /{id}/cancelar` |
| `Comparada` | `Aceita` | `POST /{id}/propostas/{propostaId}/aceitar` |
| `Comparada` | `Recusada` | `POST /{id}/cancelar` |
| `Aceita` | `Convertida` | `POST /{id}/converter-em-contrato` |
| `Aceita` | `Comparada` | `POST /{id}/propostas/{propostaId}/desfazer-aceitacao` |

Estados finais (sem saída): `Convertida`, `Recusada`. Detalhes em [SPEC §4](../specs/cotacoes/SPEC.md).

---

## Endpoints — Cotações

### Listar Cotações

```
GET /api/v1/cotacoes
Autorização: Leitura
```

**Query Parameters:**

| Parâmetro | Tipo | Descrição |
|-----------|------|-----------|
| `status` | string | Filtra por [StatusCotacao](#statuscotacao) |
| `modalidade` | string | Filtra por [ModalidadeContrato](./schemas.md#modalidadecontrato) (MVP: `Finimp`) |
| `desde` | date | Data mínima de abertura (`YYYY-MM-DD`) |
| `ate` | date | Data máxima de abertura (`YYYY-MM-DD`) |
| `page` | int | Padrão `1` |
| `pageSize` | int | Padrão `20`, máx `100` |

**Response 200 OK:** `PagedResult<CotacaoDto>`

---

### Criar Cotação

```
POST /api/v1/cotacoes
Autorização: Escrita
```

Cria nova cotação em status `Rascunho`. Exige PTAX D-1 cadastrada na data útil anterior à `DataAbertura`.

**Request Body:**

```json
{
  "codigoInterno": "string | null",
  "modalidade": "Finimp",
  "valorAlvoBrl": 5000000.00,
  "prazoMaximoDias": 180,
  "dataAbertura": "2026-05-16",
  "observacoes": "string | null"
}
```

| Campo | Tipo | Obrigatório | Descrição |
|-------|------|-------------|-----------|
| `codigoInterno` | string | Não | Se omitido, gerado automaticamente no padrão `COT-YYYY-NNNN` |
| `modalidade` | string | Sim | MVP aceita apenas `Finimp` |
| `valorAlvoBrl` | decimal | Sim | > 0 |
| `prazoMaximoDias` | int | Sim | ≥ 1 |
| `dataAbertura` | date | Sim | Usada para resolver PTAX D-1 |
| `observacoes` | string | Não | Texto livre |

**Responses:**
- `201 Created` — [CotacaoDto](#cotacaodto)
- `400 Bad Request` — Validação falhou
- `409 Conflict` — PTAX não disponível para a data ou modalidade inválida

---

### Buscar Cotação por ID

```
GET /api/v1/cotacoes/{id}
Autorização: Leitura
```

Retorna detalhe completo incluindo todas as propostas e bancos-alvo.

**Responses:**
- `200 OK` — [CotacaoDto](#cotacaodto)
- `404 Not Found`

---

### Atualizar Cotação

```
PATCH /api/v1/cotacoes/{id}
Autorização: Escrita
```

Atualiza campos editáveis. Permitido **apenas em status `Rascunho`**.

**Request Body:**

```json
{
  "prazoMaximoDias": 180,
  "observacoes": "string"
}
```

| Campo | Tipo | Obrigatório | Descrição |
|-------|------|-------------|-----------|
| `prazoMaximoDias` | int? | Não | Quando informado, ≥ 1 |
| `observacoes` | string? | Não | — |

**Responses:**
- `200 OK` — [CotacaoDto](#cotacaodto)
- `404 Not Found`
- `409 Conflict` — Cotação não está em `Rascunho`

---

### Cancelar Cotação (DELETE)

```
DELETE /api/v1/cotacoes/{id}
Autorização: Escrita
```

Soft delete equivalente a `POST /{id}/cancelar` com motivo obrigatório.

**Request Body:**

```json
{ "motivo": "string" }
```

**Responses:**
- `204 No Content`
- `404 Not Found`
- `409 Conflict` — Já em status final (`Convertida` ou `Recusada`)

---

### Adicionar Banco-Alvo

```
POST /api/v1/cotacoes/{id}/bancos
Autorização: Escrita
```

Inclui banco-alvo na cotação. Valida limite operacional disponível: o `valorAlvoBrl` da cotação deve ser ≤ `ValorDisponivelBrl` do [LimiteBanco](./limites-banco.md) para a modalidade da cotação.

**Request Body:**

```json
{ "bancoId": "guid" }
```

**Responses:**
- `204 No Content`
- `404 Not Found` — Cotação ou banco inexistente
- `409 Conflict` — Banco já incluso, limite insuficiente ou status não permite alteração

---

### Remover Banco-Alvo

```
DELETE /api/v1/cotacoes/{id}/bancos/{bancoId}
Autorização: Escrita
```

Remove banco da lista. Permitido em `Rascunho` ou `EmCaptacao`.

**Responses:**
- `204 No Content`
- `404 Not Found`
- `409 Conflict`

---

### Enviar Cotação aos Bancos

```
POST /api/v1/cotacoes/{id}/enviar
Autorização: Escrita
```

Transição `Rascunho → EmCaptacao`. Exige ao menos um banco-alvo adicionado.

**Responses:**
- `204 No Content`
- `404 Not Found`
- `409 Conflict` — Status inválido ou sem bancos-alvo

---

### Encerrar Captação

```
POST /api/v1/cotacoes/{id}/encerrar-captacao
Autorização: Escrita
```

Transição manual `EmCaptacao → Comparada`. Trava o recebimento de novas propostas e habilita a aceitação.

**Responses:**
- `204 No Content`
- `404 Not Found`
- `409 Conflict`

---

### Cancelar Cotação

```
POST /api/v1/cotacoes/{id}/cancelar
Autorização: Escrita
```

Move para `Recusada` com motivo obrigatório.

**Request Body:**

```json
{ "motivo": "string" }
```

**Responses:**
- `204 No Content`
- `404 Not Found`
- `409 Conflict` — Cotação já em estado final

---

### Refresh de Mercado

```
POST /api/v1/cotacoes/{id}/refresh-mercado
Autorização: Escrita
```

Re-busca PTAX corrente, atualiza `PtaxUsadaUsdBrl` e invalida o cache de CET de todas as propostas. Disponível em `EmCaptacao` ou `Comparada`.

**Responses:**
- `200 OK` — [CotacaoDto](#cotacaodto) atualizado
- `404 Not Found`
- `409 Conflict` — Status incompatível

---

### Comparativo de Propostas

```
GET /api/v1/cotacoes/{id}/comparativo
Autorização: Leitura
```

Retorna a tabela comparativa lado a lado com três métricas por proposta (SPEC §5.3):

1. **Taxa nominal anualizada** — `taxaAa + spreadAa`.
2. **CET** — Custo Efetivo Total calculado.
3. **Custo Total Equivalente em BRL** — fluxo trazido a valor presente via CDI, equalizando prazos.

**Response 200 OK:** `ComparativoDto[]` — ver [Schemas](#comparativodto).

---

### Trilha de Auditoria

```
GET /api/v1/cotacoes/{id}/auditoria
Autorização: Auditoria
```

Retorna todas as transições de estado, ator e timestamp. Reaproveita o esquema [AuditEventoDto](./auditoria.md#auditeventodto) filtrado por `entity = "Cotacao"` e `entityId = {id}`.

---

### Relatório de Economia por Período

```
GET /api/v1/cotacoes/economia
Autorização: Leitura
```

Agregado de economia negociada por mês e por banco no período. Calculada na conversão de cada cotação aceita em contrato (SPEC §5.2).

**Query Parameters:**

| Parâmetro | Tipo | Obrigatório | Descrição |
|-----------|------|-------------|-----------|
| `de` | string | Sim | Mês inicial no formato `YYYY-MM` |
| `ate` | string | Sim | Mês final no formato `YYYY-MM` |
| `bancoId` | guid | Não | Filtra subtotais por banco específico |

**Response 200 OK:** [EconomiaPeriodoDto](#economiaperiododto)

**Erros:**
- `400 Bad Request` — `de` ou `ate` em formato inválido

---

## Endpoints — Propostas

### Registrar Proposta

```
POST /api/v1/cotacoes/{id}/propostas
Autorização: Escrita
```

Registra proposta recebida de um banco e calcula o CET automaticamente. O `cotacaoId` no corpo é ignorado — vale o da rota.

**Request Body:**

```json
{
  "bancoId": "guid",
  "moedaOriginal": "Usd",
  "valorOferecido": 1000000.00,
  "taxaAa": 5.25,
  "iofPct": 0.38,
  "spreadAa": 1.2,
  "prazoDias": 180,
  "estruturaAmortizacao": "Bullet",
  "periodicidadeJuros": "Bullet",
  "exigeNdf": true,
  "custoNdfAa": 0.85,
  "garantiaExigida": "CDB cativo 100% do principal",
  "valorGarantiaBrl": 5000000.00,
  "garantiaEhCdbCativo": true,
  "rendimentoCdbAa": 11.5
}
```

| Campo | Tipo | Obrigatório | Descrição |
|-------|------|-------------|-----------|
| `bancoId` | guid | Sim | Deve estar entre os bancos-alvo da cotação |
| `moedaOriginal` | string | Sim | [Moeda](./schemas.md#moeda) — `Brl`, `Usd`, `Eur`, `Cny`, `Jpy` |
| `valorOferecido` | decimal | Sim | > 0, na `moedaOriginal` |
| `taxaAa` | decimal | Sim | % a.a., ≥ 0 |
| `iofPct` | decimal | Sim | % sobre principal, ≥ 0 |
| `spreadAa` | decimal | Sim | % a.a., ≥ 0 |
| `prazoDias` | int | Sim | ≥ 1 |
| `estruturaAmortizacao` | string | Sim | `Bullet` \| `Price` \| `Sac` |
| `periodicidadeJuros` | string | Sim | `Bullet` \| `Mensal` \| `Bimestral` \| `Trimestral` \| `Semestral` \| `Anual` |
| `exigeNdf` | bool | Sim | Se true, `custoNdfAa` é obrigatório |
| `custoNdfAa` | decimal? | Condicional | Custo do hedge NDF em % a.a. |
| `garantiaExigida` | string | Sim | Descrição livre |
| `valorGarantiaBrl` | decimal | Sim | Valor estimado em BRL |
| `garantiaEhCdbCativo` | bool | Sim | Se true, `rendimentoCdbAa` é obrigatório |
| `rendimentoCdbAa` | decimal? | Condicional | Rendimento do CDB cativo em % a.a. |

**Responses:**
- `201 Created` — [PropostaDto](#propostadto) com `cetCalculadoAaPercentual` e `valorTotalEstimadoBrl` preenchidos
- `400 Bad Request` — Validação falhou
- `404 Not Found` — Cotação não encontrada
- `409 Conflict` — Cotação não está em `EmCaptacao`; banco não é alvo da cotação

---

### Atualizar Proposta

```
PATCH /api/v1/cotacoes/{id}/propostas/{propostaId}
Autorização: Escrita
```

Substitui a proposta existente e recalcula o CET. Permitido **apenas se a proposta está em status `Recebida`**. Os IDs no corpo são ignorados — valem os da rota.

**Request Body:** mesmos campos de [Registrar Proposta](#registrar-proposta) (exceto `bancoId`, que é imutável).

**Responses:**
- `200 OK` — [PropostaDto](#propostadto)
- `400 Bad Request`
- `404 Not Found`
- `409 Conflict` — Proposta não está em `Recebida`

---

### Aceitar Proposta

```
POST /api/v1/cotacoes/{id}/propostas/{propostaId}/aceitar
Autorização: Escrita
```

Aceita a proposta e move a cotação para `Aceita`. Apenas **uma** proposta por cotação pode estar aceita. Registra `AceitaPor` (claim `sub` do JWT) e `DataAceitacao`. Exige cotação em status `Comparada`.

**Responses:**
- `204 No Content`
- `404 Not Found`
- `409 Conflict` — Cotação não está em `Comparada`, ou outra proposta já aceita

---

### Desfazer Aceitação

```
POST /api/v1/cotacoes/{id}/propostas/{propostaId}/desfazer-aceitacao
Autorização: Escrita
```

Reverte a aceitação: `Aceita → Comparada`. Permitido apenas se a cotação **ainda não foi convertida** em contrato. O `propostaId` na URL é validado mas o comando opera na cotação (que possui no máximo uma proposta aceita).

**Responses:**
- `204 No Content`
- `404 Not Found`
- `409 Conflict` — Cotação já convertida

---

### Converter em Contrato

```
POST /api/v1/cotacoes/{id}/converter-em-contrato
Autorização: Escrita
```

Cria atomicamente um novo `Contrato` a partir da proposta aceita, registra um `EconomiaNegociacao` (snapshot imutável + economia bruta + economia ajustada por CDI) e atualiza o `ValorUtilizadoBrl` do limite do banco. Move a cotação para `Convertida`.

**Request Body:**

```json
{
  "numeroExternoContrato": "FINIMP-2026-001",
  "codigoInternoContrato": "string | null",
  "dataContratacao": "2026-05-20",
  "dataVencimento": "2026-11-16",
  "taxaAa": 5.10,
  "observacoes": "string | null",
  "rofNumero": "string | null",
  "exportadorNome": "string | null",
  "exportadorPais": "string | null",
  "produtoImportado": "string | null"
}
```

| Campo | Tipo | Obrigatório | Descrição |
|-------|------|-------------|-----------|
| `numeroExternoContrato` | string | Sim | Número do banco |
| `codigoInternoContrato` | string | Não | Gerado se omitido |
| `dataContratacao` | date | Sim | — |
| `dataVencimento` | date | Sim | > `dataContratacao` |
| `taxaAa` | decimal | Sim | Taxa final negociada |
| `observacoes` | string | Não | — |
| `rofNumero` | string | Não | Específico FINIMP |
| `exportadorNome` | string | Não | Específico FINIMP |
| `exportadorPais` | string | Não | Específico FINIMP |
| `produtoImportado` | string | Não | Específico FINIMP |

**Responses:**
- `201 Created` — [ContratoDto](./contratos.md#contratodto)
- `400 Bad Request`
- `404 Not Found`
- `409 Conflict` — Cotação não está em `Aceita`

---

## Schemas

### CotacaoDto

```json
{
  "id": "guid",
  "codigoInterno": "string",
  "modalidade": "Finimp",
  "valorAlvoBrl": 5000000.00,
  "prazoMaximoDias": 180,
  "dataAbertura": "YYYY-MM-DD",
  "dataPtaxReferencia": "YYYY-MM-DD",
  "ptaxUsadaUsdBrl": 5.1234,
  "status": "Rascunho | EmCaptacao | Comparada | Aceita | Convertida | Recusada",
  "propostaAceitaId": "guid | null",
  "contratoGeradoId": "guid | null",
  "aceitaPor": "string | null",
  "dataAceitacao": "DateTimeOffset | null",
  "observacoes": "string | null",
  "createdAt": "DateTimeOffset",
  "updatedAt": "DateTimeOffset",
  "bancosAlvo": ["guid"],
  "propostas": ["PropostaDto"]
}
```

### PropostaDto

```json
{
  "id": "guid",
  "cotacaoId": "guid",
  "bancoId": "guid",
  "moedaOriginal": "Brl | Usd | Eur | Jpy | Cny",
  "valorOferecidoMoedaOriginal": 1000000.00,
  "taxaAaPercentual": 5.25,
  "iofPercentual": 0.38,
  "spreadAaPercentual": 1.20,
  "prazoDias": 180,
  "estruturaAmortizacao": "Bullet | Price | Sac",
  "periodicidadeJuros": "Bullet | Mensal | Bimestral | Trimestral | Semestral | Anual",
  "exigeNdf": true,
  "custoNdfAaPercentual": 0.85,
  "garantiaExigida": "string",
  "valorGarantiaExigidaBrl": 5000000.00,
  "garantiaEhCdbCativo": true,
  "rendimentoCdbAaPercentual": 11.5,
  "cetCalculadoAaPercentual": 8.42,
  "valorTotalEstimadoBrl": 5210000.00,
  "dataCaptura": "YYYY-MM-DD",
  "dataValidadeMercado": "YYYY-MM-DD | null",
  "status": "Recebida | Aceita | Recusada | Expirada",
  "motivoRecusa": "string | null"
}
```

### ComparativoDto

```json
{
  "propostaId": "guid",
  "bancoId": "guid",
  "moedaOriginal": "string",
  "prazoDias": 180,
  "taxaNominalAaPercentual": 6.45,
  "cetAaPercentual": 8.42,
  "custoTotalEquivalenteBrl": 5230000.00,
  "exigeNdf": true,
  "garantiaExigida": "string",
  "valorGarantiaExigidaBrl": 5000000.00,
  "status": "string"
}
```

### EconomiaPeriodoDto

```json
{
  "porMes": [
    {
      "ano": 2026,
      "mes": 5,
      "quantidadeOperacoes": 4,
      "economiaBrutaBrl": 120000.00,
      "economiaAjustadaCdiBrl": 118500.00
    }
  ],
  "porBanco": [
    {
      "bancoId": "guid",
      "quantidadeOperacoes": 2,
      "economiaBrutaBrl": 70000.00,
      "economiaAjustadaCdiBrl": 69200.00
    }
  ],
  "totalEconomiaBrutaBrl": 240000.00,
  "totalEconomiaAjustadaCdiBrl": 237800.00,
  "totalOperacoes": 8
}
```

### EconomiaNegociacaoDto

Snapshot imutável criado na conversão da cotação em contrato.

```json
{
  "id": "guid",
  "cotacaoId": "guid",
  "contratoId": "guid",
  "cetPropostaAaPercentual": 8.50,
  "cetContratoAaPercentual": 8.15,
  "economiaBrl": 15000.00,
  "economiaAjustadaCdiBrl": 14800.00,
  "dataReferenciaCdi": "YYYY-MM-DD",
  "createdAt": "DateTimeOffset"
}
```

---

## Enums

### StatusCotacao

| Valor | Descrição |
|-------|-----------|
| `Rascunho` | Cotação recém criada; permite edição livre e gestão de bancos-alvo |
| `EmCaptacao` | Enviada aos bancos; aceita registro de propostas |
| `Comparada` | Captação encerrada; aceita seleção/aceitação de proposta |
| `Aceita` | Proposta vencedora aceita; aguardando conversão em contrato |
| `Convertida` | Contrato gerado; `EconomiaNegociacao` registrada (estado final) |
| `Recusada` | Cotação cancelada com motivo (estado final) |

### StatusProposta

| Valor | Descrição |
|-------|-----------|
| `Recebida` | Proposta cadastrada; editável e elegível para aceitação |
| `Aceita` | Proposta vencedora; única por cotação |
| `Recusada` | Descartada explicitamente |
| `Expirada` | `dataValidadeMercado` ultrapassada |

---

## Cálculos

### CET (Custo Efetivo Total)

O CET é recalculado automaticamente em **toda** alteração da proposta. Algoritmo (SPEC §5.1):

1. Converter `valorAlvoBrl` para `moedaOriginal` usando `ptaxUsadaUsdBrl` (cross-rate via USD quando aplicável).
2. Projetar cronograma hipotético usando o motor de amortização (`Sgcf.Domain.Cronograma`) com `taxaAa + spreadAa`, `prazoDias`, `estruturaAmortizacao` e `periodicidadeJuros`.
3. Adicionar custos extras ao fluxo: IOF (sobre principal), custo NDF se `exigeNdf`, **subtrair** rendimento do CDB cativo se `garantiaEhCdbCativo`.
4. Converter cada fluxo projetado para BRL via PTAX.
5. Calcular TIR do fluxo em BRL → CET anualizado.

### Economia Negociada

Calculada uma única vez na conversão em contrato e armazenada em `EconomiaNegociacao` (SPEC §5.2):

- **Bruta:** `(CET_proposta − CET_contrato) × principal × (prazo/360)`
- **Ajustada por CDI:** quando os prazos diferem, comparação de VPL dos dois fluxos usando o snapshot de CDI da data de conversão (ver [CDI Snapshots](./cdi-snapshots.md)).

---

## Pré-Requisitos Operacionais

Antes de criar uma cotação:

1. **PTAX D-1** — deve existir cotação cambial cadastrada para o dia útil anterior à `dataAbertura` (caso contrário, retorna 409 na criação).
2. **CDI Snapshot** — necessário na data de conversão para cálculo da economia ajustada. Ver [CDI Snapshots API](./cdi-snapshots.md).
3. **LimiteBanco** — todos os bancos-alvo devem possuir limite vigente para a modalidade da cotação. Ver [Limites de Banco API](./limites-banco.md).

---

## Referências

- [SPEC do módulo](../specs/cotacoes/SPEC.md) — fonte autoritativa de invariantes, regras de negócio e cálculos.
- [Coleção Bruno — 10-Cotacoes](./collections/sgcf-api/10-Cotacoes/) — 18 requests prontos cobrindo todo o fluxo.
- [Limites de Banco](./limites-banco.md) — aggregate independente usado na validação dos bancos-alvo.
- [CDI Snapshots](./cdi-snapshots.md) — taxa CDI diária usada no cálculo de economia.
- [Contratos](./contratos.md) — contrato gerado por `POST /converter-em-contrato`.
