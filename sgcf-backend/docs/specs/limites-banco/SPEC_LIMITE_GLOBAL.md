# SPEC — LimiteGlobalBanco (Limite Guarda-Chuva por Banco)

> **Status:** Aprovado — implementado em produção
> **Data:** 2026-05-23
> **Última revisão:** 2026-06-03
> **Autor:** Engenharia de Requisitos (SGCF Backend)
> **Versão:** v1.1
> **Escopo:** Domain + Application + Infrastructure + Api + Tests
> **Dependências:** SPEC §3 (Cotacoes — `LimiteBanco`, `LimiteBancoHistorico`), `Banco`, `ModalidadeContrato`, `Contrato`.
>
> **Alteração v1.1 (2026-06-03):** Seção §3.2 e §5.2 atualizadas para refletir a semântica correta de "vigente" (Opção A). A definição anterior ("vigente = sem data fim") foi substituída pela definição baseada em janela de datas. Consulte §3.2-A para detalhes.

---

## 1. Objetivo

### 1.1. O quê

Introduzir o agregado `LimiteGlobalBanco` para representar o **teto agregado** (linha guarda-chuva) que um banco concede à empresa, independente de modalidade. Ele coexiste com `LimiteBanco` (que continua representando limite por modalidade) e habilita dois regimes operacionais por banco:

- **Cenário A:** apenas limite global registrado (sem limites por modalidade). Qualquer modalidade opera sob o teto global.
- **Cenário B:** ao menos um `LimiteBanco` (por modalidade) registrado. O regime passa a ser per-modality e o global atua como **invariante de soma** (Σ limites por modalidade ≤ limite global).

### 1.2. Por quê

Bancos podem operar sob dois modelos comerciais:

1. Concedem uma linha única (umbrella) que a empresa distribui livremente entre FINIMP / REFINIMP / NCE / etc.
2. Concedem linhas separadas por produto, com tetos individuais que, em conjunto, respeitam um teto consolidado.

Hoje o SGCF só modela o caso (2) parcialmente, sem o teto consolidado. Sem o `LimiteGlobalBanco` não há como:

- Permitir uso flexível inter-modalidades quando o banco oferece linha única.
- Garantir que a soma das linhas por modalidade não excede o teto global negociado.
- Gerar alertas de exposição agregada antes de novo contrato.

### 1.3. Personas

| Persona                    | Necessidade                                                                                  |
| -------------------------- | -------------------------------------------------------------------------------------------- |
| **Operador de Tesouraria** | Saber o teto global vigente de cada banco e o disponível agregado antes de cotar/contratar. |
| **Gerente Financeiro**     | Garantir que limites por modalidade nunca excedam o teto consolidado negociado.              |
| **Auditor**                | Rastrear alterações no teto global (quem, quando, valor anterior/novo, observações).         |

### 1.4. Métricas de sucesso

- 100% dos bancos com linha de crédito ativa têm `LimiteGlobalBanco` vigente cadastrado.
- 0 contratos criados que violem o teto global (regime A) ou o teto por modalidade (regime B).
- Histórico de alterações disponível por banco para análise de tendência (aumentos/reduções de linha).

---

## 2. Comandos

```bash
# Build
dotnet build

# Test (fast loop)
dotnet test --filter "Category!=Slow"

# Test (full suite, inclui Testcontainers)
dotnet test

# Test only this feature
dotnet test --filter "FullyQualifiedName~LimiteGlobalBanco"

# Adicionar migration (após editar entity + configuration)
dotnet ef migrations add S33_LimiteGlobalBanco \
  --project src/Sgcf.Infrastructure \
  --startup-project src/Sgcf.Api \
  --output-dir Persistence/Migrations

# Aplicar migration
dotnet ef database update \
  --project src/Sgcf.Infrastructure \
  --startup-project src/Sgcf.Api

# Run API
dotnet run --project src/Sgcf.Api

# Coverage local
dotnet test --collect:"XPlat Code Coverage" --results-directory ./coverage
```

---

## 3. Modelo de Domínio

### 3.1. Diagrama lógico

```
┌────────────────────────────────────────────────────┐
│                    Banco                            │
│  - Id, CodigoCompe, RazaoSocial, ...                │
└──────────┬─────────────────────────────────────────┘
           │ 1
           │
           │ 0..N
           ▼
┌────────────────────────────────────────────────────┐
│              LimiteGlobalBanco                      │
│  (aggregate root — implements ITenantScoped)        │
│  - Id                                               │
│  - TenantId (preenchido pelo TenantSaveInterceptor) │
│  - BancoId                                          │
│  - ValorLimiteBrl (Money, BRL)                      │
│  - DataVigenciaInicio (LocalDate)                   │
│  - DataVigenciaFim (LocalDate?)                     │
│  - Observacoes (string?)                            │
│  - CreatedAt, UpdatedAt (Instant)                   │
│  - Historico: List<LimiteGlobalBancoHistorico>      │
│                                                     │
│  NOTA: ValorUtilizado é SEMPRE computado            │
│        dinamicamente — nunca persistido.            │
└─────────────────┬──────────────────────────────────┘
                  │ 1
                  │
                  │ 0..N (append-only)
                  ▼
┌────────────────────────────────────────────────────┐
│         LimiteGlobalBancoHistorico                  │
│  (child entity, append-only)                        │
│  - Id, LimiteGlobalBancoId                          │
│  - ValorAnteriorBrl? (null na entrada inicial)      │
│  - ValorNovoBrl                                     │
│  - RegistradoEm (Instant)                           │
│  - Observacoes?                                     │
└────────────────────────────────────────────────────┘

Relação com LimiteBanco (existente, SPEC §3.1 de Cotacoes):

  LimiteGlobalBanco (1) ──── (0..N) LimiteBanco
       Σ LimiteBanco.ValorLimiteBrl  ≤  LimiteGlobalBanco.ValorLimiteBrl
       (invariante validado em create/update de LimiteBanco)
```

