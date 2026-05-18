using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;

namespace Sgcf.Application.Cotacoes.Conversores;

/// <summary>
/// Implementação do conversor para a modalidade FINIMP.
/// Extrai a lógica que estava inline no ConverterEmContratoCommand (linhas ~100-113).
/// O comportamento é bit-a-bit idêntico ao código anterior — nenhuma fórmula foi alterada.
/// </summary>
public sealed class ConversorFinimp : IConversorModalidade
{
    /// <inheritdoc/>
    public ModalidadeContrato Modalidade => ModalidadeContrato.Finimp;

    /// <inheritdoc/>
    public Task<(Entity Principal, Entity? Secundario)> CriarDetailAsync(
        ConverterEmContratoContext ctx,
        CancellationToken cancellationToken)
    {
        FinimpDetail detail = FinimpDetail.Criar(
            contratoId: ctx.ContratoCriado.Id,
            rofNumero: ctx.Command.RofNumero,
            rofDataEmissao: null,
            exportadorNome: ctx.Command.ExportadorNome,
            exportadorPais: ctx.Command.ExportadorPais,
            produtoImportado: ctx.Command.ProdutoImportado,
            faturaReferencia: null,
            incoterm: null,
            breakFundingFeePercentual: null,
            temMarketFlex: false,
            clock: ctx.Clock);

        return Task.FromResult<(Entity, Entity?)>((detail, null));
    }
}
