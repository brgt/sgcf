# Todo — Garantias Alternativas (Grupos "OU")

> Plano: `tasks/plan_garantias_alternativas.md` · Spec: `SPEC_GARANTIAS_ALTERNATIVAS.md`
> Marque cada item ao concluir. Critérios de aceite detalhados estão no plano.

> Branch: `feat/garantias-alternativas` (a partir de `main`).

## Fase 1 — Cadastro & Persistência
- [x] **T1** Campos de grupo no domínio (`GarantiaExigidaItem` + `Spec`, normalização GA-04; rótulo GA-01) — campos opcionais/retrocompatíveis ✓
- [x] **T2** Invariantes de agregado em `GarantiaExigidaRevisao`: GA-02 (≥2 itens) + GA-05 (rótulo ≤120 e consistente). GA-03/GA-07 já garantidos por SR-06; GA-06 por SR-05. 8 testes GA-01..GA-07 verdes; Domain 763/763 ✓
- [x] **T3** Migration `S36_GarantiasAlternativas` (2 colunas nullable + índice parcial) + EF config ✓ — snapshot S34 é por-referência (sem tabela separada); migration aplica em container novo; 16/16 garantias verdes
- [x] **T4** DTOs (`GarantiaExigidaItemDto`, `GarantiaExigidaSnapshotItemDto`) + `CriarGarantiaExigidaItemRequest` + `ContratoDto` ✓ — PATCH/POST e `GET revisoes-garantias` expõem grupo via `GarantiaExigidaItemDto.From`; 2 testes de integração (cadastro→leitura + retrocompat). Controller inalterado (DTOs propagam)

### ✅ Checkpoint Fase 1
- [x] `dotnet build` limpo + `dotnet test --filter "Category!=Slow"` verde (Domain 763, Application 461)
- [x] Migration S36 aplica em container novo sem afetar linhas legadas (NULL)
- [x] Cadastrar/reler "CDB OU Recebíveis" via API funciona (LimitesBancoGruposTests)
- [ ] Revisado com o humano

### 🔎 Achados (registrar antes da Fase 2)
- **Modelagem (impacta RV-GA):** `PercentualSobreLimite` é limitado a (0,100]. O exemplo "Recebíveis **120%**" NÃO é representável como percentual — usar `ValorFixoBrl` para alvos >100%. Afeta os cenários AC-1..AC-7.
- **Infra S34 (diagnosticada/corrigida em parte):** os 4 testes vermelhos eram (a) `PendingModelChangesWarning` — causado pela T1 sem migration, **resolvido pela T3**; (b) 3 testes PATCH desatualizados liam `garantiasExigidas` na raiz, mas o PATCH retorna envelope `{ limite, avisos }` — **corrigidos** (`.limite.garantiasExigidas`). Banco POST 500 era efeito do mesmo PendingModelChanges.
- **Dois `__EFMigrationsHistory` (public vs sgcf):** o DB dev tem o histórico real em `public`, mas o DbContext tem `HasDefaultSchema("sgcf")` sem override do history → `dotnet ef database update` via CLI mira `sgcf` (vazio) e não aplica no DB dev. Testes usam `public` (ok). **Não corrigido** (fora do escopo; recomenda configurar `MigrationsHistoryTable(..., "public")` no DbContext). É a raiz do "dois diretórios de migrations".
- **Flaky:** banco POST 500 intermitente sob carga paralela de Testcontainers (`CotacoesFluxoTests` falhou 1× no full-run, passou isolado). Pré-existente.

## ✅ GATE (liberado 02/jun/2026)
- [x] **RV-GA confirmada = Opção A (normalização por fração)**. Alvos ≤100% sobre o valor contratado (teto (0,100] mantido); enforcement pontual na conversão; over-coverage ao longo do tempo é estado válido (cap em 1,0 + indicadores tolerantes). Ver SPEC §2.2.

## Fase 2 — Operacionalização
- [ ] **T5** `CalculadorValorGarantiaExigida` trata alvo de grupo (RF-09) — unit
- [ ] **T6** `AvaliarCoberturaGrupo` + exceção de grupo (SPEC §4.4) — unit (AC-1..AC-7 fn)
- [ ] **T7** Enforcement e2e em `ConverterEmContrato` (Testcontainers) — integration (AC-1..AC-7)

### ✅ Checkpoint Fase 2
- [ ] Conversão respeita grupos OU ponta a ponta
- [ ] Sem regressão em políticas legadas (AC-7)
- [ ] Revisado com o humano

## Fase 3 — Exposição & Regressão
- [ ] **T8** `IndicadoresGarantiaDto` reflete grupos sem dupla contagem (RF-14)
- [ ] **T9** Snapshot S34 preserva `GrupoAlternativaId`/`GrupoRotulo` na conversão (RF-13)
- [ ] **T10** Golden dataset (CDB OU Recebíveis) + docs `limites-banco.md`

### ✅ Checkpoint Completo
- [ ] `dotnet test` completo verde (inclui `Category=Slow` + golden)
- [ ] API expõe grupos por limite e por contrato; snapshot preserva histórico
- [ ] Pronto para `/review` e PR
