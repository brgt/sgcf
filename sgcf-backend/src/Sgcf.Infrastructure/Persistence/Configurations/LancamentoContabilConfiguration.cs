using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgcf.Domain.Contabilidade;

namespace Sgcf.Infrastructure.Persistence.Configurations;

internal sealed class LancamentoContabilConfiguration : IEntityTypeConfiguration<LancamentoContabil>
{
    public void Configure(EntityTypeBuilder<LancamentoContabil> builder)
    {
        builder.ToTable("lancamento_contabil");

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();

        builder.Property(l => l.ContratoId)
            .HasColumnName("contrato_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(l => l.Data)
            .HasColumnName("data")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(l => l.Origem)
            .HasColumnName("origem")
            .HasColumnType("varchar(50)")
            .IsRequired();

        builder.Property(l => l.ValorDecimal)
            .HasColumnName("valor")
            .HasColumnType("numeric(20,6)")
            .IsRequired();

        builder.Property(l => l.MoedaContrato)
            .HasColumnName("moeda_contrato")
            .HasConversion(SgcfConverters.Moeda)
            .HasColumnType("char(3)")
            .IsRequired();

        builder.Property(l => l.Descricao)
            .HasColumnName("descricao")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(l => l.CriadoEm)
            .HasColumnName("criado_em")
            .HasColumnType("timestamptz")
            .IsRequired();

        // Idempotency unique constraint: one entry per (contrato, data, origem)
        builder.HasIndex(l => new { l.ContratoId, l.Data, l.Origem })
            .IsUnique()
            .HasDatabaseName("UX_lancamento_contabil_contrato_data_origem");

        builder.Ignore(l => l.Valor);
    }
}
