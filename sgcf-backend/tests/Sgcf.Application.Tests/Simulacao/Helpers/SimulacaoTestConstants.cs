namespace Sgcf.Application.Tests.Simulacao.Helpers;

/// <summary>
/// Constantes compartilhadas pelos testes do módulo Simulação.
/// Elimina magic numbers e strings espalhados nos arquivos de teste,
/// tornando a intenção de cada valor explícita e a manutenção centralizada.
/// </summary>
internal static class SimulacaoTestConstants
{
    /// <summary>Ano base padrão usado nos cenários de teste.</summary>
    internal const int AnoBaseDefault = 2026;

    /// <summary>Número de meses em um ano — usado em <c>Enumerable.Range</c> e contagem de parcelas.</summary>
    internal const int MesesNoAno = 12;

    /// <summary>
    /// Subject identifier de usuário autenticado para testes que precisam de um actor real.
    /// Use <c>AuditConstants.SystemActor</c> quando o caso de teste é o path sem autenticação.
    /// </summary>
    internal const string UserSubDefault = "test-user-default";

    /// <summary>Nome de cenário genérico para testes que não se importam com o nome em si.</summary>
    internal const string NomeCenarioDefault = "Cenário de Teste";

    /// <summary>
    /// Guid semente para cenários — valor determinístico que torna logs de teste legíveis.
    /// Use <c>Guid.NewGuid()</c> quando o teste precisa de IDs únicos entre si.
    /// </summary>
    internal static readonly Guid CenarioIdSeed = new("11111111-0000-0000-0000-000000000001");

    /// <summary>
    /// Guid semente para simulações de contratação — complementar a <see cref="CenarioIdSeed"/>.
    /// </summary>
    internal static readonly Guid SimulacaoIdSeed = new("22222222-0000-0000-0000-000000000002");

    /// <summary>Valor principal de R$ 1.000.000 — referência de escala para testes numéricos.</summary>
    internal const decimal ValorPrincipalDefault = 1_000_000m;

    /// <summary>Taxa de juros anual padrão de 6% a.a. — valor redondo para facilitar assertions.</summary>
    internal const decimal TaxaAaDefault = 6m;
}
