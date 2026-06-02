# Plano de Implementação — Garantias Alternativas (Grupos "OU")

> **Spec:** `SPEC_GARANTIAS_ALTERNATIVAS.md` (raiz)
> **Todo dia a dia:** `tasks/todo_garantias_alternativas.md`
> **Status:** Proposta — aguardando aprovação do plano e da decisão RV-GA
> **Stack:** .NET / ASP.NET Core / EF Core / NodaTime / PostgreSQL (ver `sgcf-backend/CLAUDE.md`)

## Overview

Adicionar ao módulo de Garantias Exigidas o conceito de **Grupo de Alternativas** (satisfação "OU" combinável): itens de tipos distintos, dentro de um grupo, em que a exigência do banco é cumprida quando o contrato cobre a cota do grupo por uma alternativa **ou pela combinação** delas. A entrega é completa (enforcement de conversão + cálculo em cotação + exposição na API/painel) e **aditiva/retrocompatível**: políticas existentes (itens sem grupo) mantêm o comportamento atual.

## Architecture Decisions

- **Campos aditivos nullable** (`GrupoAlternativaId`, `GrupoRotulo`) em `GarantiaExigidaItem` — `NULL` = item legado, comportamento idêntico ao atual. Sem backfill.
- **Grupo sempre obrigatório** (D3): quando `GrupoAlternativaId != null`, o flag `Obrigatoria` é normalizado e não decide enforcement.
- **Cobertura por fração** (RV-GA default): cada alternativa contribui `min(coberto/alvo, 1.0)`; grupo coberto quando soma das frações ≥ 1,0. Algoritmo em `SPEC §4.4`.
- **Regras do projeto preservadas:** `Money` (sem decimal cru), NodaTime + `IClock`, EF só em Infrastructure, camadas (Mcp/A2a não importam Infrastructure).
- **Slicing vertical pragmático:** cada fase entrega um caminho completo e testável (cadastrar+ler → conversão respeita OU → indicadores+snapshot+regressão), respeitando a estratificação obrigatória do backend.

## Dependency Graph

```
[GATE RV-GA] ──(bloqueia Fase 2)
                     │
Fase 1 — Cadastro & Persistência (independe de RV-GA)
  T1 Domain: campos + Spec + normalização (GA-01,04,06)
        │
        ├── T2 Agregado: invariantes GA-02,03,05,07
        │
        └── T3 Migration + EF config (+ colunas snapshot S34)
                │
                └── T4 DTOs + Request + LimitesBancoController (cadastro→leitura via API)
                        │
Fase 2 — Operacionalização (requer RV-GA confirmado)
  T5 CalculadorValorGarantiaExigida: alvo de grupo (RF-09)
        │
        └── T6 AvaliarCobertura: cobertura de grupo + exceção (RF-07/10)
                │
                └── T7 ConverterEmContrato: enforcement e2e (AC-1..AC-7)
                        │
Fase 3 — Exposição consolidada & Regressão
  T8 IndicadoresGarantiaDto reflete grupos (RF-14)
  T9 Snapshot S34 preserva grupo na conversão (RF-13)
  T10 Golden dataset + docs (limites-banco.md)
```

Ordem bottom-up: domínio → persistência → API de cadastro → cálculo → enforcement → exposição/regressão.

---

## Task List

### Fase 1 — Cadastro & Persistência

#### Task 1: Campos de grupo no item de garantia exigida (domínio)

**Description:** Adicionar `GrupoAlternativaId` (Guid?) e `GrupoRotulo` (string?) a `GarantiaExigidaItem` e `GarantiaExigidaItemSpec`. Em `Criar`/`Atualizar`, quando `GrupoAlternativaId != null`, normalizar `Obrigatoria` para o valor canônico (GA-04). Preservar AD-4 (percentual XOR valor fixo).

**Acceptance criteria:**
- [ ] `GarantiaExigidaItem` e `GarantiaExigidaItemSpec` expõem os dois campos (setters privados no item).
- [ ] Item com grupo é sempre tratado como obrigatório (GA-04); item sem grupo mantém semântica atual (GA-01).
- [ ] Validações existentes (AD-4, campos exclusivos) intactas.

**Verification:**
- [ ] Unit tests: `dotnet test tests/Sgcf.Domain.Tests --filter "FullyQualifiedName~GarantiaExigidaItem"`
- [ ] Build: `dotnet build src/Sgcf.Domain`
- [ ] Manual: criar item com/sem grupo cobre GA-01 e GA-04.

**Dependencies:** None
**Files likely touched:** `src/Sgcf.Domain/Cotacoes/GarantiaExigidaItem.cs`, `src/Sgcf.Domain/Cotacoes/GarantiaExigidaItemSpec.cs`, `tests/Sgcf.Domain.Tests/Cotacoes/GarantiaExigidaItemTests.cs`
**Estimated scope:** S (2-3 arquivos)

