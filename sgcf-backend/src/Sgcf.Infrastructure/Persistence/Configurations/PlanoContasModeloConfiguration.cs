using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgcf.Domain.Contabilidade;

namespace Sgcf.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuração EF Core para <see cref="PlanoContasModelo"/> — global, sem query filter.
///
/// Tabela <c>plano_contas_modelo</c> não é tenant-scoped.
/// Índice único em <c>codigo_gerencial</c> — cada código aparece uma única vez no modelo.
/// </summary>
internal sealed class PlanoContasModeloConfiguration : IEntityTypeConfiguration<PlanoContasModelo>
{
    public void Configure(EntityTypeBuilder<PlanoContasModelo> builder)
    {
        builder.ToTable("plano_contas_modelo", "sgcf");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(p => p.CodigoGerencial)
            .HasColumnName("codigo_gerencial")
            .HasColumnType("text")
            .HasMaxLength(20)
            .IsRequired();
        builder.HasIndex(p => p.CodigoGerencial).IsUnique();

        builder.Property(p => p.Nome)
            .HasColumnName("nome")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(p => p.Natureza)
            .HasColumnName("natureza")
            .HasColumnType("text")
            .HasConversion(SgcfConverters.NaturezaConta)
            .IsRequired();

        builder.Property(p => p.CodigoSapB1)
            .HasColumnName("codigo_sap_b1")
            .HasColumnType("text")
            .IsRequired(false);

        builder.Property(p => p.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(p => p.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz")
            .IsRequired();
    }
}
