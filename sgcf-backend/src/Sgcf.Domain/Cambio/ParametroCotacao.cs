using NodaTime;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Tenancy;

namespace Sgcf.Domain.Cambio;

public sealed class ParametroCotacao : Entity, ITenantScoped
{
    public Guid TenantId { get; private set; }
    public Guid? BancoId { get; private set; }
    public ModalidadeContrato? Modalidade { get; private set; }
    public TipoCotacao TipoCotacao { get; private set; }
    public bool Ativo { get; private set; }
    public Instant CreatedAt { get; private set; }
    public Instant UpdatedAt { get; private set; }

    private ParametroCotacao() { }

    public static ParametroCotacao Criar(
        Guid? bancoId,
        ModalidadeContrato? modalidade,
        TipoCotacao tipoCotacao,
        IClock clock)
    {
        Instant now = clock.GetCurrentInstant();
        return new ParametroCotacao
        {
            BancoId = bancoId,
            Modalidade = modalidade,
            TipoCotacao = tipoCotacao,
            Ativo = true,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>
    /// Cria o parâmetro de cotação padrão para um tenant recém-provisionado.
    /// Usa PTAX D-1 como tipo de cotação default — o mais comum para operações FINIMP.
    /// Sem restrição de banco ou modalidade (regra global do tenant).
    /// TenantId definido explicitamente porque o provisionador opera fora do contexto
    /// de request do tenant alvo (sem TenantSaveInterceptor ativo).
    /// </summary>
    public static ParametroCotacao CriarDefault(Guid tenantId, IClock clock)
    {
        Instant now = clock.GetCurrentInstant();
        return new ParametroCotacao
        {
            TenantId = tenantId,
            BancoId = null,
            Modalidade = null,
            TipoCotacao = TipoCotacao.PtaxD1,
            Ativo = true,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void Atualizar(TipoCotacao tipoCotacao, bool ativo, IClock clock)
    {
        TipoCotacao = tipoCotacao;
        Ativo = ativo;
        UpdatedAt = clock.GetCurrentInstant();
    }
}
