using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgcf.Domain.Auditoria;

namespace Sgcf.Infrastructure.Persistence.Configurations;

internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_log");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id)
            .HasColumnName("id")
            .HasColumnType("bigint")
            .ValueGeneratedOnAdd();

        builder.Property(a => a.OccurredAt)
            .HasColumnName("occurred_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(a => a.ActorSub)
            .HasColumnName("actor_sub")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(a => a.ActorRole)
            .HasColumnName("actor_role")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(a => a.Source)
            .HasColumnName("source")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(a => a.Entity)
            .HasColumnName("entity")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(a => a.EntityId)
            .HasColumnName("entity_id")
            .HasColumnType("uuid")
            .IsRequired(false);

        builder.Property(a => a.Operation)
            .HasColumnName("operation")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(a => a.DiffJson)
            .HasColumnName("diff_json")
            .HasColumnType("jsonb")
            .IsRequired(false);

        builder.Property(a => a.RequestId)
            .HasColumnName("request_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(a => a.IpHash)
            .HasColumnName("ip_hash")
            .HasColumnType("bytea")
            .IsRequired(false);

        builder.HasIndex(a => new { a.Entity, a.EntityId, a.OccurredAt })
            .HasDatabaseName("ix_audit_entity")
            .IsDescending(false, false, true);

        builder.HasIndex(a => new { a.ActorSub, a.OccurredAt })
            .HasDatabaseName("ix_audit_actor")
            .IsDescending(false, true);
    }
}
