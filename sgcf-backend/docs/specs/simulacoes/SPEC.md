# SPEC — Simulações de Contratação + Quadro da Dívida

> **Status:** Entregue — v0.10.0 (2026-05-19)
> **Data:** 2026-05-18
> **Autor:** Análise técnica colaborativa (PO + arquitetura)
> **Versão:** v1.0
> **Release alvo:** v0.7.0 → entregue como v0.10.0 (versão ajustada após sequência 0.7–0.9)
> **Plano de execução:** `tasks/quadro-divida-simulacao/plan.md`

---

## 0. Premissas e Decisões Trancadas

> Decisões aprovadas em 2026-05-18 antes da redação deste SPEC. Imutáveis sem nova rodada de revisão.

| # | Decisão | Origem |
|---|---------|--------|
| D-1 | Simulações agrupadas em **Cenário com versionamento** (Rascunho/Ativo/Arquivado) | PO 2026-05-18 |
| D-2 | Quadro da Dívida cobre **12 meses do ano informado** (`?ano=YYYY`) | PO 2026-05-18 |
| D-3 | Projeção considera **apenas dívida pura** — MTM de hedge e simulação de antecipação ficam fora | PO 2026-05-18 |
| D-4 | `SimulacaoContratacao` suporta **taxa fixa OU CDI+spread** | PO 2026-05-18 (Q4) |
| D-5 | Ultrapassar `LimiteBanco` gera **alerta não-bloqueante** | PO 2026-05-18 (Q5) |
| D-6 | **Qualquer membro da tesouraria edita qualquer cenário** (sem RBAC fino); audit via `audit_log` | PO 2026-05-19 (Q1) |
| D-7 | **Notificações fora do MVP** — audit_log cobre arquivamento | PO 2026-05-19 (Q2) |
| D-8 | **Cenário default só no frontend** (sem flag backend) | PO 2026-05-19 (Q3) |
| D-9 | **Simulações capturam `GarantiaExigidaPrevista` (string descritiva)** — informativa, sem validação contra `LimiteBanco` | PO 2026-05-19 (Q6) |
| D-10 | **Endpoint `POST /cenarios/{id}/duplicar`** cria cópia profunda em status `Rascunho` | PO 2026-05-19 (Q7) |
| D-11 | **Tetão mensal configurável via `ParametroSistema.TetaoMensalCapacidadeBrl`** — alerta não-bloqueante no `alertas[]` do Quadro quando captações+amortizações de um mês excedem | PO 2026-05-19 (Q8) |

---

## 1. Objetivo

### 1.1 O que estamos construindo

Duas capacidades acopladas:

1. **Quadro da Dívida** — endpoint único que devolve, para um ano civil, o saldo da dívida da carteira mês a mês, com breakdown por banco, totais consolidados e variação. Reproduz a aba `Quadro_da_Divida` da planilha de Endividamento.
2. **Simulação de Contratação** — novo módulo que permite criar cenários nomeados (Otimista/Realista/Pessimista) contendo N captações hipotéticas futuras. Aplicado opcionalmente ao Quadro da Dívida, projeta o impacto das novas operações sobre saldo e share por banco.

### 1.2 Por quê

Hoje a aba `Quadro_da_Divida` da planilha é a visão central da tesouraria para planejamento de captação e amortização. O backend cobre fragmentos (saldo atual, calendário de vencimentos) mas não permite responder à pergunta "qual a posição da dívida em outubro se eu tomar R$ 5MM no BB em julho?". Sem essa capacidade, a tesouraria continua dependendo da planilha para qualquer decisão de planejamento.

### 1.3 Personas

| Persona | Necessidade primária |
|---|---|
| **Tesouraria — Analista** | Criar cenários de captação, simular fluxos alternativos, comparar lado a lado, decidir banco/modalidade/timing |
| **Gerente Financeiro** | Aprovar cenário antes de ir para comitê; arquivar cenários superados |
| **Diretoria Financeira** | Consultar quadro consolidado em reunião; entender impacto de captações no Dívida/EBITDA |
| **Auditoria** | Reconstituir qual cenário foi usado para uma decisão histórica |

### 1.4 Sucesso (system-level)

| # | Critério | Como medir |
|---|----------|------------|
| 1 | Frontend renderiza a tabela inteira com uma única chamada de API | Network tab mostra 1 request `GET /painel/quadro-divida` |
| 2 | Cenário com 5 simulações é projetado em ≤ 500ms (carteira de 1.500 contratos) | Logs OpenTelemetry com p99 |
| 3 | Reproduz exatamente os números de janeiro/2026 da planilha original (sem cenário) | Golden test passa com tolerância R$ 1,00 |
| 4 | Cronograma gerado por simulação é idêntico ao cronograma do contrato real após conversão (mesmos inputs) | Golden test cruzado simulação ↔ contrato |
| 5 | Cenário arquivado permanece consultável indefinidamente (auditoria histórica) | Integration test: arquivar → consultar → 200 OK |

### 1.5 User stories

| # | Como | Quero | Para |
|---|------|-------|------|
| US-Q1 | Analista de tesouraria | Ver a posição consolidada da dívida em 12 meses do ano corrente, com breakdown por banco | Substituir a aba `Quadro_da_Divida` da planilha |
| US-Q2 | Analista de tesouraria | Criar um cenário "Realista 2026" com 5 captações previstas | Planejar a estratégia anual de captação |
| US-Q3 | Analista de tesouraria | Adicionar/remover/editar simulações no cenário enquanto está em Rascunho | Iterar a análise sem afetar nada |
| US-Q4 | Analista de tesouraria | Pré-visualizar o cronograma de uma simulação antes de salvar | Validar que a estrutura faz sentido |
| US-Q5 | Gerente financeiro | Ativar um cenário pronto (transição Rascunho → Ativo) | Marcar como decisão tomada |
| US-Q6 | Analista de tesouraria | Aplicar um cenário ao Quadro da Dívida | Ver o impacto das captações no saldo mensal |
| US-Q7 | Gerente financeiro | Comparar até 5 cenários lado a lado, com deltas | Decidir qual estratégia adotar |
| US-Q8 | Gerente financeiro | Arquivar cenário superado (transição Ativo → Arquivado) | Manter apenas cenários vivos visíveis na lista |
| US-Q9 | Auditor | Consultar cenário arquivado meses depois | Reconstituir decisões históricas |
| US-Q10 | Analista de tesouraria | Receber alerta quando simulação ultrapassa o limite de crédito do banco | Identificar necessidade de pedido de aumento |

---

## 2. Tech Stack

Herdado do projeto principal (SPEC.md raiz §3). Sem adições.

| Camada | Tecnologia |
|---|---|
| Backend | .NET 10 + ASP.NET Core |
| ORM | Entity Framework Core 10 |
| Banco | PostgreSQL 16 (Cloud SQL) |
| Datas | NodaTime |
| Validação | FluentValidation |
| Testes | xUnit + FluentAssertions + Testcontainers + FsCheck |
| Cálculo | Motor `Sgcf.Domain.Cronograma` (Bullet/SAC/Price/BulletComJurosPeriódicos) já existente |

