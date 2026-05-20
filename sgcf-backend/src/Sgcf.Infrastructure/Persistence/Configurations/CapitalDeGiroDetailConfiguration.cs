using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgcf.Domain.Contratos;

namespace Sgcf.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuração EF Core de <see cref="CapitalDeGiroDetail"/>.
/// Tabela física mantida como <c>balcao_caixa_detail</c> para compatibilidade de dados.
/// Renomear para <c>capital_de_giro_detail</c> requer migration dedicada em momento operacional propício.
/// <para>
/// Onda 3b: colunas <c>tipo_produto</c> e <c>tem_fgi</c> foram dropadas da entidade (SPEC §3.3 e §3.4).
/// Migration <c>S8_DropTipoProdutoTemFgi</c> aplica o DROP COLUMN físico.
/// </para>
/// </summary>
internal sealed class CapitalDeGiroDetailConfiguration : IEntityTypeConfiguration<CapitalDeGiroDetail>
{
    public void Configure(EntityTypeBuilder<CapitalDeGiroDetail> builder)
    {
        // Mantém o nome físico da tabela — renomear via migration dedicada no futuro.
        builder.ToTable("balcao_caixa_detail");

        builder.HasKey(b => b.Id);
        builder.Property(e => e.TenantId).HasColumnName("tenant_id").HasColumnType("uuid").IsRequired();
        builder.Property(b => b.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();

        builder.Property(b => b.ContratoId).HasColumnName("contrato_id").HasColumnType("uuid").IsRequired();
        builder.HasIndex(b => b.ContratoId).IsUnique();

        builder.Property(b => b.NumeroOperacao).HasColumnName("numero_operacao").HasColumnType("text").IsRequired(false);

        // tipo_produto e tem_fgi foram removidos da entidade (Onda 3b — SPEC §3.3, §3.4).
        // As colunas físicas são dropadas pela migration S8_DropTipoProdutoTemFgi.

        builder.Property(b => b.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();
        builder.Property(b => b.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").IsRequired();
    }
}
