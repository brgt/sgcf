# SPEC — SGCF (Sistema de Gestão de Contratos de Financiamento)

**Empresa:** Proxys Comércio Eletrônico
**Sponsor / Product Owner:** Welysson Soares
**Versão:** v1.0 — 08/maio/2026
**Status:** Proposta — aguardando aprovação para entrar em vigor

> Este SPEC é o **documento âncora** do projeto. Consolida a visão e preenche lacunas operacionais que não estão nos documentos de origem. Para detalhes de domínio, sempre consulte os documentos referenciados na seção 20.

---

## 1. Objetivo

### 1.1 O que estamos construindo

Um **backend agent-ready** (REST + MCP + A2A) para gerir 1.200+ contratos de dívida da Proxys (FINIMP, REFINIMP, 4131, NCE, Balcão Caixa, FGI, NDF) substituindo a planilha Excel atual e servindo como **sistema de registro** para uma futura camada de agentes de IA (Comparador, Gravador, Parecerista — Fase 2).

### 1.2 Por que

A planilha atingiu o limite operacional: 3-4 dias de fechamento mensal, erros silenciosos em fórmulas, sem audit trail, sem capacidade de simulação, conhecimento concentrado em 1-2 pessoas. O custo da inação em 24 meses é estimado em R$ 200k–500k em retrabalho, multas e oportunidades perdidas. Ver `plan/Business_Case_Sistema_Contratos.md` §7.

### 1.3 Sucesso (system-level)

O MVP é considerado bem-sucedido quando, **30 dias após go-live**:

| # | Critério | Como medir |
|---|---|---|
| 1 | Tesouraria fecha o mês usando **apenas o SGCF** | Ata do fechamento + planilha está read-only há 30 dias |
| 2 | Saldo total da dívida em BRL no SGCF reconcilia com lançamentos contábeis com divergência **≤ 0,1%** | Relatório de reconciliação aprovado por contabilidade |
| 3 | Tempo de fechamento mensal de dívida cai de 3-4 dias para **≤ 1 dia** | Cronometragem de 2 fechamentos consecutivos |
| 4 | Resposta a pedido de auditoria sobre dívida em **≤ 1 hora** | Simulação de auditoria com sponsor |
| 5 | Cobertura de testes do motor de cálculo financeiro **≥ 95%** | Relatório de coverage do CI |
| 6 | **Zero incidentes P0/P1** nos primeiros 30 dias de produção | Tracker de incidentes |
| 7 | Sponsor consulta posição de dívida via **Claude Desktop (MCP)** em demo formal | Demo gravada |
| 8 | 1.200+ contratos importados com **≥ 99% de match** vs saldo da planilha | Relatório de migração |

---

## 2. Personas e papéis

### 2.1 Personas operacionais

| Persona | Quem | Necessidade primária | Frequência |
|---|---|---|---|
| **Tesouraria — Analista** | Welysson + 1 analista | Cadastrar contratos, gerar tabela completa, baixar pagamentos, simular cenários | Diária |
| **Tesouraria — Gerente Financeiro** | Aprovador de operações | Painéis consolidados, aprovação de novos contratos | Semanal |
| **Diretoria Financeira** | Sponsor executivo | Indicadores executivos (Dívida/EBITDA, Share, Custo Médio) | Mensal / sob demanda |
| **Contabilidade** | Equipe contábil | Lançamentos gerenciais para conciliação manual com SAP B1 | Mensal (fechamento) |
| **Auditoria interna** | Compliance/Auditoria | Audit trail, reprodução de relatórios históricos | Trimestral / sob demanda |
| **Auditoria externa** | Big4 ou independente | Validação de números, valoração de NDFs (IFRS 9) | Anual |
| **TI / Admin** | Dev + ops | Configurações, RBAC, jobs, observabilidade | Sob demanda |

### 2.2 Papéis no sistema (RBAC) — ver §11 para matriz por endpoint

`tesouraria` · `contabilidade` · `gerente` · `diretor` · `auditor` · `admin`

### 2.3 User stories essenciais (top 15 — backlog completo na Fase B.3)

| # | Como | Quero | Para |
|---|---|---|---|
| US-01 | Analista de tesouraria | Cadastrar um contrato de FINIMP com extração polimórfica (FINIMP_DETAIL) | Substituir o cadastro manual na planilha |
| US-02 | Analista de tesouraria | Ver o cronograma gerado automaticamente após cadastro | Validar antes de salvar |
| US-03 | Analista de tesouraria | Consultar a "tabela completa do contrato" (8 blocos) sob demanda | Apresentar a auditoria/diretoria |
| US-04 | Analista de tesouraria | Cadastrar NDF Forward ou Collar vinculado a um contrato | Documentar o hedge |
| US-05 | Analista de tesouraria | Ver MTM atualizado dos NDFs em tempo real | Decidir rolagem/liquidação |
| US-06 | Analista de tesouraria | Baixar uma parcela do cronograma com data efetiva e valor real | Refletir o que foi pago |
| US-07 | Gerente financeiro | Consultar o painel de dívida consolidada multi-moeda com ajuste MTM | Reunião de comitê |
| US-08 | Gerente financeiro | Simular impacto cambial (-10%/+10%) em cenário de stress | Resposta a perguntas da diretoria |
| US-09 | Diretor financeiro | Ver KPIs (Dívida Total, Líquida, Dívida/EBITDA, Custo Médio) | Decisão executiva |
| US-10 | Contabilidade | Baixar lançamentos gerenciais do mês em Excel padronizado | Reconciliar com SAP B1 |
| US-11 | Auditor interno | Reproduzir posição de dívida em uma data passada | Validar fechamento histórico |
| US-12 | Sistema (job) | Ingerir PTAX automaticamente do BCB | Manter cotações atualizadas |
| US-13 | Sistema (job) | Recalcular MTM intraday a cada 5 minutos em horário de mercado | Visão real-time da posição |
| US-14 | Sistema (job) | Disparar alertas D-7/D-3/D-0 de vencimento | Evitar atraso de pagamento |
| US-15 | Agente externo (LLM) | Consultar posição de dívida via MCP (`get_posicao_divida`) | Permitir consulta conversacional via Claude Desktop |
| US-16 | Analista de tesouraria | **Simular o custo de antecipar (total ou parcialmente) um contrato em qualquer data futura** | Decidir entre quitar antecipadamente, manter o contrato, ou refinanciar |
| US-17 | Analista de tesouraria | Comparar a simulação de antecipação com o cenário "sem antecipar + aplicar caixa em CDI" | Tomar decisão financeira com base no resultado líquido projetado |
| US-18 | Gerente financeiro | Ver o histórico de simulações de antecipação por contrato e por banco | Auditar decisões e identificar padrões de negociação |
| US-19 | Analista de tesouraria | Buscar cláusulas em contratos por tema/conceito (ex: "market flex", "cross-default") | Tirar dúvidas textuais sobre o que o contrato diz |
| US-20 | Analista de tesouraria | Comparar a cláusula de pré-pagamento de 2 contratos lado a lado | Validar se um contrato novo é mais ou menos favorável que o histórico |

---

## 3. Tech stack

| Camada | Tecnologia | Versão | Origem |
|---|---|---|---|
| Backend | .NET + ASP.NET Core | **11** (fallback **.NET 10 LTS**) | ADR-003 v1.1 |
| ORM | Entity Framework Core | atual da .NET 11 (ou EF 10 com .NET 10 LTS) | ADR-003 |
| Banco | PostgreSQL (Cloud SQL) | 16 | ADR-003 |
| Cache | Memorystore Redis | 7+ | ADR-003 |
| Auth | OAuth 2.1 + OIDC (Identity Platform GCP) | — | ADR-006 |
| Containers | Cloud Run (managed) | — | ADR-005 |
| Storage | Cloud Storage | — | ADR-005 |
| Filas/Jobs | Cloud Tasks + Cloud Scheduler | — | ADR-003 |
| Secrets | Secret Manager | — | ADR-006 |
| CI/CD | Cloud Build + Cloud Deploy | — | ADR-007 |
| Observabilidade | Cloud Logging + Cloud Monitoring + Cloud Trace + OpenTelemetry | — | ADR-003 |
| **MCP** | SDK MCP (Anthropic oficial em .NET, ou wrapper sobre spec) | spec MCP atual | ADR-012 |
| **A2A** | Spec A2A (Google + parceiros) | última estável | ADR-013 |
| Front-end (consumidor) | **Headless — stack a definir** (decisão Fase B.4) | — | ADR-009 (pendente) |

### 3.1 Bibliotecas .NET adotadas (no MVP)

| Lib | Uso | Por quê |
|---|---|---|
| **MediatR** | Application layer (CQRS leve) | Separação Command/Query, injeção limpa |
| **FluentValidation** | Validação de inputs | Composable, testável, mensagens claras |
| **NodaTime** | Datas/horários financeiros | Timezone-safe, melhor que `DateTime`/`DateOnly` para finanças |
| **Serilog** + sink GCP | Logs estruturados | Padrão do mercado .NET |
| **OpenTelemetry** | Tracing distribuído | Cloud Trace + futura observabilidade |
| **QuestPDF** | Geração de PDF | Fluent API, sem dependência de wkhtmltopdf |
| **ClosedXML** | Geração de Excel | Sem licença comercial (vs EPPlus 5+) |
| **Testcontainers** | Testes de integração | Postgres real em CI, sem mocks frágeis |
| **xUnit + FluentAssertions** | Framework de testes | Padrão .NET maduro |
| **Bogus** | Geração de dados sintéticos para testes | Reduz manutenção de fixtures |
| **FsCheck** ou **CsCheck** | Property-based testing no motor financeiro | ADR-014: pure functions testáveis exaustivamente |

