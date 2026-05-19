namespace Sgcf.Application.Simulacao.Dtos;

/// <summary>
/// Input para adicionar uma nova simulação de contratação a um cenário.
/// Todos os campos são necessários para criar um <see cref="Sgcf.Domain.Simulacao.SimulacaoContratacao"/>.
/// Invariantes I-1..I-11 são verificadas pelo factory do domínio.
/// </summary>
public sealed record AdicionarSimulacaoInput(
    Guid BancoId,
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
