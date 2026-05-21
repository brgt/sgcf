using FluentAssertions;
using NodaTime;
using NSubstitute;
using Sgcf.Domain.Common;
using Sgcf.Domain.Tesouraria;
using Xunit;

namespace Sgcf.Domain.Tests.Tesouraria;

[Trait("Category", "Domain")]
public sealed class EventoFluxoCaixaTests
{
    private static IClock CriarClock(Instant instant)
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(instant);
        return clock;
    }

    private static readonly Instant InstanteBase = Instant.FromUtc(2026, 5, 21, 10, 0);
    private static readonly LocalDate DataBase = new(2026, 5, 21);

    [Fact]
    public void Criar_ComDadosValidos_DefinePropriedadesCorretas()
    {
        IClock clock = CriarClock(InstanteBase);
        Money valor = new(1500m, Moeda.Brl);

        EventoFluxoCaixa evento = EventoFluxoCaixa.Criar(
            DataBase,
            TipoEventoFluxo.Entrada,
            valor,
            "Recebimento de cliente",
            "operador@empresa.com",
            clock);

        evento.Id.Should().NotBeEmpty();
        evento.Data.Should().Be(DataBase);
        evento.Tipo.Should().Be(TipoEventoFluxo.Entrada);
        evento.Valor.Should().Be(valor);
        evento.Descricao.Should().Be("Recebimento de cliente");
        evento.RegistradoPor.Should().Be("operador@empresa.com");
        evento.RegistradoEm.Should().Be(InstanteBase);
        // TenantId é preenchido pelo TenantSaveInterceptor — fica Guid.Empty no domínio puro.
        evento.TenantId.Should().Be(Guid.Empty);
    }

    [Fact]
    public void Criar_ValorZero_LancaArgumentException()
    {
        IClock clock = CriarClock(InstanteBase);
        Money valorZero = new(0m, Moeda.Brl);

        Action act = () => EventoFluxoCaixa.Criar(
            DataBase,
            TipoEventoFluxo.Saida,
            valorZero,
            "Pagamento fornecedor",
            "operador",
            clock);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*valor*");
    }
}