### 3.2 O que NÃO usamos (decisões deliberadas)

- ❌ **AutoMapper** — preferimos mapeamento manual (ADR-014: simple > clever; reduz uso de reflection)
- ❌ **MicroORM (Dapper) puro** — EF Core cobre 95% das necessidades; Dapper só para queries de painel pesadas se necessário
- ❌ **Dependency injection containers de terceiros** — `Microsoft.Extensions.DependencyInjection` basta
- ❌ **Mocks no domínio** — Testcontainers + in-memory (ADR-014)

---

## 4. Comandos do projeto

> Comandos relativos à raiz do repositório `sgcf-backend/` (a ser criado na Sprint 0).

### 4.1 Desenvolvimento local

| Ação | Comando |
|---|---|
| Restaurar dependências | `dotnet restore` |
| Build | `dotnet build --configuration Release` |
| Testes (todos) | `dotnet test --collect:"XPlat Code Coverage"` |
| Testes (motor financeiro) | `dotnet test --filter "Category=Financeiro"` |
| Testes (golden dataset) | `dotnet test --filter "Category=Golden"` |
| Subir ambiente dev (Postgres + Redis local via docker-compose) | `docker compose -f infra/dev/docker-compose.yml up -d` |
| Aplicar migrations | `dotnet ef database update --project src/Sgcf.Infrastructure --startup-project src/Sgcf.Api` |
| Criar migration | `dotnet ef migrations add <Nome> --project src/Sgcf.Infrastructure --startup-project src/Sgcf.Api` |
| Rodar API localmente | `dotnet run --project src/Sgcf.Api` |
| Watch (dev) | `dotnet watch --project src/Sgcf.Api run` |
| Lint / format | `dotnet format` |
| Análise estática | `dotnet build /p:TreatWarningsAsErrors=true` |
| Conectar ao Cloud SQL (dev) | `cloud-sql-proxy --port=5433 PROJECT:REGION:INSTANCE` |
| Limpar cache de build | `dotnet clean && rm -rf src/**/bin src/**/obj` |

### 4.2 CI (Cloud Build) — gates obrigatórios

```yaml
# Pipeline em ordem; cada step bloqueia o próximo se falhar
1. dotnet restore
2. dotnet build --configuration Release /p:TreatWarningsAsErrors=true
3. dotnet test --filter "Category!=Slow" --collect:"XPlat Code Coverage"
4. coverage check: motor financeiro >= 95%, geral >= 80%
5. dotnet format --verify-no-changes
6. complexity check (alerta, não bloqueia): cyclomatic > 10
7. security scan (gcloud artifacts): nenhuma vuln CRITICAL/HIGH
8. container build + push
9. deploy dev (auto)
10. smoke tests em dev (5 endpoints críticos)
11. deploy staging (auto após merge na main)
12. deploy prod (manual gate)
```

### 4.3 Operacional (produção)

| Ação | Comando |
|---|---|
| Ver logs últimas 1h | `gcloud logging read 'resource.type=cloud_run_revision AND resource.labels.service_name=sgcf-api' --freshness=1h` |
| Trigger ingestão PTAX manual | `gcloud scheduler jobs run ptax-ingestion-job --location=southamerica-east1` |
| Restore DB (point-in-time) | `gcloud sql backups restore BACKUP_ID --restore-instance=sgcf-db-prod` |
| Rollback Cloud Run | `gcloud run services update-traffic sgcf-api --to-revisions=sgcf-api-PREV=100` |

---

## 5. Estrutura do projeto

```
sgcf-backend/
├── src/
│   ├── Sgcf.Domain/                 # Entidades, value objects, regras de negócio puras
│   │   ├── Contratos/
│   │   │   ├── Contrato.cs          # Entidade master
│   │   │   ├── Modalidades/         # FINIMP_DETAIL, REFINIMP_DETAIL, etc.
│   │   │   └── Cronograma/
│   │   │       └── Strategies/      # BulletStrategy, SacStrategy, CustomStrategy
│   │   ├── Hedges/
│   │   │   ├── InstrumentoHedge.cs
│   │   │   └── Mtm/                 # Funções puras de MTM (Forward, Collar)
│   │   ├── Garantias/
│   │   ├── Cotacoes/
│   │   ├── PlanoContas/
│   │   ├── Bancos/
│   │   ├── Calculo/                 # CalculadorSaldo, GrossUp, IOF, etc. — funções puras
│   │   ├── Common/                  # ValueObjects, Money, Percentual, FxRate
│   │   └── Sgcf.Domain.csproj       # SEM dependências de Application/Infra
│   │
│   ├── Sgcf.Application/            # Use cases, handlers MediatR, DTOs
│   │   ├── Contratos/
│   │   │   ├── Commands/            # CreateContrato, BaixarPagamento, etc.
│   │   │   └── Queries/             # GetContrato, GetTabelaCompleta, ListContratos
│   │   ├── Hedges/
│   │   ├── Painel/                  # Queries dos painéis consolidados
│   │   ├── Validacao/               # FluentValidation validators
│   │   ├── Authorization/           # Policies por papel
│   │   └── Sgcf.Application.csproj  # Depende apenas de Domain
│   │
│   ├── Sgcf.Infrastructure/         # EF Core, integrações externas, jobs
│   │   ├── Persistence/
│   │   │   ├── SgcfDbContext.cs
│   │   │   ├── Configurations/      # IEntityTypeConfiguration<T>
│   │   │   ├── Migrations/
│   │   │   └── Audit/               # AuditInterceptor.cs
│   │   ├── Bcb/                     # Cliente HTTP da API SCB-BCB (PTAX)
│   │   ├── Storage/                 # Adapter Cloud Storage para PDFs
│   │   ├── Identity/                # Adapter Identity Platform
│   │   └── Sgcf.Infrastructure.csproj
│   │
│   ├── Sgcf.Api/                    # ASP.NET Core Web API
│   │   ├── Controllers/             # ContratosController, HedgesController, etc.
│   │   ├── Middleware/              # ExceptionHandler, CorrelationId, RateLimit
│   │   ├── Auth/                    # JWT validation, claims transformation
│   │   ├── Telemetry/               # OpenTelemetry setup
│   │   ├── Program.cs               # Composition root
│   │   ├── appsettings.json
│   │   └── Sgcf.Api.csproj
│   │
│   ├── Sgcf.Mcp/                    # Servidor MCP (ADR-012)
│   │   ├── Server.cs
│   │   ├── Tools/                   # ListContratosTool, GetTabelaCompletaTool, etc.
│   │   ├── Schemas/                 # JSON Schema dos tools
│   │   ├── Auth/
│   │   └── Sgcf.Mcp.csproj
│   │
│   ├── Sgcf.A2a/                    # Servidor A2A (ADR-013)
│   │   ├── AgentCard.cs             # Geração do /.well-known/agent.json
│   │   ├── Skills/                  # Implementação de skills A2A
│   │   ├── TaskManager.cs           # Estado de tasks A2A
│   │   └── Sgcf.A2a.csproj
│   │
│   └── Sgcf.Jobs/                   # Background workers (Cloud Tasks consumers)
│       ├── IngestaoPtaxJob.cs
│       ├── MtmIntradayJob.cs
│       ├── ProvisaoJurosJob.cs
│       ├── AlertasVencimentoJob.cs
│       └── Sgcf.Jobs.csproj
│
├── tests/
│   ├── Sgcf.Domain.Tests/           # Unit tests (puras, sem I/O)
│   │   ├── Calculo/                 # Golden tests + property-based
│   │   └── Hedges/                  # Golden tests do MTM (Anexo A)
│   ├── Sgcf.Application.Tests/      # Tests com Testcontainers
│   ├── Sgcf.Api.IntegrationTests/   # WebApplicationFactory + Testcontainers
│   ├── Sgcf.Mcp.Tests/
│   └── Sgcf.GoldenDataset/          # Casos reais validados manualmente
│       └── data/                    # JSON com inputs + outputs esperados
│
├── tools/
│   └── Migracao/                    # Console app .NET para ETL da planilha
│       └── Migracao.csproj
│
├── infra/
│   ├── terraform/                   # IaC do GCP (projects, IAM, redes, Cloud SQL, Cloud Run, Memorystore)
│   ├── dev/
│   │   └── docker-compose.yml       # Postgres + Redis para dev local
│   └── cloudbuild/
│       └── cloudbuild.yaml          # Pipeline CI/CD
│
├── docs/
│   ├── api/                         # OpenAPI gerado + exemplos
│   ├── mcp/                         # Catálogo de tools MCP
│   ├── a2a/                         # Agent Card + exemplos
│   ├── runbooks/                    # 5 runbooks (deploy, rollback, restore, incident, alertas)
│   ├── architecture/                # Documento de Arquitetura + diagramas
│   ├── adr/                         # ADRs versionados (este projeto vem com ADR v1.1)
│   └── domain/                      # Glossário, dicionário de dados, fluxos
│
├── spikes/                          # Provas de conceito da Fase B.5 (MCP, A2A)
├── SPEC.md                          # Este documento
├── README.md
├── CLAUDE.md                        # Contexto para agentes Claude trabalharem no repo
├── .editorconfig
├── .gitignore
├── Directory.Build.props            # Configurações compartilhadas (.NET 11, nullable, warnings as errors)
├── Directory.Packages.props         # Central package management
├── sgcf-backend.sln
└── global.json                      # Pin da versão do SDK
```