### 3.2-A. Definição de Limite Vigente (Opção A — semântica correta)

> **Nota:** a definição abaixo substitui qualquer referência anterior a "vigente = sem data fim" que possa existir neste documento ou em código de versões anteriores. A semântica correta é baseada em janela de datas e está em vigor desde [0.10.1].

Um `LimiteGlobalBanco` é considerado **vigente** em uma data de referência `D` quando:

```
DataVigenciaInicio ≤ D
    E
(DataVigenciaFim == null OR DataVigenciaFim ≥ D)
```

Consequências diretas:
- `DataVigenciaFim == null` significa vigência em aberto (sem encerramento programado) — o registro é vigente para qualquer data futura.
- `DataVigenciaFim` preenchido com data anterior a `D` torna o registro **encerrado** para aquela data de referência, mesmo que seja o único registro do banco.
- O endpoint `GET /api/v1/bancos/{bancoId}/limite-global-vigente` usa `D = hoje` (data corrente no fuso horário de Brasília, `America/Sao_Paulo`) para determinar vigência.
- A query de listagem `GET /api/v1/limites-globais-banco?vigentesEm=YYYY-MM-DD` aceita `D` arbitrária para consultas históricas.

**Impacto no repositório:**

O método `GetVigenteByBancoAsync` deve aplicar a condição completa de janela, não apenas `DataVigenciaFim == null`. A implementação correta (Opção A) é:

```csharp
// Opção A — filtra por janela de datas contendo hoje
LocalDate hoje = clock.GetCurrentInstant().InZone(fusoHorarioBrasilia).Date;

context.LimitesGlobaisBanco
    .Where(l => l.BancoId == bancoId
             && l.DataVigenciaInicio <= hoje
             && (l.DataVigenciaFim == null || l.DataVigenciaFim >= hoje))
    .FirstOrDefaultAsync(ct);
```

A versão anterior (`DataVigenciaFim == null`) era uma simplificação que ignorava limites com data de fim explícita ainda dentro do período de validade. A Opção A é a semântica correta e está sendo aplicada pela correção de bug em andamento.

---

### 3.2. Entidade `LimiteGlobalBanco`

Campos:

| Campo                  | Tipo                | Notas                                                                 |
| ---------------------- | ------------------- | --------------------------------------------------------------------- |
| `Id`                   | `Guid` (v7)         | PK herdada de `Entity`                                                |
| `TenantId`             | `Guid`              | Preenchido por `TenantSaveInterceptor`                                |
| `BancoId`              | `Guid`              | FK → `banco_config.id` (Restrict)                                     |
| `ValorLimiteBrlDecimal`| `decimal` (interno) | Persistido como `numeric(20,6)`                                       |
| `ValorLimiteBrl`       | `Money` (BRL)       | Propriedade computada read-only sobre `ValorLimiteBrlDecimal`         |
| `DataVigenciaInicio`   | `LocalDate`         | Obrigatória                                                           |
| `DataVigenciaFim`      | `LocalDate?`        | Null = vigente em aberto                                              |
| `Observacoes`          | `string?`           | Texto livre                                                           |
| `CreatedAt`            | `Instant`           | NodaTime                                                              |
| `UpdatedAt`            | `Instant`           | NodaTime                                                              |
| `Historico`            | `IReadOnlyCollection<LimiteGlobalBancoHistorico>` | Backing list privado                  |

Propriedades computadas (não persistidas):

- `ValorUtilizadoBrl(IConsultaSaldoBanco svc)` → calculado em domain service (ver §3.4).
- `ValorDisponivelBrl` → `Max(0, ValorLimiteBrl − ValorUtilizadoBrl)`.

Factory + métodos de domínio:

```csharp
public static LimiteGlobalBanco Criar(
    Guid bancoId,
    Money valorLimiteBrl,
    LocalDate dataVigenciaInicio,
    IClock clock,
    LocalDate? dataVigenciaFim = null,
    string? observacoes = null);

public void Atualizar(
    IClock clock,
    Money? novoLimiteBrl = null,
    LocalDate? novaDataVigenciaInicio = null,
    LocalDate? novaDataVigenciaFim = null,
    string? observacoes = null,
    Money? saldoDevedorAtual = null); // necessário para validar redução

public void EncerrarVigencia(LocalDate dataFim, IClock clock);
```

Notas críticas:

- `ValorUtilizado` **nunca** é armazenado. É calculado on-demand pelo domain service `IConsultaSaldoBanco` somando contratos ativos do banco (Cenário A) ou somando `LimiteBanco.ValorUtilizadoBrl` por modalidade (Cenário B).
- A regra "novo limite ≥ saldo devedor atual" é validada injetando `saldoDevedorAtual` no `Atualizar` — o domínio não conhece o repositório.
- Histórico inicial (`ValorAnterior = null`) é gravado na criação. Cada alteração subsequente de `ValorLimiteBrl` grava nova entrada.

### 3.3. Entidade `LimiteGlobalBancoHistorico`

Espelha exatamente `LimiteBancoHistorico`:

| Campo                       | Tipo          | Notas                              |
| --------------------------- | ------------- | ---------------------------------- |
| `Id`                        | `Guid` (v7)   |                                    |
| `LimiteGlobalBancoId`       | `Guid`        | FK → `limite_global_banco.id` (Cascade) |
| `ValorAnteriorBrlDecimal`   | `decimal?`    | Null na entrada inicial            |
| `ValorAnteriorBrl`          | `Money?`      | Computed                           |
| `ValorNovoBrlDecimal`       | `decimal`     |                                    |
| `ValorNovoBrl`              | `Money`       | Computed                           |
| `RegistradoEm`              | `Instant`     |                                    |
| `Observacoes`               | `string?`     |                                    |

Factory `internal static Criar(...)` análoga a `LimiteBancoHistorico.Criar`.

