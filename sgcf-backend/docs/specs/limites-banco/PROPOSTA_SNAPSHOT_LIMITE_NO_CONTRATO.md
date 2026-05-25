# Proposta — Snapshot Temporal do LimiteBanco no Contrato

> **Status:** Para discussão com o time de backend SGCF
> **Data:** 2026-05-25
> **Autor:** Investigação frontend (Welysson + Claude)
> **Tipo:** Modelagem de dados — requisição formal para discussão
> **Pertinência:** Backend SGCF (LimiteBanco, LimiteGlobalBanco, Contrato, GarantiaExigidaLimite)

---

## 1. Resumo executivo

O sistema atual mantém os contratos com garantias próprias (snapshot independente), mas **não preserva qual era a política do banco no momento da contratação**. Isso cria três lacunas:

1. **`LimiteBanco.garantiasExigidas` não tem histórico** — alterações são destrutivas (PATCH replace-all).
2. **`Contrato` não carrega `limiteBancoId`** — impossível reconstruir o contexto de aprovação.
3. **Frontend não enforça `garantiasExigidas`** ao criar contrato — confia 100% no backend.

Cenário real que motivou a investigação: bancos como Santander mudam exigências de garantia ao longo dos 15 anos de relacionamento. Contratos antigos sob regras antigas convivem com contratos novos sob regras novas — situação comum em tesouraria. Sem snapshot temporal, **auditoria e regulação não conseguem provar a política vigente em uma data passada**.

A decisão preliminar (escopo "Opção A+") é introduzir snapshot mínimo no contrato: FK + cópia imutável das `garantiasExigidas` vigentes no momento da contratação. Detalhes nas seções 4 e 5.

---

## 2. Cenário de negócio

### Narrativa do usuário (verbatim)

> "Temos relacionamento com bancos a mais de 15 anos. As garantias deles mudam conforme o tempo passa. Por exemplo, ano passado o Santander realizava operações de FINIMP sem a exigência de garantia. Imaginando um cenário onde tivéssemos 2 milhões contratados de FINIMP sem a necessidade de garantia, ainda para ser quitado, e agora contratássemos mais 2 milhões, porém, com 30% de garantia."

### Tabela do cenário

| Momento | Operação | Política do banco na época | Garantia ligada à operação |
| --- | --- | --- | --- |
| 2025 | Contrato A — FINIMP R$ 2M no Santander | Santander não exigia garantia | Nenhuma (histórico) |
| 2026 | Contrato B — FINIMP R$ 2M no Santander | Santander agora exige 30% | R$ 600 mil (snapshot da contratação) |

Os dois contratos coexistem ativos. O sistema **deve** distinguir corretamente as obrigações de cada um — sem reescrever a história do Contrato A só porque a política mudou.

---

## 3. Investigação no código frontend — evidências

Caminho-base: `/Users/welysson/projects/nordware-landing/src/modules/finance`

### 3.1. O que o sistema FAZ corretamente

**Contratos carregam snapshot próprio de garantias.**

`src/modules/finance/types/sgcf.types.ts:499-513`:
```ts
export interface GarantiaDto {
  id: string
  contratoId: string         // ← FK para o contrato; não para o LimiteBanco
  tipo: TipoGarantia
  valorMoedaOriginal: number
  // ...
  status: StatusGarantia
}
```

`src/modules/finance/types/sgcf.types.ts:191-219`:
```ts
export interface ContratoCreateInput {
  // ...
  garantias?: GarantiaInput[]   // ← lista enviada na criação, persistida como GarantiaDto[]
}
```

Quando o Contrato A é criado em 2025 sem garantias, ele nasce com `garantias: []`. Se em 2026 o `LimiteBanco.garantiasExigidas` do Santander/FINIMP for alterado, **o Contrato A continua com lista vazia** — não há referência viva, é cópia.

**Cotação tem mecanismo de "template" usando a política atual.**

`src/modules/finance/types/cotacoes.types.ts:196-208`:
```ts
/**
 * Template of guarantee data derived from the LimiteBanco's garantiasExigidas
 * and returned by POST /cotacoes/{id}/bancos when preencherGarantiaAutomaticamente
 * is true and the limit has at least one garantia exigida.
 * Mirror of GarantiaPreenchidaDto from sgcf-backend 0.6.0.
 * This is a suggestion for the future Proposta — it does not mutate the limit.
 */
export interface GarantiaPreenchidaDto {
  garantiaExigida: string
  percentualGarantiaExigidaSobreOperacao: number | null
  valorGarantiaExigidaBrl: number | null
  garantiaEhCdbCativo: boolean
}
```

