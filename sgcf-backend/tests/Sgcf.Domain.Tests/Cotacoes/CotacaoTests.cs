using FluentAssertions;
using NodaTime;
using NSubstitute;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;
using Xunit;

namespace Sgcf.Domain.Tests.Cotacoes;

public sealed class CotacaoTests
{
    private static readonly IClock ClockFixo = PropostaFactory.CriarClockFixo();

    // ─── Factory Criar ───────────────────────────────────────────────────────

    [Fact]
    public void Criar_cotacao_valida_deve_ter_status_Rascunho()
    {
        var cotacao = PropostaFactory.CriarCotacaoRascunho();
        cotacao.Status.Should().Be(StatusCotacao.Rascunho);
    }

    [Fact]
    public void Criar_cotacao_define_CodigoInterno_corretamente()
    {
        var cotacao = PropostaFactory.CriarCotacaoRascunho(codigoInterno: "COT-2026-00042");
        cotacao.CodigoInterno.Should().Be("COT-2026-00042");
    }

    [Fact]
    public void Criar_cotacao_com_ValorAlvo_zero_deve_lancar_excecao()
    {
        var act = () => Cotacao.Criar(
            codigoInterno: "COT-2026-00001",
            modalidade: ModalidadeContrato.Finimp,
            valorAlvoBrl: new Money(0m, Moeda.Brl),
            prazoMaximoDias: 180,
            dataAbertura: new LocalDate(2026, 5, 15),
            dataPtaxReferencia: new LocalDate(2026, 5, 14),
            ptaxUsadaUsdBrl: 5.20m,
            clock: ClockFixo);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*ValorAlvoBRL*zero*");
    }

    [Fact]
    public void Criar_cotacao_com_PrazoMaximo_zero_deve_lancar_excecao()
    {
        var act = () => Cotacao.Criar(
            codigoInterno: "COT-2026-00001",
            modalidade: ModalidadeContrato.Finimp,
            valorAlvoBrl: new Money(1_000_000m, Moeda.Brl),
            prazoMaximoDias: 0,
            dataAbertura: new LocalDate(2026, 5, 15),
            dataPtaxReferencia: new LocalDate(2026, 5, 14),
            ptaxUsadaUsdBrl: 5.20m,
            clock: ClockFixo);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*PrazoMaximoDias*maior ou igual*");
    }

    [Fact]
    public void Criar_cotacao_com_DataPtax_nao_anterior_a_DataAbertura_deve_lancar_excecao()
    {
        var act = () => Cotacao.Criar(
            codigoInterno: "COT-2026-00001",
            modalidade: ModalidadeContrato.Finimp,
            valorAlvoBrl: new Money(1_000_000m, Moeda.Brl),
            prazoMaximoDias: 180,
            dataAbertura: new LocalDate(2026, 5, 15),
            dataPtaxReferencia: new LocalDate(2026, 5, 15), // mesma data — inválido
            ptaxUsadaUsdBrl: 5.20m,
            clock: ClockFixo);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*DataPtaxReferencia*anterior*");
    }

    [Fact]
    public void Criar_cotacao_com_CodigoInterno_vazio_deve_lancar_excecao()
    {
        var act = () => Cotacao.Criar(
            codigoInterno: "",
            modalidade: ModalidadeContrato.Finimp,
            valorAlvoBrl: new Money(1_000_000m, Moeda.Brl),
            prazoMaximoDias: 180,
            dataAbertura: new LocalDate(2026, 5, 15),
            dataPtaxReferencia: new LocalDate(2026, 5, 14),
            ptaxUsadaUsdBrl: 5.20m,
            clock: ClockFixo);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*CodigoInterno*");
    }

    // ─── AdicionarBancoAlvo / RemoverBancoAlvo ───────────────────────────────

    [Fact]
    public void AdicionarBancoAlvo_em_Rascunho_deve_adicionar_banco()
    {
        var cotacao = PropostaFactory.CriarCotacaoRascunho();
        var bancoId = Guid.NewGuid();

        cotacao.AdicionarBancoAlvo(bancoId);

        cotacao.BancosAlvo.Should().Contain(bancoId);
    }