**Princípios da estrutura:**
- `Sgcf.Domain` **nunca** referencia Application/Infrastructure (Clean Architecture)
- `Sgcf.Mcp` e `Sgcf.A2a` são **adapters finos sobre Application** — zero lógica de negócio
- Testes seguem espelho da estrutura de `src/`
- Cada `.csproj` tem apenas as dependências mínimas necessárias

---

## 6. Estilo de código

### 6.1 Princípios não-negociáveis (ADR-014)

1. **Clean Code** — nomes que revelam intenção; funções pequenas (default ≤ 30 linhas); classes coesas
2. **Simple > clever** — preferir solução boring/óbvia
3. **YAGNI agressivo** — não construir abstração até a 3ª duplicação
4. **Imutabilidade** — `record`/`readonly struct` para DTOs e VOs
5. **Pure functions no motor financeiro** — qualquer função de cálculo é pura (sem side effects, sem `DateTime.Now`, sem I/O)
6. **Sem mocks no domínio** — Testcontainers ou in-memory

### 6.2 Convenções de nomenclatura (C# padrão Microsoft + ajustes)

| Item | Convenção | Exemplo |
|---|---|---|
| Classe / Record / Interface | `PascalCase` | `Contrato`, `IRepositorioContratos` |
| Interface | Prefixo `I` | `IGeradorCronograma` |
| Método | `PascalCase` | `CalcularSaldoDevedor` |
| Campo privado | `_camelCase` | `_repositorio` |
| Variável local / parâmetro | `camelCase` | `valorPrincipal` |
| Constante | `PascalCase` (não `UPPER_SNAKE`) | `BaseCalculo360` |
| Enum value | `PascalCase` | `Modalidade.Finimp` |
| Arquivo | igual ao tipo principal (1 tipo por arquivo, exceto records pequenos correlatos) | `Contrato.cs` |
| Namespace | `Sgcf.<Layer>.<Aggregate>` | `Sgcf.Domain.Contratos` |
| Tabela DB | `snake_case` plural | `contratos`, `cronograma_pagamento` |
| Coluna DB | `snake_case` | `valor_principal_brl` |

**Idioma:** código em **português** para conceitos de domínio (já são termos do negócio), inglês para conceitos técnicos genéricos. Ex: `class Contrato`, `interface IRepository<T>`, `class CalculadorSaldo`.

### 6.3 Exemplo concreto — função pura de cálculo financeiro

```csharp
// Sgcf.Domain/Calculo/CalculadorJuros.cs

namespace Sgcf.Domain.Calculo;

/// <summary>
/// Cálculos de juros pro rata. Funções puras: mesma entrada → mesma saída.
/// Não dependem de tempo do sistema; data é parâmetro explícito.
/// </summary>
public static class CalculadorJuros
{
    /// <summary>Calcula juros pro rata diária linear.</summary>
    /// <remarks>
    /// Fórmula: Principal × Taxa × Dias / Base.
    /// Arredondamento HalfUp em 6 decimais (ver SPEC §8).
    /// </remarks>
    public static Money CalcularJurosProRata(
        Money principal,
        Percentual taxaAnual,
        int diasDecorridos,
        BaseCalculo baseCalculo)
    {
        if (diasDecorridos < 0)
            throw new ArgumentOutOfRangeException(nameof(diasDecorridos));

        var fator = taxaAnual.AsDecimal * diasDecorridos / (decimal)baseCalculo;
        return principal.Multiplicar(fator);
    }

    /// <summary>Aplica gross-up de IRRF: empresa absorve o tributo do exterior.</summary>
    /// <remarks>IRRF efetivo = juros × aliquota / (1 - aliquota). Anexo A §1.</remarks>
    public static Money CalcularIrrfGrossUp(Money jurosNominais, Percentual aliquotaIrrf)
    {
        if (aliquotaIrrf.AsDecimal >= 1m)
            throw new ArgumentException("Alíquota não pode ser ≥ 100%", nameof(aliquotaIrrf));

        var fator = aliquotaIrrf.AsDecimal / (1m - aliquotaIrrf.AsDecimal);
        return jurosNominais.Multiplicar(fator);
    }
}
```

```csharp
// Value object para evitar primitivos: nunca passar `decimal` ou `double` cru
// para representar dinheiro.

public readonly record struct Money(decimal Valor, Moeda Moeda)
{
    public Money Multiplicar(decimal fator) =>
        this with { Valor = Arredondamento.HalfUp(Valor * fator, casas: 6) };

    public Money Somar(Money outro)
    {
        if (Moeda != outro.Moeda)
            throw new InvalidOperationException(
                $"Não é possível somar {Moeda} com {outro.Moeda} sem conversão explícita");
        return this with { Valor = Arredondamento.HalfUp(Valor + outro.Valor, casas: 6) };
    }
}
```

```csharp
// Teste correspondente — golden test contra Anexo B §6.6

[Fact]
[Trait("Category", "Golden")]
public void CalcularJurosProRata_4131BB_60DiasAposDesembolso_RetornaUsd10000()
{
    // Arrange — caso do Anexo B §6.6, contrato 4131 BB
    var principal = new Money(1_000_000m, Moeda.USD);
    var taxa = Percentual.De(6m);  // 6% a.a.
    var dias = 60;
    var baseCalc = BaseCalculo.Dias360;

    // Act
    var juros = CalculadorJuros.CalcularJurosProRata(principal, taxa, dias, baseCalc);

    // Assert — exatamente USD 10.000,00 (60/360 × 6% × 1MM)
    juros.Should().Be(new Money(10_000m, Moeda.USD));
}
```

### 6.4 Anti-padrões proibidos no MVP

- ❌ `DateTime.Now` em código de domínio (use `IClock` injetável ou parâmetro explícito)
- ❌ `decimal` cru representando dinheiro (use `Money`)
- ❌ Comparação de moeda por string (use `enum Moeda`)
- ❌ Try/catch silencioso (`catch { }`) — sempre logar ou re-throw
- ❌ `async void` (exceto event handlers)
- ❌ Reflection em hot path (cálculo financeiro, MTM)
- ❌ Static state mutável em domínio
- ❌ Comentários repetindo o que o código faz; comentar o **porquê** (especialmente regras fiscais)

---

## 7. Glossário de domínio

> Termos centralizados aqui evitam ambiguidade. Quando um documento do projeto usar um termo desta lista, este é o significado canônico.

