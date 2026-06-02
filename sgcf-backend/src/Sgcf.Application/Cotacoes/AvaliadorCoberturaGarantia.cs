using Sgcf.Application.Cotacoes.Exceptions;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Cotacoes;

/// <summary>
/// Avalia se os itens obrigatórios de uma revisão de garantias estão cobertos pelas garantias
/// declaradas no ato da conversão cotação→contrato.
///
/// Retorna lista de lacunas (vazia = cobertura completa). Pure static — sem estado, sem I/O.
///
/// Regras por tipo de item (SPEC §4.4 + RV-GA):
/// <list type="bullet">
///   <item>
///     <b>Item não agrupado</b> (<c>GrupoAlternativaId == null</c>): regra individual legada.
///     Aval puro (sem percentual e sem valor fixo): coberto pela mera presença de qualquer Aval
///     declarado. Demais tipos: coberto quando valorCoberto ≥ valorEsperado (via
///     <see cref="CalculadorValorGarantiaExigida"/>).
///   </item>
///   <item>
///     <b>Grupo de alternativas "OU"</b> (itens com mesmo <c>GrupoAlternativaId</c>):
///     cobertura por normalização de frações — Σ(min(coberto_A / alvo_A, 1.0)) ≥ 1.0.
///     Uma única lacuna de grupo é emitida quando a soma for insuficiente.
///   </item>
/// </list>
/// </summary>
internal static class AvaliadorCoberturaGarantia
{
    /// <summary>
    /// Avalia a cobertura dos itens obrigatórios.
    /// </summary>
    /// <param name="itensObrigatorios">
    /// Itens com <c>Obrigatoria = true</c> da revisão vigente do <c>LimiteBanco</c>.
    /// </param>
    /// <param name="valorCobertoPorTipo">
    /// Mapa pré-calculado de tipo → soma BRL das garantias declaradas no comando.
    /// </param>
    /// <param name="valorPrincipalBrl">Valor principal do contrato em BRL.</param>
    /// <returns>Lista de lacunas. Vazia quando todos os itens estão cobertos.</returns>
    internal static List<LacunaGarantia> Avaliar(
        IReadOnlyList<GarantiaExigidaItem> itensObrigatorios,
        IReadOnlyDictionary<TipoGarantia, decimal> valorCobertoPorTipo,
        Money valorPrincipalBrl)
    {
        var lacunas = new List<LacunaGarantia>();

        // Separa itens independentes dos agrupados para processar cada classe por sua regra.
        var itensIndependentes = itensObrigatorios.Where(i => i.GrupoAlternativaId is null).ToList();
        var grupos = itensObrigatorios
            .Where(i => i.GrupoAlternativaId is not null)
            .GroupBy(i => i.GrupoAlternativaId!.Value)
            .ToList();

        // ── Itens não agrupados: regra individual legada (RF-08) ─────────────────
        foreach (GarantiaExigidaItem item in itensIndependentes)
        {
            LacunaGarantia? lacuna = AvaliarItemIndependente(item, valorCobertoPorTipo, valorPrincipalBrl);
            if (lacuna is not null)
            {
                lacunas.Add(lacuna);
            }
        }

        // ── Grupos de alternativas "OU": regra de fração normalizada (RV-GA) ────
        foreach (var grupo in grupos)
        {
            LacunaGarantia? lacuna = AvaliarGrupo(grupo.Key, grupo.ToList(), valorCobertoPorTipo, valorPrincipalBrl);
            if (lacuna is not null)
            {
                lacunas.Add(lacuna);
            }
        }

        return lacunas;
    }

    // ── Helpers privados ─────────────────────────────────────────────────────────

