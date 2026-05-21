using NodaTime;
using Sgcf.Domain.Common;
using Sgcf.Domain.Tenancy;

namespace Sgcf.Domain.Conformidade;

/// <summary>
/// Registro regulatório associado a um contrato de financiamento. GAP-CKP-17.
/// Rastreia inscrições obrigatórias perante órgãos brasileiros (BACEN, SISCOSERV)
/// para operações de câmbio e serviços internacionais.
/// </summary>
public sealed class RegistroRegulatorio : Entity, ITenantScoped
{
    public Guid TenantId { get; private set; }
    public Guid ContratoId { get; private set; }
    public TipoRegistroRegulatorio Tipo { get; private set; }
    public StatusRegistroRegulatorio Status { get; private set; }

    /// <summary>Número atribuído pelo órgão regulador (BACEN, SISCOSERV) após aprovação.</summary>
    public string? NumeroRegistro { get; private set; }

    /// <summary>Data em que o registro foi protocolado junto ao órgão regulador.</summary>
    public LocalDate? DataRegistro { get; private set; }

    /// <summary>Data de vencimento do registro, quando aplicável.</summary>
    public LocalDate? DataVencimento { get; private set; }

    public string? Observacao { get; private set; }

    public Instant CriadoEm { get; private set; }
    public Instant AtualizadoEm { get; private set; }

    // Construtor sem parâmetros exigido pelo EF Core para materialização.
    private RegistroRegulatorio() { }

    /// <summary>
    /// Cria um novo registro regulatório com status inicial <see cref="StatusRegistroRegulatorio.Pendente"/>.
    /// TenantId é preenchido pela infraestrutura via TenantSaveInterceptor — nunca setar aqui.
    /// </summary>
    public static RegistroRegulatorio Criar(
        Guid contratoId,
        TipoRegistroRegulatorio tipo,
        LocalDate? dataVencimento,
        string? observacao,
        Instant agora)
    {
        if (contratoId == Guid.Empty)
        {
            throw new ArgumentException("ContratoId não pode ser vazio.", nameof(contratoId));
        }

        return new RegistroRegulatorio
        {
            ContratoId = contratoId,
            Tipo = tipo,
            Status = StatusRegistroRegulatorio.Pendente,
            DataVencimento = dataVencimento,
            Observacao = observacao,
            CriadoEm = agora,
            AtualizadoEm = agora,
        };
    }

    /// <summary>
    /// Atualiza data de vencimento e observação do registro.
    /// </summary>
    public void Atualizar(
        LocalDate? dataVencimento,
        string? observacao,
        Instant agora)
    {
        DataVencimento = dataVencimento;
        Observacao = observacao;
        AtualizadoEm = agora;
    }

    /// <summary>
    /// Registra o número oficial atribuído pelo órgão regulador e transiciona para
    /// <see cref="StatusRegistroRegulatorio.Registrado"/>.
    /// </summary>
    public void Registrar(
        string numeroRegistro,
        LocalDate dataRegistro,
        string? observacao,
        Instant agora)
    {
        if (string.IsNullOrWhiteSpace(numeroRegistro))
        {
            throw new ArgumentException("Número de registro não pode ser vazio.", nameof(numeroRegistro));
        }

        NumeroRegistro = numeroRegistro.Trim();
        DataRegistro = dataRegistro;
        Status = StatusRegistroRegulatorio.Registrado;

        if (observacao is not null)
        {
            Observacao = observacao;
        }

        AtualizadoEm = agora;
    }

    /// <summary>
    /// Atualiza o status do registro. Não permite retroagir para Pendente a partir de
    /// Registrado ou Dispensado — transições irreversíveis por integridade regulatória.
    /// </summary>
    public void AtualizarStatus(
        StatusRegistroRegulatorio novoStatus,
        string? observacao,
        Instant agora)
    {
        bool transicaoProibida =
            novoStatus == StatusRegistroRegulatorio.Pendente
            && (Status == StatusRegistroRegulatorio.Registrado
                || Status == StatusRegistroRegulatorio.Dispensado);

        if (transicaoProibida)
        {
            throw new InvalidOperationException(
                $"Não é permitido retornar ao status Pendente a partir de {Status}. " +
                "Registros já consolidados não podem ser revertidos.");
        }

        Status = novoStatus;

        if (observacao is not null)
        {
            Observacao = observacao;
        }

        AtualizadoEm = agora;
    }
}
