using NodaTime;
using Sgcf.Application.Cotacoes.Commands;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Cotacoes;

/// <summary>
/// Strategy para criar o detail aggregate específico de uma modalidade ao converter
/// uma cotação aceita em contrato. Cada modalidade implementa uma instância e registra
/// em DI mapeada por <see cref="ModalidadeContrato"/>.
/// </summary>
public interface IConversorModalidade
{
    /// <summary>Modalidade que esta implementação cobre.</summary>
    public ModalidadeContrato Modalidade { get; }

    /// <summary>
    /// Cria a entidade Detail (FinimpDetail, NceDetail, etc.) a partir da cotação,
    /// proposta aceita, contrato recém-criado e inputs do command de conversão.
    /// </summary>
    /// <returns>
    /// Tupla (detalhe principal, detalhe secundário opcional). O detalhe secundário
    /// é reservado para casos futuros de detail composto. No MVP, todas as modalidades
    /// retornam (detail, null). A segunda posição é mantida para suportar evolução
    /// futura sem breaking change na interface.
    /// </returns>
    public Task<(Entity Principal, Entity? Secundario)> CriarDetailAsync(
        ConverterEmContratoContext ctx,
        CancellationToken cancellationToken);
}

/// <summary>
/// Dados imutáveis disponíveis para o conversor durante a criação do detail.
/// Passado por valor como record para evitar mutação acidental entre conversores.
/// </summary>
public sealed record ConverterEmContratoContext(
    Cotacao Cotacao,
    Proposta PropostaAceita,
    Contrato ContratoCriado,
    ConverterEmContratoCommand Command,
    IClock Clock);