---

## 3. Commands

```bash
# Build / restore
dotnet restore
dotnet build --configuration Release /p:TreatWarningsAsErrors=true

# Testes — feedback rápido
dotnet test --filter "Category!=Slow"

# Testes — domínio do módulo
dotnet test tests/Sgcf.Domain.Tests --filter "FullyQualifiedName~Simulacao"
dotnet test tests/Sgcf.Domain.Tests --filter "FullyQualifiedName~ProjetorSaldoMensal"

# Testes — application
dotnet test tests/Sgcf.Application.Tests --filter "FullyQualifiedName~Simulacao"
dotnet test tests/Sgcf.Application.Tests --filter "FullyQualifiedName~QuadroDivida"

# Testes — E2E API
dotnet test tests/Sgcf.Api.IntegrationTests --filter "FullyQualifiedName~Simulacao"
dotnet test tests/Sgcf.Api.IntegrationTests --filter "FullyQualifiedName~QuadroDivida"

# Golden dataset
dotnet test tests/Sgcf.GoldenDataset --filter "FullyQualifiedName~QuadroDivida2026"

# Migration
dotnet ef migrations add S8_SimulacaoContratacao \
    --project src/Sgcf.Infrastructure --startup-project src/Sgcf.Api

dotnet ef database update \
    --project src/Sgcf.Infrastructure --startup-project src/Sgcf.Api

# Cobertura
dotnet test --collect:"XPlat Code Coverage" --results-directory ./coverage

# Smoke local
curl http://localhost:5000/api/v1/painel/quadro-divida?ano=2026 \
    -H "Authorization: Bearer dev-test-token" | jq .
```

---

## 4. Project Structure

```
sgcf-backend/
├── src/
│   ├── Sgcf.Domain/
│   │   ├── Painel/                          # já existe
│   │   │   ├── EventoProjecao.cs            # NOVO — record (BancoId, Data, Tipo, ValorBrl)
│   │   │   ├── TipoEventoProjecao.cs        # NOVO — enum (AmortizacaoPrincipal, Captacao)
│   │   │   ├── ProjetorSaldoMensal.cs       # NOVO — pure function
│   │   │   ├── QuadroDividaProjecao.cs      # NOVO — output do projetor
│   │   │   ├── MesProjecao.cs               # NOVO
│   │   │   └── SaldoBancoMes.cs             # NOVO
│   │   └── Simulacao/                       # NOVO MÓDULO
│   │       ├── CenarioSimulacao.cs          # aggregate root
│   │       ├── SimulacaoContratacao.cs      # child entity
│   │       ├── StatusCenarioSimulacao.cs    # enum
│   │       ├── TipoTaxaSimulacao.cs         # enum (Fixa | CdiSpread)
│   │       ├── ICenarioSimulacaoRepository.cs
│   │       └── Validacoes/
│   │           └── SimulacaoContratacaoSpec.cs  # validações invariantes
│   │
│   ├── Sgcf.Application/
│   │   ├── Painel/
│   │   │   ├── Queries/
│   │   │   │   ├── GetSaldoPorBancoAtualQuery.cs           # NOVO
│   │   │   │   ├── GetSaldoPorBancoAtualQueryHandler.cs    # NOVO
│   │   │   │   ├── GetQuadroDividaQuery.cs                 # NOVO
│   │   │   │   └── GetQuadroDividaQueryHandler.cs          # NOVO
│   │   │   ├── SaldoBancoDto.cs                            # NOVO
│   │   │   ├── QuadroDividaDto.cs                          # NOVO
│   │   │   ├── MesQuadroDividaDto.cs                       # NOVO
│   │   │   ├── SaldoBancoMesDto.cs                         # NOVO
│   │   │   ├── CenarioAplicadoDto.cs                       # NOVO
│   │   │   └── SumarioMensalDto.cs                         # NOVO
│   │   └── Simulacao/                                       # NOVO MÓDULO
│   │       ├── Commands/
│   │       │   ├── CriarCenarioSimulacao{Command,Handler,Validator}.cs
│   │       │   ├── AtualizarCenario{Command,Handler,Validator}.cs
│   │       │   ├── AtivarCenario{Command,Handler}.cs
│   │       │   ├── ArquivarCenario{Command,Handler}.cs
│   │       │   ├── DeletarCenario{Command,Handler}.cs
│   │       │   ├── AdicionarSimulacao{Command,Handler,Validator}.cs
│   │       │   └── RemoverSimulacao{Command,Handler}.cs
│   │       ├── Queries/
│   │       │   ├── GetCenarioSimulacaoByIdQuery{,Handler}.cs
│   │       │   ├── ListCenariosSimulacaoQuery{,Handler}.cs
│   │       │   ├── SimularCronogramaHipoteticoQuery{,Handler}.cs
│   │       │   └── CompararCenariosQuery{,Handler}.cs
│   │       ├── Dtos/
│   │       │   ├── CenarioSimulacaoDto.cs
│   │       │   ├── SimulacaoContratacaoDto.cs
│   │       │   ├── CronogramaHipoteticoDto.cs
│   │       │   ├── EventoCronogramaHipoteticoDto.cs
│   │       │   ├── ResultadoComparacaoCenariosDto.cs
│   │       │   └── AlertaSimulacaoDto.cs
│   │       └── SimulacaoCronogramaCalculator.cs            # bridge para motor existente
│   │
│   ├── Sgcf.Infrastructure/
│   │   ├── Migrations/
│   │   │   └── S8_SimulacaoContratacao.cs                  # NOVA
│   │   └── Persistence/
│   │       ├── Configurations/
│   │       │   ├── CenarioSimulacaoConfiguration.cs        # NOVO
│   │       │   └── SimulacaoContratacaoConfiguration.cs    # NOVO
│   │       └── Repositories/
│   │           └── CenarioSimulacaoRepository.cs           # NOVO
│   │
│   ├── Sgcf.Api/
│   │   └── Controllers/
│   │       ├── PainelController.cs                          # add `GET /quadro-divida`
│   │       └── SimulacoesController.cs                      # NOVO
│   │
│   └── Sgcf.Mcp/
│       └── Tools/
│           ├── GetQuadroDividaTool.cs                       # NOVO
│           ├── ListCenariosSimulacaoTool.cs                 # NOVO
│           └── GetCenarioSimulacaoTool.cs                   # NOVO
│
├── tests/
│   ├── Sgcf.Domain.Tests/
│   │   ├── Painel/
│   │   │   ├── ProjetorSaldoMensalTests.cs                 # NOVO
│   │   │   └── ProjetorSaldoMensalProperties.cs            # NOVO (FsCheck)
│   │   └── Simulacao/                                       # NOVO
│   │       ├── CenarioSimulacaoTests.cs
│   │       └── SimulacaoContratacaoTests.cs
│   ├── Sgcf.Application.Tests/
│   │   ├── Painel/
│   │   │   ├── GetSaldoPorBancoAtualQueryHandlerTests.cs   # NOVO
│   │   │   └── GetQuadroDividaQueryHandlerTests.cs         # NOVO
│   │   └── Simulacao/                                       # NOVO
│   │       ├── SimulacaoCronogramaCalculatorTests.cs
│   │       └── *CommandHandlerTests.cs (8 arquivos)
│   ├── Sgcf.Api.IntegrationTests/
│   │   ├── Painel/
│   │   │   └── QuadroDividaApiTests.cs                     # NOVO
│   │   └── Simulacao/                                       # NOVO
│   │       ├── SimulacaoApiFixture.cs
│   │       ├── CenarioSimulacaoApiTests.cs
│   │       ├── CenarioQuadroDividaApiTests.cs
│   │       └── CompararCenariosApiTests.cs
│   ├── Sgcf.GoldenDataset/
│   │   ├── data/quadro-divida-2026/                        # NOVO
│   │   │   ├── input.json
│   │   │   ├── output_esperado.json
│   │   │   └── README.md
│   │   └── QuadroDivida2026GoldenTest.cs                   # NOVO
│   └── Sgcf.Mcp.Tests/
│       └── Simulacao/                                       # NOVO
│
└── docs/
    ├── api/
    │   ├── painel.md                                        # add seção Quadro da Dívida
    │   ├── simulacoes.md                                    # NOVO
    │   ├── schemas.md                                       # add novos DTOs
    │   └── collections/sgcf-api/
    │       ├── 07-Painel/6 - Quadro da Divida.bru           # NOVO
    │       └── 13-Simulacoes/                                # NOVO (10+ requests)
    ├── specs/simulacoes/
    │   └── SPEC.md                                          # ESTE DOCUMENTO
    ├── mcp/tools/
    │   ├── get_quadro_divida.json                           # NOVO
    │   ├── list_cenarios_simulacao.json                     # NOVO
    │   └── get_cenario_simulacao.json                       # NOVO
    └── changelog/CHANGELOG.md                               # add v0.7.0
```

