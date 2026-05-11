using NodaTime;
using Sgcf.Domain.Common;

namespace Sgcf.Domain.Contratos;

/// <summary>
/// Detalhe de contrato Balcão Caixa — crédito direto no balcão da Caixa Econômica Federal.
/// Tabela de extensão 1:1 com <see cref="Contrato"/> — mesma convenção de FinimpDetail e Lei4131Detail.
/// <para>
/// O cronograma é importado manualmente via <c>ImportarCronogramaCommand</c>; não há geração automática.
/// </para>
/// </summary>
public sealed class BalcaoCaixaDetail : Entity
{
    public Guid ContratoId { get; private set; }

    /// <summary>Número da operação no sistema Caixa (ex: identificador interno do produto).</summary>
    public string? NumeroOperacao { get; private set; }

    /// <summary>Tipo do produto Caixa (ex: "PROGER", "FCO", "BNDES Automático").</summary>
    public string? TipoProduto { get; private set; }

    /// <summary>Indica se o contrato possui garantia do FGI (Fundo Garantidor de Investimentos).</summary>
    public bool TemFgi { get; private set; }

    public Instant CreatedAt { get; private set; }
    public Instant UpdatedAt { get; private set; }

    private BalcaoCaixaDetail() { }

    /// <summary>
    /// Cria um novo <see cref="BalcaoCaixaDetail"/> com os campos opcionais fornecidos.
    /// </summary>
    public static BalcaoCaixaDetail Criar(
        Guid contratoId,
        string? numeroOperacao,
        string? tipoProduto,
        bool temFgi,
        IClock clock)
    {
        Instant now = clock.GetCurrentInstant();
        return new BalcaoCaixaDetail
        {
            ContratoId = contratoId,
            NumeroOperacao = numeroOperacao,
            TipoProduto = tipoProduto,
            TemFgi = temFgi,
            CreatedAt = now,
            UpdatedAt = now
        };
    }
}
