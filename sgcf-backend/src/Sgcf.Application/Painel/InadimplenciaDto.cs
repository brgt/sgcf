namespace Sgcf.Application.Painel;

/// <summary>
/// Agrega os contratos inadimplentes do tenant com dias de atraso médio e exposição total em BRL.
/// </summary>
/// <param name="QuantidadeContratos">Número de contratos com ao menos uma parcela em atraso.</param>
/// <param name="QuantidadeParcelas">Total de parcelas com <c>Status == Atrasado</c>.</param>
/// <param name="ExposicaoTotalBrl">Soma do valor em moeda original de todas as parcelas atrasadas, convertida para BRL.</param>
/// <param name="DiasAtrasoMedio">
/// Média aritmética dos dias vencidos de todas as parcelas atrasadas (cada parcela contribui com
/// <c>Ceiling(hoje - DataPrevista)</c> dias). Arredondado para 2 casas.
/// </param>
/// <param name="Contratos">Lista dos contratos inadimplentes, ordenada por <see cref="InadimplenciaItemDto.ExposicaoBrl"/> decrescente.</param>
public sealed record InadimplenciaDto(
    int QuantidadeContratos,
    int QuantidadeParcelas,
    decimal ExposicaoTotalBrl,
    decimal DiasAtrasoMedio,
    IReadOnlyList<InadimplenciaItemDto> Contratos);

/// <summary>
/// Detalhe de inadimplência de um contrato específico.
/// </summary>
/// <param name="ContratoId">Identificador do contrato.</param>
/// <param name="NumeroExterno">Número externo do contrato.</param>
/// <param name="BancoApelido">Apelido do banco credor; vazio quando o banco não for encontrado.</param>
/// <param name="QuantidadeParcelasAtrasadas">Número de parcelas com <c>Status == Atrasado</c> neste contrato.</param>
/// <param name="DiasAtrasoMaior">Dias de atraso da parcela mais antiga (pior caso do contrato).</param>
/// <param name="ExposicaoBrl">Soma das parcelas atrasadas deste contrato convertida para BRL.</param>
public sealed record InadimplenciaItemDto(
    Guid ContratoId,
    string NumeroExterno,
    string BancoApelido,
    int QuantidadeParcelasAtrasadas,
    int DiasAtrasoMaior,
    decimal ExposicaoBrl);
