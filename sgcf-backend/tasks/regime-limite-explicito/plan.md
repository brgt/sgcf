# Plano de Implementação — Regime de Limite Explícito por Banco

> **Spec:** `docs/specs/limites-banco/SPEC_REGIME_LIMITE_EXPLICITO.md` (v0.2)
> **Spec-mãe:** `docs/specs/limites-banco/SPEC_LIMITE_GLOBAL.md` (v1.1) — esta feature emenda §4.3.
> **Data:** 2026-06-03
> **Objetivo:** Suportar banco que opera só com limite global (Itaú e similares) via flag explícita de regime, com enforcement de teto na cotação e na conversão em contrato (LG-11 + LG-12).

---

## 1. Princípios do plano

- **Default seguro:** a flag `RegimeLimite` nasce `PerModalidade`. Bancos existentes não mudam de comportamento. O backfill marca como `GlobalPuro` apenas quem já operava assim de fato.
- **Fatias verticais:** cada tarefa de enforcement (T4–T8) entrega um caminho completo (validação → handler → teste). As tarefas T1–T3 são fundação compartilhada inevitável (enum + coluna + detecção de regime) e vêm primeiro.
- **Checkpoints entre fases:** build + suite completa verde, sem regressão, antes de avançar.
- **Reuso máximo:** o motor de cálculo (`IConsultaSaldoBanco`, `ConsultaSaldoBancoService`) e os repositórios (`ILimiteGlobalBancoRepository.GetVigenteByBancoAsync`) já existem. Nenhum componente novo de infraestrutura de cálculo.

---

## 2. Grafo de dependências

```
T1 (Domain: enum + Banco.RegimeLimite)
   │
   ├──► T2 (Infra: BancoConfiguration + migration S37 + backfill)
   │        │
   │        └──► T3 (Detecção de regime lê a flag)  ──► [CHECKPOINT A]
   │                       │
   │      ┌────────────────┼─────────────────────────────┐
   │      ▼                ▼                              ▼
   │   T4 (REG-01)     T5 (cadastro regime + REG-02)   (independentes entre si)
   │      │                │
   │      └──────┬─────────┘
   │             ▼
   │        [CHECKPOINT B]
   │             │
   │             ▼
   │        T6 (cotação §4.3)  ──► [CHECKPOINT C — Itaú entra na cotação]
   │             │
   │             ▼
   │        T7 (conversão GlobalPuro / LG-12)
   │             │
   │             ▼
   │        T8 (conversão PerModalidade / LG-11)  ──► [CHECKPOINT D]
   │             │
   │             ▼
   └────────► T9 (docs/api + OpenAPI)  ──► [CHECKPOINT E — review/merge]
```

Caminho crítico: **T1 → T2 → T3 → T6 → T7 → T8**. T4 e T5 podem correr em paralelo após T3. T9 fecha.

---

## 3. Pontos de edição confirmados no código

| Componente | Arquivo | Estado atual |
| --- | --- | --- |
| Entidade banco | `src/Sgcf.Domain/Bancos/Banco.cs` | Sem flag de regime. `IClock` já usado nos métodos. |
| Detecção de regime | `src/Sgcf.Infrastructure/Persistence/Repositories/ConsultaSaldoBancoService.cs` | `BancoEmRegimePerModalityAsync(bancoId, tenantId, ct)` consulta existência de `LimiteBanco`. |
| Adição à cotação | `src/Sgcf.Application/Cotacoes/Commands/AdicionarBancoNaCotacaoCommand.cs` | Injeta `ICotacaoRepository`, `ILimiteBancoRepository`. Exige `LimiteBanco` incondicional (linhas 61–75). |
| Conversão | `src/Sgcf.Application/Cotacoes/Commands/ConverterEmContratoCommand.cs` | Já injeta `ILimiteGlobalBancoRepository`, `IClock`; já busca `limiteGlobalVigente` (l.202) e faz `RegistrarUso` (l.406). Falta ramificar por regime + `IConsultaSaldoBanco`/`ITenantContext`. |
| Criar limite modalidade | `src/Sgcf.Application/Cotacoes/Commands/CreateLimiteBancoCommand.cs` | Já injeta `ILimiteGlobalBancoRepository`. Falta `IBancoRepository` p/ REG-01. |
| Atualizar limite modalidade | `src/Sgcf.Application/Cotacoes/Commands/UpdateLimiteBancoCommand.cs` | Idem REG-01. |
| Cadastro/edição banco | `src/Sgcf.Application/Bancos/Commands/CreateBancoCommand.cs`, `UpdateBancoConfigCommand.cs` | `UpdateBancoConfigCommandHandler(IBancoRepository, IClock)`. |
| Mapeamento EF | `src/Sgcf.Infrastructure/Persistence/Configurations/BancoConfiguration.cs` | — |
| Controller | `src/Sgcf.Api/Controllers/BancosController.cs` | — |

