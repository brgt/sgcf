# SPEC — Regime de Limite Explícito por Banco (Suporte a Banco com Limite Global Puro)

> **Status:** Pronta para planejamento — perguntas abertas resolvidas com o PO (§11)
> **Data:** 2026-06-03
> **Autor:** Engenharia (SGCF Backend)
> **Versão:** v0.2
> **Escopo:** Domain + Application + Infrastructure + Api + Tests
> **Dependências:** `SPEC_LIMITE_GLOBAL.md` (v1.1), `Banco`, `LimiteBanco`, `LimiteGlobalBanco`, `IConsultaSaldoBanco`, `Cotacao`, `Contrato`.
> **Relação com a spec-mãe:** Esta spec **emenda** `SPEC_LIMITE_GLOBAL.md §4.3` (seleção de regime). A regra de seleção deixa de ser implícita (baseada na existência de `LimiteBanco`) e passa a ser **explícita** (flag no cadastro do banco). Todas as demais seções da spec-mãe (LG-01..LG-13, endpoints de limite global, cálculo de disponibilidade §4.4) permanecem válidas, exceto onde indicado.

---

## 1. Objetivo

### 1.1. O quê

Permitir que um banco opere **exclusivamente sob limite global** (sem nenhum `LimiteBanco` por modalidade), de forma que qualquer modalidade de operação consuma o teto global único. O regime do banco passa a ser uma **decisão explícita de cadastro** (flag), não mais inferida da presença de limites por modalidade.

### 1.2. Por quê

Bancos como o **Itaú** não concedem limite por operação/modalidade — concedem uma linha única que a empresa distribui livremente entre FINIMP / REFINIMP / NCE / etc. Hoje o sistema:

- **Bloqueia** a adição desse banco a uma cotação, pois `AdicionarBancoNaCotacaoCommand` exige incondicionalmente um `LimiteBanco` para a modalidade (lança `InvalidOperationException`).
- **Não valida** nenhum teto na conversão em contrato quando não há `LimiteBanco` (a invariante LG-12 da spec-mãe nunca foi implementada no fluxo de conversão).

Resultado: bancos de linha única são inoperáveis no fluxo de cotação, e mesmo que fossem, não haveria controle de teto na contratação.

### 1.3. Por que flag explícita (e não detecção implícita)

A spec-mãe (§4.3) infere o regime: "tem `LimiteBanco` → regime B; senão → regime A". Isso cria um *footgun*: cadastrar **um** `LimiteBanco` por engano no Itaú vira o banco inteiro para regime per-modality e passa a bloquear todas as demais modalidades silenciosamente. A flag explícita:

- Torna o regime uma decisão auditável e intencional do cadastro.
- Elimina a virada acidental de regime.
- Permite validar coerência (bloquear cadastro de `LimiteBanco` em banco de regime global puro).

### 1.4. Personas

| Persona | Necessidade |
| --- | --- |
| **Operador de Tesouraria** | Cotar e contratar com bancos de linha única (Itaú) sem precisar cadastrar limites fictícios por modalidade. |
| **Gerente Financeiro** | Garantir que a contratação em banco de linha única respeite o teto global agregado. |
| **Auditor** | Saber, no cadastro, qual regime de limite cada banco segue. |

### 1.5. Métricas de sucesso

- Banco marcado como regime global puro pode ser adicionado a cotações em qualquer modalidade.
- 0 contratos criados que excedam o limite global vigente (regime global puro).
- 0 regressão no comportamento dos bancos existentes (regime per-modalidade preservado).

---

## 2. Comandos

```bash
# Build
dotnet build

# Test rápido (sem Testcontainers)
dotnet test --filter "Category!=Slow"

# Test desta feature
dotnet test --filter "FullyQualifiedName~RegimeLimite"

# Migration (após editar Banco + BancoConfiguration)
dotnet ef migrations add S37_RegimeLimiteBanco \
  --project src/Sgcf.Infrastructure \
  --startup-project src/Sgcf.Api \
  --output-dir Persistence/Migrations

# Aplicar migration
dotnet ef database update \
  --project src/Sgcf.Infrastructure \
  --startup-project src/Sgcf.Api
```

---

## 3. Modelo de Domínio

### 3.1. Novo enum `RegimeLimiteBanco`

Namespace: `Sgcf.Domain.Bancos`.

