using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;

namespace Sgcf.Domain.Cotacoes;

/// <summary>
/// Descritor de garantia exigida sem identidade. Usado para construir
/// <see cref="GarantiaExigidaLimite"/> dentro do agregado <see cref="LimiteBanco"/>,
/// onde o LimiteBancoId é definido pelo próprio agregado.
/// </summary>
public sealed record GarantiaExigidaLimiteSpec(
    TipoGarantia Tipo,
    decimal? PercentualSobreLimite,
    Money? ValorFixoBrl,
    bool Obrigatoria,
    string? Observacoes);
