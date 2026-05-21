# SPEC — Task 0.4 — Rules Engine Inicial de Alertas

> **Master:** `SPEC.md`
> **Plano:** `tasks/plan_cockpit_backend_gaps.md` Task 0.4
> **Status:** Draft
> **Versão:** v1.0
> **Escopo:** M
> **Dependências:** Tasks 0.2, 0.3

---

## 1. Objetivo

Construir o motor in-house de geração automática de alertas, rodando como `IHostedService` em `Sgcf.Jobs`, com 3 regras no MVP do cockpit. Substitui a geração ad-hoc atual em `GetPainelDividaQueryHandler.GerarAlertasSemHedge` e nos jobs existentes que criam `AlertaVencimento`.

---

## 2. Regras MVP

| Código | Categoria | Severidade | Origem | Disparo | Perfis |
|--------|-----------|------------|--------|---------|--------|
| `R-VENC-D7` | VENCIMENTO | ATENCAO | CONTRATO | parcela com `DataVencimento ∈ [hoje, hoje+7]` | FINANCEIRO, TESOURARIA |
| `R-VENC-D3` | VENCIMENTO | CRITICO | CONTRATO | parcela com `DataVencimento ∈ [hoje, hoje+3]` | FINANCEIRO, TESOURARIA |
| `R-VENC-D0` | VENCIMENTO | CRITICO | CONTRATO | parcela com `DataVencimento = hoje` | FINANCEIRO, TESOURARIA |
| `R-HEDGE-AUSENTE` | HEDGE | ATENCAO | CONTRATO | contrato ativo em moeda estrangeira sem hedge ativo vinculado | CFO, TESOURARIA |
| `R-LIMITE-85` | LIMITE | ATENCAO | LIMITE | `ValorUtilizadoBrl / ValorLimiteBrl ≥ 0.85` | CFO, FINANCEIRO |
| `R-LIMITE-95` | LIMITE | CRITICO | LIMITE | `ValorUtilizadoBrl / ValorLimiteBrl ≥ 0.95` | CFO, FINANCEIRO |

`R-VENC-*` substitui a geração atual de `AlertaVencimento`. `R-HEDGE-AUSENTE` substitui `GerarAlertasSemHedge`. `R-LIMITE-*` é novidade.

---

## 3. Arquitetura

```
Sgcf.Jobs/
  Alertas/
    AlertasHostedService.cs           ← coordena execução periódica
    Regras/
      IRegraAlerta.cs                 ← contrato
      RegraVencimentoIminente.cs      ← gera R-VENC-D7, D-3, D-0
      RegraContratoSemHedge.cs        ← gera R-HEDGE-AUSENTE
      RegraLimiteBancoUtilizacao.cs   ← gera R-LIMITE-85, R-LIMITE-95
    Schedules/
      AlertasSchedule.cs              ← cron expressions
```

### 3.1 Contrato de regra

```csharp
public interface IRegraAlerta
{
    string Codigo { get; }
    CronExpression Cron { get; }
    Task<IReadOnlyList<Alerta>> AvaliarAsync(IClock clock, CancellationToken ct);
}
```

Cada regra é **pura no critério de avaliação** (não persiste) — `AvaliarAsync` retorna a lista de candidatos; o hosted service chama `IAlertaRepository.AddAsync` aproveitando a idempotência por `ChaveIdempotencia` (Task 0.2).

### 3.2 Schedules

| Regra | Cron (UTC) | Equivalente BRT |
|-------|------------|------------------|
| `R-VENC-*` | `0 9 * * *` | 06:00 BRT |
| `R-HEDGE-AUSENTE` | `0 9 * * *` | 06:00 BRT |
| `R-LIMITE-*` | `*/5 * * * *` | a cada 5 min |

(Cron pode ser parametrizado em `appsettings.json` para ajuste sem deploy.)

### 3.3 Hosted service

```csharp
public sealed class AlertasHostedService(
    IServiceProvider sp,
    IClock clock,
    ILogger<AlertasHostedService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using IServiceScope scope = sp.CreateScope();
            IEnumerable<IRegraAlerta> regras = scope.ServiceProvider.GetServices<IRegraAlerta>();
            IAlertaRepository repo = scope.ServiceProvider.GetRequiredService<IAlertaRepository>();

            Instant agora = clock.GetCurrentInstant();

            foreach (IRegraAlerta regra in regras)
            {
                if (!regra.Cron.MatchesUtcMinute(agora))
                {
                    continue;
                }

                try
                {
                    IReadOnlyList<Alerta> candidatos = await regra.AvaliarAsync(clock, stoppingToken);
                    foreach (Alerta alerta in candidatos)
                    {
                        await repo.AddAsync(alerta, stoppingToken); // silencioso em duplicação
                    }
                    logger.LogInformation("Regra {Codigo}: {Count} candidatos avaliados", regra.Codigo, candidatos.Count);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Regra {Codigo} falhou", regra.Codigo);
                    // não interrompe as outras
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
        }
    }
}
```

---

## 4. Implementação das Regras

### 4.1 `RegraVencimentoIminente`