---

## 5. Glossário

| Termo | Definição |
|---|---|
| **Quadro da Dívida** | Visão consolidada multi-banco, mês a mês, de saldo, captações e liquidações para um ano civil. Reproduz a aba `Quadro_da_Divida` da planilha. |
| **Cenário de Simulação** | Agregado nomeado contendo N simulações de contratação. Ciclo de vida Rascunho/Ativo/Arquivado. |
| **Simulação de Contratação** | Captação hipotética futura: banco, modalidade, valor, prazo, taxa, estrutura. **Não é contrato real**. |
| **Cronograma Hipotético** | Eventos gerados on-the-fly a partir de uma simulação, **sem persistência**. |
| **EventoProjecao** | Unidade comum no projetor: `AmortizacaoPrincipal` (de contrato real ou simulado) ou `Captacao` (apenas de simulação). |
| **Saldo de Abertura (mês N)** | Saldo no primeiro dia do mês N. Igual ao saldo de fechamento do mês N-1. Para mês 1, igual ao snapshot atual. |
| **Saldo de Fechamento (mês N)** | `SaldoAbertura − Σ Amortizações[mês N] + Σ Captações[mês N]`. |
| **Share** | Percentual do banco no saldo total de fechamento do mês. Soma = 100% por mês com tolerância 0,01 pp. |
| **Variação** | `(SaldoFechamento[N] − SaldoFechamento[N-1]) / SaldoFechamento[N-1]`. Para mês 1, vs. snapshot atual. |
| **Cenário aplicado** | Cenário com status `Ativo` ou `Arquivado` que foi passado via `cenarioId` na consulta do Quadro. |
| **Taxa Fixa** | `TipoTaxa = Fixa`. Campo `TaxaAa` armazena a taxa nominal anual diretamente. |
| **CDI+Spread** | `TipoTaxa = CdiSpread`. Campos `SpreadAa` (componente fixo). CDI base aplicado em consulta vem do snapshot mais recente (`CdiSnapshot.GetMaisRecenteAsync`). |

---

## 6. Modelo de Domínio

### 6.1 Diagrama lógico

```
┌────────────────────────────────────────────────────────┐
│                  CenarioSimulacao                       │
│  (aggregate root — implements ITenantScoped)            │
│  - Id                                                   │
│  - TenantId (preenchido pelo TenantSaveInterceptor)     │
│  - Nome  (ex: "Realista 2026", "Otimista Q3")          │
│  - Descricao? (texto livre)                            │
│  - AnoBase (2026)                                       │
│  - Status (Rascunho | Ativo | Arquivado)                │
│  - CriadoPor (sub do JWT)                              │
│  - CreatedAt, UpdatedAt, DeletedAt?                     │
└─────────────────┬──────────────────────────────────────┘
                  │
                  │ 0..*
                  ▼
┌────────────────────────────────────────────────────────┐
│              SimulacaoContratacao                       │
│  (implements ITenantScoped — tenant_id herdado via FK) │
│  - Id                                                   │
│  - TenantId (preenchido pelo TenantSaveInterceptor)     │
│  - CenarioId (FK)                                       │
│  - BancoId (FK → banco; RESTRICT)                       │
│  - Modalidade (FINIMP, BalcaoCaixa, NCE, ...)           │
│  - Moeda (BRL, USD, EUR, JPY, CNY)                      │
│  - ValorPrincipal (Money)                              │
│  - DataContratacaoPrevista (LocalDate)                  │
│  - DataPrimeiroVencimento (LocalDate)                   │
│  - TipoTaxa (Fixa | CdiSpread)                          │
│  - TaxaAa? (Percentual) — preenchido se Fixa            │
│  - SpreadAa? (Percentual) — preenchido se CdiSpread     │
│  - BaseCalculo (Dias252 | Dias360 | Dias365)            │
│  - EstruturaAmortizacao (Bullet | Sac | Price | ...)    │
│  - Periodicidade (Mensal | Trimestral | Bullet | ...)   │
│  - QuantidadeParcelas                                   │
│  - AnchorDiaMes (Fixo | UltimoDiaUtil)                  │
│  - AnchorDiaFixo? (1..31)                               │
│  - Observacoes?                                         │
│  - CreatedAt, UpdatedAt                                 │
└────────────────────────────────────────────────────────┘
```

### 6.2 Ciclo de vida do `CenarioSimulacao`

```
                  AdicionarSimulacao
                  AtualizarCampos
                       │
                       ▼
                 ┌──────────┐
   Criar  ────→  │ Rascunho │
                 └────┬─────┘
                      │ Ativar()   (exige >= 1 simulação)
                      ▼
                 ┌──────────┐
                 │  Ativo   │  ← AdicionarSimulacao
                 └────┬─────┘    RemoverSimulacao
                      │ Arquivar()
                      ▼
                 ┌────────────┐
                 │ Arquivado  │   (imutável; pode consultar)
                 └────────────┘
```

**Regras:**
- `Rascunho` aceita todas as operações de edição
- `Ativo` ainda aceita `AdicionarSimulacao`/`RemoverSimulacao` (cenários ativos vivem com a tesouraria; podem refinar)
- `Arquivado` é **imutável**. Qualquer operação que mude estado lança `InvalidOperationException` → HTTP 409
- `Deletar` (soft delete) permitido em qualquer status; oculta da lista mas mantém auditoria

