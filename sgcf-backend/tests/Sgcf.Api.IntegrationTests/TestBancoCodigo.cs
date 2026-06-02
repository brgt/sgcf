using System.Threading;

namespace Sgcf.Api.IntegrationTests;

/// <summary>
/// Gera <c>codigoCompe</c> de banco únicos e determinísticos por processo de teste.
///
/// Motivo: <c>codigo_compe</c> tem índice único e os testes de integração compartilham
/// um container PostgreSQL por collection. Gerar o código com <c>Random.Shared.Next</c>
/// numa faixa pequena (ex.: 600–699) causava colisões à medida que os bancos se
/// acumulavam no container compartilhado → violação de unique → 500 intermitente
/// (flaky que atingia um teste aleatório a cada full-run).
///
/// O validador exige apenas <c>Length(3)</c> (3 caracteres quaisquer), então usamos
/// um contador atômico codificado em base 36 (0–9, A–Z): 36³ = 46.656 códigos únicos,
/// muito acima da quantidade de bancos criados num run. Thread-safe via
/// <see cref="Interlocked"/> para tolerar execução paralela de collections.
/// </summary>
internal static class TestBancoCodigo
{
    private const string Alfabeto = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private static int _seq = -1;

    /// <summary>Retorna o próximo <c>codigoCompe</c> único (3 caracteres base-36).</summary>
    public static string Next()
    {
        int n = Interlocked.Increment(ref _seq);
        return string.Create(3, n, static (span, value) =>
        {
            span[0] = Alfabeto[(value / 1296) % 36];
            span[1] = Alfabeto[(value / 36) % 36];
            span[2] = Alfabeto[value % 36];
        });
    }
}
