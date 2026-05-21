using NodaTime;
using Sgcf.Domain.Common;
using Sgcf.Domain.Tenancy;

namespace Sgcf.Domain.Tesouraria;

/// <summary>
/// Representa uma conta bancária pertencente a um tenant.
/// Cada conta está vinculada a um banco (FK <see cref="BancoId"/>), tem moeda definida
/// e suporta soft delete via <see cref="DeletedAt"/>.
/// </summary>
public sealed class ContaBancaria : Entity, ITenantScoped
{
    public Guid TenantId { get; private set; }

    /// <summary>FK para o catálogo global <c>Banco</c>.</summary>
    public Guid BancoId { get; private set; }

    /// <summary>Nome descritivo da conta; ex.: "Conta Corrente Principal". Máx. 200 chars.</summary>
    public string Nome { get; private set; } = default!;

    /// <summary>Número da agência. Máx. 10 chars.</summary>
    public string Agencia { get; private set; } = default!;

    /// <summary>Número da conta corrente/poupança. Máx. 20 chars.</summary>
    public string NumeroConta { get; private set; } = default!;

    /// <summary>Moeda da conta (BRL, USD, etc.).</summary>
    public Moeda Moeda { get; private set; }

    /// <summary>Indica se a conta está ativa para uso em movimentações.</summary>
    public bool Ativa { get; private set; }

    public Instant CriadoEm { get; private set; }
    public Instant AtualizadoEm { get; private set; }

    /// <summary>Preenchido pelo soft delete; nulo enquanto a conta não foi deletada.</summary>
    public Instant? DeletedAt { get; private set; }

    // Construtor privado para EF Core.
    private ContaBancaria() { }

    /// <summary>
    /// Cria uma nova conta bancária ativa. O TenantId é preenchido automaticamente pelo
    /// <c>TenantSaveInterceptor</c> no momento do INSERT — não é passado aqui por design.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Lançado quando <paramref name="bancoId"/> é vazio, ou <paramref name="nome"/>,
    /// <paramref name="agencia"/> ou <paramref name="numeroConta"/> são inválidos.
    /// </exception>
    public static ContaBancaria Criar(
        Guid bancoId,
        string nome,
        string agencia,
        string numeroConta,
        Moeda moeda,
        IClock clock)
    {
        if (bancoId == Guid.Empty)
        {
            throw new ArgumentException("BancoId não pode ser vazio.", nameof(bancoId));
        }

        ValidarNome(nome);
        ValidarAgencia(agencia);
        ValidarNumeroConta(numeroConta);

        Instant agora = clock.GetCurrentInstant();

        return new ContaBancaria
        {
            BancoId = bancoId,
            Nome = nome.Trim(),
            Agencia = agencia.Trim(),
            NumeroConta = numeroConta.Trim(),
            Moeda = moeda,
            Ativa = true,
            CriadoEm = agora,
            AtualizadoEm = agora,
        };
    }

    /// <summary>
    /// Atualiza os campos mutáveis da conta. Todos os parâmetros são obrigatórios —
    /// use os valores atuais para campos que não devem mudar.
    /// </summary>
    public void Atualizar(
        string nome,
        string agencia,
        string numeroConta,
        Moeda moeda,
        IClock clock)
    {
        ValidarNome(nome);
        ValidarAgencia(agencia);
        ValidarNumeroConta(numeroConta);

        Nome = nome.Trim();
        Agencia = agencia.Trim();
        NumeroConta = numeroConta.Trim();
        Moeda = moeda;
        AtualizadoEm = clock.GetCurrentInstant();
    }

    /// <summary>
    /// Desativa a conta, impedindo seu uso em novas movimentações. Idempotente.
    /// </summary>
    public void Desativar(IClock clock)
    {
        if (!Ativa)
        {
            return;
        }

        Ativa = false;
        AtualizadoEm = clock.GetCurrentInstant();
    }

    /// <summary>
    /// Reativa uma conta previamente desativada. Idempotente.
    /// </summary>
    public void Reativar(IClock clock)
    {
        if (Ativa)
        {
            return;
        }

        Ativa = true;
        AtualizadoEm = clock.GetCurrentInstant();
    }

    /// <summary>
    /// Soft delete: registra o <see cref="DeletedAt"/>. Idempotente — chamadas
    /// subsequentes não alteram o timestamp original da deleção.
    /// </summary>
    public void Deletar(IClock clock)
    {
        // Idempotente: preserva o timestamp da primeira deleção.
        if (DeletedAt.HasValue)
        {
            return;
        }

        DeletedAt = clock.GetCurrentInstant();
        AtualizadoEm = DeletedAt.Value;
    }

    // ── Validações ────────────────────────────────────────────────────────────

    private static void ValidarNome(string nome)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            throw new ArgumentException("Nome não pode ser vazio.", nameof(nome));
        }

        if (nome.Trim().Length > 200)
        {
            throw new ArgumentException("Nome não pode ter mais de 200 caracteres.", nameof(nome));
        }
    }

    private static void ValidarAgencia(string agencia)
    {
        if (string.IsNullOrWhiteSpace(agencia))
        {
            throw new ArgumentException("Agencia não pode ser vazia.", nameof(agencia));
        }

        if (agencia.Trim().Length > 10)
        {
            throw new ArgumentException("Agencia não pode ter mais de 10 caracteres.", nameof(agencia));
        }
    }

    private static void ValidarNumeroConta(string numeroConta)
    {
        if (string.IsNullOrWhiteSpace(numeroConta))
        {
            throw new ArgumentException("NumeroConta não pode ser vazio.", nameof(numeroConta));
        }

        if (numeroConta.Trim().Length > 20)
        {
            throw new ArgumentException("NumeroConta não pode ter mais de 20 caracteres.", nameof(numeroConta));
        }
    }
}
