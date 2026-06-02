# Todo — Garantias Alternativas (Grupos "OU")

> Plano: `tasks/plan_garantias_alternativas.md` · Spec: `SPEC_GARANTIAS_ALTERNATIVAS.md`
> Marque cada item ao concluir. Critérios de aceite detalhados estão no plano.

> Branch: `feat/garantias-alternativas` (a partir de `main`).

## Fase 1 — Cadastro & Persistência
- [x] **T1** Campos de grupo no domínio (`GarantiaExigidaItem` + `Spec`, normalização GA-04; rótulo GA-01) — campos opcionais/retrocompatíveis ✓
- [x] **T2** Invariantes de agregado em `GarantiaExigidaRevisao`: GA-02 (≥2 itens) + GA-05 (rótulo ≤120 e consistente). GA-03/GA-07 já garantidos por SR-06; GA-06 por SR-05. 8 testes GA-01..GA-07 verdes; Domain 763/763 ✓
- [ ] **T3** Migration `S36_GarantiasAlternativas` + EF config (item + snapshot S34) — integration — **BLOQUEADO**: investigar infra de teste S34 antes (4 testes vermelhos pré-existentes; banco POST 500 só nas fixtures `LimitesBancoApiFixture`/`GarantiaPreenchimento`)
- [ ] **T4** DTOs + Request + `LimitesBancoController` (cadastrar→ler grupo via API) — integration

### ✅ Checkpoint Fase 1
- [x] `dotnet build` limpo + Domain `dotnet test --filter "Category!=Slow"` verde (763)
- [ ] Migration aplica em base existente sem afetar linhas legadas (T3)
- [ ] Cadastrar/reler "CDB OU Recebíveis" via API funciona (T4)
- [ ] Revisado com o humano

### 🔎 Achados (registrar antes da Fase 2)
- **Modelagem:** `PercentualSobreLimite` é limitado a (0,100]. O exemplo da spec "Recebíveis **120%**" NÃO é representável como percentual — usar `ValorFixoBrl` para alvos >100%. Impacta os cenários AC-1..AC-7 e a decisão RV-GA.
- **Infra S34:** 4 testes de integração de garantias (stream S34) estão vermelhos no HEAD/`main` (banco POST 500 nas fixtures S34). Pré-existente; bloqueia T3/T4 até diagnóstico.

## ⛔ GATE
- [ ] **RV-GA confirmada** (regra de fração default ou Alternativa B) — bloqueia Fase 2

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