#### Task 2: Invariantes de agregado para grupos (GarantiaExigidaRevisao)

**Description:** Validar, no agregado, as regras de grupo: GA-02 (grupo com ≥ 2 itens), GA-03 (tipos distintos no grupo — derivado de SR-06), GA-05 (rótulo ≤ 120, consistente no grupo), GA-07 (um tipo em no máximo um grupo). Reaproveitar a verificação de imutabilidade pós-`VigenciaFim` (GA-06/SR-05).

**Acceptance criteria:**
- [ ] Revisão rejeita grupo com 1 item (GA-02) e rótulo > 120 chars (GA-05).
- [ ] Revisão aceita grupo válido com 2+ tipos distintos (GA-03/GA-07).
- [ ] Tentativa de alterar campos de grupo após encerramento lança (GA-06).

**Verification:**
- [ ] Unit tests: `dotnet test tests/Sgcf.Domain.Tests --filter "FullyQualifiedName~GarantiaExigidaRevisao"`
- [ ] Build: `dotnet build src/Sgcf.Domain`

**Dependencies:** Task 1
**Files likely touched:** `src/Sgcf.Domain/Cotacoes/GarantiaExigidaRevisao.cs`, `tests/Sgcf.Domain.Tests/Cotacoes/GarantiaExigidaRevisaoTests.cs`
**Estimated scope:** S (2 arquivos)

#### Task 3: Migration aditiva + mapeamento EF (item + snapshot S34)

**Description:** Migration PostgreSQL adicionando `grupo_alternativa_id uuid NULL` e `grupo_rotulo varchar(120) NULL` em `sgcf.garantia_exigida_item` e na tabela de snapshot S34. Índice parcial `(revisao_id, grupo_alternativa_id) WHERE grupo_alternativa_id IS NOT NULL`. Atualizar `GarantiaExigidaItemConfiguration` (e a config do snapshot).

**Acceptance criteria:**
- [ ] `dotnet ef migrations add S36_GarantiasAlternativas` gera migration aditiva e reversível (Up/Down).
- [ ] `dotnet ef database update` aplica sem erro; linhas legadas ficam com `NULL`.
- [ ] Round-trip: persistir e reler um item com grupo preserva os campos.

**Verification:**
- [ ] `dotnet ef database update --project src/Sgcf.Infrastructure --startup-project src/Sgcf.Api`
- [ ] Integration: `dotnet test tests/Sgcf.Application.Tests --filter "FullyQualifiedName~GarantiaExigida&Category=Slow"`
- [ ] Manual: `\d sgcf.garantia_exigida_item` mostra as colunas e o índice.

**Dependencies:** Task 1, Task 2
**Files likely touched:** `src/Sgcf.Infrastructure/Persistence/Migrations/*S36*`, `src/Sgcf.Infrastructure/Persistence/Configurations/GarantiaExigidaItemConfiguration.cs`, (config snapshot S34)
**Estimated scope:** M (3-4 arquivos)

#### Task 4: Cadastro e leitura de grupo via API (DTOs + Request + Controller)

**Description:** Acrescentar `GrupoAlternativaId`/`GrupoRotulo` a `GarantiaExigidaItemDto`, `CriarGarantiaExigidaItemRequest` e ao DTO de snapshot. Propagar os campos no PATCH de garantias do `LimitesBancoController` e no retorno de `GET /limites-banco/{id}/revisoes-garantias`. Campos aditivos e opcionais (RF-15).

**Acceptance criteria:**
- [ ] PATCH cria um grupo "CDB OU Recebíveis"; `GET revisoes-garantias` retorna os itens com o mesmo `GrupoAlternativaId` e rótulo.
- [ ] Requests sem campos de grupo continuam válidos (retrocompatível).
- [ ] Nenhuma rota/contrato existente quebrado.

**Verification:**
- [ ] Integration: `dotnet test tests/Sgcf.Api.IntegrationTests --filter "FullyQualifiedName~LimitesBanco"`
- [ ] Build: `dotnet build`
- [ ] Manual: `curl` PATCH + GET confirmam o agrupamento.

**Dependencies:** Task 3
**Files likely touched:** `src/Sgcf.Application/Cotacoes/GarantiaExigidaItemDto.cs`, `src/Sgcf.Application/Cotacoes/CriarGarantiaExigidaItemRequest.cs`, `src/Sgcf.Application/Contratos/GarantiaExigidaSnapshotItemDto.cs`, `src/Sgcf.Api/Controllers/LimitesBancoController.cs`, `tests/Sgcf.Api.IntegrationTests/...`
**Estimated scope:** M (4-5 arquivos)

