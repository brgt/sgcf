# Plano de Implementação — Garantias Exigidas em LimiteBanco

**Status:** Pendente de aprovação humana
**Autor:** Planning agent (read-only mode)
**Data:** 2026-05-16
**Dependências externas:** Conclusão da segunda rodada do GAP-001 (overlap em limites-banco)

---

## 1. Contexto

Hoje o agregado `LimiteBanco` (`src/Sgcf.Domain/Cotacoes/LimiteBanco.cs`) carrega apenas o teto de exposição (`valorLimiteBrl`, vigência, modalidade). O modelo não representa o requisito estrutural mais relevante de uma linha bancária: **a garantia exigida pelo banco para liberar a linha**.

Casos reais que o modelo precisa cobrir:
- Linha de R$ 1M em FINIMP com exigência de **20% em CDB cativo** como garantia.
- Linha de R$ 5M em NCE liberada **no Aval** (apenas assinatura dos sócios — sem garantia real).
- Linha com **múltiplas garantias** (raro mas existe: ex. CDB 10% + Aval).

O domínio já contém uma rica hierarquia de `Garantia` no contexto de `Contrato` (`Sgcf.Domain.Contratos.Garantia` + 8 sub-tipos: Aval, CdbCativo, AlienacaoFiduciaria, Sblc, Duplicatas, RecebiveisCartao, BoletoBancario, Fgi). O enum `TipoGarantia` já existe e será reaproveitado.

Em `Proposta` já existem campos planos (`GarantiaExigida` string, `ValorGarantiaExigidaBrl`, `GarantiaEhCdbCativo`) com regras de SPEC §3.3 — esses campos **permanecerão** e serão pré-preenchidos a partir do `LimiteBanco`, não substituídos. Mudar a estrutura de `Proposta` está fora de escopo.

---

## 2. Decisões Arquiteturais

| # | Decisão | Rationale |
|---|---------|-----------|
| AD-1 | Modelar `GarantiaExigidaLimite` como **child entity** owned by `LimiteBanco` (não value object) | Cada requisito tem identidade, pode ser referenciado/auditado individualmente, e pode evoluir (vigência futura, status "negociada/aceita") |
| AD-2 | Coleção `GarantiasExigidas` em `LimiteBanco` (0..N) | Permite linha sem garantia (= zero items), linha no Aval (1 item tipo=Aval), e composições complexas (N items) |
| AD-3 | Reaproveitar enum `TipoGarantia` de `Sgcf.Domain.Contratos` | Consistência semântica entre garantia exigida (planejada) e garantia efetiva (contrato) |
| AD-4 | Campos `PercentualSobreLimite` **OU** `ValorFixoBrl` (mutuamente exclusivos) | "20% sobre o limite" e "R$ 200k fixos" são expressões equivalentes mas operacionalmente distintas; o banco define em uma delas |
| AD-5 | Campo `Obrigatoria` (bool) na garantia exigida | Bancos frequentemente negociam: "CDB 20% obrigatório, FGI opcional". Modelar essa nuance preserva fidelidade |
| AD-6 | Não alterar `Proposta` no MVP | A estrutura plana de `Proposta` é estável e testada; o `LimiteBanco.GarantiasExigidas` apenas **pré-preenche** ao adicionar banco-alvo |
| AD-7 | Migration `S5_GarantiasExigidasLimite` aditiva, default = coleção vazia para limites existentes | Não quebra dados em produção; rollback simples |
| AD-8 | Não validar consistência entre garantias exigidas e garantias efetivas do contrato no MVP | Validação cruzada é um requisito de auditoria, não de cadastro; deferir para módulo de conciliação |

---

## 3. Grafo de Dependências

