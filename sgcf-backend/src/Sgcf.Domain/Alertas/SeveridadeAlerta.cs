namespace Sgcf.Domain.Alertas;

/// <summary>
/// Nível de urgência do alerta, usado para ordenação visual no cockpit e definição de cor/ícone.
/// </summary>
public enum SeveridadeAlerta : byte
{
    Critico     = 1,
    Atencao     = 2,
    Informativo = 3,
}