### Checkpoint: Fase 1 (Cadastro & Persistência)
- [ ] `dotnet build` limpo e `dotnet test --filter "Category!=Slow"` verde.
- [ ] Migration aplica em base existente sem afetar linhas legadas.
- [ ] É possível cadastrar e reler um grupo "CDB OU Recebíveis" via API.
- [ ] **Revisar com o humano antes da Fase 2.**

---

### ⛔ GATE RV-GA (antes da Fase 2)
- [ ] Decisão **RV-GA** confirmada (regra de fração *default* ou Alternativa B). Bloqueia T5/T6/T7 e os números dos testes/golden.

### Fase 2 — Operacionalização (enforcement)

#### Task 5: Cálculo de valor exigido com grupos

**Description:** Estender `CalculadorValorGarantiaExigida` para que um grupo contribua ao "valor total exigido" com o **alvo do grupo** (conforme RV-GA), não a soma das alternativas. Itens sem grupo continuam somando (comportamento atual). Função pura.

**Acceptance criteria:**
- [ ] Grupo "CDB 100% OU Recebíveis 120%" contribui o alvo do grupo (não 220%).
- [ ] Itens sem grupo mantêm a soma atual (sem regressão).
- [ ] Função permanece pura (sem I/O).

**Verification:**
- [ ] Unit: `dotnet test tests/Sgcf.Application.Tests --filter "FullyQualifiedName~CalculadorValorGarantiaExigida"`

**Dependencies:** Task 1, GATE RV-GA
**Files likely touched:** `src/Sgcf.Application/Cotacoes/CalculadorValorGarantiaExigida.cs`, `tests/Sgcf.Application.Tests/Cotacoes/CalculadorValorGarantiaExigidaTests.cs`
**Estimated scope:** S (2 arquivos)

#### Task 6: Avaliação de cobertura de grupo + exceção

**Description:** Implementar `AvaliarCoberturaGrupo` (algoritmo de fração, SPEC §4.4): por grupo, somar `min(coberto/alvo, 1.0)` por alternativa; lacuna se soma < 1,0. Itens obrigatórios sem grupo seguem avaliação individual (RF-08). Estender `GarantiaExigidaNaoCobertaException`/`LacunaGarantia` para distinguir lacuna de **grupo** (alternativas aceitas + fração coberta) de lacuna de item.

**Acceptance criteria:**
- [ ] Cenários AC-1..AC-7 (nível de função) produzem liberado/bloqueado corretos.
- [ ] Lacuna de grupo lista as alternativas aceitas e a fração coberta.
- [ ] Itens sem grupo: comportamento atual inalterado.

**Verification:**
- [ ] Unit: `dotnet test tests/Sgcf.Application.Tests --filter "FullyQualifiedName~AvaliarCobertura"`

**Dependencies:** Task 5
**Files likely touched:** `src/Sgcf.Application/Cotacoes/Commands/ConverterEmContratoCommand.cs`, `src/Sgcf.Application/Cotacoes/Exceptions/GarantiaExigidaNaoCobertaException.cs`, `tests/Sgcf.Application.Tests/...`
**Estimated scope:** M (3 arquivos)

#### Task 7: Enforcement end-to-end na conversão em contrato

**Description:** Garantir que `ConverterEmContratoCommand` aplica a cobertura de grupo antes de persistir (mantendo a ordem atual: enforcement antes de `Contrato.Criar`). Teste de integração com Testcontainers cobrindo AC-1..AC-7 ponta a ponta.

**Acceptance criteria:**
- [ ] AC-1/AC-2/AC-3 liberam a conversão; AC-4/AC-5/AC-6 bloqueiam com a lacuna correta.
- [ ] AC-7 (política legada) mantém comportamento atual.
- [ ] Falha de enforcement não persiste estado parcial.

**Verification:**
- [ ] Integration: `dotnet test tests/Sgcf.Api.IntegrationTests --filter "FullyQualifiedName~ConverterEmContrato"` (base `sgcf_garantias_alt_e2e`)

**Dependencies:** Task 6
**Files likely touched:** `src/Sgcf.Application/Cotacoes/Commands/ConverterEmContratoCommand.cs`, `tests/Sgcf.Api.IntegrationTests/Cotacoes/ConverterEmContratoGruposTests.cs`
**Estimated scope:** M (2-3 arquivos)

### Checkpoint: Fase 2 (Operacionalização)
- [ ] Conversão respeita grupos OU ponta a ponta (AC-1..AC-7 verdes).
- [ ] Sem regressão em políticas legadas.
- [ ] **Revisar com o humano antes da Fase 3.**

---

### Fase 3 — Exposição consolidada & Regressão

#### Task 8: Indicadores de garantia refletem grupos

**Description:** Ajustar a query/handler de `IndicadoresGarantiaDto` para considerar grupos OU sem dupla contagem do alvo (RF-14). Validar `PercentualCoberturaTotalPct`, cobertura líquida sem CDB, etc., na presença de grupos.

