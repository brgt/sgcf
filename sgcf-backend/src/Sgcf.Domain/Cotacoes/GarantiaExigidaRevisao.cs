using NodaTime;
using Sgcf.Domain.Auditoria;
using Sgcf.Domain.Common;
using Sgcf.Domain.Tenancy;

namespace Sgcf.Domain.Cotacoes;

/// <summary>
/// Revisão temporal das garantias exigidas por um <see cref="LimiteBanco"/>.
/// Append-only: cada PATCH na política do banco fecha a revisão vigente e
/// abre uma nova. Itens da revisão tornam-se imutáveis após VigenciaFim ser definida.
/// SPEC §3.3 e §4.1 (SR-01..SR-08).
/// </summary>
public sealed class GarantiaExigidaRevisao : Entity, IAuditable, ITenantScoped
{
    /// <inheritdoc />
    public Guid TenantId { get; private set; }

    /// <summary>FK → limite_banco.id (Cascade).</summary>
    public Guid LimiteBancoId { get; private set; }

    /// <summary>Quando esta revisão entrou em vigor. Definido pelo clock na criação.</summary>
    public Instant VigenciaInicio { get; private set; }

    /// <summary>
    /// Null enquanto a revisão for a vigente. Preenchido quando uma nova revisão a substitui
    /// via <see cref="EncerrarVigencia(Instant)"/>. SR-03.
    /// </summary>
    public Instant? VigenciaFim { get; private set; }

    /// <summary>Timestamp de gravação (= VigenciaInicio na maioria dos casos).</summary>
    public Instant RegistradoEm { get; private set; }

    /// <summary>Texto livre — ex.: "Renegociação 2026-06", "Comitê de risco aprovou redução".</summary>
    public string? Motivo { get; private set; }

    /// <summary>Texto livre adicional (≤ 1024 caracteres).</summary>
    public string? Observacoes { get; private set; }

    /// <inheritdoc />
    public Instant CreatedAt { get; private set; }

    /// <inheritdoc />
    public Instant UpdatedAt { get; private set; }

    private readonly List<GarantiaExigidaItem> _itens = new();

    /// <summary>
    /// Itens desta revisão. Coleção append-only; imutável após <see cref="EncerrarVigencia(Instant)"/>.
    /// </summary>
    public IReadOnlyCollection<GarantiaExigidaItem> Itens => _itens.AsReadOnly();

    /// <summary>True enquanto VigenciaFim for null (revisão não encerrada). SR-03.</summary>
    public bool EstaVigente => VigenciaFim is null;

    /// <summary>Construtor privado para EF Core.</summary>
    private GarantiaExigidaRevisao() { }

    /// <summary>
    /// Cria uma nova revisão com vigência iniciando no instante atual do clock.
    /// SR-01: limiteBancoId não pode ser Guid.Empty.
    /// SR-06: não pode haver dois itens com o mesmo Tipo.
    /// SR-08: lista de itens pode ser vazia.
    /// </summary>
    internal static GarantiaExigidaRevisao Criar(
        Guid limiteBancoId,
        IEnumerable<GarantiaExigidaItemSpec> itens,
        IClock clock,
        string? motivo = null,
        string? observacoes = null)
    {
        var now = clock.GetCurrentInstant();
        return CriarComInstant(limiteBancoId, itens, now, motivo, observacoes);
    }

    /// <summary>
    /// Sobrecarga que aceita um <see cref="Instant"/> explícito. Usada pelo agregado
    /// <see cref="LimiteBanco"/> para garantir que VigenciaFim da revisão anterior e
    /// VigenciaInicio da nova revisão sejam exatamente o mesmo valor (SLB-03).
    /// </summary>
    internal static GarantiaExigidaRevisao CriarComInstant(
        Guid limiteBancoId,
        IEnumerable<GarantiaExigidaItemSpec> itens,
        Instant momento,
        string? motivo = null,
        string? observacoes = null)
    {
        if (limiteBancoId == Guid.Empty)
        {
            // SR-01
            throw new ArgumentException("LimiteBancoId não pode ser vazio.", nameof(limiteBancoId));
        }

        var revisao = new GarantiaExigidaRevisao
        {
            LimiteBancoId = limiteBancoId,
            VigenciaInicio = momento,
            VigenciaFim = null,
            RegistradoEm = momento,
            Motivo = motivo,
            Observacoes = observacoes,
            CreatedAt = momento,
            UpdatedAt = momento,
        };

        foreach (var spec in itens)
        {
            revisao.AdicionarItemInterno(spec, momento);
        }

        revisao.ValidarGrupos();

        return revisao;
    }

