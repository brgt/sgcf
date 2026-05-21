using NodaTime;
using Sgcf.Domain.Alertas;

namespace Sgcf.Application.Alertas.Dtos;

/// <summary>
/// DTO de saída para o agregado <see cref="Alerta"/>.
/// Expõe todos os campos relevantes para o cockpit financeiro.
/// </summary>
public sealed record AlertaDto(
    Guid Id,
    CategoriaAlerta Categoria,
    SeveridadeAlerta Severidade,
    string Titulo,
    string Descricao,
    string OrigemTipo,
    Guid? OrigemId,
    string? AcaoRotulo,
    string? AcaoRota,
    IReadOnlyList<PerfilCockpit> PerfisVisiveis,
    StatusAlerta Status,
    Instant CriadoEm,
    Instant? ExpiraEm)
{
    /// <summary>
    /// Mapeia um agregado <see cref="Alerta"/> para o DTO de saída.
    /// Mantém o mapeamento centralizado no DTO — sem AutoMapper.
    /// </summary>
    public static AlertaDto From(Alerta alerta) => new(
        Id:             alerta.Id,
        Categoria:      alerta.Categoria,
        Severidade:     alerta.Severidade,
        Titulo:         alerta.Titulo,
        Descricao:      alerta.Descricao,
        OrigemTipo:     alerta.OrigemTipo,
        OrigemId:       alerta.OrigemId,
        AcaoRotulo:     alerta.AcaoRotulo,
        AcaoRota:       alerta.AcaoRota,
        PerfisVisiveis: alerta.PerfisVisiveis.Select(p => p.Perfil).ToList().AsReadOnly(),
        Status:         alerta.Status,
        CriadoEm:       alerta.CriadoEm,
        ExpiraEm:       alerta.ExpiraEm);
}