### 6.3 Invariantes do `SimulacaoContratacao`

| # | Invariante |
|---|------------|
| I-1 | `ValorPrincipal > 0` na moeda informada |
| I-2 | `DataContratacaoPrevista >= clock.Today()` (não cria simulação no passado) |
| I-3 | `DataPrimeiroVencimento > DataContratacaoPrevista` |
| I-4 | `DataContratacaoPrevista` está dentro de `[CenarioAnoBase-01-01, CenarioAnoBase-12-31]` |
| I-5 | `QuantidadeParcelas >= 1` |
| I-6 | Se `TipoTaxa = Fixa`: `TaxaAa.HasValue == true` E `SpreadAa.HasValue == false` |
| I-7 | Se `TipoTaxa = CdiSpread`: `SpreadAa.HasValue == true` E `TaxaAa.HasValue == false`; `Moeda == BRL` (CDI só faz sentido em BRL) |
| I-8 | `Moeda` consistente com `Modalidade` (FINIMP/Lei4131 não aceitam BRL; NCE/BalcaoCaixa/FGI/CapitalDeGiro só BRL) |
| I-9 | `BancoId` aponta para banco existente (validado na Application antes de invocar `AdicionarSimulacao`) |
| I-10 | `EstruturaAmortizacao` e `Periodicidade` formam combinação válida (consistência com `CronogramaStrategyFactory`) |
| I-11 | `GarantiaExigidaPrevista : string?` — campo livre opcional (D-9). Quando informado, máx. 500 caracteres. **Informativo apenas** — sem validação contra `LimiteBanco.GarantiasExigidas` (v0.6.0). |

### 6.4 Função pura `ProjetorSaldoMensal`

**Assinatura:**

```csharp
public static class ProjetorSaldoMensal
{
    public static QuadroDividaProjecao Projetar(
        IReadOnlyDictionary<Guid, Money> saldoInicialPorBanco,
        IReadOnlyList<EventoProjecao> eventos,
        int ano);
}

public sealed record EventoProjecao(
    Guid BancoId,
    LocalDate Data,
    TipoEventoProjecao Tipo,
    Money ValorBrl);

public enum TipoEventoProjecao
{
    AmortizacaoPrincipal = 1,
    Captacao = 2
}
```

**Algoritmo:**

```
para cada mes m em 1..12:
    se m == 1:
        saldoInicio[m, banco] = saldoInicialPorBanco[banco]
    senão:
        saldoInicio[m, banco] = saldoFim[m-1, banco]

    eventos_mes = eventos.Where(e => e.Data.Year == ano && e.Data.Month == m)

    totalAmort[m, banco] = soma(eventos_mes.Where(Tipo=AmortizacaoPrincipal).ValorBrl por banco)
    totalCaptacao[m, banco] = soma(eventos_mes.Where(Tipo=Captacao).ValorBrl por banco)

    saldoFim[m, banco] = saldoInicio[m, banco] - totalAmort[m, banco] + totalCaptacao[m, banco]

    totalGeralFim[m] = soma(saldoFim[m, *])
    share[m, banco] = saldoFim[m, banco] / totalGeralFim[m] se totalGeralFim > 0, senão 0
    variacao[m] = (totalGeralFim[m] - totalGeralFim[m-1]) / totalGeralFim[m-1] se m > 1
                  senão (totalGeralFim[1] - sum(saldoInicialPorBanco)) / sum(saldoInicialPorBanco)
```

**Propriedades garantidas:**

- P-1: `saldoFim[m] == saldoInicio[m+1]` por banco
- P-2: `totalGeralFim[m] == sum(saldoFim[m, *])`
- P-3: `abs(sum(share[m, *]) - 1.00) < 0.0001` quando `totalGeralFim[m] > 0`
- P-4: Bancos sem saldo inicial mas com captação aparecem a partir do mês da primeira captação
- P-5: Eventos com `Data.Year != ano` são ignorados (não erro)
- P-6: Bancos sem nenhum evento e sem saldo inicial não aparecem no resultado

### 6.5 `SimulacaoCronogramaCalculator`

Bridge entre `SimulacaoContratacao` e o motor `Sgcf.Domain.Cronograma`:

```csharp
public static class SimulacaoCronogramaCalculator
{
    public static IReadOnlyList<EventoCronogramaGerado> Gerar(
        SimulacaoContratacao simulacao,
        decimal? cdiAnualPctVigente);  // necessário se TipoTaxa = CdiSpread
}
```

**Lógica:**
1. Resolve taxa efetiva: `TipoTaxa = Fixa` → `simulacao.TaxaAa`; `CdiSpread` → `cdiAnualPctVigente + simulacao.SpreadAa`
2. Monta `GerarCronogramaInput` com todos os campos correspondentes
3. Invoca `CronogramaStrategyFactory.Criar(estrutura).Gerar(input)`
4. Retorna eventos sem persistir

**Erro:**
- `CdiSpread` + `cdiAnualPctVigente == null` → `InvalidOperationException("CDI vigente é obrigatório para simulação CDI-indexada")`

---

## 7. API REST

### 7.1 Convenções herdadas

- Base path: `/api/v1`
- Erro padronizado: RFC 7807 (Problem Details)
- Idempotência via header `Idempotency-Key` em todo POST que cria recurso
- Audit log com `source: "rest"`

### 7.2 Endpoint: Quadro da Dívida

```
GET /api/v1/painel/quadro-divida
Autorização: Leitura (qualquer role autenticada)
```

**Query Parameters:**

| Parâmetro | Tipo | Obrigatório | Descrição |
|-----------|------|-------------|-----------|
| `ano` | int | **Sim** | Ano de referência (2020–2100) |
| `cenarioId` | guid | Não | Cenário de simulação a aplicar |
| `bancoId` | guid | Não | Filtra todos os blocos para um banco específico |
| `modalidade` | string | Não | Filtra contratos (não simulações) por modalidade |

**Response 200 OK:** `QuadroDividaDto`

```json
{
  "ano": 2026,
  "dataHoraCalculo": "2026-05-18T14:30:00Z",
  "tipoCotacao": "PTAX_D1_FALLBACK",
  "cenarioAplicado": {
    "id": "01927e21-cc10-2f79-c0b2-c148ad8fef9d",
    "nome": "Realista 2026",
    "status": "Ativo",
    "quantidadeSimulacoes": 5
  },
  "snapshotAtual": {
    "saldoTotalBrl": 54731885.00,
    "breakdownPorBanco": [
      { "bancoId": "...", "bancoApelido": "ITAÚ", "bancoCodigoCompe": "341", "saldoBrl": 12440913.00, "quantidadeContratos": 8 },
      { "bancoId": "...", "bancoApelido": "BB", "bancoCodigoCompe": "001", "saldoBrl": 12969702.00, "quantidadeContratos": 12 }
    ]
  },
  "meses": [
    {
      "mesNumero": 1,
      "mesNome": "Janeiro",
      "saldoInicialTotalBrl": 54731885.00,
      "totalAmortizacaoBrl": 3546412.00,
      "totalCaptacaoBrl": 4605137.00,
      "saldoFinalTotalBrl": 55790610.00,
      "variacaoPctVsMesAnterior": 0.0193,
      "bancos": [
        {
          "bancoId": "...",
          "bancoApelido": "ITAÚ",
          "saldoInicialBrl": 12440913.00,
          "totalAmortizacaoBrl": 0.00,
          "totalCaptacaoBrl": 898534.00,
          "saldoFinalBrl": 13339447.00,
          "sharePct": 0.2390
        }
      ]
    }
  ],
  "sumario": [
    { "mesNumero": 1, "mesReferencia": "2026-01-25", "saldoInicialBrl": 54731885.00, "totalCaptacaoBrl": 4605137.00, "totalLiquidacaoBrl": 3546412.00, "saldoFinalBrl": 55790610.00 }
  ],
  "alertas": []
}
```

