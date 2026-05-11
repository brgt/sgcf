using MediatR;

namespace Sgcf.Application.Painel.Commands;

/// <summary>
/// Cria ou atualiza o EBITDA mensal para o ano/mês especificado.
/// Se já existir registro, atualiza o valor (upsert semântico).
/// </summary>
public sealed record UpsertEbitdaMensalCommand(
    int Ano,
    int Mes,
    decimal ValorBrl,
    string CreatedBy) : IRequest<Unit>;
