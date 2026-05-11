using NodaTime;
using Sgcf.Domain.Common;

namespace Sgcf.Domain.Contratos;

/// <summary>
/// Detalhe de garantia do tipo Duplicatas — extensão 1:1 com <see cref="Garantia"/>.
/// </summary>
public sealed class GarantiaDuplicatasDetail : Entity
{
    public Guid GarantiaId { get; private set; }

    /// <summary>Percentual de desconto aplicado ao valor de face, armazenado como fração (0..1).</summary>
    internal decimal PercentualDescontoDecimal { get; private set; }

    /// <summary>Percentual de desconto aplicado ao valor de face das duplicatas.</summary>
    public Percentual PercentualDesconto => Percentual.DeFracao(PercentualDescontoDecimal);

    public LocalDate VencimentoEscalonadoInicio { get; private set; }
    public LocalDate VencimentoEscalonadoFim { get; private set; }
    public int QtdDuplicatasCedidas { get; private set; }

    internal decimal ValorTotalDuplicatasDecimal { get; private set; }

    /// <summary>Valor total de face das duplicatas cedidas, em BRL.</summary>
    public Money ValorTotalDuplicatas => new(ValorTotalDuplicatasDecimal, Moeda.Brl);

    /// <summary>Data do instrumento de cessão. Null quando ainda não lavrado.</summary>
    public LocalDate? InstrumentoCessaoData { get; private set; }

    private GarantiaDuplicatasDetail() { }

    /// <summary>
    /// Cria um novo detalhe de Duplicatas.
    /// <paramref name="percentualDescontoPct"/> é fornecido como valor humano (ex: 20.0 para 20%).
    /// </summary>
    public static GarantiaDuplicatasDetail Criar(
        Guid garantiaId,
        decimal percentualDescontoPct,
        LocalDate vencimentoEscalonadoInicio,
        LocalDate vencimentoEscalonadoFim,
        int qtdDuplicatasCedidas,
        Money valorTotalDuplicatas,
        LocalDate? instrumentoCessaoData)
    {
        if (percentualDescontoPct < 0m || percentualDescontoPct > 100m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(percentualDescontoPct), "PercentualDesconto deve estar entre 0 e 100.");
        }

        if (vencimentoEscalonadoFim < vencimentoEscalonadoInicio)
        {
            throw new ArgumentException(
                "VencimentoEscalonadoFim não pode ser anterior ao início.", nameof(vencimentoEscalonadoFim));
        }

        if (qtdDuplicatasCedidas <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(qtdDuplicatasCedidas), "QtdDuplicatasCedidas deve ser maior que zero.");
        }

        if (valorTotalDuplicatas.Moeda != Moeda.Brl)
        {
            throw new ArgumentException("ValorTotalDuplicatas deve ser informado em BRL.", nameof(valorTotalDuplicatas));
        }

        return new GarantiaDuplicatasDetail
        {
            GarantiaId = garantiaId,
            PercentualDescontoDecimal = percentualDescontoPct / 100m,
            VencimentoEscalonadoInicio = vencimentoEscalonadoInicio,
            VencimentoEscalonadoFim = vencimentoEscalonadoFim,
            QtdDuplicatasCedidas = qtdDuplicatasCedidas,
            ValorTotalDuplicatasDecimal = valorTotalDuplicatas.Valor,
            InstrumentoCessaoData = instrumentoCessaoData
        };
    }
}
