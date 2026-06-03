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

### Consultar Limite Global Vigente do Banco

> **Adicionado em [0.10.0] (S33). Atualizado em [0.10.1] — semântica de vigência corrigida (Opção A).**

```
GET /api/v1/bancos/{bancoId}/limite-global-vigente
Autorização: Leitura
```

Retorna o limite global (guarda-chuva) vigente de um banco na data de hoje, com valores de utilização e disponibilidade calculados em tempo de consulta. Utilizar este endpoint para exibir o teto agregado do banco antes de iniciar uma cotação.

**Rota para o frontend (via proxy Vite):**

```
GET /sgcf-api/api/v1/bancos/{bancoId}/limite-global-vigente
```

O proxy Vite (porta 3000) reescreve o prefixo `/sgcf-api` para a URL base do backend. O path efetivo que chega ao backend é `/api/v1/bancos/{bancoId}/limite-global-vigente`.

**Path Parameters:**

| Parâmetro | Tipo | Restrição | Descrição |
|-----------|------|-----------|-----------|
| `bancoId` | guid | GUID v4/v7 válido (formato `xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx`) | ID do banco cujo limite vigente se deseja consultar |

**Autenticação e Autorização:**

- Requer token JWT válido no cabeçalho `Authorization: Bearer <token>`.
- O token deve satisfazer a policy `Leitura`. Qualquer role com permissão de leitura é aceita.
- Ausência de token ou policy não atendida resultam em `401 Unauthorized`.

**Exemplo de requisição (curl):**

```bash
curl -X GET \
  "http://localhost:3000/sgcf-api/api/v1/bancos/a1b2c3d4-e5f6-7890-abcd-ef1234567890/limite-global-vigente" \
  -H "Authorization: Bearer <seu_token>"
```

**Exemplo de resposta 200 OK:**

```json
{
  "id": "f47ac10b-58cc-4372-a567-0e02b2c3d479",
  "bancoId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "valorLimiteBrl": 15000000.00,
  "valorUtilizadoBrl": 7200000.00,
  "valorDisponivelBrl": 7800000.00,
  "regime": "GlobalPuro",
  "dataVigenciaInicio": "2026-01-01",
  "dataVigenciaFim": null,
  "observacoes": "Linha aprovada em comitê BB de 21/05/2026",
  "createdAt": "2026-01-15T10:30:00+00:00",
  "updatedAt": "2026-05-21T14:00:00+00:00",
  "historico": [
    {
      "id": "c9d8e7f6-5a4b-3c2d-1e0f-a1b2c3d4e5f6",
      "limiteGlobalBancoId": "f47ac10b-58cc-4372-a567-0e02b2c3d479",
      "valorAnteriorBrl": null,
      "valorNovoBrl": 15000000.00,
      "registradoEm": "2026-01-15T10:30:00+00:00",
      "observacoes": "Criação do limite global"
    }
  ]
}
```

**Campos do corpo de resposta (`LimiteGlobalBancoVigenteDto`):**

| Campo | Tipo | Descrição |
|-------|------|-----------|
| `id` | guid | ID do registro `LimiteGlobalBanco` |
| `bancoId` | guid | ID do banco ao qual o limite pertence |
| `valorLimiteBrl` | decimal | Teto agregado concedido pelo banco, em BRL |
| `valorUtilizadoBrl` | decimal | Valor atualmente utilizado, calculado em tempo de consulta (ver nota de regime abaixo) |
| `valorDisponivelBrl` | decimal | `max(0, valorLimiteBrl − valorUtilizadoBrl)` |
| `regime` | string | `"GlobalPuro"` ou `"PerModalidade"` — indica o regime operacional detectado para o banco |
| `dataVigenciaInicio` | date (`YYYY-MM-DD`) | Data de início da vigência do limite |
| `dataVigenciaFim` | date (`YYYY-MM-DD`) \| null | Data de fim da vigência; `null` indica vigência em aberto (sem data de encerramento programada) |
| `observacoes` | string \| null | Texto livre registrado no limite |
| `createdAt` | DateTimeOffset (ISO 8601) | Instante de criação do registro |
| `updatedAt` | DateTimeOffset (ISO 8601) | Instante da última atualização |
| `historico` | `LimiteGlobalBancoHistoricoDto[]` | Histórico de alterações de valor, ordenado do mais recente ao mais antigo |

**Campos de cada item em `historico` (`LimiteGlobalBancoHistoricoDto`):**

| Campo | Tipo | Descrição |
|-------|------|-----------|
| `id` | guid | ID do registro de histórico |
| `limiteGlobalBancoId` | guid | ID do limite global ao qual o histórico pertence |
| `valorAnteriorBrl` | decimal \| null | Valor anterior em BRL; `null` na entrada de criação |
| `valorNovoBrl` | decimal | Novo valor em BRL registrado nesta entrada |
| `registradoEm` | DateTimeOffset (ISO 8601) | Instante em que a alteração foi registrada |
| `observacoes` | string \| null | Motivo ou descrição da alteração |

**Nota sobre `regime` e `valorUtilizadoBrl`:**

O campo `valorUtilizadoBrl` é calculado dinamicamente conforme o regime do banco:
- `"GlobalPuro"` (Cenário A): o banco não possui limites por modalidade (`LimiteBanco`) ativos. O `valorUtilizadoBrl` é a soma dos saldos devedores de todos os contratos ativos do banco.
- `"PerModalidade"` (Cenário B): o banco possui ao menos um `LimiteBanco` ativo. O `valorUtilizadoBrl` é a soma dos campos `ValorUtilizadoBrl` de cada `LimiteBanco` vigente do banco.

