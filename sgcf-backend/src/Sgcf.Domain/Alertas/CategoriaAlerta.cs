namespace Sgcf.Domain.Alertas;

/// <summary>
/// Categoriza o alerta por domínio funcional, permitindo filtros e roteamento por perfil de cockpit.
/// </summary>
public enum CategoriaAlerta : byte
{
    Vencimento  = 1,
    Hedge       = 2,
    Liquidez    = 3,
    LimiteBanco = 4,
    Covenant    = 5,
    Documento   = 6,
    Regulatorio = 7,
    Operacional = 8,
}
