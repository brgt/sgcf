using Sgcf.Domain.Bancos;

namespace Sgcf.Application.Bancos;

public sealed record BancoDto(
    Guid Id,
    string CodigoCompe,
    string RazaoSocial,
    string Apelido,
    bool AceitaLiquidacaoTotal,
    bool AceitaLiquidacaoParcial,
    bool ExigeAnuenciaExpressa,
    bool ExigeParcelaInteira,
    int AvisoPrevioMinDiasUteis,
    string RegimeLimite,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static BancoDto From(Banco banco) =>
        new(
            banco.Id,
            banco.CodigoCompe,
            banco.RazaoSocial,
            banco.Apelido,
            banco.AceitaLiquidacaoTotal,
            banco.AceitaLiquidacaoParcial,
            banco.ExigeAnuenciaExpressa,
            banco.ExigeParcelaInteira,
            banco.AvisoPrevioMinDiasUteis,
            banco.RegimeLimite.ToString(),
            banco.CreatedAt.ToDateTimeOffset(),
            banco.UpdatedAt.ToDateTimeOffset());
}