Ou seja: ao adicionar um banco-alvo a uma cotação, o backend monta uma sugestão a partir da política atual. É template, não enforcement.

### 3.2. Lacuna 1 — `LimiteBanco.garantiasExigidas` não tem histórico

`src/modules/finance/types/limites-banco.types.ts:62-69`:
```ts
/**
 * Mirror of LimiteBancoHistoricoDto from sgcf-backend 0.6.0.
 * Returned inside LimiteBancoDto.historico on GET /limites-banco/{id},
 * ordered by registradoEm ascending.
 * valorAnteriorBrl is null on the initial creation entry.
 */
export interface LimiteBancoHistoricoDto {
  id: string
  limiteBancoId: string
  valorAnteriorBrl: number | null   // ← apenas mudanças de VALOR
  valorNovoBrl: number
  registradoEm: string
  observacoes: string | null
}
```

`src/modules/finance/types/limites-banco.types.ts:145-153`:
```ts
/**
 * PATCH body for PATCH /limites-banco/{id}.
 * (...)
 * garantiasExigidas semantics (SGCF 0.6.0):
 *   - null (or omitted) → preserve current guarantees unchanged
 *   - []               → remove all guarantees
 *   - [...items]       → replace-all with the provided list
 */
export interface AtualizarLimiteBancoInput {
  novoValorLimiteBrl?: number
  garantiasExigidas?: CriarGarantiaExigidaLimiteRequest[] | null
  // ...
}
```

**Confirmação**: o histórico registra apenas mudanças de `valorLimiteBrl`. Mudanças em `garantiasExigidas` são feitas com semântica **replace-all** e destroem a versão anterior. Não há `GarantiaExigidaLimiteHistorico` nem audit trail equivalente.

### 3.3. Lacuna 2 — `Contrato` não tem `limiteBancoId`

`src/modules/finance/types/sgcf.types.ts:74-94`:
```ts
export interface ContratoDto {
  id: string
  numeroExterno: string
  codigoInterno: string
  bancoId: string                     // ← só o banco
  modalidade: ModalidadeContrato      // ← só a modalidade
  moeda: Moeda
  valorPrincipal: number
  dataContratacao: string
  // ... (nenhum campo limiteBancoId / limiteGlobalBancoId)
  status: StatusContrato
  temHedge: boolean
  temGarantia: boolean
  contratoPaiId: string | null
  createdAt: string
  updatedAt: string
}
```

**Confirmação**: o contrato sabe a qual `bancoId + modalidade` pertence, mas não sabe qual instância específica de `LimiteBanco` (e qual snapshot de `garantiasExigidas`) estava em vigência quando ele foi assinado.

### 3.4. Lacuna 3 — Frontend não enforça `garantiasExigidas`

`src/modules/finance/composables/useContratoWizard.ts:126`:
```ts
const form = ref<WizardFormState>({
  // ...
  garantias: [],   // ← inicializa vazio, sem auto-fill
})
```

`src/modules/finance/composables/useContratoWizard.ts:249`:
```ts
// Garantias are optional — business rules per bank cannot be enforced client-side.
```

`src/modules/finance/components/contratos/wizard/StepGarantias.vue` — passo 5 do wizard é 100% manual. O operador adiciona garantias clicando em "+". Não há pré-preenchimento a partir do `LimiteBanco.garantiasExigidas` vigente, nem aviso quando o operador cria contrato com `garantias: []` enquanto a política do banco exige.

---

## 4. Matriz de risco do estado atual

| Aspecto do cenário do usuário | Como o sistema lida hoje | Risco |
| --- | --- | --- |
| Contrato A (sem garantia, 2025) e Contrato B (30% garantia, 2026) coexistirem ativos | OK — cada contrato carrega seu snapshot via `GarantiaDto[]` próprio | Baixo |
| Consultar o `LimiteBanco` do Santander e ver "30% obrigatório" | OK — é a regra atual | Baixo |
| Provar para auditoria/regulação que **em 2025 o Santander não exigia garantia** | **Impossível** — versão antiga foi sobrescrita pelo PATCH replace-all | **Alto** (compliance, BACEN, FATCA, etc.) |
| Saber qual era a política do banco quando o Contrato A foi assinado | **Impossível** — sem `limiteBancoId` no Contrato e sem `GarantiaExigidaLimiteHistorico` | **Alto** (forense, defesa em fiscalização) |
| Sistema impedir cadastro de Contrato B sem garantia hoje | **Depende exclusivamente do backend.** Frontend não bloqueia, só sugere via cotação. Comportamento real precisa ser auditado no SGCF. | Médio |
| Cálculo de exposição (R$ 4M) e garantia comprometida (R$ 600 mil) | OK — `GarantiaDto[]` por contrato somam corretamente | Baixo |
| Visualizar linha do tempo das políticas do banco | **Não existe** | Médio (UX/treasury operations) |

