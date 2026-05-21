# SPEC — Task 0.2 — Agregado `Alerta` Unificado

> **Master:** `SPEC.md`
> **Plano:** `tasks/plan_cockpit_backend_gaps.md` Task 0.2
> **Status:** Draft
> **Versão:** v1.0
> **Escopo:** M
> **Dependências:** Task 0.1 (envelope) para os endpoints subsequentes (Task 0.3)

---

## 1. Objetivo

Substituir os agregados isolados `AlertaVencimento` e `AlertaExposicaoBanco` e o anti-padrão `IReadOnlyList<string> Alertas` por um agregado unificado `Alerta` com severidade, categoria, ação recomendada e visibilidade por perfil — capaz de alimentar a faixa de alertas da Camada 2 do cockpit (UX §2.1).

---

## 2. Modelo de Domínio

### 2.1 Enums

```csharp
namespace Sgcf.Domain.Alertas;

public enum CategoriaAlerta : byte
{
    COVENANT     = 1,
    VENCIMENTO   = 2,
    HEDGE        = 3,
    LIQUIDEZ     = 4,
    DOCUMENTO    = 5,
    LIMITE       = 6,
    REGULATORIO  = 7,
    OPERACIONAL  = 8,
}

public enum SeveridadeAlerta : byte
{
    INFORMATIVO = 1,
    ATENCAO     = 2,
    CRITICO     = 3,
}

public enum StatusAlerta : byte
{
    ABERTO     = 1,
    LIDO       = 2,
    DISPENSADO = 3,
}

public enum PerfilCockpit : byte
{
    CFO         = 1,
    FINANCEIRO  = 2,
    TESOURARIA  = 3,
}

public enum TipoOrigem : byte
{
    CONTRATO  = 1,
    COTACAO   = 2,
    HEDGE     = 3,
    LIMITE    = 4,
    COVENANT  = 5,
    DOCUMENTO = 6,
    SISTEMA   = 7,
}
```

### 2.2 Value objects

```csharp
public sealed record OrigemAlerta(TipoOrigem Tipo, Guid Id);

public sealed record AcaoRecomendada(string Rotulo, string Rota);
```

### 2.3 Agregado

```csharp
public sealed class Alerta : Entity
{
    public CategoriaAlerta Categoria { get; private set; }
    public SeveridadeAlerta Severidade { get; private set; }
    public string Titulo { get; private set; } = default!;
    public string Descricao { get; private set; } = default!;
    public OrigemAlerta Origem { get; private set; } = default!;
    public AcaoRecomendada? Acao { get; private set; }
    public IReadOnlyList<PerfilCockpit> PerfisVisiveis { get; private set; } = Array.Empty<PerfilCockpit>();
    public StatusAlerta Status { get; private set; }
    public Instant CriadoEm { get; private set; }
    public Instant? ExpiraEm { get; private set; }
    public Instant? DispensadoEm { get; private set; }
    public string? DispensadoPor { get; private set; }
    public string ChaveIdempotencia { get; private set; } = default!;

    private Alerta() { }

    public static Alerta Criar(
        CategoriaAlerta categoria,
        SeveridadeAlerta severidade,
        string titulo,
        string descricao,
        OrigemAlerta origem,
        IReadOnlyList<PerfilCockpit> perfisVisiveis,
        AcaoRecomendada? acao,
        Instant? expiraEm,
        IClock clock)
    {
        if (string.IsNullOrWhiteSpace(titulo)) throw new ArgumentException(nameof(titulo));
        if (string.IsNullOrWhiteSpace(descricao)) throw new ArgumentException(nameof(descricao));
        if (perfisVisiveis is null || perfisVisiveis.Count == 0)
            throw new ArgumentException("PerfisVisiveis deve conter ao menos um perfil.", nameof(perfisVisiveis));

        Instant agora = clock.GetCurrentInstant();
        LocalDate hoje = agora.InZone(DateTimeZoneProviders.Tzdb["America/Sao_Paulo"]).Date;

        return new Alerta
        {
            Categoria = categoria,
            Severidade = severidade,
            Titulo = titulo.Trim(),
            Descricao = descricao.Trim(),
            Origem = origem,
            Acao = acao,
            PerfisVisiveis = perfisVisiveis,
            Status = StatusAlerta.ABERTO,
            CriadoEm = agora,
            ExpiraEm = expiraEm,
            ChaveIdempotencia = MontarChave(origem, categoria, hoje),
        };
    }

    public void MarcarComoLido(IClock clock)
    {
        if (Status == StatusAlerta.DISPENSADO) return;
        Status = StatusAlerta.LIDO;
    }

    public void Dispensar(string usuario, IClock clock)
    {
        if (Status == StatusAlerta.DISPENSADO) return;
        Status = StatusAlerta.DISPENSADO;
        DispensadoEm = clock.GetCurrentInstant();
        DispensadoPor = usuario;
    }

    private static string MontarChave(OrigemAlerta origem, CategoriaAlerta categoria, LocalDate dia) =>
        $"{origem.Tipo}:{origem.Id:N}:{categoria}:{dia:yyyy-MM-dd}";
}
```