```
Sgcf.Domain.Contratos.TipoGarantia (existe)
    │
    └─► Sgcf.Domain.Cotacoes.GarantiaExigidaLimite (NEW — child entity)
            │
            └─► Sgcf.Domain.Cotacoes.LimiteBanco (estende — coleção)
                    │
                    ├─► Migration S5_GarantiasExigidasLimite
                    │       └─► EF Configuration (LimiteBancoConfiguration + nova)
                    │               └─► LimiteBancoRepository (eager loading)
                    │
                    ├─► Application: CreateLimiteBancoCommand (estende)
                    ├─► Application: UpdateLimiteBancoCommand (estende)
                    ├─► Application: GetLimiteBancoQuery (estende DTO)
                    │
                    ├─► API: LimitesBancoController (request/response)
                    │
                    └─► Application: AdicionarBancoNaCotacaoCommand
                                (pré-preenche Proposta.GarantiaExigida)
                                    │
                                    └─► Sgcf.Domain.Cotacoes.Proposta (sem mudança estrutural)

Documentação:
    docs/api/limites-banco.md (estende)
    docs/api/schemas.md (adiciona GarantiaExigidaLimiteDto)
    docs/api/collections/sgcf-api/11-LimitesBanco/ (atualiza Bruno)
    docs/changelog/CHANGELOG.md (adiciona v0.6.0 — ADDITIVE)
```

---

## 4. Fases e Tarefas

### Fase 1: Domínio (Foundation)

#### Task 1.1 — Criar `GarantiaExigidaLimite` (child entity)

**Descrição:** Modelar a entidade filha que representa um único requisito de garantia atrelado a um limite. Identidade própria, owned by `LimiteBanco`, sem repositório próprio.

**Critérios de aceite:**
- [ ] Classe `GarantiaExigidaLimite : Entity` em `Sgcf.Domain/Cotacoes/`
- [ ] Propriedades: `LimiteBancoId`, `Tipo` (`TipoGarantia`), `PercentualSobreLimite` (decimal?), `ValorFixoBrl` (Money?), `Obrigatoria` (bool), `Observacoes` (string?)
- [ ] Factory `Criar(...)` valida: exatamente um de `PercentualSobreLimite` ou `ValorFixoBrl` informado; percentual ∈ (0, 100]; valor fixo > 0
- [ ] Método `Atualizar(...)` para alteração in-place
- [ ] Construtor privado para EF

**Verificação:**
- [ ] Testes em `tests/Sgcf.Domain.Tests/Cotacoes/GarantiaExigidaLimiteTests.cs` cobrem: criação válida (cada caminho), rejeições (ambos nulos, ambos preenchidos, percentual fora do intervalo, valor ≤ 0)
- [ ] `dotnet test --filter "FullyQualifiedName~GarantiaExigidaLimite"` passa

**Dependências:** nenhuma

**Arquivos prováveis:**
- `src/Sgcf.Domain/Cotacoes/GarantiaExigidaLimite.cs`
- `tests/Sgcf.Domain.Tests/Cotacoes/GarantiaExigidaLimiteTests.cs`

**Escopo:** S

---

#### Task 1.2 — Estender `LimiteBanco` com coleção `GarantiasExigidas`

**Descrição:** Adicionar `IReadOnlyCollection<GarantiaExigidaLimite> GarantiasExigidas` ao agregado, com métodos `AdicionarGarantiaExigida`, `RemoverGarantiaExigida`, `SubstituirGarantiasExigidas`.

**Critérios de aceite:**
- [ ] Campo privado `_garantiasExigidas: List<GarantiaExigidaLimite>` + propriedade pública `IReadOnlyCollection`
- [ ] Método `SubstituirGarantiasExigidas(IEnumerable<GarantiaExigidaLimite> novas, IClock clock)` — operação atômica que limpa e re-popula
- [ ] Métodos granulares `Adicionar` / `Remover` por id (para PATCH parcial futuro, opcional no MVP)
- [ ] Invariante: não permitir duas garantias do mesmo `Tipo` no mesmo limite (banco não exige "20% CDB + 10% CDB" — seria um único requisito agregado)
- [ ] `Criar(...)` ganha parâmetro `IEnumerable<GarantiaExigidaLimite>? garantiasExigidas = null`

**Verificação:**
- [ ] Testes em `LimiteBancoTests.cs` cobrem: criar limite sem garantias (Aval implícito), criar com 1, criar com N, substituir, rejeitar duplicado por tipo
- [ ] Testes existentes de `LimiteBanco` continuam passando

**Dependências:** Task 1.1

**Arquivos prováveis:**
- `src/Sgcf.Domain/Cotacoes/LimiteBanco.cs`
- `tests/Sgcf.Domain.Tests/Cotacoes/LimiteBancoGarantiasTests.cs`