### 3.4. Domain Service `IConsultaSaldoBanco`

```csharp
namespace Sgcf.Domain.Cotacoes;

public interface IConsultaSaldoBanco
{
    /// <summary>
    /// Soma dos saldos devedores em BRL de todos os contratos ATIVOS do banco,
    /// independente de modalidade. Usado quando o banco opera em regime Cenário A
    /// (sem LimiteBanco por modalidade).
    /// </summary>
    Task<Money> CalcularSaldoDevedorBancoAsync(Guid bancoId, CancellationToken ct);

    /// <summary>
    /// Soma dos LimiteBanco.ValorUtilizadoBrl para todas as modalidades do banco.
    /// Usado quando o banco opera em regime Cenário B.
    /// </summary>
    Task<Money> CalcularUtilizadoAgregadoModalidadesAsync(Guid bancoId, CancellationToken ct);

    /// <summary>
    /// Soma dos LimiteBanco.ValorLimiteBrl ativos para o banco.
    /// Usado para validar Σ modalidades ≤ global.
    /// </summary>
    Task<Money> CalcularSomaLimitesModalidadesAsync(
        Guid bancoId,
        Guid? excluirLimiteBancoId,
        CancellationToken ct);

    /// <summary>
    /// Indica se o banco está em regime per-modality (Cenário B).
    /// True quando existe ao menos um LimiteBanco ativo para o banco.
    /// </summary>
    Task<bool> BancoEmRegimePerModalityAsync(Guid bancoId, CancellationToken ct);
}
```

Implementação concreta vive em `Sgcf.Infrastructure.Persistence.Repositories.ConsultaSaldoBancoService` (não no domínio).

---

## 4. Regras de Validação (Invariantes)

### 4.1. Invariantes do agregado `LimiteGlobalBanco`

| #     | Regra                                                                                                                                | Comportamento se violada            |
| ----- | ------------------------------------------------------------------------------------------------------------------------------------ | ----------------------------------- |
| LG-01 | `ValorLimiteBrl.Moeda == Moeda.Brl`                                                                                                  | `ArgumentException`                 |
| LG-02 | `ValorLimiteBrl.Valor > 0`                                                                                                           | `ArgumentOutOfRangeException`       |
| LG-03 | `DataVigenciaFim > DataVigenciaInicio` (quando informada)                                                                            | `ArgumentException`                 |
| LG-04 | Apenas um `LimiteGlobalBanco` com `DataVigenciaFim IS NULL` por `(TenantId, BancoId)`                                                | Índice único parcial no banco       |
| LG-05 | Períodos de vigência de registros do mesmo `(TenantId, BancoId)` não podem se sobrepor                                               | Validação na Application (handler)  |
| LG-06 | Em `Atualizar`, se `novoLimiteBrl` informado: `novoLimiteBrl.Valor ≥ saldoDevedorAtual.Valor`                                        | `InvalidOperationException`         |
| LG-07 | `Historico` recebe entrada na criação (`ValorAnterior = null`) e em toda alteração subsequente de `ValorLimiteBrl`                   | Sem alteração de histórico = bug    |
| LG-08 | `EncerrarVigencia(dataFim)`: `dataFim ≥ DataVigenciaInicio` e não pode encerrar vigência já encerrada                                | `InvalidOperationException`         |

### 4.2. Invariantes cruzadas com `LimiteBanco` (validação na Application)

| #     | Regra                                                                                                                                                       | Onde validar                                                          | Bloqueio |
| ----- | ----------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------- | -------- |
| LG-09 | Ao criar/atualizar `LimiteBanco`: `Σ LimiteBanco.ValorLimiteBrl ativos do banco (incluindo o novo) ≤ LimiteGlobalBanco.ValorLimiteBrl vigente`              | `CriarLimiteBancoHandler`, `AtualizarLimiteBancoHandler`              | Hard     |
| LG-10 | Reduzir `LimiteGlobalBanco`: novo valor ≥ saldo devedor atual (Cenário A) **ou** ≥ Σ `LimiteBanco.ValorLimiteBrl` ativos (Cenário B)                       | `AtualizarLimiteGlobalBancoHandler` antes de chamar `Atualizar`       | Hard     |
| LG-11 | Cenário B: criar contrato em modalidade requer `LimiteBanco` registrado para essa modalidade **e** disponibilidade simultânea modalidade + global          | `CriarContratoHandler` (ou validador equivalente)                     | Hard     |
| LG-12 | Cenário A: criar contrato consome do disponível global; `Σ contratos ativos + novo ≤ LimiteGlobalBanco.ValorLimiteBrl`                                      | `CriarContratoHandler`                                                | Hard     |
| LG-13 | Não permitir criar `LimiteGlobalBanco` para banco que já possui `LimiteBanco` cuja soma excede o valor proposto                                              | `CriarLimiteGlobalBancoHandler`                                       | Hard     |

### 4.3. Regra de seleção de regime (Cenário A vs B)

Avaliada via `IConsultaSaldoBanco.BancoEmRegimePerModalityAsync(bancoId)`:

```
SE existe LimiteBanco WHERE banco_id = X AND data_vigencia_fim IS NULL
    REGIME = B (per-modality)
SENÃO
    REGIME = A (global puro)
```

Não há fallback automático de modalidade para global no Cenário B. Modalidade sem `LimiteBanco` registrado **não opera** no Cenário B (erro: "modalidade X requer LimiteBanco registrado neste banco — regime per-modality").

### 4.4. Cálculo de disponibilidade

| Regime | Disponível para nova operação |
| ------ | ----------------------------- |
| A      | `LimiteGlobalBanco.ValorLimiteBrl − Σ contratos ativos do banco` |
| B      | `min(LimiteBanco.ValorDisponivelBrl_da_modalidade, LimiteGlobalBanco.ValorLimiteBrl − Σ LimiteBanco.ValorUtilizadoBrl)` |