**Padrões a seguir** (extraídos de `CriarLimiteGlobalBancoCommandHandler`):
- `Guid tenantId = tenantContext.TenantId;` (injetar `ITenantContext`).
- `LocalDate hoje = clock.GetCurrentInstant().InZone(fuso).Date;` com fuso `America/Sao_Paulo`.
- Conversão `DateOnly`→`LocalDate` como nas linhas 47–50 do handler de limite global.
- Violação de invariante cruzada → `InvalidOperationException` (mapeada para 409 pelo middleware).

---

## 4. Tarefas

### Fase 0 — Fundação

#### T1 — Domínio: enum `RegimeLimiteBanco` + `Banco.RegimeLimite`
- **Arquivos:** `src/Sgcf.Domain/Bancos/RegimeLimiteBanco.cs` (novo), `src/Sgcf.Domain/Bancos/Banco.cs` (editar).
- **Fazer:** enum `{ PerModalidade=0, GlobalPuro=1 }`; propriedade `RegimeLimite` com default `PerModalidade` e setter privado; método `DefinirRegimeLimite(RegimeLimiteBanco, IClock)` que atualiza `UpdatedAt`.
- **Critério de aceitação:** banco criado via `Banco.Criar(...)` nasce `PerModalidade`; `DefinirRegimeLimite` altera o valor e o `UpdatedAt`.
- **Verificação:** `BancoRegimeLimiteTests` (2 testes) verdes; `dotnet test --filter "FullyQualifiedName~BancoRegimeLimite"`.

#### T2 — Infra: mapeamento + migration `S37` + backfill
- **Arquivos:** `BancoConfiguration.cs` (editar), `Persistence/Migrations/S37_RegimeLimiteBanco.*` (gerado).
- **Fazer:** mapear `regime_limite integer NOT NULL DEFAULT 0`; gerar migration; editar o `Up` para incluir o UPDATE de backfill (banco com `LimiteGlobalBanco` vigente e sem `LimiteBanco` ativo → 1); `Down` remove a coluna.
- **Critério de aceitação:** `dotnet ef database update` aplica sem erro; coluna existe; backfill marca corretamente os bancos elegíveis.
- **Verificação:** `dotnet ef migrations list` mostra S37 aplicada; query SQL confirma `regime_limite=1` só nos bancos elegíveis; gerar relatório dos bancos marcados para conferência do PO.

#### T3 — Detecção de regime lê a flag
- **Arquivo:** `ConsultaSaldoBancoService.cs` (editar `BancoEmRegimePerModalityAsync`).
- **Fazer:** trocar a consulta de "existe LimiteBanco?" por leitura de `banco_config.regime_limite == PerModalidade`.
- **Critério de aceitação:** método retorna o regime conforme a flag; demais métodos de cálculo inalterados.
- **Verificação:** testes existentes de `LimiteGlobalBanco`/`ConsultaSaldoBanco` continuam verdes (ajustar setup dos que dependiam da inferência implícita).

> **[CHECKPOINT A]** `dotnet build` + `dotnet test` (suite completa) verdes. Backfill validado no banco local. Nenhuma mudança de comportamento observável para bancos existentes. **Revisar antes de prosseguir.**

