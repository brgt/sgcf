# SPEC — Onda 0: Foundation para Cotações Multi-Modalidade

**Versão alvo:** v0.6.1 (interna) ou consolidada na release v0.7.0 (REFINIMP)
**Status:** Entregue — v0.6.1 consolidada em v0.7.0 (2026-05-18)
**Pré-requisito de:** Ondas 2 (NCE), 3 (FGI + Capital de Giro), 4 (Lei 4131)
**Plano de execução:** `tasks/cotacoes-modalidades/plan.md` §3
**Decisões trancadas:** MD-1..MD-10 do plano mestre + respostas do PO em 2026-05-18

---

## 1. Objetivo

Habilitar a base técnica que permite ao módulo de Cotações suportar as 5 modalidades pendentes (REFINIMP, Lei 4131, NCE, Capital de Giro, FGI) sem reescritas posteriores. Esta SPEC entrega três mudanças cross-cutting que cada modalidade futura referenciará. **Não introduz nenhuma modalidade nova por si só**: o MVP FINIMP continua funcional e os testes existentes permanecem verdes.

### 1.1. Não-objetivos (Out of Scope da Onda 0)

- Adicionar suporte a qualquer modalidade nova (cada uma tem SPEC própria nesta pasta).
- Refatorar `Proposta` (decisão MD-5: campos específicos de modalidade ficam em commands, não no agregado).
- Migrar dados existentes (Migration S6 é puramente aditiva no schema; dados FINIMP atuais permanecem).
- Alterar contratos públicos da API que já estão em uso por clientes (PTAX continua aceito nas chamadas atuais; só deixa de ser exigido para modalidades futuras).

---

## 2. Mudanças nucleares

A Onda 0 entrega **três mudanças independentes** que podem ser implementadas em paralelo após F0.1 (PTAX nullable é pré-requisito de F0.2 e F0.3).

```
F0.1  PTAX nullable em Cotacao  ───┬──→  F0.2  CalculadoraCet com método por modalidade
                                    │
                                    └──→  F0.3  IConversorModalidade dispatcher
```

---

## 3. F0.1 — `Cotacao.PtaxUsadaUsdBrl` opcional

### 3.1. Motivação

O MVP FINIMP exige PTAX D-1 obrigatoriamente em toda criação de cotação (`CriarCotacaoCommand` linha 56–83). Três das cinco modalidades futuras (NCE, Capital de Giro, FGI) são em BRL puro e não têm conversão cambial — PTAX não faz sentido nelas. Manter PTAX obrigatório forçaria valor sentinel (PTAX=1) ou rejeições artificiais.

### 3.2. Mudança no domínio

**Arquivo:** `src/Sgcf.Domain/Cotacoes/Cotacao.cs`

```csharp
// Antes
public decimal PtaxUsadaUsdBrl { get; private set; }

// Depois
public decimal? PtaxUsadaUsdBrl { get; private set; }
```

**Factory `Cotacao.Criar`:** aceita `decimal? ptaxUsadaUsdBrl = null`. Invariante adicionado:

```csharp
// Modalidades em moeda estrangeira (FINIMP, REFINIMP, Lei4131) exigem PTAX
if (ExigeMoedaEstrangeira(modalidade) && ptaxUsadaUsdBrl is null)
{
    throw new ArgumentException(
        $"PTAX D-1 é obrigatória para modalidade {modalidade}.",
        nameof(ptaxUsadaUsdBrl));
}

// Modalidades BRL puras (NCE, CapitalDeGiro, Fgi) rejeitam PTAX
if (!ExigeMoedaEstrangeira(modalidade) && ptaxUsadaUsdBrl is not null)
{
    throw new ArgumentException(
        $"PTAX não se aplica à modalidade {modalidade} (operação em BRL).",
        nameof(ptaxUsadaUsdBrl));
}
```

Helper estático:

```csharp
internal static bool ExigeMoedaEstrangeira(ModalidadeContrato m) =>
    m == ModalidadeContrato.Finimp ||
    m == ModalidadeContrato.Refinimp ||
    m == ModalidadeContrato.Lei4131;
```

