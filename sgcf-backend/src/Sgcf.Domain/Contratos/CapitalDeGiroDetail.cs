using NodaTime;
using Sgcf.Domain.Common;
using Sgcf.Domain.Tenancy;

namespace Sgcf.Domain.Contratos;

/// <summary>
/// Detalhe de contrato Capital de Giro — crédito direto BRL universal ofertado por qualquer banco comercial.
/// Tabela de extensão 1:1 com <see cref="Contrato"/> — mesma convenção de FinimpDetail, NceDetail e Lei4131Detail.
/// <para>
/// Campos removidos na Onda 3b (SPEC §3.3 e §3.4):
/// - <c>TipoProduto</c>: particularidade interna do banco; sistema agnóstico ao produto.
/// - <c>TemFgi</c>: FGI é modelado via <c>GarantiaExigidaLimite.Tipo=Fgi</c> no limite do banco,
///   ou via modalidade própria <c>ModalidadeContrato.Fgi</c> quando o produto é BNDES-FGI direto.
/// </para>
/// </summary>
public sealed class CapitalDeGiroDetail : Entity, ITenantScoped
{
    public Guid TenantId { get; private set; }
    public Guid ContratoId { get; private set; }

    /// <summary>Número da operação no sistema interno do banco (opcional).</summary>
    public string? NumeroOperacao { get; private set; }

    public Instant CreatedAt { get; private set; }
    public Instant UpdatedAt { get; private set; }

    private CapitalDeGiroDetail() { }

    /// <summary>
    /// Cria um novo <see cref="CapitalDeGiroDetail"/>.
    /// </summary>
    /// <param name="contratoId">Id do contrato associado. Não pode ser <see cref="Guid.Empty"/>.</param>
    /// <param name="numeroOperacao">Número de operação no sistema do banco — opcional.</param>
    /// <param name="clock">Relógio para atribuição de timestamps.</param>
    /// <exception cref="ArgumentException">Quando <paramref name="contratoId"/> é <see cref="Guid.Empty"/>.</exception>
    public static CapitalDeGiroDetail Criar(
        Guid contratoId,
        string? numeroOperacao,
        IClock clock)
    {
        if (contratoId == Guid.Empty)
        {
            throw new ArgumentException("contratoId não pode ser Guid.Empty.", nameof(contratoId));
        }

        Instant now = clock.GetCurrentInstant();
        return new CapitalDeGiroDetail
        {
            ContratoId = contratoId,
            NumeroOperacao = numeroOperacao,
            CreatedAt = now,
            UpdatedAt = now
        };
    }
}
