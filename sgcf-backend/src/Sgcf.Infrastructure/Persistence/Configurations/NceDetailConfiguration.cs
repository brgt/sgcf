using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgcf.Domain.Contratos;

namespace Sgcf.Infrastructure.Persistence.Configurations;

internal sealed class NceDetailConfiguration : IEntityTypeConfiguration<NceDetail>
{
    public void Configure(EntityTypeBuilder<NceDetail> builder)
    {
        builder.ToTable("nce_detail");

        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();

        builder.Property(n => n.ContratoId).HasColumnName("contrato_id").HasColumnType("uuid").IsRequired();
        builder.HasIndex(n => n.ContratoId).IsUnique();

        builder.Property(n => n.NceNumero).HasColumnName("nce_numero").HasColumnType("text").IsRequired(false);
        builder.Property(n => n.DataEmissao).HasColumnName("data_emissao").HasColumnType("date").IsRequired(false);
        builder.Property(n => n.BancoMandatario).HasColumnName("banco_mandatario").HasColumnType("text").IsRequired(false);
        builder.Property(n => n.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();
        builder.Property(n => n.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").IsRequired();
    }
}
