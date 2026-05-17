using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Cotacoes;

/// <summary>
/// Estrutura de entrada para declarar uma garantia exigida ao criar ou atualizar um limite.
/// Não carrega identidade — o agregado LimiteBanco é responsável por atribuir Id.
/// </summary>
public sealed record CriarGarantiaExigidaLimiteRequest(
    /// <summary>Nome do enum TipoGarantia (ex: "CdbCativo", "Aval"). Case-insensitive.</summary>
    string Tipo,
    decimal? PercentualSobreLimite = null,
    decimal? ValorFixoBrl = null,
    bool Obrigatoria = true,
    string? Observacoes = null)
{
    /// <summary>
    /// Converte o request em <see cref="GarantiaExigidaLimiteSpec"/> para passar ao domínio.
    /// Lança <see cref="ArgumentException"/> se o Tipo não for um valor válido do enum.
    /// A validação XOR (percentual×valorFixo) é deliberadamente delegada ao domínio
    /// para que retorne 409 via InvalidOperationException ou 400 via ArgumentException.
    /// </summary>
    public GarantiaExigidaLimiteSpec ParaSpec()
    {
        if (!Enum.TryParse<TipoGarantia>(Tipo, ignoreCase: true, out TipoGarantia tipo))
        {
            throw new ArgumentException(
                $"Tipo de garantia inválido: '{Tipo}'. Valores aceitos: {string.Join(", ", Enum.GetNames<TipoGarantia>())}.",
                nameof(Tipo));
        }

        Money? valorFixo = ValorFixoBrl.HasValue
            ? new Money(ValorFixoBrl.Value, Moeda.Brl)
            : null;

        return new GarantiaExigidaLimiteSpec(tipo, PercentualSobreLimite, valorFixo, Obrigatoria, Observacoes);
    }
}
