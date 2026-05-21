# SPEC — Cockpit SGCF Multi-Persona (Backend)

> **Status:** Draft para aprovação
> **Data:** 2026-05-20
> **Versão:** v1.0
> **Audiência:** Time de backend SGCF, líder de arquitetura, PO de Tesouraria
> **Plano de execução:** `tasks/plan_cockpit_backend_gaps.md` v1.1
> **UX de referência:** `nordware-landing/docs/2.ui_design/UI_TESOURARIA/10_COCKPIT_UX_SPEC_MULTI_PERSONA.md`
> **Catálogo de gaps:** `nordware-landing/docs/2.ui_design/UI_TESOURARIA/11_BACKEND_API_GAPS_COCKPIT.md`
> **Guia FE para não-gaps:** `nordware-landing/docs/2.ui_design/UI_TESOURARIA/12_BACKEND_API_COCKPIT_FE_GUIDE.md`

---

## 1. Objetivo

Entregar no backend SGCF os endpoints, agregados e capacidades necessários para o cockpit multi-persona (CFO, Gerente Financeiro, Gerente de Tesouraria) operar conforme a especificação UX. O escopo cobre apenas os **gaps reais** identificados na análise de 2026-05-20 — itens cobríveis com endpoints existentes ficam no guia FE (documento 12).

### 1.1 Personas

| Persona | Pergunta central | Cobertura no MVP do cockpit |
|---------|-------------------|------------------------------|
| **CFO** | "Qual é o custo, o risco e o retorno do nosso endividamento?" | Fase 1 (breakdown modalidade, curva multi-ano, estrutura capital) |
| **Gerente Financeiro** | "Os contratos estão sendo executados conforme planejado?" | Fase 2 (inadimplência) + Guia FE (funil, a vencer, headroom) |
| **Gerente de Tesouraria** | "Tenho caixa hoje e estou protegido?" | Fase 3 (posição caixa, fluxo diário, efetividade hedge) |

### 1.2 Métricas de sucesso

- **Time-to-insight do CFO** menor que 30 s para alertas críticos.
- **Taxa de ação por alerta** maior que 70%.
- **P95 dos endpoints de painel** menor que 800 ms; `GET /alertas/contadores` menor que 200 ms.
- **Zero divergência** entre `Σ breakdownModalidade.valorBrl` e `painelDivida.dividaBrutaBrl` (consistência).

---

## 2. Escopo

### 2.1 Dentro do escopo deste SPEC (Fases 0 a 3)

| Task | Gap | SPEC dedicado |
|------|-----|---------------|
| 0.1 | Envelope `{ data, meta }` (ADR-019) | `SPEC_0_1_envelope.md` |
| 0.2 | Agregado `Alerta` unificado | `SPEC_0_2_alerta_dominio.md` |
| 0.3 | Endpoints REST de alertas | `SPEC_0_3_alertas_endpoints.md` |
| 0.4 | Rules engine inicial | `SPEC_0_4_rules_engine.md` |
| 0.5 | Expansão `StatusCotacao` | `SPEC_0_5_status_cotacao.md` |
| 1.1 | GAP-CKP-01 Breakdown modalidade | `SPEC_1_1_breakdown_modalidade.md` |
| 1.2 | GAP-CKP-03 Curva vencimentos multi-ano | `SPEC_1_2_curva_vencimentos.md` |
| 1.3 | GAP-CKP-04 Estrutura de capital | `SPEC_1_3_estrutura_capital.md` |
| 2.1 | GAP-CKP-07 Inadimplência agregada | `SPEC_2_1_inadimplencia.md` |
| 3.1 | Domínio `ContaBancaria` | `SPEC_3_1_conta_bancaria.md` |
| 3.2 | GAP-CKP-08 Posição de caixa | `SPEC_3_2_posicao_caixa.md` |
| 3.3 | GAP-CKP-09 Fluxo de caixa diário | `SPEC_3_3_fluxo_caixa.md` |
| 3.4 | GAP-CKP-10 Efetividade de hedge | `SPEC_3_4_hedge_efetividade.md` |

### 2.2 Fora do escopo deste SPEC

Fases 4 (P1) e 5 (P2) — listadas no plano para visibilidade, mas SPECs serão escritos quando as tasks forem iniciadas:

