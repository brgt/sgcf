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
- [x] **T5** `CalculadorValorGarantiaExigida` trata grupo = `min(alvo)` das alternativas (RF-09) ✓ — +4 unit (agent paralelo)
- [x] **T6** `AvaliadorCoberturaGarantia` (classe pura) — fração `min(coberto/alvo,1.0)` Σ≥1.0; lacuna de grupo em `LacunaGarantia` (campos aditivos); `AvaliarCobertura` delega ✓ — 8 unit AC-1..AC-6 (agent paralelo)
- [x] **T7** Enforcement e2e em `ConverterEmContrato` + `GlobalExceptionHandler` expõe campos de grupo no 409 ✓ — 2 testes HTTP (bloqueio 0,9 / liberação 1,0); legado EH-01..03 inalterado

### ✅ Checkpoint Fase 2
- [x] Conversão respeita grupos OU ponta a ponta (409 com lacuna de grupo / 201)
- [x] Sem regressão em políticas legadas (EH-01..03 + AvaliadorCobertura regressão verdes)
- [x] `dotnet test --filter "Category!=Slow"` verde: Application 473, Domain 763
- [ ] Revisado com o humano

> Execução: T5 e T6 por **2 agents `dotnet-clean-architect` em paralelo** (worktrees isolados); integrados e re-verificados na branch; T7 (e2e) feito na sequência.

## Fase 3 — Exposição & Regressão (3 agents em paralelo, base da branch)
- [x] **T8** Indicadores com grupo (RF-14) ✓ — teste de verificação: cobertura reflete o **real declarado** (1.04M), não a soma dos alvos (~2.08M). RF-14 já satisfeito por T5 + indicador cobertura-real-based; sem mudança de produção
- [x] **T9** Snapshot S34 preserva grupo (RF-13) ✓ — teste round-trip; snapshot é por-referência, T1+T4 já carregam os campos; sem mudança de produção
- [x] **T10** Docs `limites-banco.md` (grupos OU + lacuna de grupo no 409) ✓ — **golden NÃO estendido** (harness sem auto-discovery e sem acesso aos tipos internal; regra já coberta por unit/domínio/integração). Desvio justificado do plano

### ✅ Checkpoint Completo
- [x] API expõe grupos por limite e por contrato; snapshot preserva histórico (T4/T9)
- [x] Não-slow verde: Domain 763, Application 475; integração de grupo verde (T8/T9/LimitesBancoGrupos/Enforcement)
- [~] `dotnet test` Slow completo: testes de grupo verdes; **flaky pré-existente** do banco POST 500 sob carga paralela (atinge teste aleatório, passa isolado) — infra, não-garantias
- [ ] Pronto para `/review` e PR (pós-revisão humana)
