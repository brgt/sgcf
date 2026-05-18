using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;

namespace Sgcf.Application.Cotacoes.Conversores;

/// <summary>
/// Conversor de modalidade Capital de Giro.
/// Cria <see cref="CapitalDeGiroDetail"/> a partir do context de conversão.
/// Retorna <c>(CapitalDeGiroDetail, null)</c> — Capital de Giro nunca cria detail secundário.
/// SPEC §6 — Onda 3b.
/// </summary>
public sealed class ConversorCapitalDeGiro : IConversorModalidade
{
    /// <inheritdoc/>
    public ModalidadeContrato Modalidade => ModalidadeContrato.CapitalDeGiro;

    /// <inheritdoc/>
    public Task<(Entity, Entity?)> CriarDetailAsync(
        ConverterEmContratoContext ctx,
        CancellationToken cancellationToken)
    {
        // NumeroOperacao é opcional — vem de CapitalDeGiroInputs se informado.
        // Quando o operador não informa o bloco CapitalDeGiro no payload, NumeroOperacao fica null.
        string? numeroOperacao = ctx.Command.CapitalDeGiro?.NumeroOperacao;

        CapitalDeGiroDetail detail = CapitalDeGiroDetail.Criar(
            ctx.ContratoCriado.Id,
            numeroOperacao,
            ctx.Clock);

        // Capital de Giro não tem detail secundário (sem FgiDetail — SPEC §6 e §13).
        return Task.FromResult<(Entity, Entity?)>((detail, null));
    }
}
