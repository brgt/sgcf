# Plano — Quadro da Dívida + Simulação de Contratação

**Status final:** ENTREGUE em 2026-05-19 — todas as 4 fases completas, incluindo tasks 4.1 (Comparativo), 4.2 (Golden Dataset) e 4.4 (MCP Tools) entregues em sessão complementar
**Autor:** Planning agent
**Data:** 2026-05-18
**Versão alvo:** v0.7.0 → entregue como **v0.10.0** (sequência ajustada após releases 0.7, 0.8 e 0.9)
**Referência visual:** `documentos/Endividamento.xlsx`, aba `Quadro_da_Divida`

---

## 1. Contexto e objetivo

A aba `Quadro_da_Divida` da planilha de endividamento é a visão central da tesouraria. Combina, para cada mês de um ano civil:

1. Saldo de abertura por banco
2. Pagamentos previstos por banco (amortizações de principal)
3. **Novas captações simuladas** por banco (operações ainda não contratadas)
4. Saldo de fechamento por banco
5. Share % de cada banco no total
6. Variação % do total mês a mês

O backend já cobre os blocos (1), (2) e (5) parcialmente, mas **não há nenhum mecanismo para representar uma captação futura hipotética**. O `Simulador` atual cobre apenas estresse cambial e ranking de antecipação. As `Cotações` representam propostas reais em negociação, não simulações de planejamento.

Este plano entrega:

- **Quadro da Dívida** consolidado num único endpoint, com breakdown por banco, projeção mensal e sumário.
- **Simulação de Contratação** como novo módulo: cenários nomeados (Otimista/Realista/Pessimista) contendo N captações hipotéticas com cronograma gerado on-the-fly pelo motor existente.
- **Integração no Quadro da Dívida**: ao informar `cenarioId`, a projeção mensal incorpora as captações simuladas.

### 1.1 Não-objetivos (out of scope deste plano)

- **Hedge na projeção** — o saldo do quadro é dívida bruta (principal a amortizar). MTM de NDF continua exposto via `/painel/divida`.
- **Antecipações simuladas dentro do cenário** — o `/simulador/antecipacao-portfolio` permanece independente.
- **Conversão de moeda variável ao longo do ano** — toda projeção usa spot/PTAX corrente flat. Para cenários cambiais, usar o simulador específico.
- **Múltiplos anos numa mesma chamada** — MVP retorna 12 meses do ano informado.
- **Persistência de cronograma da simulação** — calculado on-the-fly a cada consulta.
- **Cenário "ativo" global** — frontend sempre escolhe o `cenarioId` explicitamente.

---

## 2. Decisões arquiteturais

| # | Decisão | Rationale |
|---|---------|-----------|
| AD-1 | **Novo módulo `Sgcf.Domain.Simulacao`** com agregado raiz `CenarioSimulacao` e child `SimulacaoContratacao`. Separado de `Cotacoes` (propostas reais) e de `Painel/Simulador` (cenário cambial / antecipação portfólio). | Lifecycle e semântica distintos: simulação é hipotética e mutável; cotação é proposta em negociação ativa. Misturar polui invariantes. |
| AD-2 | **Cenário como agregado versionável** (Rascunho → Ativo → Arquivado). Frontend chama `cenarioId` explicitamente. | Permite comparar múltiplos cenários (Otimista/Realista/Pessimista) lado a lado. Sem cenário, vira coleção sem identidade. |
| AD-3 | **Cronograma da simulação on-the-fly + cache Redis com TTL curto** — não persistido em PostgreSQL. Calculado via `CronogramaStrategyFactory` e cacheado em Redis com TTL de 60s por chave `(cenarioId, simulacaoId, version)`. Invalidação na escrita via incremento de `version`. | Aprovação PO 2026-05-19. Simulação muda muito durante refinamento; persistir em DB gera drift. Cache Redis dá performance em consultas repetidas dentro do mesmo refresh do frontend sem comprometer consistência. |
| AD-4 | **`SimulacaoContratacao` reutiliza o input do motor de Cronograma** (`GerarCronogramaInput`). Os mesmos campos que um contrato real precisaria. | Garante que a simulação produza cronograma idêntico ao que o contrato real geraria após conversão. Zero divergência. |
| AD-5 | **Projeção mensal como função pura** — `ProjetorSaldoMensal` em `Sgcf.Domain.Painel` recebe saldo inicial + eventos e devolve `QuadroDividaProjecao`. Sem I/O, sem clock. | Permite property-based testing das invariantes (`SaldoFinal[m] == SaldoInicial[m+1]`, soma de shares = 100%, etc.). |
| AD-6 | **`EventoProjecao` é a unidade comum** entre amortizações reais (do cronograma) e captações (reais ou simuladas). Tipo: `AmortizacaoPrincipal` ou `Captacao`. Juros NÃO aparecem (despesa, não amortização). | Simplifica o projetor: trata tudo como `(BancoId, Data, Tipo, ValorBrl)`. |
| AD-7 | **Endpoint único `/api/v1/painel/quadro-divida?ano=YYYY[&cenarioId=...]`** retorna estrutura completa: snapshot + projeção mensal + sumário consolidado. | Frontend faz uma única chamada para renderizar a tabela inteira. |
| AD-8 | **Conversão de moedas via spot/PTAX corrente flat** para toda a projeção. Documentar como limitação no SPEC. | Simulação de cenário cambial é função separada (`/simulador/cenario-cambial`). Misturar gera complexidade O(moedas × meses) sem ganho de precisão. |
| AD-9 | **Quadro sem `cenarioId` retorna apenas dados reais** (contratos ativos + suas amortizações futuras). Permite frontend renderizar a tabela mesmo sem cenário criado. | Visão "base" do quadro é a realidade atual. Cenário é overlay opcional. |
| AD-10 | **Banco como dimensão primária** em todo breakdown. Adicionar `bancoId` + `bancoApelido` em campos onde hoje só há `contratoId` (parcelas do calendário). | Frontend já exige agrupamento por banco. Manter como pós-processamento no front é redundância. |
| AD-11 | **RBAC**: criar/editar cenário exige `tesouraria + admin`; arquivar exige `gerente + admin`; consultar é leitura geral. Quadro da Dívida é leitura geral. | Consistente com matriz RBAC do SPEC §11 (tesouraria opera; gerente aprova). |
| AD-12 | **Alerta (não bloqueio) ao ultrapassar `LimiteBanco`** na simulação. Frontend recebe array `alertas[]` e renderiza warning sem impedir salvar. | Simulação é "what-if" — pode ultrapassar limite intencionalmente para justificar pedido de aumento. |

