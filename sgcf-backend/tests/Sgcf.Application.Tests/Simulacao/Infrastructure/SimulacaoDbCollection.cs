using Xunit;

namespace Sgcf.Application.Tests.Simulacao.Infrastructure;

/// <summary>
/// Collection definition que garante que a <see cref="SimulacaoDbFixture"/> seja
/// compartilhada entre todos os testes de integração do módulo Simulação.
/// Um único container PostgreSQL é iniciado para toda a coleção.
/// </summary>
[CollectionDefinition("SimulacaoDb")]
public sealed class SimulacaoDbGroup : ICollectionFixture<SimulacaoDbFixture>;
