using NodaTime;
using Sgcf.Domain.Common;
using Sgcf.Domain.Tenancy;

namespace Sgcf.Domain.Contabilidade;

/// <summary>
/// Conta do plano de contas gerencial de um tenant específico.
///
/// Cada tenant tem sua própria cópia independente, gerada pelo provisionamento
/// via <see cref="ClonarDeModelo"/> a partir de <see cref="PlanoContasModelo"/>.
/// Edições em um tenant não afetam outros tenants nem o modelo global.
///
/// Task −1.10: adicionado <see cref="ClonadaDeModelo"/> para rastreabilidade de origem.
/// </summary>
public sealed class PlanoContasGerencial : Entity, ITenantScoped
{
    public Guid TenantId { get; private set; }
    public string CodigoGerencial { get; private set; } = default!;
    public string Nome { get; private set; } = default!;
    public NaturezaConta Natureza { get; private set; }
    public string? CodigoSapB1 { get; private set; }
    public bool Ativo { get; private set; }

    /// <summary>
    /// Indica se esta conta foi criada via clonagem do modelo global.
    /// Contas criadas diretamente pelo tenant (custom) têm este campo como false.
    /// </summary>
    public bool ClonadaDeModelo { get; private set; }

    public Instant CreatedAt { get; private set; }
    public Instant UpdatedAt { get; private set; }

    private PlanoContasGerencial() { }

    /// <summary>
    /// Clona uma entrada do modelo global para um tenant.
    /// Chamado pelo provisioner — TenantId é injetado pelo <c>TenantSaveInterceptor</c>
    /// que está ativo no escopo do provisionamento.
    /// </summary>
    public static PlanoContasGerencial ClonarDeModelo(PlanoContasModelo modelo, IClock clock)
    {
        Instant agora = clock.GetCurrentInstant();
        return new PlanoContasGerencial
        {
            CodigoGerencial = modelo.CodigoGerencial,
            Nome = modelo.Nome,
            Natureza = modelo.Natureza,
            CodigoSapB1 = modelo.CodigoSapB1,
            Ativo = true,
            ClonadaDeModelo = true,
            CreatedAt = agora,
            UpdatedAt = agora
        };
    }

    /// <summary>
    /// Cria uma conta custom no tenant (não derivada do modelo).
    /// </summary>
    public static PlanoContasGerencial Criar(
        string codigoGerencial,
        string nome,
        NaturezaConta natureza,
        IClock clock)
    {
        if (string.IsNullOrWhiteSpace(codigoGerencial) || codigoGerencial.Length > 20)
        {
            throw new ArgumentException("CodigoGerencial não pode ser vazio e deve ter no máximo 20 caracteres.", nameof(codigoGerencial));
        }

        if (string.IsNullOrWhiteSpace(nome))
        {
            throw new ArgumentException("Nome não pode ser vazio.", nameof(nome));
        }

        Instant now = clock.GetCurrentInstant();
        return new PlanoContasGerencial
        {
            CodigoGerencial = codigoGerencial,
            Nome = nome,
            Natureza = natureza,
            CodigoSapB1 = null,
            Ativo = true,
            ClonadaDeModelo = false,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void Atualizar(string nome, NaturezaConta natureza, string? codigoSapB1, IClock clock)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            throw new ArgumentException("Nome não pode ser vazio.", nameof(nome));
        }

        Nome = nome;
        Natureza = natureza;
        CodigoSapB1 = codigoSapB1;
        UpdatedAt = clock.GetCurrentInstant();
    }

    public void Desativar(IClock clock)
    {
        Ativo = false;
        UpdatedAt = clock.GetCurrentInstant();
    }
}
