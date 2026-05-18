# Todo — Garantias Exigidas em LimiteBanco

Lista de tarefas em ordem de execução. Marque cada item conforme concluir.
Plano completo em `plan.md` (mesmo diretório).

---

## Bloqueio externo

- [ ] **PRÉ-REQUISITO:** Segunda rodada do GAP-001 (overlap em limites-banco) fechada e mergeada

---

## Fase 1 — Domínio

- [ ] **Task 1.1** Criar entidade `GarantiaExigidaLimite` em `Sgcf.Domain/Cotacoes/`
  - [ ] Definir entity com propriedades, factory `Criar`, método `Atualizar`
  - [ ] Validações: XOR percentual/valorFixo, percentual ∈ (0,100], valorFixo > 0
  - [ ] Testes unitários cobrindo criação e rejeições

- [ ] **Task 1.2** Estender `LimiteBanco` com coleção `GarantiasExigidas`
  - [ ] Adicionar campo privado + `IReadOnlyCollection` pública
  - [ ] Métodos `SubstituirGarantiasExigidas`, `AdicionarGarantiaExigida`, `RemoverGarantiaExigida`
  - [ ] Invariante: sem duplicação por `Tipo`
  - [ ] `Criar(...)` aceita coleção opcional
  - [ ] Testes unitários cobrindo todas as transições

- [ ] **Checkpoint A** — Build verde + testes de domínio passam + revisão humana

---

## Fase 2 — Persistência

- [ ] **Task 2.1** Migration `S5_GarantiasExigidasLimite`
  - [ ] Criar tabela `limite_banco_garantia_exigida` com FK, CHECK XOR, UNIQUE(limite_banco_id, tipo)
  - [ ] `dotnet ef migrations add` gera migration limpa
  - [ ] `dotnet ef database update` aplica em banco existente sem afetar dados

- [ ] **Task 2.2** EF Configuration + Repository eager loading
  - [ ] `GarantiaExigidaLimiteConfiguration` mapeia todos campos
  - [ ] `LimiteBancoConfiguration` declara navigation HasMany / Cascade
  - [ ] `LimiteBancoRepository` métodos de leitura usam `.Include(l => l.GarantiasExigidas)`
  - [ ] Teste de integração: round-trip persistência + cascade delete

- [ ] **Checkpoint B** — Migration aplica/reverte limpo + round-trip OK

---

## Fase 3 — Application + API

- [ ] **Task 3.1** DTOs e mapeamento
  - [ ] `GarantiaExigidaLimiteDto`
  - [ ] `LimiteBancoDto.GarantiasExigidas`
  - [ ] `CriarGarantiaExigidaLimiteRequest` (input)
  - [ ] Mapper atualizado

- [ ] **Task 3.2** `CreateLimiteBancoCommand` aceita garantias
  - [ ] Command + handler aceitam coleção opcional
  - [ ] Teste E2E: criar com 1 garantia CDB 20%
  - [ ] Teste E2E: criar sem garantias (linha "no Aval")
  - [ ] Teste E2E: rejeitar duas garantias mesmo tipo (400)

- [ ] **Task 3.3** `UpdateLimiteBancoCommand` substitui garantias
  - [ ] Semântica replace-all com null = preservar
  - [ ] Teste E2E: PATCH adicionando, limpando, e preservando

- [ ] **Task 3.4** GET endpoints retornam garantias
  - [ ] Listagem e detalhe incluem coleção populada
  - [ ] Sem N+1
  - [ ] Bruno collection mostra payload completo

- [ ] **Checkpoint C** — CRUD ponta a ponta verde + revisão humana

---

## Fase 4 — Integração com Cotações

- [ ] **Task 4.1** Pré-preenchimento de `Proposta` ao adicionar banco-alvo
  - [ ] `FormatadorGarantiaExigida` converte coleção em string descritiva
  - [ ] Cálculo de `ValorGarantiaExigidaBrl` (soma valor fixo + percentual × alvo)
  - [ ] `GarantiaEhCdbCativo` derivado da presença de `Tipo == CdbCativo`
  - [ ] Flag `preencherGarantiaAutomaticamente` no input
  - [ ] Mantém regra SPEC §3.3 (CdbCativo exige RendimentoCdbAaPercentual)
  - [ ] Testes E2E + regressão de cotações

- [ ] **Task 4.2** Validação opcional de coerência (alerta informativo)
  - [ ] Campo `alertas[]` no response quando proposta diverge do limite
  - [ ] Não bloqueia (apenas informa)
  - [ ] Teste E2E confirma alerta

- [ ] **Checkpoint D** — Integração Cotações funcional + sem regressão + CET correto

---

## Fase 5 — Documentação

- [ ] **Task 5.1** `docs/api/limites-banco.md` atualizado
- [ ] **Task 5.2** `docs/api/schemas.md` atualizado
- [ ] **Task 5.3** Bruno collection `11-LimitesBanco/` atualizada
- [ ] **Task 5.4** `CHANGELOG.md` v0.6.0

- [ ] **Checkpoint Final** — `dotnet test` verde, docs revisadas, PR pronto

---

## Perguntas pendentes (responder antes de iniciar)

- [ ] Q1: PATCH replace-all vs. endpoints granulares?
- [ ] Q2: Aval pode ter ambos os campos nulos (relaxar AD-4)?
- [ ] Q3: Modelar vigência da garantia exigida agora ou diferir?
- [ ] Q4: Auditoria granular por garantia ou via UpdatedAt do limite?
- [ ] Q5: Frontend correlato — registrar no backlog do `sgcf-frontend`?
