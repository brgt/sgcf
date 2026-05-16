using NodaTime;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;

namespace Sgcf.Domain.Cambio;

public sealed class ParametroCotacao : Entity
{
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

    public void Atualizar(TipoCotacao tipoCotacao, bool ativo, IClock clock)
    {
        TipoCotacao = tipoCotacao;
        Ativo = ativo;
        UpdatedAt = clock.GetCurrentInstant();
    }
}
