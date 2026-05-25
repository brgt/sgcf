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
/// </summary>
/// <param name="Tipo">Nome do <c>TipoGarantia</c> (ex.: "CdbCativo", "Aval").</param>
/// <param name="Obrigatoria">Sempre <c>true</c> — apenas itens obrigatórios geram lacuna.</param>
/// <param name="ValorEsperadoBrl">
/// Valor mínimo esperado em BRL. <c>null</c> para tipo Aval sem percentual e sem valor fixo
/// (cobertura satisfeita pela mera presença de qualquer garantia Aval).
/// </param>
/// <param name="ValorCobertoBrl">
/// Soma dos valores BRL das garantias do tipo correspondente no contrato.
/// <c>null</c> quando não há nenhuma garantia do tipo (mesmo caso especial do Aval puro).
/// </param>
public sealed record LacunaGarantia(
    string Tipo,
    bool Obrigatoria,
    decimal? ValorEsperadoBrl,
    decimal? ValorCobertoBrl);
