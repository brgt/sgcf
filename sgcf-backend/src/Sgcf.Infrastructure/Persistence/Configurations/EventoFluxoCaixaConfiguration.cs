using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgcf.Domain.Tesouraria;

namespace Sgcf.Infrastructure.Persistence.Configurations;

internal sealed class EventoFluxoCaixaConfiguration : IEntityTypeConfiguration<EventoFluxoCaixa>
{
    public void Configure(EntityTypeBuilder<EventoFluxoCaixa> builder)
    {
        builder.ToTable("evento_fluxo_caixa");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(e => e.TenantId)
            .HasColumnName("tenant_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(e => e.Data)
            .HasColumnName("data")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(e => e.Tipo)
            .HasColumnName("tipo")
            .HasConversion(
                t => t.ToString().ToUpperInvariant(),
                s => Enum.Parse<TipoEventoFluxo>(s, true))
            .HasColumnType("varchar(10)")
            .IsRequired();

        // Backing fields para o Money — padrão do projeto (ver SaldoCaixaConfiguration).
        builder.Property(e => e.ValorDecimal)
            .HasColumnName("valor")
            .HasColumnType("numeric(20,6)")
            .IsRequired();

        builder.Property(e => e.ValorMoeda)
            .HasColumnName("valor_moeda")
            .HasConversion(SgcfConverters.Moeda)
            .HasColumnType("char(3)")
            .IsRequired();

        builder.Property(e => e.Descricao)
            .HasColumnName("descricao")
            .HasColumnType("varchar(500)")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(e => e.RegistradoPor)
            .HasColumnName("registrado_por")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(e => e.RegistradoEm)
            .HasColumnName("registrado_em")
            .HasColumnType("timestamptz")
            .IsRequired();

        // Computed property — não persiste diretamente.
        builder.Ignore(e => e.Valor);

        // Índice principal: consulta de fluxo por tenant + período.
        builder.HasIndex(e => new { e.TenantId, e.Data })
            .HasDatabaseName("ix_evento_fluxo_caixa_tenant_data");
    }
}
