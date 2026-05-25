# SPEC — Snapshot Temporal de Garantias Exigidas no Contrato

> **Status:** Draft para aprovação
> **Data:** 2026-05-25
> **Autor:** Engenharia de Requisitos (SGCF Backend)
> **Versão:** v1.0
> **Escopo:** Domain + Application + Infrastructure + Api + Tests
> **Dependências:**
> - `SPEC_LIMITE_GLOBAL.md` (S33 — `LimiteGlobalBanco` já entregue)
> - `LimiteBanco`, `GarantiaExigidaLimite` (S5 — entregues)
> - `Contrato`, `Garantia` (S2 — entregues)
> - `ConverterEmContratoCommand` (Cotações — entregue)
> **Investigação de origem:** `PROPOSTA_SNAPSHOT_LIMITE_NO_CONTRATO.md` (este diretório)

---

## 1. Objetivo

### 1.1. O quê

Introduzir **versionamento temporal** das garantias exigidas pelos bancos e **vincular cada `Contrato` à revisão de política vigente** no momento da contratação. Três entregas técnicas indivisíveis:

1. **Versionar `GarantiaExigidaLimite`**: a coleção mutável atual (PATCH replace-all destrutivo) passa a ser organizada em **`GarantiaExigidaRevisao`** com vigência (`vigenciaInicio`/`vigenciaFim`). Cada PATCH na política fecha a revisão vigente e abre uma nova; revisões antigas ficam imutáveis.
2. **Rastreabilidade no `Contrato`**: três FKs novos — `limiteBancoId`, `limiteGlobalBancoId`, `garantiasExigidasRevisaoId` — preenchidos na conversão cotação→contrato.
3. **Enforcement na conversão**: `ConverterEmContratoHandler` passa a validar que cada `GarantiaExigidaItem.Obrigatoria = true` da revisão vigente está coberta pelas garantias declaradas no contrato; conversão é bloqueada se houver lacuna.

### 1.2. Por quê

Bancos alteram política de garantia ao longo de relacionamentos de 15+ anos. Sem versionamento:

- **Lacuna 1** — `LimiteBanco.garantiasExigidas` é destruído a cada PATCH; impossível responder "qual era a política vigente em data X".
- **Lacuna 2** — `Contrato` não sabe sob qual limite foi assinado; auditoria não consegue reconstruir o contexto de aprovação.
- **Lacuna 3** — Sistema não impede a criação de contrato sem garantia quando o banco a exige; depende de disciplina do operador.

Cenário motivador: Santander em 2025 não exigia garantia para FINIMP; em 2026 passa a exigir 30%. Contrato A (2025, sem garantia, R$ 2M) e Contrato B (2026, 30% garantia, R$ 2M) coexistem ativos. O sistema precisa **provar** que a política era diferente em cada momento e **impedir** que B nasça sem garantia hoje.

### 1.3. Personas

| Persona                    | Necessidade                                                                                                       |
| -------------------------- | ----------------------------------------------------------------------------------------------------------------- |
| **Operador de Tesouraria** | Receber bloqueio claro ao tentar converter cotação em contrato sem cobrir garantias obrigatórias.                 |
| **Gerente Financeiro**     | Visualizar no detalhe do contrato a política do banco no momento da contratação, independente de mudanças futuras. |
| **Auditor / Compliance**   | Reconstituir a política vigente do banco em qualquer data passada (BACEN, fiscalização tributária, fiscalização cambial). |
| **Engenharia (Frontend)**  | Consumir um endpoint estável que retorna o snapshot junto do contrato, sem precisar derivar do histórico.          |

### 1.4. Métricas de sucesso

- 0 contratos novos criados sem `garantiasExigidasRevisaoId` quando o banco tem `LimiteBanco` ativo.
- 0 conversões cotação→contrato bem-sucedidas com `GarantiaExigidaItem.Obrigatoria = true` descoberto sem cobertura.
- 100% das alterações em `LimiteBanco.garantiasExigidas` geram nova revisão (auditável).
- Query "política de Santander/FINIMP em 2025-08-15" retorna resultado correto e determinístico.

---

## 2. Comandos

```bash
# Build
dotnet build

# Test (fast loop)
dotnet test --filter "Category!=Slow"

# Test (full suite, inclui Testcontainers)
dotnet test

# Test apenas esta feature
dotnet test --filter "FullyQualifiedName~GarantiaExigidaRevisao|FullyQualifiedName~SnapshotGarantia"

# Adicionar migration
dotnet ef migrations add S34_SnapshotGarantiasContrato \
  --project src/Sgcf.Infrastructure \
  --startup-project src/Sgcf.Api \
  --output-dir Persistence/Migrations

# Aplicar migration (local)
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
┌──────────────────────────────────────────────────────────┐
│                        LimiteBanco                        │
│  (aggregate root — já existe; modificações abaixo)        │
│                                                           │
│  - Id, TenantId, BancoId, Modalidade                      │
│  - ValorLimiteBrl, ValorUtilizadoBrl, ...                 │
│  - Historico: List<LimiteBancoHistorico>   (existente)    │
│  ─────────────────── NOVO ───────────────────             │
│  - RevisoesGarantiasExigidas:                             │
│        List<GarantiaExigidaRevisao>                       │
│  - GarantiasExigidasVigentes (computed):                  │
│        Itens da revisão com VigenciaFim IS NULL           │
│                                                           │
│  PATCH em garantias agora fecha revisão vigente +         │
│  abre nova; nunca apaga itens existentes.                 │
└─────────────────────────┬────────────────────────────────┘
                          │ 1
                          │
                          │ 0..N (append-only)
                          ▼
┌──────────────────────────────────────────────────────────┐
│                GarantiaExigidaRevisao  (NEW)              │
│  (child entity de LimiteBanco; mesma agregação)           │
│                                                           │
│  - Id  (Guid v7)                                          │
│  - TenantId                                               │
│  - LimiteBancoId  (FK pai)                                │
│  - VigenciaInicio (Instant)                               │
│  - VigenciaFim    (Instant?)  ← null = revisão atual      │
│  - RegistradoEm   (Instant)                               │
│  - Motivo         (string?)                               │
│  - Observacoes    (string?)                               │
│  - Itens: List<GarantiaExigidaItem>                       │
└─────────────────────────┬────────────────────────────────┘
                          │ 1
                          │
                          │ 1..N
                          ▼
┌──────────────────────────────────────────────────────────┐
│              GarantiaExigidaItem  (rename)                │
│  (antiga GarantiaExigidaLimite, reparentada)              │
│                                                           │
│  - Id  (Guid v7)                                          │
│  - TenantId                                               │
│  - RevisaoId  (FK; substitui LimiteBancoId)               │
│  - Tipo (TipoGarantia)                                    │
│  - PercentualSobreLimite                                  │
│  - ValorFixoBrl                                           │
│  - Obrigatoria                                            │
│  - Observacoes                                            │
│  - CreatedAt, UpdatedAt                                   │
│                                                           │
│  IMUTÁVEL após a revisão pai fechar a vigência.           │
└──────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────┐
│                         Contrato                           │
│  (aggregate root — já existe; modificações abaixo)        │
│                                                           │
│  - Id, TenantId, NumeroExterno, BancoId, Modalidade...    │
│  - Garantias: List<Garantia>  (existente, snapshot próprio)│
│  ─────────────────── NOVOS CAMPOS ───────────────────     │
│  - LimiteBancoId               (Guid?)                    │
│  - LimiteGlobalBancoId         (Guid?)                    │
│  - GarantiasExigidasRevisaoId  (Guid?)                    │
│                                                           │
│  Todos NULLABLE — preenchidos na conversão cotação→       │
│  contrato; ficam NULL para contratos pré-feature ou       │
│  criados diretamente sem LimiteBanco cadastrado.          │
│                                                           │
│  IMUTÁVEIS após persistência — domain layer não expõe     │
│  setters; PATCH no contrato não toca esses campos.        │
└──────────────────────────────────────────────────────────┘
```

