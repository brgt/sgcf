using NodaTime;

using Sgcf.Domain.Common;

namespace Sgcf.Domain.Painel;

/// <summary>
/// Unidade de entrada para o projetor de saldo mensal.
/// Representa tanto amortizações de contratos reais quanto captações hipotéticas de cenários.
/// Imutável por design — o projetor é uma função pura (AD-5, AD-6).
/// </summary>
/// <param name="BancoId">Identificador do banco credor.</param>
/// <param name="Data">Data em que o evento ocorre. Apenas o ano e mês são usados pelo projetor.</param>
/// <param name="Tipo">Se o evento reduz (<see cref="TipoEventoProjecao.AmortizacaoPrincipal"/>)
/// ou aumenta (<see cref="TipoEventoProjecao.Captacao"/>) o saldo do banco.</param>
/// <param name="ValorBrl">Valor do evento em BRL. Deve ser positivo.</param>
public sealed record EventoProjecao(
    Guid BancoId,
    LocalDate Data,
    TipoEventoProjecao Tipo,
    Money ValorBrl);
