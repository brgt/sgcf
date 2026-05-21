using NodaTime;
using Sgcf.Domain.Common;
using Sgcf.Domain.Tenancy;

namespace Sgcf.Domain.Documentos;

/// <summary>
/// Documento contratual anexado a um contrato de financiamento. GAP-CKP-16.
/// Implementa máquina de estados: Pendente → EmRevisao → Aprovado/Rejeitado.
/// Qualquer estado ativo pode transitar para Expirado.
/// </summary>
public sealed class DocumentoContratual : Entity, ITenantScoped
{
    public Guid TenantId { get; private set; }
    public Guid ContratoId { get; private set; }
    public TipoDocumento Tipo { get; private set; }
    public StatusDocumento Status { get; private set; }

    /// <summary>Nome/título do documento.</summary>
    public string Nome { get; private set; } = default!;

    /// <summary>URL de armazenamento do arquivo (opcional).</summary>
    public string? UrlArmazenamento { get; private set; }

    /// <summary>Data de emissão do documento.</summary>
    public LocalDate? DataEmissao { get; private set; }

    /// <summary>Data de vencimento/expiração do documento.</summary>
    public LocalDate? DataVencimento { get; private set; }

    /// <summary>Observações sobre o documento ou a última alteração de status.</summary>
    public string? Observacao { get; private set; }

    public Instant CriadoEm { get; private set; }
    public Instant AtualizadoEm { get; private set; }

    // Construtor privado para EF Core — não deve ser usado no código de aplicação.
    private DocumentoContratual() { }

    /// <summary>
    /// Cria um novo documento contratual com status inicial <see cref="StatusDocumento.Pendente"/>.
    /// </summary>
    public static DocumentoContratual Criar(
        Guid contratoId,
        TipoDocumento tipo,
        string nome,
        LocalDate? dataEmissao,
        LocalDate? dataVencimento,
        string? urlArmazenamento,
        string? observacao,
        Instant agora)
    {
        if (contratoId == Guid.Empty)
        {
            throw new ArgumentException("ContratoId não pode ser vazio.", nameof(contratoId));
        }

        if (string.IsNullOrWhiteSpace(nome))
        {
            throw new ArgumentException("Nome não pode ser vazio.", nameof(nome));
        }

        return new DocumentoContratual
        {
            ContratoId = contratoId,
            Tipo = tipo,
            Status = StatusDocumento.Pendente,
            Nome = nome.Trim(),
            DataEmissao = dataEmissao,
            DataVencimento = dataVencimento,
            UrlArmazenamento = urlArmazenamento,
            Observacao = observacao,
            CriadoEm = agora,
            AtualizadoEm = agora,
        };
    }

    /// <summary>
    /// Atualiza os campos descritivos do documento sem alterar seu status.
    /// </summary>
    public void Atualizar(
        string nome,
        LocalDate? dataEmissao,
        LocalDate? dataVencimento,
        string? urlArmazenamento,
        string? observacao,
        Instant agora)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            throw new ArgumentException("Nome não pode ser vazio.", nameof(nome));
        }

        Nome = nome.Trim();
        DataEmissao = dataEmissao;
        DataVencimento = dataVencimento;
        UrlArmazenamento = urlArmazenamento;
        Observacao = observacao;
        AtualizadoEm = agora;
    }

    /// <summary>
    /// Avança o status do documento segundo as regras da máquina de estados.
    /// Transições proibidas: de Aprovado, Rejeitado ou Expirado para Pendente ou EmRevisao.
    /// Qualquer estado ativo pode transitar para Expirado.
    /// </summary>
    public void AtualizarStatus(StatusDocumento novoStatus, string? observacao, Instant agora)
    {
        bool estadoFinal = Status is StatusDocumento.Aprovado
            or StatusDocumento.Rejeitado
            or StatusDocumento.Expirado;

        bool tentandoReabrirFluxo = novoStatus is StatusDocumento.Pendente
            or StatusDocumento.EmRevisao;

        if (estadoFinal && tentandoReabrirFluxo)
        {
            throw new InvalidOperationException(
                $"Transição inválida: documento no estado '{Status}' não pode retornar para '{novoStatus}'. " +
                $"Documentos aprovados, rejeitados ou expirados são imutáveis.");
        }

        Status = novoStatus;

        if (observacao is not null)
        {
            Observacao = observacao;
        }

        AtualizadoEm = agora;
    }
}