**Escopo:** M

---

#### Checkpoint A — Domínio

- [ ] `dotnet build` limpo
- [ ] Testes do domínio passam (`dotnet test tests/Sgcf.Domain.Tests`)
- [ ] Revisão humana das invariantes (especialmente AD-4 e AD-5) antes de prosseguir

---

### Fase 2: Persistência

#### Task 2.1 — Migration `S5_GarantiasExigidasLimite`

**Descrição:** Criar nova tabela `limite_banco_garantia_exigida` ligada a `limite_banco` por FK; aditiva, sem alterar tabela existente.

**Critérios de aceite:**
- [ ] Tabela com colunas: `id` (uuid PK), `limite_banco_id` (FK), `tipo` (int — enum), `percentual_sobre_limite` (decimal nullable), `valor_fixo_brl` (decimal nullable), `obrigatoria` (bool not null default true), `observacoes` (text nullable)
- [ ] Constraint CHECK: `(percentual_sobre_limite IS NOT NULL) <> (valor_fixo_brl IS NOT NULL)` (XOR)
- [ ] Constraint UNIQUE: `(limite_banco_id, tipo)` — espelha invariante de Task 1.2
- [ ] Index em `limite_banco_id` para join eager
- [ ] Migration reverte sem erro (down drop table)

**Verificação:**
- [ ] `dotnet ef migrations add S5_GarantiasExigidasLimite --project src/Sgcf.Infrastructure --startup-project src/Sgcf.Api` gera migration limpa
- [ ] `dotnet ef database update` aplica em banco com dados existentes (não afeta limites já cadastrados)
- [ ] `dotnet ef migrations remove` reverte sem erro

**Dependências:** Task 1.2

**Arquivos prováveis:**
- `src/Sgcf.Infrastructure/Migrations/2026xxxx_S5_GarantiasExigidasLimite.cs`
- `src/Sgcf.Infrastructure/Migrations/2026xxxx_S5_GarantiasExigidasLimite.Designer.cs`
- `src/Sgcf.Infrastructure/Migrations/SgcfDbContextModelSnapshot.cs`

**Escopo:** S

---

#### Task 2.2 — EF Configuration + repository eager loading

**Descrição:** Configurar mapeamento EF Core para `GarantiaExigidaLimite` como entidade própria (não owned — para suportar identidade + queries) com relação 1:N para `LimiteBanco`. Garantir que `LimiteBancoRepository.GetByIdAsync` e queries de listagem incluam `.Include(l => l.GarantiasExigidas)`.

**Critérios de aceite:**
- [ ] `GarantiaExigidaLimiteConfiguration : IEntityTypeConfiguration<GarantiaExigidaLimite>` mapeia todas colunas, CHECK constraint, índices
- [ ] `LimiteBancoConfiguration` declara navigation `HasMany(l => l.GarantiasExigidas).WithOne()...OnDelete(Cascade)`
- [ ] `LimiteBancoRepository` métodos de leitura usam `.Include(l => l.GarantiasExigidas)`
- [ ] `Money` (ValorFixoBrl) mapeado via owned type ou conversor — consistente com padrão de outras entidades

**Verificação:**
- [ ] Teste de integração `LimiteBancoRepositoryTests`: persistir limite com 2 garantias, recarregar, verificar coleção
- [ ] Cascade delete: deletar limite remove garantias órfãs

**Dependências:** Task 2.1

**Arquivos prováveis:**
- `src/Sgcf.Infrastructure/Persistence/Configurations/GarantiaExigidaLimiteConfiguration.cs`
- `src/Sgcf.Infrastructure/Persistence/Configurations/LimiteBancoConfiguration.cs` (edita)
- `src/Sgcf.Infrastructure/Persistence/Repositories/LimiteBancoRepository.cs` (edita)
- `tests/Sgcf.Api.IntegrationTests/LimitesBanco/LimiteBancoRepositoryTests.cs`

**Escopo:** M

---

#### Checkpoint B — Persistência

- [ ] Migration aplica e reverte sem erro em banco vazio e com dados
- [ ] Round-trip de persistência funciona (write → read recupera garantias)
- [ ] Cascade delete validado

