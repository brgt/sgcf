using MediatR;

namespace Sgcf.Application.Painel.Queries;

/// <summary>
/// Retorna o saldo atual da carteira de contratos ativos agrupado por banco, convertido para BRL.
/// Não expõe endpoint REST próprio — é chamada internamente por <c>GetQuadroDividaQuery</c> (Task 1.4)
/// para obter o saldo de abertura do projetor mensal.
/// </summary>
public sealed record GetSaldoPorBancoAtualQuery() : IRequest<SaldoPorBancoAtualDto>;
