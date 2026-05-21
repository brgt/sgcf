namespace Sgcf.Domain.Cotacoes;

/// <summary>
/// Ciclo de vida de uma Cotacao. Valores byte fixos — não reordenar (compatibilidade com migrations).
/// Valores 7 e 8 foram adicionados ao final para não deslocar os bytes existentes no PostgreSQL.
/// Ver máquina de estados em docs/specs/cotacoes/SPEC.md §4.
/// </summary>
public enum StatusCotacao : byte
{
    Rascunho         = 1,
    EmCaptacao       = 2,  // enviada aos bancos, aguardando confirmação de análise
    Comparada        = 3,  // todas as propostas recebidas, em análise interna
    Aceita           = 4,
    Convertida       = 5,
    Recusada         = 6,
    EmAnaliseBanco   = 7,  // banco confirmou recebimento e está analisando
    PropostaRecebida = 8,  // ao menos uma proposta foi registrada
}