---

### Fase 3: Application + API

#### Task 3.1 — DTOs e mapeamento

**Descrição:** Criar `GarantiaExigidaLimiteDto` e estender `LimiteBancoDto` para incluir a coleção.

**Critérios de aceite:**
- [ ] `GarantiaExigidaLimiteDto`: `id`, `tipo` (string), `percentualSobreLimite`, `valorFixoBrl`, `obrigatoria`, `observacoes`
- [ ] `LimiteBancoDto.GarantiasExigidas: IReadOnlyList<GarantiaExigidaLimiteDto>`
- [ ] `LimiteBancoMapper` atualizado para preencher a coleção
- [ ] `CriarGarantiaExigidaLimiteRequest` (input DTO) para criação/atualização

**Verificação:**
- [ ] Teste de mapeamento bidirecional (entidade ↔ DTO) preserva dados

**Dependências:** Task 1.1

**Escopo:** S

---

#### Task 3.2 — `CreateLimiteBancoCommand` aceita garantias

**Descrição:** Estender o command para aceitar `IReadOnlyList<CriarGarantiaExigidaLimiteRequest> garantiasExigidas` e propagar para `LimiteBanco.Criar`.

**Critérios de aceite:**
- [ ] Command + handler aceitam coleção (default = vazio)
- [ ] Validação de entrada usa as regras do domínio (Task 1.1) — erros viram 400 com mensagens claras
- [ ] Endpoint `POST /api/v1/limites-banco` aceita payload novo (backwards-compatible: campo opcional)

**Verificação:**
- [ ] Teste E2E: criar limite com 1 garantia CDB 20% → 201 com payload completo
- [ ] Teste E2E: criar limite sem garantias → 201 (linha "no Aval" / sem garantia)
- [ ] Teste E2E: criar limite com 2 garantias mesmo tipo → 400

**Dependências:** Tasks 1.2, 2.2, 3.1

**Escopo:** M

---

#### Task 3.3 — `UpdateLimiteBancoCommand` substitui garantias

**Descrição:** Endpoint `PATCH /api/v1/limites-banco/{id}` permite substituir a coleção inteira de garantias exigidas (semântica "replace all"). Operação granular (add/remove single) fica para uma evolução futura.

**Critérios de aceite:**
- [ ] Command aceita `IReadOnlyList<CriarGarantiaExigidaLimiteRequest>? garantiasExigidas` (null = não alterar; vazio = limpar todas; preenchido = substituir)
- [ ] Handler chama `LimiteBanco.SubstituirGarantiasExigidas(...)` quando informado
- [ ] Campo aparece como opcional no contrato da API

**Verificação:**
- [ ] Teste E2E: PATCH adicionando garantia funciona; PATCH com lista vazia limpa; PATCH sem campo preserva
- [ ] Histórico (`UpdatedAt`) atualiza apenas quando há mudança real

**Dependências:** Tasks 1.2, 2.2, 3.1

**Escopo:** M

---

#### Task 3.4 — `GET` endpoints retornam garantias

**Descrição:** Garantir que `GET /api/v1/limites-banco` e `GET /api/v1/limites-banco/{id}` retornam a coleção populada via eager loading.

**Critérios de aceite:**
- [ ] Listagem inclui garantias em cada item
- [ ] Detalhe inclui garantias
- [ ] Sem N+1 (verificado via teste de assertion de queries SQL ou inspeção manual)

**Verificação:**
- [ ] Teste E2E inspeciona payload e confirma campos presentes
- [ ] Bruno collection mostra payload novo

**Dependências:** Tasks 2.2, 3.1

**Escopo:** S

---

#### Checkpoint C — CRUD ponta a ponta

- [ ] Usuário consegue criar, ler, atualizar limite com garantias via API
- [ ] Suite E2E `LimitesBancoApi` verde
- [ ] Bruno collection atualizada — operador valida manualmente
- [ ] Revisão humana antes da integração com Cotações

---

### Fase 4: Integração com Cotações

#### Task 4.1 — Pré-preenchimento de `Proposta` ao adicionar banco-alvo