    /// <summary>
    /// Avalia um item independente (sem grupo). Retorna null quando coberto.
    /// Aplica a regra Aval-puro para <c>Aval</c> sem parâmetros monetários;
    /// caso contrário usa <see cref="CalculadorValorGarantiaExigida"/>.
    /// </summary>
    private static LacunaGarantia? AvaliarItemIndependente(
        GarantiaExigidaItem item,
        IReadOnlyDictionary<TipoGarantia, decimal> valorCobertoPorTipo,
        Money valorPrincipalBrl)
    {
        bool ehAvalPuro = item.Tipo == TipoGarantia.Aval
            && !item.PercentualSobreLimite.HasValue
            && !item.ValorFixoBrl.HasValue;

        if (ehAvalPuro)
        {
            // Cobertura satisfeita pela mera presença de qualquer Aval declarado.
            return valorCobertoPorTipo.ContainsKey(TipoGarantia.Aval)
                ? null
                : new LacunaGarantia(
                    Tipo: item.Tipo.ToString(),
                    Obrigatoria: true,
                    ValorEsperadoBrl: null,
                    ValorCobertoBrl: null);
        }

        Money valorEsperado = CalculadorValorGarantiaExigida.Calcular([item], valorPrincipalBrl);
        decimal valorCoberto = valorCobertoPorTipo.GetValueOrDefault(item.Tipo, 0m);

        return valorCoberto < valorEsperado.Valor
            ? new LacunaGarantia(
                Tipo: item.Tipo.ToString(),
                Obrigatoria: true,
                ValorEsperadoBrl: valorEsperado.Valor,
                ValorCobertoBrl: valorCoberto)
            : null;
    }

    /// <summary>
    /// Avalia um grupo "OU" usando normalização de frações (RV-GA):
    /// Σ(min(coberto_A / alvo_A, 1.0)) ≥ 1.0 → coberto.
    ///
    /// Casos limite:
    /// - alvo_A == 0 e coberto_A > 0: fração = 1.0 (alternativa satisfeita por presença).
    /// - alvo_A == 0 e coberto_A == 0: fração = 0.0.
    /// </summary>
    private static LacunaGarantia? AvaliarGrupo(
        Guid grupoId,
        IReadOnlyList<GarantiaExigidaItem> itensDoGrupo,
        IReadOnlyDictionary<TipoGarantia, decimal> valorCobertoPorTipo,
        Money valorPrincipalBrl)
    {
        decimal somaFracoes = 0m;

        foreach (GarantiaExigidaItem alternativa in itensDoGrupo)
        {
            decimal alvo = CalculadorValorGarantiaExigida.Calcular([alternativa], valorPrincipalBrl).Valor;
            decimal coberto = valorCobertoPorTipo.GetValueOrDefault(alternativa.Tipo, 0m);

            decimal fracao;
            if (alvo > 0m)
            {
                fracao = Math.Min(coberto / alvo, 1.0m);
            }
            else
            {
                // Aval-puro dentro de um grupo: presença equivale a fração = 1.0.
                fracao = coberto > 0m ? 1.0m : 0m;
            }

            somaFracoes += fracao;
        }

        if (somaFracoes >= 1.0m)
        {
            return null;
        }

        // Emite UMA lacuna para o grupo inteiro, com fracao coberta arredondada para exibição.
        decimal fracaoCoberta = Math.Round(somaFracoes, 4, MidpointRounding.AwayFromZero);

        // Determina o rótulo do grupo: usa GrupoRotulo se todos os itens concordam,
        // caso contrário constrói "Grupo: Tipo1 OU Tipo2 …".
        string? grupoRotulo = itensDoGrupo
            .Select(i => i.GrupoRotulo)
            .Distinct()
            .SingleOrDefault(); // null quando ausente ou divergente entre itens do grupo

        List<string> tiposDoGrupo = itensDoGrupo.Select(i => i.Tipo.ToString()).ToList();

        string tipoLabel = !string.IsNullOrWhiteSpace(grupoRotulo)
            ? grupoRotulo
            : "Grupo: " + string.Join(" OU ", tiposDoGrupo);

        return new LacunaGarantia(
            Tipo: tipoLabel,
            Obrigatoria: true,
            ValorEsperadoBrl: null,
            ValorCobertoBrl: null,
            GrupoAlternativaId: grupoId,
            GrupoRotulo: grupoRotulo,
            AlternativasAceitas: tiposDoGrupo,
            FracaoCoberta: fracaoCoberta);
    }
}