### 2.4 Repositório

```csharp
public interface IAlertaRepository
{
    Task<Alerta?> GetByChaveIdempotenciaAsync(string chave, CancellationToken ct);
    Task AddAsync(Alerta alerta, CancellationToken ct);
    Task<Alerta?> GetAsync(Guid id, CancellationToken ct);
    Task<PagedResult<Alerta>> ListAsync(AlertaFilter filtro, CancellationToken ct);
    Task<ContadoresAlerta> GetContadoresAsync(PerfilCockpit perfil, CancellationToken ct);
}

public sealed record AlertaFilter(
    PerfilCockpit Perfil,
    SeveridadeAlerta? Severidade,
    CategoriaAlerta? Categoria,
    StatusAlerta? Status,
    int Page = 1,
    int PageSize = 25);

public sealed record ContadoresAlerta(int Critico, int Atencao, int Informativo);
```

---

## 3. Schema PostgreSQL

```sql
CREATE TABLE alertas (
    id                  UUID         PRIMARY KEY,
    categoria           SMALLINT     NOT NULL,
    severidade          SMALLINT     NOT NULL,
    titulo              TEXT         NOT NULL,
    descricao           TEXT         NOT NULL,
    origem_tipo         SMALLINT     NOT NULL,
    origem_id           UUID         NOT NULL,
    acao_rotulo         TEXT         NULL,
    acao_rota           TEXT         NULL,
    perfis_visiveis     SMALLINT[]   NOT NULL,
    status              SMALLINT     NOT NULL,
    criado_em           TIMESTAMPTZ  NOT NULL,
    expira_em           TIMESTAMPTZ  NULL,
    dispensado_em       TIMESTAMPTZ  NULL,
    dispensado_por      TEXT         NULL,
    chave_idempotencia  TEXT         NOT NULL,
    CONSTRAINT uq_alertas_chave UNIQUE (chave_idempotencia)
);

CREATE INDEX ix_alertas_perfil_status_severidade
    ON alertas USING GIN (perfis_visiveis)
    WHERE status <> 3; -- 3 = DISPENSADO

CREATE INDEX ix_alertas_criado_em ON alertas (criado_em DESC);
```

**Observação:** `perfis_visiveis SMALLINT[]` permite filtragem com operador `&&` (intersecção) no Postgres, eficiente com índice GIN.

---

## 4. EF Core Configuration