### 3.2. Modificações no `LimiteBanco`

Adiciona uma coleção privada de revisões e revisa a semântica de manipulação de garantias exigidas:

```csharp
private readonly List<GarantiaExigidaRevisao> _revisoesGarantias = new();
public IReadOnlyCollection<GarantiaExigidaRevisao> RevisoesGarantiasExigidas
    => _revisoesGarantias.AsReadOnly();

/// <summary>
/// Revisão de garantias vigente (VigenciaFim IS NULL).
/// Null se nunca houve revisão cadastrada (banco sem política formal).
/// </summary>
public GarantiaExigidaRevisao? RevisaoGarantiasVigente
    => _revisoesGarantias.SingleOrDefault(r => r.VigenciaFim is null);

/// <summary>
/// Itens da revisão vigente. Coleção vazia se não houver revisão.
/// Mantém o nome <c>GarantiasExigidas</c> por compatibilidade da API existente.
/// </summary>
public IReadOnlyCollection<GarantiaExigidaItem> GarantiasExigidas
    => RevisaoGarantiasVigente?.Itens ?? Array.Empty<GarantiaExigidaItem>();
```

Métodos públicos atualizados (mesma assinatura externa, semântica nova internamente):

```csharp
/// <summary>
/// Substitui as garantias exigidas: fecha a revisão vigente (se houver)
/// e abre uma nova com os itens fornecidos. Append-only.
/// </summary>
public void SubstituirGarantiasExigidas(
    IEnumerable<GarantiaExigidaItemSpec> novas,
    IClock clock,
    string? motivo = null,
    string? observacoes = null);

/// <summary>
/// Adiciona uma garantia exigida. Comportamento: fecha revisão vigente,
/// cria nova revisão com (itens da anterior + novo item).
/// </summary>
public void AdicionarGarantiaExigida(
    GarantiaExigidaItemSpec spec,
    IClock clock,
    string? motivo = null);

/// <summary>
/// Remove uma garantia exigida pelo Tipo. Fecha revisão vigente e cria nova
/// com (itens da anterior − tipo informado). Remoção por Id é descontinuada
/// porque um Id pertence a uma revisão específica que pode estar fechada.
/// </summary>
public void RemoverGarantiaExigidaPorTipo(
    TipoGarantia tipo,
    IClock clock,
    string? motivo = null);
```

**Rename do descritor:** `GarantiaExigidaLimiteSpec` → `GarantiaExigidaItemSpec`. Mesma estrutura (`Tipo`, `PercentualSobreLimite`, `ValorFixoBrl`, `Obrigatoria`, `Observacoes`).

### 3.3. Entidade `GarantiaExigidaRevisao` (NOVA)

| Campo            | Tipo                                | Notas                                                                          |
| ---------------- | ----------------------------------- | ------------------------------------------------------------------------------ |
| `Id`             | `Guid` (v7)                         | PK                                                                             |
| `TenantId`       | `Guid`                              | Preenchido por `TenantSaveInterceptor`                                         |
| `LimiteBancoId`  | `Guid`                              | FK → `limite_banco.id` (Cascade)                                               |
| `VigenciaInicio` | `Instant`                           | Quando a revisão entrou em vigor (= `clock.GetCurrentInstant()` na criação)    |
| `VigenciaFim`    | `Instant?`                          | `null` enquanto vigente; setado quando uma nova revisão substitui esta         |
| `RegistradoEm`   | `Instant`                           | Quando foi gravada (= VigenciaInicio na maioria dos casos)                     |
| `Motivo`         | `string?` (≤ 256)                   | Texto livre — ex.: "Renegociação 2026-06", "Comitê de risco aprovou redução"   |
| `Observacoes`    | `string?` (≤ 1024)                  | Texto livre adicional                                                          |
| `Itens`          | `IReadOnlyCollection<GarantiaExigidaItem>` | Lista privada `_itens` exposta read-only; mínimo 0, sem máximo lógico   |

Factory:

```csharp
internal static GarantiaExigidaRevisao Criar(
    Guid limiteBancoId,
    IEnumerable<GarantiaExigidaItemSpec> itens,
    IClock clock,
    string? motivo = null,
    string? observacoes = null);
```

Métodos internos (chamados apenas pelo agregado `LimiteBanco`):

```csharp
internal void EncerrarVigencia(IClock clock);
```

Invariantes (ver §4.1 para SR-01..SR-08).

### 3.4. Entidade `GarantiaExigidaItem` (renomeada)

Renomeada de `GarantiaExigidaLimite`. Mudanças estruturais:

- `LimiteBancoId` é **substituído** por `RevisaoId` (FK → `garantia_exigida_revisao.id`).
- Após a revisão pai fechar (`VigenciaFim != null`), o item torna-se imutável: chamar `Atualizar(...)` lança `InvalidOperationException`.
- Demais campos permanecem idênticos.

Justificativa de rename: o nome anterior sugeria pertencimento direto ao `LimiteBanco`; o novo evidencia o papel como item dentro de uma revisão. Custo: rename amplo na codebase (~12 arquivos). Benefício: clareza de domínio e prevenção de pensamento "uma garantia por limite".

### 3.5. Modificações no `Contrato`

Três novos campos privados-set, expostos read-only e setados apenas em conversão cotação→contrato:

```csharp
public Guid? LimiteBancoId { get; private set; }
public Guid? LimiteGlobalBancoId { get; private set; }
public Guid? GarantiasExigidasRevisaoId { get; private set; }
```

Método interno chamado pelo handler de conversão (não exposto fora do agregado):

```csharp
/// <summary>
/// Vincula o contrato à política vigente do banco no momento da conversão.
/// Idempotente quando os valores são iguais; lança se já vinculado a valores diferentes
/// (snapshot imutável).
/// </summary>
internal void VincularPoliticaBanco(
    Guid? limiteBancoId,
    Guid? limiteGlobalBancoId,
    Guid? garantiasExigidasRevisaoId);
```

Regra de imutabilidade: uma vez setado um campo não-nulo, `VincularPoliticaBanco` com valor diferente lança `InvalidOperationException`. Permite re-chamada idempotente com mesmos valores (defensive coding em retries).

### 3.6. Notas críticas

- **Append-only**: `GarantiaExigidaRevisao` e `GarantiaExigidaItem` nunca são apagados. Mesmo deleção de `LimiteBanco` (improvável; hoje não é exposta) seria configurada com `Restrict` se viesse a existir.
- **Vigência por `Instant`, não `LocalDate`**: revisões podem nascer e morrer no mesmo dia (ex.: correção de PATCH errado). Precisão de timestamp evita ambiguidade.
- **Unicidade da vigente**: no máximo 1 revisão com `VigenciaFim IS NULL` por `(TenantId, LimiteBancoId)` — enforçado por índice único parcial no banco e por invariante no agregado.
- **Vigência da revisão e vigência do limite são independentes**: a vigência do `LimiteBanco` (`DataVigenciaInicio`/`Fim`) é negocial (período do limite). A vigência da revisão (`VigenciaInicio`/`Fim`) é a janela em que aquela política de garantias esteve em vigor — pode ter múltiplas revisões dentro de uma única vigência de limite.