---

## 3. Dependências e ordem

```
Fase 1 (Quadro da Dívida — real-only)
   │
   ├── 1.1 ProjetorSaldoMensal (pure function)
   ├── 1.2 GetSaldoPorBancoAtualQuery
   ├── 1.3 BancoId em VencimentoItemDto
   └── 1.4 GetQuadroDividaQuery + endpoint
        │
        ▼ Checkpoint 1: Frontend já pode renderizar com dados reais
        │
Fase 2 (Simulação de Contratação — domínio + persistência + API)
   │
   ├── 2.1 Domain CenarioSimulacao + SimulacaoContratacao
   ├── 2.2 Migration + EF config + repository
   ├── 2.3 Application CRUD (commands + queries)
   ├── 2.4 SimulacaoCronogramaCalculator
   ├── 2.5 API REST CRUD
   └── 2.6 Endpoint cronograma hipotético (preview)
        │
        ▼ Checkpoint 2: Frontend pode CRUD simulações
        │
Fase 3 (Integração no Quadro)
   │
   ├── 3.1 GetQuadroDividaQuery aceita cenarioId
   ├── 3.2 ProjetorSaldoMensal aceita eventos de Captacao
   └── 3.3 Endpoint conveniência /simulacoes/{id}/quadro-divida
        │
        ▼ Checkpoint 3: Quadro renderiza com simulação aplicada
        │
Fase 4 (Polimento)
   │
   ├── 4.1 Comparativo de cenários
   ├── 4.2 Golden dataset com números da planilha
   ├── 4.3 Documentação API + Bruno + SPEC + CHANGELOG
   └── 4.4 Tools MCP read-only
        │
        ▼ Checkpoint final: Release v0.7.0
```

---

## 4. Fase 1 — Quadro da Dívida (real-only)

### Task 1.1 — `ProjetorSaldoMensal` (pure function)

**Descrição:** Função pura em `Sgcf.Domain.Painel` que dada uma posição inicial por banco e uma lista de eventos (`AmortizacaoPrincipal` ou `Captacao`) com data e valor BRL, projeta saldo por banco mês a mês para o ano informado.

**Assinatura proposta:**

```csharp
public static class ProjetorSaldoMensal
{
    public static QuadroDividaProjecao Projetar(
        IReadOnlyDictionary<Guid, Money> saldoInicialPorBanco, // BancoId → saldo BRL
        IReadOnlyList<EventoProjecao> eventos,                 // Tipo + BancoId + Data + ValorBrl
        int ano);
}

public sealed record EventoProjecao(
    Guid BancoId,
    LocalDate Data,
    TipoEventoProjecao Tipo, // AmortizacaoPrincipal | Captacao
    Money ValorBrl);
```

Resultado contém `MesProjecao[12]` com `MesNumero`, `Dictionary<Guid, SaldoBancoMes>` (saldoInicio, totalAmortizacao, totalCaptacao, saldoFim, share %), `totalGeralSaldoFim`, `variacaoPctVsMesAnterior`.

**Critérios de aceite:**
- Função pura: sem I/O, sem `IClock`, sem state mutável
- Invariante: `mes[n].saldoFim == mes[n+1].saldoInicio` por banco
- Invariante: `mes[n].totalGeralSaldoFim == sum(mes[n].saldoFim por banco)`
- Invariante: `sum(share %) == 100` por mês com tolerância de 0,01 pp
- `mes[1].saldoInicio == saldoInicialPorBanco[banco]` por banco
- Bancos sem saldo inicial mas com captação no ano são incluídos a partir do mês da primeira captação
- Eventos fora do ano informado são ignorados (mas não erro)
- Arredondamento HalfUp em 2 decimais na borda do DTO; 6 decimais em cálculos intermediários

**Verificação:**
- 8+ unit tests cobrindo: caso vazio, único banco, múltiplos bancos, captação no mês 6, amortização zerando saldo, eventos fora do ano
- 2 property-based tests com FsCheck: invariante saldoFim/saldoInicio e soma de shares
- Golden test seed: reproduzir a coluna janeiro da `Quadro_da_Divida`

**Dependências:** Nenhuma

**Files likely touched:**
- `src/Sgcf.Domain/Painel/ProjetorSaldoMensal.cs` (novo)
- `src/Sgcf.Domain/Painel/EventoProjecao.cs` (novo)
- `src/Sgcf.Domain/Painel/QuadroDividaProjecao.cs` (novo)
- `tests/Sgcf.Domain.Tests/Painel/ProjetorSaldoMensalTests.cs` (novo)

**Escopo:** M

---

### Task 1.2 — `GetSaldoPorBancoAtualQuery`

**Descrição:** Nova query handler que retorna o saldo atual da carteira agrupado por banco em BRL. Reusa a lógica de conversão de moeda (spot intraday → PTAX D-1) do `GetPainelDividaQueryHandler`. Não substitui `GetPainelDividaQuery` (que agrupa por moeda); coexiste.

**Critérios de aceite:**
- Retorna `IReadOnlyList<SaldoBancoDto>` com `BancoId`, `BancoApelido`, `BancoCodigoCompe`, `SaldoBrl`, `QuantidadeContratos`, `TipoCotacaoUsada` (`SPOT_INTRADAY` | `PTAX_D1_FALLBACK`)
- Filtra contratos com `Status == Ativo`
- Bancos sem contratos ativos não aparecem no resultado
- Filtros opcionais: `Modalidade`, `Moeda` (consistentes com `/painel/divida`)
- Conversão usa midrate `(compra + venda) / 2` para PTAX (consistente)

**Verificação:**
- Integration test com Testcontainers: seed 3 bancos × 5 contratos × 3 moedas, valida soma e quantidade por banco
- Bate com `/painel/divida` quando somado por moeda

**Dependências:** Nenhuma

