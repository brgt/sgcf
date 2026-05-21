# Plano de Implementação — Backend para o Cockpit Multi-Persona

**Versão:** 1.1 — incorpora decisões do sponsor de 2026-05-20.
**Data:** 2026-05-20
**Documentos de referência:**

- `nordware-landing/docs/2.ui_design/UI_TESOURARIA/10_COCKPIT_UX_SPEC_MULTI_PERSONA.md` — especificação UX.
- `nordware-landing/docs/2.ui_design/UI_TESOURARIA/11_BACKEND_API_GAPS_COCKPIT.md` — gaps propostos pelo time FE.
- `nordware-landing/docs/2.ui_design/UI_TESOURARIA/12_BACKEND_API_COCKPIT_FE_GUIDE.md` — guia para o que o FE pode resolver sem novo backend (a ser criado por esta entrega).

**Escopo:** classificar os 26 gaps propostos como Real Gap (backend novo), Parcial (existe parte) ou Não-Gap (o FE já consegue com o que existe) e planejar a construção apenas dos Real Gaps em ordem priorizada.

---

## 1. Resumo Executivo

### 1.1 Classificação dos 26 gaps

Após inspeção do código atual (`src/Sgcf.Api`, `src/Sgcf.Application`, `src/Sgcf.Domain`) e das tipagens do FE (`nordware-landing/src/modules/finance/types/sgcf.types.ts`):

| ID | Classificação | Justificativa curta |
|----|---------------|---------------------|
| GAP-CKP-01 | **Real Gap** | `PainelDividaDto.BreakdownPorMoeda` existe; agregação por `ModalidadeContrato` não. |
| GAP-CKP-02 | **Real Gap (com reaproveitamento)** | Existem `AlertaVencimento` e `AlertaExposicaoBanco` no domínio, porém os DTOs do painel retornam `alertas: string[]`. Falta modelo unificado e endpoints REST. |
| GAP-CKP-03 | **Real Gap** | `GET /painel/vencimentos?ano=YYYY` exige ano único; não há horizonte 24/36/60m com granularidade configurável. |
| GAP-CKP-04 | **Real Gap** | Apenas Dívida/EBITDA disponível (`POST /painel/ebitda`); faltam cadastros de Patrimônio Líquido e Despesa Financeira. |
| GAP-CKP-05 | **Não-Gap (Guia FE) + Real Gap menor** | `GET /cotacoes?status=` + `StatusCotacao` permite agregação client-side. Decidido com sponsor (2026-05-20): expandir o enum `StatusCotacao` para incluir `EmAnaliseBanco` e `PropostaRecebida` — vira pré-requisito (Task 0.5). |
| GAP-CKP-06 | **Não-Gap (Guia FE)** | `GET /contratos?status=Ativo&vencDe=&vencAte=` resolve. FE faz 4 chamadas paralelas por janela. |
| GAP-CKP-07 | **Real Gap (agregação leve)** | Status `Vencido/Inadimplente` em `StatusContrato` e parcelas com `StatusParcela.Vencida` existem, mas "dias de atraso médio" exige varredura agregada — endpoint dedicado evita N+1 no FE. |
| GAP-CKP-08 | **Real Gap (maior)** | Não existe entidade `ContaBancaria` nem saldo de caixa. `SaldoPorBanco` atual é saldo de **dívida**, não de caixa. Requer novo domínio + estratégia de ingestão (OFX/CNAB/manual). |
| GAP-CKP-09 | **Real Gap** | Calendário hoje é mensal (`MesVencimentoDto`); falta granularidade diária + eventos extra-cronograma (recebíveis, aportes). |
| GAP-CKP-10 | **Real Gap (agregação)** | `GET /hedges/{id}/mtm` retorna MtM individual; `PainelDividaDto.AjusteMtm` é consolidado total. Falta visão por moeda exposição × cobertura × MtM. |
| GAP-CKP-11 | **Real Gap** | Sem séries históricas de MtM por instrumento. |
| GAP-CKP-12 | **Real Gap (consolidação)** | Cálculos hoje vivem no FE; centralizar evita drift. |
| GAP-CKP-13 | **Real Gap** | Sem domínio Covenants. |
| GAP-CKP-14 | **Parcial → Não-Gap CDI / Real Gap SOFR-Selic** | `GET /cotacoes/economia?de=&ate=` já entrega economia equalizada por CDI. Para SOFR/Selic é gap real. |
| GAP-CKP-15 | **Real Gap** | Sem entidade Orçamento. |
| GAP-CKP-16 | **Real Gap** | Sem entidade Documento Contratual nem máquina de estados. |
| GAP-CKP-17 | **Real Gap** | Sem domínio Conformidade Regulatória. |
| GAP-CKP-18 | **Real Gap (agregação)** | IOF/Tarifas vivem dentro de `FgiDetail`, `Lei4131Detail`, parcelas etc. Sem endpoint de agregação por banco/modalidade. |
| GAP-CKP-19 | **Parcial → Real Gap (campo novo)** | `GET /cotacoes/{id}/comparativo` agrega proposta aceita × contrato. **Confirmado por inspeção:** `Proposta` tem `TaxaAaPercentual` e `SpreadAaPercentual`, mas **não** tem `TaxaIndicativa` (taxa inicial de mercado). Para spread completo é necessário adicionar `TaxaIndicativaAa` ao agregado `Cotacao` ou `Proposta`. MVP do cockpit pode mostrar spread proposta-aceita × contrato-final usando o que existe. |
| GAP-CKP-20 | **Não-Gap (Guia FE)** | `LimiteBancoDto` já expõe `ValorLimiteBrl`, `ValorUtilizadoBrl`, `ValorDisponivelBrl`. FE faz a soma. |
| GAP-CKP-21 | **Real Gap (P2)** | Sem agregação de benefício tributário. |
| GAP-CKP-22 | **Real Gap (P2)** | `AuditLog` tem transições mas não há agregação por analista/SLA. |
| GAP-CKP-23 | **Real Gap (P2)** | Sem WebSocket/SSE. |
| GAP-CKP-24 | **Real Gap (com fallback FE)** | Preferências de usuário ainda não persistem server-side. *Para o MVP do cockpit, FE pode usar `localStorage`*; sincronização via API entra como P1. |
| GAP-CKP-25 | **Real Gap (P2)** | Sem job assíncrono de exportação. FE pode usar export HTML→PDF no MVP. |
| GAP-CKP-26 | **Real Gap (transversal)** | Endpoints atuais não retornam envelope `{ data, meta }`. Bloqueia indicação de qualidade/parcialidade pelo cockpit. |

