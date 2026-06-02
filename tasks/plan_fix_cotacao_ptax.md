# Plano de Implementação — Correção dos Bugs de Cotação / PTAX

> **Spec:** `SPEC_FIX_COTACAO_PTAX.md` (raiz)
> **Todo:** `tasks/todo_fix_cotacao_ptax.md`
> **Status:** Proposta — aguardando aprovação
> **Stack:** .NET / EF Core / NodaTime / PostgreSQL / Redis (ver `sgcf-backend/CLAUDE.md`)

## Overview

Corrigir o bug em que leituras de PTAX consultam o tipo lógico `PtaxD1` direto no repositório, enquanto o ingestor do BCB grava apenas `PtaxD0` — quebrando criação de cotação, proposta, refresh e ~14 consultas de Painel/Tesouraria/Jobs/MCP em produção real. A correção centraliza a tradução `PtaxD1 → PtaxD0(D-1)` no `IResolveTipoCotacaoService`, normaliza a semântica de data por chamador (evita off-by-one), adiciona endpoint admin para cadastro manual de PTAX, alinha as fixtures ao dado real e entrega um guia para o front. Abordagem incremental e retrocompatível no comportamento já correto (mid-rate do Painel, venda no `CriarCotacao`).

## Architecture Decisions

- **D1 — Resolver centralizado:** a regra D-1 vive em um único método de `IResolveTipoCotacaoService` (interface em `Application`, impl em `Infrastructure`). Mcp/A2a dependem só da interface (regra de camadas preservada).
- **Retorno `CotacaoFx?`** do novo método (não mid-rate), para cada chamador manter sua escolha de valor (venda vs mid).
- **Normalização de data por perfil** (auditoria da §3.2 do spec): Perfil A (pré-subtraem) exige ajuste; Perfil B (data corrente) apenas troca a dependência.
- **Endpoint manual grava `PtaxD0`** (coerente com o ingestor); leitura D-1 resolve a partir dele. Idempotente pela unique key.
- **TDD/Prove-It:** primeiro um teste que reproduz o bug com banco só-`PtaxD0`, visto falhar, antes de corrigir.

## Dependency Graph

```
T1 Prove-It (banco só-PtaxD0 → CriarCotacao falha hoje)   [risco-first]
      │
T2 Método de resolução no IResolveTipoCotacaoService (+ impl, extrai tradução)   [FUNDAÇÃO]
      │
      ├── T3 Perfil A: CriarCotacao + RegistrarProposta (off-by-one) + fixtures desses 2   ← desbloqueio do usuário
      │        │
      │   [Checkpoint 1]
      │
      ├── T4 Perfil B: Painel (10 handlers)
      ├── T5 Perfil B: Tesouraria (3) + Sensibilidade (1) + RefreshMercado (1)
      └── T6 Perfil B: Jobs (2) + MCP (1)
               │
          [Checkpoint 2]

T7 Endpoint POST /cotacoes-fx (+ command/validator/tests)   [paralelizável — independe de T2..T6]
T8 Varredura de fixtures restantes + regressão off-by-one consolidada   [depende de T2,T3]
T9 GUIA_FRONT_COTACOES.md   [paralelizável — independe de tudo]
          │
     [Checkpoint 3 — completo]
```

---

## Task List

### Fase 1 — Núcleo (desbloqueio da criação de cotação)

#### Task 1: Prove-It — reproduzir o bug com banco só-`PtaxD0`

**Description:** Escrever teste de integração (Testcontainers) que semeia **apenas** uma `CotacaoFx` `PtaxD0` (como faz o ingestor real) e tenta criar uma cotação cambial; demonstrar que **hoje falha** com "PTAX D-1 não disponível". O teste permanece como regressão permanente (AC-4).

**Acceptance criteria:**
- [ ] Teste novo, vermelho hoje, que reproduz exatamente o erro com DB contendo só `PtaxD0`.
- [ ] Documenta no nome/descrição o cenário (ingestor grava D0; leitura pede D1).

**Verification:**
- [ ] `dotnet test tests/Sgcf.Api.IntegrationTests --filter "FullyQualifiedName~CriarCotacaoPtaxD0"` → **falha** (esperado nesta task).

**Dependencies:** None
**Files likely touched:** `tests/Sgcf.Api.IntegrationTests/Cotacoes/CriarCotacaoPtaxD0Tests.cs`
**Estimated scope:** S

#### Task 2: Método de resolução centralizado (`IResolveTipoCotacaoService`)

