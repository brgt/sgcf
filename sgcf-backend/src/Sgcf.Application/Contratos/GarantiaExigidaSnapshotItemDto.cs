namespace Sgcf.Application.Contratos;

/// <summary>
/// Projeção imutável de um item da <c>GarantiaExigidaRevisao</c> no momento da contratação.
/// Serve como snapshot — os dados aqui refletem a política vigente quando o contrato foi criado,
/// independentemente de alterações posteriores na política do banco.
/// SPEC §5.2, invariante SC-05.
/// </summary>
/// <param name="Tipo">Nome do enum <c>TipoGarantia</c> (ex: "AlienacaoFiduciaria").</param>
/// <param name="PercentualSobreLimite">
/// Percentual exigido sobre o valor do limite, em formato humano (ex: 80 = 80%).
/// Null quando o item usa <paramref name="ValorFixoBrl"/>.
/// </param>
/// <param name="ValorFixoBrl">
/// Valor fixo exigido em BRL.
/// Null quando o item usa <paramref name="PercentualSobreLimite"/>.
/// </param>
/// <param name="Obrigatoria">Indica se a garantia é obrigatória (não-negociável).</param>
/// <param name="Observacoes">Texto livre adicional — pode ser null.</param>
/// <param name="GrupoAlternativaId">Grupo de alternativas "OU" do item (null = item independente).</param>
/// <param name="GrupoRotulo">Rótulo do grupo (null quando sem grupo).</param>
public sealed record GarantiaExigidaSnapshotItemDto(
    string Tipo,
    decimal? PercentualSobreLimite,
    decimal? ValorFixoBrl,
    bool Obrigatoria,
    string? Observacoes,
    Guid? GrupoAlternativaId = null,
    string? GrupoRotulo = null);