```csharp
namespace Sgcf.Domain.Bancos;

/// <summary>
/// Regime de controle de limite de crédito de um banco.
/// </summary>
public enum RegimeLimiteBanco
{
    /// <summary>
    /// Regime per-modalidade (Cenário B da SPEC_LIMITE_GLOBAL).
    /// Cada modalidade tem seu próprio LimiteBanco; o LimiteGlobalBanco (se existir) é teto agregado.
    /// É o regime padrão e o comportamento histórico do sistema.
    /// </summary>
    PerModalidade = 0,

    /// <summary>
    /// Regime de limite global puro (Cenário A da SPEC_LIMITE_GLOBAL).
    /// Banco não possui LimiteBanco por modalidade; qualquer operação consome o LimiteGlobalBanco vigente.
    /// </summary>
    GlobalPuro = 1,
}
```

> **Nota de naming:** os valores `PerModalidade` / `GlobalPuro` espelham exatamente o campo `regime` já retornado pelo DTO `GET /bancos/{bancoId}/limite-global-vigente` (SPEC_LIMITE_GLOBAL §9.1/§9.2). Mantém-se a consistência com o contrato existente.

### 3.2. Alteração na entidade `Banco`

Adicionar a propriedade e o método de domínio (arquivo `src/Sgcf.Domain/Bancos/Banco.cs`):

```csharp
public RegimeLimiteBanco RegimeLimite { get; private set; } = RegimeLimiteBanco.PerModalidade;

/// <summary>
/// Define o regime de limite do banco.
/// Mudar para GlobalPuro exige que o banco não tenha LimiteBanco por modalidade ativo
/// (validado na Application — domínio não conhece repositório).
/// </summary>
public void DefinirRegimeLimite(RegimeLimiteBanco regime, IClock clock)
{
    RegimeLimite = regime;
    UpdatedAt = clock.GetCurrentInstant();
}
```

> **Não confundir:** `Banco.LimiteCreditoBrl` (já existente) é um valor para **monitoramento de exposição** e é independente desta feature. A flag `RegimeLimite` governa apenas o **enforcement de cotação/contrato** contra `LimiteBanco` vs `LimiteGlobalBanco`. Os dois coexistem sem relação direta.

### 3.3. Migration `S37_RegimeLimiteBanco`

- Coluna `regime_limite` (`integer NOT NULL DEFAULT 0`) na tabela de bancos (`banco_config`).
- **Backfill de dados** para preservar a semântica implícita anterior no momento da migração:

```sql
-- Bancos que hoje operam de fato como "global puro" (têm limite global vigente
-- e nenhum LimiteBanco ativo) recebem GlobalPuro; os demais permanecem PerModalidade (default).
UPDATE sgcf.banco_config b
SET regime_limite = 1
WHERE EXISTS (
        SELECT 1 FROM sgcf.limite_global_banco g
        WHERE g.banco_id = b.id
          AND g.data_vigencia_fim IS NULL
      )
  AND NOT EXISTS (
        SELECT 1 FROM sgcf.limite_banco lb
        WHERE lb.banco_id = b.id
          AND lb.data_vigencia_fim IS NULL
      );
```

> O backfill é a forma de garantir **zero mudança de comportamento observável** na virada. Ver §11 (Perguntas Abertas) — confirmar com o PO se algum banco específico deve ser marcado manualmente.

---

## 4. Regras de Validação (Invariantes)

### 4.1. Emenda à seleção de regime (substitui SPEC_LIMITE_GLOBAL §4.3)

A seleção de regime passa a ser:

```
REGIME = Banco.RegimeLimite
```

`IConsultaSaldoBanco.BancoEmRegimePerModalityAsync(bancoId, ...)` passa a **ler a flag** `Banco.RegimeLimite` em vez de verificar a existência de `LimiteBanco`:

```
BancoEmRegimePerModalityAsync(X) := (Banco[X].RegimeLimite == PerModalidade)
```

Os métodos de cálculo de saldo da spec-mãe (`CalcularSaldoDevedorBancoAsync` para regime global puro, `CalcularUtilizadoAgregadoModalidadesAsync` para per-modalidade) permanecem inalterados.

### 4.2. Novas invariantes de coerência de regime

