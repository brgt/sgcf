using Sgcf.Domain.Common;

namespace Sgcf.Domain.Calculo;

/// <summary>
/// Resultado do cálculo de saldo devedor em uma data de referência.
/// Composto por três componentes conforme Annex B §6.4.
/// </summary>
public sealed record ResultadoSaldo(
    Money SaldoPrincipalAberto,
    Money JurosProvisionados,
    Money ComissoesAPagar,
    Money SaldoTotal,
    Money? SaldoPrincipalAbertoBrl = null,
    Money? JurosProvisionadosBrl = null,
    Money? ComissoesAPagarBrl = null,
    Money? SaldoTotalBrl = null
);
