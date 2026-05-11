using Sgcf.Domain.Contratos;

namespace Sgcf.Domain.Cotacoes;

public static class ResolveTipoCotacaoService
{
    public static TipoCotacao Resolve(
        IReadOnlyList<ParametroCotacao> parametros,
        Guid bancoId,
        ModalidadeContrato modalidade)
    {
        // 1. Exact: bank + modality
        ParametroCotacao? match = parametros.FirstOrDefault(
            p => p.Ativo && p.BancoId == bancoId && p.Modalidade == modalidade);
        if (match is not null)
        {
            return match.TipoCotacao;
        }

        // 2. Bank default
        match = parametros.FirstOrDefault(
            p => p.Ativo && p.BancoId == bancoId && p.Modalidade == null);
        if (match is not null)
        {
            return match.TipoCotacao;
        }

        // 3. Modality default
        match = parametros.FirstOrDefault(
            p => p.Ativo && p.BancoId == null && p.Modalidade == modalidade);
        if (match is not null)
        {
            return match.TipoCotacao;
        }

        // 4. Global default
        match = parametros.FirstOrDefault(
            p => p.Ativo && p.BancoId == null && p.Modalidade == null);
        if (match is not null)
        {
            return match.TipoCotacao;
        }

        return TipoCotacao.PtaxD1;
    }
}