    [Fact]
    public void AdicionarBancoAlvo_duplicado_nao_duplica_lista()
    {
        var cotacao = PropostaFactory.CriarCotacaoRascunho();
        var bancoId = Guid.NewGuid();

        cotacao.AdicionarBancoAlvo(bancoId);
        cotacao.AdicionarBancoAlvo(bancoId);

        cotacao.BancosAlvo.Should().HaveCount(1);
    }

    [Fact]
    public void AdicionarBancoAlvo_em_Comparada_deve_lancar_excecao()
    {
        var cotacao = PropostaFactory.CriarCotacaoRascunho();
        cotacao.Enviar(ClockFixo);
        cotacao.EncerrarCaptacao(ClockFixo);

        var act = () => cotacao.AdicionarBancoAlvo(Guid.NewGuid());

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Comparada*");
    }

    [Fact]
    public void RemoverBancoAlvo_em_Rascunho_deve_remover_banco()
    {
        var cotacao = PropostaFactory.CriarCotacaoRascunho();
        var bancoId = Guid.NewGuid();
        cotacao.AdicionarBancoAlvo(bancoId);

        cotacao.RemoverBancoAlvo(bancoId);

        cotacao.BancosAlvo.Should().NotContain(bancoId);
    }

    // ─── Transição: Rascunho → EmCaptacao ───────────────────────────────────

    [Fact]
    public void Enviar_de_Rascunho_deve_mudar_status_para_EmCaptacao()
    {
        var cotacao = PropostaFactory.CriarCotacaoRascunho();
        cotacao.Enviar(ClockFixo);
        cotacao.Status.Should().Be(StatusCotacao.EmCaptacao);
    }

    [Fact]
    public void Enviar_de_EmCaptacao_deve_lancar_excecao()
    {
        var cotacao = PropostaFactory.CriarCotacaoRascunho();
        cotacao.Enviar(ClockFixo);

        var act = () => cotacao.Enviar(ClockFixo);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Enviar*Rascunho*");
    }

    // ─── Transição: EmCaptacao → Comparada ──────────────────────────────────

    [Fact]
    public void EncerrarCaptacao_de_EmCaptacao_deve_mudar_para_Comparada()
    {
        var cotacao = PropostaFactory.CriarCotacaoRascunho();
        cotacao.Enviar(ClockFixo);
        cotacao.EncerrarCaptacao(ClockFixo);

        cotacao.Status.Should().Be(StatusCotacao.Comparada);
    }

    [Fact]
    public void EncerrarCaptacao_de_Rascunho_deve_lancar_excecao()
    {
        var cotacao = PropostaFactory.CriarCotacaoRascunho();

        var act = () => cotacao.EncerrarCaptacao(ClockFixo);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*EncerrarCaptacao*EmCaptacao*");
    }

    // ─── RegistrarProposta ───────────────────────────────────────────────────

    [Fact]
    public void RegistrarProposta_em_EmCaptacao_deve_adicionar_proposta()
    {
        var cotacao = PropostaFactory.CriarCotacaoRascunho();
        cotacao.Enviar(ClockFixo);

        var proposta = PropostaFactory.CriarProposta(cotacaoId: cotacao.Id);
        cotacao.RegistrarProposta(proposta);

        cotacao.Propostas.Should().HaveCount(1);
    }

    [Fact]
    public void RegistrarProposta_em_Rascunho_deve_lancar_excecao()
    {
        var cotacao = PropostaFactory.CriarCotacaoRascunho();
        var proposta = PropostaFactory.CriarProposta(cotacaoId: cotacao.Id);

        var act = () => cotacao.RegistrarProposta(proposta);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*registrar proposta*");
    }

    [Fact]
    public void RegistrarProposta_com_CotacaoId_diferente_deve_lancar_excecao()
    {
        var cotacao = PropostaFactory.CriarCotacaoRascunho();
        cotacao.Enviar(ClockFixo);
        var proposta = PropostaFactory.CriarProposta(cotacaoId: Guid.NewGuid()); // id errado

        var act = () => cotacao.RegistrarProposta(proposta);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*CotacaoId*");
    }

    // ─── Transição: Comparada → Aceita ───────────────────────────────────────