| Termo | Definição |
|---|---|
| **4131** | Empréstimo direto em moeda estrangeira sob a Lei 4.131/62. Tipicamente prazo longo (≥ 720 dias) com SBLC |
| **A2A** | Agent2Agent Protocol — padrão Google para comunicação entre agentes de IA (ADR-013) |
| **Audit log** | Registro imutável de toda mutação de entidade (quem, quando, antes/depois). Retenção 5 anos |
| **Balcão (Caixa)** | Modalidade de financiamento da Caixa com lastro de duplicatas |
| **Base de cálculo** | Quantidade de dias usada no denominador das fórmulas de juros: 360, 365 ou 252 |
| **Break funding fee** | Multa de pré-pagamento em operações de FINIMP/4131 |
| **CCB** | Cédula de Crédito Bancário |
| **Cash Collateral** | Garantia em CDB cativo (geralmente 20-30% do principal) |
| **CDB cativo** | CDB bloqueado como garantia de outro contrato; rende CDI mas não pode ser sacado |
| **CDI** | Certificado de Depósito Interbancário; taxa de referência dos CDBs |
| **CMEK** | Customer-Managed Encryption Keys — chaves criptográficas gerenciadas pelo cliente no GCP |
| **Collar (Range Forward)** | NDF com banda: proteção entre `strike_put` e `strike_call`; sem custo se cotação fica dentro da banda |
| **Cronograma** | Lista explícita de todas as parcelas (principal, juros, IOF, comissões, tarifas) de um contrato |
| **CVM** | Comissão de Valores Mobiliários (regulador) |
| **Dívida bruta** | Saldo devedor total em BRL antes de ajuste de hedge |
| **Dívida líquida** | Dívida bruta + ajuste MTM hedge − caixa e equivalentes |
| **EBITDA** | Earnings Before Interest, Taxes, Depreciation and Amortization |
| **FGI** | Fundo Garantidor de Investimentos (BNDES) — garantia parcial em operações de crédito |
| **FINIMP** | Financiamento à Importação |
| **Fixing** | Cotação de referência usada para liquidação de NDF (tipicamente PTAX D0 do dia do vencimento) |
| **Forward simples** | NDF sem banda: payoff = `notional × (S_atual − strike)` |
| **Gross-up** | Mecanismo onde o devedor (empresa) absorve o tributo na fonte (IRRF) que seria do credor: `IRRF efetivo = juros × aliq / (1 − aliq)` |
| **IFRS 9** | Norma internacional sobre instrumentos financeiros (mensuração a valor justo) |
| **Idempotency-Key** | Cabeçalho HTTP que garante que a mesma operação não seja executada duas vezes |
| **IN 1.037/2010** | Instrução Normativa da Receita Federal listando paraísos fiscais (afeta IRRF de 25%) |
| **Intraday** | Durante o pregão; cotação spot que varia continuamente |
| **IOF câmbio** | Imposto sobre Operações Financeiras na conversão cambial; padrão 0,38% |
| **IRRF** | Imposto de Renda Retido na Fonte (sobre juros remetidos ao exterior) |
| **JPY** | Iene japonês — exibir multiplicado por 100 ou 1.000 em interfaces (cotação muito pequena) |
| **Market Flex** | Cláusula que permite ao banco renegociar termos do contrato em caso de stress de mercado |
| **MCP** | Model Context Protocol — padrão Anthropic para conectar LLMs a fontes de dados e ferramentas (ADR-012) |
| **Mark-to-Market (MTM)** | Valor de mercado atual de um derivativo (NDF), calculado a cada momento conforme spot/strike |
| **Modalidade** | Tipo de operação: FINIMP, REFINIMP, 4131, NCE, Balcão Caixa, FGI, NDF |
| **NCE / CCE** | Nota / Cédula de Crédito à Exportação (em BRL, sem IRRF nem IOF câmbio) |
| **NDF** | Non-Deliverable Forward — derivativo cambial com liquidação financeira (sem entrega de moeda) |
| **Notional** | Valor de referência do NDF em moeda estrangeira |
| **PII** | Personally Identifiable Information |
| **PTAX** | Cotação oficial publicada pelo BCB; PTAX D0 = do próprio dia, PTAX D-1 = do dia anterior |
| **REFINIMP** | Refinanciamento de FINIMP existente; cria contrato-filho apontando para contrato-mãe |
| **ROF** | Registro de Operação Financeira no BCB — obrigatório em FINIMP |
| **SBLC** | Standby Letter of Credit — garantia bancária internacional |
| **Spot** | Cotação à vista, atualizada continuamente em horário de mercado |
| **Strike** | Preço de exercício de um derivativo |
| **Tabela completa** | Visão consolidada do contrato em 8 blocos (Anexo B §7) |
| **Teto mensal** | Limite operacional de R$ 4MM em concentração de pagamentos por mês (Plano FINIMP §1) |
| **TFM** | Target Framework Moniker (`net11.0`, `net10.0`) |
| **YAGNI** | You Aren't Gonna Need It — não construir abstração antes da necessidade real |

---

## 8. Padrão de precisão decimal e arredondamento

> **Regra crítica do MVP.** Divergências de centavo em sistemas financeiros geram retrabalho de auditoria. Esta seção é normativa.

### 8.1 Precisão de armazenamento (PostgreSQL)

| Categoria | Tipo SQL | Exemplo |
|---|---|---|
| Monetário em moeda original (USD, EUR, JPY, CNY, BRL) | `numeric(20, 6)` | USD 200000.000000 |
| Monetário em BRL convertido | `numeric(20, 6)` | R$ 5300000.000000 |
| Cotação FX (PTAX, spot) | `numeric(12, 6)` | 5.300000 |
| Percentual (taxa, alíquota) | `numeric(9, 6)` | 5.879000 (= 5,879%) |
| Quantidade de dias | `integer` | 180 |

**Por que 6 decimais em monetário:** absorve erros acumulados de cálculos pro rata sem perder precisão. Apresentação corta para 2 decimais (exceto JPY, que mostra inteiros ou ×100/×1000 conforme contexto).

### 8.2 Regra de arredondamento — **HalfUp (arredondamento comercial)**

Decisão registrada: arredondamento comercial (5 sobe sempre). Padrão da Receita Federal, bancos brasileiros e Excel — facilita reconciliação.

```csharp
public static class Arredondamento
{
    public static decimal HalfUp(decimal valor, int casas) =>
        Math.Round(valor, casas, MidpointRounding.AwayFromZero);
}
```

**Aplicação:**
- **Cálculos intermediários** mantêm 6 decimais (evita drift)
- **Persistência** em 6 decimais
- **Apresentação ao usuário** arredonda na borda da serialização: 2 decimais para BRL/USD/EUR/CNY, 0 para JPY (ou ×1000 conforme regra de UI), 4 para FX rates, 4 para percentuais

### 8.3 Regras de comparação

- **Nunca comparar `decimal` com `==`** em testes; usar `.Should().BeApproximately(esperado, tolerancia: 0.01m)` ou similar quando aplicável
- Para cálculos de cronograma, exigir **igualdade exata** (não aproximada) — divergência indica bug

### 8.4 Cross-check obrigatório no motor

A cada release, executar suite de **golden tests** contra `tests/Sgcf.GoldenDataset/`:
- 4131 BB do Anexo B §6.6 (cenários em 01/03/2026 e 15/09/2026)
- Forward simples USD 200k a 5,50 (Anexo A §2.2 — 3 cenários)
- Collar USD 200k put 5,10 / call 5,40 (Anexo A §2.1 — 3 cenários)
- FINIMP BB Tóquio do `Analise_FINIMP_BB_vs_Itau.xlsx`
- Mínimo de 5 contratos reais por modalidade (golden dataset coletado pelo Stream M)

---

## 8.5 Antecipação de pagamento (pré-pagamento e liquidação antecipada)

> **Capacidade de primeira classe do MVP.** A análise dos contratos da Proxys revelou **5 padrões distintos** de cálculo de antecipação por banco/modalidade. Modelar essa diversidade como Strategy pattern configurável por `BANCO_CONFIG` é o que diferencia o SGCF de uma planilha — permite simular antecipação em segundos com detalhamento item a item.

### 8.5.1 Tipos de antecipação suportados

| Código | Tipo | Descrição |
|---|---|---|
| **T1** | Liquidação Total Antecipada | Quita 100% do saldo restante; encerra o contrato |
| **T2** | Liquidação Parcial — Redução de Prazo | Paga parte do principal, mantém valor das parcelas, encurta prazo |
| **T3** | Liquidação Parcial — Redução de Parcela | Paga parte do principal, mantém prazo, reduz valor das parcelas |
| **T4** | Amortização Extraordinária Avulsa | Antecipa uma parcela inteira sem alterar a estrutura |
| **T5** | Refinanciamento Interno | Liquida com recursos de nova operação no mesmo banco (pode ter isenções) |

### 8.5.2 Os 5 padrões de cálculo

| Padrão | Bancos identificados | Característica essencial |
|---|---|---|
| **A — Pro rata + break flat + indenização** | BB FINIMP (provavelmente Itaú, Bradesco, Santander, Safra) | Juros pro rata até a data + break funding fee fixo (ex: 1%) + indenização variável apresentada pelo banco |
| **B — Sem desconto** | **Sicredi FINIMP** | Cobra juros do período TOTAL contratado — antecipar não economiza juros (regra exclusiva e restritiva) |
| **C — MTM com desconto a taxa de mercado** | **FGI BV (PEAC)** | Equivalente a precificar a operação como derivativo: saldo + juros futuros capitalizados − desconto à taxa atual |
| **D — TLA BACEN** | **Caixa Balcão** | Fórmula explícita Resoluções BACEN 3401/06 e 3516/07: `max(2% × VTD; 0,1% × Pzr_meses × VTD)` |
| **E — Pagamento ordinário com abatimento** | Caixa Balcão (caso prefixado) | Liquidação sem TLA com abatimento proporcional de juros futuros embutidos |

### 8.5.3 15 componentes de custo possíveis

Cada simulação detalha quais dos componentes a seguir aplicam-se ao caso (ver Anexo C §2.2 para definições completas):

`C1 PRINCIPAL_A_QUITAR` · `C2 JUROS_PRO_RATA_ATE_DATA` · `C3 JUROS_PERIODO_TOTAL_FORCADO` · `C4 JUROS_FUTUROS_CAPITALIZADOS` · `C5 DESCONTO_TAXA_MERCADO` · `C6 BREAK_FUNDING_FEE_FLAT` · `C7 INDENIZACAO_EXPECTATIVA_FRUSTRADA` · `C8 TLA_BACEN` · `C9 IOF_COMPLEMENTAR` · `C10 SPREAD_CAMBIAL_ANTECIPACAO` · `C11 VARIACAO_CAMBIAL_REALIZADA` · `C12 IRRF_COMPLEMENTAR` · `C13 MULTA_NDF_DESCASADO` · `C14 TARIFA_OPERACAO_ANTECIPADA` · `C15 RESTITUICAO_CDB_CATIVO`

### 8.5.4 Restrições operacionais (não custos)

`AVISO_PREVIO_MIN_DIAS_UTEIS` (BB: 20 d.u.) · `VALOR_MINIMO_PARCIAL_PCT` (BB: 20%) · `EXIGE_ANUENCIA_EXPRESSA` (Sicredi) · `EXIGE_PARCELA_INTEIRA` (FGI BV) · `EXIGE_AUTORIZACAO_GOVERNAMENTAL`