Em todos os casos: se < 0, considerar 0 (clamp).

---

## 5. Endpoints da API

Convenção: prefixo `/api/v1/limites-globais-banco`. Autorização: `Admin` para escrita; `Operador` para leitura.

### 5.1. Rotas

| Verbo    | Rota                                                          | Descrição                                                           | Roles    |
| -------- | ------------------------------------------------------------- | ------------------------------------------------------------------- | -------- |
| `GET`    | `/api/v1/limites-globais-banco?bancoId=&vigentesEm=YYYY-MM-DD` | Lista (filtra por banco e/ou data de vigência)                       | Operador |
| `GET`    | `/api/v1/limites-globais-banco/{id}`                           | Detalhe de um registro com histórico                                 | Operador |
| `GET`    | `/api/v1/bancos/{bancoId}/limite-global-vigente`               | Retorna o registro vigente do banco (janela `[DataVigenciaInicio, DataVigenciaFim]` contém hoje) com `valorUtilizado`/`disponivel` computados | Operador |
| `POST`   | `/api/v1/limites-globais-banco`                                | Cria novo limite global                                              | Admin    |
| `PATCH`  | `/api/v1/limites-globais-banco/{id}`                           | Atualiza valor / vigência / observações                              | Admin    |
| `POST`   | `/api/v1/limites-globais-banco/{id}/encerrar-vigencia`         | Encerra vigência (define `DataVigenciaFim`)                          | Admin    |

### 5.2. Contratos de request/response (resumidos)

**`POST /api/v1/limites-globais-banco`**

Request:
```json
{
  "bancoId": "uuid",
  "valorLimiteBrl": "1500000.00",
  "dataVigenciaInicio": "2026-06-01",
  "dataVigenciaFim": null,
  "observacoes": "Linha aprovada em comitê BB de 21/05"
}
```

Response `201 Created`: `LimiteGlobalBancoDto`.

Erros:
- `400` — payload inválido (Money negativo, datas inválidas).
- `409` — sobreposição de vigência (LG-05) ou Σ `LimiteBanco` > novo global (LG-13).
- `404` — `bancoId` não encontrado.

**`PATCH /api/v1/limites-globais-banco/{id}`**

Request (todos opcionais):
```json
{
  "valorLimiteBrl": "1300000.00",
  "dataVigenciaInicio": null,
  "dataVigenciaFim": null,
  "observacoes": "Renegociação 2026-06-15"
}
```

Response `200 OK`: `LimiteGlobalBancoDto`.

Erros:
- `409` — redução abaixo do saldo devedor (LG-10) ou abaixo de Σ `LimiteBanco` (LG-10).
- `400` — payload inválido.

**`POST /api/v1/limites-globais-banco/{id}/encerrar-vigencia`**

Request:
```json
{ "dataFim": "2026-12-31" }
```

Response `200 OK`: `LimiteGlobalBancoDto` com `dataVigenciaFim` preenchido.

**`GET /api/v1/bancos/{bancoId}/limite-global-vigente`**

Retorna o limite global cujo período `[DataVigenciaInicio, DataVigenciaFim]` contém a data de hoje (ver §3.2-A para a definição formal de vigente). Utilização e disponibilidade são calculadas dinamicamente conforme o regime do banco (§4.3 e §4.4).

Response `200 OK`:
```json
{
  "id": "uuid",
  "bancoId": "uuid",
  "valorLimiteBrl": 1500000.00,
  "valorUtilizadoBrl": 720000.00,
  "valorDisponivelBrl": 780000.00,
  "regime": "PerModalidade",
  "dataVigenciaInicio": "2026-06-01",
  "dataVigenciaFim": null,
  "observacoes": "Linha aprovada em comitê BB de 21/05",
  "createdAt": "2026-06-01T00:00:00+00:00",
  "updatedAt": "2026-06-01T00:00:00+00:00",
  "historico": [
    {
      "id": "uuid",
      "limiteGlobalBancoId": "uuid",
      "valorAnteriorBrl": null,
      "valorNovoBrl": 1500000.00,
      "registradoEm": "2026-06-01T00:00:00+00:00",
      "observacoes": "Criação do limite global"
    }
  ]
}
```

`404` se não houver limite com janela de datas contendo hoje. Consulte a seção de Troubleshooting em `docs/api/bancos.md` para os casos mais frequentes.

---

## 6. Estrutura de Arquivos

Novos arquivos (todos novos — nenhum existente é renomeado):

