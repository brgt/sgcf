using NodaTime;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;

namespace Sgcf.Application.Contratos.Queries;

/// <summary>
/// Parâmetros de filtro, ordenação e paginação para <see cref="ListContratosQuery"/>.
/// Todos os campos são opcionais; quando omitidos, a dimensão correspondente não é filtrada.
/// </summary>
/// <param name="Search">Texto livre — aplica ILIKE em NumeroExterno e CodigoInterno.</param>
/// <param name="BancoId">Filtra pelo banco credor.</param>
/// <param name="Modalidade">Filtra por modalidade de contrato.</param>
/// <param name="Moeda">Filtra por moeda do principal.</param>
/// <param name="Status">Filtra por status do contrato.</param>
/// <param name="DataVencimentoDe">Data de vencimento mínima (inclusive).</param>
/// <param name="DataVencimentoAte">Data de vencimento máxima (inclusive).</param>
/// <param name="ValorPrincipalMin">Valor mínimo do principal na moeda original do contrato.</param>
/// <param name="ValorPrincipalMax">Valor máximo do principal na moeda original do contrato.</param>
/// <param name="TemHedge">Quando true, retorna apenas contratos com pelo menos um hedge vinculado.</param>
/// <param name="TemGarantia">Quando true, retorna apenas contratos com pelo menos uma garantia ativa.</param>
/// <param name="TemAlertaVencimento">Quando true, retorna apenas contratos com alerta de vencimento configurado.</param>
/// <param name="Page">Número da página (base 1).</param>
/// <param name="PageSize">Tamanho da página — mínimo 1, máximo 100.</param>
/// <param name="Sort">Campo de ordenação. Valores permitidos: DataVencimento | DataContratacao | ValorPrincipal | NumeroExterno.</param>
/// <param name="Dir">Direção da ordenação: asc | desc.</param>
public sealed record ContratoFilter(
    string? Search = null,
    Guid? BancoId = null,
    ModalidadeContrato? Modalidade = null,
    Moeda? Moeda = null,
    StatusContrato? Status = null,
    LocalDate? DataVencimentoDe = null,
    LocalDate? DataVencimentoAte = null,
    decimal? ValorPrincipalMin = null,
    decimal? ValorPrincipalMax = null,
    bool? TemHedge = null,
    bool? TemGarantia = null,
    bool? TemAlertaVencimento = null,
    int Page = 1,
    int PageSize = 25,
    string Sort = "DataVencimento",
    string Dir = "asc");
