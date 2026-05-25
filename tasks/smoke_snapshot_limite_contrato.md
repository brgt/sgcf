# Smoke Tests — S34 Snapshot Temporal de Garantias

> **Versão:** v1.0
> **Data:** 2026-05-25
> **Escopo:** Validação pós-deploy do S34 em ambiente real (staging primeiro, produção depois).
> **Pré-requisito:** migration `S34_SnapshotGarantiasContrato` aplicada com sucesso.

---

## 0. Sanity check pré-roteiro

```bash
# 1. Confirme que a migration está aplicada.
psql $DATABASE_URL -c "SELECT name FROM \"__EFMigrationsHistory\" WHERE name LIKE '%S34_SnapshotGarantiasContrato%';"
# Esperado: 1 linha com o nome completo da migration.

# 2. Confirme que a tabela nova existe.
psql $DATABASE_URL -c "\dt sgcf.garantia_exigida_revisao"
# Esperado: tabela listada com colunas (id, tenant_id, limite_banco_id, vigencia_inicio, vigencia_fim, ...).

# 3. Confirme que garantia_exigida_limite NÃO existe mais.
psql $DATABASE_URL -c "\dt sgcf.garantia_exigida_limite"
# Esperado: "Did not find any relation named 'sgcf.garantia_exigida_limite'."

# 4. Confirme que garantia_exigida_item existe.
psql $DATABASE_URL -c "\dt sgcf.garantia_exigida_item"
# Esperado: tabela listada.

# 5. Confirme as 3 colunas novas em contrato.
psql $DATABASE_URL -c "\d sgcf.contrato" | grep -E "limite_banco_id|limite_global_banco_id|garantias_exigidas_revisao_id"
# Esperado: 3 linhas, todas uuid NULLABLE.
```

Se qualquer um falhar: **abortar deploy** e investigar antes de prosseguir.

---

## 1. Validação de backfill

```sql
-- 1.1. Itens sem revisao_id (deve ser 0).
SELECT COUNT(*) FROM sgcf.garantia_exigida_item WHERE revisao_id IS NULL;
-- Esperado: 0.

-- 1.2. Revisões iniciais geradas (deve haver 1 por LimiteBanco que tinha itens).
SELECT COUNT(*) FROM sgcf.garantia_exigida_revisao
WHERE motivo = 'Revisão inicial gerada pela migration S34';
-- Esperado: igual a SELECT COUNT(DISTINCT limite_banco_id) FROM (previa) garantia_exigida_limite.

-- 1.3. Toda revisão inicial está vigente (vigencia_fim IS NULL).
SELECT COUNT(*) FROM sgcf.garantia_exigida_revisao
WHERE motivo = 'Revisão inicial gerada pela migration S34' AND vigencia_fim IS NOT NULL;
-- Esperado: 0.

-- 1.4. Continuidade limite_banco × itens × revisão.
SELECT lb.id AS limite_id, COUNT(DISTINCT gei.id) AS itens, COUNT(DISTINCT ger.id) AS revisoes
FROM sgcf.limite_banco lb
LEFT JOIN sgcf.garantia_exigida_revisao ger ON ger.limite_banco_id = lb.id
LEFT JOIN sgcf.garantia_exigida_item gei ON gei.revisao_id = ger.id
GROUP BY lb.id
HAVING COUNT(DISTINCT gei.id) > 0
LIMIT 5;
-- Esperado: cada linha com revisoes >= 1 (pelo menos a revisão inicial).
```

---

## 2. Validação de RLS

```bash
# Sem app.tenant_id setado: deve retornar 0 linhas (RLS bloqueia).
psql $DATABASE_URL -c "SELECT COUNT(*) FROM sgcf.garantia_exigida_revisao;"
# Esperado: 0.

# Com tenant válido: deve retornar as revisões daquele tenant.
psql $DATABASE_URL <<'EOF'
SET LOCAL app.tenant_id = '<UUID DO TENANT DE STAGING>';
SELECT COUNT(*) FROM sgcf.garantia_exigida_revisao;
EOF
# Esperado: > 0.
```

---

## 3. Smoke tests via API HTTP

Pré-requisito: token JWT válido para um usuário com policy `Leitura` (e `Admin` para os tests de mutação). Substitua `$BASE_URL`, `$TOKEN`, `$LIMITE_ID`, `$BANCO_ID`, `$COTACAO_ID` conforme o ambiente.

### 3.1. GET /revisoes-garantias retorna histórico

```bash
curl -sS -H "Authorization: Bearer $TOKEN" \
  "$BASE_URL/api/v1/limites-banco/$LIMITE_ID/revisoes-garantias" | jq .
```