```
src/
├── Sgcf.Domain/
│   └── Cotacoes/
│       ├── LimiteGlobalBanco.cs                              (NEW — aggregate root)
│       ├── LimiteGlobalBancoHistorico.cs                     (NEW — child entity)
│       └── IConsultaSaldoBanco.cs                            (NEW — domain service contract)
│
├── Sgcf.Application/
│   └── Cotacoes/
│       ├── ILimiteGlobalBancoRepository.cs                   (NEW)
│       ├── LimiteGlobalBancoDto.cs                           (NEW)
│       ├── LimiteGlobalBancoVigenteDto.cs                    (NEW — inclui valorUtilizado computado)
│       ├── Commands/
│       │   ├── CriarLimiteGlobalBancoCommand.cs              (NEW)
│       │   ├── CriarLimiteGlobalBancoHandler.cs              (NEW)
│       │   ├── AtualizarLimiteGlobalBancoCommand.cs          (NEW)
│       │   ├── AtualizarLimiteGlobalBancoHandler.cs          (NEW)
│       │   ├── EncerrarVigenciaLimiteGlobalBancoCommand.cs   (NEW)
│       │   └── EncerrarVigenciaLimiteGlobalBancoHandler.cs   (NEW)
│       └── Queries/
│           ├── ListarLimitesGlobaisBancoQuery.cs             (NEW)
│           ├── ListarLimitesGlobaisBancoHandler.cs           (NEW)
│           ├── GetLimiteGlobalBancoQuery.cs                  (NEW)
│           ├── GetLimiteGlobalBancoHandler.cs                (NEW)
│           ├── GetLimiteGlobalVigenteBancoQuery.cs           (NEW)
│           └── GetLimiteGlobalVigenteBancoHandler.cs         (NEW)
│
├── Sgcf.Infrastructure/
│   └── Persistence/
│       ├── Configurations/
│       │   ├── LimiteGlobalBancoConfiguration.cs             (NEW)
│       │   └── LimiteGlobalBancoHistoricoConfiguration.cs    (NEW)
│       ├── Repositories/
│       │   ├── LimiteGlobalBancoRepository.cs                (NEW)
│       │   └── ConsultaSaldoBancoService.cs                  (NEW — implementa IConsultaSaldoBanco)
│       └── Migrations/
│           └── S33_LimiteGlobalBanco.cs                      (NEW — auto-generated by ef)
│
├── Sgcf.Api/
│   └── Controllers/
│       └── LimitesGlobaisBancoController.cs                  (NEW)
│
└── (toques opcionais em CriarLimiteBancoHandler / AtualizarLimiteBancoHandler para invariante LG-09)

tests/
├── Sgcf.Domain.Tests/
│   └── Cotacoes/
│       ├── LimiteGlobalBancoTests.cs                         (NEW — invariantes LG-01..LG-08)
│       └── LimiteGlobalBancoHistoricoTests.cs                (NEW)
│
├── Sgcf.Application.Tests/
│   └── Cotacoes/
│       ├── CriarLimiteGlobalBancoHandlerTests.cs             (NEW)
│       ├── AtualizarLimiteGlobalBancoHandlerTests.cs         (NEW)
│       ├── EncerrarVigenciaLimiteGlobalBancoHandlerTests.cs  (NEW)
│       ├── LimiteGlobalBancoInvariantesCruzadasTests.cs      (NEW — LG-09, LG-10, LG-13)
│       └── ConsultaSaldoBancoServiceTests.cs                 (NEW — Testcontainers)
│
└── Sgcf.Api.IntegrationTests/
    └── LimitesGlobaisBancoEndpointsTests.cs                  (NEW — fluxos HTTP)

docs/specs/limites-banco/
└── SPEC_LIMITE_GLOBAL.md                                     (este arquivo)
```

### 6.1. Tabelas PostgreSQL (schema `sgcf`)

| Tabela                            | Chave           | Notas                                                                                              |
| --------------------------------- | --------------- | -------------------------------------------------------------------------------------------------- |
| `limite_global_banco`             | `id` (uuid v7)  | `tenant_id UUID NOT NULL`; UQ parcial `(tenant_id, banco_id) WHERE data_vigencia_fim IS NULL`; RLS |
| `limite_global_banco_historico`   | `id` (uuid v7)  | `tenant_id UUID NOT NULL`; FK `limite_global_banco_id` CASCADE; append-only; RLS                   |

Migration: `S33_LimiteGlobalBanco`. Inclui RLS policy `USING (tenant_id = current_setting('app.tenant_id', true)::uuid)`.

---

## 7. Estilo de Código

Todos os exemplos seguem `CLAUDE.md` (Money, NodaTime, `AwayFromZero`, layers).

### 7.1. Entity esqueleto (`LimiteGlobalBanco.cs`)