**Erros:**
- `400` — `ano` ausente ou fora do range
- `404` — `cenarioId` inexistente
- `422` — `cenario.AnoBase != ano` (semântico — incompatibilidade)

### 7.3 Endpoint: Atalho Cenário → Quadro

```
GET /api/v1/simulacoes/cenarios/{id}/quadro-divida[?ano=YYYY]
Autorização: Leitura
```

Equivalente a `GET /painel/quadro-divida?ano={cenario.AnoBase}&cenarioId={id}`. Se `?ano` for informado, override do `AnoBase`.

### 7.4 Endpoints: CRUD de Cenário

| Verb | Path | Policy | Status | Descrição |
|------|------|--------|--------|-----------|
| POST | `/api/v1/simulacoes/cenarios` | Tesouraria | 201 | Cria cenário em `Rascunho` |
| GET | `/api/v1/simulacoes/cenarios` | Leitura | 200 | Lista com filtros opcionais |
| GET | `/api/v1/simulacoes/cenarios/{id}` | Leitura | 200 | Detalhe com todas as simulações |
| PATCH | `/api/v1/simulacoes/cenarios/{id}` | Tesouraria | 200 | Renomeia / atualiza descricao / anoBase (só em Rascunho) |
| POST | `/api/v1/simulacoes/cenarios/{id}/ativar` | Tesouraria | 200 | Transição Rascunho → Ativo |
| POST | `/api/v1/simulacoes/cenarios/{id}/arquivar` | Gerente | 200 | Transição Ativo → Arquivado |
| POST | `/api/v1/simulacoes/cenarios/{id}/duplicar` | Tesouraria | 201 | **D-10**: cria cópia profunda em `Rascunho` (nome sufixado `" (cópia)"`); todas simulações filhas duplicadas com novos Ids |
| DELETE | `/api/v1/simulacoes/cenarios/{id}` | Tesouraria | 204 | Soft delete |

**POST `/cenarios` body:**

```json
{
  "nome": "Realista 2026",
  "descricao": "Captações conservadoras Q1-Q4",
  "anoBase": 2026
}
```

**GET `/cenarios` query params:**

| Parâmetro | Tipo | Descrição |
|-----------|------|-----------|
| `status` | string | `Rascunho` \| `Ativo` \| `Arquivado` (ou múltiplo CSV) |
| `criadoPor` | string | Filtra por owner |
| `anoBase` | int | Filtra por ano-base |

### 7.5 Endpoints: Simulações dentro do Cenário

| Verb | Path | Policy |
|------|------|--------|
| POST | `/api/v1/simulacoes/cenarios/{id}/simulacoes` | Tesouraria |
| DELETE | `/api/v1/simulacoes/cenarios/{id}/simulacoes/{simId}` | Tesouraria |

**POST `/cenarios/{id}/simulacoes` body:**

```json
{
  "bancoId": "...",
  "modalidade": "BalcaoCaixa",
  "moeda": "Brl",
  "valorPrincipalBrl": 5000000.00,
  "dataContratacaoPrevista": "2026-07-15",
  "dataPrimeiroVencimento": "2026-08-15",
  "tipoTaxa": "CdiSpread",
  "taxaAa": null,
  "spreadAa": 3.17,
  "baseCalculo": "Dias252",
  "estruturaAmortizacao": "Bullet",
  "periodicidade": "Mensal",
  "quantidadeParcelas": 36,
  "anchorDiaMes": "Fixo",
  "anchorDiaFixo": 15,
  "garantiaExigidaPrevista": "CDB cativo 20% + Aval",
  "observacoes": "Linha pré-aprovada negociada com gerente"
}
```

**Response 201 Created:** `SimulacaoContratacaoDto` + `alertas[]` (se ultrapassa limite).

### 7.6 Endpoint: Preview de Cronograma

```
POST /api/v1/simulacoes/cronograma-hipotetico
Autorização: Leitura
```

Body idêntico ao `POST /cenarios/{id}/simulacoes` (sem `cenarioId`).

Response 200: array de eventos gerados pelo motor.

```json
{
  "cdiVigentePct": 14.40,
  "taxaEfetivaAa": 17.57,
  "eventos": [
    { "numeroEvento": 1, "data": "2026-08-15", "tipo": "Juros", "valorMoedaOriginal": 73239.04, "moeda": "Brl" },
    { "numeroEvento": 2, "data": "2026-09-15", "tipo": "Juros", "valorMoedaOriginal": 73239.04, "moeda": "Brl" },
    { "numeroEvento": 36, "data": "2029-07-15", "tipo": "Principal", "valorMoedaOriginal": 5000000.00, "moeda": "Brl" }
  ]
}
```

### 7.7 Endpoint: Comparar Cenários

```
POST /api/v1/simulacoes/comparar
Autorização: Executivo
```

Body:

```json
{
  "ano": 2026,
  "cenarioIds": ["id1", "id2", "id3"]
}
```

Response: `ResultadoComparacaoCenariosDto` com cada quadro projetado + deltas mensais vs. o primeiro (baseline).

**Validações:**
- 2 ≤ `cenarioIds.Count` ≤ 5
- Todos os cenários devem ter mesmo `AnoBase` igual a `ano`; caso contrário 422

---

## 8. Storage / Schema

### 8.1 Migration `S8_SimulacaoContratacao`

> **Multi-tenant:** ambas as tabelas implementam `ITenantScoped`. `tenant_id UUID NOT NULL`
> é obrigatório. O `TenantSaveInterceptor` preenche automaticamente no momento do INSERT.
> RLS policies devem ser criadas na mesma migration.