**Description:** Adicionar à interface (`Application/Cambio/IResolveTipoCotacaoService.cs`) um método `ResolverFxAsync(Moeda, TipoCotacao tipoLogico, LocalDate dataReferencia, ct) : Task<CotacaoFx?>`. Implementar em `CotacaoResolverService` extraindo a tradução já existente (`:39-40`): `PtaxD1 → PtaxD0` em `dataReferencia.PlusDays(-1)`; demais tipos → consulta direta. `ResolveAsync` (por banco/modalidade) passa a reaproveitar o novo método.

**Acceptance criteria:**
- [ ] `PtaxD1` resolve para `PtaxD0` mais recente antes de `dataReferencia`.
- [ ] Outros tipos repassados sem alteração de data.
- [ ] `ResolveAsync` mantém comportamento atual (testes existentes do resolver verdes).

**Verification:**
- [ ] `dotnet test tests/Sgcf.Application.Tests --filter "FullyQualifiedName~CotacaoResolver"`
- [ ] `dotnet build`

**Dependencies:** None (mas T3+ dependem desta)
**Files likely touched:** `src/Sgcf.Application/Cambio/IResolveTipoCotacaoService.cs`, `src/Sgcf.Infrastructure/Cambio/CotacaoResolverService.cs`, `tests/Sgcf.Application.Tests/Cambio/CotacaoResolverServiceTests.cs`
**Estimated scope:** S-M

#### Task 3: Perfil A — `CriarCotacao` + `RegistrarProposta` (corrige off-by-one)

**Description:** Migrar os dois chamadores que pré-subtraem o dia para o resolver, ajustando a data para evitar dupla subtração. `CriarCotacaoCommand`: passar `dataAbertura` (remover `PlusDays(-1)`), usar `ValorVenda` do `CotacaoFx` retornado, manter cálculo de `DataPtaxReferencia`. `RegistrarPropostaCommand`: resolver `PtaxD0` na data exata `DataPtaxReferencia` (sem novo `-1`). Atualizar as fixtures desses dois (`CriarCotacaoRefinimpTests`, `RegistrarPropostaCommandHandlerTests`) para semear/mokar `PtaxD0`. Faz o teste da Task 1 passar.

**Acceptance criteria:**
- [ ] AC-1: `CriarCotacao` com abertura R resolve o fechamento `PtaxD0` de `R-1` (não `R-2`, não null).
- [ ] AC-3: `RegistrarProposta` usa a mesma data de PTAX travada na criação.
- [ ] AC-4: o teste Prove-It (Task 1) agora passa.
- [ ] Fixtures desses comandos deixam de semear `PtaxD1`.

**Verification:**
- [ ] `dotnet test tests/Sgcf.Api.IntegrationTests --filter "FullyQualifiedName~CriarCotacaoPtaxD0"` → **verde**
- [ ] `dotnet test tests/Sgcf.Application.Tests --filter "FullyQualifiedName~CriarCotacao|FullyQualifiedName~RegistrarProposta"`

**Dependencies:** Task 1, Task 2
**Files likely touched:** `src/Sgcf.Application/Cotacoes/Commands/CriarCotacaoCommand.cs`, `.../RegistrarPropostaCommand.cs`, `tests/.../CriarCotacaoRefinimpTests.cs`, `tests/.../RegistrarPropostaCommandHandlerTests.cs`
**Estimated scope:** M

### Checkpoint: Fase 1
- [ ] Criar cotação e registrar proposta funcionam com banco contendo **só `PtaxD0`**.
- [ ] Off-by-one coberto (AC-1, AC-3); Prove-It verde (AC-4).
- [ ] `dotnet build` limpo; testes de Cotações verdes.
- [ ] **Revisar com o humano antes da Fase 2.**

---

### Fase 2 — Propagação das demais leituras (Perfil B, mid-rate preservado)

#### Task 4: Reroute Painel (10 handlers)

**Description:** Trocar `fxRepo.GetMaisRecenteAsync(moeda, PtaxD1, R, ct)` por `resolver.ResolverFxAsync(moeda, PtaxD1, R, ct)` **sem alterar o argumento de data**, preservando o cálculo de mid-rate em: GetPainelDivida, GetQuadroDivida, GetDashboardKpis, GetBreakdownModalidade, GetCalendarioVencimentos, GetCurvaVencimentos, GetInadimplencia, SimularCenarioCambial, SimularAntecipacaoPortfolio, GetSaldoPorBancoAtual. Ajustar injeção de dependência (passar a injetar `IResolveTipoCotacaoService`).