```csharp
public sealed class AlertaConfiguration : IEntityTypeConfiguration<Alerta>
{
    public void Configure(EntityTypeBuilder<Alerta> b)
    {
        b.ToTable("alertas");
        b.HasKey(a => a.Id);

        b.Property(a => a.Categoria).HasConversion<byte>().HasColumnName("categoria");
        b.Property(a => a.Severidade).HasConversion<byte>().HasColumnName("severidade");
        b.Property(a => a.Status).HasConversion<byte>().HasColumnName("status");
        b.Property(a => a.Titulo).HasColumnName("titulo").HasMaxLength(200).IsRequired();
        b.Property(a => a.Descricao).HasColumnName("descricao").IsRequired();

        b.OwnsOne(a => a.Origem, o =>
        {
            o.Property(x => x.Tipo).HasConversion<byte>().HasColumnName("origem_tipo");
            o.Property(x => x.Id).HasColumnName("origem_id");
        });

        b.OwnsOne(a => a.Acao, o =>
        {
            o.Property(x => x.Rotulo).HasColumnName("acao_rotulo").HasMaxLength(100);
            o.Property(x => x.Rota).HasColumnName("acao_rota").HasMaxLength(500);
        });

        b.Property(a => a.PerfisVisiveis)
            .HasConversion(
                v => v.Select(p => (byte)p).ToArray(),
                v => v.Select(b => (PerfilCockpit)b).ToList().AsReadOnly())
            .HasColumnType("smallint[]")
            .HasColumnName("perfis_visiveis");

        b.Property(a => a.CriadoEm).HasColumnName("criado_em");
        b.Property(a => a.ExpiraEm).HasColumnName("expira_em");
        b.Property(a => a.DispensadoEm).HasColumnName("dispensado_em");
        b.Property(a => a.DispensadoPor).HasColumnName("dispensado_por").HasMaxLength(120);
        b.Property(a => a.ChaveIdempotencia).HasColumnName("chave_idempotencia").IsRequired();

        b.HasIndex(a => a.ChaveIdempotencia).IsUnique();
    }
}
```

---

## 5. Compatibilidade e Migração

### 5.1 Conviver com legados

Os agregados `AlertaVencimento` e `AlertaExposicaoBanco` **continuam existindo** durante a Fase 0 — viram **emissores** de instâncias de `Alerta`. As tabelas legadas não são removidas nesta task.

`PainelDividaDto.Alertas` e `PainelGarantiasDto.Alertas` (campos `IReadOnlyList<string>`) **continuam preenchidos** por 2 sprints. Eles passam a derivar das mesmas regras que geram o `Alerta` unificado (apenas a apresentação textual).

### 5.2 Backfill inicial

A Task 0.4 (rules engine) inclui um job idempotente de backfill que lê os alertas legados das últimas 24 h e cria os correspondentes na nova tabela com `ChaveIdempotencia` adequada.

---

## 6. Idempotência

`ChaveIdempotencia` evita duplicação por combinação `(origem.Tipo, origem.Id, categoria, dia BRT)`. Tentativa de inserir duplicada deve ser silenciosamente ignorada pelo repositório:

```csharp
public async Task AddAsync(Alerta alerta, CancellationToken ct)
{
    try
    {
        await _db.Alertas.AddAsync(alerta, ct);
        await _db.SaveChangesAsync(ct);
    }
    catch (DbUpdateException ex) when (IsUniqueViolation(ex, "uq_alertas_chave"))
    {
        // já existe alerta com mesma chave para o dia; silencioso
    }
}
```

---

## 7. Casos de Borda

| Cenário | Comportamento |
|---------|---------------|
| Regra dispara duas vezes no mesmo dia para a mesma origem | Segunda inserção é ignorada (idempotência) |
| Alerta criado e dispensado no mesmo dia, regra dispara de novo | Não recria (chave_idempotencia ainda existe) |
| Alerta com `ExpiraEm` no passado | Listado normalmente mas marcado como expirado no DTO via campo derivado |
| `PerfisVisiveis = [CFO]` consultado por usuário Tesouraria | Não aparece (filtro `&& ARRAY[3]` falso) |
| `AcaoRecomendada` ausente | Cockpit FE apresenta alerta sem botão de CTA |
| Origem aponta para entidade que foi soft-deletada | Alerta permanece visível; cockpit exibe aviso ao clicar (tratado no FE) |

---

## 8. Critérios de Aceite

