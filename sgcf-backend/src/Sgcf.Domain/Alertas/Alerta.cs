using NodaTime;
using Sgcf.Domain.Common;
using Sgcf.Domain.Tenancy;

namespace Sgcf.Domain.Alertas;

/// <summary>
/// Agregado unificado de alertas do cockpit financeiro.
/// Representa qualquer notificação de ação/atenção gerada pelo sistema
/// — vencimentos, limites de banco, hedge, liquidez, documentos, etc.
/// Idempotência garantida por <see cref="ChaveIdempotencia"/> (unique constraint no banco).
/// </summary>
public sealed class Alerta : Entity, ITenantScoped
{
    // Backing field para a coleção de perfis, exposta como IReadOnlyList.
    private readonly List<AlertaPerfilVisivel> _perfisVisiveis = [];

    public Guid TenantId { get; private set; }
    public CategoriaAlerta Categoria { get; private set; }
    public SeveridadeAlerta Severidade { get; private set; }

    /// <summary>Título curto exibido no card do cockpit. Máx. 200 caracteres.</summary>
    public string Titulo { get; private set; } = default!;

    /// <summary>Texto explicativo completo. Máx. 1000 caracteres.</summary>
    public string Descricao { get; private set; } = default!;

    /// <summary>Nome do tipo de entidade de origem, ex.: "Contrato", "Cotacao".</summary>
    public string OrigemTipo { get; private set; } = default!;

    /// <summary>Id da entidade de origem, quando aplicável.</summary>
    public Guid? OrigemId { get; private set; }

    /// <summary>Label do botão de ação rápida no cockpit, ex.: "Ver contrato".</summary>
    public string? AcaoRotulo { get; private set; }

    /// <summary>Rota SPA de destino ao clicar na ação, ex.: "/contratos/{id}".</summary>
    public string? AcaoRota { get; private set; }

    /// <summary>Perfis que enxergam este alerta no cockpit.</summary>
    public IReadOnlyList<AlertaPerfilVisivel> PerfisVisiveis => _perfisVisiveis;

    public StatusAlerta Status { get; private set; }
    public Instant CriadoEm { get; private set; }
    public Instant? ExpiraEm { get; private set; }

    /// <summary>
    /// Chave de negócio usada para garantir idempotência na criação do alerta.
    /// Único por tenant. Máx. 200 caracteres.
    /// </summary>
    public string ChaveIdempotencia { get; private set; } = default!;

    // Construtor privado para EF Core.
    private Alerta() { }

    /// <summary>
    /// Cria um novo alerta com status <see cref="StatusAlerta.Aberto"/>.
    /// O <see cref="TenantId"/> será preenchido automaticamente pelo <c>TenantSaveInterceptor</c>.
    /// </summary>
    /// <param name="categoria">Domínio funcional do alerta.</param>
    /// <param name="severidade">Urgência do alerta.</param>
    /// <param name="titulo">Título exibido no card (máx. 200 chars).</param>
    /// <param name="descricao">Descrição completa (máx. 1000 chars).</param>
    /// <param name="origemTipo">Tipo de entidade que originou o alerta, ex.: "Contrato".</param>
    /// <param name="origemId">Id da entidade de origem, se houver.</param>
    /// <param name="perfisVisiveis">Perfis que verão este alerta no cockpit.</param>
    /// <param name="chaveIdempotencia">Chave única para evitar duplicatas (máx. 200 chars).</param>
    /// <param name="clock">Relógio injetado — nunca DateTime.Now.</param>
    /// <param name="acaoRotulo">Label do botão de ação rápida (opcional).</param>
    /// <param name="acaoRota">Rota SPA de destino (opcional).</param>
    /// <param name="expiraEm">Instante em que o alerta deixa de ser relevante (opcional).</param>
    public static Alerta Criar(
        CategoriaAlerta categoria,
        SeveridadeAlerta severidade,
        string titulo,
        string descricao,
        string origemTipo,
        Guid? origemId,
        IEnumerable<PerfilCockpit> perfisVisiveis,
        string chaveIdempotencia,
        IClock clock,
        string? acaoRotulo = null,
        string? acaoRota = null,
        Instant? expiraEm = null)
    {
        if (string.IsNullOrWhiteSpace(titulo))
        {
            throw new ArgumentException("Título obrigatório.", nameof(titulo));
        }

        if (titulo.Length > 200)
        {
            throw new ArgumentException("Título não pode exceder 200 caracteres.", nameof(titulo));
        }

        if (string.IsNullOrWhiteSpace(descricao))
        {
            throw new ArgumentException("Descrição obrigatória.", nameof(descricao));
        }

        if (descricao.Length > 1000)
        {
            throw new ArgumentException("Descrição não pode exceder 1000 caracteres.", nameof(descricao));
        }

        if (string.IsNullOrWhiteSpace(origemTipo))
        {
            throw new ArgumentException("OrigemTipo obrigatório.", nameof(origemTipo));
        }

        if (string.IsNullOrWhiteSpace(chaveIdempotencia))
        {
            throw new ArgumentException("ChaveIdempotencia obrigatória.", nameof(chaveIdempotencia));
        }

        if (chaveIdempotencia.Length > 200)
        {
            throw new ArgumentException("ChaveIdempotencia não pode exceder 200 caracteres.", nameof(chaveIdempotencia));
        }

        // Entity() chama Guid.CreateVersion7() internamente — alerta.Id é válido logo após a criação.
        Alerta alerta = new()
        {
            Categoria = categoria,
            Severidade = severidade,
            Titulo = titulo.Trim(),
            Descricao = descricao.Trim(),
            OrigemTipo = origemTipo.Trim(),
            OrigemId = origemId,
            AcaoRotulo = acaoRotulo?.Trim(),
            AcaoRota = acaoRota?.Trim(),
            Status = StatusAlerta.Aberto,
            CriadoEm = clock.GetCurrentInstant(),
            ExpiraEm = expiraEm,
            ChaveIdempotencia = chaveIdempotencia.Trim(),
        };

        foreach (PerfilCockpit perfil in perfisVisiveis)
        {
            alerta._perfisVisiveis.Add(new AlertaPerfilVisivel(alerta.Id, perfil));
        }

        return alerta;
    }

    /// <summary>
    /// Marca o alerta como lido. Idempotente se já estava lido.
    /// Lança <see cref="InvalidOperationException"/> se o alerta estiver dispensado.
    /// </summary>
    public void MarcarComoLido(IClock clock)
    {
        if (Status == StatusAlerta.Dispensado)
        {
            throw new InvalidOperationException("Alerta dispensado não pode ser marcado como lido.");
        }

        if (Status == StatusAlerta.Lido)
        {
            return;
        }

        Status = StatusAlerta.Lido;
    }

    /// <summary>
    /// Dispensa o alerta, removendo-o da fila ativa. Idempotente se já estava dispensado.
    /// </summary>
    public void Dispensar(IClock clock)
    {
        if (Status == StatusAlerta.Dispensado)
        {
            return;
        }

        Status = StatusAlerta.Dispensado;
    }
}
