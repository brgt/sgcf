using NodaTime;

using Sgcf.Domain.Auditoria;
using Sgcf.Domain.Common;
using Sgcf.Domain.Tenancy;

namespace Sgcf.Domain.Simulacao;

/// <summary>
/// Agregado raiz do módulo Simulação.
/// Representa um conjunto nomeado de captações hipotéticas futuras
/// (ex: "Realista 2026", "Otimista Q3") com ciclo de vida
/// Rascunho → Ativo → Arquivado.
///
/// SPEC §6.1, §6.2.
///
/// Invariantes de status:
///   - <c>Rascunho</c>: aceita todas as operações de edição
///   - <c>Ativo</c>: ainda aceita AdicionarSimulacao / RemoverSimulacao
///   - <c>Arquivado</c>: imutável — qualquer mutação lança <see cref="InvalidOperationException"/>
///
/// D-6 (Q1): domínio não impõe ownership exclusivo. CriadoPor é para auditoria.
/// </summary>
public sealed class CenarioSimulacao : Entity, IAuditable, ITenantScoped
{
    public Guid TenantId { get; private set; }
    private const int AnoBaseMinimo = 2020;
    private const int AnoBaseMaximo = 2050;
    private const int NomeMaxChars = 100;

    // ── Propriedades ──────────────────────────────────────────────────────────

    /// <summary>Nome legível (ex: "Realista 2026"). Máx. 100 caracteres.</summary>
    public string Nome { get; private set; } = default!;

    /// <summary>Descrição livre opcional.</summary>
    public string? Descricao { get; private set; }

    /// <summary>Ano-calendário de referência das simulações (ex: 2026).</summary>
    public int AnoBase { get; private set; }

    /// <summary>Status atual do ciclo de vida.</summary>
    public StatusCenarioSimulacao Status { get; private set; }

    /// <summary>Identificador do criador (sub do JWT). Informativo — não impede edição por outros.</summary>
    public string CriadoPor { get; private set; } = default!;

    public Instant CreatedAt { get; private set; }
    public Instant UpdatedAt { get; private set; }

    /// <summary>Soft delete: quando preenchido, o cenário é ocultado da listagem padrão.</summary>
    public Instant? DeletedAt { get; private set; }

    // ── Coleção de simulações ─────────────────────────────────────────────────

    private readonly List<SimulacaoContratacao> _simulacoes = new();

    /// <summary>Simulações filhas. Imutável externamente.</summary>
    public IReadOnlyCollection<SimulacaoContratacao> Simulacoes => _simulacoes.AsReadOnly();

    // ── Construtor privado para EF Core ──────────────────────────────────────

    private CenarioSimulacao() { }

    // ── Factory principal ─────────────────────────────────────────────────────

    /// <summary>
    /// Cria um novo cenário em status <see cref="StatusCenarioSimulacao.Rascunho"/>.
    /// </summary>
    /// <param name="nome">Nome do cenário. Não pode ser vazio. Máx. 100 chars.</param>
    /// <param name="anoBase">Ano-calendário. Deve estar entre 2020 e 2050.</param>
    /// <param name="criadoPor">Identificador do usuário criador (sub do JWT).</param>
    /// <param name="clock">Relógio para captura do timestamp de criação.</param>
    /// <param name="descricao">Descrição opcional.</param>
    public static CenarioSimulacao Criar(
        string nome,
        int anoBase,
        string criadoPor,
        IClock clock,
        string? descricao = null)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ValidarNome(nome);
        ValidarAnoBase(anoBase);

        Instant agora = clock.GetCurrentInstant();