---

## 4. Regras de Validação (Invariantes)

### 4.1. Invariantes do agregado `GarantiaExigidaRevisao` (SR — Snapshot Revisão)

| #     | Regra                                                                                                                    | Comportamento se violada      |
| ----- | ------------------------------------------------------------------------------------------------------------------------ | ----------------------------- |
| SR-01 | `LimiteBancoId` não pode ser `Guid.Empty`                                                                                | `ArgumentException`           |
| SR-02 | `VigenciaInicio` é obrigatório e definido pelo `IClock` no momento da criação                                            | (não aplicável; preenchido pela factory) |
| SR-03 | `VigenciaFim` é `null` na criação; só pode ser definido uma vez via `EncerrarVigencia`                                   | `InvalidOperationException`   |
| SR-04 | `VigenciaFim`, quando setado, deve ser `>=` `VigenciaInicio`                                                             | `ArgumentException`           |
| SR-05 | Itens são imutáveis após `EncerrarVigencia` ser chamado                                                                  | `InvalidOperationException`   |
| SR-06 | Não pode haver dois itens com o mesmo `Tipo` na mesma revisão                                                            | `InvalidOperationException`   |
| SR-07 | Itens herdam validação atual de `GarantiaExigidaLimite` (campos percentual/valor fixo exclusivos; Aval pode ter ambos nulos) | `ArgumentException`        |
| SR-08 | Uma revisão pode nascer com `Itens` vazios (significa "política sem exigências")                                         | Permitido                     |

### 4.2. Invariantes do agregado `LimiteBanco` relativas a revisões (SLB)

| #      | Regra                                                                                                                                | Comportamento se violada       |
| ------ | ------------------------------------------------------------------------------------------------------------------------------------ | ------------------------------ |
| SLB-01 | No máximo uma `GarantiaExigidaRevisao` com `VigenciaFim IS NULL` por `LimiteBancoId`                                                 | Índice único parcial + invariante (`InvalidOperationException`) |
| SLB-02 | `SubstituirGarantiasExigidas` / `AdicionarGarantiaExigida` / `RemoverGarantiaExigidaPorTipo`: se há revisão vigente, fecha antes de abrir nova | Comportamento normal (transacional) |
| SLB-03 | A nova revisão recebe `VigenciaInicio = clock.GetCurrentInstant()` exatamente igual ao `VigenciaFim` da anterior (continuidade)      | Garantido pela factory         |
| SLB-04 | `Substituir...` com lista idêntica à atual **não** cria nova revisão (idempotência por valor)                                        | Comportamento defensivo        |
| SLB-05 | Histórico de revisões é ordenado por `VigenciaInicio` ascendente na exposição                                                        | Garantido pela query           |

### 4.3. Invariantes cruzadas com `Contrato` (SC — Snapshot Contrato)

| #     | Regra                                                                                                                                                                                   | Onde validar                       | Bloqueio |
| ----- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------- | -------- |
| SC-01 | Em conversão cotação→contrato: se o banco tem `LimiteBanco` ativo para a modalidade, `Contrato.LimiteBancoId` deve ser preenchido com esse Id                                            | `ConverterEmContratoHandler`       | Hard     |
| SC-02 | Em conversão: se existe `LimiteGlobalBanco` vigente para o banco, `Contrato.LimiteGlobalBancoId` deve ser preenchido                                                                     | `ConverterEmContratoHandler`       | Hard     |
| SC-03 | Em conversão: se há revisão vigente em `LimiteBanco`, `Contrato.GarantiasExigidasRevisaoId` deve ser preenchido com essa revisão                                                         | `ConverterEmContratoHandler`       | Hard     |
| SC-04 | **Enforcement (Lacuna 3):** para cada item da revisão com `Obrigatoria = true`, o contrato deve ter ao menos uma `Garantia` com `Tipo` compatível e `ValorBrl >= valor esperado` (ver §4.4) | `ConverterEmContratoHandler`       | Hard     |
| SC-05 | `Contrato.LimiteBancoId`, `LimiteGlobalBancoId` e `GarantiasExigidasRevisaoId` são imutáveis após criação. `UpdateContratoHandler` não modifica esses campos                              | `Contrato.Atualizar` (domain)      | Hard     |
| SC-06 | Contratos pré-feature retornam `NULL` nos três campos. Frontend / consumidores devem tratar como "legado / não rastreado"                                                                | Comportamento natural (sem backfill) | —        |
| SC-07 | Contratos criados em banco/modalidade sem `LimiteBanco` cadastrado retornam `NULL` em `LimiteBancoId` e `GarantiasExigidasRevisaoId`. Enforcement SC-04 não se aplica (não há política a validar) | `ConverterEmContratoHandler` | —    |

### 4.4. Cálculo de "valor esperado" para enforcement SC-04

Para cada `GarantiaExigidaItem` com `Obrigatoria = true`:

| Item tem...                 | Valor esperado para cobertura                                                  |
| --------------------------- | ------------------------------------------------------------------------------ |
| `PercentualSobreLimite`     | `valor = ContratoValorPrincipalBrl × PercentualSobreLimite / 100`              |
| `ValorFixoBrl`              | `valor = ValorFixoBrl`                                                         |
| Aval (ambos nulos)          | Cobertura é satisfeita se houver `Garantia` do tipo `Aval`, independente do valor |

Conversão do valor do contrato para BRL: usa a cotação de fechamento da `Cotacao` que está sendo convertida (já existe na pipeline). Se o contrato é em BRL, comparação direta.

Reaproveitar **`CalculadorValorGarantiaExigida`** (existe em `Sgcf.Application/Cotacoes/`) — ele já implementa essa lógica para a feature de "preenchimento automático de garantia em cotação". A spec exige que a mesma calculadora seja usada na conversão para garantir consistência (mesma fórmula = mesmo resultado).

Agregação de cobertura: somar `Garantia.ValorBrl` por `Tipo` no contrato e comparar com o valor esperado por tipo. Cobertura **parcial** não satisfaz item obrigatório — bloqueia. Cobertura **excedente** é permitida.

### 4.5. Mensagens de erro padronizadas (SC-04)

Formato JSON do erro retornado pelo endpoint de conversão quando SC-04 falha:

```json
{
  "type": "https://sgcf.io/errors/garantia-exigida-nao-coberta",
  "title": "Garantias exigidas pela política do banco não foram cobertas pelo contrato.",
  "status": 409,
  "detail": "A revisão vigente do LimiteBanco {limiteBancoId} exige 2 garantia(s) obrigatória(s) que não foram supridas.",
  "limiteBancoId": "uuid",
  "garantiasExigidasRevisaoId": "uuid",
  "lacunas": [
    {
      "tipo": "Cdb",
      "obrigatoria": true,
      "valorEsperadoBrl": "600000.00",
      "valorCobertoBrl": "0.00"
    },
    {
      "tipo": "Aval",
      "obrigatoria": true,
      "valorEsperadoBrl": null,
      "valorCobertoBrl": null
    }
  ]
}
```

