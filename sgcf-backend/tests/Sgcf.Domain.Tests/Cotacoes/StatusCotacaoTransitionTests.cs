using FluentAssertions;
using NodaTime;
using NSubstitute;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;
using Xunit;

namespace Sgcf.Domain.Tests.Cotacoes;

/// <summary>
/// Table-driven: valida todas as transições válidas e inválidas da máquina de estados.
/// SPEC §4.1.
/// </summary>
public sealed class StatusCotacaoTransitionTests
{
    private static readonly IClock Clock = PropostaFactory.CriarClockFixo();

    // ─── Transições VÁLIDAS (tabela) ─────────────────────────────────────────

    [Theory]
    [InlineData("Rascunho→EmCaptacao")]
    [InlineData("EmCaptacao→Comparada")]
    [InlineData("Comparada→Aceita")]
    [InlineData("Aceita→Convertida")]
    [InlineData("Aceita→Comparada")]
    [InlineData("Rascunho→Recusada")]
    [InlineData("EmCaptacao→Recusada")]
    [InlineData("Comparada→Recusada")]
    public void Transicao_valida_nao_deve_lancar_excecao(string transicao)
    {
        var act = () => ExecutarTransicao(transicao);
        act.Should().NotThrow($"transição '{transicao}' é válida segundo SPEC §4.1");
    }

    // ─── Transições INVÁLIDAS (tabela) ───────────────────────────────────────

    [Theory]
    [InlineData("EmCaptacao→Aceita")]
    [InlineData("Rascunho→Comparada")]
    [InlineData("Rascunho→Aceita")]
    [InlineData("Rascunho→Convertida")]
    [InlineData("Comparada→EmCaptacao")]
    [InlineData("Comparada→Convertida")]
    [InlineData("Convertida→Aceita")]
    [InlineData("Convertida→Recusada")]
    [InlineData("Recusada→Rascunho")]
    [InlineData("Recusada→EmCaptacao")]
    public void Transicao_invalida_deve_lancar_excecao(string transicao)
    {
        var act = () => ExecutarTransicao(transicao);
        act.Should().Throw<InvalidOperationException>(
            $"transição '{transicao}' não é permitida pela máquina de estados SPEC §4.1");
    }

    // ─── Helper de execução ──────────────────────────────────────────────────

    private static void ExecutarTransicao(string transicao)
    {
        switch (transicao)
        {
            case "Rascunho→EmCaptacao":
                CriarRascunho().Enviar(Clock);
                break;

            case "EmCaptacao→Comparada":
                CriarEmCaptacao().EncerrarCaptacao(Clock);
                break;

            case "Comparada→Aceita":
                {
                    var (cotacao, proposta) = CriarComparada();
                    cotacao.AceitarProposta(proposta.Id, "op@emp.com", Clock);
                    break;
                }

            case "Aceita→Convertida":
                {
                    var (cotacao, proposta) = CriarComparada();
                    cotacao.AceitarProposta(proposta.Id, "op@emp.com", Clock);
                    cotacao.ConverterEmContrato(Guid.NewGuid(), Clock);
                    break;
                }

            case "Aceita→Comparada":
                {
                    var (cotacao, proposta) = CriarComparada();
                    cotacao.AceitarProposta(proposta.Id, "op@emp.com", Clock);
                    cotacao.DesfazerAceitacao(Clock);
                    break;
                }

            case "Rascunho→Recusada":
                CriarRascunho().Cancelar("motivo", Clock);
                break;

            case "EmCaptacao→Recusada":
                CriarEmCaptacao().Cancelar("motivo", Clock);
                break;

            case "Comparada→Recusada":
                {
                    var (cotacao, _) = CriarComparada();
                    cotacao.Cancelar("motivo", Clock);
                    break;
                }

            // ── Transições INVÁLIDAS ─────────────────────────────────────────

            case "EmCaptacao→Aceita":
                {
                    var cotacao = CriarEmCaptacao();
                    var proposta = PropostaFactory.CriarProposta(cotacaoId: cotacao.Id);
                    cotacao.RegistrarProposta(proposta);
                    cotacao.AceitarProposta(proposta.Id, "op@emp.com", Clock);
                    break;
                }

            case "Rascunho→Comparada":
                CriarRascunho().EncerrarCaptacao(Clock);
                break;

            case "Rascunho→Aceita":
                {
                    var cotacao = CriarRascunho();
                    cotacao.AceitarProposta(Guid.NewGuid(), "op@emp.com", Clock);
                    break;
                }

            case "Rascunho→Convertida":
                CriarRascunho().ConverterEmContrato(Guid.NewGuid(), Clock);
                break;

            case "Comparada→EmCaptacao":
                {
                    var (cotacao, _) = CriarComparada();
                    cotacao.Enviar(Clock); // Enviar exige Rascunho
                    break;
                }

            case "Comparada→Convertida":
                {
                    var (cotacao, _) = CriarComparada();
                    cotacao.ConverterEmContrato(Guid.NewGuid(), Clock);
                    break;
                }

            case "Convertida→Aceita":
                {
                    var (cotacao, proposta) = CriarComparada();
                    cotacao.AceitarProposta(proposta.Id, "op@emp.com", Clock);
                    cotacao.ConverterEmContrato(Guid.NewGuid(), Clock);
                    cotacao.AceitarProposta(proposta.Id, "op@emp.com", Clock);
                    break;
                }

            case "Convertida→Recusada":
                {
                    var (cotacao, proposta) = CriarComparada();
                    cotacao.AceitarProposta(proposta.Id, "op@emp.com", Clock);
                    cotacao.ConverterEmContrato(Guid.NewGuid(), Clock);
                    cotacao.Cancelar("tarde demais", Clock);
                    break;
                }

            case "Recusada→Rascunho":
                {
                    var cotacao = CriarRascunho();
                    cotacao.Cancelar("cancelado", Clock);
                    cotacao.Enviar(Clock); // deve falhar
                    break;
                }

            case "Recusada→EmCaptacao":
                {
                    var cotacao = CriarRascunho();
                    cotacao.Cancelar("cancelado", Clock);
                    cotacao.Enviar(Clock); // deve falhar
                    break;
                }

            default:
                throw new ArgumentException($"Transição desconhecida no teste: '{transicao}'");
        }
    }

    private static Cotacao CriarRascunho() => PropostaFactory.CriarCotacaoRascunho();

    private static Cotacao CriarEmCaptacao()
    {
        var c = CriarRascunho();
        c.Enviar(Clock);
        return c;
    }

    private static (Cotacao Cotacao, Proposta Proposta) CriarComparada()
    {
        var cotacao = CriarEmCaptacao();
        var proposta = PropostaFactory.CriarProposta(cotacaoId: cotacao.Id);
        cotacao.RegistrarProposta(proposta);
        cotacao.EncerrarCaptacao(Clock);
        return (cotacao, proposta);
    }
}