    [Fact]
    public void AceitarProposta_de_Comparada_deve_mudar_status_para_Aceita()
    {
        var cotacao = CriarCotacaoComPropostaComparada(out var proposta);

        cotacao.AceitarProposta(proposta.Id, "operador@empresa.com", ClockFixo);

        cotacao.Status.Should().Be(StatusCotacao.Aceita);
        cotacao.PropostaAceitaId.Should().Be(proposta.Id);
        cotacao.AceitaPor.Should().Be("operador@empresa.com");
        cotacao.DataAceitacao.Should().NotBeNull();
    }

    [Fact]
    public void AceitarProposta_deve_mudar_status_da_proposta_para_Aceita()
    {
        var cotacao = CriarCotacaoComPropostaComparada(out var proposta);

        cotacao.AceitarProposta(proposta.Id, "operador@empresa.com", ClockFixo);

        proposta.Status.Should().Be(StatusProposta.Aceita);
    }

    [Fact]
    public void AceitarProposta_sem_ActorSub_deve_lancar_excecao()
    {
        var cotacao = CriarCotacaoComPropostaComparada(out var proposta);

        var act = () => cotacao.AceitarProposta(proposta.Id, "", ClockFixo);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*ActorSub*obrigatório*");
    }

    [Fact]
    public void AceitarProposta_com_id_inexistente_deve_lancar_excecao()
    {
        var cotacao = CriarCotacaoComPropostaComparada(out _);

        var act = () => cotacao.AceitarProposta(Guid.NewGuid(), "operador@empresa.com", ClockFixo);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*não encontrada*");
    }

    // ─── Transição: Aceita → Comparada (DesfazerAceitacao) ──────────────────

    [Fact]
    public void DesfazerAceitacao_deve_voltar_para_Comparada()
    {
        var cotacao = CriarCotacaoComPropostaAceita(out _);

        cotacao.DesfazerAceitacao(ClockFixo);

        cotacao.Status.Should().Be(StatusCotacao.Comparada);
        cotacao.PropostaAceitaId.Should().BeNull();
        cotacao.AceitaPor.Should().BeNull();
    }

    [Fact]
    public void DesfazerAceitacao_deve_reverter_status_da_proposta()
    {
        var cotacao = CriarCotacaoComPropostaAceita(out var proposta);

        cotacao.DesfazerAceitacao(ClockFixo);

        proposta.Status.Should().Be(StatusProposta.Recebida);
    }

    [Fact]
    public void DesfazerAceitacao_de_Comparada_deve_lancar_excecao()
    {
        var cotacao = CriarCotacaoComPropostaComparada(out _);

        var act = () => cotacao.DesfazerAceitacao(ClockFixo);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*DesfazerAceitacao*Aceita*");
    }

    // ─── Transição: Aceita → Convertida ─────────────────────────────────────

    [Fact]
    public void ConverterEmContrato_deve_mudar_para_Convertida()
    {
        var cotacao = CriarCotacaoComPropostaAceita(out _);
        var contratoId = Guid.NewGuid();

        cotacao.ConverterEmContrato(contratoId, ClockFixo);

        cotacao.Status.Should().Be(StatusCotacao.Convertida);
        cotacao.ContratoGeradoId.Should().Be(contratoId);
    }

    [Fact]
    public void ConverterEmContrato_de_Comparada_deve_lancar_excecao()
    {
        var cotacao = CriarCotacaoComPropostaComparada(out _);

        var act = () => cotacao.ConverterEmContrato(Guid.NewGuid(), ClockFixo);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*ConverterEmContrato*Aceita*");
    }

    // ─── Transição: → Recusada (Cancelar) ───────────────────────────────────

    [Fact]
    public void Cancelar_de_Rascunho_deve_mudar_para_Recusada()
    {
        var cotacao = PropostaFactory.CriarCotacaoRascunho();
        cotacao.Cancelar("Captação desnecessária", ClockFixo);
        cotacao.Status.Should().Be(StatusCotacao.Recusada);
    }