**Acceptance criteria:**
- [ ] Indicadores de um contrato coberto via grupo refletem cobertura coerente (sem somar alvos de alternativas).
- [ ] Contratos sem grupo: indicadores inalterados.

**Verification:**
- [ ] Tests: `dotnet test --filter "FullyQualifiedName~IndicadoresGarantia"`

**Dependencies:** Task 7
**Files likely touched:** `src/Sgcf.Application/Contratos/Queries/GetIndicadoresGarantiaQuery.cs` (+ handler), `tests/...`
**Estimated scope:** M (2-3 arquivos)

#### Task 9: Snapshot S34 preserva grupo na conversão

**Description:** No momento da conversão, o snapshot de garantias exigidas do contrato passa a gravar `GrupoAlternativaId`/`GrupoRotulo` (RF-13), garantindo reprodutibilidade histórica da política aplicada.

**Acceptance criteria:**
- [ ] Conversão com grupo grava os campos no snapshot.
- [ ] Releitura do snapshot retorna o agrupamento idêntico ao vigente na conversão.

**Verification:**
- [ ] Integration: `dotnet test tests/Sgcf.Api.IntegrationTests --filter "FullyQualifiedName~Snapshot"`

**Dependencies:** Task 7
**Files likely touched:** `src/Sgcf.Application/Cotacoes/Commands/ConverterEmContratoCommand.cs` (gravação snapshot), `src/Sgcf.Application/Contratos/GarantiaExigidaSnapshotItemDto.cs`, `tests/...`
**Estimated scope:** S-M (2-3 arquivos)

#### Task 10: Golden dataset + documentação de API

**Description:** Adicionar casos golden JSON para "CDB OU Recebíveis" (cobertura por uma alternativa, por combinação e não-cobertura) sem alterar expected outputs existentes. Atualizar `sgcf-backend/docs/api/limites-banco.md` documentando os novos campos.

**Acceptance criteria:**
- [ ] Novos casos golden passam: `dotnet test tests/Sgcf.GoldenDataset`.
- [ ] Nenhum expected output pré-existente alterado.
- [ ] Docs de API descrevem `GrupoAlternativaId`/`GrupoRotulo` e a semântica OU.

**Verification:**
- [ ] `dotnet test tests/Sgcf.GoldenDataset/Sgcf.GoldenDataset.csproj`
- [ ] Revisão da doc.

**Dependencies:** Task 7 (algoritmo estável); pode rodar em paralelo a T8/T9
**Files likely touched:** `tests/Sgcf.GoldenDataset/data/*.json`, `sgcf-backend/docs/api/limites-banco.md`
**Estimated scope:** S-M

### Checkpoint: Completo
- [ ] Todos os critérios de aceite atendidos; suíte completa (`dotnet test`) verde, incluindo `Category=Slow` e golden.
- [ ] API expõe grupos por limite e por contrato; snapshot preserva histórico.
- [ ] Migration aditiva validada em base existente.
- [ ] Pronto para revisão de código (`/review`) e PR.

---

## Risks and Mitigations

| Risco | Impacto | Mitigação |
|---|---|---|
| RV-GA não confirmada (regra de combinação) | Alto | GATE explícito antes da Fase 2; Fase 1 prossegue sem depender disso |
| Regressão em políticas legadas (itens sem grupo) | Alto | Campos nullable; AC-7 e testes de não-regressão; migration sem backfill |
| SR-06 vs grupos (tipo duplicado) | Médio | GA-03/GA-07 mantêm tipos distintos; sem relaxar SR-06 |
| Dupla contagem de alvo nos indicadores | Médio | T8 dedicada + testes de indicadores com grupo |
| Snapshot S34 perder semântica de grupo | Médio | T9 grava campos no snapshot; teste de round-trip |
| Migration em base com dados | Médio | Aditiva/reversível; testar `database update` na base dev existente |

## Open Questions

- **RV-GA (bloqueante):** confirmar regra de combinação — fração (default) vs Alternativa B (alvo = menor valor exigido). Define o algoritmo de T6 e os números de T7/T10.
- Rótulo do grupo é editável após criação enquanto a revisão está vigente? (assumido: sim, com GA-05; confirmar.)
- Indicadores: ao combinar alternativas, exibir a fração por alternativa ou só o consolidado do grupo? (assumido: consolidado; detalhamento é refinamento futuro.)

## Parallelização

- **Sequencial:** T1→T2→T3→T4 (foundation); T5→T6→T7 (enforcement, pós-GATE).
- **Paralelizável após T7:** T8, T9 e T10 são independentes entre si.
- **Coordenação:** contrato de API (campos de grupo nos DTOs) é fixado em T4 antes de qualquer trabalho de exposição.
