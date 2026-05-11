using NodaTime;
using Sgcf.Domain.Common;

namespace Sgcf.Domain.Contratos;

/// <summary>
/// Detalhe de garantia do tipo Aval — extensão 1:1 com <see cref="Garantia"/>.
/// </summary>
public sealed class GarantiaAvalDetail : Entity
{
    public Guid GarantiaId { get; private set; }

    /// <summary>"PF" para pessoa física ou "PJ" para pessoa jurídica.</summary>
    public string AvalistaTipo { get; private set; } = default!;

    public string AvalistaNome { get; private set; } = default!;

    /// <summary>CPF (PF) ou CNPJ (PJ) do avalista.</summary>
    public string AvalistaDocumento { get; private set; } = default!;

    internal decimal ValorAvalDecimal { get; private set; }

    /// <summary>Valor do aval em BRL.</summary>
    public Money ValorAval => new(ValorAvalDecimal, Moeda.Brl);

    /// <summary>Data de vigência do aval. Null quando indefinida.</summary>
    public LocalDate? VigenciaAte { get; private set; }

    private GarantiaAvalDetail() { }

    /// <summary>Cria um novo detalhe de Aval.</summary>
    public static GarantiaAvalDetail Criar(
        Guid garantiaId,
        string avalistaTipo,
        string avalistaNome,
        string avalistaDocumento,
        Money valorAval,
        LocalDate? vigenciaAte)
    {
        if (string.IsNullOrWhiteSpace(avalistaTipo))
        {
            throw new ArgumentException("AvalistaTipo não pode ser vazio.", nameof(avalistaTipo));
        }

        if (string.IsNullOrWhiteSpace(avalistaNome))
        {
            throw new ArgumentException("AvalistaNome não pode ser vazio.", nameof(avalistaNome));
        }

        if (string.IsNullOrWhiteSpace(avalistaDocumento))
        {
            throw new ArgumentException("AvalistaDocumento não pode ser vazio.", nameof(avalistaDocumento));
        }

        if (valorAval.Moeda != Moeda.Brl)
        {
            throw new ArgumentException("ValorAval deve ser informado em BRL.", nameof(valorAval));
        }

        return new GarantiaAvalDetail
        {
            GarantiaId = garantiaId,
            AvalistaTipo = avalistaTipo,
            AvalistaNome = avalistaNome,
            AvalistaDocumento = avalistaDocumento,
            ValorAvalDecimal = valorAval.Valor,
            VigenciaAte = vigenciaAte
        };
    }
}