### 8.5.5 Implementação de referência

- **Domain**: `Sgcf.Domain.Antecipacao/Strategies/{PadraoA..PadraoE}.cs` — funções puras
- **Endpoint REST**: `POST /api/v1/contratos/{id}/simular-antecipacao` (idempotente)
- **Tool MCP**: `simular_antecipacao` (read-only, 9º tool no MVP)
- **Persistência**: `simulacao_antecipacao` com payload JSONB de componentes (audit trail e análise histórica)
- **Comparativo automático**: cada simulação inclui o cenário "se não antecipar + aplicar caixa em CDI até o vencimento" para apoiar a decisão

### 8.5.6 Golden tests obrigatórios

Mínimo 5 casos golden em `tests/Sgcf.GoldenDataset/` cobrindo os 5 padrões — ver Anexo C §7 para inputs e outputs esperados.

> **Detalhamento completo:** `plan/Anexo_C_Regras_Antecipacao_Pagamento.md` — citações literais dos contratos, modelo de dados, fórmulas algébricas, casos de teste e lacunas pendentes (3 contratos a coletar na Fase 0).

## 8.6 Camada RAG complementar (busca semântica em texto contratual)

> Adicionada na v1.2 do SPEC. Decisão fundadora em **ADR-015**.

A camada estruturada (§8.5) cobre **~80% das perguntas calculáveis** com determinismo total. Para os **~20% restantes** — perguntas textuais sobre cláusulas atípicas (market flex, cross-default, jurisdição), validação de contratos novos, busca de padrões — adota-se uma **camada RAG complementar**.

### 8.6.1 Princípios

1. **Camada secundária**: nunca substitui a estruturada quando a pergunta é calculável
2. **Boundary não-negociável**: RAG/LLM **nunca** responde sozinho sobre valores financeiros calculáveis (ver §17.3)
3. **Mesmo banco**: usa **pgvector** no PostgreSQL Cloud SQL existente — sem novo serviço
4. **Chunking por cláusula** (não por tamanho fixo) para preservar fronteiras semânticas
5. **Citação literal obrigatória** em cada resposta — referência ao contrato e cláusula de origem

### 8.6.2 Roteamento de perguntas (orquestrador)

| Tipo de pergunta | Camada usada | Exemplo |
|---|---|---|
| Calculável (custo, MTM, juros) | **8.5 Estruturada** | "Quanto custa antecipar contrato X hoje?" |
| Textual (o que diz a cláusula) | **8.6 RAG** | "O que a cláusula 12 diz sobre pré-pagamento?" |
| Discovery / busca semântica | **8.6 RAG** | "Quais contratos têm cláusula de market flex?" |
| Validação contrato novo | **8.6 RAG** + comparação | "O contrato Bradesco recebido tem cláusula desfavorável?" |
| Híbrida | **Estruturada como fonte primária + RAG cita cláusula** | "Por que o cálculo de antecipação BB é assim?" → simulação + citação da cláusula 12 |

### 8.6.3 Schema (alto nível)

```sql
CREATE EXTENSION IF NOT EXISTS vector;

CREATE TABLE clausula_contratual (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    contrato_id UUID NOT NULL REFERENCES contrato(id),
    banco_id UUID NOT NULL REFERENCES banco(id),
    modalidade TEXT NOT NULL,
    data_assinatura DATE NOT NULL,
    clausula_numero TEXT NULL,                    -- "12", "5 §6º", "Anexo III"
    topico TEXT NOT NULL,                          -- "antecipacao", "vencimento_antecipado", "mora", etc.
    texto_literal TEXT NOT NULL,
    embedding vector(768) NOT NULL,                -- depende do modelo (text-embedding-005 = 768)
    versao INTEGER NOT NULL DEFAULT 1,
    indexado_em TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_clausula_embedding ON clausula_contratual
    USING hnsw (embedding vector_cosine_ops);
CREATE INDEX idx_clausula_metadata ON clausula_contratual (banco_id, modalidade, topico);
```

### 8.6.4 Tools MCP (ver §13.1 — adicionados ao MVP)

- `buscar_clausula_contratual(query, filtros)` — busca semântica + filtros estruturados
- `comparar_clausulas(contrato_id_1, contrato_id_2, topico)` — comparação textual lado a lado

### 8.6.5 Indexação no MVP

- **Sprint 0**: habilitar extension `pgvector` no Cloud SQL (1 migration)
- **Fase 2**: criar schema `clausula_contratual`
- **Fase 7B**: implementar 2 tools MCP + endpoint REST `POST /api/v1/contratos/buscar-clausula`
- **Fase 9**: indexar os 1.200 contratos existentes em batch durante a migração (extração por cláusula via parser específico — sem agente no MVP)

### 8.6.6 Agente Extrator de Cláusulas (Fase 2 do projeto, fora do MVP)

Alinhado ao "Validador de Contrato" do `Plano_Agentes_FINIMP.md`, será o agente que recebe contratos novos, extrai cláusulas estruturadas, gera embeddings, indexa em pgvector e **sugere atualizações ao `BANCO_CONFIG`** quando detectar parâmetros novos. No MVP, a extração é feita uma vez em batch.

> **Detalhamento da decisão e custos:** ver **ADR-015**.

---

## 9. Padrão de data/hora e timezone

### 9.1 Premissas

- **Timezone de negócio:** `America/Sao_Paulo` (BR-SP, sem horário de verão a partir de 2019)
- **Storage:** `timestamptz` no Postgres (sempre UTC)
- **Apresentação:** sempre em America/Sao_Paulo
- **Biblioteca:** **NodaTime** em vez de `DateTime` para datas financeiras

### 9.2 Tipos por contexto

| Conceito | Tipo NodaTime | Por quê |
|---|---|---|
| Data de uma parcela / vencimento | `LocalDate` | Não tem hora; calendário civil |
| Data-hora de um evento (criação, audit) | `Instant` (UTC) | Universal, comparável globalmente |
| Data-hora apresentada ao usuário | `ZonedDateTime` (`SaoPaulo`) | Conversão na borda |
| Período (prazo do contrato) | `Period` ou `int dias` | Operações com calendário |

### 9.3 Calendário e dias úteis

- **Pagamentos cuja data cair em fim de semana ou feriado nacional brasileiro:** rolar para o **próximo dia útil** (default; pode ser sobrescrito por contrato específico)
- **Feriados:** lista mantida em tabela `feriados_nacionais` com seed inicial (2026-2030); admin pode adicionar
- **Pro rata** sempre conta dias corridos (não úteis), exceto quando contrato específico exigir

### 9.4 PTAX intraday (Anexo A §5.1)

Sistema mantém 3 visões com timestamp explícito:
- **Estimada (intraday)** com label "Posição estimada — PTAX não publicada"
- **Contábil oficial** travada após 13h15 BRT (publicação oficial)
- **Fixing** congelada na data de liquidação do NDF

---

## 10. Classificação de dados e LGPD

### 10.1 Classificação por categoria

| Categoria | Dados | Tratamento |
|---|---|---|
| **PII (LGPD)** | **CPF/CNPJ de avalistas e contrapartes** (decisão sponsor 08/05/2026) | Mascaramento em logs (`***.***.***-**`); criptografia em trânsito + em repouso (CMEK); registro de tratamento LGPD; direito de portabilidade implementado em endpoint admin |
| **Confidencial — sigilo bancário** | PDFs originais de contratos, valores de operação, taxas, garantias | Cloud Storage com CMEK; acesso por papel (RBAC); **não mascarados em logs** (decisão sponsor: não considerados PII) |
| **Interno** | Códigos de contrato, status, datas, indicadores agregados | Sem mascaramento; auditoria padrão |
| **Público** | Cotações PTAX (já públicas via BCB), tabelas oficiais (IRRF, IOF) | Sem restrição; podem ser cacheados externamente |
| **Operacional dos usuários** | Email, papel, log de auditoria | Tratamento como funcionário (não PII de cliente); retenção 5 anos |

### 10.2 Retenção por categoria

| Categoria | Retenção mínima | Justificativa |
|---|---|---|
| Audit log | **5 anos** | Anexo A §1; LGPD; padrão fiscal |
| PDFs de contratos | **5 anos após quitação** | Padrão fiscal/auditoria |
| Cotações FX históricas | **Indefinida** | Reproduzibilidade de snapshots históricos |
| Posição snapshots mensais | **Indefinida** | Reproduzibilidade de fechamentos |
| Logs de aplicação | **90 dias** quentes + **1 ano** frio | Custo vs valor de troubleshooting |
| Métricas de telemetria | **30 dias** | Custo |

### 10.3 LGPD — direitos do titular

- **Direito de acesso e portabilidade**: endpoint admin `GET /api/v1/admin/lgpd/titular/{cpf}/dados` retorna todos os dados associados (apenas perfil `admin`)
- **Direito de eliminação**: aplicação de **anonimização** (não exclusão real) em registros de avalistas após solicitação formal — preserva audit trail e integridade fiscal
- **Política**: documento separado em `docs/compliance/lgpd-politica.md` (a redigir na Fase B com Compliance)

### 10.4 Mascaramento em logs (regra padrão)