**Acceptance criteria:**
- [ ] AC-2: um painel cambial com banco só-`PtaxD0` resolve o mesmo fechamento de AC-1.
- [ ] Mid-rate `(compra+venda)/2` inalterado.

**Verification:**
- [ ] `dotnet test tests/Sgcf.Application.Tests --filter "FullyQualifiedName~Painel"`
- [ ] `dotnet test --filter "Category!=Slow"`

**Dependencies:** Task 2 (recomenda-se após Checkpoint 1)
**Files likely touched:** 10 handlers em `src/Sgcf.Application/Painel/Queries/`
**Estimated scope:** M-L (mecânico, padrão único)

#### Task 5: Reroute Tesouraria + Sensibilidade + RefreshMercado

**Description:** Mesmo padrão de troca em: `GetFluxoCaixaQuery`, `GetPosicaoCaixaQuery`, `GetHedgeEfetividadeQuery`, `GetSensibilidadeIndexadoresQuery` e `RefreshCotacaoMercadoCommand` (mantém semântica D-1 atual; mensagem de erro preservada).

**Acceptance criteria:**
- [ ] Consultas resolvem PTAX com banco só-`PtaxD0`; mid-rate inalterado.
- [ ] `RefreshMercado` mantém comportamento (erro claro se ausente).

**Verification:**
- [ ] `dotnet test tests/Sgcf.Application.Tests --filter "FullyQualifiedName~Tesouraria|FullyQualifiedName~Sensibilidade|FullyQualifiedName~Refresh"`

**Dependencies:** Task 2
**Files likely touched:** 3 em `Tesouraria/Queries/`, `Contratos/Queries/GetSensibilidadeIndexadoresQuery.cs`, `Cotacoes/Commands/RefreshCotacaoMercadoCommand.cs`
**Estimated scope:** M

#### Task 6: Reroute Jobs + MCP

**Description:** Trocar para o resolver em `RecalcularMtmJob`, `SnapshotMensalJob` (resolução via `sp.GetRequiredService<IResolveTipoCotacaoService>()`) e `DividaTools` (MCP). Confirmar que MCP depende só da interface em `Application` (sem Infrastructure).

**Acceptance criteria:**
- [ ] Jobs e MCP resolvem PTAX com banco só-`PtaxD0`.
- [ ] Nenhuma referência a `Sgcf.Infrastructure` em `Sgcf.Mcp`.

**Verification:**
- [ ] `dotnet build` (Jobs, Mcp)
- [ ] `dotnet test tests/Sgcf.Mcp.Tests`

**Dependencies:** Task 2
**Files likely touched:** `src/Sgcf.Jobs/Jobs/RecalcularMtmJob.cs`, `.../SnapshotMensalJob.cs`, `src/Sgcf.Mcp/Tools/DividaTools.cs`
**Estimated scope:** S-M

### Checkpoint: Fase 2
- [ ] Nenhum chamador restante usa `fxRepo.GetMaisRecenteAsync(..., PtaxD1, ...)` direto (varredura `grep`).
- [ ] Painéis/Tesouraria/Jobs/MCP cambiais funcionam com banco só-`PtaxD0` (AC-2).
- [ ] `dotnet test --filter "Category!=Slow"` verde.
- [ ] **Revisar com o humano antes da Fase 3.**

---

### Fase 3 — Cadastro manual, varredura e guia

#### Task 7: Endpoint admin `POST /cotacoes-fx` (paralelizável)

**Description:** Novo `CotacoesFxController` (policy Admin) + `RegistrarCotacaoFxCommand` + validator. Grava `PtaxD0` por padrão via `CotacaoFx.Criar` + `repo.UpsertAsync` (idempotente). `GET /cotacoes-fx` opcional para conferência. Validação: valores > 0, quote BRL, `momento` não-futuro, `tipo` no enum.

**Acceptance criteria:**
- [ ] AC-6: POST (admin) grava `PtaxD0`; repetição idempotente não duplica.
- [ ] AC-7: não-admin → 403; payload inválido → 400 com mensagem clara.
- [ ] Após cadastrar manualmente, `CriarCotacao` funciona sem seed via SQL.

**Verification:**
- [ ] `dotnet test tests/Sgcf.Api.IntegrationTests --filter "FullyQualifiedName~CotacaoFx"`

