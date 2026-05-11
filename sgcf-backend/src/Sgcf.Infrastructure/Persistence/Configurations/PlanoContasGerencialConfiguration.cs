using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NodaTime;
using Sgcf.Domain.Contabilidade;

namespace Sgcf.Infrastructure.Persistence.Configurations;

internal sealed class PlanoContasGerencialConfiguration : IEntityTypeConfiguration<PlanoContasGerencial>
{
    public void Configure(EntityTypeBuilder<PlanoContasGerencial> builder)
    {
        builder.ToTable("plano_contas_gerencial", "sgcf");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();

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

        builder.Property(p => p.Ativo)
            .HasColumnName("ativo")
            .HasDefaultValue(true);

        builder.Property(p => p.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").IsRequired();

        Instant seedInstant = Instant.FromUtc(2026, 5, 11, 0, 0);

        builder.HasData(
            // Ativo
            new { Id = Guid.Parse("00000000-0000-0000-0001-000000000001"), CodigoGerencial = "1.1.1", Nome = "Conta Corrente em BRL", Natureza = NaturezaConta.Ativo, CodigoSapB1 = (string?)null, Ativo = true, CreatedAt = seedInstant, UpdatedAt = seedInstant },
            new { Id = Guid.Parse("00000000-0000-0000-0001-000000000002"), CodigoGerencial = "1.1.2", Nome = "CDBs e Aplicações Livres", Natureza = NaturezaConta.Ativo, CodigoSapB1 = (string?)null, Ativo = true, CreatedAt = seedInstant, UpdatedAt = seedInstant },
            new { Id = Guid.Parse("00000000-0000-0000-0001-000000000003"), CodigoGerencial = "1.2.1", Nome = "CDB Cativo (Cash Collateral)", Natureza = NaturezaConta.Ativo, CodigoSapB1 = (string?)null, Ativo = true, CreatedAt = seedInstant, UpdatedAt = seedInstant },
            new { Id = Guid.Parse("00000000-0000-0000-0001-000000000004"), CodigoGerencial = "1.2.2", Nome = "Outras Garantias Bloqueadas", Natureza = NaturezaConta.Ativo, CodigoSapB1 = (string?)null, Ativo = true, CreatedAt = seedInstant, UpdatedAt = seedInstant },
            new { Id = Guid.Parse("00000000-0000-0000-0001-000000000005"), CodigoGerencial = "1.3.1", Nome = "NDFs a Receber", Natureza = NaturezaConta.Ativo, CodigoSapB1 = (string?)null, Ativo = true, CreatedAt = seedInstant, UpdatedAt = seedInstant },
            // Passivo
            new { Id = Guid.Parse("00000000-0000-0000-0002-000000000001"), CodigoGerencial = "2.1.1", Nome = "FINIMP em Moeda Estrangeira", Natureza = NaturezaConta.Passivo, CodigoSapB1 = (string?)null, Ativo = true, CreatedAt = seedInstant, UpdatedAt = seedInstant },
            new { Id = Guid.Parse("00000000-0000-0000-0002-000000000002"), CodigoGerencial = "2.1.2", Nome = "4131 em Moeda Estrangeira", Natureza = NaturezaConta.Passivo, CodigoSapB1 = (string?)null, Ativo = true, CreatedAt = seedInstant, UpdatedAt = seedInstant },
            new { Id = Guid.Parse("00000000-0000-0000-0002-000000000003"), CodigoGerencial = "2.1.3", Nome = "NCE/CCE em BRL", Natureza = NaturezaConta.Passivo, CodigoSapB1 = (string?)null, Ativo = true, CreatedAt = seedInstant, UpdatedAt = seedInstant },
            new { Id = Guid.Parse("00000000-0000-0000-0002-000000000004"), CodigoGerencial = "2.1.4", Nome = "Balcão Caixa", Natureza = NaturezaConta.Passivo, CodigoSapB1 = (string?)null, Ativo = true, CreatedAt = seedInstant, UpdatedAt = seedInstant },
            new { Id = Guid.Parse("00000000-0000-0000-0002-000000000005"), CodigoGerencial = "2.1.5", Nome = "FGI (BNDES via Banco Intermediário)", Natureza = NaturezaConta.Passivo, CodigoSapB1 = (string?)null, Ativo = true, CreatedAt = seedInstant, UpdatedAt = seedInstant },
            new { Id = Guid.Parse("00000000-0000-0000-0002-000000000006"), CodigoGerencial = "2.1.6", Nome = "REFINIMPs Ativos", Natureza = NaturezaConta.Passivo, CodigoSapB1 = (string?)null, Ativo = true, CreatedAt = seedInstant, UpdatedAt = seedInstant },
            new { Id = Guid.Parse("00000000-0000-0000-0002-000000000007"), CodigoGerencial = "2.2.1", Nome = "NDFs a Pagar", Natureza = NaturezaConta.Passivo, CodigoSapB1 = (string?)null, Ativo = true, CreatedAt = seedInstant, UpdatedAt = seedInstant },
            new { Id = Guid.Parse("00000000-0000-0000-0002-000000000008"), CodigoGerencial = "2.3.1", Nome = "Juros Provisionados FINIMP", Natureza = NaturezaConta.Passivo, CodigoSapB1 = (string?)null, Ativo = true, CreatedAt = seedInstant, UpdatedAt = seedInstant },
            new { Id = Guid.Parse("00000000-0000-0000-0002-000000000009"), CodigoGerencial = "2.3.2", Nome = "Juros Provisionados 4131", Natureza = NaturezaConta.Passivo, CodigoSapB1 = (string?)null, Ativo = true, CreatedAt = seedInstant, UpdatedAt = seedInstant },
            new { Id = Guid.Parse("00000000-0000-0000-0002-000000000010"), CodigoGerencial = "2.3.3", Nome = "Juros Provisionados Outros", Natureza = NaturezaConta.Passivo, CodigoSapB1 = (string?)null, Ativo = true, CreatedAt = seedInstant, UpdatedAt = seedInstant },
            new { Id = Guid.Parse("00000000-0000-0000-0002-000000000011"), CodigoGerencial = "2.4.1", Nome = "IRRF s/ Juros Remetidos ao Exterior", Natureza = NaturezaConta.Passivo, CodigoSapB1 = (string?)null, Ativo = true, CreatedAt = seedInstant, UpdatedAt = seedInstant },
            new { Id = Guid.Parse("00000000-0000-0000-0002-000000000012"), CodigoGerencial = "2.4.2", Nome = "IOF Câmbio a Recolher", Natureza = NaturezaConta.Passivo, CodigoSapB1 = (string?)null, Ativo = true, CreatedAt = seedInstant, UpdatedAt = seedInstant },
            // Resultado
            new { Id = Guid.Parse("00000000-0000-0000-0003-000000000001"), CodigoGerencial = "3.1.1", Nome = "Rendimento de CDB Cativo", Natureza = NaturezaConta.Resultado, CodigoSapB1 = (string?)null, Ativo = true, CreatedAt = seedInstant, UpdatedAt = seedInstant },
            new { Id = Guid.Parse("00000000-0000-0000-0003-000000000002"), CodigoGerencial = "3.1.2", Nome = "Ganho com NDF (MTM e Liquidação)", Natureza = NaturezaConta.Resultado, CodigoSapB1 = (string?)null, Ativo = true, CreatedAt = seedInstant, UpdatedAt = seedInstant },
            new { Id = Guid.Parse("00000000-0000-0000-0003-000000000003"), CodigoGerencial = "3.1.3", Nome = "Variação Cambial Ativa", Natureza = NaturezaConta.Resultado, CodigoSapB1 = (string?)null, Ativo = true, CreatedAt = seedInstant, UpdatedAt = seedInstant },
            new { Id = Guid.Parse("00000000-0000-0000-0003-000000000004"), CodigoGerencial = "3.2.1", Nome = "Juros sobre FINIMP", Natureza = NaturezaConta.Resultado, CodigoSapB1 = (string?)null, Ativo = true, CreatedAt = seedInstant, UpdatedAt = seedInstant },
            new { Id = Guid.Parse("00000000-0000-0000-0003-000000000005"), CodigoGerencial = "3.2.2", Nome = "Juros sobre 4131", Natureza = NaturezaConta.Resultado, CodigoSapB1 = (string?)null, Ativo = true, CreatedAt = seedInstant, UpdatedAt = seedInstant },
            new { Id = Guid.Parse("00000000-0000-0000-0003-000000000006"), CodigoGerencial = "3.2.3", Nome = "Juros sobre Demais Modalidades", Natureza = NaturezaConta.Resultado, CodigoSapB1 = (string?)null, Ativo = true, CreatedAt = seedInstant, UpdatedAt = seedInstant },
            new { Id = Guid.Parse("00000000-0000-0000-0003-000000000007"), CodigoGerencial = "3.2.4", Nome = "IRRF Gross-Up", Natureza = NaturezaConta.Resultado, CodigoSapB1 = (string?)null, Ativo = true, CreatedAt = seedInstant, UpdatedAt = seedInstant },
            new { Id = Guid.Parse("00000000-0000-0000-0003-000000000008"), CodigoGerencial = "3.2.5", Nome = "IOF Câmbio", Natureza = NaturezaConta.Resultado, CodigoSapB1 = (string?)null, Ativo = true, CreatedAt = seedInstant, UpdatedAt = seedInstant },
            new { Id = Guid.Parse("00000000-0000-0000-0003-000000000009"), CodigoGerencial = "3.2.6", Nome = "Comissões SBLC, CPG e Garantia", Natureza = NaturezaConta.Resultado, CodigoSapB1 = (string?)null, Ativo = true, CreatedAt = seedInstant, UpdatedAt = seedInstant },
            new { Id = Guid.Parse("00000000-0000-0000-0003-000000000010"), CodigoGerencial = "3.2.7", Nome = "Tarifas (ROF, CADEMP, Cartório)", Natureza = NaturezaConta.Resultado, CodigoSapB1 = (string?)null, Ativo = true, CreatedAt = seedInstant, UpdatedAt = seedInstant },
            new { Id = Guid.Parse("00000000-0000-0000-0003-000000000011"), CodigoGerencial = "3.2.8", Nome = "Perda com NDF (MTM e Liquidação)", Natureza = NaturezaConta.Resultado, CodigoSapB1 = (string?)null, Ativo = true, CreatedAt = seedInstant, UpdatedAt = seedInstant },
            new { Id = Guid.Parse("00000000-0000-0000-0003-000000000012"), CodigoGerencial = "3.2.9", Nome = "Variação Cambial Passiva", Natureza = NaturezaConta.Resultado, CodigoSapB1 = (string?)null, Ativo = true, CreatedAt = seedInstant, UpdatedAt = seedInstant },
            new { Id = Guid.Parse("00000000-0000-0000-0003-000000000013"), CodigoGerencial = "3.2.10", Nome = "Custo de Oportunidade do CDB Cativo", Natureza = NaturezaConta.Resultado, CodigoSapB1 = (string?)null, Ativo = true, CreatedAt = seedInstant, UpdatedAt = seedInstant }
        );
    }
}