---

## 5. Endpoints da API

### 5.1. Rotas afetadas

| Verbo   | Rota                                                            | Mudança                                                                                  | Roles    |
| ------- | --------------------------------------------------------------- | ---------------------------------------------------------------------------------------- | -------- |
| `PATCH` | `/api/v1/limites-banco/{id}`                                    | **Semântica nova:** `garantiasExigidas` PATCH agora fecha revisão e abre nova. Mesma assinatura externa. | Admin    |
| `POST`  | `/api/v1/limites-banco/{id}/garantias-exigidas`                 | Mesma semântica de criação aditiva, agora gera nova revisão.                              | Admin    |
| `DELETE`| `/api/v1/limites-banco/{id}/garantias-exigidas/{itemId}`        | **Descontinuado.** Substituído por `DELETE /.../garantias-exigidas?tipo=X` (remove por tipo, abre nova revisão). | Admin    |
| `DELETE`| `/api/v1/limites-banco/{id}/garantias-exigidas?tipo=X`          | **Novo.** Remove o item do tipo informado da revisão vigente; abre nova revisão.          | Admin    |
| `GET`   | `/api/v1/limites-banco/{id}`                                    | **Inalterado** no shape; `garantiasExigidas` continua retornando os itens da revisão vigente. | Operador |
| `GET`   | `/api/v1/limites-banco/{id}/revisoes-garantias`                 | **Novo.** Lista todas as revisões (vigentes e fechadas) com `itens[]`.                    | Operador |
| `GET`   | `/api/v1/contratos/{id}`                                        | **Estendido.** Inclui `limiteBancoId`, `limiteGlobalBancoId`, `garantiasExigidasRevisaoId`, `garantiasExigidasSnapshot[]`. | Operador |
| `GET`   | `/api/v1/contratos`                                             | **Estendido.** Inclui as 3 FKs; **não** inclui `garantiasExigidasSnapshot[]` (payload pesado). | Operador |
| `POST`  | `/api/v1/cotacoes/{id}/converter`                               | **Comportamento novo:** preenche as 3 FKs no contrato + valida SC-04. Retorna `409` se enforcement falhar. | Operador |

### 5.2. Contratos selecionados

**`POST /api/v1/cotacoes/{id}/converter` — resposta de erro SC-04**

Status `409 Conflict`. Body conforme §4.5. Nenhuma escrita ocorre (transação rollback).

**`GET /api/v1/contratos/{id}` — body estendido (extrato dos campos novos)**

```json
{
  "id": "uuid",
  "numeroExterno": "FINIMP-001",
  "bancoId": "uuid",
  "modalidade": "Finimp",
  "valorPrincipal": "2000000.00",
  "limiteBancoId": "uuid",
  "limiteGlobalBancoId": "uuid",
  "garantiasExigidasRevisaoId": "uuid",
  "garantiasExigidasSnapshot": [
    {
      "tipo": "Cdb",
      "percentualSobreLimite": 30.0,
      "valorFixoBrl": null,
      "obrigatoria": true,
      "observacoes": "30% mínimo aprovado em comitê 2026-04"
    }
  ],
  "garantias": [ /* shape atual de Garantia, snapshot próprio do contrato */ ]
}
```

**`GET /api/v1/limites-banco/{id}/revisoes-garantias` — body**

```json
{
  "limiteBancoId": "uuid",
  "revisoes": [
    {
      "id": "uuid",
      "vigenciaInicio": "2025-03-15T13:22:08Z",
      "vigenciaFim": "2026-02-01T09:11:43Z",
      "registradoEm": "2025-03-15T13:22:08Z",
      "motivo": "Política inicial",
      "observacoes": null,
      "itens": [ /* GarantiaExigidaItemDto[] */ ]
    },
    {
      "id": "uuid",
      "vigenciaInicio": "2026-02-01T09:11:43Z",
      "vigenciaFim": null,
      "registradoEm": "2026-02-01T09:11:43Z",
      "motivo": "Renegociação anual — exige 30% CDB",
      "observacoes": null,
      "itens": [ /* ... */ ]
    }
  ]
}
```

Ordem: `VigenciaInicio` ascendente (mais antiga primeiro).

`GarantiaExigidaSnapshotItemDto` (no contrato) tem o mesmo shape de `GarantiaExigidaItemDto` mas **sem** `id`, `createdAt`, `updatedAt` — é uma projeção imutável que reflete o estado da revisão vinculada.

### 5.3. Compatibilidade

- `GET /limites-banco/{id}` permanece com shape idêntico ao atual. Frontend / consumidores não quebram.
- `PATCH /limites-banco/{id}` mantém a mesma assinatura. Mudança é apenas semântica interna.
- `DELETE /limites-banco/{id}/garantias-exigidas/{itemId}` é **descontinuado**. Plano de descontinuação:
  - **Fase 1 (esta SPEC):** endpoint passa a retornar `410 Gone` com `Location: /limites-banco/{id}/garantias-exigidas?tipo=X`.
  - **Fase 2 (próxima onda, fora desta SPEC):** endpoint é removido.

---

## 6. Estrutura de Arquivos

### 6.1. Árvore de mudanças

