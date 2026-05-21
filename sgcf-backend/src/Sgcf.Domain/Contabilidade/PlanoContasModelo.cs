using NodaTime;
using Sgcf.Domain.Auditoria;
using Sgcf.Domain.Common;

namespace Sgcf.Domain.Contabilidade;

/// <summary>
/// Modelo global de plano de contas — referência para clonagem em novos tenants.
///
/// Não é tenant-scoped: uma única tabela global, visível por todos os tenants.
/// Mutável apenas por super-admin via <c>PlanoContasModeloController</c>.
///
/// Task −1.10: introduzido para suportar provisionamento per-tenant de PlanoContas.
/// A migration semeia o plano padrão brasileiro de financiamentos (~25 contas).
/// </summary>
public sealed class PlanoContasModelo : Entity, IAuditable
{
    /// <summary>Código gerencial hierárquico (ex.: "1.1.1"). Único globalmente.</summary>
    public string CodigoGerencial { get; private set; } = default!;

    /// <summary>Nome descritivo da conta no modelo padrão.</summary>
    public string Nome { get; private set; } = default!;

    /// <summary>Natureza contábil: Ativo, Passivo ou Resultado.</summary>
    public NaturezaConta Natureza { get; private set; }

    /// <summary>Código SAP B1, se aplicável (opcional).</summary>
    public string? CodigoSapB1 { get; private set; }

    public Instant CreatedAt { get; private set; }
    public Instant UpdatedAt { get; private set; }

    private PlanoContasModelo() { }

    /// <summary>
    /// Cria uma nova entrada no modelo global.
    /// Deve ser chamado apenas por super-admin ou pela migration de seed.
    /// </summary>
    public static PlanoContasModelo Criar(
        string codigoGerencial,
        string nome,
        NaturezaConta natureza,
        string? codigoSapB1,
        IClock clock)
    {
        if (string.IsNullOrWhiteSpace(codigoGerencial) || codigoGerencial.Length > 20)
        {
            throw new ArgumentException(
                "CodigoGerencial não pode ser vazio e deve ter no máximo 20 caracteres.",
                nameof(codigoGerencial));
        }

        if (string.IsNullOrWhiteSpace(nome))
        {
            throw new ArgumentException("Nome não pode ser vazio.", nameof(nome));
        }

        Instant agora = clock.GetCurrentInstant();
        return new PlanoContasModelo
        {
            CodigoGerencial = codigoGerencial,
            Nome = nome,
            Natureza = natureza,
            CodigoSapB1 = codigoSapB1,
            CreatedAt = agora,
            UpdatedAt = agora
        };
    }

    /// <summary>
    /// Renomeia a entrada no modelo global.
    /// Não retroage a tenants já provisionados — cada tenant tem cópia independente.
    /// </summary>
    public void Renomear(string novoNome, IClock clock)
    {
        if (string.IsNullOrWhiteSpace(novoNome))
        {
            throw new ArgumentException("Nome não pode ser vazio.", nameof(novoNome));
        }

        Nome = novoNome;
        UpdatedAt = clock.GetCurrentInstant();
    }
}