- GAP-CKP-11 Hedge histórico
- GAP-CKP-12 Sensibilidade indexadores
- GAP-CKP-13 Covenants
- GAP-CKP-14 SOFR/Selic
- GAP-CKP-15 Orçamento
- GAP-CKP-16 Workflow documentos
- GAP-CKP-17 Conformidade regulatória
- GAP-CKP-18 Tarifas/IOF agregados
- GAP-CKP-19 `TaxaIndicativaAa` em `Proposta`
- GAP-CKP-21 Economia tributária
- GAP-CKP-22 Produtividade equipe
- GAP-CKP-23 WebSocket/SSE
- GAP-CKP-24 Preferências (parte backend)
- GAP-CKP-25 Exportação assíncrona

Itens cobertos por **guia FE** (não-gaps) não recebem SPEC backend:

- GAP-CKP-05 Funil cotações
- GAP-CKP-06 A vencer
- GAP-CKP-14 (parte CDI)
- GAP-CKP-19 (parte cobrível com `SpreadAaPercentual`)
- GAP-CKP-20 Headroom de crédito

---

## 3. Tech Stack

Conforme `sgcf-backend/CLAUDE.md` (regras inegociáveis aplicam-se a todos os SPECs subordinados):

- **.NET 11**, ASP.NET Core 11, EF Core 11.
- **MediatR** para Commands/Queries.
- **NodaTime** para datas (`LocalDate`, `Instant`, `IClock` injetado).
- **PostgreSQL 16** + **Redis 7** (cache de cotações spot).
- **xUnit + FsCheck + FluentAssertions + Testcontainers** para testes.
- **Money value object** (`Sgcf.Domain.Financeiro.Money`) — nunca `decimal` cru para valores monetários.
- **Rounding** sempre `MidpointRounding.AwayFromZero` (HalfUp).
- **Timezone** datas brasileiras via `DateTimeZoneProviders.Tzdb["America/Sao_Paulo"]`.

---

## 4. Commands

Comuns a todos os SPECs subordinados:

```bash
# Build
dotnet build sgcf-backend.sln

# Testes rápidos (sem Testcontainers)
dotnet test --filter "Category!=Slow"

# Testes completos (CI)
dotnet test

# Cobertura
dotnet test --collect:"XPlat Code Coverage" --results-directory ./coverage

# Aplicar migration
dotnet ef database update --project src/Sgcf.Infrastructure --startup-project src/Sgcf.Api

# Subir API local
dotnet run --project src/Sgcf.Api

# Infraestrutura local (Postgres + Redis)
docker compose -f infra/dev/docker-compose.yml up -d
```

---

## 5. Project Structure

Cada task respeita o layout existente:

```
src/Sgcf.Domain/<Modulo>/            ← Entidades, value objects, enums
src/Sgcf.Application/<Modulo>/
    Commands/                        ← MediatR commands + handlers
    Queries/                         ← MediatR queries + handlers + DTOs
src/Sgcf.Infrastructure/
    Persistence/Configurations/      ← EF Core entity configurations
    Migrations/                      ← EF Core migrations
src/Sgcf.Api/Controllers/            ← Controllers HTTP
src/Sgcf.Jobs/                       ← Hosted services e jobs agendados
tests/Sgcf.Domain.Tests/             ← Unit tests do domínio
tests/Sgcf.Application.Tests/        ← Tests com Testcontainers
tests/Sgcf.Api.IntegrationTests/     ← End-to-end via WebApplicationFactory
docs/adr/                            ← ADRs
docs/specs/cockpit/                  ← Estes SPECs
```

Módulos novos criados por esta entrega:

```
src/Sgcf.Domain/Alertas/             ← Agregado Alerta unificado (Task 0.2) — implementa ITenantScoped
src/Sgcf.Domain/Tesouraria/          ← ContaBancaria, SaldoCaixa, EventoFluxoCaixa — implementam ITenantScoped
src/Sgcf.Application/Common/         ← EnvelopeResponse<T>, EnvelopeMeta
```

> **Regra multi-tenant:** todo domínio listado acima deve implementar `ITenantScoped` e ter
> `tenant_id UUID NOT NULL` na migration correspondente, com RLS policy ativa na tabela.

---

## 6. Code Style

Snippet representativo (Query handler de painel):

