using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Infrastructure.Persistence.Configurations;

internal sealed class ParametroCotacaoConfiguration : IEntityTypeConfiguration<ParametroCotacao>
{
    public void Configure(EntityTypeBuilder<ParametroCotacao> builder)
    {
        builder.ToTable("parametro_cotacao");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();

        builder.Property(p => p.BancoId)
            .HasColumnName("banco_id")
            .HasColumnType("uuid")
            .IsRequired(false);

        builder.Property(p => p.Modalidade)
            .HasColumnName("modalidade")
            .HasConversion(
                m => m == null ? null
                   : m == ModalidadeContrato.Finimp ? "FINIMP"
                   : m == ModalidadeContrato.Refinimp ? "REFINIMP"
                   : m == ModalidadeContrato.Lei4131 ? "LEI_4131"
                   : m == ModalidadeContrato.Nce ? "NCE"
                   : m == ModalidadeContrato.BalcaoCaixa ? "BALCAO_CAIXA"
                   : "FGI",
                s => s == null ? (ModalidadeContrato?)null
                   : s == "FINIMP" ? ModalidadeContrato.Finimp
                   : s == "REFINIMP" ? ModalidadeContrato.Refinimp
                   : s == "LEI_4131" ? ModalidadeContrato.Lei4131
                   : s == "NCE" ? ModalidadeContrato.Nce
                   : s == "BALCAO_CAIXA" ? ModalidadeContrato.BalcaoCaixa
                   : (ModalidadeContrato?)ModalidadeContrato.Fgi)
            .HasColumnType("text")
            .IsRequired(false);

        builder.Property(p => p.TipoCotacao)
            .HasColumnName("tipo_cotacao")
            .HasConversion(SgcfConverters.TipoCotacao)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(p => p.Ativo)
            .HasColumnName("ativo")
            .HasDefaultValue(true);

        builder.Property(p => p.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(p => p.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.HasIndex(p => new { p.BancoId, p.Modalidade });
    }
}