```csharp
public sealed class RegraVencimentoIminente(IParcelaRepository parcelaRepo) : IRegraAlerta
{
    public string Codigo => "R-VENC";
    public CronExpression Cron { get; } = CronExpression.Parse("0 9 * * *");

    public async Task<IReadOnlyList<Alerta>> AvaliarAsync(IClock clock, CancellationToken ct)
    {
        LocalDate hoje = clock.GetCurrentInstant().InZone(FusoBrasilia).Date;

        IReadOnlyList<Parcela> parcelas = await parcelaRepo
            .ListPendentesEntreAsync(hoje, hoje.PlusDays(7), ct);

        List<Alerta> alertas = new();
        foreach (Parcela p in parcelas)
        {
            int dias = Period.Between(hoje, p.DataVencimento, PeriodUnits.Days).Days;
            (SeveridadeAlerta sev, string sufixo) = dias switch
            {
                <= 0 => (SeveridadeAlerta.CRITICO, "D-0"),
                <= 3 => (SeveridadeAlerta.CRITICO, "D-3"),
                _    => (SeveridadeAlerta.ATENCAO, "D-7"),
            };

            alertas.Add(Alerta.Criar(
                CategoriaAlerta.VENCIMENTO,
                sev,
                titulo: $"Parcela {p.Numero} vence em {dias} dia(s)",
                descricao: $"Contrato {p.ContratoId} — valor {p.ValorPrincipal.Valor:N2} {p.Moeda}",
                origem: new OrigemAlerta(TipoOrigem.CONTRATO, p.ContratoId),
                perfisVisiveis: [PerfilCockpit.FINANCEIRO, PerfilCockpit.TESOURARIA],
                acao: new AcaoRecomendada(
                    Rotulo: "Ver contrato",
                    Rota: $"/app/finance/contratos/{p.ContratoId}"),
                expiraEm: clock.GetCurrentInstant() + Duration.FromDays(1),
                clock: clock));
        }
        return alertas;
    }
}
```

### 4.2 `RegraContratoSemHedge`

Reaproveita a lógica atual de `GetPainelDividaQueryHandler.GerarAlertasSemHedge`:

```csharp
public async Task<IReadOnlyList<Alerta>> AvaliarAsync(IClock clock, CancellationToken ct)
{
    var contratos = await _contratoRepo.ListByStatusAsync(StatusContrato.Ativo, ct);
    var hedgesAtivos = await _hedgeRepo.ListAtivosAsync(ct);
    var comHedge = hedgesAtivos.Select(h => h.ContratoId).ToHashSet();

    return contratos
        .Where(c => c.Moeda != Moeda.Brl && !comHedge.Contains(c.Id))
        .Select(c => Alerta.Criar(
            CategoriaAlerta.HEDGE,
            SeveridadeAlerta.ATENCAO,
            $"Contrato {c.NumeroExterno} em {c.Moeda} sem hedge",
            $"Exposição cambial não coberta — avaliar contratação de NDF",
            new OrigemAlerta(TipoOrigem.CONTRATO, c.Id),
            [PerfilCockpit.CFO, PerfilCockpit.TESOURARIA],
            new AcaoRecomendada("Contratar hedge", $"/app/finance/contratos/{c.Id}?tab=hedge"),
            expiraEm: null,
            clock))
        .ToList()
        .AsReadOnly();
}
```

### 4.3 `RegraLimiteBancoUtilizacao`

```csharp
public async Task<IReadOnlyList<Alerta>> AvaliarAsync(IClock clock, CancellationToken ct)
{
    var limites = await _limiteRepo.ListAsync(null, null, ct);

    return limites
        .Where(l => l.ValorLimiteBrl.Valor > 0)
        .Select(l => (limite: l, util: l.ValorUtilizadoBrl.Valor / l.ValorLimiteBrl.Valor))
        .Where(t => t.util >= 0.85m)
        .Select(t => Alerta.Criar(
            CategoriaAlerta.LIMITE,
            t.util >= 0.95m ? SeveridadeAlerta.CRITICO : SeveridadeAlerta.ATENCAO,
            $"Limite {t.limite.Modalidade} do banco {t.limite.BancoId:N} em {t.util:P0}",
            $"Utilizado R$ {t.limite.ValorUtilizadoBrl.Valor:N2} de R$ {t.limite.ValorLimiteBrl.Valor:N2}",
            new OrigemAlerta(TipoOrigem.LIMITE, t.limite.Id),
            [PerfilCockpit.CFO, PerfilCockpit.FINANCEIRO],
            new AcaoRecomendada("Ver limite", $"/app/finance/limites-banco/{t.limite.Id}"),
            expiraEm: null,
            clock))
        .ToList()
        .AsReadOnly();
}
```

---

## 5. Migração de Alertas Legados (Backfill)

Migration EF Core + comando manual `dotnet sgcf-cli migrar-alertas` (job one-shot) faz:

1. Lê últimos 7 dias de `alerta_vencimento` e `alerta_exposicao_banco`.
2. Para cada um, calcula `ChaveIdempotencia` no novo formato.
3. Insere em `alertas` se a chave não existe.