```
src/
├── Sgcf.Domain/
│   ├── Cotacoes/
│   │   ├── LimiteBanco.cs                              (MODIFY — métodos de garantia revisados)
│   │   ├── GarantiaExigidaRevisao.cs                   (NEW)
│   │   ├── GarantiaExigidaItem.cs                      (RENAME de GarantiaExigidaLimite)
│   │   └── GarantiaExigidaItemSpec.cs                  (RENAME de GarantiaExigidaLimiteSpec)
│   └── Contratos/
│       └── Contrato.cs                                 (MODIFY — 3 campos + VincularPoliticaBanco)
│
├── Sgcf.Application/
│   ├── Cotacoes/
│   │   ├── GarantiaExigidaItemDto.cs                   (RENAME de GarantiaExigidaLimiteDto.cs)
│   │   ├── GarantiaExigidaRevisaoDto.cs                (NEW)
│   │   ├── CriarGarantiaExigidaItemRequest.cs          (RENAME de CriarGarantiaExigidaLimiteRequest.cs)
│   │   ├── CalculadorValorGarantiaExigida.cs           (REUSE — sem alteração estrutural)
│   │   ├── Commands/
│   │   │   ├── ConverterEmContratoCommand.cs           (MODIFY — handler valida SC-04 e preenche FKs)
│   │   │   ├── ConverterEmContratoHandler.cs           (MODIFY — lógica de enforcement)
│   │   │   ├── CreateLimiteBancoCommand.cs             (MODIFY — passa por nova revisão na criação)
│   │   │   ├── UpdateLimiteBancoCommand.cs             (MODIFY — passa por nova revisão no PATCH)
│   │   │   └── (existentes para adicionar/remover/substituir garantias — assinaturas inalteradas; semântica nova)
│   │   ├── Queries/
│   │   │   ├── ListarRevisoesGarantiasQuery.cs         (NEW)
│   │   │   └── ListarRevisoesGarantiasHandler.cs       (NEW)
│   │   └── ILimiteBancoRepository.cs                   (MODIFY — método para query de revisões)
│   └── Contratos/
│       ├── ContratoDto.cs                              (MODIFY — 3 campos novos + GarantiasExigidasSnapshot[])
│       └── GarantiaExigidaSnapshotItemDto.cs           (NEW)
│
├── Sgcf.Infrastructure/
│   └── Persistence/
│       ├── Configurations/
│       │   ├── LimiteBancoConfiguration.cs             (MODIFY — navegação para Revisoes)
│       │   ├── GarantiaExigidaRevisaoConfiguration.cs  (NEW)
│       │   ├── GarantiaExigidaItemConfiguration.cs     (RENAME de GarantiaExigidaLimiteConfiguration.cs)
│       │   └── ContratoConfiguration.cs                (MODIFY — 3 FKs + Restrict ON DELETE)
│       ├── Repositories/
│       │   └── LimiteBancoRepository.cs                (MODIFY — eager-load Revisoes.Itens onde necessário)
│       └── Migrations/
│           └── 20260526..._S34_SnapshotGarantiasContrato.cs   (NEW — auto-generated)
│
├── Sgcf.Api/
│   └── Controllers/
│       ├── LimitesBancoController.cs                   (MODIFY — endpoint `GET /revisoes-garantias`, DELETE por tipo, DELETE por id → 410)
│       └── ContratosController.cs                      (MODIFY — DTO de retorno inclui campos novos)
│
└── Sgcf.Mcp/
    └── Tools/ContratoTools.cs                          (MODIFY — expor campos novos onde já expõe ContratoDto)

tests/
├── Sgcf.Domain.Tests/
│   └── Cotacoes/
│       ├── GarantiaExigidaRevisaoTests.cs              (NEW — invariantes SR-01..SR-08)
│       ├── LimiteBancoRevisoesTests.cs                 (NEW — invariantes SLB-01..SLB-05)
│       ├── GarantiaExigidaItemImutabilidadeTests.cs    (NEW — SR-05)
│       └── ContratoVincularPoliticaBancoTests.cs       (NEW — SC-05 imutabilidade)
│
├── Sgcf.Application.Tests/
│   └── Cotacoes/
│       ├── ConverterEmContratoEnforcementTests.cs      (NEW — SC-04 cenários cobertura completa/parcial/zero)
│       ├── ConverterEmContratoFKsTests.cs              (NEW — SC-01, SC-02, SC-03)
│       ├── LimiteBancoPatchAbreRevisaoTests.cs         (NEW — fluxo end-to-end via handler)
│       └── ListarRevisoesGarantiasHandlerTests.cs      (NEW)
│
└── Sgcf.Api.IntegrationTests/
    ├── SnapshotGarantiasEndpointsTests.cs              (NEW — GET /revisoes, contrato GET inclui snapshot)
    └── ConverterEmContratoEnforcementHttpTests.cs      (NEW — 409 com body §4.5)

docs/specs/limites-banco/
├── SPEC_SNAPSHOT_LIMITE_NO_CONTRATO.md                 (este arquivo)
└── PROPOSTA_SNAPSHOT_LIMITE_NO_CONTRATO.md             (histórico de investigação — não-actionable)
```

### 6.2. Tabelas PostgreSQL (schema `sgcf`)

| Tabela                          | Mudança                                                                                                                       |
| ------------------------------- | ----------------------------------------------------------------------------------------------------------------------------- |
| `garantia_exigida_revisao`      | **NEW.** PK `id`, `tenant_id`, `limite_banco_id` (FK Cascade), `vigencia_inicio`, `vigencia_fim NULL`, `registrado_em`, `motivo`, `observacoes`. UQ parcial `(tenant_id, limite_banco_id) WHERE vigencia_fim IS NULL`. RLS por `tenant_id`. |
| `garantia_exigida_limite`       | **RENAME → `garantia_exigida_item`.** `limite_banco_id` removido; `revisao_id` adicionado (FK Cascade). Demais colunas inalteradas. RLS preservada. |
| `contrato`                      | **ALTER.** Adiciona `limite_banco_id` (FK → `limite_banco.id`, ON DELETE SET NULL), `limite_global_banco_id` (FK → `limite_global_banco.id`, ON DELETE SET NULL), `garantias_exigidas_revisao_id` (FK → `garantia_exigida_revisao.id`, ON DELETE SET NULL). Todos NULLABLE. |

### 6.3. Migration `S34_SnapshotGarantiasContrato`

Conteúdo da migration EF Core (geração manual após edit de configurations):

1. `CREATE TABLE sgcf.garantia_exigida_revisao (...)`.
2. `ALTER TABLE sgcf.garantia_exigida_limite ADD COLUMN revisao_id uuid` (nullable inicialmente).
3. **Data migration in-migration** (em `Up`, antes de tornar `revisao_id` NOT NULL):
   - Para cada `limite_banco` que tem ≥ 1 `garantia_exigida_limite`, inserir uma `garantia_exigida_revisao` (id v7, tenant_id do limite, limite_banco_id, vigencia_inicio = `limite_banco.created_at`, vigencia_fim = NULL, registrado_em = `limite_banco.created_at`, motivo = `'Revisão inicial gerada pela migration S34'`).
   - `UPDATE garantia_exigida_limite SET revisao_id = (subquery) WHERE limite_banco_id = ...`.
4. `ALTER TABLE garantia_exigida_limite ALTER COLUMN revisao_id SET NOT NULL`.
5. `ALTER TABLE garantia_exigida_limite DROP COLUMN limite_banco_id` (FK + coluna).
6. `ALTER TABLE garantia_exigida_limite RENAME TO garantia_exigida_item`.
7. `ALTER TABLE contrato ADD COLUMN limite_banco_id uuid NULL`, `limite_global_banco_id uuid NULL`, `garantias_exigidas_revisao_id uuid NULL` — todos com FK (`ON DELETE SET NULL`).
8. Criar índices: UQ parcial em `garantia_exigida_revisao(tenant_id, limite_banco_id) WHERE vigencia_fim IS NULL`; índices não-únicos nas 3 FKs do contrato.
9. RLS policies em `garantia_exigida_revisao` (mesma fórmula das demais tabelas: `USING (tenant_id = current_setting('app.tenant_id', true)::uuid)`).
10. **`Down`** reverte na ordem inversa, **com perda de dados** — documentar no header da migration que o rollback após uso em produção é destrutivo. Não fornecemos suporte a `Down` em produção; apenas em dev.

### 6.4. Plano de migração de dados (produção)

Política: **sem janela de manutenção**.

- A migration é aditiva no schema de `contrato` (apenas colunas NULL).
- O rename `garantia_exigida_limite → garantia_exigida_item` ocorre na mesma transação que cria as revisões; tempo total estimado < 5s mesmo com 100k rows (operação metadata + INSERT linear).
- API permanece operacional durante a migration; deploy em ordem:
  1. Aplicar migration (servidor em modo manutenção curta de leitura — apenas durante o ALTER TABLE).
  2. Deploy do binário novo que consome `garantia_exigida_item` e `garantia_exigida_revisao`.
- Validação pós-migration: query `SELECT COUNT(*) FROM garantia_exigida_item WHERE revisao_id IS NULL` deve retornar 0.

---

## 7. Estilo de Código

Segue `CLAUDE.md` (Money, NodaTime, `AwayFromZero`, layers).

### 7.1. Entity esqueleto — `GarantiaExigidaRevisao.cs`

