namespace Sgcf.Domain.Common;

/// <summary>
/// Denominador da base de cálculo de juros pro rata.
/// </summary>
public enum BaseCalculo
{
    Dias252 = 252,  // base dias úteis (padrão CDI/DI)
    Dias360 = 360,  // base comercial (FINIMP, 4131, internacional)
    Dias365 = 365   // base exata (alguns contratos de NCE/CCE)
}