### 1.2 Totais

- **Real Gaps a construir:** 19 (GAP-01, 02, 03, 04, 07, 08, 09, 10, 11, 12, 13, 15, 16, 17, 18, 22, 23, 24, 25, 26 + parte SOFR/Selic do 14, parte agregação do 21).
- **Não-Gaps (resolvidos via guia FE):** 5 (GAP-05, 06, 14-CDI, 19, 20).
- **Parciais (entrega FE primeiro + backend depois):** GAP-14, GAP-24.

---

## 2. Decisões Arquiteturais

### 2.1 Envelope padrão `{ data, meta }` (GAP-CKP-26)

**Decisão:** todo endpoint **novo** de painel ou agregação retorna:

```jsonc
{
  "data": { /* payload de domínio */ },
  "meta": {
    "dataHoraCalculo": "2026-05-19T14:32:00Z",
    "fontesConsultadas": [
      { "fonte": "contratos", "status": "OK", "registros": 142 }
    ],
    "completude": "COMPLETO" | "PARCIAL" | "DEGRADADO"
  }
}
```

Endpoints **existentes** não recebem migração no MVP do cockpit (custo de regressão alto). Em vez disso, são marcados como *legacy envelope* e migrados ao longo da Fase 4. Documentar em ADR.

**Custo:** classe `EnvelopeResponse<T>` em `Sgcf.Application.Common` + filtro `[ProducesEnvelope]`.

### 2.2 Sistema unificado de alertas (GAP-CKP-02)

**Decisão:** introduzir agregado `Alerta` em `Sgcf.Domain.Alertas`, substituindo gradualmente `AlertaVencimento` e `AlertaExposicaoBanco`:

```csharp
public sealed class Alerta : Entity
{
    public CategoriaAlerta Categoria { get; }      // COVENANT, VENCIMENTO, HEDGE, LIQUIDEZ, DOCUMENTO, LIMITE, REGULATORIO, OPERACIONAL
    public SeveridadeAlerta Severidade { get; }    // CRITICO, ATENCAO, INFORMATIVO
    public string Titulo { get; }
    public string Descricao { get; }
    public OrigemAlerta Origem { get; }            // {Tipo, Id}
    public AcaoRecomendada? Acao { get; }          // {Rotulo, Rota}
    public IReadOnlyList<PerfilCockpit> PerfisVisiveis { get; }
    public StatusAlerta Status { get; }            // ABERTO, LIDO, DISPENSADO
    public Instant CriadoEm { get; }
    public Instant? ExpiraEm { get; }
    public string ChaveIdempotencia { get; }       // origem.id + categoria + dia
}
```

`AlertaVencimento` e `AlertaExposicaoBanco` ficam como **value objects internos** consumidos pelas regras que geram instâncias de `Alerta`. Campo `IReadOnlyList<string> Alertas` em `PainelDividaDto`/`PainelGarantiasDto` é depreciado, mantido por 2 sprints com flag `?legacyAlerts=true`.

**Rules engine:** in-house, baseado em hosted services no projeto `Sgcf.Jobs`. Não justificamos Drools/Camunda para a quantidade prevista (< 50 regras).

### 2.3 Conta Bancária e Posição de Caixa (GAP-CKP-08)