Não remove dados legados — desativação dos jobs antigos acontece após observabilidade de 1 sprint confirmar a paridade.

---

## 6. Observabilidade

- Métrica `sgcf_alertas_regra_execucoes_total{regra, resultado}` (counter).
- Métrica `sgcf_alertas_criados_total{categoria, severidade}` (counter).
- Métrica `sgcf_alertas_regra_duracao_segundos{regra}` (histogram).
- Log estruturado: `{ regra, dataInstant, candidatos, inseridos, ignorados }`.

---

## 7. Casos de Borda

| Cenário | Comportamento |
|---------|---------------|
| Regra leva mais que o intervalo (ex.: 6 min para regra de 5 min) | Próxima execução respeita o cron — sem fila acumulada |
| Falha de DB temporária | Log de erro; próximas regras continuam; tentativa nova no próximo tick |
| Parcela com `DataVencimento = hoje - 1` | Não gera D-0 (passou); fica para job de inadimplência (Task 2.1) |
| Hedge cancelado mas ainda na tabela com `Status = Ativo` | Tratado pela regra normalmente — responsabilidade do hedge é fechar status |
| Contrato BRL sem hedge | Não gera alerta (regra filtra `Moeda != Brl`) |
| Limite `ValorLimiteBrl = 0` | Ignorado para evitar divisão por zero |

---

## 8. Critérios de Aceite

- [ ] `AlertasHostedService` registrado em `Sgcf.Jobs` startup.
- [ ] Três regras concretas implementadas.
- [ ] Cron expressões configuráveis via `appsettings.Jobs.json`.
- [ ] Execução de cada regra é log-observable.
- [ ] Idempotência via `IAlertaRepository.AddAsync` (silencioso em duplicação).
- [ ] Testes unitários por regra (input fixture → lista esperada).
- [ ] Teste de integração rodando o hosted service em modo "now" cria os alertas esperados.
- [ ] Rodar 2x em sequência não duplica.
- [ ] Comando de backfill documentado e testado em ambiente de teste.
- [ ] Métricas Prometheus expostas (se infraestrutura já suporta) ou logs equivalentes.

---

## 9. Verificação

```bash
# Unit tests por regra
dotnet test --filter "FullyQualifiedName~RegraVencimento"
dotnet test --filter "FullyQualifiedName~RegraContratoSemHedge"
dotnet test --filter "FullyQualifiedName~RegraLimite"

# Integration: hosted service end-to-end
dotnet test --filter "FullyQualifiedName~AlertasHostedServiceTests"

# Backfill
dotnet run --project src/Sgcf.Jobs -- migrar-alertas --dias 7
```

**Teste-chave:**

```csharp
[Fact]
public async Task RegraVencimentoIminente_quando_executada_2x_no_mesmo_dia_nao_duplica()
{
    await _hostedService.ExecutarParaTesteAsync();
    int count1 = await _db.Alertas.CountAsync();

    await _hostedService.ExecutarParaTesteAsync();
    int count2 = await _db.Alertas.CountAsync();

    count2.Should().Be(count1);
}
```

---

## 10. Boundaries específicas

### 10.1 Always do
- Capturar exceção por regra; uma regra falhando não trava outras.
- Logar `regra, candidatos, inseridos, duplicados`.
- Idempotência por `ChaveIdempotencia`.

### 10.2 Ask first
- Adicionar regra que dispara alerta com severidade `CRITICO` para mais de 100 entidades simultaneamente (spam de cockpit).
- Mudar cron expression de regra existente (afeta SLA do FE).

### 10.3 Never do
- Bloquear thread principal — sempre async + `await Task.Delay`.
- Disparar e-mail/Slack dentro da regra (responsabilidade de canal separado, fora do escopo).
- Acessar `Sgcf.Api` a partir de `Sgcf.Jobs`.

---

## 11. Arquivos esperados

- `src/Sgcf.Jobs/Alertas/AlertasHostedService.cs`
- `src/Sgcf.Jobs/Alertas/Regras/IRegraAlerta.cs`
- `src/Sgcf.Jobs/Alertas/Regras/RegraVencimentoIminente.cs`
- `src/Sgcf.Jobs/Alertas/Regras/RegraContratoSemHedge.cs`
- `src/Sgcf.Jobs/Alertas/Regras/RegraLimiteBancoUtilizacao.cs`
- `src/Sgcf.Jobs/Alertas/Schedules/AlertasSchedule.cs`
- `src/Sgcf.Jobs/Alertas/Backfill/MigrarAlertasLegadosCommand.cs`
- `src/Sgcf.Jobs/Program.cs` (registrar DI)
- `src/Sgcf.Jobs/appsettings.Jobs.json` (cron expressions)
- `tests/Sgcf.Jobs.Tests/Alertas/RegraVencimentoIminenteTests.cs`
- `tests/Sgcf.Jobs.Tests/Alertas/RegraContratoSemHedgeTests.cs`
- `tests/Sgcf.Jobs.Tests/Alertas/RegraLimiteBancoUtilizacaoTests.cs`
- `tests/Sgcf.Jobs.Tests/Alertas/AlertasHostedServiceTests.cs`