**Descrição:** Quando `AdicionarBancoNaCotacaoCommand` é executado e o `LimiteBanco` selecionado tem `GarantiasExigidas`, o handler popula automaticamente `Proposta.GarantiaExigida` (string formatada), `Proposta.ValorGarantiaExigidaBrl`, e `Proposta.GarantiaEhCdbCativo`.

**Critérios de aceite:**
- [ ] Helper `FormatadorGarantiaExigida` converte coleção em string descritiva (ex: "CDB cativo 20% + Aval (obrigatório)")
- [ ] `ValorGarantiaExigidaBrl` calculado como soma de: cada item com `ValorFixoBrl` + cada item com `PercentualSobreLimite` × `valorAlvoBrl` da proposta
- [ ] `GarantiaEhCdbCativo` = true se qualquer garantia exigida tem `Tipo == CdbCativo`
- [ ] Comportamento ativável via flag de input (`preencherGarantiaAutomaticamente: bool = true`) para preservar override manual

**Verificação:**
- [ ] Teste E2E: criar limite com 20% CDB → criar cotação → adicionar banco → `Proposta.GarantiaEhCdbCativo` = true e `ValorGarantiaExigidaBrl` correto
- [ ] Teste E2E: limite sem garantias → proposta criada com `GarantiaEhCdbCativo` = false e string vazia (preserva fluxo atual)
- [ ] Teste de regressão: cenários existentes de cotações continuam verdes

**Dependências:** Tasks 1.2, 2.2

**Arquivos prováveis:**
- `src/Sgcf.Application/Cotacoes/AdicionarBancoNaCotacaoCommand.cs` (edita)
- `src/Sgcf.Application/Cotacoes/FormatadorGarantiaExigida.cs` (novo)
- Testes de integração de Cotações

**Escopo:** M

---

#### Task 4.2 — Validação opcional de coerência

**Descrição:** Emitir alerta informativo (não bloqueante) quando o operador adiciona um banco-alvo a uma cotação cuja modalidade tem garantias exigidas estruturais no limite, mas o operador remove/altera os campos populados na proposta.

**Critérios de aceite:**
- [ ] Campo `alertas[]` no response de `AdicionarBancoNaCotacao` quando proposta difere de garantias do limite
- [ ] Alerta inclui texto descritivo e referência aos campos divergentes
- [ ] Não bloqueia operação (consistente com decisão de "alertas informativos" do módulo de Simulações)

**Verificação:**
- [ ] Teste E2E confirma presença do alerta no payload

**Dependências:** Task 4.1

**Escopo:** S

---

#### Checkpoint D — Integração Cotações

- [ ] Pré-preenchimento funciona em cenário FINIMP com CDB cativo
- [ ] Cotações existentes não regridem
- [ ] CET continua sendo calculado corretamente (rendimento CDB cativo entra na conta — SPEC §3.3)

---

### Fase 5: Documentação

#### Task 5.1 — Atualizar `docs/api/limites-banco.md`

**Critérios de aceite:**
- [ ] Seção nova "Garantias Exigidas" explica modelagem
- [ ] Schema de `LimiteBancoDto` atualizado com `garantiasExigidas[]`
- [ ] Schema de `GarantiaExigidaLimiteDto` documentado
- [ ] Exemplos de POST/PATCH com garantias

**Escopo:** S

---

#### Task 5.2 — Atualizar `docs/api/schemas.md`

**Critérios de aceite:**
- [ ] `GarantiaExigidaLimiteDto` adicionado
- [ ] `LimiteBancoDto` atualizado
- [ ] Enum `TipoGarantia` documentado (provavelmente já existe — verificar)

**Escopo:** XS

---

#### Task 5.3 — Bruno collection

**Critérios de aceite:**
- [ ] Requests `POST` e `PATCH` em `11-LimitesBanco/` incluem payload com garantias exemplares
- [ ] Novo request `POST limite no Aval` (zero garantias) para documentar caso comum
- [ ] Variáveis de ambiente atualizadas se necessário

**Escopo:** S

---

#### Task 5.4 — CHANGELOG v0.6.0

**Critérios de aceite:**
- [ ] Seção `[0.6.0] — 2026-MM-DD` adicionada
- [ ] Bloco `ADDITIVE — Limites de Banco — Garantias Exigidas` documenta nova capacidade
- [ ] Bloco `ADDITIVE — Cotações — Pré-preenchimento de Garantia` documenta integração
- [ ] Bloco `INTERNAL — Migration S5` documenta tabela nova