**Por que rejeitar PTAX em modalidades BRL?** Garante que snapshots em `EconomiaNegociacao` sejam semanticamente consistentes — não há "PTAX de uma operação em BRL". Sentinel (`PTAX=1`) seria fonte de bugs sutis em análises.

### 3.3. Mudança em `CriarCotacaoCommand`

**Arquivo:** `src/Sgcf.Application/Cotacoes/Commands/CriarCotacaoCommand.cs`

```csharp
public sealed class CriarCotacaoCommandHandler(...)
{
    public async Task<CotacaoDto> Handle(CriarCotacaoCommand cmd, CancellationToken ct)
    {
        ModalidadeContrato modalidade = Enum.Parse<ModalidadeContrato>(cmd.Modalidade, true);

        decimal? ptax = null;
        LocalDate? dataPtaxReferencia = null;

        if (Cotacao.ExigeMoedaEstrangeira(modalidade))
        {
            LocalDate dataPtax = dataAbertura.PlusDays(-1);
            CotacaoFx cotacaoFx = await fxRepo.GetMaisRecenteAsync(
                Moeda.Usd, TipoCotacao.PtaxD1, dataPtax, ct)
                ?? throw new InvalidOperationException(
                    $"PTAX D-1 não disponível para a data {dataPtax}.");
            ptax = cotacaoFx.ValorVenda.Valor;
            dataPtaxReferencia = cotacaoFx.Momento
                .InZone(DateTimeZoneProviders.Tzdb["America/Sao_Paulo"]).Date;
        }

        Cotacao cotacao = Cotacao.Criar(
            codigoInterno, modalidade, valorAlvo, cmd.PrazoMaximoDias,
            dataAbertura, dataPtaxReferencia, ptax, clock, cmd.Observacoes);
        // ...
    }
}
```

### 3.4. Mudança em `ConverterEmContratoCommand`

**Arquivo:** `src/Sgcf.Application/Cotacoes/Commands/ConverterEmContratoCommand.cs`

Linhas 150-152 calculam `valorPrincipalBrl`:

```csharp
// Antes
Money valorPrincipalBrl = propostaAceita.MoedaOriginal == Moeda.Brl
    ? valorPrincipal
    : new Money(Math.Round(valorPrincipal.Valor * cotacao.PtaxUsadaUsdBrl, 6, MidpointRounding.AwayFromZero), Moeda.Brl);

// Depois
Money valorPrincipalBrl = propostaAceita.MoedaOriginal == Moeda.Brl
    ? valorPrincipal
    : new Money(
        Math.Round(
            valorPrincipal.Valor * cotacao.PtaxUsadaUsdBrl!.Value,  // ! seguro: invariante garante não-null para moeda estrangeira
            6, MidpointRounding.AwayFromZero),
        Moeda.Brl);
```

### 3.5. Migration

**Arquivo:** `src/Sgcf.Infrastructure/Migrations/{timestamp}_S6_PtaxNullable.cs`

```csharp
public partial class S6_PtaxNullable : Migration
{
    protected override void Up(MigrationBuilder mb)
    {
        mb.AlterColumn<decimal>(
            name: "ptax_usada_usd_brl",
            schema: "sgcf", table: "cotacao",
            type: "numeric(10,6)", nullable: true,
            oldClrType: typeof(decimal), oldType: "numeric(10,6)", oldNullable: false);

        // data_ptax_referencia também: já é nullable? confirmar no snapshot atual
    }

    protected override void Down(MigrationBuilder mb)
    {
        // Reverter sem perda: aceita-se que existirão cotações BRL sem PTAX no Down;
        // o rollback usa PTAX=0 como sentinel apenas para satisfazer NOT NULL.
        mb.Sql("UPDATE sgcf.cotacao SET ptax_usada_usd_brl = 0 WHERE ptax_usada_usd_brl IS NULL");
        mb.AlterColumn<decimal>(
            name: "ptax_usada_usd_brl",
            schema: "sgcf", table: "cotacao",
            type: "numeric(10,6)", nullable: false,
            oldClrType: typeof(decimal), oldType: "numeric(10,6)", oldNullable: true);
    }
}
```