**Decisão MVP (confirmada 2026-05-20):** novo domínio `Sgcf.Domain.Tesouraria` com entidades `ContaBancaria` e `SaldoCaixa`, **input manual com edição por data de referência**. Cada `SaldoCaixa` carrega `(contaId, dataReferencia, valor, moeda, registradoPor, registradoEm)` — operação é upsert idempotente por `(contaId, dataReferencia)` permitindo correção retroativa. Integração OFX/CNAB fica como Fase 2 (ADR separada).

### 2.4 Preferências de usuário (GAP-CKP-24)

**Decisão MVP:** FE usa `localStorage` para perfil ativo, filtros persistidos e cards customizados. Backend de preferências entra como item P1 depois do cockpit funcionar end-to-end. Risco: usuário perde estado ao trocar de dispositivo.

---

## 3. Plano por Fases

### Fase 0 — Transversais (Sprint 1)

Pré-requisitos para todo o resto.

#### Task 0.1 — ADR + envelope `EnvelopeResponse<T>`

**Descrição:** Criar ADR-019 (Envelope Padrão `{data, meta}`). Implementar `EnvelopeResponse<T>` em `Sgcf.Application.Common` e filtro `EnvelopeResultFilter` em `Sgcf.Api`. Aplicar apenas a endpoints novos desta entrega.

**Acceptance criteria:**

- [ ] ADR-019 escrito e revisado.
- [ ] Classe `EnvelopeResponse<T>` aceita `data: T`, `meta: EnvelopeMeta`.
- [ ] `EnvelopeMeta` carrega `dataHoraCalculo`, `fontesConsultadas[]`, `completude`.
- [ ] Filtro ASP.NET Core envolve respostas automaticamente quando o endpoint declara `[ProducesEnvelope]`.

**Verification:**

- [ ] Tests passam: `dotnet test --filter "FullyQualifiedName~Envelope"`.
- [ ] Snapshot JSON de endpoint piloto bate com schema.

**Dependências:** Nenhuma.

**Files likely touched:**

- `src/Sgcf.Application/Common/EnvelopeResponse.cs`
- `src/Sgcf.Api/Filters/EnvelopeResultFilter.cs`
- `docs/adr/ADR-019-envelope-data-meta.md`
- `tests/Sgcf.Api.IntegrationTests/EnvelopeResponseTests.cs`

**Escopo:** S.

---

#### Task 0.2 — Domínio `Alerta` unificado

**Descrição:** Criar agregado `Alerta` em `Sgcf.Domain.Alertas` com enums `CategoriaAlerta`, `SeveridadeAlerta`, `StatusAlerta`, `PerfilCockpit`. Repositório `IAlertaRepository` + migration EF Core.

**Acceptance criteria:**

- [ ] Agregado `Alerta` modelado conforme §2.2.
- [ ] Enums com mesmos valores listados no doc 11 §3 (categorias e severidades).
- [ ] Migration EF Core cria tabela `alertas` com índice em `(perfil_visivel, status, severidade)`.
- [ ] `ChaveIdempotencia` única; tentativa de inserir duplicada é silenciosamente ignorada.

**Verification:**

- [ ] Unit tests do agregado passam (FluentAssertions).
- [ ] Migration aplica e reverte sem dados perdidos em ambiente de teste (Testcontainers).

**Dependências:** Task 0.1 (envelope).

**Files likely touched:**

- `src/Sgcf.Domain/Alertas/Alerta.cs`
- `src/Sgcf.Domain/Alertas/CategoriaAlerta.cs`
- `src/Sgcf.Domain/Alertas/SeveridadeAlerta.cs`
- `src/Sgcf.Domain/Alertas/StatusAlerta.cs`
- `src/Sgcf.Domain/Alertas/PerfilCockpit.cs`
- `src/Sgcf.Application/Alertas/IAlertaRepository.cs`
- `src/Sgcf.Infrastructure/Persistence/Configurations/AlertaConfiguration.cs`
- `src/Sgcf.Infrastructure/Migrations/<timestamp>_AddAlertasUnificados.cs`

**Escopo:** M.

---

#### Task 0.3 — Endpoints REST de Alertas

**Descrição:** Controller `AlertasController` com `GET /alertas`, `GET /alertas/contadores`, `POST /alertas/{id}/dispensar`, `POST /alertas/{id}/marcar-como-lido`. Filtros por `severidade`, `categoria`, `status`, `perfil`. Paginação `PagedResult<T>` já existente.

**Acceptance criteria:**

- [ ] `GET /alertas` retorna `EnvelopeResponse<PagedResult<AlertaDto>>`.
- [ ] `GET /alertas/contadores` retorna `{critico, atencao, informativo}` P95 < 200 ms.
- [ ] `POST /alertas/{id}/dispensar` muda `StatusAlerta` para `DISPENSADO`; idempotente.
- [ ] Autorização: claim `perfil` filtra automaticamente o array `PerfisVisiveis`.

**Verification:**

- [ ] Testes de integração HTTP cobrindo cada verbo.
- [ ] Teste de autorização: usuário Tesouraria não vê alerta com `PerfisVisiveis = [CFO]`.

**Dependências:** 0.1, 0.2.

**Files likely touched:**

