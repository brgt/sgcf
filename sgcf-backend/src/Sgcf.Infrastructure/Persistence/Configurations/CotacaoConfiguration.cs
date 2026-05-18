using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeamento EF Core da entidade <see cref="Cotacao"/> para a tabela <c>sgcf.cotacao</c>.
/// SPEC §8.1.
///
/// Decisões de mapeamento:
/// - <c>BancosAlvo</c>: List&lt;Guid&gt; mapeada como coluna <c>uuid[]</c> (array nativo do PostgreSQL)
///   via <c>PrimitiveCollection</c> do EF Core 8+. Evita tabela auxiliar desnecessária no MVP.
/// - <c>PropostaAceitaId</c>: sem FK física para evitar ciclo cotacao→proposta→cotacao.
///   A integridade é garantida pela Application layer (SPEC §13).
/// - <c>ContratoGeradoId</c>: FK para contrato com comportamento SetNull.
/// </summary>
internal sealed class CotacaoConfiguration : IEntityTypeConfiguration<Cotacao>
{
    public void Configure(EntityTypeBuilder<Cotacao> builder)
    {
        builder.ToTable("cotacao");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(c => c.CodigoInterno)
            .HasColumnName("codigo_interno")
            .HasColumnType("text")
            .IsRequired();
        builder.HasIndex(c => c.CodigoInterno)
            .IsUnique()
            .HasFilter("deleted_at IS NULL");

        builder.Property(c => c.Modalidade)
            .HasColumnName("modalidade")
            .HasConversion(SgcfConverters.Modalidade)
            .HasColumnType("text")
            .IsRequired();
        builder.HasIndex(c => c.Modalidade).HasFilter("deleted_at IS NULL");

        builder.Property(c => c.ValorAlvoBrlDecimal)
            .HasColumnName("valor_alvo_brl")
            .HasColumnType("numeric(20,6)")
            .IsRequired();

        builder.Property(c => c.PrazoMaximoDias)
            .HasColumnName("prazo_maximo_dias")
            .HasColumnType("integer")
            .IsRequired();

        builder.Property(c => c.DataAbertura)
            .HasColumnName("data_abertura")
            .HasColumnType("date")
            .IsRequired();
        builder.HasIndex(c => c.DataAbertura).HasFilter("deleted_at IS NULL");

        // Onda 0 F0.1: nullable — modalidades BRL puras (NCE, CapitalDeGiro, FGI) não têm PTAX.
        // Migration S6_PtaxNullable altera as colunas de NOT NULL para nullable.
        builder.Property(c => c.DataPtaxReferencia)
            .HasColumnName("data_ptax_referencia")
            .HasColumnType("date")
            .IsRequired(false);

        builder.Property(c => c.PtaxUsadaUsdBrl)
            .HasColumnName("ptax_usada_usd_brl")
            .HasColumnType("numeric(12,6)")
            .IsRequired(false);

        builder.Property(c => c.Status)
            .HasColumnName("status")
            .HasConversion(new ValueConverter<StatusCotacao, short>(
                v => (short)v,
                v => (StatusCotacao)v))
            .HasColumnType("smallint")
            .IsRequired();
        builder.HasIndex(c => c.Status).HasFilter("deleted_at IS NULL");

        // PropostaAceitaId: sem FK física (ciclo cotacao→proposta→cotacao — SPEC §13).
        builder.Property(c => c.PropostaAceitaId)
            .HasColumnName("proposta_aceita_id")
            .HasColumnType("uuid")
            .IsRequired(false);

        builder.Property(c => c.ContratoGeradoId)
            .HasColumnName("contrato_gerado_id")
            .HasColumnType("uuid")
            .IsRequired(false);

        builder.Property(c => c.AceitaPor)
            .HasColumnName("aceita_por")
            .HasColumnType("text")
            .IsRequired(false);

        builder.Property(c => c.DataAceitacao)
            .HasColumnName("data_aceitacao")
            .HasColumnType("timestamptz")
            .IsRequired(false);

        builder.Property(c => c.Observacoes)
            .HasColumnName("observacoes")
            .HasColumnType("text")
            .IsRequired(false);

        builder.Property(c => c.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(c => c.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(c => c.DeletedAt)
            .HasColumnName("deleted_at")
            .HasColumnType("timestamptz")
            .IsRequired(false);

        builder.HasQueryFilter(c => c.DeletedAt == null);

        // BancosAlvo: List<Guid> persistida como array uuid[] via PrimitiveCollection (EF8+).
        builder.PrimitiveCollection<List<Guid>>("_bancosAlvo")
            .HasField("_bancosAlvo")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasColumnName("bancos_alvo_ids")
            .HasColumnType("uuid[]")
            .IsRequired();

        // FK para Contrato (SetNull — contrato pode ser deletado independentemente).
        builder.HasOne<Domain.Contratos.Contrato>()
            .WithMany()
            .HasForeignKey(c => c.ContratoGeradoId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        // Onda 1 REFINIMP — SPEC §3.5: ContratoMaeId é FK nullable para contrato(id).
        // ON DELETE RESTRICT: não permite apagar contrato que tenha cotação REFINIMP filha.
        // Sem navegação inversa — id puro (AD-10 plano refinimp/).
        builder.Property(c => c.ContratoMaeId)
            .HasColumnName("contrato_mae_id")
            .HasColumnType("uuid")
            .IsRequired(false);

        builder.HasOne<Domain.Contratos.Contrato>()
            .WithMany()
            .HasForeignKey(c => c.ContratoMaeId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasIndex(c => c.ContratoMaeId)
            .HasDatabaseName("ix_cotacao_contrato_mae_id")
            .HasFilter("contrato_mae_id IS NOT NULL");

        // Propostas: 1:N com Proposta; cascade Restrict para não apagar propostas ao cancelar cotação.
        builder.HasMany(c => c.Propostas)
            .WithOne()
            .HasForeignKey(p => p.CotacaoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(c => c.Propostas)
            .HasField("_propostas")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // Propriedades computadas (Money wrappers) não são persistidas.
        builder.Ignore(c => c.ValorAlvoBrl);
        builder.Ignore(c => c.BancosAlvo);
    }
}
