namespace Sgcf.Domain.Alertas;

/// <summary>
/// Perfis funcionais que têm visibilidade sobre determinados alertas no cockpit financeiro.
/// Um mesmo alerta pode ser visível para múltiplos perfis simultaneamente.
/// </summary>
public enum PerfilCockpit : byte
{
    Cfo               = 1,
    GerenteFinanceiro = 2,
    Tesouraria        = 3,
    Diretor           = 4,
}
