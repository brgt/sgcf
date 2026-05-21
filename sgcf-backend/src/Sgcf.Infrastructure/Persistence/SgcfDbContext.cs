using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Sgcf.Application.Cotacoes;
using Sgcf.Application.Tenancy;
using Sgcf.Domain.Alertas;
using Sgcf.Domain.Antecipacao;
using Sgcf.Domain.Auditoria;
using Sgcf.Domain.Bancos;
using Sgcf.Domain.Calendario;
using Sgcf.Domain.Contabilidade;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cambio;
using Sgcf.Domain.Cronograma;
using Sgcf.Domain.Cotacoes;
using Sgcf.Domain.Hedge;
using Sgcf.Domain.Painel;
using Sgcf.Domain.Simulacao;
using Sgcf.Domain.Sistema;
using Sgcf.Domain.Tenancy;

namespace Sgcf.Infrastructure.Persistence;

public class SgcfDbContext(
    DbContextOptions<SgcfDbContext> options,
    ITenantContext tenantContext) : DbContext(options)
{
    // Cached MethodInfo for AplicarFiltroDeTenant<T> — called once per entity type at model build time.
    private static readonly MethodInfo AplicarFiltroMethod =
        typeof(SgcfDbContext).GetMethod(
            nameof(AplicarFiltroDeTenant),
            BindingFlags.NonPublic | BindingFlags.Instance)!;
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Banco> Bancos => Set<Banco>();
    public DbSet<Contrato> Contratos => Set<Contrato>();
    public DbSet<Parcela> Parcelas => Set<Parcela>();
    public DbSet<Garantia> Garantias => Set<Garantia>();
    public DbSet<GarantiaCdbCativoDetail> GarantiaCdbCativoDetails => Set<GarantiaCdbCativoDetail>();
    public DbSet<GarantiaSblcDetail> GarantiaSblcDetails => Set<GarantiaSblcDetail>();
    public DbSet<GarantiaAvalDetail> GarantiaAvalDetails => Set<GarantiaAvalDetail>();
    public DbSet<GarantiaAlienacaoFiduciariaDetail> GarantiaAlienacaoFiduciariaDetails => Set<GarantiaAlienacaoFiduciariaDetail>();
    public DbSet<GarantiaDuplicatasDetail> GarantiaDuplicatasDetails => Set<GarantiaDuplicatasDetail>();
    public DbSet<GarantiaRecebiveisCartaoDetail> GarantiaRecebiveisCartaoDetails => Set<GarantiaRecebiveisCartaoDetail>();
    public DbSet<GarantiaBoletoBancarioDetail> GarantiaBoletoBancarioDetails => Set<GarantiaBoletoBancarioDetail>();
    public DbSet<GarantiaFgiDetail> GarantiaFgiDetails => Set<GarantiaFgiDetail>();
    public DbSet<FinimpDetail> FinimpDetails => Set<FinimpDetail>();
    public DbSet<Lei4131Detail> Lei4131Details => Set<Lei4131Detail>();
    public DbSet<RefinimpDetail> RefinimpDetails => Set<RefinimpDetail>();
    public DbSet<NceDetail> NceDetails => Set<NceDetail>();
    public DbSet<CapitalDeGiroDetail> CapitalDeGiroDetails => Set<CapitalDeGiroDetail>();
    public DbSet<FgiDetail> FgiDetails => Set<FgiDetail>();
    public DbSet<PlanoContasGerencial> PlanoContas => Set<PlanoContasGerencial>();
    public DbSet<PlanoContasModelo> PlanoContasModelo => Set<PlanoContasModelo>();
    public DbSet<InstrumentoHedge> InstrumentosHedge => Set<InstrumentoHedge>();
    public DbSet<PosicaoSnapshot> PosicoesSnapshot => Set<PosicaoSnapshot>();
    public DbSet<CotacaoFx> CotacoesFx => Set<CotacaoFx>();
    public DbSet<ParametroCotacao> ParametrosCotacao => Set<ParametroCotacao>();
    public DbSet<EventoCronograma> EventosCronograma => Set<EventoCronograma>();
    public DbSet<SimulacaoAntecipacao> SimulacoesAntecipacao => Set<SimulacaoAntecipacao>();
    public DbSet<EbitdaMensal> EbitdasMensais => Set<EbitdaMensal>();
    public DbSet<Alerta> Alertas => Set<Alerta>();
    public DbSet<AlertaPerfilVisivel> AlertasPerfisVisiveis => Set<AlertaPerfilVisivel>();
    public DbSet<AlertaVencimento> AlertasVencimento => Set<AlertaVencimento>();
    public DbSet<AlertaExposicaoBanco> AlertasExposicaoBanco => Set<AlertaExposicaoBanco>();
    public DbSet<SnapshotMensalPosicao> SnapshotsMensais => Set<SnapshotMensalPosicao>();
    public DbSet<LancamentoContabil> LancamentosContabeis => Set<LancamentoContabil>();
    public DbSet<Feriado> Feriados => Set<Feriado>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Cotacao> Cotacoes => Set<Cotacao>();
    public DbSet<Proposta> Propostas => Set<Proposta>();
    public DbSet<LimiteBanco> LimitesBanco => Set<LimiteBanco>();
    public DbSet<EconomiaNegociacao> EconomiasNegociacao => Set<EconomiaNegociacao>();
    public DbSet<CdiSnapshot> CdiSnapshots => Set<CdiSnapshot>();
    public DbSet<CenarioSimulacao> CenariosSimulacao => Set<CenarioSimulacao>();
    public DbSet<SimulacaoContratacao> SimulacoesContratacao => Set<SimulacaoContratacao>();
    public DbSet<ParametroSistema> ParametrosSistema => Set<ParametroSistema>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("sgcf");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SgcfDbContext).Assembly);

        // Aplica global query filter em todas as entidades que implementam ITenantScoped.
        // Usa reflection para chamar o método genérico tipado por entidade — necessário
        // porque ModelBuilder.Entity<T>().HasQueryFilter() exige o tipo concreto em compile time.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ITenantScoped).IsAssignableFrom(entityType.ClrType))
            {
                AplicarFiltroMethod
                    .MakeGenericMethod(entityType.ClrType)
                    .Invoke(this, [modelBuilder]);
            }
        }

        base.OnModelCreating(modelBuilder);
    }

    /// <summary>
    /// Aplica o filtro de tenant na entidade <typeparamref name="T"/>.
    ///
    /// Combina (AND) com qualquer filtro já registrado (ex.: soft-delete) para que ambos
    /// coexistam. <see cref="ITenantContext.TenantIdOrDefault"/> é usado em vez de
    /// <see cref="ITenantContext.TenantId"/> para evitar <c>MissingTenantContextException</c>
    /// quando o EF Core avalia os parâmetros de closure antes de aplicar curto-circuito.
    /// Quando não resolvido, o filtro retorna <c>false</c> — zero linhas visíveis.
    /// Operações cross-tenant (provisioner, seeds) devem resolver o contexto explicitamente
    /// via <see cref="ITenantContext.Resolve"/> antes de emitir queries.
    /// </summary>
    private void AplicarFiltroDeTenant<T>(ModelBuilder modelBuilder)
        where T : class, ITenantScoped
    {
        Expression<Func<T, bool>> tenantFilter =
            e => tenantContext.IsResolved && e.TenantId == tenantContext.TenantIdOrDefault;

        LambdaExpression? existingFilter =
            modelBuilder.Entity<T>().Metadata.GetQueryFilter();

        if (existingFilter is null)
        {
            modelBuilder.Entity<T>().HasQueryFilter(tenantFilter);
            return;
        }

        // Combine existingFilter AND tenantFilter, unifying their parameters.
        ParameterExpression param = tenantFilter.Parameters[0];
        Expression existingBody = new ParameterReplacer(existingFilter.Parameters[0], param)
            .Visit(existingFilter.Body);

        modelBuilder.Entity<T>().HasQueryFilter(
            Expression.Lambda<Func<T, bool>>(
                Expression.AndAlso(existingBody, tenantFilter.Body), param));
    }

    private sealed class ParameterReplacer(ParameterExpression from, ParameterExpression to)
        : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node)
            => node == from ? to : base.VisitParameter(node);
    }
}
