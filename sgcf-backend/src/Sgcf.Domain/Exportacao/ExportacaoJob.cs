using NodaTime;
using Sgcf.Domain.Tenancy;

namespace Sgcf.Domain.Exportacao;

/// <summary>
/// Representa uma solicitação de exportação de dados feita por um usuário.
/// O job é criado com status <see cref="StatusExportacao.Pendente"/> e processado
/// assincronamente pelo <c>ExportacaoProcessorService</c>.
/// O resultado final (JSON) é persistido em <see cref="ResultadoJson"/> quando o job conclui.
/// </summary>
public sealed class ExportacaoJob : ITenantScoped
{
    // Construtor privado para EF Core — não deve ser chamado diretamente.
#pragma warning disable CS8618
    private ExportacaoJob() { }
#pragma warning restore CS8618

    public Guid Id { get; private set; }

    /// <summary>
    /// Preenchido automaticamente pelo <c>TenantSaveInterceptor</c> no INSERT.
    /// NUNCA defina este valor manualmente.
    /// </summary>
    public Guid TenantId { get; private set; }

    public TipoExportacao Tipo { get; private set; }
    public StatusExportacao Status { get; private set; }

    /// <summary>JSON opcional com filtros da exportação (intervalo de datas, bankId, etc.).</summary>
    public string? ParametrosJson { get; private set; }

    /// <summary>Payload JSON gerado após a conclusão bem-sucedida do job.</summary>
    public string? ResultadoJson { get; private set; }

    /// <summary>Mensagem de erro preenchida quando o job falha.</summary>
    public string? MensagemErro { get; private set; }

    public Instant CriadoEm { get; private set; }
    public Instant? IniciadoEm { get; private set; }
    public Instant? ConcluidoEm { get; private set; }

    /// <summary>Sub (identificador) do usuário que solicitou a exportação.</summary>
    public string SolicitadoPor { get; private set; } = string.Empty;

    /// <summary>
    /// Cria um novo job de exportação no estado <see cref="StatusExportacao.Pendente"/>.
    /// </summary>
    /// <param name="tipo">Tipo de dado a exportar.</param>
    /// <param name="parametrosJson">Parâmetros de filtro opcionais, serializados em JSON.</param>
    /// <param name="solicitadoPor">Sub do usuário solicitante (obtido de <c>ICurrentUserService.ActorSub</c>).</param>
    /// <param name="agora">Instante atual obtido via <c>IClock</c>.</param>
    public static ExportacaoJob Criar(
        TipoExportacao tipo,
        string? parametrosJson,
        string solicitadoPor,
        Instant agora)
    {
        return new ExportacaoJob
        {
            Id = Guid.NewGuid(),
            Tipo = tipo,
            Status = StatusExportacao.Pendente,
            ParametrosJson = parametrosJson,
            SolicitadoPor = solicitadoPor,
            CriadoEm = agora
        };
    }

    /// <summary>
    /// Transiciona o job de <see cref="StatusExportacao.Pendente"/> para
    /// <see cref="StatusExportacao.Processando"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Lançada quando o job já foi iniciado (estado não é Pendente).
    /// </exception>
    public void IniciarProcessamento(Instant agora)
    {
        if (Status != StatusExportacao.Pendente)
        {
            throw new InvalidOperationException("Job já foi iniciado.");
        }

        Status = StatusExportacao.Processando;
        IniciadoEm = agora;
    }

    /// <summary>
    /// Transiciona o job para <see cref="StatusExportacao.Concluido"/> e persiste o resultado.
    /// </summary>
    /// <param name="resultadoJson">Payload JSON gerado pelo processador.</param>
    /// <param name="agora">Instante UTC de conclusão.</param>
    public void Concluir(string resultadoJson, Instant agora)
    {
        Status = StatusExportacao.Concluido;
        ResultadoJson = resultadoJson;
        ConcluidoEm = agora;
    }

    /// <summary>
    /// Transiciona o job para <see cref="StatusExportacao.Falhou"/> e registra a mensagem de erro.
    /// </summary>
    /// <param name="mensagemErro">Descrição do erro ocorrido durante o processamento.</param>
    /// <param name="agora">Instante UTC da falha.</param>
    public void Falhar(string mensagemErro, Instant agora)
    {
        Status = StatusExportacao.Falhou;
        MensagemErro = mensagemErro;
        ConcluidoEm = agora;
    }
}