```csharp
using MediatR;
using NodaTime;
using NodaTime.TimeZones;
using Sgcf.Application.Common;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;

namespace Sgcf.Application.Painel.Queries;

public sealed record GetBreakdownModalidadeQuery() : IRequest<EnvelopeResponse<BreakdownModalidadeDto>>;

public sealed class GetBreakdownModalidadeQueryHandler(
    IContratoRepository contratoRepo,
    ICotacaoSpotCache spotCache,
    ICotacaoFxRepository cotacaoFxRepo,
    IClock clock)
    : IRequestHandler<GetBreakdownModalidadeQuery, EnvelopeResponse<BreakdownModalidadeDto>>
{
    private static readonly DateTimeZone FusoBrasilia =
        DateTimeZoneProviders.Tzdb["America/Sao_Paulo"];

    public async Task<EnvelopeResponse<BreakdownModalidadeDto>> Handle(
        GetBreakdownModalidadeQuery query,
        CancellationToken cancellationToken)
    {
        Instant agora = clock.GetCurrentInstant();
        LocalDate hoje = agora.InZone(FusoBrasilia).Date;

        IReadOnlyList<Contrato> ativos = await contratoRepo
            .ListByStatusAsync(StatusContrato.Ativo, cancellationToken);

        // ... agregação por modalidade com conversão BRL

        BreakdownModalidadeDto data = new(
            DataHoraCalculo: agora,
            TotalBrl: total,
            Itens: linhas);

        return EnvelopeResponse.Ok(data, agora, fontesConsultadas);
    }
}
```

**Convenções aplicáveis a toda a entrega do cockpit:**

- Nomes de domínio em **português** (`Alerta`, `ContaBancaria`, `SaldoCaixa`).
- Nomes técnicos em **inglês** (`Handler`, `Repository`, `Controller`, `EnvelopeResponse`).
- DTOs como `sealed record` com propriedades imutáveis.
- Construtores primários quando há injeção de dependências.
- Nunca `DateTime.Now`; sempre `IClock`.
- Arredondamento `AwayFromZero` em toda operação monetária.

---

## 7. Testing Strategy

| Nível | Frame | Objetivo | Onde |
|-------|-------|----------|------|
| Unit | xUnit + FsCheck + FluentAssertions | Comportamento puro do domínio (máquinas de estado, cálculos) | `tests/Sgcf.Domain.Tests/` |
| Integration | xUnit + Testcontainers (Postgres) | Handlers MediatR + EF Core contra DB real | `tests/Sgcf.Application.Tests/` |
| API | xUnit + WebApplicationFactory | Contratos HTTP + autenticação + envelope | `tests/Sgcf.Api.IntegrationTests/` |
| Golden | Datasets JSON | Regressão de cálculos financeiros | `tests/Sgcf.GoldenDataset/` |

**Cobertura mínima por task desta entrega:** 80% de linhas no handler MediatR + 100% das transições de máquina de estado introduzidas.

**Convenções:**

- Testes seguem `Arrange / Act / Assert` separados por linhas em branco.
- Fixtures de dados em `tests/<projeto>/Fixtures/` quando reutilizadas.
- Testes que sobem container marcados `[Trait("Category", "Slow")]`.

---

## 8. Boundaries

### 8.1 Always do

- Aplicar envelope `{ data, meta }` (ADR-019) em **todo endpoint novo** desta entrega.
- Filtrar dados por claims de perfil (`CFO`, `FINANCEIRO`, `TESOURARIA`) quando o agregado tem `PerfisVisiveis`.
- Registrar mutações sensíveis (preferências, saldos, dispensa de alertas) em `AuditLog`.
- Aceitar `Idempotency-Key` em endpoints `POST` que disparam efeitos colaterais (criação de alerta, saldos, eventos de fluxo).
- Usar `IClock` injetado.
- Retornar `RFC 7807 Problem Details` em erros 4xx/5xx (padrão atual `ProblemDetails` mantido).
- Documentar cada endpoint novo em Swagger com `[ProducesResponseType<EnvelopeResponse<T>>]`.
- Migrations EF Core nomeadas com data + descrição (`<ts>_<Descricao>.cs`).
- **[Multi-tenant — obrigatório]** Toda entidade nova de domínio operacional (`Alerta`, `ContaBancaria`, `SaldoCaixa`, `EventoFluxoCaixa`) deve implementar `ITenantScoped` desde o nascimento. Sem `ITenantScoped`, o `TenantSaveInterceptor` não preenche `tenant_id` automaticamente e o EF global filter não isola as linhas.
- **[Multi-tenant — obrigatório]** Toda migration de tabela operacional nova deve incluir `tenant_id UUID NOT NULL` + índice composto `(tenant_id, ...)` + RLS policy (`CREATE POLICY ... USING (tenant_id = current_setting('app.tenant_id')::uuid)`). Sem RLS policy a tabela fica exposta na segunda camada de isolação.
- **[Multi-tenant — obrigatório]** Testes de integração (Testcontainers) devem usar fixture com `ITenantContext` resolvido (`IsResolved=true`, `TenantId=TestTenantId`) e registrar `TenantSaveInterceptor`. O EF global filter pós-C1 retorna zero linhas para contexto não resolvido.