**Files likely touched:**
- `src/Sgcf.Application/Painel/Queries/GetSaldoPorBancoAtualQuery.cs` (novo)
- `src/Sgcf.Application/Painel/Queries/GetSaldoPorBancoAtualQueryHandler.cs` (novo)
- `src/Sgcf.Application/Painel/SaldoBancoDto.cs` (novo)
- `tests/Sgcf.Application.Tests/Painel/GetSaldoPorBancoAtualQueryHandlerTests.cs` (novo)

**Escopo:** S

---

### Task 1.3 — Estender `VencimentoItemDto` com `bancoId` e `bancoApelido`

**Descrição:** Adicionar campos `bancoId` e `bancoApelido` em `VencimentoItemDto` (parcelas individuais do calendário). Atualizar `GetCalendarioVencimentosQueryHandler` para popular esses campos a partir do contrato. Mudança aditiva — não quebra contrato existente.

**Critérios de aceite:**
- DTO ganha `BancoId: Guid` e `BancoApelido: string` (ambos não-nulos quando contrato tem banco — sempre tem)
- Suite de testes existente do calendário continua verde
- Pelo menos 1 teste novo valida que `bancoId` aparece corretamente em parcela de cada banco

**Verificação:**
- `dotnet test --filter "FullyQualifiedName~Calendario"` continua verde
- Curl em `/painel/vencimentos?ano=2026` mostra os novos campos

**Dependências:** Nenhuma

**Files likely touched:**
- `src/Sgcf.Application/Painel/VencimentoItemDto.cs`
- `src/Sgcf.Application/Painel/Queries/GetCalendarioVencimentosQueryHandler.cs`
- `tests/Sgcf.Api.IntegrationTests/Painel/CalendarioVencimentosApiTests.cs`

**Escopo:** S

---

### Task 1.4 — `GetQuadroDividaQuery` + endpoint `/painel/quadro-divida`

**Descrição:** Query handler que orquestra: (a) chama `GetSaldoPorBancoAtualQuery` para saldo de abertura; (b) chama repositório de eventos de cronograma para gerar lista de `EventoProjecao` do tipo `AmortizacaoPrincipal`; (c) invoca `ProjetorSaldoMensal.Projetar(saldoInicial, eventos, ano)`; (d) monta DTO de resposta. Endpoint REST expõe o resultado.

**Critérios de aceite:**
- `GET /api/v1/painel/quadro-divida?ano=2026` retorna `QuadroDividaDto` completo
- Filtros opcionais: `bancoId`, `modalidade` (igual ao `/painel/divida`)
- Resposta inclui:
  - `ano`
  - `dataHoraCalculo`
  - `tipoCotacao` (mesmo enum do `/painel/divida`)
  - `cenarioAplicado: null` (placeholder para Fase 3)
  - `snapshotAtual: { saldoTotalBrl, breakdownPorBanco[] }`
  - `meses[12]`: cada mês com `mesNumero`, `mesNome`, `saldoInicialTotalBrl`, `totalAmortizacaoBrl`, `totalCaptacaoBrl` (= 0 sem cenário), `saldoFinalTotalBrl`, `variacaoPctVsMesAnterior`, `bancos[]` (cada um com saldoInicial, amortizacao, captacao, saldoFim, share %)
  - `sumario[12]`: visão chata da tabela inferior da planilha (`Mês, Saldo Inicial, Total Captação, Total Liquidação, Saldo Final`)
- Quando ano informado é o passado, projeta normalmente mas log warn ("ano histórico, snapshot inicial é o atual")
- 401 Unauthorized sem token; 200 com qualquer role de leitura

**Verificação:**
- Integration test E2E com Testcontainers: seed 3 contratos, valida números em 3 meses específicos
- Smoke test manual: curl no dev valida resposta estruturada
- Cobertura ≥ 90% no handler

**Dependências:** 1.1, 1.2, 1.3

**Files likely touched:**
- `src/Sgcf.Application/Painel/Queries/GetQuadroDividaQuery.cs` (novo)
- `src/Sgcf.Application/Painel/Queries/GetQuadroDividaQueryHandler.cs` (novo)
- `src/Sgcf.Application/Painel/QuadroDividaDto.cs` (novo)
- `src/Sgcf.Application/Painel/MesQuadroDividaDto.cs` (novo)
- `src/Sgcf.Application/Painel/SaldoBancoMesDto.cs` (novo)
- `src/Sgcf.Api/Controllers/PainelController.cs` (add endpoint)
- `tests/Sgcf.Api.IntegrationTests/Painel/QuadroDividaApiTests.cs` (novo)

**Escopo:** M

---

### Checkpoint Fase 1

- [ ] Tasks 1.1, 1.2, 1.3, 1.4 mergeadas
- [ ] Suite completa verde (Domain + Application + IntegrationTests + GoldenDataset)
- [ ] Manual: `curl /api/v1/painel/quadro-divida?ano=2026` devolve estrutura completa, sem `cenarioAplicado`
- [ ] **Gate humano:** frontend valida que a estrutura é suficiente para renderizar a tabela base (sem simulações)

---

## 5. Fase 2 — Simulação de Contratação (domínio + persistência + API)

### Task 2.1 — Domain `CenarioSimulacao` + `SimulacaoContratacao`

**Descrição:** Criar agregado raiz `CenarioSimulacao` em `Sgcf.Domain.Simulacao` com child entity `SimulacaoContratacao`. Cenário tem ciclo de vida `Rascunho → Ativo → Arquivado`. Simulação carrega exatamente os campos que um contrato real precisaria para gerar cronograma.

**Esqueleto proposto:**

