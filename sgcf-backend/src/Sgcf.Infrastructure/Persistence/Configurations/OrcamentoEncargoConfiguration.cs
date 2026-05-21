using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgcf.Domain.OrcamentosEncargo;

namespace Sgcf.Infrastructure.Persistence.Configurations;

internal sealed class OrcamentoEncargoConfiguration : IEntityTypeConfiguration<OrcamentoEncargo>
{
    public void Configure(EntityTypeBuilder<OrcamentoEncargo> builder)
    {
        builder.ToTable("orcamento_encargo", "sgcf");

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

        builder.Property(e => e.TipoEncargo)
            .HasColumnName("tipo_encargo")
            .HasColumnType("varchar(50)")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.ValorOrcadoBrlDecimal)
            .HasColumnName("valor_orcado_brl_decimal")
            .HasColumnType("numeric(18,4)")
            .IsRequired();

        builder.Property(e => e.BancoId)
            .HasColumnName("banco_id")
            .HasColumnType("uuid");

        builder.Property(e => e.ContratoId)
            .HasColumnName("contrato_id")
            .HasColumnType("uuid");

        builder.Property(e => e.Observacao)
            .HasColumnName("observacao")
            .HasColumnType("text")
            .HasMaxLength(500);

        builder.Property(e => e.CriadoEm)
            .HasColumnName("criado_em")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(e => e.AtualizadoEm)
            .HasColumnName("atualizado_em")
            .HasColumnType("timestamptz")
            .IsRequired();

        // Unique constraint: apenas um registro por (tenant, ano, mês, tipo, banco, contrato).
        builder.HasIndex(e => new { e.TenantId, e.Ano, e.Mes, e.TipoEncargo, e.BancoId, e.ContratoId })
            .IsUnique()
            .HasDatabaseName("ux_orcamento_encargo_periodo_tipo_banco_contrato");

        // Propriedade computada — EF não deve tentar mapeá-la como coluna.
        builder.Ignore(e => e.ValorOrcadoBrl);
    }
}