```sql
CREATE TABLE cenario_simulacao (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL,                           -- multi-tenant: isolação por tenant
    nome TEXT NOT NULL,
    descricao TEXT,
    ano_base INTEGER NOT NULL CHECK (ano_base BETWEEN 2020 AND 2100),
    status INTEGER NOT NULL,                           -- 1=Rascunho, 2=Ativo, 3=Arquivado
    criado_por TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL,
    deleted_at TIMESTAMPTZ,
    CONSTRAINT chk_status_cenario CHECK (status IN (1, 2, 3))
);

CREATE INDEX idx_cenario_tenant_status_owner
    ON cenario_simulacao (tenant_id, status, criado_por)
    WHERE deleted_at IS NULL;

CREATE INDEX idx_cenario_tenant_ano_base
    ON cenario_simulacao (tenant_id, ano_base)
    WHERE deleted_at IS NULL;

-- RLS: isolação na segunda camada (primeira é o EF global filter)
ALTER TABLE cenario_simulacao ENABLE ROW LEVEL SECURITY;
CREATE POLICY cenario_simulacao_tenant_isolation ON cenario_simulacao
    USING (tenant_id = current_setting('app.tenant_id', true)::uuid);

CREATE TABLE simulacao_contratacao (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL,                           -- multi-tenant: deve coincidir com cenario_simulacao.tenant_id
    cenario_id UUID NOT NULL REFERENCES cenario_simulacao(id) ON DELETE CASCADE,
    banco_id UUID NOT NULL REFERENCES banco(id) ON DELETE RESTRICT,
    modalidade INTEGER NOT NULL,
    moeda INTEGER NOT NULL,
    valor_principal NUMERIC(20,6) NOT NULL CHECK (valor_principal > 0),
    valor_principal_moeda INTEGER NOT NULL,
    data_contratacao_prevista DATE NOT NULL,
    data_primeiro_vencimento DATE NOT NULL,
    tipo_taxa INTEGER NOT NULL,                        -- 1=Fixa, 2=CdiSpread
    taxa_aa NUMERIC(9,6),
    spread_aa NUMERIC(9,6),
    base_calculo INTEGER NOT NULL,
    estrutura_amortizacao INTEGER NOT NULL,
    periodicidade INTEGER NOT NULL,
    quantidade_parcelas INTEGER NOT NULL CHECK (quantidade_parcelas >= 1),
    anchor_dia_mes INTEGER NOT NULL,
    anchor_dia_fixo INTEGER CHECK (anchor_dia_fixo IS NULL OR anchor_dia_fixo BETWEEN 1 AND 31),
    observacoes TEXT,
    created_at TIMESTAMPTZ NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL,
    CONSTRAINT chk_tipo_taxa_consistente CHECK (
        (tipo_taxa = 1 AND taxa_aa IS NOT NULL AND spread_aa IS NULL) OR
        (tipo_taxa = 2 AND spread_aa IS NOT NULL AND taxa_aa IS NULL)
    ),
    CONSTRAINT chk_datas CHECK (data_primeiro_vencimento > data_contratacao_prevista)
);

CREATE INDEX idx_simulacao_tenant_cenario ON simulacao_contratacao (tenant_id, cenario_id);
CREATE INDEX idx_simulacao_banco ON simulacao_contratacao (banco_id);

ALTER TABLE simulacao_contratacao ENABLE ROW LEVEL SECURITY;
CREATE POLICY simulacao_contratacao_tenant_isolation ON simulacao_contratacao
    USING (tenant_id = current_setting('app.tenant_id', true)::uuid);
```

### 8.2 Soft delete query filter

`SgcfDbContext.OnModelCreating`:

```csharp
// O EF global filter combina isolação de tenant com soft-delete.
// Padrão: tenantContext.IsResolved && e.TenantId == tenantContext.TenantIdOrDefault && e.DeletedAt == null
// Implementado via ParameterReplacer (ExpressionVisitor) em SgcfDbContext — não adicionar manualmente.
modelBuilder.Entity<CenarioSimulacao>()
    .HasQueryFilter(c => c.DeletedAt == null); // tenant filter combinado automaticamente pelo contexto
```

> **Importante:** Não adicione `WHERE tenant_id = ...` manualmente. O EF global filter
> combina a expressão de soft-delete com o filtro de tenant através do `ParameterReplacer`.
> Contexto não resolvido (`IsResolved=false`) retorna zero linhas — comportamento intencional.

### 8.3 Audit interceptor

Captura automaticamente via `AuditInterceptor` existente (já configurado). Cada mutação grava em `audit_log` com `source: "rest"|"mcp"|"a2a"` conforme contexto.

---

## 9. RBAC

> Extensão da matriz RBAC do SPEC raiz §11.

| Operação | tesouraria | contabilidade | gerente | diretor | auditor | admin |
|----------|:-:|:-:|:-:|:-:|:-:|:-:|
| Consultar Quadro da Dívida | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Listar/consultar cenários | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Criar cenário | ✅ | ❌ | ❌ | ❌ | ❌ | ✅ |
| Atualizar cenário (Rascunho) | ✅ | ❌ | ❌ | ❌ | ❌ | ✅ |
| Ativar cenário | ✅ | ❌ | ❌ | ❌ | ❌ | ✅ |
| Adicionar/remover simulação | ✅ | ❌ | ❌ | ❌ | ❌ | ✅ |
| Arquivar cenário | ❌ | ❌ | ✅ | ✅ | ❌ | ✅ |
| Deletar cenário | ✅ | ❌ | ✅ | ✅ | ❌ | ✅ |
| Preview cronograma hipotético | ✅ | ❌ | ✅ | ✅ | ❌ | ✅ |
| Comparar cenários | ❌ | ❌ | ✅ | ✅ | ❌ | ✅ |
| MCP — tools read-only | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |

**Policies novas em `Sgcf.Application.Authorization.Policies`:**
- `SimulacaoEditor` — papel tesouraria + admin (alias semântico)
- Reusa `Gerente`, `Executivo`, `Leitura` existentes

---

## 10. Code Style

### 10.1 Exemplo concreto — função pura de projeção

```csharp
// src/Sgcf.Domain/Painel/ProjetorSaldoMensal.cs

namespace Sgcf.Domain.Painel;

/// <summary>
/// Projeta o saldo mensal por banco a partir de uma posição inicial e
/// uma lista de eventos (amortizações + captações).
///
/// Função pura: mesma entrada → mesma saída. Sem I/O, sem clock, sem state.
/// Invariantes garantidas:
///   - saldoFim[m] == saldoInicio[m+1] por banco
///   - sum(share[m, *]) == 100% (tolerância 0,01 pp)
///   - bancos sem saldo inicial e sem eventos não aparecem no resultado
/// </summary>
public static class ProjetorSaldoMensal
{
    public static QuadroDividaProjecao Projetar(
        IReadOnlyDictionary<Guid, Money> saldoInicialPorBanco,
        IReadOnlyList<EventoProjecao> eventos,
        int ano)
    {
        ArgumentNullException.ThrowIfNull(saldoInicialPorBanco);
        ArgumentNullException.ThrowIfNull(eventos);
        if (ano < 2020 || ano > 2100)
            throw new ArgumentOutOfRangeException(nameof(ano));

        IReadOnlyList<EventoProjecao> eventosFiltrados = eventos
            .Where(e => e.Data.Year == ano)
            .ToList()
            .AsReadOnly();

        HashSet<Guid> bancosEnvolvidos = saldoInicialPorBanco.Keys
            .Concat(eventosFiltrados.Select(e => e.BancoId))
            .ToHashSet();

        var meses = new List<MesProjecao>(12);
        var saldoBanco = new Dictionary<Guid, decimal>(
            saldoInicialPorBanco.ToDictionary(kv => kv.Key, kv => kv.Value.Valor));

        decimal saldoTotalAbertura = Math.Round(
            saldoBanco.Values.Sum(),
            6,
            MidpointRounding.AwayFromZero);

        for (int m = 1; m <= 12; m++)
        {
            // ... projeta mês m
        }

        return new QuadroDividaProjecao(ano, meses.AsReadOnly(), saldoTotalAbertura);
    }
}
```