```csharp
using NodaTime;
using Sgcf.Domain.Auditoria;
using Sgcf.Domain.Common;
using Sgcf.Domain.Tenancy;

namespace Sgcf.Domain.Cotacoes;

/// <summary>
/// Revisão temporal das garantias exigidas por um <see cref="LimiteBanco"/>.
/// Append-only: cada PATCH na política do banco fecha a revisão vigente e
/// abre uma nova. Itens da revisão tornam-se imutáveis após VigenciaFim.
/// SPEC §3.3.
/// </summary>
public sealed class GarantiaExigidaRevisao : Entity, IAuditable, ITenantScoped
{
    public Guid TenantId { get; private set; }
    public Guid LimiteBancoId { get; private set; }
    public Instant VigenciaInicio { get; private set; }
    public Instant? VigenciaFim { get; private set; }
    public Instant RegistradoEm { get; private set; }
    public string? Motivo { get; private set; }
    public string? Observacoes { get; private set; }

    public Instant CreatedAt { get; private set; }
    public Instant UpdatedAt { get; private set; }

    private readonly List<GarantiaExigidaItem> _itens = new();
    public IReadOnlyCollection<GarantiaExigidaItem> Itens => _itens.AsReadOnly();

    public bool EstaVigente => VigenciaFim is null;

    private GarantiaExigidaRevisao() { }

    internal static GarantiaExigidaRevisao Criar(
        Guid limiteBancoId,
        IEnumerable<GarantiaExigidaItemSpec> itens,
        IClock clock,
        string? motivo = null,
        string? observacoes = null)
    {
        if (limiteBancoId == Guid.Empty)
        {
            throw new ArgumentException("LimiteBancoId não pode ser vazio.", nameof(limiteBancoId));
        }

        var now = clock.GetCurrentInstant();
        var revisao = new GarantiaExigidaRevisao
        {
            LimiteBancoId = limiteBancoId,
            VigenciaInicio = now,
            VigenciaFim = null,
            RegistradoEm = now,
            Motivo = motivo,
            Observacoes = observacoes,
            CreatedAt = now,
            UpdatedAt = now,
        };

        foreach (var spec in itens)
        {
            revisao.AdicionarItemInterno(spec, clock);
        }

        return revisao;
    }

    internal void EncerrarVigencia(IClock clock)
    {
        if (VigenciaFim is not null)
        {
            throw new InvalidOperationException(
                $"Revisão {Id} já encerrada em {VigenciaFim}.");
        }

        var now = clock.GetCurrentInstant();
        if (now < VigenciaInicio)
        {
            throw new ArgumentException(
                "Instante atual é anterior a VigenciaInicio — clock invariante violado.");
        }

        VigenciaFim = now;
        UpdatedAt = now;
    }

    private void AdicionarItemInterno(GarantiaExigidaItemSpec spec, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(spec);

        if (_itens.Any(i => i.Tipo == spec.Tipo))
        {
            throw new InvalidOperationException(
                $"Garantia exigida do tipo {spec.Tipo} já está cadastrada (duplicada) na revisão {Id}.");
        }

        _itens.Add(GarantiaExigidaItem.Criar(
            revisaoId: Id,
            tipo: spec.Tipo,
            percentualSobreLimite: spec.PercentualSobreLimite,
            valorFixoBrl: spec.ValorFixoBrl,
            obrigatoria: spec.Obrigatoria,
            observacoes: spec.Observacoes,
            clock: clock));
    }
}
```

### 7.2. Trecho — `LimiteBanco.SubstituirGarantiasExigidas` (revisado)

```csharp
public void SubstituirGarantiasExigidas(
    IEnumerable<GarantiaExigidaItemSpec> novas,
    IClock clock,
    string? motivo = null,
    string? observacoes = null)
{
    ArgumentNullException.ThrowIfNull(novas);

    var listaNova = novas.ToList();
    ValidarSemDuplicadosPorTipo(listaNova);

    // SLB-04: idempotência por valor — se a lista nova é equivalente à vigente, não cria revisão.
    var vigente = RevisaoGarantiasVigente;
    if (vigente is not null && PoliticasEquivalentes(vigente.Itens, listaNova))
    {
        return;
    }

    // SLB-02 + SLB-03: fecha a vigente e abre nova com mesmo Instant.
    vigente?.EncerrarVigencia(clock);

    var novaRevisao = GarantiaExigidaRevisao.Criar(
        limiteBancoId: Id,
        itens: listaNova,
        clock: clock,
        motivo: motivo,
        observacoes: observacoes);

    _revisoesGarantias.Add(novaRevisao);
    UpdatedAt = clock.GetCurrentInstant();
}

private static bool PoliticasEquivalentes(
    IReadOnlyCollection<GarantiaExigidaItem> atuais,
    IReadOnlyCollection<GarantiaExigidaItemSpec> novas)
{
    if (atuais.Count != novas.Count) return false;

    var porTipoAtuais = atuais.ToDictionary(i => i.Tipo);
    foreach (var nova in novas)
    {
        if (!porTipoAtuais.TryGetValue(nova.Tipo, out var atual)) return false;
        if (atual.PercentualSobreLimite != nova.PercentualSobreLimite) return false;
        if (atual.ValorFixoBrl?.Valor != nova.ValorFixoBrl?.Valor) return false;
        if (atual.Obrigatoria != nova.Obrigatoria) return false;
        if (atual.Observacoes != nova.Observacoes) return false;
    }
    return true;
}
```

### 7.3. Handler — `ConverterEmContratoHandler` (trecho de enforcement)

```csharp
// Pseudocódigo — ver implementação final na fase de build.

var limite = await _limitesBanco.GetByBancoModalidadeAsync(
    bancoId: cotacao.BancoVencedor.Id,
    modalidade: cotacao.Modalidade,
    ct);

var limiteGlobal = await _limitesGlobais.GetVigenteByBancoAsync(
    bancoId: cotacao.BancoVencedor.Id,
    ct);

GarantiaExigidaRevisao? revisao = limite?.RevisaoGarantiasVigente;

if (revisao is not null)
{
    var lacunas = AvaliarCobertura(
        itensObrigatorios: revisao.Itens.Where(i => i.Obrigatoria),
        garantiasDoContrato: garantiasInformadasNoCommand,
        valorPrincipalBrl: cmd.ValorPrincipalBrl);

    if (lacunas.Count > 0)
    {
        throw new GarantiaExigidaNaoCobertaException(
            limiteBancoId: limite!.Id,
            garantiasExigidasRevisaoId: revisao.Id,
            lacunas: lacunas);
    }
}

var contrato = Contrato.Criar(/* ... */);
contrato.VincularPoliticaBanco(
    limiteBancoId: limite?.Id,
    limiteGlobalBancoId: limiteGlobal?.Id,
    garantiasExigidasRevisaoId: revisao?.Id);
```

`GarantiaExigidaNaoCobertaException` é mapeada para `409 Conflict` no `ExceptionHandlingMiddleware` (já existe; só adicionar entrada).

### 7.4. Regras de estilo aplicáveis

- **Money:** `ValorEsperadoBrl` calculado pela `CalculadorValorGarantiaExigida` retorna `Money`; comparação direta `Money == Money`.
- **NodaTime:** `Instant` para `VigenciaInicio`/`VigenciaFim`/`RegistradoEm`. Nunca `DateTime`.
- **Clock:** `IClock` injetado no agregado (já é o padrão).
- **DTOs:** `static From(entity)` em todos os DTOs novos.
- **EF Core:** apenas em `Sgcf.Infrastructure`. Owned entity para itens de revisão **não** é usada (cada item tem `Id` para preservar a tabela existente após rename).
- **Naming:** `GarantiaExigidaRevisao`, `GarantiaExigidaItem` (PT-BR no domínio). `Snapshot` aparece apenas em DTOs frontend (`GarantiaExigidaSnapshotItemDto`) — onde "snapshot" é jargão de consumo.

