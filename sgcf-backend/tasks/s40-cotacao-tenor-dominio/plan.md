# Plano de Implementação — S40 Cotação: Tenor, Campos de Domínio, PTAX Multimoeda e Erros RFC 7807

> **Spec:** `docs/specs/cotacoes/SPEC_S40_TENOR_DOMINIO_PTAX.md` (v1.0)
> **Handover FE:** `docs/api/S40_FE_HANDOVER.md`
> **Data:** 2026-06-06
> **Objetivo:** Persistir o prazo da cotação como tenor `{valor, unidade}` derivando `prazoMaximoDias` (30/360); adicionar campos de domínio opcionais; generalizar a resolução de PTAX para múltiplas moedas; padronizar erros em RFC 7807; expor validação suave via `alertas[]`.

---

## 1. Princípios do plano

- **Fundação compartilhada primeiro:** a entidade `Cotacao`, a configuração EF e a migração tocam um arquivo cada e são pré-requisito de todas as fatias. São inevitavelmente compartilhadas (mesmo padrão de `tasks/regime-limite-explicito`); por isso vêm na Fase 0, uma única vez, em vez de reabrir a raiz do agregado a cada fatia.
- **Fatias verticais a partir da Fase 1:** cada tarefa de Application/API entrega um caminho completo (request → validação → handler → DTO → teste de integração) de uma capacidade isolada.
- **Estratégia "Opção B" (não destrutiva):** preservar campos canônicos (`prazoMaximoDias`, `ptaxUsadaUsdBrl`) e adicionar camadas de intenção/generalização. Nenhuma migração destrutiva.
- **Checkpoints entre fases:** `dotnet build` + suíte alvo verde, migração aplicável, zero regressão, antes de avançar.
- **Reuso máximo:** `IResolveTipoCotacaoService.ResolverFxAsync` já recebe `Moeda` — a generalização de PTAX é troca de argumento, não componente novo. `GlobalExceptionHandler` já mapeia ProblemDetails — apenas estende-se o catálogo.

---

## 2. Decisões de design (a confirmar na execução)

| # | Decisão | Justificativa |
|---|---------|---------------|
| AD-01 | `Cotacao.Criar` recebe um _options record_ `DadosDominioCotacao` (moedaAlvo, carenciaMeses, indexadorBase, estruturantes FGI) + par de tenor, em vez de adicionar 8 parâmetros posicionais | Evita factory com 15 argumentos; mantém legibilidade; um único ponto de evolução |
| AD-02 | Conversão 30/360 como aritmética pura (`valor * 30`), **não** `NodaTime.Period` | Não é duração de calendário real; é teto comparável (Spec §1.3) |
| AD-03 | `IndexadorBase` mapeado em **colunas planas** (owned ou propriedades diretas) | Decisão da spec §3; queryável e simples para conjunto fixo de campos |
| AD-04 | `ptaxUsada` novo campo canônico; `ptaxUsadaUsdBrl` depreciado (espelha quando `moedaAlvo=Usd`) | Generaliza PTAX sem quebrar leitura legada |
| AD-05 | `PtaxIndisponivelException : InvalidOperationException` e `ConflitoDeEstadoException : InvalidOperationException` | Permite mapeamento central tipado; o handler testa o subtipo específico antes do genérico |
| AD-06 | `alertas[]` adicionado a `CotacaoDto` (transiente, `[]` em leitura) em vez de um envelope novo | Mantém desserialização uniforme no FE; retrocompatível |

> **AD-01 a confirmar:** se o time preferir manter parâmetros posicionais, a primeira fatia deve ser revista. Recomendação do plano: _options record_.

---

## 3. Grafo de dependências

```
Fundação (Fase 0)
  E1 enums (UnidadePrazo, TipoIndexador)
        └─> E2 IndexadorBase (VO)
        └─> E3 Cotacao (tenor + moedaAlvo + carencia + indexador + FGI + ptaxUsada + invariantes)
                  └─> I1 CotacaoConfiguration ──> I2 Migration S40 (+ backfill)

Erros (Fase 2 — cross-cutting)
  X1 PtaxIndisponivelException ┐
  X2 ConflitoDeEstadoException ┘──> H1 GlobalExceptionHandler ──> C1 CotacoesController (remove catches)

Capacidades (Fases 1, 3, 4 — verticais)
  S1 ResolvedorTenor (puro) ─┐
  S2 GeradorAlertas (puro) ──┤
  D1 AlertaDto ──────────────┤
  D2 CotacaoDto (+campos) ───┤
                             ├─> A1 CriarCotacaoCommand (+validator +handler)
                             └─> A2 AtualizarCotacaoCommand (+validator +handler)
  R1 PTAX por moedaAlvo ─────────> (usado por A1/refresh)
```

**Caminho crítico:** `E1 → E3 → I1 → I2` (fundação) e `X1/X2 → H1 → C1` (erros). A fatia de PTAX (A1+R1) depende das duas.

**Ordem de entrega de valor:** Tenor (core) sai logo após a fundação, antes da refatoração de erros, porque não depende dela.

---

## 4. Fases e tarefas

