namespace Sgcf.Application.Cotacoes.Exceptions;

/// <summary>
/// Lançada pelo <c>ConverterEmContratoHandler</c> quando ao menos um
/// <c>GarantiaExigidaItem</c> com <c>Obrigatoria = true</c> da revisão vigente
/// do <c>LimiteBanco</c> não é coberto pelas garantias declaradas no command de conversão.
/// Mapeada para HTTP 409 Conflict no <c>GlobalExceptionHandler</c> (SPEC §4.5, SC-04).
/// </summary>
public sealed class GarantiaExigidaNaoCobertaException : Exception
{
    /// <summary>Id do <c>LimiteBanco</c> cuja política de garantias gerou o bloqueio.</summary>
    public Guid LimiteBancoId { get; }

    /// <summary>Id da <c>GarantiaExigidaRevisao</c> vigente no momento do bloqueio.</summary>
    public Guid GarantiasExigidasRevisaoId { get; }

    /// <summary>Lista de garantias obrigatórias sem cobertura suficiente.</summary>
    public IReadOnlyList<LacunaGarantia> Lacunas { get; }

    public GarantiaExigidaNaoCobertaException(
        Guid limiteBancoId,
        Guid garantiasExigidasRevisaoId,
        IReadOnlyList<LacunaGarantia> lacunas)
        : base($"Conversão bloqueada: {lacunas.Count} garantia(s) obrigatória(s) sem cobertura.")
    {
        LimiteBancoId = limiteBancoId;
        GarantiasExigidasRevisaoId = garantiasExigidasRevisaoId;
        Lacunas = lacunas;
    }
}

/// <summary>
/// Detalhe de uma garantia obrigatória que não atingiu a cobertura mínima exigida.
/// Pode representar uma lacuna de <b>item</b> (independente) ou de <b>grupo</b> de alternativas "OU".
/// </summary>
/// <param name="Tipo">
/// Para itens independentes: nome do <c>TipoGarantia</c> (ex.: "CdbCativo", "Aval").
/// Para lacunas de grupo: <see cref="GrupoRotulo"/> se presente, senão <c>"Grupo: Tipo1 OU Tipo2"</c>.
/// </param>
/// <param name="Obrigatoria">Sempre <c>true</c> — apenas itens obrigatórios geram lacuna.</param>
/// <param name="ValorEsperadoBrl">
/// Valor mínimo esperado em BRL. <c>null</c> para Aval puro e para lacunas de grupo (baseadas em fração).
/// </param>
/// <param name="ValorCobertoBrl">
/// Soma dos valores BRL das garantias do tipo correspondente no contrato.
/// <c>null</c> para Aval puro e para lacunas de grupo.
/// </param>
/// <param name="GrupoAlternativaId">Id do grupo "OU". <c>null</c> para itens independentes.</param>
/// <param name="GrupoRotulo">Rótulo legível do grupo. <c>null</c> para itens independentes / grupo sem rótulo.</param>
/// <param name="AlternativasAceitas">Tipos aceitos no grupo. <c>null</c> para itens independentes.</param>
/// <param name="FracaoCoberta">Fração coberta (0.0–&lt;1.0), 4 casas AwayFromZero. <c>null</c> para itens independentes.</param>
public sealed record LacunaGarantia(
    string Tipo,
    bool Obrigatoria,
    decimal? ValorEsperadoBrl,
    decimal? ValorCobertoBrl,
    Guid? GrupoAlternativaId = null,
    string? GrupoRotulo = null,
    IReadOnlyList<string>? AlternativasAceitas = null,
    decimal? FracaoCoberta = null);
