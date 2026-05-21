using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgcf.Domain.Alertas;

namespace Sgcf.Infrastructure.Persistence.Configurations;

internal sealed class AlertaConfiguration : IEntityTypeConfiguration<Alerta>
{
    public void Configure(EntityTypeBuilder<Alerta> builder)
    {
        builder.ToTable("alertas");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(a => a.TenantId).HasColumnName("tenant_id").HasColumnType("uuid").IsRequired();

        builder.Property(a => a.Categoria)
            .HasColumnName("categoria")
            .HasColumnType("smallint")
            .HasConversion<byte>()
            .IsRequired();

        builder.Property(a => a.Severidade)
            .HasColumnName("severidade")
            .HasColumnType("smallint")
            .HasConversion<byte>()
            .IsRequired();

        builder.Property(a => a.Titulo)
            .HasColumnName("titulo")
            .HasColumnType("varchar(200)")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(a => a.Descricao)
            .HasColumnName("descricao")
            .HasColumnType("varchar(1000)")
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(a => a.OrigemTipo)
            .HasColumnName("origem_tipo")
            .HasColumnType("varchar(100)")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(a => a.OrigemId)
            .HasColumnName("origem_id")
            .HasColumnType("uuid")
            .IsRequired(false);

        builder.Property(a => a.AcaoRotulo)
            .HasColumnName("acao_rotulo")
            .HasColumnType("varchar(200)")
            .HasMaxLength(200)
            .IsRequired(false);

        builder.Property(a => a.AcaoRota)
            .HasColumnName("acao_rota")
            .HasColumnType("varchar(500)")
            .HasMaxLength(500)
            .IsRequired(false);

        builder.Property(a => a.Status)
            .HasColumnName("status")
            .HasColumnType("smallint")
            .HasConversion<byte>()
            .IsRequired();

        builder.Property(a => a.CriadoEm)
            .HasColumnName("criado_em")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(a => a.ExpiraEm)
            .HasColumnName("expira_em")
            .HasColumnType("timestamptz")
            .IsRequired(false);

        builder.Property(a => a.ChaveIdempotencia)
            .HasColumnName("chave_idempotencia")
            .HasColumnType("varchar(200)")
            .HasMaxLength(200)
            .IsRequired();

        // Garante idempotência: uma chave por tenant é única no banco.
        builder.HasIndex(a => a.ChaveIdempotencia)
            .IsUnique()
            .HasDatabaseName("ux_alertas_chave_idempotencia");

        // Índice de consulta principal do cockpit: filtra por tenant + status + severidade.
        // Cobrirá as queries de badge e listagem com P95 < 200ms em volumes esperados.
        builder.HasIndex(a => new { a.TenantId, a.Status, a.Severidade })
            .HasDatabaseName("ix_alertas_tenant_status_severidade");

        // Tabela de join para os perfis visíveis.
        // Usa o backing field "_perfisVisiveis" declarado no agregado;
        // a propriedade pública PerfisVisiveis é IReadOnlyList (read-only).
        builder.HasMany(a => a.PerfisVisiveis)
            .WithOne()
            .HasForeignKey(p => p.AlertaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(a => a.PerfisVisiveis).HasField("_perfisVisiveis");
    }
}

internal sealed class AlertaPerfilVisivelConfiguration : IEntityTypeConfiguration<AlertaPerfilVisivel>
{
    public void Configure(EntityTypeBuilder<AlertaPerfilVisivel> builder)
    {
        builder.ToTable("alerta_perfil_visivel");

        // Chave composta: um alerta não pode ter o mesmo perfil duplicado.
        builder.HasKey(p => new { p.AlertaId, p.Perfil });

        builder.Property(p => p.AlertaId)
            .HasColumnName("alerta_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(p => p.Perfil)
            .HasColumnName("perfil")
            .HasColumnType("smallint")
            .HasConversion<byte>()
            .IsRequired();
    }
}