```csharp
using NodaTime;
using Sgcf.Domain.Auditoria;
using Sgcf.Domain.Common;
using Sgcf.Domain.Tenancy;

namespace Sgcf.Domain.Cotacoes;

/// <summary>
/// Limite global (umbrella) que um banco concede à empresa.
/// Funciona como teto agregado independente de modalidade.
/// SPEC §3 — LimiteGlobalBanco.
/// </summary>
public sealed class LimiteGlobalBanco : Entity, IAuditable, ITenantScoped
{
    public Guid TenantId { get; private set; }
    public Guid BancoId { get; private set; }

    internal decimal ValorLimiteBrlDecimal { get; private set; }
    public Money ValorLimiteBrl => new(ValorLimiteBrlDecimal, Moeda.Brl);

    public LocalDate DataVigenciaInicio { get; private set; }
    public LocalDate? DataVigenciaFim { get; private set; }
    public string? Observacoes { get; private set; }

    public Instant CreatedAt { get; private set; }
    public Instant UpdatedAt { get; private set; }

    private readonly List<LimiteGlobalBancoHistorico> _historico = new();
    public IReadOnlyCollection<LimiteGlobalBancoHistorico> Historico => _historico.AsReadOnly();

    private LimiteGlobalBanco() { }

    public static LimiteGlobalBanco Criar(
        Guid bancoId,
        Money valorLimiteBrl,
        LocalDate dataVigenciaInicio,
        IClock clock,
        LocalDate? dataVigenciaFim = null,
        string? observacoes = null)
    {
        if (valorLimiteBrl.Moeda != Moeda.Brl)
            throw new ArgumentException("ValorLimiteBrl deve ser em BRL.", nameof(valorLimiteBrl));

        if (valorLimiteBrl.Valor <= 0)
            throw new ArgumentOutOfRangeException(nameof(valorLimiteBrl), "ValorLimiteBrl deve ser positivo.");

        if (dataVigenciaFim.HasValue && dataVigenciaFim.Value <= dataVigenciaInicio)
            throw new ArgumentException(
                "DataVigenciaFim deve ser posterior a DataVigenciaInicio.",
                nameof(dataVigenciaFim));

        var now = clock.GetCurrentInstant();
        var limite = new LimiteGlobalBanco
        {
            BancoId = bancoId,
            ValorLimiteBrlDecimal = valorLimiteBrl.Valor,
            DataVigenciaInicio = dataVigenciaInicio,
            DataVigenciaFim = dataVigenciaFim,
            Observacoes = observacoes,
            CreatedAt = now,
            UpdatedAt = now,
        };

        limite._historico.Add(LimiteGlobalBancoHistorico.Criar(
            limiteGlobalBancoId: limite.Id,
            valorAnteriorBrl: null,
            valorNovoBrl: valorLimiteBrl,
            registradoEm: now,
            observacoes: "Criação do limite global"));

        return limite;
    }

    /// <summary>
    /// Atualiza valor e/ou vigência. Reduções de valor exigem que <paramref name="saldoDevedorAtual"/>
    /// seja fornecido pelo caller (Application) — domínio não conhece repositório.
    /// </summary>
    public void Atualizar(
        IClock clock,
        Money? novoLimiteBrl = null,
        LocalDate? novaDataVigenciaInicio = null,
        LocalDate? novaDataVigenciaFim = null,
        string? observacoes = null,
        Money? saldoDevedorAtual = null)
    {
        if (novoLimiteBrl.HasValue)
        {
            if (novoLimiteBrl.Value.Moeda != Moeda.Brl)
                throw new ArgumentException("NovoLimiteBrl deve ser em BRL.", nameof(novoLimiteBrl));

            if (novoLimiteBrl.Value.Valor <= 0)
                throw new ArgumentOutOfRangeException(nameof(novoLimiteBrl), "NovoLimiteBrl deve ser positivo.");

            if (saldoDevedorAtual.HasValue
                && novoLimiteBrl.Value.Valor < saldoDevedorAtual.Value.Valor)
            {
                throw new InvalidOperationException(
                    $"Novo limite global (BRL {novoLimiteBrl.Value.Valor:F2}) é menor que o saldo devedor atual " +
                    $"(BRL {saldoDevedorAtual.Value.Valor:F2}).");
            }

            if (novoLimiteBrl.Value.Valor != ValorLimiteBrlDecimal)
            {
                var valorAnterior = new Money(ValorLimiteBrlDecimal, Moeda.Brl);
                ValorLimiteBrlDecimal = novoLimiteBrl.Value.Valor;
                _historico.Add(LimiteGlobalBancoHistorico.Criar(
                    limiteGlobalBancoId: Id,
                    valorAnteriorBrl: valorAnterior,
                    valorNovoBrl: novoLimiteBrl.Value,
                    registradoEm: clock.GetCurrentInstant(),
                    observacoes: observacoes));
            }
        }

        LocalDate vigenciaInicio = novaDataVigenciaInicio ?? DataVigenciaInicio;
        LocalDate? vigenciaFim = novaDataVigenciaFim ?? DataVigenciaFim;

        if (vigenciaFim.HasValue && vigenciaFim.Value <= vigenciaInicio)
            throw new ArgumentException(
                "DataVigenciaFim deve ser posterior a DataVigenciaInicio.",
                nameof(novaDataVigenciaFim));

        if (novaDataVigenciaInicio.HasValue) DataVigenciaInicio = novaDataVigenciaInicio.Value;
        if (novaDataVigenciaFim.HasValue) DataVigenciaFim = novaDataVigenciaFim;
        if (observacoes is not null) Observacoes = observacoes;

        UpdatedAt = clock.GetCurrentInstant();
    }

    public void EncerrarVigencia(LocalDate dataFim, IClock clock)
    {
        if (DataVigenciaFim.HasValue)
            throw new InvalidOperationException("Vigência já encerrada.");

        if (dataFim < DataVigenciaInicio)
            throw new ArgumentException(
                "DataFim não pode ser anterior a DataVigenciaInicio.",
                nameof(dataFim));

        DataVigenciaFim = dataFim;
        UpdatedAt = clock.GetCurrentInstant();
    }
}
```

### 7.2. Handler exemplo (`AtualizarLimiteGlobalBancoHandler.cs`)

```csharp
public sealed class AtualizarLimiteGlobalBancoHandler
    : IRequestHandler<AtualizarLimiteGlobalBancoCommand, LimiteGlobalBancoDto>
{
    private readonly ILimiteGlobalBancoRepository _repo;
    private readonly IConsultaSaldoBanco _saldo;
    private readonly IClock _clock;

    public AtualizarLimiteGlobalBancoHandler(
        ILimiteGlobalBancoRepository repo,
        IConsultaSaldoBanco saldo,
        IClock clock)
    {
        _repo = repo;
        _saldo = saldo;
        _clock = clock;
    }

    public async Task<LimiteGlobalBancoDto> Handle(
        AtualizarLimiteGlobalBancoCommand cmd,
        CancellationToken ct)
    {
        var limite = await _repo.GetByIdAsync(cmd.Id, ct)
            ?? throw new InvalidOperationException($"LimiteGlobalBanco {cmd.Id} não encontrado.");

        Money? novoLimite = cmd.ValorLimiteBrl is { } v
            ? new Money(v, Moeda.Brl)
            : null;

        // LG-10: redução exige saldo atual.
        Money? saldoAtual = null;
        if (novoLimite is { } nv && nv.Valor < limite.ValorLimiteBrl.Valor)
        {
            bool perModality = await _saldo.BancoEmRegimePerModalityAsync(limite.BancoId, ct);
            saldoAtual = perModality
                ? await _saldo.CalcularUtilizadoAgregadoModalidadesAsync(limite.BancoId, ct)
                : await _saldo.CalcularSaldoDevedorBancoAsync(limite.BancoId, ct);
        }

        limite.Atualizar(
            clock: _clock,
            novoLimiteBrl: novoLimite,
            novaDataVigenciaInicio: cmd.DataVigenciaInicio,
            novaDataVigenciaFim: cmd.DataVigenciaFim,
            observacoes: cmd.Observacoes,
            saldoDevedorAtual: saldoAtual);

        await _repo.SaveChangesAsync(ct);
        return LimiteGlobalBancoDto.From(limite);
    }
}
```

### 7.3. Regras de estilo aplicáveis