| # | Regra | Onde validar | Bloqueio |
| --- | --- | --- | --- |
| REG-01 | Não permitir criar/atualizar `LimiteBanco` (por modalidade) para banco em regime `GlobalPuro`. | `CriarLimiteBancoHandler`, `AtualizarLimiteBancoHandler` | Hard (409) |
| REG-02 | Não permitir mudar banco para regime `GlobalPuro` enquanto existir `LimiteBanco` ativo para ele. | Handler de atualização de banco | Hard (409) |
| REG-03 | Banco em regime `GlobalPuro` só opera (cotação/contrato) se tiver `LimiteGlobalBanco` **vigente** cadastrado; caso contrário, bloqueio com mensagem explícita. | `AdicionarBancoNaCotacaoCommand`, `ConverterEmContratoCommand` | Hard |
| REG-04 | Mudar banco de `GlobalPuro` para `PerModalidade` é permitido a qualquer momento (não há `LimiteBanco` a violar). | Handler de atualização de banco | Permitido |

### 4.3. Enforcement na adição de banco à cotação (`AdicionarBancoNaCotacaoCommand`)

Ramificar por regime:

- **`PerModalidade`**:
  1. Exige `LimiteBanco` para a modalidade. Se não houver → bloqueio (comportamento atual).
  2. `disponivelModalidade = LimiteBanco.ValorDisponivelBrl`.
  3. Se existir `LimiteGlobalBanco` vigente: `disponivelGlobal = max(0, ValorLimiteGlobal − CalcularUtilizadoAgregadoModalidadesAsync(banco))`; caso contrário, `disponivelGlobal = +∞` (sem teto agregado).
  4. `disponivel = min(disponivelModalidade, disponivelGlobal)`. Se `disponivel < ValorAlvoBrl` → bloqueio.
- **`GlobalPuro`** (novo):
  1. Buscar `LimiteGlobalBanco` vigente (data = hoje, fuso `America/Sao_Paulo`). Se não houver → bloqueio (REG-03).
  2. `disponivelGlobal = max(0, ValorLimiteGlobal − CalcularSaldoDevedorBancoAsync(banco))`.
  3. Se `disponivelGlobal < ValorAlvoBrl` → bloqueio com mensagem clara.
  4. Caso contrário → `cotacao.AdicionarBancoAlvo(bancoId)`.

> A validação na cotação é **best-effort** (a cotação é candidata, não reserva limite — consistente com o comportamento atual, que não reserva em `AdicionarBanco`). O enforcement definitivo e transacional ocorre na conversão (§4.4).

### 4.4. Enforcement na conversão em contrato (`ConverterEmContratoCommand`) — implementa LG-11 e LG-12

Ramificar por regime. Em ambos os casos o enforcement roda **antes** de `Contrato.Criar`, para não persistir estado parcial.

- **`PerModalidade`** (implementa LG-11):
  1. Buscar `LimiteBanco` vigente para a modalidade/data de contratação. Se não houver → bloqueio: "modalidade '{modalidade}' requer LimiteBanco registrado neste banco — regime per-modalidade" (LG-11).
  2. Checagem de modalidade: `LimiteBanco.ValorDisponivelBrl ≥ principalNovo`. Se não → bloqueio.
  3. Checagem de teto global (se existir `LimiteGlobalBanco` vigente): `CalcularUtilizadoAgregadoModalidadesAsync(banco) + principalNovo ≤ ValorLimiteGlobal`. Se exceder → bloqueio (teto agregado). Se o banco não tiver limite global vigente, esta checagem é ignorada (o global é teto opcional no regime per-modalidade).
  4. `RegistrarUso(principal)` no `LimiteBanco` (comportamento atual preservado) e criar o contrato.
- **`GlobalPuro`** (implementa LG-12):
  1. Buscar `LimiteGlobalBanco` vigente. Se não houver → bloqueio (REG-03).
  2. `saldoDevedor = CalcularSaldoDevedorBancoAsync(banco)` (soma de contratos ativos, pré-criação).
  3. Se `saldoDevedor + principalNovo > ValorLimiteGlobal` → lançar exceção de domínio (bloqueio LG-12).
  4. Caso contrário → criar o contrato normalmente. **Não há `RegistrarUso`** no regime global puro: o consumo é o próprio contrato ativo, computado dinamicamente em consultas subsequentes (consistente com SPEC_LIMITE_GLOBAL §3.2 — `ValorUtilizado` nunca persistido).