- [ ] Enums e value objects criados em `Sgcf.Domain.Alertas`.
- [ ] Agregado `Alerta` com fábrica `Criar(...)`, transições `MarcarComoLido` e `Dispensar`.
- [ ] Interface `IAlertaRepository` + implementação EF Core em `Sgcf.Infrastructure`.
- [ ] Migration cria tabela `alertas` com índices descritos.
- [ ] Constraint `uq_alertas_chave` impede duplicação.
- [ ] `AddAsync` é silencioso em duplicação.
- [ ] Configuração EF Core mapeia `perfis_visiveis` como `smallint[]`.
- [ ] Soft delete (se aplicável) **não** é usado — alertas dispensados ficam com `Status = DISPENSADO`.

---

## 9. Verificação

```bash
# Migration aplica e reverte
dotnet ef migrations add AddAlertasUnificados --project src/Sgcf.Infrastructure --startup-project src/Sgcf.Api
dotnet ef database update --project src/Sgcf.Infrastructure --startup-project src/Sgcf.Api

# Unit tests do agregado
dotnet test --filter "FullyQualifiedName~Alerta" tests/Sgcf.Domain.Tests/

# Integration: idempotência
dotnet test --filter "FullyQualifiedName~AlertaRepository" tests/Sgcf.Application.Tests/
```

**Teste-chave (FluentAssertions):**

```csharp
[Fact]
public async Task AddAsync_quando_chave_idempotencia_duplicada_nao_lanca()
{
    var alerta1 = Alerta.Criar(CategoriaAlerta.COVENANT, SeveridadeAlerta.CRITICO,
        "X", "Y", new OrigemAlerta(TipoOrigem.CONTRATO, _contratoId),
        [PerfilCockpit.CFO], null, null, _clock);

    var alerta2 = Alerta.Criar(CategoriaAlerta.COVENANT, SeveridadeAlerta.CRITICO,
        "X2", "Y2", new OrigemAlerta(TipoOrigem.CONTRATO, _contratoId),
        [PerfilCockpit.CFO], null, null, _clock);

    await _repo.AddAsync(alerta1, default);
    Func<Task> act = () => _repo.AddAsync(alerta2, default);

    await act.Should().NotThrowAsync();
    (await _db.Alertas.CountAsync()).Should().Be(1);
}
```

---

## 10. Boundaries específicas

### 10.1 Always do
- Construir `ChaveIdempotencia` pelo método estático `MontarChave` — nunca à mão.
- Validar `PerfisVisiveis` não-vazio.
- Usar `IClock` para todas as datas.

### 10.2 Ask first
- Adicionar nova `CategoriaAlerta` ou `TipoOrigem` — exige atualização de regras downstream e FE.

### 10.3 Never do
- Permitir `Status = DISPENSADO` voltar para `ABERTO`.
- Apagar fisicamente alertas (sem hard delete).
- Persistir `PerfisVisiveis` como JSON — usar `smallint[]` nativo do Postgres.

---

## 11. Arquivos esperados

- `src/Sgcf.Domain/Alertas/Alerta.cs`
- `src/Sgcf.Domain/Alertas/CategoriaAlerta.cs`
- `src/Sgcf.Domain/Alertas/SeveridadeAlerta.cs`
- `src/Sgcf.Domain/Alertas/StatusAlerta.cs`
- `src/Sgcf.Domain/Alertas/PerfilCockpit.cs`
- `src/Sgcf.Domain/Alertas/TipoOrigem.cs`
- `src/Sgcf.Domain/Alertas/OrigemAlerta.cs`
- `src/Sgcf.Domain/Alertas/AcaoRecomendada.cs`
- `src/Sgcf.Application/Alertas/IAlertaRepository.cs`
- `src/Sgcf.Application/Alertas/AlertaFilter.cs`
- `src/Sgcf.Application/Alertas/ContadoresAlerta.cs`
- `src/Sgcf.Infrastructure/Persistence/Configurations/AlertaConfiguration.cs`
- `src/Sgcf.Infrastructure/Persistence/Repositories/AlertaRepository.cs`
- `src/Sgcf.Infrastructure/Migrations/<ts>_AddAlertasUnificados.cs`
- `tests/Sgcf.Domain.Tests/Alertas/AlertaTests.cs`
- `tests/Sgcf.Application.Tests/Alertas/AlertaRepositoryTests.cs`