---

## 8. Estratégia de Testes

### 8.1. Pirâmide

| Camada               | Quantidade alvo | Foco                                                                                       |
| -------------------- | --------------- | ------------------------------------------------------------------------------------------ |
| **Unit Domain**      | ~25 testes      | SR-01..SR-08, SLB-01..SLB-05, imutabilidade SC-05, factory, idempotência SLB-04            |
| **Unit Application** | ~18 testes      | SC-01..SC-07; handlers com mocks; cobertura completa/parcial/zero em SC-04                 |
| **Integration**      | ~8 testes       | Repositórios + migration end-to-end via Testcontainers; backfill correto                   |
| **API/E2E**          | ~6 fluxos       | `GET /revisoes-garantias`, conversão 409 com body §4.5, `GET /contratos/{id}` com snapshot |

### 8.2. Testes unitários de domínio (obrigatórios)

| Teste                                                                                                  | Invariante      |
| ------------------------------------------------------------------------------------------------------ | --------------- |
| `Criar_ComLimiteBancoIdVazio_LancaArgumentException`                                                   | SR-01           |
| `Criar_DefineVigenciaInicioComoInstantAtual`                                                           | SR-02           |
| `EncerrarVigencia_ChamadaDuasVezes_LancaInvalidOperationException`                                     | SR-03           |
| `EncerrarVigencia_DefineVigenciaFimComoInstantAtual`                                                   | SR-03/SR-04     |
| `AtualizarItem_AposRevisaoEncerrada_LancaInvalidOperationException`                                    | SR-05           |
| `Criar_ComItensDeMesmoTipo_LancaInvalidOperationException`                                             | SR-06           |
| `Criar_ComItensVazios_Permitido`                                                                       | SR-08           |
| `SubstituirGarantiasExigidas_PrimeiraVez_CriaRevisaoInicial`                                           | SLB-02          |
| `SubstituirGarantiasExigidas_ListaEquivalente_NaoCriaNovaRevisao`                                      | SLB-04          |
| `SubstituirGarantiasExigidas_ListaDiferente_FechaVigenteEAbreNovaNoMesmoInstant`                       | SLB-02/SLB-03   |
| `RevisaoGarantiasVigente_ApenasUmaPorLimite`                                                           | SLB-01          |
| `VincularPoliticaBanco_AposJaVinculado_LancaSeValoresDiferentes`                                       | SC-05           |
| `VincularPoliticaBanco_AposJaVinculado_SilencioSeMesmosValores`                                        | SC-05 (idempotência) |

### 8.3. Testes de Application (Testcontainers + mocks)

| Teste                                                                                                                          | Cenário        |
| ------------------------------------------------------------------------------------------------------------------------------ | -------------- |
| `Converter_ComLimiteBancoExistente_PreencheLimiteBancoIdENoContrato`                                                           | SC-01          |
| `Converter_ComLimiteGlobalVigente_PreencheLimiteGlobalBancoId`                                                                 | SC-02          |
| `Converter_ComRevisaoVigente_PreencheGarantiasExigidasRevisaoId`                                                               | SC-03          |
| `Converter_ItemObrigatorioPercentual_SemGarantiaContrato_Bloqueia409`                                                          | SC-04          |
| `Converter_ItemObrigatorioPercentual_ComGarantiaParcial_Bloqueia409`                                                           | SC-04          |
| `Converter_ItemObrigatorioPercentual_ComGarantiaCompleta_Sucede`                                                               | SC-04          |
| `Converter_ItemObrigatorioValorFixo_AvaliadoIndependentementeDePrincipal`                                                      | SC-04          |
| `Converter_ItemObrigatorioAval_SatisfeitoPorQualquerGarantiaAval`                                                               | SC-04          |
| `Converter_ItemNaoObrigatorio_NaoBloqueiaMesmoSemCobertura`                                                                    | SC-04          |
| `Converter_SemLimiteBancoCadastrado_PermiteContratoFKsNulas`                                                                   | SC-07          |
| `PatchLimiteBanco_AlterandoGarantias_FechaRevisaoEAbreNova_Idempotente`                                                        | SLB-02/SLB-04  |
| `ListarRevisoesGarantias_OrdemAscendentePorVigenciaInicio`                                                                     | SLB-05         |
| `Migration_S34_BackfillCriaUmaRevisaoPorLimiteComItens`                                                                        | Migration data |

Marcados com `[Trait("Category", "Slow")]` quando dependem de PostgreSQL/Testcontainers.

### 8.4. Testes HTTP (`Sgcf.Api.IntegrationTests`)

| Fluxo                                                                                                                                                       |
| ----------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `POST /api/v1/cotacoes/{id}/converter` com cobertura insuficiente → `409` com body conforme §4.5.                                                            |
| `POST /api/v1/cotacoes/{id}/converter` com cobertura completa → `201` e `GET /contratos/{newId}` retorna as 3 FKs + `garantiasExigidasSnapshot[]`.           |
| `PATCH /api/v1/limites-banco/{id}` alterando `garantiasExigidas` → `GET /limites-banco/{id}/revisoes-garantias` retorna 2 revisões (anterior fechada + nova). |
| `DELETE /api/v1/limites-banco/{id}/garantias-exigidas/{itemId}` → `410 Gone` com header `Location` apontando para o novo endpoint.                            |
| `GET /api/v1/contratos` (lista) **não** inclui `garantiasExigidasSnapshot[]` (payload pesado); inclui as 3 FKs.                                              |
| Multi-tenant: `GET /revisoes-garantias` de outro tenant retorna vazio (RLS).                                                                                |

### 8.5. Critérios de cobertura

- Domain (`GarantiaExigidaRevisao`, `LimiteBanco` modificado, `Contrato` modificado): ≥ 95% linhas.
- Application (`ConverterEmContratoHandler`, handlers de PATCH/POST/DELETE de garantia): ≥ 90% linhas.
- Infrastructure: ≥ 75% linhas (foco em migration + repository com eager-load).
- Zero regressão nos testes existentes de `LimiteBanco`, `LimiteGlobalBanco`, `Contrato`, `Cotacao`.

---

## 9. Critérios de Sucesso

### 9.1. Lacuna 1 — Histórico de garantias exigidas

A implementação resolve a Lacuna 1 quando:

1. Toda alteração em garantias de um `LimiteBanco` via API gera uma nova `GarantiaExigidaRevisao` com `VigenciaInicio` no instante do PATCH.
2. A revisão imediatamente anterior recebe `VigenciaFim` igual ao `VigenciaInicio` da nova (continuidade temporal sem gaps).
3. Itens de revisões fechadas são imutáveis: tentativa de `Atualizar` em item cuja revisão tem `VigenciaFim != null` lança `InvalidOperationException`.
4. Query "política em data X" funciona: `WHERE vigencia_inicio <= X AND (vigencia_fim IS NULL OR vigencia_fim > X)` retorna exatamente uma revisão por `LimiteBancoId`.
5. `GET /api/v1/limites-banco/{id}/revisoes-garantias` retorna todas as revisões em ordem ascendente.