- CPF: `***.***.***-NN` (últimos 2)
- CNPJ: `**.***.***/****-NN`
- Tokens / secrets: nunca logar (mesmo mascarados)
- Email de usuário: pode logar (necessário para audit)
- Valores monetários: **podem ser logados em logs operacionais** (decisão sponsor — não considerados PII)

---

## 11. Matriz RBAC (papéis × operações)

> Matriz autoritativa. Cada endpoint REST/MCP/A2A consulta esta matriz via policy.

| Operação | tesouraria | contabilidade | gerente | diretor | auditor | admin |
|---|:-:|:-:|:-:|:-:|:-:|:-:|
| Listar contratos (resumo) | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Ver contrato detalhado | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Criar contrato | ✅ | ❌ | ❌ | ❌ | ❌ | ✅ |
| Editar contrato | ✅ | ❌ | ❌ | ❌ | ❌ | ✅ |
| Cancelar contrato | ❌ | ❌ | ✅ | ✅ | ❌ | ✅ |
| Criar/editar NDF | ✅ | ❌ | ❌ | ❌ | ❌ | ✅ |
| Baixar pagamento | ✅ | ❌ | ❌ | ❌ | ❌ | ✅ |
| Ver tabela completa | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Exportar tabela completa (PDF/Excel) | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Ver painel de dívida | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Ver dashboard executivo | ✅ | ❌ | ✅ | ✅ | ❌ | ✅ |
| Simulador de cenários | ✅ | ❌ | ✅ | ✅ | ❌ | ✅ |
| Ver/baixar lançamentos gerenciais | ✅ | ✅ | ❌ | ✅ | ✅ | ✅ |
| Snapshot mensal (gerar/consultar) | ❌ | ❌ | ❌ | ❌ | ✅ | ✅ |
| Configurar BANCO_CONFIG / parâmetros | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ |
| Configurar plano de contas | ❌ | ✅ | ❌ | ❌ | ❌ | ✅ |
| Configurar limite de crédito por banco | ❌ | ❌ | ✅ | ✅ | ❌ | ✅ |
| Aprovar exceções (cobertura, override de validação) | ❌ | ❌ | ✅ | ✅ | ❌ | ✅ |
| Ver audit log | ❌ | ❌ | ❌ | ❌ | ✅ | ✅ |
| Endpoint LGPD (dados de titular) | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ |
| MCP — tools read-only | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| MCP — tools de escrita (se 7B.5) | ✅ | ❌ | ❌ | ❌ | ❌ | ✅ |
| A2A — invocar skills | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Gerenciar usuários e papéis | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ |

**Implementação:** policies em `Sgcf.Application/Authorization/Policies/`. Atributo `[Authorize(Policy = "PolicyXxx")]` em controllers/handlers. Nunca checagem ad-hoc de role string.

---

## 12. Convenções de API REST

### 12.1 Versionamento

- Path-based: `/api/v1/...`
- Breaking change → nova versão `/api/v2/...`; versão anterior mantida por 6 meses mínimo
- Mudanças aditivas (campo opcional novo) **não** quebram contrato → mantém v1

### 12.2 Formato de erro padronizado (RFC 7807 — Problem Details)

```json
{
  "type": "https://sgcf.proxys.com.br/errors/validacao",
  "title": "Validação falhou",
  "status": 400,
  "detail": "Banco Bradesco não aceita REFINIMP",
  "instance": "/api/v1/contratos",
  "trace_id": "00-abcd1234-...-01",
  "errors": [
    { "campo": "modalidade", "mensagem": "Banco não aceita REFINIMP", "regra": "BANCO_CONFIG.aceita_refinimp" }
  ]
}
```

### 12.3 Paginação — cursor-based

```
GET /api/v1/contratos?limit=50&cursor=eyJpZCI6MTIzfQ==

{
  "data": [...],
  "next_cursor": "eyJpZCI6MTczfQ==",
  "has_more": true
}
```

### 12.4 Idempotência

- Header `Idempotency-Key` obrigatório em **todo POST** que cria recurso
- Retenção da chave: 24h
- Mesma chave + mesmo body em 24h → mesmo resultado (não duplica)
- Mesma chave + body diferente → 422

### 12.5 Auditoria de chamadas

- Todo request gera linha em `AUDIT_LOG` com:
  - `actor_id` (do JWT)
  - `actor_type` ("user" ou "agent" — para A2A/MCP)
  - `source` ("rest" / "mcp" / "a2a")
  - `correlation_id`
  - `entity_type`, `entity_id`, `action`
  - `before` / `after` (JSONB)
  - `created_at` (Instant UTC)

### 12.6 Rate limiting

- REST: 600 req/min por usuário (token bucket)
- MCP: 60 req/min por usuário (mais restrito — agentes podem encadear)
- A2A: 30 tasks/min por agente
- Burst: até 2× o limite por 10s
- Resposta 429 com `Retry-After`

### 12.7 OpenAPI

- Gerado automaticamente via Swashbuckle
- Disponível em `/swagger` (auth obrigatória em prod)
- Versão estática commitada em `docs/api/openapi-v1.yaml` a cada release

---

## 13. Convenções de MCP e A2A

### 13.1 MCP (ADR-012, ADR-015)

| Aspecto | Decisão |
|---|---|
| Endpoint | `/mcp` no domínio principal (subpath, não subdomínio no MVP) |
| Transport | HTTP+SSE (streamable HTTP) |
| Auth | OAuth 2.1 (mesmo IdP do REST) |
| Schema dos tools | JSON Schema 2020-12 versionado em `docs/mcp/tools/` |
| Erros | Padrão MCP (`isError: true` no resultado); detalhes em `content.text` |
| Audit | Mesmo `AUDIT_LOG`, com `source: "mcp"` |
| Convenção de nome de tool | `verbo_substantivo` snake_case: `list_contratos`, `get_posicao_divida` |
| Idempotência (tools de escrita) | Header `Idempotency-Key` no payload do tool |

**Tools read-only do MVP — 11 tools:**

| # | Tool | Camada | Função |
|---|---|---|---|
| 1 | `list_contratos` | Estruturada | Lista resumida com filtros |
| 2 | `get_contrato` | Estruturada | Contrato + extensão por modalidade |
| 3 | `get_tabela_completa` | Estruturada | 8 blocos da tabela completa |
| 4 | `get_posicao_divida` | Estruturada | Posição consolidada multi-moeda |
| 5 | `get_calendario_vencimentos` | Estruturada | Próximos vencimentos |
| 6 | `get_cotacao_fx` | Estruturada | Cotação atual ou histórica |
| 7 | `get_mtm_hedge` | Estruturada | MTM atual de NDF |
| 8 | `simular_cenario_cambial` | Estruturada | Estresse cambial |
| 9 | `simular_antecipacao` | Estruturada | Simulação de antecipação (Anexo C) |
| 10 | `buscar_clausula_contratual` | **RAG** | Busca semântica em cláusulas (ADR-015) |
| 11 | `comparar_clausulas` | **RAG** | Comparação textual lado a lado entre 2 contratos |

### 13.2 A2A (ADR-013)

| Aspecto | Decisão |
|---|---|
| Agent Card | Servido em `/.well-known/agent.json` |
| Identidade | Nome: "SGCF — Sistema de Gestão de Contratos da Proxys"; versão = release semver |
| Skills no MVP | Apenas 1 skill demonstrativa (`consulta_posicao_divida`) — escopo expandido na Fase 2 |
| Auth | OAuth 2.1 |
| Estado de tasks | Persistido em `a2a_tasks` (Postgres); TTL configurável (default 7 dias) |
| Streaming | SSE para `tasks/sendSubscribe` |

### 13.3 Princípio compartilhado MCP + A2A + REST

> **Tools MCP e skills A2A NUNCA implementam lógica de negócio.** São adapters finos sobre os mesmos handlers MediatR usados pela REST API.

```csharp
// Sgcf.Mcp/Tools/GetPosicaoDividaTool.cs
public class GetPosicaoDividaTool(IMediator mediator) : IMcpTool
{
    public async Task<McpResult> ExecuteAsync(JsonElement args, CancellationToken ct)
    {
        var query = new GetPosicaoDividaQuery(args.GetProperty("data").GetDateTime());
        var resultado = await mediator.Send(query, ct);  // Mesma query do REST
        return McpResult.Json(resultado);
    }
}
```

---

## 14. Estratégia de testes

### 14.1 Pirâmide

```
         ┌──────────────────┐
         │  E2E (poucos)    │   < 20 testes — fluxos críticos via WebApplicationFactory
         └──────────────────┘
       ┌────────────────────┐
       │  Integração (médio)│     50-100 testes — controllers + Postgres real (Testcontainers)
       └────────────────────┘
   ┌──────────────────────────┐
   │  Unidade (muitos)        │   500+ testes — domínio puro, sem I/O
   └──────────────────────────┘
```

### 14.2 Coverage targets (gate no CI)

| Camada | Mínimo | Bloqueia merge se < |
|---|---|---|
| `Sgcf.Domain` (motor financeiro) | **95%** | Sim |
| `Sgcf.Domain` (outros) | 90% | Sim |
| `Sgcf.Application` | 85% | Sim |
| `Sgcf.Api` (controllers) | 70% | Não (warning) |
| `Sgcf.Infrastructure` | 60% | Não (warning) |
| `Sgcf.Mcp`, `Sgcf.A2a` | 80% | Sim |
| Geral | 80% | Sim |