        return new CenarioSimulacao
        {
            Nome = nome.Trim(),
            Descricao = descricao,
            AnoBase = anoBase,
            Status = StatusCenarioSimulacao.Rascunho,
            CriadoPor = criadoPor,
            CreatedAt = agora,
            UpdatedAt = agora
        };
    }

    // ── Transições de status ──────────────────────────────────────────────────

    /// <summary>
    /// Transição Rascunho → Ativo.
    /// </summary>
    public void Ativar(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ExigirNaoArquivado("Ativar");

        if (Status == StatusCenarioSimulacao.Ativo)
        {
            throw new InvalidOperationException("Cenário já está Ativo.");
        }

        UpdatedAt = clock.GetCurrentInstant();
        Status = StatusCenarioSimulacao.Ativo;
    }

    /// <summary>
    /// Transição Ativo → Arquivado.
    /// Somente cenários Ativos podem ser arquivados.
    /// </summary>
    public void Arquivar(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);

        if (Status != StatusCenarioSimulacao.Ativo)
        {
            throw new InvalidOperationException(
                $"Somente cenários Ativos podem ser arquivados. Status atual: {Status}.");
        }

        Status = StatusCenarioSimulacao.Arquivado;
        UpdatedAt = clock.GetCurrentInstant();
    }

    // ── Atualização de campos descritivos ─────────────────────────────────────

    /// <summary>
    /// Atualiza nome, descrição e/ou anoBase.
    /// Permitido em Rascunho ou Ativo.
    /// </summary>
    public void Atualizar(string? nome, string? descricao, int? anoBase, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ExigirNaoArquivado("Atualizar");

        if (nome is not null)
        {
            ValidarNome(nome);
            Nome = nome.Trim();
        }

        if (descricao is not null)
        {
            Descricao = descricao;
        }

        if (anoBase.HasValue)
        {
            // Invariante: AnoBase altera o significado de todas as simulações filhas
            // (datas previstas relativas ao ano-base). Permitido apenas em Rascunho.
            if (Status != StatusCenarioSimulacao.Rascunho && anoBase.Value != AnoBase)
            {
                throw new InvalidOperationException(
                    $"AnoBase não pode ser alterado em cenário com status {Status}. " +
                    "Duplique como Rascunho para experimentar outro ano-base.");
            }

            ValidarAnoBase(anoBase.Value);
            AnoBase = anoBase.Value;
        }

        UpdatedAt = clock.GetCurrentInstant();
    }

    // ── Gestão de simulações filhas ───────────────────────────────────────────

    /// <summary>
    /// Adiciona uma simulação ao cenário.
    /// Permitido em Rascunho e Ativo. Bloqueado em Arquivado.
    /// </summary>
    public void AdicionarSimulacao(SimulacaoContratacao simulacao, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(simulacao);
        ArgumentNullException.ThrowIfNull(clock);
        ExigirNaoArquivado("AdicionarSimulacao");

        _simulacoes.Add(simulacao);
        UpdatedAt = clock.GetCurrentInstant();
    }

    /// <summary>
    /// Remove uma simulação do cenário pelo Id.
    /// Permitido em Rascunho e Ativo. Bloqueado em Arquivado.
    /// </summary>
    public void RemoverSimulacao(Guid simulacaoId, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ExigirNaoArquivado("RemoverSimulacao");

        SimulacaoContratacao? simulacao = _simulacoes.Find(s => s.Id == simulacaoId)
            ?? throw new InvalidOperationException($"Simulação '{simulacaoId}' não encontrada neste cenário.");

        _simulacoes.Remove(simulacao);
        UpdatedAt = clock.GetCurrentInstant();
    }

    /// <summary>
    /// Executa uma mutação em uma simulação existente e atualiza UpdatedAt do cenário.
    /// Permitido em Rascunho e Ativo.
    /// </summary>
    public void AtualizarSimulacao(Guid simulacaoId, Action<SimulacaoContratacao> mutador, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(mutador);
        ArgumentNullException.ThrowIfNull(clock);
        ExigirNaoArquivado("AtualizarSimulacao");

        SimulacaoContratacao? simulacao = _simulacoes.Find(s => s.Id == simulacaoId)
            ?? throw new InvalidOperationException($"Simulação '{simulacaoId}' não encontrada neste cenário.");

        mutador(simulacao);
        UpdatedAt = clock.GetCurrentInstant();
    }

    // ── Soft delete ───────────────────────────────────────────────────────────

    /// <summary>Marca o cenário como deletado (soft delete). Permitido em qualquer status.</summary>
    public void Deletar(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        Instant agora = clock.GetCurrentInstant();
        DeletedAt = agora;
        UpdatedAt = agora;
    }

    // ── Factory DuplicarComoRascunho (D-10 / Task 2.1b) ──────────────────────

    /// <summary>
    /// Cria uma cópia profunda do cenário origem em status <see cref="StatusCenarioSimulacao.Rascunho"/>.
    ///
    /// Regras (SPEC D-10):
    ///   - Novo <c>Id</c> gerado automaticamente
    ///   - <c>Nome</c> = <c>"{origem.Nome} (cópia)"</c>
    ///   - <c>CriadoPor</c> = <paramref name="novoCriadoPor"/> (caller da duplicação)
    ///   - Todas as <see cref="Simulacoes"/> são copiadas com novos Ids e Version = 1
    ///   - Cenários Arquivados também podem ser duplicados
    /// </summary>
    /// <param name="origem">Cenário a duplicar. Pode estar em qualquer status.</param>
    /// <param name="novoCriadoPor">Identificador do usuário que executa a duplicação.</param>
    /// <param name="clock">Relógio para captura dos timestamps da cópia.</param>
    public static CenarioSimulacao DuplicarComoRascunho(
        CenarioSimulacao origem,
        string novoCriadoPor,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(origem);
        ArgumentNullException.ThrowIfNull(clock);

        // Invariante: cenário deletado não pode ser ressuscitado via duplicação.
        // O cliente deve restaurar explicitamente ou partir de outro cenário.
        if (origem.DeletedAt is not null)
        {
            throw new InvalidOperationException(
                "Cenário deletado não pode ser duplicado. Restaure-o primeiro ou parta de outro cenário.");
        }

        Instant agora = clock.GetCurrentInstant();

        CenarioSimulacao copia = new()
        {
            Nome = $"{origem.Nome} (cópia)",
            Descricao = origem.Descricao,
            AnoBase = origem.AnoBase,
            Status = StatusCenarioSimulacao.Rascunho,
            CriadoPor = novoCriadoPor,
            CreatedAt = agora,
            UpdatedAt = agora
        };

        // Cópia profunda — cada simulação recebe novo Id e Version = 1
        foreach (SimulacaoContratacao sim in origem.Simulacoes)
        {
            copia._simulacoes.Add(sim.CopiarParaCenario(copia.Id, agora));
        }

        return copia;
    }

    // ── Validações internas ───────────────────────────────────────────────────

    private static void ValidarNome(string nome)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            throw new ArgumentException("Nome do cenário não pode ser vazio.", nameof(nome));
        }

        if (nome.Trim().Length > NomeMaxChars)
        {
            throw new ArgumentException(
                $"Nome do cenário não pode exceder {NomeMaxChars} caracteres.", nameof(nome));
        }
    }

    private static void ValidarAnoBase(int anoBase)
    {
        if (anoBase < AnoBaseMinimo || anoBase > AnoBaseMaximo)
        {
            throw new ArgumentOutOfRangeException(
                nameof(anoBase),
                $"AnoBase deve estar entre {AnoBaseMinimo} e {AnoBaseMaximo}. Informado: {anoBase}.");
        }
    }

    /// <summary>Lança <see cref="InvalidOperationException"/> se o cenário estiver Arquivado.</summary>
    private void ExigirNaoArquivado(string operacao)
    {
        if (Status == StatusCenarioSimulacao.Arquivado)
        {
            throw new InvalidOperationException(
                $"Operação '{operacao}' não é permitida em cenário Arquivado.");
        }
    }
}
