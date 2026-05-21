using NodaTime;
using Sgcf.Domain.Common;
using Sgcf.Domain.Tenancy;

namespace Sgcf.Domain.Benchmarks;

/// <summary>
/// Armazena a taxa diária de um benchmark de mercado (Selic ou SOFR) para uma data-referência.
/// Usada para calcular economia comparativa em relação a taxas de mercado — GAP-CKP-14.
/// </summary>
public sealed class TaxaBenchmark : Entity, ITenantScoped
{
    public Guid TenantId { get; private set; }

    /// <summary>Identificador do benchmark. Valores aceitos: "Selic" ou "Sofr".</summary>
    public string TipoBenchmark { get; private set; } = default!;

    /// <summary>Data de vigência desta taxa.</summary>
    public LocalDate DataReferencia { get; private set; }

    internal decimal TaxaAaDecimal { get; private set; }

    /// <summary>Taxa anualizada como fração (0.1075 = 10,75% a.a.).</summary>
    public decimal TaxaAa => TaxaAaDecimal;

    /// <summary>Origem do dado. Valores esperados: "BCB", "FED", "Manual".</summary>
    public string Fonte { get; private set; } = default!;

    /// <summary>Instante em que o registro foi criado ou atualizado pela última vez.</summary>
    public Instant RegistradoEm { get; private set; }

    private TaxaBenchmark() { }

    public static TaxaBenchmark Criar(
        string tipo,
        LocalDate data,
        decimal taxaAa,
        string fonte,
        Instant agora)
    {
        if (string.IsNullOrWhiteSpace(tipo))
        {
            throw new ArgumentException("TipoBenchmark não pode ser vazio.", nameof(tipo));
        }

        if (taxaAa < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(taxaAa), "TaxaAa não pode ser negativa.");
        }

        if (string.IsNullOrWhiteSpace(fonte))
        {
            throw new ArgumentException("Fonte não pode ser vazia.", nameof(fonte));
        }

        return new TaxaBenchmark
        {
            TipoBenchmark = tipo,
            DataReferencia = data,
            TaxaAaDecimal = taxaAa,
            Fonte = fonte,
            RegistradoEm = agora,
        };
    }

    public void Atualizar(decimal novaTaxa, string novaFonte, Instant agora)
    {
        if (novaTaxa < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(novaTaxa), "TaxaAa não pode ser negativa.");
        }

        if (string.IsNullOrWhiteSpace(novaFonte))
        {
            throw new ArgumentException("Fonte não pode ser vazia.", nameof(novaFonte));
        }

        TaxaAaDecimal = novaTaxa;
        Fonte = novaFonte;
        RegistradoEm = agora;
    }
}