```csharp
public sealed class CenarioSimulacao : Entity, IAuditable
{
    public string Nome { get; private set; }
    public string? Descricao { get; private set; }
    public int AnoBase { get; private set; }                  // 2026
    public StatusCenarioSimulacao Status { get; private set; } // Rascunho | Ativo | Arquivado
    public string CriadoPor { get; private set; }
    public Instant CreatedAt { get; private set; }
    public Instant UpdatedAt { get; private set; }
    public Instant? DeletedAt { get; private set; }

    private readonly List<SimulacaoContratacao> _simulacoes = new();
    public IReadOnlyCollection<SimulacaoContratacao> Simulacoes => _simulacoes.AsReadOnly();

    public static CenarioSimulacao Criar(string nome, string? descricao, int anoBase, string criadoPor, IClock clock);
    public SimulacaoContratacao AdicionarSimulacao(/* params */);
    public void RemoverSimulacao(Guid simulacaoId);
    public void Ativar(IClock clock);   // requer >= 1 simulação
    public void Arquivar(IClock clock);
    public void Renomear(string nome, string? descricao, IClock clock);
    public void Deletar(IClock clock);  // soft delete
}

public sealed class SimulacaoContratacao : Entity
{
    public Guid CenarioId { get; private set; }
    public Guid BancoId { get; private set; }
    public ModalidadeContrato Modalidade { get; private set; }
    public Moeda Moeda { get; private set; }
    public Money ValorPrincipal { get; private set; }
    public LocalDate DataContratacaoPrevista { get; private set; }
    public LocalDate DataPrimeiroVencimento { get; private set; }
    public Percentual TaxaAa { get; private set; }
    public BaseCalculo BaseCalculo { get; private set; }
    public EstruturaAmortizacao EstruturaAmortizacao { get; private set; }
    public Periodicidade Periodicidade { get; private set; }
    public int QuantidadeParcelas { get; private set; }
    public string? Observacoes { get; private set; }
    public Instant CreatedAt { get; private set; }
    public Instant UpdatedAt { get; private set; }
}
```

**Critérios de aceite:**
- Validações de domínio:
  - `Nome` não vazio, ≤ 100 chars; único por owner (verificado na Application)
  - `AnoBase` entre 2020 e 2100
  - `Cenario.Ativar()` exige ≥ 1 simulação; lança `InvalidOperationException` se vazio
  - `Cenario.Arquivar()` permitido apenas a partir de `Ativo`
  - `Cenario.AdicionarSimulacao()` bloqueado em status `Arquivado`
  - `SimulacaoContratacao`: `ValorPrincipal > 0`; `DataContratacaoPrevista` >= hoje (clock); `DataPrimeiroVencimento` > `DataContratacaoPrevista`; `QuantidadeParcelas >= 1`
- `IClock` injetado para timestamps
- Soft delete via `DeletedAt` (consistente com padrão do projeto)
- Auditoria via `IAuditable`

**Verificação:**
- 20+ unit tests cobrindo criação, transição de status, validações, regras de adição/remoção
- Property test: cenário arquivado é imutável (qualquer método que muda estado lança)

**Dependências:** Nenhuma

**Files likely touched:**
- `src/Sgcf.Domain/Simulacao/CenarioSimulacao.cs` (novo)
- `src/Sgcf.Domain/Simulacao/SimulacaoContratacao.cs` (novo)
- `src/Sgcf.Domain/Simulacao/StatusCenarioSimulacao.cs` (novo)
- `src/Sgcf.Domain/Simulacao/ICenarioSimulacaoRepository.cs` (novo)
- `tests/Sgcf.Domain.Tests/Simulacao/CenarioSimulacaoTests.cs` (novo)
- `tests/Sgcf.Domain.Tests/Simulacao/SimulacaoContratacaoTests.cs` (novo)

**Escopo:** M

---

### Task 2.2 — Migration + EF Configurations + Repository

**Descrição:** Migration `S8_SimulacaoContratacao` cria tabelas `cenario_simulacao` e `simulacao_contratacao`. EF configurations mapeiam ownership de Money, índices e FKs. Implementação do `ICenarioSimulacaoRepository` em `Sgcf.Infrastructure`.

**Critérios de aceite:**
- Tabelas:
  - `cenario_simulacao(id uuid pk, nome text not null, descricao text, ano_base int not null, status int not null, criado_por text not null, created_at timestamptz, updated_at timestamptz, deleted_at timestamptz)`
  - `simulacao_contratacao(id uuid pk, cenario_id uuid fk → cenario_simulacao.id ON DELETE CASCADE, banco_id uuid fk → banco.id ON DELETE RESTRICT, modalidade int, moeda int, valor_principal numeric(20,6), valor_principal_moeda int, data_contratacao_prevista date, data_primeiro_vencimento date, taxa_aa numeric(9,6), base_calculo int, estrutura_amortizacao int, periodicidade int, quantidade_parcelas int, observacoes text, created_at, updated_at)`
- Índices:
  - `idx_cenario_status_owner` em `(status, criado_por)`
  - `idx_simulacao_cenario` em `(cenario_id)`
  - `idx_simulacao_banco` em `(banco_id)`
- Soft delete query filter em `cenario_simulacao`
- Repository: `GetByIdAsync(id, ct)` com Include de simulações; `ListAsync(filtros, ct)`; `AddAsync`; `Remove`
- Audit interceptor existente captura mudanças automaticamente

**Verificação:**
- `dotnet ef migrations add S8_SimulacaoContratacao --project src/Sgcf.Infrastructure --startup-project src/Sgcf.Api` aplica limpo
- `dotnet ef database update` em DB de teste sobe sem erros
- Integration test cria cenário com 2 simulações, lê, valida persistência

**Dependências:** 2.1

**Files likely touched:**
- `src/Sgcf.Infrastructure/Migrations/S8_SimulacaoContratacao.cs` (novo)
- `src/Sgcf.Infrastructure/Persistence/Configurations/CenarioSimulacaoConfiguration.cs` (novo)
- `src/Sgcf.Infrastructure/Persistence/Configurations/SimulacaoContratacaoConfiguration.cs` (novo)
- `src/Sgcf.Infrastructure/Persistence/Repositories/CenarioSimulacaoRepository.cs` (novo)
- `src/Sgcf.Infrastructure/Persistence/SgcfDbContext.cs` (add DbSets)
- `tests/Sgcf.Application.Tests/Simulacao/CenarioSimulacaoRepositoryTests.cs` (novo)

**Escopo:** M

---

### Task 2.3 — Application: Commands + Queries (CRUD)

**Descrição:** Implementar handlers MediatR para o ciclo de vida completo do cenário e suas simulações. FluentValidation valida payloads.

**Comandos e queries:**

| Tipo | Nome | Descrição |
|------|------|-----------|
| Command | `CriarCenarioSimulacaoCommand` | Cria cenário em status `Rascunho` |
| Command | `AdicionarSimulacaoCommand` | Adiciona uma simulação a cenário em `Rascunho`/`Ativo` |
| Command | `RemoverSimulacaoCommand` | Remove simulação do cenário |
| Command | `AtualizarCenarioCommand` | Renomeia / descrição / anoBase (apenas em `Rascunho`) |
| Command | `AtivarCenarioCommand` | Transição `Rascunho` → `Ativo` |
| Command | `ArquivarCenarioCommand` | Transição `Ativo` → `Arquivado` |
| Command | `DeletarCenarioCommand` | Soft delete |
| Query | `GetCenarioSimulacaoByIdQuery` | Cenário com todas as simulações |
| Query | `ListCenariosSimulacaoQuery` | Filtros: status, criadoPor, anoBase |

