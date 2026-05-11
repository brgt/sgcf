namespace Sgcf.Application.Contratos;

// Block A — Identificação do contrato
public sealed record IdentificacaoDto(
    Guid Id,
    string CodigoInterno,
    string Banco,
    string Modalidade,
    string NumeroContratoExterno,
    DateOnly DataContratacao,
    DateOnly DataVencimento,
    string Status
);

// Block B — Valores do principal
public sealed record ValoresPrincipaisDto(
    decimal ValorPrincipalOriginal,
    string MoedaOriginal
);

// Block C — Encargos financeiros
public sealed record EncargosDto(
    decimal TaxaAaPct,
    string BaseCalculo,
    decimal? AliqIrrfPct,
    decimal? AliqIofPct
);

// Block D — Resumo financeiro consolidado
public sealed record ResumoFinanceiroDto(
    decimal TotalPrincipalPago,
    decimal TotalJurosPagos,
    decimal TotalComissoesPagas,
    string Moeda,
    decimal SaldoPrincipalAberto,
    decimal JurosProvisionados,
    decimal ComissoesAPagar,
    decimal SaldoTotalDevedor,
    decimal? SaldoPrincipalAbertoBrl,
    decimal? JurosProvisionadosBrl,
    decimal? ComissoesAPagarBrl,
    decimal? SaldoTotalDevedorBrl,
    int TotalEventos,
    int EventosPagos,
    int EventosEmAberto,
    int EventosEmAtraso,
    decimal PctAdimplencia,
    DateOnly? ProximaParcela,
    decimal? ValorProximaParcela,
    decimal PctPrincipalAmortizado,
    decimal PctPrazoDecorrido
);

// Block E — Linha do cronograma de eventos
public sealed record EventoCronogramaDto(
    short NumeroEvento,
    string Tipo,
    DateOnly DataPrevista,
    decimal Valor,
    string Moeda,
    decimal? SaldoDevedorApos,
    string Status,
    DateOnly? DataPagamentoEfetivo,
    decimal? ValorPagamentoEfetivo
);

// Block G — Hedge (placeholder para Fase 4)
public sealed record HedgePlaceholderDto(string Observacao);

// Block H — Histórico de pagamentos efetivos
public sealed record PagamentoEfetivoDto(
    short NumeroEvento,
    string Tipo,
    DateOnly DataPagamentoEfetivo,
    decimal ValorPagoMoedaOriginal,
    decimal? ValorPagoBrl,
    decimal? TaxaCambioPagamento
);

// Block I — Cotação aplicada para conversão BRL
public sealed record CotacaoAplicadaDto(
    string TipoCotacao,
    decimal ValorCotacao,
    string? DataHoraCotacao  // ISO 8601 string; null for manual override
);

// DTO principal com os 8 blocos do Annex B §7.1
public sealed record TabelaCompletaDto(
    IdentificacaoDto Identificacao,
    ValoresPrincipaisDto ValoresPrincipais,
    EncargosDto Encargos,
    ResumoFinanceiroDto ResumoFinanceiro,
    IReadOnlyList<EventoCronogramaDto> Cronograma,
    IReadOnlyList<GarantiaResumoDto> Garantias,
    HedgePlaceholderDto Hedge,
    IReadOnlyList<PagamentoEfetivoDto> HistoricoPagamentos,
    CotacaoAplicadaDto? CotacaoAplicada = null
);