### 3.6. Snapshot JSON em `EconomiaNegociacao`

**Critério crítico:** snapshot existente (antes da migration) deve continuar deserializando.

Verificação:
- Snapshot atual tem `"PtaxUsada": <decimal>` (não-null sempre)
- Snapshot novo pode ter `"PtaxUsada": null` ou ausente para BRL
- Round-trip teste: serializar/deserializar cotações FINIMP existentes e cotações novas BRL — assertar igualdade

### 3.7. Testes de F0.1

```
tests/Sgcf.Domain.Tests/Cotacoes/CotacaoPtaxNullableTests.cs
  - Criar_FINIMP_sem_PTAX_lanca_excecao
  - Criar_NCE_com_PTAX_lanca_excecao
  - Criar_NCE_sem_PTAX_sucesso
  - Criar_REFINIMP_sem_PTAX_lanca_excecao

tests/Sgcf.Application.Tests/Cotacoes/CriarCotacaoCommandHandlerTests.cs (estender)
  - Handle_FINIMP_busca_PTAX_via_repo
  - Handle_NCE_pula_busca_PTAX_e_PtaxUsada_fica_null

tests/Sgcf.Api.IntegrationTests/Cotacoes/CotacoesFluxoTests.cs (regressão)
  - Suite FINIMP existente continua verde sem ajuste
```

---

## 4. F0.2 — `CalculadoraCet` com método dedicado por modalidade

### 4.1. Motivação

A `CalculadoraCet` atual (`src/Sgcf.Application/Cotacoes/CalculadoraCet.cs`) assume moeda estrangeira com NDF como inputs primários. Cada modalidade futura tem fórmula distinta:

| Modalidade | Inputs específicos | Saída CET |
|---|---|---|
| FINIMP | TaxaAa, IOF, NDF, BreakFunding | a.a. |
| REFINIMP | Mesma fórmula de FINIMP | a.a. |
| Lei 4131 | TaxaAa, IOF, SBLC custo (informativo) | a.a. (IRRF fora do CET — MD travado) |
| NCE | TaxaAa, IOF crédito (sem IRRF — isenção), Periodicidade | a.a. |
| Capital de Giro | Estimativa baseada em premissas; cronograma real chega na importação | a.a. estimado |
| FGI | TaxaAa, IOF crédito, TaxaFgiAa anual sobre saldo | a.a. |

Tentar generalizar via um método único com 15 parâmetros opcionais é fonte de bugs. Cinco métodos nomeados, cada um com inputs explícitos, é mais simples e testável.

### 4.2. Nova superfície pública

```csharp
namespace Sgcf.Application.Cotacoes;

public static class CalculadoraCet
{
    // Fachada — dispatcheia por modalidade. Existente, mantém assinatura.
    public static decimal CalcularCet(
        Proposta proposta,
        decimal? ptaxUsadaUsdBrl,
        LocalDate dataReferencia,
        decimal? taxaAaPercentualOverride = null);

    // Métodos especializados — adicionados pela Onda 0. Implementação concreta vem nas
    // SPECs de cada modalidade.
    internal static decimal CalcularCetFinimp(Proposta p, decimal ptax, LocalDate dt, decimal? override_ = null);
    internal static decimal CalcularCetRefinimp(Proposta p, decimal ptax, LocalDate dt, decimal? override_ = null);
    internal static decimal CalcularCetLei4131(Proposta p, decimal ptax, LocalDate dt, decimal? override_ = null);
    internal static decimal CalcularCetNce(Proposta p, LocalDate dt, decimal? override_ = null);
    internal static decimal CalcularCetCapitalDeGiro(Proposta p, LocalDate dt, decimal? override_ = null);
    internal static decimal CalcularCetFgi(Proposta p, LocalDate dt, FgiInputs fgi, decimal? override_ = null);
}

public sealed record FgiInputs(decimal TaxaFgiAaPercentual, decimal? PercentualCoberto);
```