> **Concorrência:** as checagens de teto (passos 3 de cada ramo) leem agregados calculados (`CalcularUtilizado...`/`CalcularSaldoDevedor...`) e em seguida criam o contrato na mesma transação. Manter a leitura e a escrita dentro da mesma `SaveChanges`/transação para reduzir janela de corrida; corrida residual é aceitável dado o caráter de teto de crédito (revisão manual posterior). Não introduzir lock pessimista nesta entrega (ver §10 "Pergunte Primeiro").

---

## 5. Endpoints da API

### 5.1. Cadastro de banco

O campo `regimeLimite` é exposto no create/update de banco:

- **POST/PUT/PATCH** do banco aceitam `regimeLimite: "PerModalidade" | "GlobalPuro"` (default `PerModalidade` se omitido na criação).
- Mudança para `GlobalPuro` com `LimiteBanco` ativo existente → `409 Conflict` (REG-02).

### 5.2. Mensagens de erro (exemplos)

| Cenário | HTTP | Mensagem |
| --- | --- | --- |
| Regime global puro sem limite global vigente (cotação ou contrato) | 409 | "Banco '{apelido}' opera em regime de limite global, mas não possui limite global vigente cadastrado. Cadastre o limite global antes de operar." |
| Regime global puro, disponível insuficiente (cotação) | 409 | "Banco '{apelido}' não possui limite global disponível suficiente. Disponível: BRL {x}, necessário: BRL {y}." |
| Conversão excede teto global (LG-12) | 409 | "Contratação excede o limite global do banco '{apelido}'. Saldo devedor: BRL {s}, principal: BRL {p}, limite: BRL {l}." |
| Cadastrar `LimiteBanco` em banco global puro (REG-01) | 409 | "Banco '{apelido}' opera em regime de limite global e não admite limite por modalidade." |
| Mudar para global puro com `LimiteBanco` ativo (REG-02) | 409 | "Não é possível mudar o banco '{apelido}' para regime global: existem limites por modalidade ativos. Encerre-os primeiro." |

> Endpoints de `LimiteGlobalBanco` (SPEC_LIMITE_GLOBAL §5) permanecem inalterados.

---

## 6. Estrutura de Arquivos

```
src/
├── Sgcf.Domain/
│   └── Bancos/
│       ├── Banco.cs                                  (EDIT — propriedade RegimeLimite + DefinirRegimeLimite)
│       └── RegimeLimiteBanco.cs                      (NEW — enum)
│
├── Sgcf.Application/
│   ├── Bancos/Commands/
│   │   └── (handler de atualização de banco)         (EDIT — aceitar/validar regimeLimite; REG-02)
│   └── Cotacoes/Commands/
│       ├── AdicionarBancoNaCotacaoCommand.cs         (EDIT — ramificar por regime; §4.3)
│       ├── ConverterEmContratoCommand.cs             (EDIT — enforcement LG-11 + LG-12; §4.4)
│       ├── CreateLimiteBancoCommand.cs               (EDIT — REG-01)
│       └── UpdateLimiteBancoCommand.cs               (EDIT — REG-01)
│
├── Sgcf.Infrastructure/
│   └── Persistence/
│       ├── Configurations/
│       │   └── BancoConfiguration.cs                 (EDIT — mapear regime_limite)
│       ├── Repositories/
│       │   └── ConsultaSaldoBancoService.cs          (EDIT — BancoEmRegimePerModalityAsync lê a flag)
│       └── Migrations/
│           └── S37_RegimeLimiteBanco.cs              (NEW — coluna + backfill)
│
└── Sgcf.Api/
    └── Controllers/                                  (EDIT — DTO de banco expõe regimeLimite)

tests/
├── Sgcf.Domain.Tests/Bancos/
│   └── BancoRegimeLimiteTests.cs                     (NEW)
└── Sgcf.Application.Tests/Cotacoes/
    ├── AdicionarBancoRegimeGlobalTests.cs            (NEW — §4.3)
    ├── ConverterEmContratoRegimeGlobalTests.cs       (NEW — §4.4 / LG-12)
    ├── ConverterEmContratoLG11Tests.cs               (NEW — §4.4 / LG-11 per-modalidade)
    └── CoerenciaRegimeLimiteTests.cs                 (NEW — REG-01, REG-02)

docs/specs/limites-banco/
└── SPEC_REGIME_LIMITE_EXPLICITO.md                   (este arquivo)
```