### 14.3 Tipos de teste

| Tipo | Onde | Uso |
|---|---|---|
| **Unit puro** | `Sgcf.Domain.Tests/` | Funções de cálculo, value objects, regras de negócio |
| **Property-based** | `Sgcf.Domain.Tests/` | Invariantes do motor: `SUM(parcelas PRINCIPAL) == valor_principal_inicial`, `MTM(strike, strike) == 0`, etc. |
| **Golden** | `Sgcf.GoldenDataset/` | Casos reais validados manualmente — qualquer mudança no motor exige re-validação humana |
| **Integração (handler)** | `Sgcf.Application.Tests/` | MediatR handlers + Postgres real via Testcontainers |
| **Integração (API)** | `Sgcf.Api.IntegrationTests/` | WebApplicationFactory + auth fake + Postgres |
| **MCP / A2A** | `Sgcf.Mcp.Tests/` | Cliente MCP de teste invoca tools |
| **Smoke (CI/CD)** | `tools/SmokeTests/` | 5-10 endpoints críticos após cada deploy |

### 14.4 Golden dataset — formato

Cada caso golden é um par `(entrada.json, saída_esperada.json)`:

```json
// tests/Sgcf.GoldenDataset/data/4131-bb-anexo-b-6.6/entrada.json
{
  "contrato": {
    "modalidade": "LEI_4131",
    "banco": "BB",
    "moeda": "USD",
    "principal": 1000000,
    "taxa_aa": 6.0,
    "base_calculo": 360,
    "data_desembolso": "2026-01-01",
    "estrutura": "SAC",
    "amortizacoes_semestrais": 4
  },
  "consulta_em": "2026-03-01"
}
```

```json
// tests/Sgcf.GoldenDataset/data/4131-bb-anexo-b-6.6/saida_esperada.json
{
  "saldo_principal_aberto_usd": 1000000,
  "juros_provisionados_usd": 10000,
  "saldo_total_usd": 1010000,
  "saldo_total_brl_at_5_30": 5353000,
  "fonte": "Anexo_B §6.6"
}
```

### 14.5 Regras

- **Teste falhando nunca é mergeable** (sem skip, sem `[Fact(Skip = ...)]` em main)
- **Cada bug em produção gera primeiro um golden test** que reproduz, então fix
- **Property-based** obrigatório para qualquer função pública em `Sgcf.Domain.Calculo` e `Sgcf.Domain.Hedges.Mtm`
- **Mocks proibidos no domínio**; permitidos em adapters de I/O (BCB API, identity provider)

---

## 15. Observabilidade (padrão obrigatório)

### 15.1 Logs estruturados

Cada log é JSON com no mínimo:

```json
{
  "timestamp": "2026-05-08T14:32:01.123Z",
  "level": "Information",
  "message": "Contrato criado",
  "trace_id": "00-abcd...",
  "span_id": "ef01...",
  "correlation_id": "req-uuid",
  "service": "sgcf-api",
  "version": "1.2.3",
  "environment": "prod",
  "actor_id": "usr-123",
  "actor_type": "user",
  "source": "rest",
  "entity_type": "Contrato",
  "entity_id": "FIN-2026-0042"
}
```

### 15.2 Métricas custom (Cloud Monitoring)

| Métrica | Tipo | Quando |
|---|---|---|
| `sgcf_contratos_criados_total` | counter | Por banco, modalidade |
| `sgcf_pagamentos_baixados_total` | counter | Por modalidade |
| `sgcf_mtm_recalculos_total` | counter | Job intraday |
| `sgcf_mtm_duracao_segundos` | histogram | Performance do job |
| `sgcf_ptax_ingestao_falhas_total` | counter | Saúde da integração BCB |
| `sgcf_calculo_saldo_duracao_ms` | histogram | Performance do motor |
| `sgcf_audit_log_lag_segundos` | gauge | Saúde do audit pipeline |
| `sgcf_mcp_tool_invocacoes_total` | counter | Por tool |
| `sgcf_mcp_tool_duracao_ms` | histogram | Por tool |
| `sgcf_a2a_tasks_total` | counter | Por skill, status |

### 15.3 Tracing (OpenTelemetry → Cloud Trace)

- Trace por request HTTP, MCP call, A2A task
- Span por chamada de Application Service, repository, external call
- Atributos: `actor_id`, `entity_type`, `entity_id`

### 15.4 Alertas críticos (PagerDuty/Slack)

| Alerta | Threshold | Ação |
|---|---|---|
| 5xx rate | > 1% por 5min | Alerta crítico |
| p99 latência leitura | > 500ms por 10min | Alerta warning |
| Job PTAX falhando | 2 falhas consecutivas | Alerta crítico |
| Audit log lag | > 5min | Alerta crítico |
| MTM job duração | > 60s | Alerta warning |
| Cotação FX stale | > 20min em horário de mercado | Alerta crítico |
| Coverage no CI | < target | Bloqueia merge |

### 15.5 Health checks

| Endpoint | Conteúdo |
|---|---|
| `/health/live` | App está rodando (sempre 200 se processo está OK) |
| `/health/ready` | DB conectado + Redis conectado + última PTAX < 30min |
| `/health/startup` | Migrations aplicadas + seed mínimo presente |

---

## 16. Requisitos não-funcionais

### 16.1 SLAs (compromisso do MVP — decisão sponsor 08/05/2026)

| Métrica | Compromisso |
|---|---|
| **Uptime** | **99,5%** em horário comercial (8h-19h America/Sao_Paulo) |
| **RTO** (Recovery Time Objective) | **4 horas** |
| **RPO** (Recovery Point Objective) | **1 hora** |
| **p99 leitura** | **< 500 ms** |
| **p99 escrita** | **< 2 s** |
| **On-call** | Horário comercial; após-hora best-effort |
| **Janela de manutenção** | Madrugada (02h-04h BRT), quinzenal, com aviso de 48h |
| **Downtime aceitável** | ~1h/mês (janela noturna planejada) |

### 16.2 Capacidade (dimensionamento inicial)

| Recurso | MVP | 1 ano (projeção) |
|---|---|---|
| Contratos ativos | 1.200 | 2.000 |
| Contratos totais (incluindo histórico) | 1.500 | 3.000 |
| Usuários simultâneos | 10 | 25 |
| Cloud Run instâncias | min 1, max 3 | min 1, max 5 |
| Cloud SQL | db-custom-2-4 (2 vCPU, 4GB) | db-custom-4-8 |
| Memorystore Redis | 1GB Basic | 2GB Standard HA |

### 16.3 Backup e DR

- Cloud SQL: backup automático diário; PITR (point-in-time recovery) habilitado por 7 dias
- Snapshot mensal posicional (`POSICAO_SNAPSHOT`) imutável — fonte de verdade para reconstruir histórico
- Teste de restore: **mensal** em staging, com simulação de incidente (cronograma no `runbooks/restore-db.md`)
- Cloud Storage (PDFs): versionamento + retenção 5 anos
- Audit log: replicado para BigQuery (cold storage) com retenção indefinida

### 16.4 Segurança

| Aspecto | Especificação |
|---|---|
| TLS | 1.3 obrigatório; sem fallback < 1.2 |
| At rest | CMEK no Cloud SQL e Cloud Storage |
| Tokens | Access 15min / refresh 24h |
| Senha | n/a (OIDC delegado a Identity Platform) |
| WAF | Cloud Armor com regras OWASP Core |
| Secrets | Secret Manager; rotação trimestral |
| Headers HTTP | HSTS, CSP, X-Frame-Options, X-Content-Type-Options |
| CORS | Allowlist explícita por origem |

---

## 17. Boundaries (Always / Ask First / Never)

### 17.1 Sempre fazer

- ✅ Aplicar princípios ADR-014 em cada PR (clean code, simple > clever)
- ✅ Funções puras no motor financeiro (sem `DateTime.Now`, sem I/O, sem state mutável)
- ✅ Usar `Money`, `Percentual`, `FxRate` value objects — nunca `decimal` cru para dinheiro
- ✅ Usar NodaTime para datas (não `DateTime`)
- ✅ Idempotência em todo POST que cria recurso
- ✅ Audit log com `source` correto (`rest`/`mcp`/`a2a`)
- ✅ Migrations EF Core code-first, revisadas no PR
- ✅ Golden test ao tocar no motor de cálculo
- ✅ Property-based test em função pública de cálculo
- ✅ Mascaramento de CPF/CNPJ em logs
- ✅ FluentValidation em toda entrada externa
- ✅ Errors padronizados (RFC 7807)
- ✅ Coverage gate no CI antes de merge

### 17.2 Perguntar antes de fazer

- ⚠️ Adicionar nova dependência NuGet (vetar duplicação, lock-in)
- ⚠️ Mudar schema de uma entidade central (`Contrato`, `Cronograma`, `Hedge`) — exige revisão por sponsor
- ⚠️ Alterar regra de cálculo financeiro — exige golden test atualizado e aprovação
- ⚠️ Ignorar gate de coverage com justificativa
- ⚠️ Criar nova abstração (interface, base class) — checar regra das 3 duplicações
- ⚠️ Tools MCP de escrita (cobertos em 7B.5) — só após decisão explícita do sponsor
- ⚠️ Mudar SLA, retenção, classificação de dados — exige nova ADR
- ⚠️ Habilitar feature flag em prod
- ⚠️ Rodar migration que não seja aditiva (drop column, type change destrutivo)
- ⚠️ Trocar versão do .NET (10 LTS ↔ 11) — recompilar e re-testar tudo