### Fase 1 — Coerência e cadastro

#### T4 — REG-01: bloquear `LimiteBanco` em banco `GlobalPuro`
- **Arquivos:** `CreateLimiteBancoCommand.cs`, `UpdateLimiteBancoCommand.cs` (injetar `IBancoRepository`).
- **Fazer:** antes de criar/atualizar, ler `Banco.RegimeLimite`; se `GlobalPuro` → `InvalidOperationException` (mensagem REG-01, §5.2).
- **Critério de aceitação:** criar/atualizar `LimiteBanco` em banco `GlobalPuro` retorna 409; em banco `PerModalidade` segue normal.
- **Verificação:** `CoerenciaRegimeLimiteTests.CriarLimiteBanco_EmBancoGlobalPuro_Bloqueia` + regressão dos testes existentes de `CreateLimiteBanco`.

#### T5 — Cadastro de regime + REG-02 (Admin-only)
- **Arquivos:** `CreateBancoCommand.cs` (campo opcional `RegimeLimite`, default `PerModalidade`); novo `DefinirRegimeLimiteBancoCommand` + handler (injeta `IBancoRepository`, `IConsultaSaldoBanco`, `ITenantContext`, `IClock`); `BancosController.cs` (endpoint Admin); DTO de banco expõe `regimeLimite`.
- **Decisão de design:** troca de regime fica em **comando dedicado** (não em `UpdateBancoConfigCommand`), porque exige validação REG-02 com `IConsultaSaldoBanco` — mantém o handler de config simples e a regra isolada.
- **Fazer:** mudar para `GlobalPuro` exige zero `LimiteBanco` ativo (senão 409 REG-02); mudar para `PerModalidade` sempre permitido (REG-04). Autorização `Admin`.
- **Critério de aceitação:** endpoint Admin altera regime; REG-02 bloqueia troca para `GlobalPuro` com `LimiteBanco` ativo; `Operador` recebe 403.
- **Verificação:** `CoerenciaRegimeLimiteTests` (REG-02 bloqueia / permite); teste de autorização no `Sgcf.Api.IntegrationTests`.

> **[CHECKPOINT B]** Build + suite verdes. Regime é cadastrável e coerente. **Revisar.**

### Fase 2 — Enforcement na cotação

#### T6 — `AdicionarBancoNaCotacao` ramificado por regime (§4.3)
- **Arquivo:** `AdicionarBancoNaCotacaoCommand.cs` (injetar `IBancoRepository`, `ILimiteGlobalBancoRepository`, `IConsultaSaldoBanco`, `ITenantContext`, `IClock`).
- **Fazer:**
  - `PerModalidade`: exige `LimiteBanco`; `disponivel = min(LimiteBanco.ValorDisponivelBrl, disponivelGlobal)` onde `disponivelGlobal = ValorLimiteGlobal − CalcularUtilizadoAgregadoModalidadesAsync` quando há global vigente (senão sem teto). Bloqueia se `< ValorAlvoBrl`.
  - `GlobalPuro`: exige `LimiteGlobalBanco` vigente (REG-03); `disponivelGlobal = ValorLimiteGlobal − CalcularSaldoDevedorBancoAsync`; bloqueia se `< ValorAlvoBrl`.
  - Preservar o pré-preenchimento de garantia atual no ramo `PerModalidade`.
- **Critério de aceitação:** banco `GlobalPuro` com global suficiente é adicionado; insuficiente/sem global bloqueia; `PerModalidade` mantém comportamento (com novo teto global agregado quando aplicável).
- **Verificação:** `AdicionarBancoRegimeGlobalTests` (4 testes, §8.2) + regressão dos testes atuais de `AdicionarBanco`.

> **[CHECKPOINT C]** Cenário Itaú: banco de linha única pode ser adicionado a cotação em qualquer modalidade. **Smoke manual** com banco de teste `GlobalPuro`. **Revisar.**

### Fase 3 — Enforcement na conversão (LG-11 + LG-12)

