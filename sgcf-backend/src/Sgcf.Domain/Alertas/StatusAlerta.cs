namespace Sgcf.Domain.Alertas;

/// <summary>
/// Ciclo de vida do alerta a partir da perspectiva do usuário.
/// </summary>
public enum StatusAlerta : byte
{
    Aberto     = 1,
    Lido       = 2,
    Dispensado = 3,
}