    [Fact]
    public void Cancelar_de_EmCaptacao_deve_mudar_para_Recusada()
    {
        var cotacao = PropostaFactory.CriarCotacaoRascunho();
        cotacao.Enviar(ClockFixo);
        cotacao.Cancelar("Cancelado pelo operador", ClockFixo);
        cotacao.Status.Should().Be(StatusCotacao.Recusada);
    }

    [Fact]
    public void Cancelar_de_Comparada_deve_mudar_para_Recusada()
    {
        var cotacao = PropostaFactory.CriarCotacaoRascunho();
        cotacao.Enviar(ClockFixo);
        cotacao.EncerrarCaptacao(ClockFixo);
        cotacao.Cancelar("Todas propostas insatisfatórias", ClockFixo);
        cotacao.Status.Should().Be(StatusCotacao.Recusada);
    }

    [Fact]
    public void Cancelar_de_Convertida_deve_lancar_excecao()
    {
        var cotacao = CriarCotacaoComPropostaAceita(out _);
        cotacao.ConverterEmContrato(Guid.NewGuid(), ClockFixo);

        var act = () => cotacao.Cancelar("tarde demais", ClockFixo);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Convertida*final*");
    }

    [Fact]
    public void Cancelar_de_Recusada_deve_lancar_excecao()
    {
        var cotacao = PropostaFactory.CriarCotacaoRascunho();
        cotacao.Cancelar("primeiro cancelamento", ClockFixo);

        var act = () => cotacao.Cancelar("segundo cancelamento", ClockFixo);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Recusada*final*");
    }

    // ─── RefreshSnapshotMercado ──────────────────────────────────────────────

    [Fact]
    public void RefreshSnapshotMercado_em_EmCaptacao_deve_atualizar_PTAX_e_invalidar_cache()
    {
        var cotacao = PropostaFactory.CriarCotacaoRascunho(ptax: 5.20m);
        cotacao.Enviar(ClockFixo);

        var proposta = PropostaFactory.CriarProposta(cotacaoId: cotacao.Id);
        proposta.AtualizarCacheCalculos(6.0m, new Money(1_000_000m, Moeda.Brl));
        cotacao.RegistrarProposta(proposta);

        cotacao.RefreshSnapshotMercado(5.35m, ClockFixo);

        cotacao.PtaxUsadaUsdBrl.Should().Be(5.35m);
        proposta.CetCalculadoAaPercentual.Should().BeNull(); // cache invalidado
    }

    [Fact]
    public void RefreshSnapshotMercado_em_Rascunho_deve_lancar_excecao()
    {
        var cotacao = PropostaFactory.CriarCotacaoRascunho();

        var act = () => cotacao.RefreshSnapshotMercado(5.35m, ClockFixo);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*EmCaptacao*Comparada*");
    }

    // ─── Soft delete ─────────────────────────────────────────────────────────

    [Fact]
    public void Deletar_em_Rascunho_deve_preencher_DeletedAt()
    {
        var cotacao = PropostaFactory.CriarCotacaoRascunho();
        cotacao.Deletar(ClockFixo);
        cotacao.DeletedAt.Should().NotBeNull();
    }

    [Fact]
    public void Deletar_em_EmCaptacao_deve_lancar_excecao()
    {
        var cotacao = PropostaFactory.CriarCotacaoRascunho();
        cotacao.Enviar(ClockFixo);

        var act = () => cotacao.Deletar(ClockFixo);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Rascunho*excluídas*");
    }

    // ─── Helpers de arrange ──────────────────────────────────────────────────

    private static Cotacao CriarCotacaoComPropostaComparada(out Proposta proposta)
    {
        var cotacao = PropostaFactory.CriarCotacaoRascunho();
        cotacao.Enviar(ClockFixo);

        proposta = PropostaFactory.CriarProposta(cotacaoId: cotacao.Id);
        cotacao.RegistrarProposta(proposta);
        cotacao.EncerrarCaptacao(ClockFixo);

        return cotacao;
    }

    private static Cotacao CriarCotacaoComPropostaAceita(out Proposta proposta)
    {
        var cotacao = CriarCotacaoComPropostaComparada(out proposta);
        cotacao.AceitarProposta(proposta.Id, "operador@empresa.com", ClockFixo);
        return cotacao;
    }
}
