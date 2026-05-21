using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgcf.Domain.Preferencias;

namespace Sgcf.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuração EF Core para <see cref="PreferenciaUsuario"/>.
///
/// Índice único (tenant_id, user_id, chave) garante um valor por usuário por chave dentro do tenant.
/// TenantId é preenchido pelo TenantSaveInterceptor — não mapeado com valor gerado aqui.
/// </summary>
internal sealed class PreferenciaUsuarioConfiguration : IEntityTypeConfiguration<PreferenciaUsuario>
{
    public void Configure(EntityTypeBuilder<PreferenciaUsuario> builder)
    {
        builder.ToTable("preferencia_usuario");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(p => p.TenantId)
            .HasColumnName("tenant_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(p => p.UserId)
            .HasColumnName("user_id")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(p => p.Chave)
            .HasColumnName("chave")
            .HasColumnType("text")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(p => p.Valor)
            .HasColumnName("valor")
            .HasColumnType("text")
            .HasMaxLength(4000)
            .IsRequired();

        builder.Property(p => p.AtualizadoEm)
            .HasColumnName("atualizado_em")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.HasIndex(p => new { p.TenantId, p.UserId, p.Chave })
            .IsUnique()
            .HasDatabaseName("ux_preferencia_usuario_tenant_user_chave");
    }
}
