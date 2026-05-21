using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgcf.Domain.Exportacao;

namespace Sgcf.Infrastructure.Persistence.Configurations;

internal sealed class ExportacaoJobConfiguration : IEntityTypeConfiguration<ExportacaoJob>
{
    public void Configure(EntityTypeBuilder<ExportacaoJob> builder)
    {
        builder.ToTable("exportacao_job", "sgcf");

        builder.HasKey(j => j.Id);

        builder.Property(j => j.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(j => j.TenantId)
            .HasColumnName("tenant_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(j => j.Tipo)
            .HasColumnName("tipo")
            .HasColumnType("integer")
            .IsRequired();

        builder.Property(j => j.Status)
            .HasColumnName("status")
            .HasColumnType("integer")
            .IsRequired();

        // JSON de parâmetros pode ser arbitrariamente grande — sem limite de tamanho.
        builder.Property(j => j.ParametrosJson)
            .HasColumnName("parametros_json")
            .HasColumnType("text")
            .IsRequired(false);

        // Payload de resultado pode ser grande (lista de contratos, fluxo de caixa, etc.).
        builder.Property(j => j.ResultadoJson)
            .HasColumnName("resultado_json")
            .HasColumnType("text")
            .IsRequired(false);

        builder.Property(j => j.MensagemErro)
            .HasColumnName("mensagem_erro")
            .HasColumnType("text")
            .HasMaxLength(2000)
            .IsRequired(false);

        builder.Property(j => j.SolicitadoPor)
            .HasColumnName("solicitado_por")
            .HasColumnType("text")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(j => j.CriadoEm)
            .HasColumnName("criado_em")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(j => j.IniciadoEm)
            .HasColumnName("iniciado_em")
            .HasColumnType("timestamptz")
            .IsRequired(false);

        builder.Property(j => j.ConcluidoEm)
            .HasColumnName("concluido_em")
            .HasColumnType("timestamptz")
            .IsRequired(false);

        // Suporta polling eficiente de jobs pendentes por tenant.
        builder.HasIndex(j => new { j.TenantId, j.Status })
            .HasDatabaseName("ix_exportacao_job_tenant_status");

        // Suporta listagem de jobs por usuário solicitante dentro de um tenant.
        builder.HasIndex(j => new { j.TenantId, j.SolicitadoPor })
            .HasDatabaseName("ix_exportacao_job_solicitado_por");
    }
}