### 4.3. Implementação inicial (Onda 0)

- `CalcularCetFinimp`: extrair lógica atual da `CalcularCet` existente sem mudar comportamento.
- `CalcularCetRefinimp`: delega para `CalcularCetFinimp` (mesma fórmula).
- `CalcularCetLei4131`, `CalcularCetNce`, `CalcularCetCapitalDeGiro`, `CalcularCetFgi`: lançam `NotImplementedException("Implementação pendente — Onda <X>")`. As SPECs de cada modalidade entregam.

### 4.4. Base de cálculo

**Decisão travada:** todas as modalidades BRL usam **360 dias** (mesma de FINIMP). Helper `BaseCalculo.Dias360` aplica.

Implicações:
- Não introduzimos `BaseCalculo.Dias252` na Onda 0.
- `CalculadoraCet.AnualizarTaxaDiaria` aceita `dias=360` em todos os branches.
- Se houver necessidade futura de 252, fica documentada como evolução (não roadmap atual).

### 4.5. Testes de F0.2

```
tests/Sgcf.Application.Tests/Cotacoes/CalculadoraCetTests.cs (estender)
  - CalcularCetFinimp_FromProposta_USD_NDF_resultado_consistente_com_legado
  - CalcularCet_fachada_dispatcheia_Finimp_para_CalcularCetFinimp
  - CalcularCet_fachada_modalidade_BRL_chama_branch_correspondente

  - CalcularCetNce_lanca_NotImplementedException
  - CalcularCetLei4131_lanca_NotImplementedException
  - CalcularCetCapitalDeGiro_lanca_NotImplementedException
  - CalcularCetFgi_lanca_NotImplementedException

tests/Sgcf.GoldenDataset/data/cotacoes/  (regressão)
  - 3 cenários existentes FINIMP passam sem ajuste
```

---

## 5. F0.3 — `IConversorModalidade` dispatcher

### 5.1. Motivação

`ConverterEmContratoCommand` (linha 100) hard-codes `if (cotacao.Modalidade == ModalidadeContrato.Finimp)`. Cada modalidade futura precisa criar seu próprio `*Detail`. Adicionar 5 `else if` cresce o command monolítico e dificulta testes isolados.

**Decisão MD-3 travada:** strategy via interface `IConversorModalidade`. Cada modalidade implementa `CriarDetail(...)` e registra como serviço DI.

### 5.2. Interface

**Arquivo:** `src/Sgcf.Application/Cotacoes/IConversorModalidade.cs` (novo)

```csharp
using NodaTime;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Cotacoes;

/// <summary>
/// Strategy para criar o detail aggregate específico de uma modalidade ao converter
/// uma cotação aceita em contrato. Cada modalidade implementa uma instância e registra
/// em DI mapeada por <see cref="ModalidadeContrato"/>.
/// </summary>
public interface IConversorModalidade
{
    /// <summary>Modalidade que esta implementação cobre.</summary>
    ModalidadeContrato Modalidade { get; }

    /// <summary>
    /// Cria a entidade Detail (FinimpDetail, NceDetail, etc.) a partir da cotação,
    /// proposta aceita, contrato recém-criado e inputs do command de conversão.
    /// Retorna a entidade que deve ser persistida pelo repositório.
    /// </summary>
    /// <returns>
    /// Tupla (detalhe principal, detalhe secundário opcional). O detalhe secundário foi
    /// originalmente desenhado para o caso "Capital de Giro com FGI" (BalcaoCaixaDetail + FgiDetail),
    /// removido na correção de 2026-05-18. No MVP, todas as modalidades retornam (detail, null).
    /// A segunda posição é mantida na assinatura para suportar evolução futura sem breaking change.
    /// </returns>
    Task<(Entity Principal, Entity? Secundario)> CriarDetailAsync(
        ConverterEmContratoContext ctx,
        CancellationToken cancellationToken);
}

/// <summary>
/// Dados imutáveis disponíveis para o conversor durante a criação do detail.
/// </summary>
public sealed record ConverterEmContratoContext(
    Cotacao Cotacao,
    Proposta PropostaAceita,
    Contrato ContratoCriado,
    ConverterEmContratoCommand Command,
    IClock Clock);
```