- `src/Sgcf.Api/Controllers/AlertasController.cs`
- `src/Sgcf.Application/Alertas/Queries/ListAlertasQuery.cs`
- `src/Sgcf.Application/Alertas/Queries/GetContadoresAlertasQuery.cs`
- `src/Sgcf.Application/Alertas/Commands/DispensarAlertaCommand.cs`
- `src/Sgcf.Application/Alertas/Commands/MarcarAlertaComoLidoCommand.cs`
- `tests/Sgcf.Api.IntegrationTests/AlertasControllerTests.cs`

**Escopo:** M.

---

#### Task 0.4 — Rules engine inicial (3 regras MVP)

**Descrição:** Hosted service em `Sgcf.Jobs` rodando a cada 5 min (liquidez) e diário 06:00 BRT (vencimentos/covenants). Implementa **apenas** três regras no MVP:

1. `RegraVencimentoIminente` — D-7, D-3, D-0 (substitui geração atual de `AlertaVencimento`).
2. `RegraContratoSemHedge` — copia regra atual de `GerarAlertasSemHedge` em `GetPainelDividaQueryHandler`.
3. `RegraLimiteBancoUtilizacao` — disparo quando `ValorUtilizadoBrl/ValorLimiteBrl > 0.85`.

**Acceptance criteria:**

- [ ] Hosted service registrado e parametrizado por cron expressão.
- [ ] Idempotência por `ChaveIdempotencia` evita duplicar alertas no mesmo dia.
- [ ] Migration de dados converte alertas legados (`alerta_vencimento`, `alerta_exposicao_banco`) para a nova tabela.

**Verification:**

- [ ] Teste rodando o job em modo "now" cria os alertas esperados a partir de fixture conhecida.
- [ ] Rodar 2x em sequência não cria duplicatas.

**Dependências:** 0.2, 0.3.

**Files likely touched:**

- `src/Sgcf.Jobs/Alertas/AlertasHostedService.cs`
- `src/Sgcf.Jobs/Alertas/Regras/RegraVencimentoIminente.cs`
- `src/Sgcf.Jobs/Alertas/Regras/RegraContratoSemHedge.cs`
- `src/Sgcf.Jobs/Alertas/Regras/RegraLimiteBancoUtilizacao.cs`
- `src/Sgcf.Infrastructure/Migrations/<ts>_MigrarAlertasLegados.cs`

**Escopo:** M.

---

#### Task 0.5 — Expandir `StatusCotacao` com estágios intermediários

**Descrição:** Decidido com sponsor (2026-05-20): expandir o enum `StatusCotacao` para refletir o pipeline completo solicitado pelo cockpit do Gerente Financeiro. Inclui ajuste de máquina de estados em `Cotacao` (`RegistrarProposta`, `EncerrarCaptacao`, `Aceitar`, `Recusar`), migração de dados e atualização dos handlers que filtram por status.

**Novo enum proposto:**

```csharp
public enum StatusCotacao : byte
{
    Rascunho         = 1,
    EmCaptacao       = 2,  // enviada aos bancos, aguardando proposta
    EmAnaliseBanco   = 7,  // (novo) banco confirmou recebimento e está analisando
    PropostaRecebida = 8,  // (novo) ao menos uma proposta registrada
    Comparada        = 3,  // todas as propostas recebidas, em análise interna
    Aceita           = 4,
    Convertida       = 5,
    Recusada         = 6,
}
```

Os novos valores recebem **bytes não contíguos (7, 8)** para preservar a ordem dos valores existentes na coluna PostgreSQL `byte`/`smallint`, conforme regra do `StatusCotacao.cs` (não reordenar).

**Acceptance criteria:**

- [ ] Enum atualizado com novos membros + ordem visual documentada em XMLDoc.
- [ ] Máquina de estados em `Cotacao` aceita transições novas:
  - `EmCaptacao → EmAnaliseBanco` (evento informado pelo banco ou job de timeout 24h após envio).
  - `EmAnaliseBanco → PropostaRecebida` (ao registrar primeira `Proposta`).
  - `PropostaRecebida → Comparada` (ao encerrar captação).
- [ ] Migration EF Core preserva dados existentes (cotações em `EmCaptacao` ficam onde estão).
- [ ] `ListCotacoesQuery` aceita os novos valores.
- [ ] Documentação `docs/specs/cotacoes/SPEC.md §4` atualizada com diagrama de transições.

**Verification:**

- [ ] Testes da máquina de estados cobrem as novas transições e rejeitam transições inválidas (ex.: `Rascunho → PropostaRecebida`).
- [ ] Endpoint `GET /cotacoes?status=PropostaRecebida` retorna 200 com lista vazia em ambiente novo.

**Dependências:** Nenhuma.

**Files likely touched:**

