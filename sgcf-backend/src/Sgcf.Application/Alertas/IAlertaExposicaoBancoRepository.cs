using NodaTime;
using Sgcf.Domain.Alertas;

namespace Sgcf.Application.Alertas;

public interface IAlertaExposicaoBancoRepository
{
    /// <summary>
    /// Verifica se já existe um alerta de exposição para o mesmo banco na mesma data.
    /// Garante idempotência do job diário.
    /// </summary>
    public Task<bool> ExisteAsync(Guid bancoId, LocalDate dataAlerta, CancellationToken ct = default);

    public void Add(AlertaExposicaoBanco alerta);

    public Task<int> SaveChangesAsync(CancellationToken ct = default);
}