### 5.3. Implementação FINIMP

**Arquivo:** `src/Sgcf.Application/Cotacoes/Conversores/ConversorFinimp.cs` (novo)

```csharp
public sealed class ConversorFinimp : IConversorModalidade
{
    public ModalidadeContrato Modalidade => ModalidadeContrato.Finimp;

    public Task<(Entity, Entity?)> CriarDetailAsync(
        ConverterEmContratoContext ctx, CancellationToken ct)
    {
        FinimpDetail detail = FinimpDetail.Criar(
            ctx.ContratoCriado.Id,
            ctx.Command.RofNumero,
            null,
            ctx.Command.ExportadorNome,
            ctx.Command.ExportadorPais,
            ctx.Command.ProdutoImportado,
            null, null, null, false,
            ctx.Clock);

        return Task.FromResult<(Entity, Entity?)>((detail, null));
    }
}
```

### 5.4. Stubs das outras modalidades

5 classes — `ConversorRefinimp`, `ConversorLei4131`, `ConversorNce`, `ConversorCapitalDeGiro`, `ConversorFgi` — em `src/Sgcf.Application/Cotacoes/Conversores/`. Cada uma:

```csharp
public sealed class ConversorXxx : IConversorModalidade
{
    public ModalidadeContrato Modalidade => ModalidadeContrato.Xxx;

    public Task<(Entity, Entity?)> CriarDetailAsync(
        ConverterEmContratoContext ctx, CancellationToken ct) =>
        throw new NotImplementedException(
            "Conversor da modalidade Xxx será entregue na Onda <X>. " +
            "Veja docs/specs/cotacoes/modalidades/<xxx>.md.");
}
```

### 5.5. Refatoração de `ConverterEmContratoCommand`

```csharp
public sealed class ConverterEmContratoCommandHandler(
    ICotacaoRepository cotacaoRepo,
    IContratoRepository contratoRepo,
    IEconomiaRepository economiaRepo,
    ILimiteBancoRepository limiteRepo,
    ICdiSnapshotRepository cdiRepo,
    IEnumerable<IConversorModalidade> conversores,
    IClock clock) : IRequestHandler<ConverterEmContratoCommand, ContratoDto>
{
    private readonly IReadOnlyDictionary<ModalidadeContrato, IConversorModalidade> _conversoresMap =
        conversores.ToDictionary(c => c.Modalidade);

    public async Task<ContratoDto> Handle(ConverterEmContratoCommand cmd, CancellationToken ct)
    {
        // ... mesmo fluxo de carregar Cotacao, criar Contrato ...

        // Linha 100 antiga substituída por:
        IConversorModalidade conversor = _conversoresMap.GetValueOrDefault(cotacao.Modalidade)
            ?? throw new InvalidOperationException(
                $"Conversor não registrado para modalidade {cotacao.Modalidade}.");

        var ctx = new ConverterEmContratoContext(
            cotacao, propostaAceita, contrato, cmd, clock);

        (Entity detailPrincipal, Entity? detailSecundario) =
            await conversor.CriarDetailAsync(ctx, ct);

        contratoRepo.AddDetail(detailPrincipal);  // novo método polimórfico
        if (detailSecundario is not null)
        {
            contratoRepo.AddDetail(detailSecundario);
        }

        // ... resto do fluxo (CET, Economia, LimiteBanco, transição) ...
    }
}
```

**Nota sobre `contratoRepo.AddDetail`:** o repository atual tem `AddFinimpDetail` (linha 112 do command). Generaliza-se com:

```csharp
public interface IContratoRepository
{
    // ... métodos existentes ...
    void AddDetail(Entity detail);  // novo: aceita qualquer *Detail
}

// Implementação:
public void AddDetail(Entity detail)
{
    switch (detail)
    {
        case FinimpDetail f: context.FinimpDetails.Add(f); break;
        case Lei4131Detail l: context.Lei4131Details.Add(l); break;
        case RefinimpDetail r: context.RefinimpDetails.Add(r); break;
        case NceDetail n: context.NceDetails.Add(n); break;
        case CapitalDeGiroDetail bc: context.CapitalDeGiroDetails.Add(bc); break;
        case FgiDetail fg: context.FgiDetails.Add(fg); break;
        default: throw new ArgumentException($"Detail type {detail.GetType().Name} não suportado.");
    }
}
```

### 5.6. Registro DI

**Arquivo:** `src/Sgcf.Application/DependencyInjection.cs` (ou equivalente)

```csharp
services.AddScoped<IConversorModalidade, ConversorFinimp>();
services.AddScoped<IConversorModalidade, ConversorRefinimp>();
services.AddScoped<IConversorModalidade, ConversorLei4131>();
services.AddScoped<IConversorModalidade, ConversorNce>();
services.AddScoped<IConversorModalidade, ConversorCapitalDeGiro>();
services.AddScoped<IConversorModalidade, ConversorFgi>();
```

### 5.7. Testes de F0.3

```
tests/Sgcf.Application.Tests/Cotacoes/Conversores/ConversorFinimpTests.cs
  - CriarDetail_com_inputs_completos_retorna_FinimpDetail_populado
  - CriarDetail_com_RofNumero_nulo_persiste_null
  - Secundario_eh_sempre_null

tests/Sgcf.Application.Tests/Cotacoes/Conversores/ConversorStubsTests.cs
  - Refinimp/Lei4131/Nce/CapitalDeGiro/Fgi_lancam_NotImplementedException

tests/Sgcf.Api.IntegrationTests/Cotacoes/CotacoesFluxoTests.cs (regressão)
  - Fluxo completo FINIMP convertendo em contrato continua verde
```

---

## 6. Critérios de Aceite Globais da Onda 0

- [ ] Migration S6_PtaxNullable aplica e reverte sem erro em banco vazio e com dados FINIMP.
- [ ] `dotnet build sgcf-backend.sln` limpo (0 warnings, 0 errors).
- [ ] Suite Domain: **442 → 442+novos** verdes.
- [ ] Suite Application (fast): **108 → 108+novos** verdes.
- [ ] Suite IntegrationTests: **26 → 26+novos** verdes (regressão FINIMP intacta).
- [ ] Suite GoldenDataset: **18/18** verdes (3 FINIMP existentes não regridem).
- [ ] Snapshot JSON de `EconomiaNegociacao` antigo (com PTAX) deserializa sem erro.
- [ ] `ConverterEmContratoCommand` reduzido em ≥20 linhas (medida do diff).
- [ ] Documentação técnica atualizada: `docs/api/cotacoes.md` lista o status nullable da PTAX por modalidade.
- [ ] CHANGELOG `## [0.6.1] — <data>` ou consolidado em v0.7.0 com bloco `INTERNAL — Onda 0 Foundation`.

---

## 7. Estratégia de Testes

### 7.1. Testes unitários (small, ~80%)

- Pure logic da factory `Cotacao.Criar` com invariantes de PTAX.
- Métodos especializados de `CalculadoraCet` (estes têm massa só na entrega de cada modalidade).
- Cada `IConversorModalidade` em isolamento.

### 7.2. Testes de integração (medium, ~15%)

- `ConverterEmContratoCommand` rodando contra Postgres via Testcontainers, fluxo FINIMP end-to-end.
- Migration S6 sobre database com dados FINIMP existentes; round-trip JSON do `EconomiaNegociacao`.

### 7.3. Regressão de golden dataset (~5%)

- 3 cenários FINIMP existentes (USD com BB, CNY com NDF, USD com CDB cativo) recalculados após F0.2 produzem CET idêntico bit a bit.

### 7.4. Não testar