---

## 7. Estilo de Código

Segue `CLAUDE.md` integralmente:

- **Money** para todo valor monetário; nunca `decimal` cru em assinaturas de domínio/contrato.
- **NodaTime**: `LocalDate` para vigência, `Instant` para timestamps; `IClock` injetado; nunca `DateTime.Now`/`UtcNow`.
- **Fuso**: `America/Sao_Paulo` para resolver "hoje".
- **Camadas**: enum e propriedade no Domain; validações de coerência (REG-01..REG-04) e enforcement na Application; EF/migration apenas na Infrastructure.
- **Naming**: domínio em português (`RegimeLimiteBanco`, `RegimeLimite`, `DefinirRegimeLimite`); técnico em inglês.
- **Erros de invariante cruzada/coerência** → `409 Conflict` (consistente com LG-* da spec-mãe).

---

## 8. Estratégia de Testes

### 8.1. Domínio (`BancoRegimeLimiteTests`)

| Teste | Foco |
| --- | --- |
| `Criar_BancoNasce_ComRegimePerModalidade` | Default = PerModalidade |
| `DefinirRegimeLimite_AlteraRegime_EAtualizaUpdatedAt` | Mutação + timestamp |

### 8.2. Application — adição à cotação (`AdicionarBancoRegimeGlobalTests`)

| Teste | Cenário |
| --- | --- |
| `RegimeGlobal_ComGlobalSuficiente_PermiteAdicionar` | §4.3 caminho feliz |
| `RegimeGlobal_ComGlobalInsuficiente_Bloqueia` | §4.3 disponível < alvo |
| `RegimeGlobal_SemLimiteGlobalVigente_Bloqueia` | REG-03 |
| `RegimePerModalidade_SemLimiteBanco_ContinuaBloqueando` | Regressão: comportamento atual preservado |

### 8.3. Application — conversão (`ConverterEmContratoRegimeGlobalTests`)

| Teste | Cenário |
| --- | --- |
| `RegimeGlobal_DentroDoTeto_Converte` | LG-12 caminho feliz |
| `RegimeGlobal_EstourandoTeto_Bloqueia` | LG-12 |
| `RegimeGlobal_SemLimiteGlobalVigente_Bloqueia` | REG-03 |
| `RegimePerModalidade_ComLimiteBanco_RegistraUsoComoAntes` | Regressão |

### 8.3.1. Application — conversão regime per-modalidade / LG-11 (`ConverterEmContratoLG11Tests`)

| Teste | Cenário |
| --- | --- |
| `RegimePerModalidade_SemLimiteBancoNaModalidade_Bloqueia` | LG-11 (exige LimiteBanco) |
| `RegimePerModalidade_DisponivelModalidadeInsuficiente_Bloqueia` | LG-11 nível modalidade |
| `RegimePerModalidade_ExcedeTetoGlobal_Bloqueia` | LG-11 nível global agregado |
| `RegimePerModalidade_SemLimiteGlobal_IgnoraChecagemGlobal_Converte` | LG-11 (global opcional) |
| `RegimePerModalidade_DentroDeAmbosOsTetos_RegistraUsoEConverte` | LG-11 caminho feliz |

### 8.4. Application — coerência (`CoerenciaRegimeLimiteTests`)

| Teste | Invariante |
| --- | --- |
| `CriarLimiteBanco_EmBancoGlobalPuro_Bloqueia` | REG-01 |
| `MudarParaGlobalPuro_ComLimiteBancoAtivo_Bloqueia` | REG-02 |
| `MudarParaGlobalPuro_SemLimiteBanco_Permite` | REG-02 (caminho ok) |

### 8.5. Cobertura e regressão

- Zero regressão no suite completo (`dotnet test`), em especial nos testes existentes de `LimiteGlobalBanco`, `AdicionarBanco` e `ConverterEmContrato`.
- Backfill da migration coberto por verificação de que bancos pré-existentes mantêm comportamento.

---

## 9. Critérios de Sucesso (Aceitação)