- **Money:** todo `decimal` monetário é encapsulado em `Money`; o domínio expõe `Money`, persiste `decimal` interno como `numeric(20,6)`.
- **Datas:** `LocalDate` para vigências; `Instant` para `CreatedAt`/`UpdatedAt`/`RegistradoEm`.
- **Clock:** sempre injetar `IClock` (NodaTime). Nunca `DateTime.Now` / `DateTime.UtcNow`.
- **Rounding:** `MidpointRounding.AwayFromZero` em qualquer arredondamento (já enforced pelo `Money`).
- **DTOs:** `static From(entity)` pattern (ver commit `d1f5a75`).
- **EF Core:** apenas em `Sgcf.Infrastructure`. Zero atributos EF no domínio.
- **MediatR:** `IRequest<TResponse>` para commands e queries; nomes em português (`CriarLimiteGlobalBancoCommand`).
- **Naming:** domínio em português (`LimiteGlobalBanco`, `Historico`, `ValorLimiteBrl`); técnica em inglês (`Repository`, `Handler`, `Controller`).

---

## 8. Estratégia de Testes

### 8.1. Pirâmide

| Camada               | Quantidade alvo | Foco                                                                              |
| -------------------- | --------------- | --------------------------------------------------------------------------------- |
| **Unit Domain**      | ~20 testes      | Invariantes LG-01..LG-08, histórico, factory, atualização, encerramento           |
| **Unit Application** | ~15 testes      | Handlers com mocks (`ILimiteGlobalBancoRepository`, `IConsultaSaldoBanco`, `IClock`) |
| **Integration**      | ~6 testes       | Repositório + `ConsultaSaldoBancoService` contra PostgreSQL via Testcontainers    |
| **API/E2E**          | ~5 fluxos       | CRUD + cenários A/B via `WebApplicationFactory`                                   |

### 8.2. Testes unitários de domínio (obrigatórios)

| Teste                                                                          | Invariante |
| ------------------------------------------------------------------------------ | ---------- |
| `Criar_ComValorNegativo_LancaArgumentOutOfRangeException`                      | LG-02      |
| `Criar_ComMoedaNaoBrl_LancaArgumentException`                                  | LG-01      |
| `Criar_ComDataFimAnteriorAInicio_LancaArgumentException`                       | LG-03      |
| `Criar_GravaEntradaInicialNoHistoricoComValorAnteriorNull`                     | LG-07      |
| `Atualizar_ReduzindoAbaixoSaldoDevedor_LancaInvalidOperationException`         | LG-06      |
| `Atualizar_AumentandoValor_GravaNovaEntradaHistorico`                          | LG-07      |
| `Atualizar_MesmoValor_NaoGravaEntradaDuplicada`                                | LG-07      |
| `EncerrarVigencia_JaEncerrada_LancaInvalidOperationException`                  | LG-08      |
| `EncerrarVigencia_DataFimAntesInicio_LancaArgumentException`                   | LG-08      |

### 8.3. Testes de integração de Application (Testcontainers)

| Teste                                                                                          | Cenário                  |
| ---------------------------------------------------------------------------------------------- | ------------------------ |
| `CriarLimiteGlobal_ComSomaLimitesBancoMaior_Retorna409`                                        | LG-13                    |
| `AtualizarLimiteGlobal_ReduzindoAbaixoSaldoDevedor_RegimeA_Bloqueia`                           | LG-10 (A)                |
| `AtualizarLimiteGlobal_ReduzindoAbaixoSomaLimitesModalidade_RegimeB_Bloqueia`                  | LG-10 (B)                |
| `CriarLimiteBanco_ExcedendoLimiteGlobal_Bloqueia`                                              | LG-09                    |
| `AtualizarLimiteBanco_ExcedendoLimiteGlobal_Bloqueia`                                          | LG-09                    |
| `CriarLimiteGlobal_ComSobreposicaoDeVigencia_Retorna409`                                       | LG-04, LG-05             |
| `ConsultaSaldoBanco_RegimeA_CalculaSomaContratosAtivos`                                        | §3.4                     |
| `ConsultaSaldoBanco_RegimeB_DetectaPerModalityComUmLimiteBanco`                                | §4.3                     |

Marcados com `[Trait("Category", "Slow")]` para o filtro de loop rápido.

### 8.4. Testes HTTP (`Sgcf.Api.IntegrationTests`)

| Fluxo                                                                                                                                                       |
| ----------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `POST /api/v1/limites-globais-banco` → 201 com body válido; histórico inicial presente.                                                                     |
| `GET /api/v1/bancos/{bancoId}/limite-global-vigente` no Cenário A retorna `regime=GlobalPuro` e `valorUtilizado` = Σ contratos.                              |
| `GET /api/v1/bancos/{bancoId}/limite-global-vigente` no Cenário B retorna `regime=PerModalidade` e `valorUtilizado` = Σ `LimiteBanco.ValorUtilizadoBrl`.     |
| `PATCH` reduzindo abaixo do saldo devedor → 409 com mensagem clara.                                                                                         |
| `POST .../encerrar-vigencia` com data válida → 200 e `dataVigenciaFim` preenchida.                                                                          |
| Tentativa de criar dois limites globais vigentes para mesmo banco → 409.                                                                                    |

### 8.5. Critérios de cobertura

- Domain: ≥ 95% linhas.
- Application: ≥ 85% linhas.
- Infrastructure: ≥ 70% linhas (foco em `ConsultaSaldoBancoService`).
- Zero regressão nos testes existentes da SPEC de Cotações.

---

## 9. Critérios de Sucesso

### 9.1. Cenário A — Banco com apenas limite global

A implementação está correta para o Cenário A quando:

