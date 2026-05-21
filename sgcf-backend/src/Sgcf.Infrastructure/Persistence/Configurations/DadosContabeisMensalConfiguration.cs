using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgcf.Domain.Contabilidade;

namespace Sgcf.Infrastructure.Persistence.Configurations;

internal sealed class DadosContabeisMensalConfiguration : IEntityTypeConfiguration<DadosContabeisMensal>
{
    public void Configure(EntityTypeBuilder<DadosContabeisMensal> builder)
    {
        builder.ToTable("dados_contabeis_mensal");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(e => e.TenantId)
            .HasColumnName("tenant_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(e => e.Ano)
            .HasColumnName("ano")
            .HasColumnType("integer")
            .IsRequired();

        builder.Property(e => e.Mes)
            .HasColumnName("mes")
            .HasColumnType("integer")
            .IsRequired();

        builder.Property(e => e.PatrimonioLiquidoDecimal)
            .HasColumnName("patrimonio_liquido_brl")
            .HasColumnType("numeric(20,6)")
            .IsRequired();

        builder.Property(e => e.DespesaFinanceiraDecimal)
            .HasColumnName("despesa_financeira_brl")
            .HasColumnType("numeric(20,6)")
            .IsRequired();

        builder.Property(e => e.CriadoEm)
            .HasColumnName("criado_em")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(e => e.AtualizadoEm)
            .HasColumnName("atualizado_em")
            .HasColumnType("timestamptz")
            .IsRequired();

        // Unique constraint: apenas um registro de dados contábeis por (tenant, ano, mês).
        builder.HasIndex(e => new { e.TenantId, e.Ano, e.Mes })
            .IsUnique()
            .HasDatabaseName("ux_dados_contabeis_tenant_competencia");

        // Propriedades computadas — EF não deve tentar mapeá-las como colunas.
        builder.Ignore(e => e.PatrimonioLiquido);
        builder.Ignore(e => e.DespesaFinanceira);
    }
}