### 10.2 Convenções

| Item | Convenção | Exemplo |
|------|-----------|---------|
| Agregado raiz | PascalCase + Entity base | `CenarioSimulacao` |
| Enum domain | PascalCase singular | `TipoTaxaSimulacao.Fixa` |
| Repository | Interface no Domain; implementação na Infrastructure | `ICenarioSimulacaoRepository` |
| Command/Query | sufixo `Command`/`Query`; handler `*Handler` | `CriarCenarioSimulacaoCommand` + `CriarCenarioSimulacaoCommandHandler` |
| DTO | sufixo `Dto`; record imutável | `CenarioSimulacaoDto` |
| Endpoint REST | substantivo plural + verbo HTTP | `POST /simulacoes/cenarios` |
| Tool MCP | `verbo_substantivo` snake_case | `get_quadro_divida` |
| Migration | `S{n}_DescricaoCurta` | `S8_SimulacaoContratacao` |

### 10.3 Padrões herdados (não-negociáveis do projeto)

- ❌ `decimal` cru para dinheiro → use `Money`
- ❌ `DateTime.Now` em domain → use `IClock` (NodaTime)
- ❌ Comparação de moeda por string → use `enum Moeda`
- ❌ Lógica de negócio em controllers → use MediatR handlers
- ✅ Pure functions em `Sgcf.Domain.*` (sem I/O)
- ✅ `MidpointRounding.AwayFromZero` em todo arredondamento financeiro
- ✅ Audit log com `source` correto
- ✅ Idempotência via header em POST que cria recurso

---

## 11. Testing Strategy

### 11.1 Pirâmide

```
       ┌────────────────────┐
       │   E2E API (~20)    │   WebApplicationFactory + Testcontainers
       └────────────────────┘
     ┌────────────────────────┐
     │  Integração (~30)      │   Application handlers + Postgres
     └────────────────────────┘
  ┌──────────────────────────────┐
  │  Unidade (~120)              │   Domain puro, sem I/O
  └──────────────────────────────┘
```

### 11.2 Coverage targets (gate CI)

| Camada | Mínimo | Bloqueia merge |
|---|---|---|
| `Sgcf.Domain.Painel.ProjetorSaldoMensal` | 95% | Sim |
| `Sgcf.Domain.Simulacao.*` | 90% | Sim |
| `Sgcf.Application.Simulacao.*` | 85% | Sim |
| `Sgcf.Application.Painel.Queries.GetQuadroDividaQueryHandler` | 90% | Sim |
| `Sgcf.Api.Controllers.SimulacoesController` | 70% | Não (warn) |

### 11.3 Tipos de teste obrigatórios

| Tipo | Onde | Cobertura |
|------|------|-----------|
| Unit puro | `Sgcf.Domain.Tests/Painel/`, `Sgcf.Domain.Tests/Simulacao/` | Cenários típicos + edge cases |
| Property-based (FsCheck) | `Sgcf.Domain.Tests/Painel/ProjetorSaldoMensalProperties.cs` | Invariantes P-1..P-6 |
| Golden | `Sgcf.GoldenDataset/data/quadro-divida-2026/` | Reproduz janeiro/2026 da planilha; tolerância R$ 1,00 |
| Integração (handler) | `Sgcf.Application.Tests/Simulacao/` | Cada command/query 1 caminho feliz + erros principais |
| Integração (API) | `Sgcf.Api.IntegrationTests/Simulacao/` | Fluxo end-to-end: criar → adicionar 3 simulações → ativar → quadro → arquivar |
| MCP | `Sgcf.Mcp.Tests/Simulacao/` | Cada tool invocada com payload válido |

### 11.4 Golden test crítico: cronograma cruzado

Garante que `SimulacaoCronogramaCalculator` produz cronograma idêntico ao que `CreateContratoCommand` produziria após conversão da simulação em contrato real:

```csharp
[Fact]
[Trait("Category", "Golden")]
public async Task CronogramaSimulacao_IgualA_CronogramaContrato_MesmosInputs()
{
    // Arrange — input idêntico
    var input = NewBalcaoCaixaInput(valor: 5_000_000m, parcelas: 36);

    // Act — gera cronograma via simulação
    var simulacao = SimulacaoContratacao.Criar(/* mesmos campos */);
    var eventosSimulados = SimulacaoCronogramaCalculator.Gerar(simulacao, cdiVigente: 14.40m);

    // Act — gera cronograma via comando real
    var contratoId = await mediator.Send(new CreateContratoCommand(/* mesmos campos */));
    var eventosReais = await db.Eventos.Where(e => e.ContratoId == contratoId).ToListAsync();

    // Assert — exato
    eventosSimulados.Should().BeEquivalentTo(eventosReais, opts => opts
        .Excluding(e => e.Id)
        .Excluding(e => e.ContratoId)
        .Excluding(e => e.CreatedAt));
}
```

### 11.5 Property tests obrigatórios

```csharp
[Property]
public Property SaldoFimMesN_IgualA_SaldoInicioMesNMais1(NonNegativeInt saldoInicial, ...)
{
    // P-1: saldoFim[m] == saldoInicio[m+1] por banco
}

[Property]
public Property SomaDeSharesPorMes_E_100PorCento(List<EventoProjecao> eventos, ...)
{
    // P-3: abs(sum(share[m,*]) - 1.0) < 0.0001
}
```

---

## 12. Boundaries

### 12.1 Always do (em todo PR deste módulo)

- ✅ Usar `Money`, `Percentual`, `LocalDate`, `Instant` (não `decimal`, `double`, `DateTime`)
- ✅ Funções de projeção e cálculo são puras (sem I/O, sem clock injetado, sem state)
- ✅ Cronograma da simulação reusa `CronogramaStrategyFactory` (zero duplicação de lógica)
- ✅ Idempotency-Key em `POST /cenarios`, `POST /cenarios/{id}/simulacoes`, `POST /comparar`
- ✅ Audit log automático via `AuditInterceptor` + `IAuditable`
- ✅ Soft delete via `DeletedAt`; query filter aplicado
- ✅ Property tests para invariantes do `ProjetorSaldoMensal`
- ✅ Golden tests para cronograma cruzado simulação ↔ contrato
- ✅ RFC 7807 em todos os erros
- ✅ DTOs e contratos via `record` imutável

### 12.2 Ask first (exige aprovação humana antes de mudar)