1. Banco sem nenhum `LimiteBanco` (modalidade) registrado tem `IConsultaSaldoBanco.BancoEmRegimePerModalityAsync` retornando `false`.
2. `GET /api/v1/bancos/{bancoId}/limite-global-vigente` retorna `regime = "GlobalPuro"`, `valorUtilizado` = Σ contratos ativos do banco, `valorDisponivel = valorLimite − valorUtilizado` (clamp 0).
3. Criar contrato em qualquer modalidade é permitido enquanto `valorDisponivel ≥ valor do novo contrato`. (Validação LG-12.)
4. Criar contrato que excederia `valorLimite` é bloqueado com erro de domínio.
5. Reduzir o `LimiteGlobalBanco` para valor abaixo da soma de contratos ativos é bloqueado (LG-10/A).
6. Histórico registra entrada inicial e cada alteração subsequente do valor.

### 9.2. Cenário B — Banco com ao menos uma modalidade registrada

A implementação está correta para o Cenário B quando:

1. Banco com ≥ 1 `LimiteBanco` ativo tem `BancoEmRegimePerModalityAsync` retornando `true`.
2. `GET /api/v1/bancos/{bancoId}/limite-global-vigente` retorna `regime = "PerModalidade"`, `valorUtilizado` = Σ `LimiteBanco.ValorUtilizadoBrl` (não soma de contratos diretamente).
3. Criar contrato em modalidade **sem** `LimiteBanco` registrado é bloqueado com mensagem "modalidade X requer LimiteBanco registrado neste banco — regime per-modality" (LG-11).
4. Criar contrato em modalidade com `LimiteBanco` registrado exige disponibilidade simultânea em **dois** níveis: `min(disponivel_modalidade, disponivel_global)`. Se qualquer um for insuficiente → bloqueio.
5. Criar/atualizar `LimiteBanco` cuja soma com os demais limites do banco exceda o `LimiteGlobalBanco` é bloqueado com erro hard (LG-09).
6. Reduzir o `LimiteGlobalBanco` para valor abaixo da soma de `LimiteBanco` ativos é bloqueado (LG-10/B).

### 9.3. Aceitação global da feature

- [ ] Migrations `S33_LimiteGlobalBanco` aplicadas com sucesso em ambiente local e CI.
- [ ] RLS policy ativa nas duas novas tabelas; queries de outro tenant retornam vazio.
- [ ] Cobertura de testes nos limites mínimos (§8.5).
- [ ] Documentação OpenAPI gerada inclui os 6 novos endpoints.
- [ ] Zero regressão no suite completo (`dotnet test`).
- [ ] Validações LG-01..LG-13 todas exercitadas por ao menos um teste automatizado.

---

## 10. Boundaries (Sempre / Pergunte Primeiro / Nunca)

### 10.1. Sempre

- Calcular `ValorUtilizado` dinamicamente via `IConsultaSaldoBanco` — nunca persistir.
- Gravar entrada em `LimiteGlobalBancoHistorico` na criação e em toda alteração de `ValorLimiteBrl`.
- Validar invariantes cruzadas (LG-09, LG-10, LG-11, LG-12, LG-13) na camada Application, dentro do handler.
- Injetar `IClock` (NodaTime) em todo lugar que precise de timestamp.
- Usar `Money` para todo valor monetário (criação, atualização, comparação, retorno).
- Aplicar RLS + EF global filter por `TenantId` nas duas novas tabelas.
- Retornar `409 Conflict` para violações de invariante cruzadas (e não `400`).

### 10.2. Pergunte Primeiro

- Mudar a regra de seleção de regime (Cenário A vs B) — ex.: permitir fallback de modalidade não registrada ao global.
- Permitir sobreposição de vigência (`LG-05`) para representar transições negociadas.
- Adicionar mutabilidade ao `LimiteGlobalBancoHistorico` (hoje append-only).
- Reduzir nível de bloqueio de hard → soft em qualquer invariante.
- Trocar `Money` por `decimal` em qualquer DTO/contrato.
- Calcular `ValorUtilizado` em background e cachear no banco.

### 10.3. Nunca

- Persistir `ValorUtilizado` em `LimiteGlobalBanco` (deve ser sempre computado).
- Permitir mais de um `LimiteGlobalBanco` vigente (sem `DataVigenciaFim`) para o mesmo `(TenantId, BancoId)`.
- Permitir reduzir `ValorLimiteBrl` abaixo do saldo devedor / soma de modalidades.
- Permitir criar/atualizar `LimiteBanco` que faça `Σ modalidades > global`.
- Permitir criar contrato no Cenário B em modalidade sem `LimiteBanco` registrado (sem fallback ao global).
- Apagar registros de `LimiteGlobalBancoHistorico` (append-only).
- Alterar `LimiteGlobalBanco` sem registrar entrada no histórico.
- Usar `DateTime.Now`, `DateTime.UtcNow` ou `DateTimeOffset.UtcNow` em qualquer camada (Domain/Application/Infrastructure).
- Importar `Sgcf.Infrastructure` de `Sgcf.Mcp` ou `Sgcf.A2a`.

---

## 11. Perguntas Abertas

Nenhuma. Todos os requisitos de negócio foram fornecidos pelo PO e estão consolidados acima.

---

## 12. Histórico

| Data       | Versão | Mudança                                                                |
| ---------- | ------ | ---------------------------------------------------------------------- |
| 2026-05-23 | v1.0   | Draft inicial — definição completa do agregado `LimiteGlobalBanco`.    |
| 2026-06-03 | v1.1   | Adicionada seção §3.2-A com a definição formal de "vigente" (Opção A — janela de datas contém hoje). Corrigida §5.2 (`GET /bancos/{bancoId}/limite-global-vigente`): resposta de exemplo atualizada com tipos corretos (`decimal`, não `string`) e referência à nova seção. Atualizada §5.1: descrição do endpoint inclui a semântica de janela. Discrepância com a implementação anterior de `GetVigenteByBancoAsync` (que filtrava apenas por `DataVigenciaFim == null`) está sendo corrigida em correção de bug paralela; esta SPEC registra a semântica correta (Opção A). |