**Definição de limite vigente (Opção A):**

Um `LimiteGlobalBanco` é considerado **vigente** quando sua janela de datas `[DataVigenciaInicio, DataVigenciaFim]` contém a data de hoje:

- `DataVigenciaInicio ≤ hoje`, **e**
- `DataVigenciaFim == null` (vigência em aberto) **ou** `DataVigenciaFim ≥ hoje`

Registros com `DataVigenciaFim` preenchido e anterior à data de hoje são tratados como **encerrados** e não são retornados por este endpoint.

**Tabela de códigos de status:**

| Status | Significado neste endpoint |
|--------|---------------------------|
| `200 OK` | Limite global vigente encontrado; corpo contém `LimiteGlobalBancoVigenteDto` |
| `401 Unauthorized` | Token ausente, expirado ou policy `Leitura` não atendida |
| `404 Not Found` | Nenhum limite global vigente existe para o banco na data de hoje — **não é erro de sistema**; trate como "banco sem limite global configurado" |

---

### Troubleshooting — Limite Global Vigente

Esta seção descreve os problemas reais relatados pelo time de frontend ao consumir este endpoint.

#### Caso 1 — `bancoId` vazio na URL resulta em 404

**Sintoma:** A requisição é disparada com a URL `…/bancos//limite-global-vigente` (segmento vazio). O backend retorna `404`.

**Causa:** O componente ou hook realizou a chamada antes de o `bancoId` estar disponível (estado inicial `null`, `undefined` ou string vazia). O roteador do ASP.NET Core não casa a rota quando o segmento GUID está ausente.

**Ação recomendada:** Nunca emita a chamada sem um GUID válido. Utilize um guard no watcher ou no hook de dados:

```typescript
// Exemplo com Vue 3 + Composables
const { data } = useQuery({
  queryKey: ['limite-global-vigente', bancoId],
  queryFn: () => fetchLimiteGlobalVigente(bancoId.value!),
  enabled: computed(() => !!bancoId.value && isValidGuid(bancoId.value)),
})

// Exemplo com React Query
const { data } = useQuery({
  queryKey: ['limite-global-vigente', bancoId],
  queryFn: () => fetchLimiteGlobalVigente(bancoId),
  enabled: !!bancoId && isValidGuid(bancoId),
})
```

Valide o formato GUID com a expressão regular `^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$` (case-insensitive) antes de ativar a query.

---

#### Caso 2 — `404` quando o banco não possui limite global vigente

**Sintoma:** O endpoint retorna `404` mesmo com um `bancoId` válido e token correto.

**Causa:** Este é um `404` de regra de negócio — o banco existe no sistema, mas não tem nenhum `LimiteGlobalBanco` com janela de datas que contenha a data de hoje. Não indica falha de sistema.

**Ação recomendada:** Trate o `404` deste endpoint de forma distinta de outros `404`. Exiba um estado vazio informativo (ex.: "Este banco não possui limite global vigente"), em vez de uma mensagem de erro genérica. Não registre o evento como falha em ferramentas de monitoramento de erros.

```typescript
async function fetchLimiteGlobalVigente(bancoId: string) {
  const response = await fetch(`/sgcf-api/api/v1/bancos/${bancoId}/limite-global-vigente`, {
    headers: { Authorization: `Bearer ${token}` },
  });

  if (response.status === 404) {
    return null; // banco sem limite configurado — exibe estado vazio
  }

  if (!response.ok) {
    throw new Error(`Erro inesperado: ${response.status}`);
  }

  return response.json();
}
```

---

#### Caso 3 — `401 Unauthorized`

**Sintoma:** O endpoint retorna `401` independentemente do `bancoId` informado.

**Causa:** Uma das situações abaixo:
- Token JWT ausente no cabeçalho `Authorization`.
- Token expirado (verificar `exp` no payload do JWT).
- Token válido, mas sem a claim ou role que satisfaz a policy `Leitura`.

**Diferença entre 401 e 404:**
- `401` é um problema de **autenticação ou autorização** — o servidor não identificou o usuário ou o usuário não tem permissão.
- `404` neste endpoint é um problema de **regra de negócio** (banco sem limite configurado) — não há relação com autenticação.

**Ação recomendada:**
- Verificar se o token foi incluído corretamente no cabeçalho (`Authorization: Bearer <token>`).
- Verificar a validade do token (campo `exp`). Se expirado, renovar via fluxo de autenticação.
- Se o token for válido mas o `401` persistir, verificar junto ao time de backend se o perfil do usuário possui a role com policy `Leitura`.

---

#### Caso 4 — `404` por tenant divergente

**Sintoma:** O endpoint retorna `404` para um `bancoId` que a equipe confirma que existe e possui limite global, mas a sessão autenticada pertence a um tenant diferente do proprietário do registro.

**Causa:** O sistema aplica um filtro global de tenant em todas as queries (via EF Core global filter). Registros pertencentes a outro tenant ficam invisíveis — a query retorna vazio, que o handler converte em `404`. Este comportamento é intencional e garante o isolamento multi-tenant.

**Ação recomendada:**
- Verificar o tenant da sessão autenticada (inspecionar o token JWT — campo `tenant_id` ou equivalente nas claims).
- Confirmar que o `bancoId` e o `LimiteGlobalBanco` pertencem ao mesmo tenant da sessão.
- Se houver dúvida sobre o tenant correto, contatar o administrador do sistema.

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
