namespace Sgcf.Application.Cotacoes;

/// <summary>
/// Resposta do comando <c>AdicionarBancoNaCotacaoCommand</c>.
/// Contém dados de garantia pré-preenchidos (quando <c>preencherGarantiaAutomaticamente = true</c>
/// e o <see cref="LimiteBanco"/> possui garantias exigidas) e alertas informativos de coerência.
/// </summary>
/// <param name="BancoId">Banco adicionado à cotação.</param>
/// <param name="CotacaoId">Cotação alvo.</param>
/// <param name="Proposta">
/// Template de garantia sugerido para uso ao registrar a Proposta.
/// Null quando o limite não possui garantias exigidas ou <c>preencherGarantiaAutomaticamente = false</c>.
/// </param>
/// <param name="Alertas">
/// Alertas informativos quando valores manuais fornecidos divergem do pré-preenchimento calculado.
/// Não bloqueia; apenas informa o caller sobre a divergência.
/// </param>
public sealed record AdicionarBancoNaCotacaoResponse(
    Guid BancoId,
    Guid CotacaoId,
    GarantiaPreenchidaDto? Proposta,
    IReadOnlyList<string> Alertas);

/// <summary>
/// Template de valores de garantia sugeridos para a <see cref="Domain.Cotacoes.Proposta"/>,
/// derivado das <see cref="Domain.Cotacoes.GarantiaExigidaLimite"/> do <see cref="Domain.Cotacoes.LimiteBanco"/>.
/// </summary>
/// <param name="GarantiaExigida">String formatada para o campo GarantiaExigida da Proposta.</param>
/// <param name="ValorGarantiaExigidaBrl">Valor total calculado das garantias em BRL.</param>
/// <param name="GarantiaEhCdbCativo">Indica se alguma das garantias é do tipo CDB cativo.</param>
public sealed record GarantiaPreenchidaDto(
    string GarantiaExigida,
    decimal ValorGarantiaExigidaBrl,
    bool GarantiaEhCdbCativo);
