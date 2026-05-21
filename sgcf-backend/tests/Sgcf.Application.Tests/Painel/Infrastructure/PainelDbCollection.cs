using Xunit;

namespace Sgcf.Application.Tests.Painel.Infrastructure;

/// <summary>
/// Collection definition que garante que a <see cref="PainelDbFixture"/> seja
/// compartilhada entre todos os testes de integração do módulo Painel.
/// Um único container PostgreSQL é iniciado para toda a coleção.
/// </summary>
[CollectionDefinition("PainelDb")]
public sealed class PainelDbGroup : ICollectionFixture<PainelDbFixture>;
