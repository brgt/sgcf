using FluentAssertions;

using NodaTime;

using Sgcf.Application.Eventos;
using Sgcf.Infrastructure.Eventos;

using Xunit;

namespace Sgcf.Application.Tests.Eventos;

/// <summary>
/// Testes unitários para <see cref="InMemoryEventoBus"/>.
/// Verifica o comportamento de fan-out, isolamento de subscriptions e ordenação de eventos.
/// </summary>
[Trait("Category", "Domain")]
public sealed class InMemoryEventoBusTests
{
    private static readonly Instant InstanteFixo = Instant.FromUtc(2026, 5, 21, 12, 0);

    private static EventoSistemaDto CriarEvento(string tipo = "test.evento") =>
        new(
            Tipo: tipo,
            EntidadeTipo: null,
            EntidadeId: null,
            Mensagem: $"Mensagem de {tipo}",
            OcorridoEm: InstanteFixo);

    // ── Caso 1: Broadcast entrega o mesmo evento a dois subscribers ───────────────

    [Fact]
    public void Broadcast_DoisSubscribers_AmbosRecebemOEvento()
    {
        // Arrange
        InMemoryEventoBus bus = new();
        EventoSistemaDto evento = CriarEvento("alerta.criado");

        using EventoBusSubscription sub1 = bus.Subscribe();
        using EventoBusSubscription sub2 = bus.Subscribe();

        // Act
        bus.Broadcast(evento);

        // Assert — ambos os readers devem ter o evento disponível
        sub1.Reader.TryRead(out EventoSistemaDto? recebido1).Should().BeTrue();
        sub2.Reader.TryRead(out EventoSistemaDto? recebido2).Should().BeTrue();

        recebido1.Should().Be(evento);
        recebido2.Should().Be(evento);
    }

    // ── Caso 2: Após Dispose, subscription não recebe novos eventos ───────────────

    [Fact]
    public void Broadcast_AposDisposeDeUmSubscriber_SomenteOOutroRecebe()
    {
        // Arrange
        InMemoryEventoBus bus = new();
        EventoSistemaDto evento = CriarEvento("covenant.violado");

        EventoBusSubscription sub1 = bus.Subscribe();
        using EventoBusSubscription sub2 = bus.Subscribe();

        // Dispõe sub1 antes do broadcast
        sub1.Dispose();

        // Act
        bus.Broadcast(evento);

        // Assert — sub1 (disposto) não deve receber; sub2 (ativo) deve receber
        sub1.Reader.TryRead(out _).Should().BeFalse();
        sub2.Reader.TryRead(out EventoSistemaDto? recebido2).Should().BeTrue();
        recebido2.Should().Be(evento);
    }

    // ── Caso 3: ChannelReader.TryRead retorna o evento publicado ─────────────────

    [Fact]
    public void Broadcast_SubscriberUnico_TryReadRetornaEventoPublicado()
    {
        // Arrange
        InMemoryEventoBus bus = new();
        EventoSistemaDto evento = CriarEvento("heartbeat");

        using EventoBusSubscription sub = bus.Subscribe();

        // Act
        bus.Broadcast(evento);

        // Assert
        bool leu = sub.Reader.TryRead(out EventoSistemaDto? lido);
        leu.Should().BeTrue();
        lido.Should().NotBeNull();
        lido!.Tipo.Should().Be("heartbeat");
        lido.OcorridoEm.Should().Be(InstanteFixo);
    }

    // ── Caso 4: Múltiplos eventos chegam na ordem de publicação ──────────────────

    [Fact]
    public void Broadcast_MultiplosEventos_ChegamNaOrdemDePublicacao()
    {
        // Arrange
        InMemoryEventoBus bus = new();
        EventoSistemaDto evento1 = CriarEvento("evento.primeiro");
        EventoSistemaDto evento2 = CriarEvento("evento.segundo");
        EventoSistemaDto evento3 = CriarEvento("evento.terceiro");

        using EventoBusSubscription sub = bus.Subscribe();

        // Act — publica na ordem 1, 2, 3
        bus.Broadcast(evento1);
        bus.Broadcast(evento2);
        bus.Broadcast(evento3);

        // Assert — leitura deve respeitar FIFO
        sub.Reader.TryRead(out EventoSistemaDto? lido1).Should().BeTrue();
        sub.Reader.TryRead(out EventoSistemaDto? lido2).Should().BeTrue();
        sub.Reader.TryRead(out EventoSistemaDto? lido3).Should().BeTrue();

        lido1!.Tipo.Should().Be("evento.primeiro");
        lido2!.Tipo.Should().Be("evento.segundo");
        lido3!.Tipo.Should().Be("evento.terceiro");

        // Após consumir todos, canal deve estar vazio
        sub.Reader.TryRead(out _).Should().BeFalse();
    }
}
