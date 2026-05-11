using NodaTime;
using Sgcf.Domain.Common;

namespace Sgcf.Domain.Contabilidade;

public sealed class PlanoContasGerencial : Entity
{
    public string CodigoGerencial { get; private set; } = default!;
    public string Nome { get; private set; } = default!;
    public NaturezaConta Natureza { get; private set; }
    public string? CodigoSapB1 { get; private set; }
    public bool Ativo { get; private set; }
    public Instant CreatedAt { get; private set; }
    public Instant UpdatedAt { get; private set; }

    private PlanoContasGerencial() { }

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
