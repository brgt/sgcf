namespace Sgcf.Domain.Alertas;

/// <summary>
/// Linha da tabela de join <c>alerta_perfil_visivel</c>.
/// Representa a associação entre um <see cref="Alerta"/> e um <see cref="PerfilCockpit"/>
/// que tem visibilidade sobre ele.
/// </summary>
public sealed class AlertaPerfilVisivel
{
    public Guid AlertaId { get; private set; }
    public PerfilCockpit Perfil { get; private set; }

    // Construtor privado para EF Core.
    private AlertaPerfilVisivel() { }

    internal AlertaPerfilVisivel(Guid alertaId, PerfilCockpit perfil)
    {
        AlertaId = alertaId;
        Perfil = perfil;
    }
}