**Dependencies:** Task 2 (para o fluxo ponta a ponta); o endpoint em si independe de T3..T6
**Files likely touched:** `src/Sgcf.Api/Controllers/CotacoesFxController.cs` (novo), `src/Sgcf.Application/Cambio/Commands/RegistrarCotacaoFxCommand.cs` (novo), `tests/Sgcf.Api.IntegrationTests/Cambio/CotacaoFxEndpointTests.cs`
**Estimated scope:** M

#### Task 8: Varredura de fixtures + regressão off-by-one consolidada

**Description:** Varrer testes remanescentes que semeiam `PtaxD1` e migrá-los para `PtaxD0` (RF-10). Adicionar o teste de regressão que prova que Perfil A e Perfil B resolvem a **mesma** data de fechamento para a mesma referência (RF-11).

**Acceptance criteria:**
- [ ] Nenhum teste depende de linha `PtaxD1` semeada (`grep` em tests).
- [ ] Teste de paridade Perfil A × Perfil B (RF-11) verde.

**Verification:**
- [ ] `dotnet test` (suíte completa) verde, incluindo `Category=Slow`.

**Dependencies:** Task 2, Task 3 (idealmente após Task 4-6)
**Files likely touched:** `tests/Sgcf.Application.Tests/**`, `tests/Sgcf.Api.IntegrationTests/**`
**Estimated scope:** S-M

#### Task 9: `GUIA_FRONT_COTACOES.md` (paralelizável)

**Description:** Markdown orientando o front: (a) `parametros-cotacao` configura o **tipo**, não a taxa; (b) enum válido `PtaxD0,PtaxD1,SpotIntraday,Fixing` e contrato do 400; (c) como cadastrar a taxa USD/BRL (`POST /cotacoes-fx` ou ingestão); (d) `GET /parametros-cotacao/resolve`.

**Acceptance criteria:**
- [ ] Documento cobre os 4 pontos com exemplos de request/response.
- [ ] Explica explicitamente a causa do 400 que o time relatou.

**Verification:**
- [ ] Revisão do conteúdo.

**Dependencies:** None (texto); referenciar T7 para o endpoint manual
**Files likely touched:** `sgcf-backend/docs/api/GUIA_FRONT_COTACOES.md`
**Estimated scope:** S

### Checkpoint: Completo
- [ ] Suíte completa (`dotnet test`) verde, incluindo `Category=Slow`.
- [ ] Criar cotação funciona alimentando o banco **apenas** com `PtaxD0` (ingestor ou endpoint manual) — seed via SQL não é mais necessário.
- [ ] `grep` confirma zero chamadas diretas a `GetMaisRecenteAsync(..., PtaxD1, ...)`.
- [ ] Guia do front publicado.
- [ ] Pronto para `/review` e PR.

---

## Risks and Mitigations

| Risco | Impacto | Mitigação |
|---|---|---|
| Off-by-one no Perfil A | Alto | T1 (Prove-It) + AC-1/AC-3; ajuste explícito de data em T3 |
| Regressão de mid-rate no Painel | Médio | Resolver retorna `CotacaoFx`; chamadores mantêm o cálculo; testes de Painel |
| Quebra de camada (MCP→Infra) | Médio | Interface em Application; checagem em T6 |
| Fixtures `PtaxD1` mascararem regressão | Médio | T3 (parcial) + T8 (varredura) eliminam dependência de `PtaxD1` |
| Endpoint manual gravar tipo errado | Baixo | Default `PtaxD0` + validação + idempotência |

## Parallelization

- **Sequencial:** T1 → T2 → T3 (núcleo). T2 é pré-requisito de T4/T5/T6.
- **Paralelizável:** após T2, T4/T5/T6 são independentes entre si. T7 e T9 podem rodar a qualquer momento (T7 valida ponta a ponta após T2).
- **Coordenação:** a assinatura do método em `IResolveTipoCotacaoService` (T2) é o contrato; fixar antes de paralelizar T4-T6.

## Open Questions

- `RefreshCotacaoMercado`: manter D-1 (assumido) ou migrar para D0/spot? Confirmar antes de T5.
- Endpoint manual aceita `PtaxD1` explícito ou só `PtaxD0`/`SpotIntraday`/`Fixing`? (assumido: enum completo, recomendando `PtaxD0`.)
- Achado paralelo: **dois diretórios de migrations** (`Migrations/` e `Persistence/Migrations/`) — fora do escopo deste plano; investigar à parte.
