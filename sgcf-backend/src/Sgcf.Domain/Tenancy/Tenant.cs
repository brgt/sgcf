using System.Text.RegularExpressions;
using NodaTime;
using Sgcf.Domain.Auditoria;
using Sgcf.Domain.Common;

namespace Sgcf.Domain.Tenancy;

/// <summary>
/// Agregado raiz que representa um cliente (empresa) no sistema multi-tenant.
/// Cada tenant agrupa todos os seus contratos, cotações e demais entidades de negócio.
/// </summary>
public sealed class Tenant : Entity, IAuditable
{
    // Regex compilada uma única vez: kebab-case, começa com letra minúscula, 3-31 chars total.
    private static readonly Regex SlugRegex = new("^[a-z][a-z0-9-]{2,30}$", RegexOptions.Compiled);

    public string Slug { get; private set; } = default!;
    public string Nome { get; private set; } = default!;

    /// <summary>CNPJ mascarado para exibição — nunca armazena o CNPJ completo.</summary>
    public string CnpjMascarado { get; private set; } = default!;

    public StatusTenant Status { get; private set; }
    public PlanoAssinatura Plano { get; private set; }
    public Instant CriadoEm { get; private set; }
    public Instant? SuspensoEm { get; private set; }
    public Instant? ArquivadoEm { get; private set; }
    public Instant UpdatedAt { get; private set; }

    // Construtor privado para EF Core (sem parâmetros).
    private Tenant() { }

    // Construtor privado que passa o id explícito para a base Entity.
    private Tenant(Guid id) : base(id) { }

    /// <summary>
    /// Cria um novo tenant ativo. O <paramref name="id"/> deve ser gerado pelo chamador
    /// (normalmente via <c>Guid.CreateVersion7()</c> ou UUID fixo para seeds de dev).
    /// </summary>
    public static Tenant Criar(
        Guid id,
        string? slug,
        string? nome,
        string? cnpj,
        PlanoAssinatura plano,
        IClock clock)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Id inválido.", nameof(id));
        }

        if (!SlugRegex.IsMatch(slug ?? string.Empty))
        {
            throw new ArgumentException(
                "Slug deve ser kebab-case [a-z][a-z0-9-]{2,30}.", nameof(slug));
        }

        if (string.IsNullOrWhiteSpace(nome))
        {
            throw new ArgumentException("Nome obrigatório.", nameof(nome));
        }

        string cnpjDigits = cnpj is null
            ? string.Empty
            : new string(cnpj.Where(char.IsDigit).ToArray());
        if (cnpjDigits.Length < 14)
        {
            throw new ArgumentException("CNPJ inválido — deve ter 14 dígitos.", nameof(cnpj));
        }

        Instant agora = clock.GetCurrentInstant();

        // slug and nome are proven non-null/non-empty by the guards above
        return new Tenant(id)
        {
            Slug = slug!.Trim().ToLowerInvariant(),
            Nome = nome!.Trim(),
            CnpjMascarado = MascararCnpj(cnpjDigits),
            Plano = plano,
            Status = StatusTenant.Ativo,
            CriadoEm = agora,
            UpdatedAt = agora,
        };
    }

    /// <summary>
    /// Suspende o tenant, bloqueando acesso dos usuários. Idempotente se já suspenso.
    /// Lança <see cref="InvalidOperationException"/> se arquivado.
    /// </summary>
    public void Suspender(string motivo, IClock clock)
    {
        if (Status == StatusTenant.Arquivado)
        {
            throw new InvalidOperationException("Tenant arquivado não pode ser suspenso.");
        }

        if (Status == StatusTenant.Suspenso)
        {
            return;
        }

        Instant agora = clock.GetCurrentInstant();
        Status = StatusTenant.Suspenso;
        SuspensoEm = agora;
        UpdatedAt = agora;
    }

    /// <summary>
    /// Reativa um tenant suspenso. Idempotente se já ativo.
    /// Lança <see cref="InvalidOperationException"/> se arquivado.
    /// </summary>
    public void Reativar(IClock clock)
    {
        if (Status == StatusTenant.Arquivado)
        {
            throw new InvalidOperationException("Tenant arquivado não pode ser reativado.");
        }

        if (Status == StatusTenant.Ativo)
        {
            return;
        }

        Status = StatusTenant.Ativo;
        SuspensoEm = null;
        UpdatedAt = clock.GetCurrentInstant();
    }

    /// <summary>
    /// Arquiva o tenant definitivamente. Idempotente se já arquivado.
    /// </summary>
    public void Arquivar(IClock clock)
    {
        if (Status == StatusTenant.Arquivado)
        {
            return;
        }

        Instant agora = clock.GetCurrentInstant();
        Status = StatusTenant.Arquivado;
        ArquivadoEm = agora;
        UpdatedAt = agora;
    }

    /// <summary>Atualiza o plano de assinatura do tenant.</summary>
    public void AtualizarPlano(PlanoAssinatura novoPlano, IClock clock)
    {
        Plano = novoPlano;
        UpdatedAt = clock.GetCurrentInstant();
    }

    /// <summary>Exibe apenas os dois primeiros e dois últimos dígitos do CNPJ.</summary>
    private static string MascararCnpj(string cnpjDigits) =>
        $"{cnpjDigits[..2]}.***.***/****-{cnpjDigits[^2..]}";
}