### 8.2 Ask first

- Mudar contrato de endpoint **existente** (legados ainda usam payload direto sem envelope).
- Remover qualquer campo dos DTOs existentes (`PainelDividaDto.alertas`, etc.) — depreciação tem janela de 2 sprints.
- Adicionar coluna em tabela existente (`contrato`, `cotacao`, `parcela`) — verificar impacto em jobs e auditoria.
- Adicionar dependência NuGet — registrar em `Directory.Packages.props` e justificar.
- Alterar a estratégia de cache Redis (TTL, chaves) — afeta consistência multi-instância.

### 8.3 Never do

- Usar `DateTime.Now`, `DateTime.UtcNow`, `DateTimeOffset.UtcNow` em domínio ou application.
- Usar `decimal` cru para valores monetários — sempre `Money`.
- Usar `Math.Round(value, 2)` sem `MidpointRounding.AwayFromZero`.
- Importar `Sgcf.Infrastructure` a partir de `Sgcf.Mcp` ou `Sgcf.A2a`.
- Inserir lógica de cálculo financeiro em controller, handler ou repositório — deve ficar em domain service puro.
- Persistir dados em UTC com timezone implícito — sempre `Instant` (UTC) ou `LocalDate` (sem zona).
- Subir migration que descarta coluna sem aprovação de DBA e janela de deprecação.
- Bypassar `Authorize` policy em endpoint novo.
- Commitar segredos, connection strings ou tokens.

---

## 9. Success Criteria do Cockpit MVP

O cockpit é considerado entregue quando:

- [ ] Todos os 13 SPECs (Fases 0-3) implementados, com testes passando em CI.
- [ ] CFO abre o cockpit e vê: banner de alertas críticos, breakdown de modalidade, curva 36m, estrutura de capital (Dívida/PL, ICR), sem chamar suporte.
- [ ] Gerente Financeiro abre e vê: funil de cotações, contratos a vencer em 30/60/90/180 dias, inadimplência, headroom.
- [ ] Gerente de Tesouraria abre e vê: posição de caixa por banco/moeda/conta na data, fluxo D+1 a D+90, efetividade de hedge por moeda.
- [ ] Endpoints novos respondem dentro dos SLAs definidos (§1.2).
- [ ] Swagger atualizado com envelope `{ data, meta }` em endpoints novos.
- [ ] ADR-019 publicado e referenciado.
- [ ] Documentação Swagger inclui exemplos para cada endpoint novo.

---

## 10. Open Questions

Refinamentos não bloqueantes, a resolver durante a Fase 0:

1. **Edição retroativa de `SaldoCaixa`:** D-30, D-90 ou sem limite? — refinar com Tesouraria antes da Task 3.2.
2. **Transição `EmCaptacao → EmAnaliseBanco`:** manual (banco confirma) ou timeout automático (24 h)? — refinar com Gerente Financeiro antes da Task 0.5.

Decisões já tomadas (sponsor 2026-05-20) estão registradas em `tasks/plan_cockpit_backend_gaps.md` §5.

---

## 11. Referências

- `sgcf-backend/CLAUDE.md` — regras inegociáveis (Money, IClock, layer dependency).
- `sgcf-backend/docs/specs/cotacoes/SPEC.md` — padrão de SPEC seguido por estes documentos.
- `sgcf-backend/docs/specs/simulacoes/` — outro exemplo de SPEC modular.
- `SPEC.md` (raiz) — documento âncora do SGCF MVP (precedência geral).
- `ADR_Decisoes_Estrategicas.md` — decisões anteriores.
