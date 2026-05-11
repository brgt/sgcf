using Sgcf.Domain.Contratos;

namespace Sgcf.Application.Contratos;

/// <summary>Detalhe de CDB Cativo retornado na resposta.</summary>
public sealed record GarantiaCdbDto(
    string BancoCustodia,
    string NumeroCdb,
    string DataEmissaoCdb,
    string DataVencimentoCdb,
    decimal? RendimentoAaPct,
    decimal? PercentualCdiPct,
    decimal? TaxaIrrfAplicacaoPct);

/// <summary>Detalhe de SBLC retornado na resposta.</summary>
public sealed record GarantiaSblcDto(
    string BancoEmissor,
    string PaisEmissor,
    string? SwiftCode,
    int ValidadeDias,
    decimal? ComissaoAaPct,
    string? NumeroSblc);

/// <summary>Detalhe de Aval retornado na resposta.</summary>
public sealed record GarantiaAvalDto(
    string AvalistaTipo,
    string AvalistaNome,
    string AvalistaDocumento,
    decimal ValorAvalBrl,
    string? VigenciaAte);

/// <summary>Detalhe de Alienação Fiduciária retornado na resposta.</summary>
public sealed record GarantiaAlienacaoDto(
    string TipoBem,
    string DescricaoBem,
    decimal ValorAvaliadoBrl,
    string? MatriculaOuChassi,
    string? CartorioRegistro);

/// <summary>Detalhe de Duplicatas retornado na resposta.</summary>
public sealed record GarantiaDuplicatasDto(
    decimal PercentualDescontoPct,
    string VencimentoEscalonadoInicio,
    string VencimentoEscalonadoFim,
    int QtdDuplicatasCedidas,
    decimal ValorTotalDuplicatasBrl,
    string? InstrumentoCessaoData);

/// <summary>Detalhe de Recebíveis de Cartão retornado na resposta.</summary>
public sealed record GarantiaRecebiveisDto(
    string OperadoraCartao,
    string TipoRecebivel,
    decimal PercentualFaturamentoPct,
    decimal? ValorMedioMensalBrl,
    int PrazoRecebimentoDias,
    string? TermoCessaoUrl);

/// <summary>Detalhe de Boleto Bancário retornado na resposta.</summary>
public sealed record GarantiaBoletoBancarioDto(
    string BancoEmissor,
    int QuantidadeBoletos,
    decimal ValorUnitarioBrl,
    string DataEmissaoInicial,
    string DataVencimentoInicial,
    string DataVencimentoFinal,
    string Periodicidade);

/// <summary>Detalhe de FGI (garantia) retornado na resposta.</summary>
public sealed record GarantiaFgiDto(
    string TipoFgi,
    decimal PercentualCoberturaPct,
    decimal? TaxaFgiAaPct,
    string? BancoIntermediario,
    string? CodigoOperacaoBndes);

/// <summary>
/// DTO de retorno para uma <see cref="Garantia"/> com seus detalhes polimórficos.
/// Apenas um dos campos de detalhe será não-nulo, correspondendo ao <see cref="TipoGarantia"/>.
/// </summary>
public sealed record GarantiaDto(
    Guid Id,
    Guid ContratoId,
    string Tipo,
    decimal ValorBrl,
    decimal? PercentualPrincipalPct,
    string DataConstituicao,
    string? DataLiberacaoPrevista,
    string? DataLiberacaoEfetiva,
    string Status,
    string? Observacoes,
    IReadOnlyList<string> Alertas,
    GarantiaCdbDto? Cdb,
    GarantiaSblcDto? Sblc,
    GarantiaAvalDto? Aval,
    GarantiaAlienacaoDto? Alienacao,
    GarantiaDuplicatasDto? Duplicatas,
    GarantiaRecebiveisDto? Recebiveis,
    GarantiaBoletoBancarioDto? Boleto,
    GarantiaFgiDto? FgiDetalhe);

/// <summary>DTO de indicadores consolidados de garantias para um contrato.</summary>
public sealed record IndicadoresGarantiaDto(
    decimal CoberturaTotalBrl,
    decimal PercentualCoberturaTotalPct,
    decimal CoberturaLiquidaSemCdbBrl,
    decimal PercentualCoberturaLiquidaPct,
    decimal PercentualFaturamentoCartaoComprometidoPct,
    IReadOnlyList<string> Alertas);