- `src/Sgcf.Domain/Cotacoes/StatusCotacao.cs`
- `src/Sgcf.Domain/Cotacoes/Cotacao.cs` (transições)
- `src/Sgcf.Application/Cotacoes/Queries/ListCotacoesQuery.cs`
- `src/Sgcf.Infrastructure/Migrations/<ts>_ExpandirStatusCotacao.cs`
- `docs/specs/cotacoes/SPEC.md`
- `tests/Sgcf.Domain.Tests/Cotacoes/CotacaoMachineStateTests.cs`

**Escopo:** S (sem mudança no schema; apenas valores novos do enum + métodos no agregado).

---

### Checkpoint A — Após Fase 0

- [ ] `dotnet test` passa.
- [ ] Endpoints `/alertas/*` retornam o que o cockpit precisa.
- [ ] Envelope `{data, meta}` aprovado pelo time FE em sessão de 30 min.
- [ ] Novos estágios de cotação validados com Gerente Financeiro.
- [ ] Revisão humana antes de continuar.

---

### Fase 1 — Cockpit CFO (Sprints 2 a 3)

#### Task 1.1 — GAP-CKP-01 — Breakdown da dívida por modalidade

**Descrição:** `GET /api/v1/painel/divida/breakdown-modalidade` agregando contratos `Ativo` por `ModalidadeContrato`, convertendo para BRL com a mesma estratégia spot → PTAX D-1 já usada em `GetPainelDividaQueryHandler`.

**Acceptance criteria:**

- [ ] DTO `BreakdownModalidadeDto` conforme doc 11 §3.GAP-01.
- [ ] Filtragem default por `StatusContrato = Ativo`.
- [ ] Ordenação default por `valorBrl` decrescente.
- [ ] Resposta envelopada (Task 0.1).

**Verification:**

- [ ] Teste integração cobrindo 6 modalidades.
- [ ] Soma dos `valorBrl` por modalidade == `PainelDividaDto.DividaBrutaBrl` (consistência).

**Files likely touched:**

- `src/Sgcf.Application/Painel/Queries/GetBreakdownModalidadeQuery.cs`
- `src/Sgcf.Application/Painel/Queries/GetBreakdownModalidadeQueryHandler.cs`
- `src/Sgcf.Application/Painel/BreakdownModalidadeDto.cs`
- `src/Sgcf.Api/Controllers/PainelController.cs` (endpoint novo)
- `tests/Sgcf.Application.Tests/Painel/BreakdownModalidadeTests.cs`

**Escopo:** S.

---

#### Task 1.2 — GAP-CKP-03 — Curva de vencimentos multi-ano

**Descrição:** `GET /api/v1/painel/vencimentos/horizonte?meses=&granularidade=` com `meses ∈ {12,24,36,60}`, `granularidade ∈ {mes, trimestre, ano}`. Reaproveita `EventoCronograma` lido por `GetCalendarioVencimentosQueryHandler`.

**Acceptance criteria:**

- [ ] Buckets etiquetados como `YYYY-MM`, `YYYY-Qx` ou `YYYY` conforme granularidade.
- [ ] Breakdown por modalidade por bucket.
- [ ] Filtros opcionais `bancoId`, `modalidade`, `moeda`.
- [ ] Conversão BRL com spot/PTAX (mesmo padrão).

**Verification:**

- [ ] Teste integração para cada combinação (meses × granularidade).
- [ ] Soma `totalBrl` por bucket == soma flat por parcela.

**Files likely touched:**

- `src/Sgcf.Application/Painel/Queries/GetCurvaVencimentosQuery.cs`
- `src/Sgcf.Application/Painel/Queries/GetCurvaVencimentosQueryHandler.cs`
- `src/Sgcf.Application/Painel/CurvaVencimentosDto.cs`
- `src/Sgcf.Api/Controllers/PainelController.cs`
- `tests/Sgcf.Application.Tests/Painel/CurvaVencimentosTests.cs`

**Escopo:** S.

---

#### Task 1.3 — GAP-CKP-04 — Estrutura de capital

**Descrição:** Cadastro de Patrimônio Líquido e Despesa Financeira (mensal, semelhante ao `EbitdaMensal` existente). Endpoint `GET /api/v1/painel/estrutura-capital` calcula Dívida/PL e ICR.

**Acceptance criteria:**

- [ ] Entidade `DadosContabeisMensal` (PL, Despesa Financeira 12m, EBITDA 12m).
- [ ] `POST /painel/dados-contabeis` faz upsert.
- [ ] `GET /painel/estrutura-capital` retorna `{dividaTotalBrl, patrimonioLiquidoBrl, dividaSobrePatrimonio, ebitdaUltimos12mBrl, despesaFinanceira12mBrl, icr, alertas[]}`.
- [ ] ICR = EBITDA / DespesaFinanceira (NodaTime para janelas 12m).

**Verification:**

- [ ] Teste com fixture conhecida bate número esperado.
- [ ] Sem dados contábeis o endpoint retorna `completude: PARCIAL` com `alertas: ["DADOS_CONTABEIS_AUSENTES"]`.

**Dependências:** 0.1 (envelope), 0.2/0.3 (alertas).

**Files likely touched:**