---

## 5. Opções de solução

### Opção A pura — Snapshot só do ID

Adicionar `limiteBancoId: string?` e `limiteGlobalBancoId: string?` ao `Contrato`. Backend grava o ID do limite vigente no momento da conversão cotação → contrato.

- **Resolve**: Lacuna 2 (rastreabilidade).
- **Não resolve**: Lacunas 1 e 3 — se o `LimiteBanco.garantiasExigidas` for alterado depois, a "regra histórica" continua perdida; consulta ao `LimiteBanco` apontado vai mostrar a regra atual, não a da época.
- **Custo backend**: baixo (FK simples, migration aditiva).
- **Custo frontend**: baixo (consumir o ID, exibir no detalhe).
- **Útil quando**: a operação aceita a limitação ou se combina com migração futura para imutabilidade.

### Opção A+ — Snapshot do ID + das garantias exigidas *(opção recomendada e decidida)*

Mesma adição de FKs, **mais** uma cópia imutável das `garantiasExigidas` vigentes no momento da contratação, persistida como campo do `Contrato` (ou entidade filha `ContratoLimiteSnapshot`).

- **Resolve**: Lacunas 1 (parcial — congela na contratação), 2 (total).
- **Não resolve**: Lacuna 3 (enforcement na criação — depende do backend hoje).
- **Custo backend**: médio. Schema novo (campo JSON ou tabela filha), preenchimento na conversão cotação→contrato, expor no GET `/contratos/{id}`.
- **Custo frontend**: médio. Exibir no detalhe + lógica de comparação "snapshot vs política atual".
- **Útil quando**: precisamos de audit trail acionável sem reescrever a modelagem do `LimiteBanco`. **Esta é a opção escolhida** — ver seção 6.

### Opção B — Versionamento explícito do `LimiteBanco`

Tornar `LimiteBanco` imutável após criação. Mudanças de garantia ou valor exigem **novo** `LimiteBanco` com `dataVigenciaInicio` nova. O antigo recebe `dataVigenciaFim` e fica como histórico consultável. Contratos linkam ao `LimiteBanco` ativo no momento.

- **Resolve**: Todas as 3 lacunas.
- **Custo backend**: alto. Mudança fundamental de regra (PATCH passa a criar registro novo automaticamente, ou é proibido em campos sensíveis). Migração de dados. Queries de "vigente em data X" ficam não-triviais.
- **Custo frontend**: médio-alto. Edição de limite vira "criar nova versão". UX muda.
- **Útil quando**: a operação tem maturidade para um modelo bi-temporal completo.

### Opção C — Audit trail completo

Adicionar `LimiteBancoHistorico` cobrindo todos os campos relevantes (não só `valorLimiteBrl`), incluindo `garantiasExigidas`. Permitir queries do tipo "qual era a política em data X". Manter `LimiteBanco` mutável mas com trilha completa.

- **Resolve**: Todas as 3 lacunas.
- **Custo backend**: alto. Modelagem temporal séria, queries específicas, possivelmente revisão do contrato da PATCH.
- **Custo frontend**: alto. UI nova para visualização da linha do tempo.
- **Útil quando**: o requisito é compliance/forense de longo prazo com visualização rica.

---

## 6. Escopo decidido — Opção A+

### 6.1. Decisões tomadas

| Pergunta | Decisão | Implicação |
| --- | --- | --- |
| Escopo do snapshot | **Opção A+**: `limiteBancoId` + cópia das `garantiasExigidas` no momento da contratação | Fecha a Lacuna 2 totalmente, a Lacuna 1 parcialmente (a partir da contratação) |
| Cota­ção, Contrato, ou ambos | **Só Contrato** | Snapshot ocorre na conversão cotação→contrato. Cotação fica fora. |
| Backfill de dados existentes | **`NULL` permitido, sem backfill** | Contratos antigos ficam com `limiteBancoId = null` e snapshot vazio. UI mostra "Sob limite (legado)". |
| UI surface no frontend Nordware | **A definir em sessão separada** | Backend não fica bloqueado por isso. |

### 6.2. Diferenças sobre LimiteGlobalBanco

O agregado `LimiteGlobalBanco` (SGCF S33) foi entregue recentemente — é o teto guarda-chuva do banco. Para consistência arquitetural, **a mesma regra deve se aplicar**: o `Contrato` deve carregar `limiteGlobalBancoId` (e o snapshot das `garantiasExigidas` do limite global, se houver campo análogo).

