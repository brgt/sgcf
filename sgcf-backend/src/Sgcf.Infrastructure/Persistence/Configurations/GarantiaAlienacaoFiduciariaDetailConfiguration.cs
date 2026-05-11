using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgcf.Domain.Contratos;

namespace Sgcf.Infrastructure.Persistence.Configurations;

internal sealed class GarantiaAlienacaoFiduciariaDetailConfiguration
    : IEntityTypeConfiguration<GarantiaAlienacaoFiduciariaDetail>
{
    public void Configure(EntityTypeBuilder<GarantiaAlienacaoFiduciariaDetail> builder)
    {
        builder.ToTable("garantia_alienacao_fiduciaria_detail");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();

        builder.Property(e => e.GarantiaId).HasColumnName("garantia_id").HasColumnType("uuid").IsRequired();
        builder.HasIndex(e => e.GarantiaId).IsUnique();

        builder.Property(e => e.TipoBem).HasColumnName("tipo_bem").HasColumnType("text").IsRequired();
        builder.Property(e => e.DescricaoBem).HasColumnName("descricao_bem").HasColumnType("text").IsRequired();

        builder.Property(e => e.ValorAvaliadoDecimal)
            .HasColumnName("valor_avaliado").HasColumnType("numeric(20,6)").IsRequired();

        builder.Property(e => e.MatriculaOuChassi)
            .HasColumnName("matricula_ou_chassi").HasColumnType("text").IsRequired(false);
        builder.Property(e => e.CartorioRegistro)
            .HasColumnName("cartorio_registro").HasColumnType("text").IsRequired(false);

        builder.HasOne<Garantia>().WithOne().HasForeignKey<GarantiaAlienacaoFiduciariaDetail>(e => e.GarantiaId).IsRequired();

        builder.Ignore(e => e.ValorAvaliado);
    }
}