- `src/Sgcf.Domain/Contabilidade/DadosContabeisMensal.cs`
- `src/Sgcf.Application/Contabilidade/Commands/UpsertDadosContabeisCommand.cs`
- `src/Sgcf.Application/Painel/Queries/GetEstruturaCapitalQuery.cs`
- `src/Sgcf.Api/Controllers/PainelController.cs`
- `src/Sgcf.Infrastructure/Migrations/<ts>_AddDadosContabeisMensal.cs`
- `tests/Sgcf.Application.Tests/Painel/EstruturaCapitalTests.cs`

**Escopo:** M.

---

### Checkpoint B — Cockpit CFO

- [ ] Tasks 1.1 a 1.3 passam em CI.
- [ ] Demo com sponsor (PO + CFO) usando FE consumindo os três endpoints + alertas estruturados.
- [ ] Decisão go/no-go para Fase 2.

---

### Fase 2 — Cockpit Gerente Financeiro (Sprint 4)

#### Task 2.1 — GAP-CKP-07 — Visão de inadimplência agregada

**Descrição:** `GET /api/v1/painel/inadimplencia` calculando dias de atraso médio e buckets `(1-15, 16-30, 31-60, 60+)`.

**Acceptance criteria:**

- [ ] Consulta parcelas com `StatusParcela.Vencida` + contratos com `StatusContrato in (Vencido, Inadimplente)`.
- [ ] `diasAtrasoMedio` ponderado por valor em mora.
- [ ] Bucketing conforme doc 11.
- [ ] Conversão para BRL (mesmo padrão).

**Verification:**

- [ ] Teste com fixture de 5 contratos inadimplentes em buckets distintos.

**Files likely touched:**

- `src/Sgcf.Application/Painel/Queries/GetInadimplenciaQuery.cs`
- `src/Sgcf.Application/Painel/InadimplenciaDto.cs`
- `src/Sgcf.Api/Controllers/PainelController.cs`
- `tests/Sgcf.Application.Tests/Painel/InadimplenciaTests.cs`

**Escopo:** S.

---

#### Task 2.2 — Guia FE para Não-Gaps (GAP-05, GAP-06, GAP-19, GAP-20)

**Descrição:** Escrever `12_BACKEND_API_COCKPIT_FE_GUIDE.md` com receitas concretas para os indicadores que **não precisam de novo endpoint**: funil de cotações, contratos a vencer por janela, spread de negociação, headroom de crédito.

**Acceptance criteria:**

- [ ] Documento contém uma seção por indicador com: endpoint(s) usados, query params, transformação client-side, exemplo de payload e wireframe ASCII.
- [ ] Ressalva sobre divergência de estágios em GAP-05 (`Rascunho/EmCaptacao/Comparada/Aceita/Convertida/Recusada` vs proposta do doc 11).
- [ ] Revisado por um dev FE.

**Verification:** Checklist revisado em PR.

**Files likely touched:**

- `nordware-landing/docs/2.ui_design/UI_TESOURARIA/12_BACKEND_API_COCKPIT_FE_GUIDE.md`

**Escopo:** S (sem código).

---

### Fase 3 — Cockpit Tesouraria intraday (Sprints 5 a 7)

#### Task 3.1 — Domínio `ContaBancaria` + CRUD

**Descrição:** Novo agregado `ContaBancaria` em `Sgcf.Domain.Tesouraria` (campos: `bancoId`, `agencia`, `numero`, `tipo`, `moeda`, `apelido`). CRUD via `ContasBancariasController`. Sem integração OFX no MVP — input manual.

**Acceptance criteria:**

- [ ] CRUD completo: `GET /contas-bancarias`, `POST`, `PUT /{id}`, `DELETE /{id}`.
- [ ] Constraint única por `(bancoId, agencia, numero)`.
- [ ] Soft delete.

**Verification:**

- [ ] Testes de integração HTTP cobrindo verbos e validações.

**Dependências:** Nenhuma (mas bloqueia 3.2/3.3).

**Files likely touched:**

- `src/Sgcf.Domain/Tesouraria/ContaBancaria.cs`
- `src/Sgcf.Domain/Tesouraria/TipoContaBancaria.cs`
- `src/Sgcf.Application/Tesouraria/Commands/CreateContaBancariaCommand.cs` etc.
- `src/Sgcf.Api/Controllers/ContasBancariasController.cs`
- `src/Sgcf.Infrastructure/Migrations/<ts>_AddContasBancarias.cs`

**Escopo:** M.

---

#### Task 3.2 — GAP-CKP-08 — Posição de caixa consolidada

**Descrição:** Entidade `SaldoCaixa` (snapshot diário por conta) com **input manual editável por data** (decisão sponsor 2026-05-20). Endpoint `POST /tesouraria/saldos` faz upsert idempotente por `(contaId, dataReferencia)` permitindo correção retroativa. Endpoint de leitura `GET /api/v1/tesouraria/posicao-caixa` agrega por banco/moeda/conta com conversão BRL. Endpoint adicional `GET /tesouraria/saldos?contaId=&dataDe=&dataAte=` retorna a série histórica para o FE permitir edição de saldos passados.