**Critérios de aceite:**
- Validators cobrem todos os campos com mensagens claras
- Idempotência via header `Idempotency-Key` em criar/adicionar
- DTOs distintos do domínio (`CenarioSimulacaoDto`, `SimulacaoContratacaoDto`)
- Erros padronizados RFC 7807
- Audit log capturado via `source: rest` automaticamente
- Cobertura ≥ 85%

**Verificação:**
- Integration tests para cada comando/query (8 endpoints × 1+ caminhos felizes + erros principais)
- Property: nenhum command modifica cenário arquivado

**Dependências:** 2.1, 2.2

**Files likely touched:**
- `src/Sgcf.Application/Simulacao/Commands/*` (8 arquivos)
- `src/Sgcf.Application/Simulacao/Queries/*` (4 arquivos: query + handler para cada uma)
- `src/Sgcf.Application/Simulacao/Validators/*` (per command)
- `src/Sgcf.Application/Simulacao/Dtos/*`
- `tests/Sgcf.Application.Tests/Simulacao/*Tests.cs`

**Escopo:** L (mas internamente fragmentado em 8 commands/queries pequenos)

---

### Task 2.4 — `SimulacaoCronogramaCalculator`

**Descrição:** Helper na camada Application que dado uma `SimulacaoContratacao` constrói o `GerarCronogramaInput` correspondente, chama `CronogramaStrategyFactory.Criar(estrutura).Gerar(...)` e retorna `IReadOnlyList<EventoCronogramaGerado>`. Não persiste nada.

**Critérios de aceite:**
- Função pura (sem I/O); apenas chama domain
- Validações: simulação deve ter todos os campos necessários
- Retorna eventos com `EventoCronogramaGerado` existente (Principal, Juros, ...)
- Cobertura ≥ 95% com cenários Bullet, BulletComJuros, Price, SAC
- Cronograma gerado para uma simulação X é IDÊNTICO ao cronograma gerado se o contrato real X fosse criado com os mesmos parâmetros (golden test)

**Verificação:**
- 4 golden tests (1 por estrutura) reproduzindo exatamente os números de um contrato real existente
- Property: total das parcelas Principal == ValorPrincipal

**Dependências:** 2.1

**Files likely touched:**
- `src/Sgcf.Application/Simulacao/SimulacaoCronogramaCalculator.cs` (novo)
- `tests/Sgcf.Application.Tests/Simulacao/SimulacaoCronogramaCalculatorTests.cs` (novo)

**Escopo:** S

---

### Task 2.5 — API REST CRUD

**Descrição:** Controller `SimulacoesController` expõe os 8 endpoints CRUD do cenário e suas simulações. Idempotência via header. RBAC via policies.

**Endpoints:**

| Verb | Path | Policy | Comando |
|------|------|--------|---------|
| POST | `/api/v1/simulacoes/cenarios` | Tesouraria | `CriarCenarioSimulacaoCommand` |
| GET | `/api/v1/simulacoes/cenarios` | Leitura | `ListCenariosSimulacaoQuery` |
| GET | `/api/v1/simulacoes/cenarios/{id}` | Leitura | `GetCenarioSimulacaoByIdQuery` |
| PATCH | `/api/v1/simulacoes/cenarios/{id}` | Tesouraria | `AtualizarCenarioCommand` |
| POST | `/api/v1/simulacoes/cenarios/{id}/ativar` | Tesouraria | `AtivarCenarioCommand` |
| POST | `/api/v1/simulacoes/cenarios/{id}/arquivar` | Gerente | `ArquivarCenarioCommand` |
| DELETE | `/api/v1/simulacoes/cenarios/{id}` | Tesouraria | `DeletarCenarioCommand` |
| POST | `/api/v1/simulacoes/cenarios/{id}/simulacoes` | Tesouraria | `AdicionarSimulacaoCommand` |
| DELETE | `/api/v1/simulacoes/cenarios/{id}/simulacoes/{simId}` | Tesouraria | `RemoverSimulacaoCommand` |

**Critérios de aceite:**
- OpenAPI/Swagger documenta todos os endpoints com exemplos
- Códigos de status corretos (201/200/204/400/403/404/409)
- Smoke test E2E cobre o fluxo completo (criar → adicionar → ativar → consultar → arquivar)

**Verificação:**
- `dotnet test tests/Sgcf.Api.IntegrationTests/Sgcf.Api.IntegrationTests.csproj --filter "FullyQualifiedName~Simulacoes"`

**Dependências:** 2.3

**Files likely touched:**
- `src/Sgcf.Api/Controllers/SimulacoesController.cs` (novo)
- `src/Sgcf.Application/Authorization/Policies.cs` (eventual policy nova)
- `tests/Sgcf.Api.IntegrationTests/Simulacao/SimulacaoApiFixture.cs` (novo)
- `tests/Sgcf.Api.IntegrationTests/Simulacao/CenarioSimulacaoApiTests.cs` (novo)

**Escopo:** M

---

### Task 2.6 — Endpoint preview de cronograma hipotético

**Descrição:** Endpoint utilitário para frontend pré-visualizar o cronograma de uma simulação antes de salvar.

**Endpoint:** `POST /api/v1/simulacoes/cronograma-hipotetico` (POST porque o payload é amplo)

**Body:** todos os campos de `SimulacaoContratacao` (sem `cenarioId`)
**Response:** array de eventos gerados (`data`, `tipo`, `valorMoedaOriginal`, `numeroEvento`)

**Critérios de aceite:**
- Endpoint não persiste nada — função pura
- Pode ser chamado sem cenário existente
- Validação igual ao adicionar simulação

**Verificação:**
- Integration test compara resposta com um contrato real equivalente

**Dependências:** 2.4

**Files likely touched:**
- `src/Sgcf.Application/Simulacao/Queries/SimularCronogramaHipoteticoQuery.cs` (novo)
- `src/Sgcf.Api/Controllers/SimulacoesController.cs` (add endpoint)

