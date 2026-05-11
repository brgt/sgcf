using NodaTime;
using Sgcf.Domain.Contratos;

namespace Sgcf.Application.Contratos;

/// <summary>
/// Repositório para operações de leitura e gravação de <see cref="Garantia"/> e seus detalhes polimórficos.
/// A persistência efetiva ocorre via <c>SaveChangesAsync</c> do DbContext compartilhado.
/// </summary>
public interface IGarantiaRepository
{
    public Task<IReadOnlyList<Garantia>> ListByContratoAsync(Guid contratoId, CancellationToken cancellationToken = default);
    public Task<Garantia?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    public void Add(Garantia garantia);
    public void AddCdbCativoDetail(GarantiaCdbCativoDetail detail);
    public void AddSblcDetail(GarantiaSblcDetail detail);
    public void AddAvalDetail(GarantiaAvalDetail detail);
    public void AddAlienacaoFiduciariaDetail(GarantiaAlienacaoFiduciariaDetail detail);
    public void AddDuplicatasDetail(GarantiaDuplicatasDetail detail);
    public void AddRecebiveisCartaoDetail(GarantiaRecebiveisCartaoDetail detail);
    public void AddBoletoBancarioDetail(GarantiaBoletoBancarioDetail detail);
    public void AddFgiDetail(GarantiaFgiDetail detail);

    /// <summary>
    /// Retorna a soma dos percentuais de faturamento de cartão já comprometidos
    /// em garantias ativas do tipo RecebiveisCartao para o contrato.
    /// O valor retornado é uma fração (0..1). Retorna zero quando não há garantias desse tipo.
    /// </summary>
    public Task<decimal> GetTotalPercentualFaturamentoCartaoAsync(Guid contratoId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retorna detalhes de CDB cativo ativo para a garantia especificada, cujo
    /// <see cref="GarantiaCdbCativoDetail.DataVencimentoCdb"/> é menor ou igual a <paramref name="dataLimite"/>.
    /// O filtro de status Ativa é aplicado via join com a tabela de garantias.
    /// </summary>
    public Task<IReadOnlyList<GarantiaCdbCativoDetail>> ListCdbAtivosComVencimentoAteAsync(
        Guid garantiaId,
        LocalDate dataLimite,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retorna detalhes de boleto bancário ativo para a garantia especificada, cujo
    /// <see cref="GarantiaBoletoBancarioDetail.DataVencimentoFinal"/> é menor ou igual a <paramref name="dataLimite"/>.
    /// </summary>
    public Task<IReadOnlyList<GarantiaBoletoBancarioDetail>> ListBoletosAtivosComVencimentoAteAsync(
        Guid garantiaId,
        LocalDate dataLimite,
        CancellationToken cancellationToken = default);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
