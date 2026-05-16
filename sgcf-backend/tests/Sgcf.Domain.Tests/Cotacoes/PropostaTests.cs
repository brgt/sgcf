using FluentAssertions;
using NodaTime;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;
using Xunit;

namespace Sgcf.Domain.Tests.Cotacoes;

public sealed class PropostaTests
{
    // ─── Criação válida ──────────────────────────────────────────────────────

    [Fact]
    public void Criar_proposta_valida_deve_ter_status_Recebida()
    {
        var proposta = PropostaFactory.CriarProposta();
        proposta.Status.Should().Be(StatusProposta.Recebida);
    }

    [Fact]
    public void Criar_proposta_valida_deve_ter_cache_CET_nulo()
    {
        var proposta = PropostaFactory.CriarProposta();
        proposta.CetCalculadoAaPercentual.Should().BeNull();
    }

    [Fact]
    public void Criar_proposta_sem_NDF_deve_ter_custo_NDF_nulo()
    {
        var proposta = PropostaFactory.CriarProposta(exigeNdf: false);
        proposta.CustoNdfAaPercentual.Should().BeNull();
    }

    // ─── Invariante: ExigeNdf ⇒ CustoNdf obrigatório ────────────────────────

    [Fact]
    public void Criar_proposta_com_ExigeNdf_sem_custo_deve_lancar_excecao()
    {
        var act = () => PropostaFactory.CriarProposta(
            exigeNdf: true,
            custoNdfAaPercentual: null);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*CustoNdfAaPercentual*obrigatório*ExigeNdf*");
    }

    [Fact]
    public void Criar_proposta_com_ExigeNdf_e_custo_informado_deve_criar_com_sucesso()
    {
        var proposta = PropostaFactory.CriarProposta(
            exigeNdf: true,
            custoNdfAaPercentual: 1.5m);

        proposta.ExigeNdf.Should().BeTrue();
        proposta.CustoNdfAaPercentual.Should().Be(1.5m);
    }

    // ─── Invariante: GarantiaEhCdbCativo ⇒ RendimentoCdb obrigatório ────────

    [Fact]
    public void Criar_proposta_com_GarantiaCdb_sem_rendimento_deve_lancar_excecao()
    {
        var act = () => PropostaFactory.CriarProposta(
            garantiaEhCdbCativo: true,
            rendimentoCdbAaPercentual: null);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*RendimentoCdbAaPercentual*obrigatório*GarantiaEhCdbCativo*");
    }

    [Fact]
    public void Criar_proposta_com_GarantiaCdb_e_rendimento_informado_deve_criar_com_sucesso()
    {
        var proposta = PropostaFactory.CriarProposta(
            garantiaEhCdbCativo: true,
            rendimentoCdbAaPercentual: 10.5m);

        proposta.GarantiaEhCdbCativo.Should().BeTrue();
        proposta.RendimentoCdbAaPercentual.Should().Be(10.5m);
    }

    // ─── Invariante: taxa não pode ser negativa ──────────────────────────────

    [Fact]
    public void Criar_proposta_com_taxa_negativa_deve_lancar_excecao()
    {
        var act = () => PropostaFactory.CriarProposta(taxaAaPercentual: -1m);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*TaxaAaPercentual*negativa*");
    }

    [Fact]
    public void Criar_proposta_com_taxa_zero_deve_ser_permitido()
    {
        var proposta = PropostaFactory.CriarProposta(taxaAaPercentual: 0m);
        proposta.TaxaAaPercentual.Should().Be(0m);
    }

    // ─── Invariante: prazo ≥ 1 ──────────────────────────────────────────────

    [Fact]
    public void Criar_proposta_com_prazo_zero_deve_lancar_excecao()
    {
        var act = () => PropostaFactory.CriarProposta(prazoDias: 0);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*PrazoDias*maior ou igual*");
    }

    [Fact]
    public void Criar_proposta_com_prazo_negativo_deve_lancar_excecao()
    {
        var act = () => PropostaFactory.CriarProposta(prazoDias: -10);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // ─── Cache de cálculos ───────────────────────────────────────────────────

    [Fact]
    public void AtualizarCacheCalculos_deve_preencher_CET_e_valor_total()
    {
        var proposta = PropostaFactory.CriarProposta();
        var valorTotal = new Money(1_050_000m, Moeda.Brl);

        proposta.AtualizarCacheCalculos(cetAaPercentual: 7.5m, valorTotalEstimadoBrl: valorTotal);

        proposta.CetCalculadoAaPercentual.Should().Be(7.5m);
        proposta.ValorTotalEstimadoBrl.Should().NotBeNull();
        proposta.ValorTotalEstimadoBrl!.Value.Valor.Should().Be(1_050_000m);
    }

    [Fact]
    public void InvalidarCacheCalculos_deve_limpar_CET_e_valor_total()
    {
        var proposta = PropostaFactory.CriarProposta();
        proposta.AtualizarCacheCalculos(7.5m, new Money(1_050_000m, Moeda.Brl));

        proposta.InvalidarCacheCalculos();

        proposta.CetCalculadoAaPercentual.Should().BeNull();
        proposta.ValorTotalEstimadoBrl.Should().BeNull();
    }

    // ─── Transições de estado ────────────────────────────────────────────────

    [Fact]
    public void Aceitar_deve_mudar_status_para_Aceita()
    {
        var proposta = PropostaFactory.CriarProposta();
        proposta.Aceitar();
        proposta.Status.Should().Be(StatusProposta.Aceita);
    }

    [Fact]
    public void ReverterAceitacao_deve_voltar_para_Recebida()
    {
        var proposta = PropostaFactory.CriarProposta();
        proposta.Aceitar();
        proposta.ReverterAceitacao();
        proposta.Status.Should().Be(StatusProposta.Recebida);
    }

    [Fact]
    public void Recusar_deve_mudar_status_e_registrar_motivo()
    {
        var proposta = PropostaFactory.CriarProposta();
        proposta.Recusar("Taxa fora do mercado");
        proposta.Status.Should().Be(StatusProposta.Recusada);
        proposta.MotivoRecusa.Should().Be("Taxa fora do mercado");
    }

    [Fact]
    public void Expirar_deve_mudar_status_para_Expirada()
    {
        var proposta = PropostaFactory.CriarProposta();
        proposta.Expirar();
        proposta.Status.Should().Be(StatusProposta.Expirada);
    }
}