**Escopo:** S

---

### Checkpoint Fase 2

- [ ] Tasks 2.1 a 2.6 mergeadas
- [ ] `dotnet test` completo verde
- [ ] Migration aplicada em dev
- [ ] Manual: criar cenário via Bruno, adicionar 3 simulações, consultar, ativar — fluxo completo
- [ ] **Gate humano:** frontend valida shape dos DTOs

---

## 6. Fase 3 — Integração no Quadro

### Task 3.1 — `GetQuadroDividaQuery` aceita `cenarioId`

**Descrição:** Estender o handler da Task 1.4 para, quando `cenarioId` for informado, buscar o cenário, gerar cronogramas hipotéticos das simulações via `SimulacaoCronogramaCalculator`, converter eventos para `EventoProjecao` (captação na `DataContratacaoPrevista` + amortizações nas datas dos eventos), agregar com os eventos reais e passar tudo para o `ProjetorSaldoMensal`.

**Critérios de aceite:**
- `GET /api/v1/painel/quadro-divida?ano=2026&cenarioId={id}` aplica simulações ao quadro
- Resposta inclui `cenarioAplicado: { id, nome, status, quantidadeSimulacoes }`
- Cenário arquivado retorna 200 (pode visualizar histórico) mas com aviso
- Cenário inexistente: 404
- Cenário com `anoBase` diferente do `ano` da query: 422 com mensagem clara
- Captação aparece em `mes[i].totalCaptacaoBrl` somando todas as simulações cuja `DataContratacaoPrevista` cai naquele mês
- Amortizações da simulação reduzem o saldo do banco correspondente

**Verificação:**
- Integration test: cenário com 2 simulações em meses distintos, valida números mês a mês
- Property test: somar todos os `totalCaptacaoBrl` no ano == soma dos `valorPrincipal` das simulações ativas do cenário

**Dependências:** 1.4, 2.4

**Files likely touched:**
- `src/Sgcf.Application/Painel/Queries/GetQuadroDividaQueryHandler.cs` (modify)
- `src/Sgcf.Application/Painel/QuadroDividaDto.cs` (add `cenarioAplicado`)
- `src/Sgcf.Application/Painel/CenarioAplicadoDto.cs` (novo)
- `tests/Sgcf.Api.IntegrationTests/Painel/QuadroDividaComCenarioApiTests.cs` (novo)

**Escopo:** M

---

### Task 3.2 — `ProjetorSaldoMensal` aceita eventos de Captação

**Descrição:** Refactor menor: o projetor de 1.1 já aceita o enum `TipoEventoProjecao`, mas a primeira versão só usava `AmortizacaoPrincipal`. Esta task formaliza o suporte a `Captacao` e adiciona testes específicos.

**Critérios de aceite:**
- `EventoProjecao` com `Tipo = Captacao` aumenta o saldo do banco no mês correspondente
- `EventoProjecao` com `Tipo = AmortizacaoPrincipal` reduz o saldo
- Eventos do mesmo banco no mesmo mês são somados antes de aplicar
- Captação só altera saldo no mês da `DataContratacaoPrevista`
- Property test: `SaldoFinal[m] = SaldoInicial[m] - Σ Amortizações[m] + Σ Captações[m]`

**Verificação:**
- 4 unit tests cobrindo combinações
- 1 property test com FsCheck (3 bancos × 12 meses × eventos aleatórios)

**Dependências:** 1.1

**Files likely touched:**
- `src/Sgcf.Domain/Painel/ProjetorSaldoMensal.cs` (modify)
- `tests/Sgcf.Domain.Tests/Painel/ProjetorSaldoMensalCaptacaoTests.cs` (novo)

**Escopo:** S

---

### Task 3.3 — Endpoint conveniência `/simulacoes/cenarios/{id}/quadro-divida`

**Descrição:** Endpoint atalho para frontend: dado um cenário, retorna o quadro da dívida com aquele cenário aplicado. Internamente chama `GetQuadroDividaQuery` com o `anoBase` do cenário.

**Critérios de aceite:**
- `GET /api/v1/simulacoes/cenarios/{id}/quadro-divida` retorna `QuadroDividaDto`
- Usa `cenario.AnoBase` automaticamente
- Aceita query `?ano=...` opcional (override do anoBase para casos de teste)
- Mesmas regras de erro que 3.1

**Verificação:**
- Integration test: cenário ativo + chamada de conveniência == chamada direta com `cenarioId`

**Dependências:** 3.1

**Files likely touched:**
- `src/Sgcf.Api/Controllers/SimulacoesController.cs` (add endpoint)
- `tests/Sgcf.Api.IntegrationTests/Simulacao/CenarioQuadroDividaApiTests.cs` (novo)

**Escopo:** S

---

### Checkpoint Fase 3

- [ ] Tasks 3.1, 3.2, 3.3 mergeadas
- [ ] Frontend consegue renderizar quadro completo com cenário aplicado
- [ ] **Gate humano:** reproduzir manualmente os números de 1 mês da planilha original com cenário equivalente

---

## 7. Fase 4 — Polimento

### Task 4.1 — Comparativo entre cenários

**Descrição:** Endpoint que recebe múltiplos `cenarioId` e retorna cada quadro projetado com deltas vs. o primeiro (baseline).

**Endpoint:** `POST /api/v1/simulacoes/comparar`

**Body:**
```json
{ "ano": 2026, "cenarioIds": ["id1", "id2", "id3"] }
```

**Response:** `ResultadoComparacaoCenariosDto` com cada cenário projetado + deltas mensais e anuais vs. baseline.

**Critérios de aceite:**
- Máximo 5 cenários por chamada (limite operacional)
- Primeiro `cenarioId` é baseline; deltas relativos a ele
- Cenários de `anoBase` diferentes rejeitados com 422

**Verificação:**
- Integration test com 3 cenários distintos

**Dependências:** 3.1

**Files likely touched:**
- `src/Sgcf.Application/Simulacao/Queries/CompararCenariosQuery.cs` (novo)
- `src/Sgcf.Application/Simulacao/Queries/CompararCenariosQueryHandler.cs` (novo)
- `src/Sgcf.Api/Controllers/SimulacoesController.cs` (add endpoint)
- `tests/Sgcf.Api.IntegrationTests/Simulacao/CompararCenariosApiTests.cs` (novo)

