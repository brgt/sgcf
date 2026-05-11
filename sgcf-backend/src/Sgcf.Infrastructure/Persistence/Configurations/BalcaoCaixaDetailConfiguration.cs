using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgcf.Domain.Contratos;

namespace Sgcf.Infrastructure.Persistence.Configurations;

internal sealed class BalcaoCaixaDetailConfiguration : IEntityTypeConfiguration<BalcaoCaixaDetail>
{
    public void Configure(EntityTypeBuilder<BalcaoCaixaDetail> builder)
    {
        builder.ToTable("balcao_caixa_detail");

        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();

        builder.Property(b => b.ContratoId).HasColumnName("contrato_id").HasColumnType("uuid").IsRequired();
        builder.HasIndex(b => b.ContratoId).IsUnique();

        builder.Property(b => b.NumeroOperacao).HasColumnName("numero_operacao").HasColumnType("text").IsRequired(false);
        builder.Property(b => b.TipoProduto).HasColumnName("tipo_produto").HasColumnType("text").IsRequired(false);
        builder.Property(b => b.TemFgi).HasColumnName("tem_fgi").HasDefaultValue(false);
        builder.Property(b => b.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();
        builder.Property(b => b.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").IsRequired();
    }
}