- [ ] Migration `S37_RegimeLimiteBanco` aplicada (coluna + backfill) em local e CI.
- [ ] Banco marcado `GlobalPuro` com limite global vigente pode ser adicionado a cotação em qualquer modalidade.
- [ ] Conversão em contrato em banco `GlobalPuro` respeita o teto global (LG-12): dentro do teto converte, estourando bloqueia.
- [ ] Conversão em banco `PerModalidade` implementa LG-11: exige `LimiteBanco` na modalidade, valida disponível da modalidade e, quando há limite global vigente, valida o teto agregado (Σ utilizado + principal ≤ global).
- [ ] Banco `GlobalPuro` sem limite global vigente é bloqueado com mensagem clara (REG-03), tanto na cotação quanto na conversão.
- [ ] Cadastrar `LimiteBanco` em banco `GlobalPuro` é bloqueado (REG-01).
- [ ] Mudar banco para `GlobalPuro` com `LimiteBanco` ativo é bloqueado (REG-02).
- [ ] Bancos existentes (regime per-modalidade) mantêm 100% do comportamento atual.
- [ ] `SPEC_LIMITE_GLOBAL.md §4.3` referenciada como emendada por esta spec.
- [ ] `docs/api` (bancos, limites-banco) atualizada com o regime explícito e as novas mensagens de erro.

---

## 10. Boundaries (Sempre / Pergunte Primeiro / Nunca)

### 10.1. Sempre

- Ler o regime a partir de `Banco.RegimeLimite` (fonte única da verdade).
- Validar coerência REG-01..REG-04 na camada Application.
- Exigir `LimiteGlobalBanco` vigente para operar banco em regime global puro (REG-03).
- Retornar `409 Conflict` para violações de coerência e de teto.
- Preservar o comportamento per-modalidade existente sem alteração.

### 10.2. Pergunte Primeiro

- Permitir transição de regime que migre limites automaticamente (ex.: ao virar global puro, encerrar `LimiteBanco` em lote).
- Reservar limite global/por-modalidade na adição à cotação (hoje é best-effort, sem reserva).
- Introduzir lock pessimista/transação serializável para fechar a janela de corrida no enforcement de teto na conversão (hoje aceita-se corrida residual — §4.4).
- Mudar o backfill da migration para marcar bancos específicos manualmente além da regra automática.

### 10.3. Nunca

- Persistir `ValorUtilizado` do limite global (continua computado — SPEC_LIMITE_GLOBAL §10.3).
- Permitir `LimiteBanco` por modalidade em banco de regime global puro.
- Permitir operar (cotação/contrato) banco global puro sem limite global vigente.
- Inferir o regime pela presença de `LimiteBanco` (regra implícita da spec-mãe **substituída** por esta spec).
- Usar `DateTime.Now`/`UtcNow` ou `decimal` cru em qualquer camada.

---

## 11. Perguntas Abertas

Todas resolvidas com o PO em 2026-06-03:

1. **Backfill da migração:** ✅ Resolvido. Itaú e outros bancos operam no modelo de linha única. A regra automática de backfill (banco com `LimiteGlobalBanco` vigente e sem `LimiteBanco` ativo → `GlobalPuro`) será aplicada e **cobre todos esses bancos**. Recomenda-se conferência pós-migração da lista resultante (relatório de bancos marcados `GlobalPuro`) para validação do PO, mas não há marcação manual adicional planejada.
2. **LG-11 (regime per-modalidade):** ✅ Resolvido — **incluído nesta entrega**. A conversão no regime per-modalidade passa a validar `LimiteBanco` na modalidade + teto global agregado (quando há limite global vigente). Ver §4.4.
3. **Permissão de cadastro do regime:** ✅ Resolvido — `regimeLimite` é editável **apenas por `Admin`** (consistente com a escrita de `LimiteGlobalBanco`).

---

## 12. Histórico

| Data | Versão | Mudança |
| --- | --- | --- |
| 2026-06-03 | v0.1 | Draft inicial. Introduz regime de limite explícito (flag `RegimeLimiteBanco` em `Banco`), emenda SPEC_LIMITE_GLOBAL §4.3, define enforcement de cotação (§4.3) e conversão/LG-12 (§4.4) para regime global puro, invariantes de coerência REG-01..REG-04 e estratégia de testes. |
| 2026-06-03 | v0.2 | Resolvidas as 3 perguntas abertas com o PO: (1) backfill automático cobre Itaú e demais bancos de linha única; (2) **LG-11 incluído no escopo** — conversão no regime per-modalidade valida modalidade + teto global agregado (§4.3 e §4.4 atualizadas, testes em §8.3.1); (3) cadastro de regime restrito a `Admin`. |
