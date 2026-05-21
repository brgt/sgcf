namespace Sgcf.Application.Tesouraria;

/// <summary>
/// Agregação de fluxo de caixa para um único dia do calendário.
/// </summary>
/// <param name="Data">Data no formato ISO "yyyy-MM-dd".</param>
/// <param name="EntradasBrl">Soma das entradas do dia convertidas para BRL.</param>
/// <param name="SaidasBrl">Soma das saídas do dia convertidas para BRL.</param>
/// <param name="SaldoProjetadoBrl">Saldo acumulado projetado em BRL até e incluindo este dia.</param>
/// <param name="Eventos">Lista dos eventos individuais que compõem o dia.</param>
/// <param name="Alertas">Alertas gerados para o dia (ex.: saldo projetado negativo).</param>
public sealed record FluxoCaixaDiaDto(
    string Data,
    decimal EntradasBrl,
    decimal SaidasBrl,
    decimal SaldoProjetadoBrl,
    IReadOnlyList<FluxoCaixaEventoDto> Eventos,
    IReadOnlyList<string> Alertas);

/// <summary>
/// Detalhe de um evento individual dentro do fluxo de caixa diário.
/// </summary>
/// <param name="Origem">"cronograma" para eventos do cronograma de contratos; "manual" para <see cref="Domain.Tesouraria.EventoFluxoCaixa"/>.</param>
/// <param name="Tipo">"Entrada" ou "Saida".</param>
/// <param name="Descricao">Descrição do evento.</param>
/// <param name="ValorBrl">Valor do evento convertido para BRL.</param>
public sealed record FluxoCaixaEventoDto(
    string Origem,
    string Tipo,
    string Descricao,
    decimal ValorBrl);

/// <summary>
/// Payload de um único item do lote enviado ao endpoint <c>POST /tesouraria/eventos-fluxo</c>.
/// </summary>
public sealed record CreateEventoFluxoCaixaItemDto(
    string Data,
    string Tipo,
    decimal Valor,
    string Moeda,
    string Descricao,
    string RegistradoPor);
