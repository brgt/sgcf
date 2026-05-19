namespace Sgcf.Application.Simulacao.Dtos;

/// <summary>
/// Input para atualizar os campos mutáveis de uma simulação de contratação existente.
/// Todos os campos são obrigatórios — a atualização é total (não parcial).
/// Version é incrementado automaticamente pelo domínio (AD-3).
/// </summary>
public sealed record AtualizarSimulacaoInput(
    string Modalidade,
    string Moeda,
    decimal ValorPrincipal,
    DateOnly DataContratacaoPrevista,
    DateOnly DataPrimeiroVencimento,
    string TipoTaxa,
    decimal? TaxaAa,
    decimal? SpreadAa,
    string BaseCalculo,
    string EstruturaAmortizacao,
    string Periodicidade,
    int QuantidadeParcelas,
    string AnchorDiaMes,
    int? AnchorDiaFixo = null,
    string? GarantiaExigidaPrevista = null,
    string? Observacoes = null);