**Escopo:** XS

---

#### Checkpoint Final

- [ ] Todos testes passam (`dotnet test`)
- [ ] Build limpo, sem warnings novos
- [ ] Documentação revisada
- [ ] Bruno collection valida fluxo manual completo
- [ ] PR pronto para review

---

## 5. Riscos e Mitigações

| Risco | Impacto | Mitigação |
|-------|---------|-----------|
| Mudança em `LimiteBanco` interfere no fix do GAP-001 (ainda em segunda rodada) | Médio | Sequenciar: GAP-001 fechado **antes** de começar Task 1.2; reusar a infra de teste de overlap |
| `Proposta` tem regra SPEC §3.3 que pode quebrar se `GarantiaEhCdbCativo` for setado sem `RendimentoCdbAaPercentual` | Alto | Task 4.1 deve **manter** a regra existente: se setar `GarantiaEhCdbCativo = true` automaticamente, exigir que o operador informe `RendimentoCdbAaPercentual` ou rejeitar 400 |
| Cascade delete remove garantias quando limite é arquivado | Baixo | Garantias acompanham o limite — comportamento esperado; documentar |
| Soma de garantias percentuais > 100% (banco exige 70% CDB + 50% Aval = 120%?) | Médio | Aceitar — bancos exigem coberturas redundantes; validação apenas alerta informativo, não bloqueia |
| Conflito com índice unique parcial mencionado no review de GAP-001 | Baixo | Coordenar com a segunda rodada do GAP-001; nenhuma mudança ao índice é necessária aqui |

---

## 6. Perguntas em Aberto

1. **Granularidade do PATCH:** Task 3.3 propõe semântica "replace all" para `garantiasExigidas`. Confirma, ou prefere endpoints granulares `POST /limites-banco/{id}/garantias` e `DELETE /limites-banco/{id}/garantias/{tipo}` desde o MVP?
2. **Tipo Aval com valor:** o `GarantiaAvalDetail` no contexto de Contrato tem `ValorAval` (Money). Para `GarantiaExigidaLimite` de tipo `Aval`, faz sentido exigir `ValorFixoBrl` ou ele fica opcional/null? Sugestão: para Aval, ambos os campos podem ser nulos (apenas a presença do registro com `Tipo=Aval` já significa "exige aval"). Confirma essa relaxação da regra AD-4?
3. **Vigência da garantia exigida:** banco às vezes exige garantia "enquanto a linha existir" mas às vezes "apenas no primeiro ano". Modelar `VigenciaInicio`/`VigenciaFim` agora ou deferir?
4. **Auditoria:** alterações em garantias exigidas devem produzir `AuditLog` separado (granular) ou apenas via `UpdatedAt` do limite agregado? Consistência com módulo de Auditoria existente sugere log granular.
5. **Tela frontend:** este plano cobre apenas backend. O frontend (`tasks/plan.md` original) precisará receber tasks correlatas em `AbaLimitesBanco.vue` e `LimiteBancoForm.vue`. Fora do escopo deste plano, mas vale registrar no backlog.

---

## 7. Paralelização

- **Sequencial obrigatório:** Tasks 1.1 → 1.2 → 2.1 → 2.2 → 3.2 → 4.1
- **Paralelo possível:** Tasks 5.1, 5.2, 5.3, 5.4 (documentação) podem ser feitas em paralelo após Checkpoint C
- **Paralelo após Checkpoint C:** Task 4.1 + Task 4.2 podem rodar em paralelo
- **Bloqueado por externo:** Task 1.2 espera fechamento da segunda rodada de GAP-001

---

## 8. Sumário Quantitativo

- **5 fases**, **12 tasks**, **5 checkpoints** (A, B, C, D, Final)
- **Escopo total:** ~3 M, ~5 S, ~2 XS (escopo dominante: domínio + integração)
- **Caminho crítico:** Task 1.1 → 1.2 → 2.1 → 2.2 → 3.2 → 4.1 (6 tasks)
- **Pré-requisito externo:** segunda rodada do GAP-001 fechada
