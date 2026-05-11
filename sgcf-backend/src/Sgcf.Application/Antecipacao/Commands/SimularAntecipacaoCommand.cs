using MediatR;
using NodaTime;
using Sgcf.Domain.Common;

namespace Sgcf.Application.Antecipacao.Commands;

/// <summary>
/// Comando para simular o custo de antecipação de um contrato.
/// </summary>
public sealed record SimularAntecipacaoCommand(
    Guid ContratoId,
    TipoAntecipacao TipoAntecipacao,
    LocalDate DataEfetiva,
    decimal? ValorPrincipalAQuitarMoedaOriginal,
    decimal? TaxaMercadoAtualAa,
    decimal? IndenizacaoBancoMoedaOriginal,
    bool SalvarSimulacao,
    string CreatedBy,
    string Source)
    : IRequest<ResultadoSimulacaoDto>;