    /// <summary>
    /// Valida as invariantes de grupos de alternativas "OU" no nível do agregado:
    /// GA-02 (grupo com ≥ 2 itens) e GA-05 (rótulo consistente entre os itens do grupo).
    /// GA-03 (tipos distintos no grupo) e GA-07 (um tipo em no máximo um grupo) são
    /// consequências diretas de SR-06 (sem Tipo duplicado na revisão) e não exigem
    /// verificação adicional. GA-06 (imutabilidade pós-encerramento) é coberta por SR-05.
    /// </summary>
    private void ValidarGrupos()
    {
        foreach (var grupo in _itens
            .Where(i => i.GrupoAlternativaId.HasValue)
            .GroupBy(i => i.GrupoAlternativaId!.Value))
        {
            // GA-02
            if (grupo.Count() < 2)
            {
                throw new InvalidOperationException(
                    $"Grupo de alternativas {grupo.Key} deve conter ao menos 2 itens (GA-02).");
            }

            // GA-05: no máximo um rótulo não-nulo distinto por grupo.
            var rotulosDistintos = grupo
                .Select(i => i.GrupoRotulo)
                .Where(r => r is not null)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (rotulosDistintos.Count > 1)
            {
                throw new InvalidOperationException(
                    $"Grupo {grupo.Key} tem rótulos inconsistentes (GA-05): {string.Join(" | ", rotulosDistintos)}.");
            }
        }
    }

    /// <summary>
    /// Encerra a vigência desta revisão usando o instante atual do clock.
    /// Conveniente para chamadas diretas. Internamente delega para
    /// <see cref="EncerrarVigencia(Instant)"/>.
    /// </summary>
    internal void EncerrarVigencia(IClock clock) =>
        EncerrarVigencia(clock.GetCurrentInstant());

    /// <summary>
    /// Encerra a vigência usando um <see cref="Instant"/> explícito.
    /// Usado pelo agregado pai para garantir que VigenciaFim e VigenciaInicio
    /// da revisão seguinte sejam o mesmo Instant (SLB-03).
    /// SR-03: não pode ser chamado duas vezes.
    /// SR-04: momento deve ser >= VigenciaInicio.
    /// </summary>
    internal void EncerrarVigencia(Instant momento)
    {
        if (VigenciaFim is not null)
        {
            // SR-03
            throw new InvalidOperationException(
                $"Revisão {Id} já encerrada em {VigenciaFim}. Não é possível encerrar novamente.");
        }

        if (momento < VigenciaInicio)
        {
            // SR-04
            throw new ArgumentException(
                $"O instante de encerramento ({momento}) é anterior a VigenciaInicio ({VigenciaInicio}) — clock invariante violado.",
                nameof(momento));
        }

        VigenciaFim = momento;
        UpdatedAt = momento;
    }

    /// <summary>
    /// Adiciona um item à revisão usando um Instant explícito.
    /// SR-05: lança se a revisão já estiver encerrada.
    /// SR-06: lança se já houver item com o mesmo Tipo.
    /// </summary>
    private void AdicionarItemInterno(GarantiaExigidaItemSpec spec, Instant momento)
    {
        ArgumentNullException.ThrowIfNull(spec);

        if (!EstaVigente)
        {
            // SR-05
            throw new InvalidOperationException(
                $"Não é possível adicionar itens a uma revisão encerrada (Id: {Id}, VigenciaFim: {VigenciaFim}).");
        }

        if (_itens.Any(i => i.Tipo == spec.Tipo))
        {
            // SR-06
            throw new InvalidOperationException(
                $"Garantia exigida do tipo {spec.Tipo} já está cadastrada (duplicada) na revisão {Id}.");
        }

        _itens.Add(GarantiaExigidaItem.Criar(
            revisaoId: Id,
            tipo: spec.Tipo,
            percentualSobreLimite: spec.PercentualSobreLimite,
            valorFixoBrl: spec.ValorFixoBrl,
            obrigatoria: spec.Obrigatoria,
            observacoes: spec.Observacoes,
            momento: momento,
            grupoAlternativaId: spec.GrupoAlternativaId,
            grupoRotulo: spec.GrupoRotulo));
    }
}
