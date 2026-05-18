using Sgcf.Application.Bancos;
using Sgcf.Application.Contratos;
using Sgcf.Application.Cotacoes.Commands;
using Sgcf.Domain.Bancos;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Cotacoes.Conversores;

/// <summary>
/// Conversor da modalidade REFINIMP — implementação real (Onda 1).
/// Resolve ancestral via IContratoRepository, aplica regra 70% BB e cria RefinimpDetail.
/// Marca o contrato mãe imediato como RefinanciadoParcial ou RefinanciadoTotal.
/// SPEC §6.1 — docs/specs/cotacoes/modalidades/refinimp.md.
/// </summary>
public sealed class ConversorRefinimp(
    IContratoRepository contratoRepo,
    IBancoRepository bancoRepo) : IConversorModalidade
{
    /// <summary>Código COMPE do Banco do Brasil — sujeito à regra de 70%.</summary>
    private const string CodigoCompeBancoDoBrasil = "001";

    /// <inheritdoc/>
    public ModalidadeContrato Modalidade => ModalidadeContrato.Refinimp;

    /// <inheritdoc/>
    public async Task<(Entity Principal, Entity? Secundario)> CriarDetailAsync(
        ConverterEmContratoContext ctx,
        CancellationToken cancellationToken)
    {
        // Guard: invariante do domínio — ContratoMaeId deve estar presente em cotação REFINIMP.
        Guid maeId = ctx.Cotacao.ContratoMaeId
            ?? throw new InvalidOperationException(
                "Cotação REFINIMP sem ContratoMaeId — invariante de domínio violada. " +
                "Verifique que Cotacao.Criar validou a presença do ContratoMaeId.");

        // ── 1. Carregar contrato mãe imediato ──────────────────────────────────
        Contrato contratoMae = await contratoRepo.GetByIdAsync(maeId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Contrato mãe '{maeId}' não encontrado ao converter cotação REFINIMP.");

        // ── 2. Defesa em profundidade: moeda do contrato deve coincidir com o mãe ──
        // Esta validação já ocorre em RegistrarPropostaCommand — aqui é defesa de profundidade.
        if (ctx.ContratoCriado.Moeda != contratoMae.Moeda)
        {
            throw new InvalidOperationException(
                $"A moeda do REFINIMP ({ctx.ContratoCriado.Moeda}) deve ser igual à moeda " +
                $"do contrato mãe ({contratoMae.Moeda}). Verifique a proposta aceita.");
        }

        // ── 3. Navegar até o ancestral original (FINIMP raiz) ─────────────────
        Contrato ancestral = await contratoRepo.GetAncestraNaoRefinimpAsync(maeId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Não foi possível localizar o ancestral original do contrato mãe '{maeId}'. " +
                "Verifique se a cadeia REFINIMP está corretamente configurada.");

        Money valorPrincipal = ctx.ContratoCriado.ValorPrincipal;
        Money valorPrincipalAncestral = ancestral.ValorPrincipal;

        // ── 4. Regra 70% Banco do Brasil ──────────────────────────────────────
        Banco banco = await bancoRepo.GetByIdAsync(ctx.ContratoCriado.BancoId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Banco '{ctx.ContratoCriado.BancoId}' não encontrado ao converter REFINIMP.");

        if (banco.CodigoCompe == CodigoCompeBancoDoBrasil)
        {
            // Limite = 70% do principal do ancestral FINIMP original.
            // O divisor 100 não se aplica aqui — calculamos em Money (moeda original, não BRL).
            Money limite70 = new(
                Math.Round(valorPrincipalAncestral.Valor * 0.70m, 6, MidpointRounding.AwayFromZero),
                valorPrincipalAncestral.Moeda);

            if (valorPrincipal.Valor > limite70.Valor)
            {
                throw new InvalidOperationException(
                    $"Banco do Brasil (001): o valor do REFINIMP ({valorPrincipal}) " +
                    $"excede 70% do principal do contrato ancestral ({limite70}). " +
                    "Reduza o valor ou escolha outro banco.");
            }
        }

        // ── 5. Calcular percentual sobre o ancestral original ─────────────────
        // Preserva o contrato de ProcessarRefinimpAsync linha 399:
        // percentual = valorPrincipal / ancestral.ValorPrincipal (fração sobre o FINIMP raiz).
        decimal percentualFracao = valorPrincipalAncestral.Valor == 0m
            ? 0m
            : Math.Round(valorPrincipal.Valor / valorPrincipalAncestral.Valor, 10, MidpointRounding.AwayFromZero);

        Percentual percentual = Percentual.DeFracao(percentualFracao);

        // ── 6. Criar RefinimpDetail ───────────────────────────────────────────
        RefinimpDetail detail = RefinimpDetail.Criar(
            contratoId: ctx.ContratoCriado.Id,
            contratoMaeId: maeId,
            percentualRefinanciado: percentual,
            valorQuitadoNoRefi: valorPrincipal,
            clock: ctx.Clock);

        // ── 7. Marcar contrato mãe imediato ───────────────────────────────────
        // Condição preservada de ProcessarRefinimpAsync linha 413:
        // usa fração sobre o ancestral (não sobre o mãe imediato) — comportamento atual mantido.
        // Ver SPEC §8.4 nota sobre inconsistência preservada.
        if (percentualFracao >= 1.0m)
        {
            contratoMae.MarcarRefinanciadoTotal(ctx.Clock);
        }
        else
        {
            contratoMae.MarcarRefinanciadoParcial(ctx.Clock);
        }

        // Secundário é sempre null para REFINIMP (sem detail composto como Balcão+FGI).
        return (detail, null);
    }
}