### 17.3 Nunca fazer

- ❌ Comitar secrets (chaves API, credenciais BCB, JWT signing keys) — usar Secret Manager
- ❌ Logar valores de tokens, senhas, chaves
- ❌ Usar `DateTime.Now` em código de domínio
- ❌ Usar `decimal` cru para representar dinheiro
- ❌ `catch { }` silencioso
- ❌ `async void` (exceto event handlers)
- ❌ Mock no domínio (Testcontainers ou in-memory)
- ❌ Drop de tabela em migration de produção sem plano explícito
- ❌ Pular CI gate (coverage, tests, lint)
- ❌ `git push --force` em `main`
- ❌ Modificar audit log existente (apenas append)
- ❌ Deletar contrato (apenas marcar como cancelado/quitado)
- ❌ Permitir contrato em moeda estrangeira sem hedge sem alerta crítico aprovado
- ❌ Usar reflection em hot path (cálculo de cronograma, MTM)
- ❌ Implementar lógica de negócio em `Sgcf.Mcp` ou `Sgcf.A2a` — apenas adapters
- ❌ Criar comentário que explica **o que** o código faz (comentar **por quê**, especialmente regras fiscais)
- ❌ Usar agente/LLM no caminho crítico de cálculo financeiro do MVP
- ❌ **RAG/LLM responder sozinho sobre valor financeiro calculável** (ADR-015): quando a pergunta envolve número (custo, juros, MTM), o orquestrador chama o motor estruturado e o RAG só pode citar a cláusula que justifica o cálculo
- ❌ Indexar PDF inteiro como um único vetor (perde granularidade) — chunking sempre por cláusula
- ❌ Permitir que `Sgcf.Mcp/Tools/buscar_clausula_contratual` ou `comparar_clausulas` retornem números financeiros calculados — apenas trechos de texto literal

---

## 18. Critérios de sucesso (espelham §1.3 com detalhe verificável)

| # | Critério | Evidência | Quem valida |
|---|---|---|---|
| 1 | MVP em produção em até 24 semanas | Tag `v1.0.0` no repo + URL prod ativa | Sponsor |
| 2 | 1.200+ contratos importados, ≥99% match com planilha | Relatório de migração | Sponsor + controladoria |
| 3 | Tempo de fechamento mensal ≤ 1 dia (vs 3-4 dias hoje) | Cronometragem 2 fechamentos | Sponsor |
| 4 | Cobertura motor financeiro ≥ 95% | Relatório CI no release | Dev |
| 5 | 0 incidentes P0/P1 em 30 dias | Tracker de incidentes | Sponsor |
| 6 | Sponsor consulta dívida via Claude Desktop (MCP) | Demo gravada | Sponsor |
| 7 | Reconciliação contábil divergência ≤ 0,1% | Relatório de reconciliação | Contabilidade |
| 8 | Resposta a auditoria em ≤ 1h | Simulação de auditoria | Sponsor + auditor |
| 9 | SLA 99,5% horário comercial atingido no mês 1 de produção | Cloud Monitoring uptime | Dev |
| 10 | Planilha em modo read-only há ≥ 30 dias | Atestado do sponsor | Sponsor |

---

## 19. Open Questions (gaps remanescentes — input humano necessário)

> Questões que **não foram resolvidas** nesta SPEC e dependem de decisão futura.

### 19.1 Decisões já mapeadas no plano (ver `tasks/plan.md` §5)
- Stack do front-end headless (Fase B.4)
- Perfil do dev sênior (.NET generalista vs especialista financeiro)
- Reforço pontual de dev em picos
- Política de retenção LGPD detalhada (Fase B.4 com Compliance)
- Versão definitiva do .NET (.NET 11 GA vs .NET 10 LTS) — final Fase B
- SDK MCP escolhido (Anthropic oficial vs próprio)
- Versão da spec A2A
- Inclusão do Agente Migrador na Fase 1
- Tools MCP de escrita no MVP (7B.5) ou Fase 2
- Política de auto-post vs review-then-post (relevante Fase 2 SAP)

### 19.2 Novas questões surgidas neste SPEC

1. **Lista oficial de feriados nacionais** (10.3) — usar lista pública do BCB (`feriados-bcb.json`) ou base própria? Quem mantém atualização anual?
2. **Política exata de anonimização LGPD (10.3)** — qual é o modelo? Substituir CPF por hash? Por NULL? Manter primeiros e últimos dígitos? Decisão com Compliance.
3. **Tratamento de erros do BCB API** quando PTAX D0 atrasa publicação (caso real ~5x ao ano) — fallback para PTAX D-1? Notificar tesouraria? Janela de tolerância?
4. **Quem é "auditor" no MVP** — auditoria interna apenas, ou auditoria externa também precisa de acesso? Se externa, política de account temporário.
5. **Dashboards Power BI / Looker Studio** (Business Case §4.2) — fora de escopo MVP; confirmar se backend deve já expor view dedicada para BI ou só na Fase 2.
6. **CDB Cativo — IRRF tabela regressiva** (Anexo B 8.2 menciona) — sistema calcula automaticamente baseado no prazo, ou usuário informa alíquota? Default proposto: cálculo automático conforme regra fiscal.
7. **Operações em "rascunho" / draft** — analista pode criar contrato em estado `RASCUNHO` antes de confirmar, ou todo contrato criado já entra em `ATIVO`? Default proposto: estado `RASCUNHO` permitido (workflow gerente para `ATIVO`).
8. **Sistema deve tratar feriados bancários por estado/cidade** ou só nacionais? Default proposto: só nacionais no MVP.
9. **Multi-currency display** — analista precisa configurar moeda preferida ou sistema mostra sempre BRL como primário? Default proposto: BRL primário com toggle para moeda original.
10. **Integração com Slack** para alertas — webhook único da Tesouraria ou notificação per-user? Default proposto: webhook único do canal `#tesouraria-alertas`.

---

## 20. Referências

| Documento | Conteúdo | Localização |
|---|---|---|
| **Business Case** | Visão de negócio, ROI, escopo, roadmap macro | `plan/Business_Case_Sistema_Contratos.md` |
| **Anexo A** | Valoração em tempo real, NDFs (Forward + Collar), MTM, fórmulas | `plan/Anexo_A_Valoracao_Divida_NDF.md` |
| **Anexo B** | Modalidades, modelo polimórfico, BANCO_CONFIG, garantias, cronograma | `plan/Anexo_B_Modalidades_e_Modelo_Dados.md` |
| **Anexo C** | **Regras de antecipação de pagamento** (5 padrões, 15 componentes de custo, BANCO_CONFIG ampliado, golden tests) | `plan/Anexo_C_Regras_Antecipacao_Pagamento.md` |
| **ADR v1.1** | Decisões arquiteturais (stack, MCP, A2A, princípios de código) | `ADR_Decisoes_Estrategicas.md` |
| **Plano de Implementação v1.1** | 24 semanas, 9 fases + Fase B + 7B, vertical slicing | `tasks/plan.md` |
| **Todo v1.1** | Checklist executável | `tasks/todo.md` |
| **Plano Agentes FINIMP** | Visão de agentes (Fase 2) | `plan/Plano_Agentes_FINIMP.md` |
| **Prompt Comparação FINIMP** | Especificação do Agente Comparador (Fase 2) | `plan/Prompt_Comparacao_FINIMP.md` |
| **Prompt Agente Arquiteto** | Prompt para gerar Documento de Arquitetura na Fase B.1 | `Prompt_Agente_Arquiteto_SGCF.md` |
| **Contratos modelo** | 12+ PDFs reais para golden dataset | `CONTRATOS_MODELOS/` |
| **Análise FINIMP BB vs Itaú** | Caso golden de comparação manual | `plan/Analise_FINIMP_BB_vs_Itau.xlsx` |

---

## Changelog

- **v1.0 — 08/maio/2026** — versão inicial. Consolida Business Case + Anexos + ADR v1.1 + Plan v1.1. Preenche lacunas com defaults aprovados pelo sponsor (HalfUp, SLA prudente, LGPD = CPF/CNPJ apenas).
- **v1.1 — 10/maio/2026** — adicionada nova seção **8.5 Antecipação de pagamento** (5 padrões de cálculo, 15 componentes de custo, 5 tipos de antecipação, restrições operacionais). Detalhamento completo em `plan/Anexo_C_Regras_Antecipacao_Pagamento.md`. Stories US-16 a US-18 adicionadas.
- **v1.2 — 10/maio/2026** — adicionada nova seção **8.6 Camada RAG complementar com pgvector** (ADR-015): busca semântica em texto contratual como camada secundária ao motor estruturado, com boundary explícito de que RAG/LLM nunca responde sozinho sobre valor financeiro calculável. Stories US-19/20 adicionadas. Tools MCP expandidos para 11 (added `buscar_clausula_contratual` e `comparar_clausulas`).