#### T7 — `ConverterEmContrato` ramo `GlobalPuro` (LG-12)
- **Arquivo:** `ConverterEmContratoCommand.cs` (adicionar `IConsultaSaldoBanco`, `ITenantContext`).
- **Fazer:** se `GlobalPuro`: exigir global vigente (REG-03); `saldoDevedor + principal > ValorLimiteGlobal` → exceção (LG-12); **não** chamar `RegistrarUso`. Manter leitura+criação na mesma transação.
- **Critério de aceitação:** dentro do teto converte; estourando bloqueia; sem global vigente bloqueia.
- **Verificação:** `ConverterEmContratoRegimeGlobalTests` (§8.3).

#### T8 — `ConverterEmContrato` ramo `PerModalidade` (LG-11)
- **Arquivo:** `ConverterEmContratoCommand.cs` (mesmo handler).
- **Fazer:** se `PerModalidade`: exigir `LimiteBanco` na modalidade (senão bloqueio LG-11); checar `ValorDisponivelBrl ≥ principal`; se há global vigente, checar `CalcularUtilizadoAgregadoModalidadesAsync + principal ≤ ValorLimiteGlobal`; então `RegistrarUso` (comportamento atual) e criar.
- **Critério de aceitação:** todos os caminhos de §8.3.1 (5 testes) cobertos; comportamento atual de `RegistrarUso` preservado quando dentro dos tetos.
- **Verificação:** `ConverterEmContratoLG11Tests` + regressão dos testes atuais de `ConverterEmContrato` (incluindo SC-07 de garantias).

> **[CHECKPOINT D]** Enforcement completo ponta-a-ponta nos dois regimes. Suite completa verde. **Revisar.**

### Fase 4 — Documentação e fechamento

#### T9 — Docs e OpenAPI
- **Arquivos:** `docs/api/bancos.md`, `docs/api/limites-banco.md`; nota de emenda em `SPEC_LIMITE_GLOBAL.md §4.3`.
- **Fazer:** documentar `regimeLimite` no cadastro de banco, endpoint de troca de regime, mensagens de erro (§5.2), comportamento por regime na cotação e conversão; confirmar OpenAPI inclui o novo endpoint.
- **Critério de aceitação:** docs refletem a feature; spec-mãe aponta para a emenda.
- **Verificação:** revisão de doc; swagger gerado lista o endpoint.

> **[CHECKPOINT E]** Suite completa (`dotnet test`) verde; smoke manual do cenário Itaú (cadastro `GlobalPuro` → limite global → cotação → conversão dentro e fora do teto); code review (`/review`). **Go/No-Go para merge.**

---

## 5. Riscos e mitigação

| Risco | Mitigação |
| --- | --- |
| Backfill marca banco errado → muda comportamento | Regra conservadora (só quem já operava em A); relatório pós-migração para conferência do PO antes de seguir (CHECKPOINT A). |
| Testes existentes assumiam detecção implícita de regime | T3 ajusta o setup desses testes; rodar suite completa no CHECKPOINT A. |
| Janela de corrida no teto (dois contratos simultâneos) | Aceita nesta entrega (§4.4); leitura+escrita na mesma transação; lock pessimista fica como "Pergunte Primeiro". |
| Confusão `LimiteCreditoBrl` × `LimiteGlobalBanco` | Spec §3.2 separa explicitamente; plano não toca `LimiteCreditoBrl`. |

---

## 6. Estimativa de esforço (ordem de grandeza)

| Fase | Tarefas | Esforço relativo |
| --- | --- | --- |
| 0 — Fundação | T1, T2, T3 | Médio (migration + ajuste de testes) |
| 1 — Coerência/cadastro | T4, T5 | Médio |
| 2 — Cotação | T6 | Médio |
| 3 — Conversão | T7, T8 | Médio-alto (handler central, regressão SC-07) |
| 4 — Docs | T9 | Baixo |

Sem componentes novos de infraestrutura; reuso do motor de saldo existente reduz o risco e o volume.