### Fase 0 — Fundação (domínio + persistência)
- **T1** Enums + VO `IndexadorBase`
- **T2** Entidade `Cotacao`: tenor, derivação 30/360, `moedaAlvo`, `carenciaMeses`, `indexadorBase`, estruturantes FGI, `ptaxUsada`, invariantes por modalidade
- **T3** EF `CotacaoConfiguration` + migração `S40` aditiva (nullable → backfill → constraints → NOT NULL no tenor)
- **CHECKPOINT A:** build + Domain/Application/Integration verdes; migração aplica em base com linhas; sem regressão de comportamento

### Fase 1 — Tenor (vertical, core)
- **T4** Application: `ResolvedorTenor` (puro), precedência tenor em `CriarCotacaoCommand`/`AtualizarCotacaoCommand`, `AlertaDto`, alerta `prazo-recalculado`, `CotacaoDto` (3 campos de prazo + `alertas`)
- **T5** Integração HTTP: critérios de aceite de tenor (Spec §11)
- **CHECKPOINT B:** POST/PATCH/GET tratam tenor; caminho legado (`prazoMaximoDias`) intacto

### Fase 2 — Erros RFC 7807 (cross-cutting / breaking change)
- **T6** Exceções tipadas + `GlobalExceptionHandler` (catálogo §5.2, base `sgcf.nordware.io`)
- **T7** Remover `catch (InvalidOperationException) → Conflict(new { error })` de `CotacoesController`; todos os 409 viram ProblemDetails
- **CHECKPOINT C:** breaking change validada; integração cobre shape ProblemDetails dos 409

### Fase 3 — PTAX multimoeda
- **T8** `moedaAlvo` end-to-end: generalizar `ResolverFxAsync(moedaAlvo, ...)`, `PtaxIndisponivelException` (+extensões), herança de moeda em Refinimp, refresh de mercado; testes
- **CHECKPOINT D:** Lei4131 `Eur` resolve PTAX EUR/BRL; `ptaxUsada` preenchido, `ptaxUsadaUsdBrl=null`; 409 de PTAX tipado

### Fase 4 — Campos de domínio
- **T9** `carenciaMeses` + `indexadorBase` (validação dura + suave) end-to-end + testes
- **T10** Estruturantes FGI (`finalidadeBndes`, `bancoRepassadorPretendido`, `percentualCoberturaFgi`) end-to-end + testes
- **CHECKPOINT E:** campos de domínio persistem, validam e retornam; alertas suaves corretos

### Fase 5 — Consolidação e release
- **T11** Faixas de prazo (alerta suave) + consolidação do `GeradorAlertasCotacao` (todos os códigos)
- **T12** Bump OpenAPI `0.12.0`, regenerar contrato, sincronizar handover, suíte completa
- **CHECKPOINT F (go/no-go):** suíte completa verde; critérios de aceite §11 cobertos; changelog publicado

---

## 5. Riscos e mitigações

| Risco | Mitigação |
|-------|-----------|
| Mudança de assinatura de `Cotacao.Criar` quebra testes/call-sites | AD-01 (options record) + atualizar call-site único (handler) e testes na T2 |
| Backfill sob `FORCE ROW LEVEL SECURITY` afetar 0 linhas em produção | Ambiente atual é de teste (papel superusuário ignora RLS). Registrar follow-up de produção (rodar migração com `BYPASSRLS`) — Spec §12 |
| Breaking change de erros (`{error}` → ProblemDetails) afetar consumidores além do FE | Isolar na Fase 2 com checkpoint próprio; handover FE §2 já documenta; varrer demais controllers que dependem do shape antigo |
| `moedaAlvo` não-USD sem PTAX cadastrada na base de teste | Seed de PTAX EUR/BRL nos testes de integração da T8 |
| `indexadorBase` owned vs colunas diretas divergir do snapshot EF | Validar `dotnet ef migrations add` sem _diff_ inesperado na T3 |

---

## 6. Fora de escopo (reafirmado)

- Day-count financeiro do CET (permanece na proposta/contrato).
- Campos de fase posterior na cotação (`percentualRefinanciado`, `nceNumero`, `bancoMandatario`, `paisCredor`).
- Promoção de listas FGI/BNDES a enum (string livre + validação suave por ora).
- Migração do `type` de garantia de `sgcf.io` para `sgcf.nordware.io` (follow-up coordenado).
- UI super-admin e quaisquer mudanças de front-end (tratadas no plano do FE).

---

## 7. Estratégia de verificação por fase

```bash
# Rápido (a cada tarefa de domínio/aplicação):
dotnet test --filter "Category!=Slow"

# Foco por capacidade:
dotnet test --filter "FullyQualifiedName~Cotacao&Category!=Slow"

# Integração HTTP (checkpoints B–F):
dotnet test tests/Sgcf.Api.IntegrationTests

# Migração:
dotnet ef migrations add S40_CotacaoTenorEDominio --project src/Sgcf.Infrastructure --startup-project src/Sgcf.Api
dotnet ef database update --project src/Sgcf.Infrastructure --startup-project src/Sgcf.Api

# Suíte completa (CHECKPOINT F):
dotnet test
```