**Acceptance criteria:**

- [ ] `SaldoCaixa` chave única por `(contaId, dataReferencia)`.
- [ ] `POST /tesouraria/saldos` aceita batch, usa `Idempotency-Key` e faz upsert: se já existe um saldo na mesma `(contaId, dataReferencia)`, sobrescreve `valor`, `moeda`, `registradoPor`, `registradoEm`.
- [ ] `GET /tesouraria/saldos?contaId=&dataDe=&dataAte=` lista a série histórica ordenada por data.
- [ ] `GET /posicao-caixa` aceita `dataReferencia=YYYY-MM-DD` opcional (default = hoje BRT) e retorna `saldoConsolidadoBrl`, `porMoeda[]`, `porBanco[].contas[]` na data informada.
- [ ] Edição de saldo registra entrada em `AuditLog` (`Entity: "SaldoCaixa"`, mudança de valor com `valorAntes`/`valorDepois`).
- [ ] `meta.completude = PARCIAL` se a data solicitada não tem saldo registrado para alguma conta ativa.

**Verification:**

- [ ] Teste com 3 bancos, 5 contas, 2 moedas — totais batem.

**Dependências:** Task 3.1.

**Escopo:** L.

---

#### Task 3.3 — GAP-CKP-09 — Fluxo de caixa projetado diário

**Descrição:** `GET /api/v1/tesouraria/fluxo-caixa?dataDe=&dataAte=&granularidade=dia` (limite 90 dias). Lê `EventoCronograma` + `EventoFluxoCaixa` (novo) para entradas/saídas extra-cronograma.

**Acceptance criteria:**

- [ ] Para cada dia: `entradasBrl`, `saidasBrl`, `saldoProjetadoBrl`, `eventos[]`.
- [ ] Sinaliza dias com saldo projetado negativo (`alertas[]`).
- [ ] Endpoint `POST /tesouraria/eventos-fluxo` para inserir eventos manuais.

**Verification:**

- [ ] Fixture: saldo inicial 10mi, amortização de 8mi em D+3, recebível 5mi em D+5 → saldo D+5 = 7mi.

**Dependências:** 3.1, 3.2.

**Escopo:** M.

---

#### Task 3.4 — GAP-CKP-10 — Efetividade de hedge

**Descrição:** `GET /api/v1/tesouraria/hedge-efetividade` calculando exposição (soma de contratos por moeda) × cobertura (soma de notional de hedges ativos por moeda) + MtM agregado + VaR 1d.

**Acceptance criteria:**

- [ ] Para cada moeda estrangeira: `exposicaoLiquidaOriginal/Brl`, `hedgeContratadoOriginal/Brl`, `coberturaPct`, `gapOriginal/Brl`, `mtmAtualBrl`, `var1diaBrl`.
- [ ] `coberturaConsolidadaPct` na raiz.

**Verification:**

- [ ] Reproduz manualmente cálculos do FE atual `FgiTarifaSensitivityControls`.

**Dependências:** 0.1.

**Files likely touched:**

- `src/Sgcf.Application/Tesouraria/Queries/GetHedgeEfetividadeQuery.cs`
- `src/Sgcf.Application/Tesouraria/HedgeEfetividadeDto.cs`
- `src/Sgcf.Api/Controllers/TesourariaController.cs`

**Escopo:** M.

---

### Checkpoint C — Cockpit Tesouraria

- [ ] Tasks 3.1–3.4 em CI.
- [ ] Sessão de demo com gerente de tesouraria operando o cockpit por 15 min.
- [ ] Decisão go/no-go para Fase 4.

---

### Fase 4 — P1 progressivo (Sprints 8+)

Cada item é uma task isolada; ordem pode ser repriorizada por negócio.

| Task | Gap | Esforço | Dependências |
|------|-----|---------|--------------|
| 4.1 | GAP-CKP-11 (hedge resultado histórico) | M | 3.4 |
| 4.2 | GAP-CKP-12 (sensibilidade indexadores) | M | 0.1 |
| 4.3 | GAP-CKP-13 (covenants — domínio novo) | L | 0.2, 0.3, 1.3 |
| 4.4 | GAP-CKP-14 SOFR/Selic (saving benchmark — parte não-CDI) | M | 0.1 |
| 4.5 | GAP-CKP-15 (orçamento — domínio novo) | M | 0.1 |
| 4.6 | GAP-CKP-16 (workflow documentação — domínio novo) | L | 0.2, 0.3 |
| 4.7 | GAP-CKP-17 (conformidade regulatória) | L | 0.2, 0.3 |
| 4.8 | GAP-CKP-18 (tarifas/IOF agregados) | M | 0.1 |
| 4.9 | GAP-CKP-24 (preferências usuário backend) | S | — |
| 4.10 | GAP-CKP-19 (campo `TaxaIndicativaAa` em `Cotacao`/`Proposta` + spread agregado) | S | — |

### Fase 5 — Backlog (P2)

