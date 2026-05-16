using Microsoft.EntityFrameworkCore;
using Sgcf.Application.Cotacoes;
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

namespace Sgcf.Infrastructure.Persistence;

public class SgcfDbContext(DbContextOptions<SgcfDbContext> options) : DbContext(options)
{
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
    public DbSet<BalcaoCaixaDetail> BalcaoCaixaDetails => Set<BalcaoCaixaDetail>();
    public DbSet<FgiDetail> FgiDetails => Set<FgiDetail>();
    public DbSet<PlanoContasGerencial> PlanoContas => Set<PlanoContasGerencial>();
    public DbSet<InstrumentoHedge> InstrumentosHedge => Set<InstrumentoHedge>();
    public DbSet<PosicaoSnapshot> PosicoesSnapshot => Set<PosicaoSnapshot>();
    public DbSet<CotacaoFx> CotacoesFx => Set<CotacaoFx>();
    public DbSet<ParametroCotacao> ParametrosCotacao => Set<ParametroCotacao>();
    public DbSet<EventoCronograma> EventosCronograma => Set<EventoCronograma>();
    public DbSet<SimulacaoAntecipacao> SimulacoesAntecipacao => Set<SimulacaoAntecipacao>();
    public DbSet<EbitdaMensal> EbitdasMensais => Set<EbitdaMensal>();
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("sgcf");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SgcfDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