### 9.2. Lacuna 2 — Rastreabilidade no contrato

A implementação resolve a Lacuna 2 quando:

1. Toda conversão cotação→contrato bem-sucedida preenche `Contrato.LimiteBancoId`, `LimiteGlobalBancoId` e `GarantiasExigidasRevisaoId` com os valores vigentes (ou `NULL` quando não aplicável — SC-07).
2. `GET /api/v1/contratos/{id}` retorna os 3 FKs e o array imutável `garantiasExigidasSnapshot[]` derivado da revisão vinculada.
3. Após a criação, `PATCH /contratos/{id}` não altera esses 3 campos (testado por SC-05).
4. Contratos pré-feature retornam `NULL` nos 3 campos sem erro.

### 9.3. Lacuna 3 — Enforcement na criação

A implementação resolve a Lacuna 3 quando:

1. Cotação cuja conversão deixa um `GarantiaExigidaItem` obrigatório sem cobertura é rejeitada com `409` e corpo conforme §4.5.
2. Cobertura é avaliada pela `CalculadorValorGarantiaExigida` (mesma fórmula da feature de preenchimento de cotação — consistência garantida).
3. Itens não obrigatórios são ignorados pelo enforcement (não bloqueiam).
4. Bancos sem `LimiteBanco` cadastrado convertem normalmente sem enforcement (SC-07).

### 9.4. Aceitação global

- [ ] Migration `S34_SnapshotGarantiasContrato` aplicada em local e CI sem erro.
- [ ] Backfill: `SELECT COUNT(*) FROM garantia_exigida_item WHERE revisao_id IS NULL` = 0 após migration.
- [ ] RLS policy ativa em `garantia_exigida_revisao` (verificada por teste multi-tenant).
- [ ] Cobertura nos limites mínimos (§8.5).
- [ ] OpenAPI gerado inclui o novo `GET /revisoes-garantias` e os campos novos em `ContratoDto`.
- [ ] Endpoint `DELETE /garantias-exigidas/{itemId}` retorna `410 Gone` com `Location`.
- [ ] Suite completo verde (`dotnet test`).
- [ ] Invariantes SR-01..SR-08, SLB-01..SLB-05, SC-01..SC-07 cobertas por ≥ 1 teste cada.

---

## 10. Boundaries (Sempre / Pergunte Primeiro / Nunca)

### 10.1. Sempre

- Tratar `GarantiaExigidaRevisao` como append-only.
- Fechar a revisão vigente antes de abrir nova (mesmo `Instant` em `VigenciaFim` da antiga e `VigenciaInicio` da nova).
- Preencher `Contrato.LimiteBancoId`, `LimiteGlobalBancoId`, `GarantiasExigidasRevisaoId` na conversão quando houver entidades correspondentes vigentes.
- Validar SC-04 antes de criar o contrato — falhas resultam em rollback total e `409`.
- Reutilizar `CalculadorValorGarantiaExigida` para o cálculo de valor esperado (não duplicar lógica).
- Aplicar RLS + EF global filter por `TenantId` em `garantia_exigida_revisao`.
- Aplicar `ON DELETE SET NULL` nas 3 FKs do contrato (preserva o contrato se o limite for soft-deleted no futuro).
- Retornar `409` em violações de invariantes cruzadas (não `400`).

### 10.2. Pergunte Primeiro

- Permitir snapshot pós-criação (mudar SC-05 para mutável) — quebra o contrato forense.
- Adicionar `garantiasExigidasSnapshot[]` na listagem `GET /contratos` (payload pesado; impacta performance).
- Adicionar endpoint genérico "política de banco X em data Y" — foi descartado nesta fase (acesso só via `GET /contratos/{id}`).
- Permitir backfill retroativo de `LimiteBancoId` em contratos legados — foi descartado (custo > benefício, dados ambíguos).
- Estender `LimiteGlobalBanco` para também ter `garantiasExigidas` — foi descartado nesta fase.
- Trocar `Instant` por `LocalDate` em vigências de revisão.
- Remover o `DELETE /garantias-exigidas/{itemId}` antes da Fase 2 de deprecação.

### 10.3. Nunca

- Apagar registros de `garantia_exigida_revisao` ou `garantia_exigida_item` (append-only).
- Permitir mais de uma revisão com `VigenciaFim IS NULL` por `LimiteBancoId`.
- Alterar `Contrato.LimiteBancoId`, `LimiteGlobalBancoId` ou `GarantiasExigidasRevisaoId` após persistência inicial.
- Modificar items de revisão fechada.
- Calcular valor de garantia esperada por duas vias diferentes (sempre `CalculadorValorGarantiaExigida`).
- Persistir `garantiasExigidasSnapshot` como JSONB no `Contrato` (decisão revogada — uso da FK + join).
- Permitir contrato com `GarantiasExigidasRevisaoId` apontando para revisão fechada **no momento da criação** (deve apontar para a vigente — registro temporal, não retroativo).
- Usar `DateTime.Now` / `DateTime.UtcNow` em qualquer camada.
- Importar `Sgcf.Infrastructure` em `Sgcf.Mcp` ou `Sgcf.A2a`.

---

## 11. Perguntas Abertas

Resolvidas na fase de elicitação (registradas para histórico):

| #  | Pergunta                                                                                            | Decisão                                                                                                                       |
| -- | --------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------- |
| 1  | Modelo: snapshot JSONB no Contrato vs versionamento de `GarantiaExigidaLimite`?                     | Versionamento. Fonte única de verdade, queries cross-contract habilitadas.                                                    |
| 2  | Enforcement no presente (Lacuna 3) entra nesta spec?                                                | Sim. Conversão cotação→contrato bloqueia (`409`) quando obrigatórias ficam sem cobertura.                                     |
| 3  | Endpoint forense "política em data X" genérico?                                                     | Não nesta fase. Forense acessível via `GET /contratos/{id}` e `GET /revisoes-garantias`.                                      |
| 4  | `LimiteGlobalBanco` ganha `garantiasExigidas` próprio?                                              | Não. Garantias permanecem em `LimiteBanco` per-modalidade. `LimiteGlobalBancoId` no contrato é só rastreabilidade.             |

Em aberto, dependentes do time backend para confirmação durante o build:

- **Critério de "vigência" do `LimiteBanco` na conversão**: usar `DataVigenciaInicio <= cmd.DataContratacao AND (DataVigenciaFim IS NULL OR DataVigenciaFim > cmd.DataContratacao)` ou `Instant` da conversão? Recomendação desta SPEC: usar `cmd.DataContratacao` (alinha a política com a data negocial, não com o instante de gravação no sistema).
- **`PATCH /limites-banco/{id}` com `garantiasExigidas: null`**: semântica atual diz "preserve". Mantida — não cria revisão.
- **Backfill da migration: motivo padrão**: `"Revisão inicial gerada pela migration S34"` (string fixa). Confirmar com PO se prefere algo mais informativo (ex.: incluir data da migration).

---

## 12. Histórico

| Data       | Versão | Mudança                                                                                                          |
| ---------- | ------ | ---------------------------------------------------------------------------------------------------------------- |
| 2026-05-25 | v1.0   | Draft inicial. Modelo versionado de `GarantiaExigidaRevisao` + 3 FKs no `Contrato` + enforcement na conversão. Substitui a Opção A+ proposta em `PROPOSTA_SNAPSHOT_LIMITE_NO_CONTRATO.md`. |
