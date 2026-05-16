using Xunit;

namespace Sgcf.Application.Tests.Cotacoes.Infrastructure;

/// <summary>
/// Collection definition que garante que a <see cref="CotacoesDbFixture"/> seja
/// compartilhada entre todos os testes de integração do módulo Cotações.
/// Um único container PostgreSQL é iniciado para toda a coleção.
/// </summary>
[CollectionDefinition("CotacoesDb")]
public sealed class CotacoesDbGroup : ICollectionFixture<CotacoesDbFixture>;