**Esperado:**
- `200 OK`.
- Body com `limiteBancoId` igual ao path param.
- Array `revisoes[]` com ≥ 1 item.
- Revisão mais recente com `vigenciaFim: null`.
- Cada revisão com `itens[]` (pode ser vazio se a política era "sem exigências").

### 3.2. PATCH gera nova revisão

```bash
# Capture o número de revisões atual.
COUNT_BEFORE=$(curl -sS -H "Authorization: Bearer $TOKEN" \
  "$BASE_URL/api/v1/limites-banco/$LIMITE_ID/revisoes-garantias" \
  | jq '.revisoes | length')

# Aplique PATCH alterando garantias (use payload diferente do atual).
curl -sS -X PATCH -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  "$BASE_URL/api/v1/limites-banco/$LIMITE_ID" \
  -d '{
    "garantiasExigidas": [
      { "tipo": "Aval", "obrigatoria": true, "percentualSobreLimite": null, "valorFixoBrl": null, "observacoes": "Smoke test" }
    ]
  }'

# Verifique que agora há 1 revisão a mais.
COUNT_AFTER=$(curl -sS -H "Authorization: Bearer $TOKEN" \
  "$BASE_URL/api/v1/limites-banco/$LIMITE_ID/revisoes-garantias" \
  | jq '.revisoes | length')

echo "Antes: $COUNT_BEFORE | Depois: $COUNT_AFTER"
```

**Esperado:** `COUNT_AFTER == COUNT_BEFORE + 1`. A revisão anterior agora tem `vigenciaFim` preenchido com timestamp igual ao `vigenciaInicio` da nova.

### 3.3. Idempotência de PATCH (SLB-04)

Reaplique o mesmo PATCH (mesma lista de garantias).

```bash
curl -sS -X PATCH -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  "$BASE_URL/api/v1/limites-banco/$LIMITE_ID" \
  -d '{
    "garantiasExigidas": [
      { "tipo": "Aval", "obrigatoria": true, "percentualSobreLimite": null, "valorFixoBrl": null, "observacoes": "Smoke test" }
    ]
  }'

# Quantidade de revisões deve permanecer igual.
COUNT_AFTER_2=$(curl -sS -H "Authorization: Bearer $TOKEN" \
  "$BASE_URL/api/v1/limites-banco/$LIMITE_ID/revisoes-garantias" \
  | jq '.revisoes | length')

echo "Quantidade após 2º PATCH idêntico: $COUNT_AFTER_2 (esperado: $COUNT_AFTER)"
```

**Esperado:** `COUNT_AFTER_2 == COUNT_AFTER` (nenhuma nova revisão criada).

### 3.4. Enforcement bloqueia conversão sem garantia obrigatória

Pré-requisito: cotação aprovada de modalidade cujo `LimiteBanco` tem revisão vigente com ao menos 1 item obrigatório.

```bash
# Tente converter sem informar garantias compatíveis.
RESPONSE=$(curl -sS -X POST -w "\n%{http_code}" -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  "$BASE_URL/api/v1/cotacoes/$COTACAO_ID/converter" \
  -d '{
    "numeroExternoContrato": "SMOKE-S34-001",
    "dataContratacao": "2026-05-25",
    "dataVencimento": "2027-05-25",
    "taxaAa": 12.5,
    "garantias": []
  }')

echo "$RESPONSE"
```

**Esperado:**
- HTTP `409 Conflict`.
- Body com `type: "https://sgcf.io/errors/garantia-exigida-nao-coberta"`.
- `lacunas[]` lista cada garantia obrigatória sem cobertura com `valorEsperadoBrl` e `valorCobertoBrl`.

### 3.5. Conversão com cobertura completa sucede + contrato carrega snapshot

```bash
# Converter com garantias suficientes.
CONTRATO_RESP=$(curl -sS -X POST -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  "$BASE_URL/api/v1/cotacoes/$COTACAO_ID/converter" \
  -d '{
    "numeroExternoContrato": "SMOKE-S34-002",
    "dataContratacao": "2026-05-25",
    "dataVencimento": "2027-05-25",
    "taxaAa": 12.5,
    "garantias": [
      { "tipo": "Aval", "valorBrl": 1000000.00 }
    ]
  }')

CONTRATO_ID=$(echo "$CONTRATO_RESP" | jq -r .id)
echo "Contrato criado: $CONTRATO_ID"

# GET detalhe deve retornar FKs preenchidos + snapshot.
curl -sS -H "Authorization: Bearer $TOKEN" \
  "$BASE_URL/api/v1/contratos/$CONTRATO_ID" \
  | jq '{ limiteBancoId, limiteGlobalBancoId, garantiasExigidasRevisaoId, garantiasExigidasSnapshot }'
```