**Escopo:** M

---

### Task 4.2 — Golden dataset da planilha

**Descrição:** Capturar 1 mês da `Quadro_da_Divida` (janeiro/2026) como golden test. Inputs: saldo inicial por banco (replicado do snapshot 31/12/2024), 0 simulações (real-only). Output esperado: saldo de fechamento por banco em janeiro.

**Critérios de aceite:**
- Arquivo `tests/Sgcf.GoldenDataset/data/quadro-divida-2026/input.json` com setup
- Arquivo `output_esperado.json` com saldos esperados
- Driver test em `tests/Sgcf.GoldenDataset` invoca `ProjetorSaldoMensal` e compara
- Tolerância: R$ 1,00 (arredondamento de centavos pode acumular)

**Verificação:**
- Test passa
- Documentar premissas (data de PTAX, contratos considerados) no `README.md` da pasta

**Dependências:** 1.1, 1.4

**Files likely touched:**
- `tests/Sgcf.GoldenDataset/data/quadro-divida-2026/*` (novo)
- `tests/Sgcf.GoldenDataset/QuadroDivida2026GoldenTest.cs` (novo)

**Escopo:** M

---

### Task 4.3 — Documentação

**Descrição:** Documentação completa para frontend e agentes externos.

**Entregáveis:**
- `docs/api/painel.md` — seção `Quadro da Dívida` com exemplo completo
- `docs/api/simulacoes.md` — novo documento com todos os endpoints
- `docs/specs/simulacoes/SPEC.md` — spec consolidado (resumo deste plano + esquema)
- `docs/api/collections/sgcf-api/13-Simulacoes/*` — Bruno collection (10+ requests)
- `docs/api/collections/sgcf-api/07-Painel/6 - Quadro da Divida.bru` — request adicional
- `docs/api/schemas.md` — novos DTOs (`QuadroDividaDto`, `CenarioSimulacaoDto`, etc.)
- `docs/changelog/CHANGELOG.md` — entrada v0.7.0 com Resumo executivo

**Critérios de aceite:**
- Todos os endpoints documentados com exemplo
- Bruno collection executável contra dev local

**Verificação:**
- Manual: ler docs, executar Bruno requests, validar respostas

**Dependências:** 3.3, 4.1

**Files likely touched:** (todos novos ou modificados)

**Escopo:** M

---

### Task 4.4 — Tools MCP read-only

**Descrição:** Adicionar 3 tools MCP read-only para que agentes externos (Claude Desktop, etc.) consultem o quadro e cenários.

**Tools:**
- `get_quadro_divida(ano: int, cenarioId?: string)` → `QuadroDividaDto`
- `list_cenarios_simulacao(status?: string, anoBase?: int)` → `[CenarioResumo]`
- `get_cenario_simulacao(id: string)` → `CenarioSimulacaoDto`

**Critérios de aceite:**
- Tools reusam os mesmos MediatR handlers do REST
- JSON Schema 2020-12 documentado em `docs/mcp/tools/`
- Audit log com `source: mcp`

**Verificação:**
- Test MCP de invocação local (`Sgcf.Mcp.Tests`)
- Manual: configurar Claude Desktop com endpoint MCP local e pedir "quanto vou dever no banco BB em outubro?"

**Dependências:** 3.1, 4.1

**Files likely touched:**
- `src/Sgcf.Mcp/Tools/GetQuadroDividaTool.cs` (novo)
- `src/Sgcf.Mcp/Tools/ListCenariosSimulacaoTool.cs` (novo)
- `src/Sgcf.Mcp/Tools/GetCenarioSimulacaoTool.cs` (novo)
- `docs/mcp/tools/*.json` (novo)
- `tests/Sgcf.Mcp.Tests/*Tests.cs` (novo)

**Escopo:** M

---

### Checkpoint Final — Release v0.10.0 [ENTREGUE 2026-05-19]

- [x] Todas as tasks de Fases 1, 2 e 3 mergeadas (4.1, 4.2 e 4.4 movidos para próximo sprint)
- [x] Suite fast completa verde — 847 testes
- [x] Suite Slow verde (~60 testes Testcontainers)
- [x] Cobertura ≥ 85% no módulo `Simulacao`, ≥ 90% no `Painel`
- [x] Migrations S9 e S10 aplicadas
- [x] Documentação API publicada em `docs/api/`
- [x] CHANGELOG v0.10.0 publicado
- [ ] **Gate humano:** sponsor valida que a planilha pode ser aposentada para o caso de uso do Quadro da Dívida _(pendente)_

---

## 8. Sumário executivo

| Métrica | Valor |
|---------|-------|
| **Fases** | 4 |
| **Tasks** | 14 |
| **Checkpoints** | 4 |
| **Migrations novas** | 1 (S8_SimulacaoContratacao) |
| **Endpoints REST novos** | 12 |
| **Tools MCP novos** | 3 |
| **Tabelas novas** | 2 (cenario_simulacao, simulacao_contratacao) |
| **Sem mudanças destrutivas em endpoints existentes** | ✅ (apenas campos aditivos) |
| **Release alvo** | v0.7.0 |

### Tasks por tamanho

- **S (1-2 files):** 1.2, 1.3, 2.4, 2.6, 3.2, 3.3 — 6 tasks
- **M (3-5 files):** 1.1, 1.4, 2.1, 2.2, 2.5, 3.1, 4.1, 4.2, 4.3, 4.4 — 10 tasks
- **L (5-8 files):** Nenhuma (a maior, 2.3, foi declarada L por agregar 8 commands/queries pequenos; pode ser sub-fragmentada se necessário)

### Paralelização possível

- **Fase 1**: 1.1, 1.2, 1.3 são independentes → 3 agentes em paralelo
- **Fase 2**: 2.1 → 2.2; 2.4 paralelo com 2.2 (não depende); 2.3 depende de 2.2; 2.5 depende de 2.3; 2.6 depende de 2.4 → grafo permite 2-3 agentes em pico
- **Fase 4**: 4.1, 4.2, 4.3, 4.4 são quase totalmente independentes → 4 agentes em paralelo

---

## 9. Riscos e mitigações