Ponto aberto: o `LimiteGlobalBanco` atual **não tem** o conceito de `garantiasExigidas` (vejo isso em `src/modules/finance/types/limite-global-banco.types.ts` — só `valorLimiteBrl` + vigência + histórico de valor). Portanto o snapshot relevante de garantias mora apenas no `LimiteBanco` per-modalidade. O `limiteGlobalBancoId` no contrato seria apenas referência de rastreabilidade.

---

## 7. Requisitos para o backend (Opção A+)

### 7.1. Schema

Adicionar ao `Contrato`:

```
limite_banco_id            UUID NULLABLE  REFERENCES limite_banco(id) ON DELETE SET NULL
limite_global_banco_id     UUID NULLABLE  REFERENCES limite_global_banco(id) ON DELETE SET NULL
garantias_exigidas_snapshot JSONB NULLABLE — cópia imutável das GarantiaExigidaLimite vigentes
                                             no LimiteBanco no momento da contratação
```

`NULL` é aceitável para:
- Contratos existentes (sem backfill).
- Contratos onde o banco não tem `LimiteBanco` cadastrado (cenário edge, hoje raro mas possível).

### 7.2. Quando preencher

Na conversão cotação → contrato (endpoint `POST /cotacoes/{cotacaoId}/converter` ou equivalente):

1. Buscar o `LimiteBanco` ativo do `(bancoId, modalidade)` na data da conversão.
2. Buscar o `LimiteGlobalBanco` ativo do `bancoId` na data da conversão (pode ser `null`).
3. Persistir os IDs em `limite_banco_id` e `limite_global_banco_id`.
4. Serializar `LimiteBanco.garantiasExigidas` (array completo) e gravar em `garantias_exigidas_snapshot`. O array é congelado — alterações posteriores no `LimiteBanco` original **não** se propagam.

### 7.3. Endpoint GET `/contratos/{id}` — campos novos

Estender o `ContratoDto` no contrato da API:

```ts
{
  // ... campos atuais ...
  limiteBancoId: string | null
  limiteGlobalBancoId: string | null
  garantiasExigidasSnapshot: GarantiaExigidaLimiteSnapshotDto[] | null
}

interface GarantiaExigidaLimiteSnapshotDto {
  tipo: TipoGarantiaLimite               // mesmo enum do GarantiaExigidaLimite
  percentualSobreLimite: number | null
  valorFixoBrl: number | null
  obrigatoria: boolean
  observacoes: string | null
  // sem id, sem createdAt — é cópia imutável, não tem identidade própria
}
```

### 7.4. Imutabilidade

`garantias_exigidas_snapshot`, uma vez gravado, **não deve** ser editável via PATCH. Considerar bloquear no domínio (não expor setter público, validar no application layer).

Atualizações no `LimiteBanco.garantiasExigidas` continuam funcionando com a semântica atual de PATCH replace-all — mas não tocam o snapshot já persistido nos contratos.

### 7.5. Comportamento na listagem

Endpoint `GET /contratos` (listagem): pode retornar `limiteBancoId` e `limiteGlobalBancoId` (FKs leves), mas **não** o array `garantiasExigidasSnapshot` (payload-pesado). Snapshot fica restrito ao detalhe `GET /contratos/{id}`.

### 7.6. Sem migração / backfill

Contratos pré-feature ficam com os três campos `NULL`. **Não rodar job de backfill** — concordância da operação é que o custo de tentar reconstruir o passado supera o benefício, e dados ambíguos são piores que dados ausentes.

---

## 8. Perguntas abertas para o time backend

1. **Concorda com a modelagem como `JSONB` no contrato vs uma tabela filha `ContratoLimiteSnapshot`?** JSONB é mais simples (não cria tabela nova, sem joins); tabela filha permite query e indexação. Recomendamos JSONB pela simplicidade — o snapshot é sempre consumido junto com o contrato.
2. **O `LimiteGlobalBanco` deve ganhar `garantiasExigidas` próprio no S34/futuro?** Hoje só `LimiteBanco` per-modalidade tem garantias — se houver intenção de migrar para uma estrutura onde garantias vivem no nível global, este spec precisa ser revisitado.
3. **Existe regra de auditoria/regulação específica que determine *quanto tempo* o snapshot precisa ser retido?** BACEN normalmente exige 5 anos. JSONB no contrato preserva enquanto o contrato existir; tabela separada permitiria políticas de retenção independentes.
4. **Algum endpoint análogo a "qual era a política do banco em data X" precisa ser exposto?** Útil para relatórios regulatórios fora do escopo de um contrato específico. Pode ser feature separada, mas vale alinhar agora.
5. **No `ConverterContratoCommand` (ou equivalente), qual é o critério exato hoje para "qual `LimiteBanco` está vigente"?** Confirmar que o critério temporal é compatível com o que vamos persistir como snapshot (mesmo registro que validaria a conversão).
6. **Como tratar o caso `LimiteBanco` sem `garantiasExigidas`?** Snapshot vira `[]` (array vazio) ou `null`? Recomendamos `[]` para distinguir de "não havia LimiteBanco" (`null`).

