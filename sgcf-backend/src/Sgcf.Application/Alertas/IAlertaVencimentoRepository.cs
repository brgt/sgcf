using NodaTime;
using Sgcf.Domain.Alertas;

namespace Sgcf.Application.Alertas;

public interface IAlertaVencimentoRepository
{
    /// <summary>
    /// Verifica se já existe um alerta com a mesma combinação (contrato_id, tipo_alerta, data_vencimento).
    /// Garante idempotência do job diário.
    /// </summary>
    public Task<bool> ExisteAsync(Guid contratoId, string tipoAlerta, LocalDate dataVencimento, CancellationToken ct = default);

    public void Add(AlertaVencimento alerta);

    public Task<int> SaveChangesAsync(CancellationToken ct = default);
}