| # | Risco | Impacto | Probabilidade | Mitigação |
|---|-------|---------|---------------|-----------|
| R1 | Performance: 1.200 contratos × 12 meses + N simulações por consulta | M | M | Cache do cálculo em Redis com TTL curto (5min); invalidar ao mutate cenário; benchmark obrigatório com 1.500 contratos |
| R2 | Conversão de moeda em projeção futura usa spot atual → subestima saldo BRL futuro de contratos USD em alta | M | A | Documentar limitação no SPEC; futuro: integração com cenário cambial deste mesmo handler |
| R3 | Saldo inicial para o mês corrente vs. fim de mês — confusão semântica | M | M | Convenção explícita: `mes[1].saldoInicio == GetSaldoPorBancoAtualQuery` (snapshot atual); documentar no DTO |
| R4 | Cenário com simulação para banco que não existe mais (banco desativado) | B | B | FK ON DELETE RESTRICT no banco; cenário trava migração que tenta deletar banco |
| R5 | Cronograma da simulação difere do contrato real após conversão (drift entre `SimulacaoCronogramaCalculator` e `CreateContratoCommand`) | A | M | Golden test obrigatório: 1 simulação → cronograma; mesmo input criando contrato real via REST → cronograma. Iguais. Falha se divergir. |
| R6 | RBAC mal aplicado permite usuário fora de tesouraria criar cenário | A | B | Test E2E com token de cada role; gate no CI |
| R7 | Migration S8 conflita com Onda 0 / S6 das cotações | B | M | Garantir que a migration roda APÓS Onda 0 mergeada; documentar ordem |

---

## 10. Open Questions — **Respondidas em 2026-05-19**

| # | Pergunta | Decisão | Notas |
|---|----------|---------|-------|
| Q1 | Owner do cenário (edição) | **Qualquer membro da tesouraria edita qualquer cenário** | Transparência intra-equipe. Audit via `audit_log` + `UpdatedAt`. Sem RBAC fino. |
| Q2 | Notificação ao arquivar | **Fora do MVP — apenas `audit_log`** | E-mail/push fica para módulo de Alertas futuro. |
| Q3 | Cenário "ativo" como default no frontend | **Decisão só do frontend** | Backend não tem flag `IsDefault`. UI gerencia preferência. |
| Q4 | Taxa fixa ou CDI+spread? | **Taxa fixa OU CDI+spread (D-4 do SPEC)** | `SimulacaoContratacao` aceita `TipoTaxa: Fixa \| CdiSpread`. Para CDI: campos `PercentualCdi` + `CdiAaPercentualReferencia` (snapshot CDI vigente). |
| Q5 | Validação contra `LimiteBanco` | **Alerta não-bloqueante via `alertas[]`** | Permite planejamento "e se aumentarmos limite". Validação estrita só na conversão real. |
| Q6 | Garantias na simulação | **Sim — capturar `garantiaExigidaPrevista` (string descritiva)** | Campo livre na simulação ("CDB cativo 20%"). Informativo. Não valida contra `LimiteBanco`. **Mudança vs proposta MVP** (era "não"). |
| Q7 | Duplicar cenário existente | **Sim — endpoint `POST /cenarios/{id}/duplicar`** | Cópia profunda; status `Rascunho`; `criadoPor` = caller. **Mudança vs proposta MVP** (era "não"). Adiciona ~1 task. |
| Q8 | Tetão mensal (R$ 4MM) | **Sim — configurável via `ParametroSistema`** | Operador admin define o limite em runtime. Alerta no `alertas[]` quando captações+amortizações de um mês excedem. **Mudança vs proposta MVP** (era "fora MVP"). Adiciona ~1 task. |

### Impacto das 3 mudanças vs proposta MVP

- **Q6**: `SimulacaoContratacao` ganha campo `GarantiaExigidaPrevista : string?` opcional. Impacto: 1 campo no agregado + 1 coluna no schema + serialização nos DTOs. Sem nova lógica de validação.
- **Q7**: Novo endpoint `POST /simulacoes/cenarios/{id}/duplicar` + command `DuplicarCenarioCommand`. Domain ganha factory `CenarioSimulacao.DuplicarComoRascunho(...)`. Impacto: +1 task (escopo M).
- **Q8**: Nova chave `ParametroSistema.TetaoMensalCapacidadeBrl` (decimal nullable). Helper `ValidadorTetaoMensal` injetado em `GetQuadroDividaQueryHandler` lê o valor e emite alerta. Impacto: +1 task (escopo S) + 1 migration aditiva para chave default.

---

## 11. Glossário

| Termo | Definição |
|-------|-----------|
| **Quadro da Dívida** | Visão consolidada multi-banco, mês a mês, de saldo, captações e liquidações para um ano civil. |
| **Cenário de Simulação** | Agregado nomeado contendo N simulações de contratação. Tem ciclo de vida Rascunho/Ativo/Arquivado. |
| **Simulação de Contratação** | Captação hipotética futura: banco, modalidade, valor, prazo, estrutura. Não é contrato real. |
| **Cronograma hipotético** | Conjunto de eventos gerados on-the-fly a partir de uma simulação, sem persistência. |
| **Evento de Projeção** | Unidade comum no projetor: `AmortizacaoPrincipal` (de contrato real ou simulado) ou `Captacao` (apenas de simulação). |
| **Saldo de Abertura (mês N)** | Saldo no primeiro dia útil do mês N. Igual ao saldo de fechamento do mês N-1. Para mês 1 do ano consultado, = saldo atual da carteira. |
| **Saldo de Fechamento (mês N)** | Saldo de Abertura − amortizações do mês + captações do mês. |
| **Share** | Percentual de cada banco no saldo total de fechamento do mês. |
| **Variação** | `(SaldoFechamento[N] − SaldoFechamento[N-1]) / SaldoFechamento[N-1]`. Para mês 1, vs. snapshot atual. |

---

## 12. Referências

- `documentos/Endividamento.xlsx` — aba `Quadro_da_Divida` (fonte canônica visual)
- `SPEC.md` §1, §2 (US-08, US-16), §11 (RBAC) — contexto do simulador
- `sgcf-backend/docs/api/painel.md` — endpoints atuais que serão estendidos
- `sgcf-backend/docs/api/simulador.md` — simulador existente (cenário cambial + antecipação portfólio); coexiste sem conflito
- `tasks/cotacoes-modalidades/plan.md` — estilo de planejamento adotado
- `sgcf-backend/docs/changelog/CHANGELOG.md` — formato de release
