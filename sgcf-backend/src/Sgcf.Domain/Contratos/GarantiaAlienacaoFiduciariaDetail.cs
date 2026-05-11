using Sgcf.Domain.Common;

namespace Sgcf.Domain.Contratos;

/// <summary>
/// Detalhe de garantia do tipo Alienação Fiduciária — extensão 1:1 com <see cref="Garantia"/>.
/// </summary>
public sealed class GarantiaAlienacaoFiduciariaDetail : Entity
{
    public Guid GarantiaId { get; private set; }

    /// <summary>Tipo do bem alienado: IMOVEL, EQUIPAMENTO, VEICULO ou OUTRO.</summary>
    public string TipoBem { get; private set; } = default!;

    public string DescricaoBem { get; private set; } = default!;

    internal decimal ValorAvaliadoDecimal { get; private set; }

    /// <summary>Valor avaliado do bem em BRL.</summary>
    public Money ValorAvaliado => new(ValorAvaliadoDecimal, Moeda.Brl);

    /// <summary>Matrícula do imóvel ou chassi do veículo/equipamento. Null quando não aplicável.</summary>
    public string? MatriculaOuChassi { get; private set; }

    /// <summary>Cartório de registro da alienação fiduciária. Null quando não informado.</summary>
    public string? CartorioRegistro { get; private set; }

    private GarantiaAlienacaoFiduciariaDetail() { }

    /// <summary>Cria um novo detalhe de Alienação Fiduciária.</summary>
    public static GarantiaAlienacaoFiduciariaDetail Criar(
        Guid garantiaId,
        string tipoBem,
        string descricaoBem,
        Money valorAvaliado,
        string? matriculaOuChassi,
        string? cartorioRegistro)
    {
        if (string.IsNullOrWhiteSpace(tipoBem))
        {
            throw new ArgumentException("TipoBem não pode ser vazio.", nameof(tipoBem));
        }

        if (string.IsNullOrWhiteSpace(descricaoBem))
        {
            throw new ArgumentException("DescricaoBem não pode ser vazio.", nameof(descricaoBem));
        }

        if (valorAvaliado.Moeda != Moeda.Brl)
        {
            throw new ArgumentException("ValorAvaliado deve ser informado em BRL.", nameof(valorAvaliado));
        }

        return new GarantiaAlienacaoFiduciariaDetail
        {
            GarantiaId = garantiaId,
            TipoBem = tipoBem,
            DescricaoBem = descricaoBem,
            ValorAvaliadoDecimal = valorAvaliado.Valor,
            MatriculaOuChassi = matriculaOuChassi,
            CartorioRegistro = cartorioRegistro
        };
    }
}
