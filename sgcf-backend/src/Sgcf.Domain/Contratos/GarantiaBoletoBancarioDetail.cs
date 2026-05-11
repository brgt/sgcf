using NodaTime;
using Sgcf.Domain.Common;

namespace Sgcf.Domain.Contratos;

/// <summary>
/// Detalhe de garantia do tipo Boleto Bancário — extensão 1:1 com <see cref="Garantia"/>.
/// </summary>
public sealed class GarantiaBoletoBancarioDetail : Entity
{
    public Guid GarantiaId { get; private set; }
    public string BancoEmissor { get; private set; } = default!;
    public int QuantidadeBoletos { get; private set; }

    internal decimal ValorUnitarioDecimal { get; private set; }

    /// <summary>Valor unitário de cada boleto em BRL.</summary>
    public Money ValorUnitario => new(ValorUnitarioDecimal, Moeda.Brl);

    public LocalDate DataEmissaoInicial { get; private set; }
    public LocalDate DataVencimentoInicial { get; private set; }
    public LocalDate DataVencimentoFinal { get; private set; }

    /// <summary>Periodicidade dos boletos (ex: MENSAL, TRIMESTRAL).</summary>
    public string Periodicidade { get; private set; } = default!;

    private GarantiaBoletoBancarioDetail() { }

    /// <summary>Cria um novo detalhe de Boleto Bancário.</summary>
    public static GarantiaBoletoBancarioDetail Criar(
        Guid garantiaId,
        string bancoEmissor,
        int quantidadeBoletos,
        Money valorUnitario,
        LocalDate dataEmissaoInicial,
        LocalDate dataVencimentoInicial,
        LocalDate dataVencimentoFinal,
        string periodicidade)
    {
        if (string.IsNullOrWhiteSpace(bancoEmissor))
        {
            throw new ArgumentException("BancoEmissor não pode ser vazio.", nameof(bancoEmissor));
        }

        if (quantidadeBoletos <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantidadeBoletos), "QuantidadeBoletos deve ser maior que zero.");
        }

        if (valorUnitario.Moeda != Moeda.Brl)
        {
            throw new ArgumentException("ValorUnitario deve ser informado em BRL.", nameof(valorUnitario));
        }

        if (dataVencimentoFinal < dataVencimentoInicial)
        {
            throw new ArgumentException(
                "DataVencimentoFinal não pode ser anterior a DataVencimentoInicial.", nameof(dataVencimentoFinal));
        }

        if (string.IsNullOrWhiteSpace(periodicidade))
        {
            throw new ArgumentException("Periodicidade não pode ser vazio.", nameof(periodicidade));
        }

        return new GarantiaBoletoBancarioDetail
        {
            GarantiaId = garantiaId,
            BancoEmissor = bancoEmissor,
            QuantidadeBoletos = quantidadeBoletos,
            ValorUnitarioDecimal = valorUnitario.Valor,
            DataEmissaoInicial = dataEmissaoInicial,
            DataVencimentoInicial = dataVencimentoInicial,
            DataVencimentoFinal = dataVencimentoFinal,
            Periodicidade = periodicidade
        };
    }
}