- Métodos `NotImplementedException` além de validar que a exceção é lançada (caráter contratual).
- Branches `BaseCalculo.Dias252` (fora de escopo).

---

## 8. Boundaries — Always / Ask First / Never

### 8.1. Always

- Manter compatibilidade binária do `CotacaoDto` (não adicionar nem remover campos top-level nesta Onda).
- Preservar `EconomiaNegociacao.SnapshotPropostaJson` formato (campos podem virar null, nunca sumir).
- Registrar os 6 conversores em DI, mesmo os ainda não implementados — evita exceção tardia em runtime.
- Para qualquer alteração em `CalculadoraCet`, validar contra os 3 cenários golden FINIMP antes de mergear.

### 8.2. Ask First

- Mudar a representação de PTAX em snapshots históricos (qualquer migration retroativa).
- Alterar a interface `IContratoRepository.AddDetail` se descobrir necessidade durante a implementação.
- Tornar `IConversorModalidade` parte do contrato público (`public` em vez de `internal`) — depende de plano de extensibilidade externa.
- Renumerar `ModalidadeContrato` (proibido por MD-2, mas se descobrir uma razão muito forte).

### 8.3. Never

- Renumerar `ModalidadeContrato` (MD-2).
- Generalizar `CalcularCet` num único método com 10+ parâmetros opcionais — força a usar os métodos especializados.
- Adicionar campos opcionais em `Proposta` para modalidades específicas (MD-5).
- Pular a migration "porque é só nullable" — algumas constraints de schema podem ser sensíveis em ambientes de homologação.
- Mexer em `Proposta` ou `EconomiaNegociacao` agregados nesta Onda.

---

## 9. Plano de Implementação

Referência: `tasks/cotacoes-modalidades/plan.md` §3 e `tasks/cotacoes-modalidades/todo.md` (seção Onda 0).

Ordem sugerida:

```
Dia 1: F0.1 PtaxNullable
  - Cotacao.cs + factory invariants
  - CriarCotacaoCommand handler
  - Migration S6
  - Testes Domain + Application
  - Commit

Dia 2: F0.2 CalculadoraCet refactor (PARALELIZÁVEL com Dia 3)
  - Extrair CalcularCetFinimp
  - Fachada CalcularCet dispatcheia por modalidade
  - 5 stubs NotImplemented
  - Testes Application + Golden regression
  - Commit

Dia 2-3: F0.3 IConversorModalidade dispatcher (PARALELIZÁVEL com Dia 2)
  - Criar interface + ConverterEmContratoContext record
  - ConversorFinimp extrai lógica existente
  - 5 stubs NotImplemented
  - Refatorar ConverterEmContratoCommand
  - IContratoRepository.AddDetail polimórfico
  - Registro DI
  - Testes Application + IntegrationTests regression
  - Commit

Dia 4: Doc + CHANGELOG + Checkpoint F0
  - docs/api/cotacoes.md (status PTAX por modalidade)
  - CHANGELOG bloco INTERNAL
  - Revisão humana das ADs
```

---

## 10. Versionamento

- **Internamente:** v0.6.1 (release tag opcional para marcar foundation).
- **Externamente:** consolidado na v0.7.0 (REFINIMP), referenciando "INTERNAL — Onda 0 Foundation" no CHANGELOG.
- **Quebra de contrato:** nenhuma para clientes atuais (PTAX continua aceito, deixa de ser exigido para BRL).

---

## 11. Dependências entre F0.1, F0.2 e F0.3

| | F0.1 | F0.2 | F0.3 |
|---|---|---|---|
| **F0.1 (PTAX nullable)** | — | Não bloqueia (F0.2 pode ser feito antes), mas é mais limpo depois | Bloqueia (F0.3 precisa do `decimal?` no contexto) |
| **F0.2 (CalcCet branches)** | — | — | Não bloqueia |
| **F0.3 (Dispatcher)** | — | — | — |

**Sequência recomendada:** F0.1 → (F0.2 ∥ F0.3). F0.2 e F0.3 podem ser executados em paralelo após F0.1 estar mergeado.
