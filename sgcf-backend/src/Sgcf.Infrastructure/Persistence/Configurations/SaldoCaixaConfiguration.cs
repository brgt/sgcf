using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgcf.Domain.Tesouraria;

namespace Sgcf.Infrastructure.Persistence.Configurations;

internal sealed class SaldoCaixaConfiguration : IEntityTypeConfiguration<SaldoCaixa>
{
    public void Configure(EntityTypeBuilder<SaldoCaixa> builder)
    {
        builder.ToTable("saldo_caixa");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(s => s.TenantId)
            .HasColumnName("tenant_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(s => s.ContaId)
            .HasColumnName("conta_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(s => s.DataReferencia)
            .HasColumnName("data_referencia")
            .HasColumnType("date")
            .IsRequired();

        // Backing decimal para o valor monetário — padrão do projeto (ver Contrato.ValorPrincipalDecimal).
        builder.Property(s => s.ValorDecimal)
            .HasColumnName("valor")
            .HasColumnType("numeric(20,6)")
            .IsRequired();

        builder.Property(s => s.ValorMoeda)
            .HasColumnName("valor_moeda")
            .HasConversion(SgcfConverters.Moeda)
            .HasColumnType("char(3)")
            .IsRequired();

        builder.Property(s => s.RegistradoPor)
            .HasColumnName("registrado_por")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(s => s.RegistradoEm)
            .HasColumnName("registrado_em")
            .HasColumnType("timestamptz")
            .IsRequired();

        // Garante unicidade: uma conta tem no máximo um saldo por data por tenant.
        builder.HasIndex(s => new { s.TenantId, s.ContaId, s.DataReferencia })
            .IsUnique()
            .HasDatabaseName("ux_saldo_caixa_tenant_conta_data");

        // Índice de acesso para consulta por conta ordenada por data DESC.
        builder.HasIndex(s => new { s.ContaId, s.DataReferencia })
            .HasDatabaseName("ix_saldo_caixa_conta_data");

        // Computed Money property — não persiste diretamente.
        builder.Ignore(s => s.Valor);
    }
}
