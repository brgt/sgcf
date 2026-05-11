using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgcf.Domain.Contratos;

namespace Sgcf.Infrastructure.Persistence.Configurations;

internal sealed class ParcelaConfiguration : IEntityTypeConfiguration<Parcela>
{
    public void Configure(EntityTypeBuilder<Parcela> builder)
    {
        builder.ToTable("parcela");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();

        builder.Property(p => p.ContratoId)
            .HasColumnName("contrato_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(p => p.Numero)
            .HasColumnName("numero")
            .HasColumnType("smallint")
            .IsRequired();

        builder.Property(p => p.DataVencimento)
            .HasColumnName("data_vencimento")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(p => p.ValorPrincipalDecimal)
            .HasColumnName("valor_principal")
            .HasColumnType("numeric(20,6)")
            .IsRequired();

        builder.Property(p => p.ValorJurosDecimal)
            .HasColumnName("valor_juros")
            .HasColumnType("numeric(20,6)")
            .IsRequired();

        builder.Property(p => p.ValorPagoDecimal)
            .HasColumnName("valor_pago")
            .HasColumnType("numeric(20,6)")
            .IsRequired(false);

        builder.Property(p => p.Moeda)
            .HasColumnName("moeda")
            .HasConversion(SgcfConverters.Moeda)
            .HasColumnType("char(3)")
            .IsRequired();

        builder.Property(p => p.Status)
            .HasColumnName("status")
            .HasConversion(SgcfConverters.StatusParcela)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(p => p.DataPagamento)
            .HasColumnName("data_pagamento")
            .HasColumnType("date")
            .IsRequired(false);

        builder.HasIndex(p => new { p.ContratoId, p.Numero }).IsUnique();
        builder.HasIndex(p => p.DataVencimento);

        builder.Ignore(p => p.ValorPrincipal);
        builder.Ignore(p => p.ValorJuros);
        builder.Ignore(p => p.ValorPago);
    }
}
