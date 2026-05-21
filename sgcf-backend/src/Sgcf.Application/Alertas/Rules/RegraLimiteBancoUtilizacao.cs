using NodaTime;
using Sgcf.Application.Cotacoes;
using Sgcf.Domain.Alertas;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Alertas.Rules;

/// <summary>
/// Gera alertas quando o percentual de utilização de um <see cref="LimiteBanco"/>
/// supera 85% (Atenção) ou 95% (Crítico) do valor limite.
///
/// Chave idempotente diária para detectar agravamento de utilização ao longo do tempo.
/// </summary>
public sealed class RegraLimiteBancoUtilizacao(
    ILimiteBancoRepository limiteBancoRepo,
    IAlertaRepository alertaRepo,
    IClock clock) : IAlertaRule
{
    /// <inheritdoc />
    public string Nome => "limite-banco";

    private const decimal LimiarAtencao = 0.85m;
    private const decimal LimiarCritico  = 0.95m;

    private static readonly PerfilCockpit[] PerfisVisiveis =
        [PerfilCockpit.GerenteFinanceiro, PerfilCockpit.Cfo];

    /// <inheritdoc />
    public async Task AvaliarAsync(LocalDate hoje, CancellationToken ct)
    {
        // Lista todos os limites do tenant corrente (o global query filter aplica tenant automaticamente).
        IReadOnlyList<LimiteBanco> limites = await limiteBancoRepo.ListAsync(
            bancoId: null,
            modalidade: null,
            cancellationToken: ct);

        foreach (LimiteBanco limite in limites)
        {
            // Evita divisão por zero — limite com valor zero não deveria existir (invariante de domínio),
            // mas defensivamente ignoramos.
            if (limite.ValorLimiteBrl.Valor <= 0m)
            {
                continue;
            }

            decimal percentual = Math.Round(
                limite.ValorUtilizadoBrl.Valor / limite.ValorLimiteBrl.Valor,
                6,
                MidpointRounding.AwayFromZero);

            if (percentual < LimiarAtencao)
            {
                continue;
            }

            SeveridadeAlerta severidade = percentual >= LimiarCritico
                ? SeveridadeAlerta.Critico
                : SeveridadeAlerta.Atencao;

            string percentualFormatado = (percentual * 100m).ToString("N1", System.Globalization.CultureInfo.InvariantCulture);
            string chave = $"{Nome}:{limite.Id}:{hoje:yyyy-MM-dd}";

            string titulo = $"Limite de banco {percentualFormatado}% utilizado";
            string descricao = $"Limite {limite.Id:D} (banco {limite.BancoId:D}, " +
                $"modalidade {limite.Modalidade}) está com {percentualFormatado}% de utilização " +
                $"(BRL {limite.ValorUtilizadoBrl.Valor:N2} de {limite.ValorLimiteBrl.Valor:N2}).";

            Alerta alerta = Alerta.Criar(
                categoria: CategoriaAlerta.LimiteBanco,
                severidade: severidade,
                titulo: titulo,
                descricao: descricao,
                origemTipo: "LimiteBanco",
                origemId: limite.Id,
                perfisVisiveis: PerfisVisiveis,
                chaveIdempotencia: chave,
                clock: clock,
                acaoRotulo: "Ver limites",
                acaoRota: $"/limites/{limite.Id}");

            await alertaRepo.TryAddIdempotentAsync(alerta, ct);
        }
    }
}