**Esperado:**
- `201 Created` na conversão.
- `GET /contratos/{id}` retorna `limiteBancoId`, `garantiasExigidasRevisaoId` e `garantiasExigidasSnapshot[]` populados.
- `limiteGlobalBancoId` populado se o banco tem `LimiteGlobalBanco` vigente; senão `null`.

### 3.6. Contrato legado retorna FKs como null

Localize um contrato criado **antes** do deploy de S34 (qualquer contrato com `created_at < <data_deploy>`).

```bash
curl -sS -H "Authorization: Bearer $TOKEN" \
  "$BASE_URL/api/v1/contratos/$CONTRATO_LEGADO_ID" \
  | jq '{ limiteBancoId, limiteGlobalBancoId, garantiasExigidasRevisaoId, garantiasExigidasSnapshot }'
```

**Esperado:** todos os 4 campos são `null` (sem backfill retroativo). Sem erro.

### 3.7. Listagem GET /contratos não inclui snapshot

```bash
curl -sS -H "Authorization: Bearer $TOKEN" \
  "$BASE_URL/api/v1/contratos?limit=5" \
  | jq '.items[0] | { limiteBancoId, garantiasExigidasSnapshot }'
```

**Esperado:**
- `limiteBancoId` presente (pode ser `null` se for legado).
- `garantiasExigidasSnapshot` ausente ou `null` (payload pesado fica restrito ao detalhe).

### 3.8. Query forense — política em data X

Sem endpoint dedicado (fora de escopo nesta fase), use SQL direto:

```sql
-- "Qual era a política do LimiteBanco X em 2026-04-01?"
SELECT r.id, r.vigencia_inicio, r.vigencia_fim, r.motivo,
       json_agg(json_build_object(
         'tipo', i.tipo,
         'obrigatoria', i.obrigatoria,
         'percentual', i.percentual_sobre_limite,
         'valorFixoBrl', i.valor_fixo_brl
       )) AS itens
FROM sgcf.garantia_exigida_revisao r
LEFT JOIN sgcf.garantia_exigida_item i ON i.revisao_id = r.id
WHERE r.limite_banco_id = '<LIMITE_BANCO_ID>'
  AND r.vigencia_inicio <= '2026-04-01T00:00:00Z'
  AND (r.vigencia_fim IS NULL OR r.vigencia_fim > '2026-04-01T00:00:00Z')
GROUP BY r.id;
```

**Esperado:** retorna exatamente 1 linha — a revisão que estava em vigor em 2026-04-01.

---

## 4. Monitoramento pós-deploy (primeiras 24h)

- **Métrica de bloqueio**: contar `429`+`409` retornados pelo endpoint `POST /cotacoes/{id}/converter` agrupado por `type=https://sgcf.io/errors/garantia-exigida-nao-coberta`. Spike inesperado indica que operadores estavam habituados a contornar políticas que agora bloqueiam — escalar com produto/risco antes de afrouxar.
- **Performance**: monitorar latência média do endpoint `POST /converter`. Lookup adicional (LimiteBanco + LimiteGlobal + revisão) deve adicionar ≤ 50ms; alertar acima disso.
- **Latência GET /contratos/{id}**: include do snapshot pode aumentar latência em ~10–30ms; monitorar p95.
- **Volume de revisões**: dashboard que mostra `COUNT(*) FROM garantia_exigida_revisao GROUP BY DATE(registrado_em)` para detectar padrões anormais (ex.: PATCHes em sequência sem motivo).

---

## 5. Rollback strategy

**A migration `S34_SnapshotGarantiasContrato` é forward-only em produção.** O `Down()` existe mas é destrutivo — perderia todas as revisões e snapshots.

Em caso de incidente:
1. **Reverter binário** para a versão anterior ao deploy (mantém migration aplicada).
2. Binário antigo lê apenas tabelas `garantia_exigida_item` (renomeada de `garantia_exigida_limite`) e contrato com colunas novas — schemas compatíveis com SQL DML antigo se o binário não souber dos campos novos (ele os ignora).
3. Investigar root cause.
4. Forward-fix em uma nova migration `S35_*`.

**Não rodar `Down()` em produção** salvo decisão explícita do time de risco + backup completo prévio.

---

## 6. Sinais de sucesso

Marque como passed quando:

- [ ] Todas as 8 queries SQL da seção 0 + 1 + 2 retornam o esperado.
- [ ] Os 7 cenários HTTP (3.1 a 3.7) retornam o esperado.
- [ ] Query forense (3.8) retorna exatamente 1 revisão.
- [ ] Monitoramento de 24h não mostra anomalias.

Em caso de falha em qualquer item, abrir incidente com:
- Comando exato executado
- Output observado
- Output esperado
- ID dos recursos envolvidos (limite, contrato, cotação, tenant)