- ⚠️ Persistir cronograma da simulação (decisão atual: on-the-fly; mudar exige re-análise de drift)
- ⚠️ Adicionar campo novo a `Proposta` ou `Cotacao` para integrar com simulações (cotação ≠ simulação)
- ⚠️ Mudar premissa de conversão de moeda (atual: spot/PTAX flat); cenário cambial é módulo separado
- ⚠️ Migrar simulação aceita para contrato real automaticamente (atual: out of scope; conversão é manual)
- ⚠️ Adicionar segunda dimensão de horizonte (atual: 12 meses do ano informado)
- ⚠️ Mudar comportamento de limite ultrapassado (atual: alerta não-bloqueante; D-5 trancada)
- ⚠️ Mudar `tipoTaxa` aceito (atual: `Fixa` ou `CdiSpread`; outros indexadores exigem nova decisão)

### 12.3 Never do

- ❌ Persistir cronograma da simulação em tabela separada
- ❌ Misturar `Cotacao` (proposta real) com `SimulacaoContratacao` (hipotética) na mesma tabela
- ❌ Permitir mutação em cenário `Arquivado`
- ❌ Calcular MTM de hedge na projeção do quadro (escopo `dívida pura` — D-3)
- ❌ Bloquear simulação por limite excedido (D-5: alerta, não bloqueio)
- ❌ Deletar cenário hard (sempre soft delete)
- ❌ Permitir simulação com `DataContratacaoPrevista` no passado (clock injetado valida)
- ❌ Logar valores monetários em logs operacionais — exceto em audit log (consistente com SPEC raiz §10.4)
- ❌ Implementar lógica de cálculo de cronograma fora de `Sgcf.Domain.Cronograma`
- ❌ Tools MCP de escrita para simulação no MVP (apenas read-only)

---

## 13. Success Criteria

> Concrete, testable conditions de "pronto para release v0.7.0".

| # | Critério | Como verificar |
|---|----------|----------------|
| 1 | `GET /painel/quadro-divida?ano=2026` retorna estrutura completa sem cenário em ≤ 300ms p99 com 1.500 contratos | Test E2E com seed + OpenTelemetry log |
| 2 | `GET /painel/quadro-divida?ano=2026&cenarioId={id}` aplica 5 simulações em ≤ 500ms p99 | Idem |
| 3 | Golden test janeiro/2026 (sem cenário) bate com planilha com tolerância R$ 1,00 | `dotnet test --filter "FullyQualifiedName~QuadroDivida2026GoldenTest"` |
| 4 | Cronograma simulação == cronograma contrato real para mesmos inputs (4 estruturas) | 4 golden tests; tolerância 0 (igualdade exata) |
| 5 | Property test soma de shares == 100% por mês passa 1000 iterações | FsCheck `[Property]` |
| 6 | RBAC: token tesouraria cria cenário (201), token contabilidade rejeita (403) | E2E test por role |
| 7 | Soft delete: cenário deletado some da `GET /cenarios` mas é consultável via audit log | E2E test |
| 8 | Cenário arquivado: qualquer mutação retorna 409 com mensagem clara | E2E test |
| 9 | Migration `S8` aplica em dev e staging sem erro | CI deploy step |
| 10 | Bruno collection executa fluxo completo end-to-end | Manual smoke |
| 11 | Cobertura do módulo Simulacao ≥ 85%; Painel ≥ 90% (gate CI) | `dotnet test --collect:"XPlat Code Coverage"` |
| 12 | CHANGELOG v0.7.0 publicado com resumo executivo | Manual verify |

---

## 14. Out of Scope (MVP v0.7.0)

> Documentado para evitar scope creep durante implementação.

- ❌ MTM de hedge na projeção do saldo (decisão D-3)
- ❌ Simulação de antecipação dentro do cenário (mantém `/simulador/antecipacao-portfolio` separado)
- ❌ Múltiplos anos numa mesma chamada (decisão D-2)
- ❌ Persistência do cronograma hipotético
- ❌ Conversão automática de cenário em contratos reais (após decisão "fechei a captação")
- ❌ Notificações ao arquivar cenário (criador não é notificado)
- ❌ Duplicar cenário existente como template
- ❌ Versionamento histórico do conteúdo do cenário (override é destrutivo)
- ❌ Garantias previstas na simulação
- ❌ Indexador diferente de CDI (IPCA, TR, USD-LIBOR, etc.)
- ❌ Tools MCP de escrita (criar/editar cenário via agente)
- ❌ Compartilhamento de cenários entre tenants — cada tenant vê apenas seus próprios cenários, isolados automaticamente pelo EF Core global filter + RLS. Não existe UI nem endpoint para cross-tenant sharing.

---

## 15. Open Questions (pendente decisão humana — não bloqueia implementação)

| # | Pergunta | Default proposto |
|---|----------|------------------|
| Q1 | Cenário tem owner exclusivo (só criador edita) ou qualquer tesouraria edita? | Qualquer tesouraria edita (transparência intra-equipe) |
| Q2 | Cenário pode ter `DataContratacaoPrevista` em dias úteis específicos (D+5, etc.) ou data livre? | Data livre (validação só `>= hoje`) |
| Q3 | Cenário com simulação cuja data caia em fim de semana — ajustar para próximo dia útil? | Não ajustar; data livre; cronograma do motor já aplica convenção |
| Q4 | Alerta de limite considera só `LimiteBanco.ValorUtilizadoBrl` ou também outras simulações ativas no mesmo cenário? | Considera ambos: utilizado real + soma de simulações do cenário |
| Q5 | Quantos cenários ativos simultâneos por owner? | Sem limite no MVP; observar uso |
| Q6 | Quando cenário fica > 90 dias arquivado, auto-soft-delete? | Não — manter indefinidamente para auditoria |
| Q7 | `GET /cenarios` paginado por default? | Não — limite operacional baixo (provavelmente < 20 cenários ativos por owner) |
| Q8 | Field `criadoPor` capturado do `sub` JWT vs claim `email`? | `sub` (consistente com audit log) |
| Q9 | Cenário arquivado aparece em `GET /cenarios` por default? | Não — exige `?status=Arquivado` explícito |
| Q10 | Idempotency-Key em `PATCH` para evitar dupla atualização? | Não — PATCH é idempotente por natureza HTTP |

---

## 16. Referências

| Documento | Conteúdo |
|-----------|----------|
| `SPEC.md` (raiz) | Personas, RBAC, padrões financeiros, NodaTime, Money, princípios de código |
| `tasks/quadro-divida-simulacao/plan.md` | Plano de execução em 14 tasks distribuídas em 4 fases |
| `documentos/Endividamento.xlsx` aba `Quadro_da_Divida` | Fonte visual canônica |
| `docs/specs/cotacoes/SPEC.md` | Padrão de SPEC adotado |
| `docs/api/painel.md` | Endpoints atuais que serão estendidos |
| `docs/api/simulador.md` | Simulador existente (cenário cambial + antecipação portfólio) — coexiste sem conflito |
| `src/Sgcf.Domain/Cronograma/` | Motor reutilizado para gerar cronograma hipotético |

---

## Changelog deste SPEC

- **v1.0 — 2026-05-18** — versão inicial. Consolida plano de execução, decisões trancadas D-1..D-5, modelo de domínio, contratos REST, schema, RBAC e testes. Aguarda aprovação humana.
