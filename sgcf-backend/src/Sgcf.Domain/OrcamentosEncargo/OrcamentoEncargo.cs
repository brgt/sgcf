using NodaTime;
using Sgcf.Domain.Common;
using Sgcf.Domain.Tenancy;

namespace Sgcf.Domain.OrcamentosEncargo;

/// <summary>
/// Representa o orçamento mensal de encargos financeiros (juros, IOF, tarifas, etc.).
/// Há no máximo um registro por (tenant_id, ano, mês, tipo_encargo, banco_id, contrato_id)
/// — constraint de unicidade imposta no banco.
///
/// A lógica de upsert (criar ou atualizar) é coordenada pelo handler da camada de aplicação,
/// que consulta a chave composta antes de decidir entre <see cref="Criar"/> e <see cref="Atualizar"/>.
/// </summary>
public sealed class OrcamentoEncargo : Entity, ITenantScoped
{
    public Guid TenantId { get; private set; }

    /// <summary>Ano da competência orçamentária (2000–2100).</summary>
    public int Ano { get; private set; }

    /// <summary>Mês da competência orçamentária (1–12).</summary>
    public int Mes { get; private set; }

    /// <summary>
    /// Tipo de encargo financeiro (ex.: "JUROS", "IOF", "TARIFA_BANCARIA").
    /// Máximo 50 caracteres.
    /// </summary>
    public string TipoEncargo { get; private set; } = string.Empty;

    /// <summary>
    /// Valor decimal armazenado para persistência — não expor diretamente.
    /// Use <see cref="ValorOrcadoBrl"/> para acesso tipado.
    /// </summary>
    internal decimal ValorOrcadoBrlDecimal { get; private set; }

    /// <summary>Valor orçado do encargo em BRL.</summary>
    public Money ValorOrcadoBrl => new(ValorOrcadoBrlDecimal, Moeda.Brl);

    /// <summary>
    /// Identificador do banco ao qual o orçamento está vinculado.
    /// Nulo quando o orçamento não está associado a um banco específico.
    /// </summary>
    public Guid? BancoId { get; private set; }

    /// <summary>
    /// Identificador do contrato ao qual o orçamento está vinculado.
    /// Nulo quando o orçamento não está associado a um contrato específico.
    /// </summary>
    public Guid? ContratoId { get; private set; }

    /// <summary>Observação livre. Máximo 500 caracteres. Opcional.</summary>
    public string? Observacao { get; private set; }

    public Instant CriadoEm { get; private set; }
    public Instant AtualizadoEm { get; private set; }

    private OrcamentoEncargo() { }

    /// <summary>
    /// Cria um novo orçamento de encargo financeiro.
    /// </summary>
    /// <param name="ano">Ano da competência (2000–2100).</param>
    /// <param name="mes">Mês da competência (1–12).</param>
    /// <param name="tipoEncargo">Tipo do encargo; não pode ser vazio ou apenas espaços.</param>
    /// <param name="valorOrcadoBrl">Valor orçado em BRL; deve ser não-negativo.</param>
    /// <param name="bancoId">Banco vinculado. Opcional.</param>
    /// <param name="contratoId">Contrato vinculado. Opcional.</param>
    /// <param name="observacao">Observação livre. Opcional.</param>
    /// <param name="agora">Instante corrente para preenchimento de auditoria.</param>
    public static OrcamentoEncargo Criar(
        int ano,
        int mes,
        string tipoEncargo,
        decimal valorOrcadoBrl,
        Guid? bancoId,
        Guid? contratoId,
        string? observacao,
        Instant agora)
    {
        ValidarCompetencia(ano, mes);
        ValidarTipoEncargo(tipoEncargo);
        ValidarValor(valorOrcadoBrl);

        return new OrcamentoEncargo
        {
            Ano = ano,
            Mes = mes,
            TipoEncargo = tipoEncargo.Trim(),
            ValorOrcadoBrlDecimal = Math.Round(valorOrcadoBrl, 4, MidpointRounding.AwayFromZero),
            BancoId = bancoId,
            ContratoId = contratoId,
            Observacao = observacao,
            CriadoEm = agora,
            AtualizadoEm = agora
        };
    }

    /// <summary>
    /// Atualiza o valor orçado e a observação do encargo (operação de upsert).
    /// </summary>
    /// <param name="novoValor">Novo valor orçado em BRL; deve ser não-negativo.</param>
    /// <param name="observacao">Nova observação. Opcional.</param>
    /// <param name="agora">Instante corrente para atualização de auditoria.</param>
    public void Atualizar(decimal novoValor, string? observacao, Instant agora)
    {
        ValidarValor(novoValor);

        ValorOrcadoBrlDecimal = Math.Round(novoValor, 4, MidpointRounding.AwayFromZero);
        Observacao = observacao;
        AtualizadoEm = agora;
    }

    private static void ValidarCompetencia(int ano, int mes)
    {
        if (mes < 1 || mes > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(mes), mes, "Mês deve estar entre 1 e 12.");
        }

        if (ano < 2000 || ano > 2100)
        {
            throw new ArgumentOutOfRangeException(nameof(ano), ano, "Ano deve estar entre 2000 e 2100.");
        }
    }

    private static void ValidarTipoEncargo(string tipoEncargo)
    {
        if (string.IsNullOrWhiteSpace(tipoEncargo))
        {
            throw new ArgumentException("Tipo de encargo não pode ser vazio.", nameof(tipoEncargo));
        }
    }

    private static void ValidarValor(decimal valor)
    {
        if (valor < 0m)
        {
            throw new ArgumentException("Valor orçado não pode ser negativo.", nameof(valor));
        }
    }
}