| Task | Gap | Esforço |
|------|-----|---------|
| 5.1 | GAP-CKP-21 (economia tributária) | M |
| 5.2 | GAP-CKP-22 (produtividade equipe) | M |
| 5.3 | GAP-CKP-23 (websocket eventos tempo real) | L |
| 5.4 | GAP-CKP-25 (exportação assíncrona) | M |

---

## 4. Riscos e Mitigações

| Risco | Impacto | Mitigação |
|-------|---------|-----------|
| Migração `alertas: string[]` → modelo unificado quebra FE existente | Alto | Manter `alertas: string[]` por 2 sprints; flag `?legacyAlerts=true`; comunicar deprecação. |
| Decisão de ingestão OFX/CNAB para GAP-CKP-08 atrasa cockpit Tesouraria | Alto | MVP com input manual; OFX vira ADR separada na Fase 4. |
| Estágios de cotação no doc 11 (GAP-05) divergem do domínio | Médio | Item explícito no guia FE; decisão de produto registrada. |
| Sem `taxaIndicativa` na cotação, GAP-19 não funciona | Médio | Validar com produto se já há captura em `Proposta`; pode ser que o spread proposta×contrato seja suficiente. |
| Envelope `{data, meta}` em endpoints novos cria assimetria com legacy | Baixo | ADR-019 descreve estratégia de migração gradual. |
| Patrimônio Líquido / Despesa Financeira sem integração contábil → defasagem | Médio | MVP com upsert manual mensal; alerta `DADOS_CONTABEIS_AUSENTES` quando vazio. |
| Performance de `/painel/vencimentos/horizonte` com 60m | Médio | Cache 60s via ETag + paginação por bucket se payload > 200 KB. |

---

## 5. Decisões do sponsor — 2026-05-20

1. **Estágios de cotação (GAP-05):** decidido expandir o enum com `EmAnaliseBanco` e `PropostaRecebida` (Task 0.5).
2. **Posição de caixa (GAP-08):** decidido input manual com edição por data (refletido em Task 3.2).
3. **Preferências de usuário (GAP-24):** decidido `localStorage` no MVP; sincronização server-side entra como Task 4.9.
4. **Alertas legados (`alertas: string[]`):** decidido janela de deprecação de 2 sprints (Tasks 0.2 a 0.4).
5. **`TaxaIndicativaAa` em `Proposta` (GAP-19):** **confirmado por inspeção do código que o campo não existe**. Para o MVP do cockpit, o FE mostra "Spread proposta-aceita × contrato-final" usando `SpreadAaPercentual` existente. Adição do campo `TaxaIndicativaAa` entra como Task 4.10 (Fase 4, P1).

## 6. Perguntas remanescentes

Nenhuma bloqueante. Os itens abaixo entram em refinamento durante a Fase 0:

- Granularidade da edição retroativa de `SaldoCaixa` (D-30? D-90? sem limite?) — refinar com Tesouraria antes de Task 3.2.
- Critério para considerar um banco "em análise" automaticamente na nova máquina de estados (timeout de N dias após `EmCaptacao`?) — refinar com Gerente Financeiro antes de Task 0.5.

---

## 7. Critério de Pronto para o Cockpit MVP

Cockpit MVP é considerado entregue quando:

- [ ] Fases 0, 1, 2, 3 concluídas.
- [ ] Não-gaps documentados em `12_BACKEND_API_COCKPIT_FE_GUIDE.md` e implementados no FE.
- [ ] CFO consegue abrir, ver alertas críticos, breakdown de modalidade, curva de vencimentos e estrutura de capital sem chamar suporte.
- [ ] Gerente Financeiro consegue abrir e ver funil, contratos a vencer, inadimplência e headroom.
- [ ] Gerente de Tesouraria consegue abrir e ver posição de caixa, fluxo D+1 a D+90 e efetividade de hedge.
- [ ] `dotnet test` verde; ETag/cache configurados; auditoria ativa nos endpoints sensíveis.
- [ ] Documentação Swagger gerada com `EnvelopeResponse<T>` corretamente refletido.

---

## 8. Referências

- `nordware-landing/docs/2.ui_design/UI_TESOURARIA/10_COCKPIT_UX_SPEC_MULTI_PERSONA.md`
- `nordware-landing/docs/2.ui_design/UI_TESOURARIA/11_BACKEND_API_GAPS_COCKPIT.md`
- `nordware-landing/docs/2.ui_design/UI_TESOURARIA/12_BACKEND_API_COCKPIT_FE_GUIDE.md` (a ser criado por Task 2.2)
- `sgcf-backend/CLAUDE.md` — regras inegociáveis (`Money`, `IClock`, layer dependency).
- `sgcf-backend/src/Sgcf.Application/Painel/` — handlers atuais a reaproveitar.
- `sgcf-backend/src/Sgcf.Domain/Alertas/` — alertas legados a migrar.
- `sgcf-backend/src/Sgcf.Application/Cotacoes/Queries/GetEconomiaPeriodoQuery.cs` — base para GAP-14 (CDI).