---

## 9. Mudanças no frontend Nordware (resumo, para coordenação)

Quando o backend entregar:

| Arquivo | Mudança |
| --- | --- |
| `src/modules/finance/types/sgcf.types.ts` (interface `ContratoDto`) | Adicionar `limiteBancoId?: string \| null`, `limiteGlobalBancoId?: string \| null`, `garantiasExigidasSnapshot?: GarantiaExigidaLimiteSnapshotDto[] \| null` |
| `src/modules/finance/types/sgcf.types.ts` (nova interface) | `GarantiaExigidaLimiteSnapshotDto` (espelha o shape do backend) |
| `src/modules/finance/views/ContratoDetalheView.vue` *(a confirmar em sessão frontend)* | Nova seção "Sob qual limite" com link para o `LimiteBanco` referenciado + sub-seção "Garantias exigidas na contratação" mostrando o snapshot |
| `src/modules/finance/types/cotacoes.types.ts` | Sem mudança (cotação fora do escopo) |

Frontend não bloqueia se o backend entregar primeiro: os campos novos no `ContratoDto` são opcionais; o frontend ignora até a UI ser desenhada.

---

## 10. Critérios de aceite (visão de produto)

### Para o backend

- [ ] Schema `Contrato` aceita os três campos novos (FKs + JSONB snapshot).
- [ ] Conversão cotação → contrato preenche os três campos quando os limites existem.
- [ ] `GET /contratos/{id}` retorna os três campos.
- [ ] `GET /contratos` (listagem) retorna apenas os dois FKs (não o snapshot).
- [ ] Snapshot é imutável: nenhum endpoint permite alterar `garantias_exigidas_snapshot` após criação.
- [ ] Alterar `LimiteBanco.garantiasExigidas` via PATCH não afeta snapshots já persistidos.
- [ ] Contratos pré-feature retornam `NULL` nos três campos.
- [ ] Testes de domínio cobrem: snapshot congelado, PATCH no LimiteBanco não propaga, `NULL` aceito.

### Para o frontend (em sessão separada)

A definir quando a UI for desenhada. Critérios provisórios:

- [ ] `ContratoDto` ganha os campos novos.
- [ ] `ContratoDetalheView` exibe "Sob qual limite" + snapshot das garantias.
- [ ] Estado de fallback ("Sob limite legado") quando `limiteBancoId` é `null`.

---

## 11. Risco se NÃO fizermos nada

| Risco | Probabilidade | Impacto |
| --- | --- | --- |
| Fiscalização BACEN pede "política de garantia vigente em data X" | Média (depende do regulador, mas conhecido) | Alto — defesa em fiscalização sem dados |
| Auditoria interna não consegue justificar contratos sem garantia em meio a contratos com garantia | Alta | Médio-alto |
| Operador de tesouraria cria contrato sem garantia hoje, contradizendo política vigente, sem alerta | Média | Médio — depende do enforcement backend |
| Time de produto não consegue evoluir o modelo de garantias (ex.: sobreposição com outras feature) sem revisitar este débito | Alta no horizonte de 12 meses | Médio |

---

## 12. Referências

- **Investigação frontend**: arquivos citados na seção 3 do repo `nordware-landing` na branch `main` (commit `1680dda` em 2026-05-25).
- **Backend especs relacionadas**:
  - `sgcf-backend/docs/specs/limites-banco/` — modelagem do `LimiteBanco` per-modalidade
  - `sgcf-backend/docs/specs/limites-banco/SPEC_LIMITE_GLOBAL.md` — `LimiteGlobalBanco` (S33, recém-entregue)
- **Frontend integration recente**: `nordware-landing/docs/specs/finance/limite-global-banco-frontend/SPEC.md` — feature do guarda-chuva, terminada.

---

## 13. Histórico

| Data | Versão | Mudança |
| --- | --- | --- |
| 2026-05-25 | v1.0 | Documento inicial baseado em investigação frontend + decisão preliminar Opção A+. Aguardando discussão com o time backend. |
