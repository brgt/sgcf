using System.Collections;
using System.Data;
using System.Data.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;
using Sgcf.Application.Tenancy;
using Sgcf.Infrastructure.Persistence;
using Xunit;

namespace Sgcf.Application.Tests.Tenancy;

/// <summary>
/// Testa o <see cref="TenantConnectionInterceptor"/>: verifica que
/// <c>set_config('app.tenant_id', ...)</c> é executado quando o contexto
/// está resolvido, e ignorado quando não está.
/// </summary>
public sealed class TenantConnectionInterceptorTests
{
    private static readonly Guid TenantIdFixo = Guid.Parse("00000000-0000-7000-8000-000000000001");

    // ── Cenário 1: IsResolved = true → deve emitir set_config ─────────────────

    [Fact]
    public async Task QuandoContextoResolvido_ConnectionOpenedAsync_ExecutaSetConfig()
    {
        // Arrange
        ITenantContext tenantCtx = CriarContextoResolvido(TenantIdFixo);
        TenantConnectionInterceptor sut = new(tenantCtx);
        FakeDbConnection fakeConn = new();
        ConnectionEndEventData eventData = CriarEventData();

        // Act — o método retorna Task (não ValueTask<DbConnection>) no EF Core 9
        await sut.ConnectionOpenedAsync(fakeConn, eventData, CancellationToken.None);

        // Assert — set_config foi chamado com o tenant_id correto
        fakeConn.UltimoComandoExecutado.Should().Contain("set_config");
        fakeConn.UltimoParametroValor.Should().Be(TenantIdFixo.ToString());
    }

    [Fact]
    public void QuandoContextoResolvido_ConnectionOpened_ExecutaSetConfig()
    {
        // Arrange
        ITenantContext tenantCtx = CriarContextoResolvido(TenantIdFixo);
        TenantConnectionInterceptor sut = new(tenantCtx);
        FakeDbConnection fakeConn = new();
        ConnectionEndEventData eventData = CriarEventData();

        // Act
        sut.ConnectionOpened(fakeConn, eventData);

        // Assert
        fakeConn.UltimoComandoExecutado.Should().Contain("set_config");
        fakeConn.UltimoParametroValor.Should().Be(TenantIdFixo.ToString());
    }

    // ── Cenário 2: IsResolved = false → NÃO deve emitir set_config ───────────

    [Fact]
    public async Task QuandoContextoNaoResolvido_ConnectionOpenedAsync_NaoExecutaSetConfig()
    {
        // Arrange
        ITenantContext tenantCtx = CriarContextoNaoResolvido();
        TenantConnectionInterceptor sut = new(tenantCtx);
        FakeDbConnection fakeConn = new();
        ConnectionEndEventData eventData = CriarEventData();

        // Act
        await sut.ConnectionOpenedAsync(fakeConn, eventData, CancellationToken.None);

        // Assert — nenhum comando foi emitido
        fakeConn.UltimoComandoExecutado.Should().BeNull(
            because: "contexto não resolvido (jobs/migrations) não deve configurar tenant");
    }

    [Fact]
    public void QuandoContextoNaoResolvido_ConnectionOpened_NaoExecutaSetConfig()
    {
        // Arrange
        ITenantContext tenantCtx = CriarContextoNaoResolvido();
        TenantConnectionInterceptor sut = new(tenantCtx);
        FakeDbConnection fakeConn = new();
        ConnectionEndEventData eventData = CriarEventData();

        // Act
        sut.ConnectionOpened(fakeConn, eventData);

        // Assert
        fakeConn.UltimoComandoExecutado.Should().BeNull(
            because: "contexto não resolvido não deve emitir set_config");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ITenantContext CriarContextoResolvido(Guid tenantId)
    {
        ITenantContext ctx = Substitute.For<ITenantContext>();
        ctx.IsResolved.Returns(true);
        ctx.TenantId.Returns(tenantId);
        return ctx;
    }

    private static ITenantContext CriarContextoNaoResolvido()
    {
        ITenantContext ctx = Substitute.For<ITenantContext>();
        ctx.IsResolved.Returns(false);
        return ctx;
    }

    private static ConnectionEndEventData CriarEventData()
    {
        // Assinatura EF Core 9:
        // (EventDefinitionBase, Func<EventDefinitionBase, EventData, string>,
        //  DbConnection, DbContext?, Guid, bool, DateTimeOffset, TimeSpan)
        return new ConnectionEndEventData(
            eventDefinition: null!,
            messageGenerator: null!,
            connection: null!,
            context: null,
            connectionId: Guid.Empty,
            async: false,
            startTime: DateTimeOffset.UtcNow,
            duration: TimeSpan.Zero);
    }
}

// ── Fake DbConnection ─────────────────────────────────────────────────────────
// Nullable é desabilitado nesta região porque as implementações mínimas de
// DbConnection, DbCommand, DbParameter e DbParameterCollection possuem assinaturas
// abstratas com object (non-nullable) que conflitam com nullable reference types.
// A lógica de teste em si está na classe acima com nullable habilitado.
#nullable disable

/// <summary>
/// Implementação mínima de <see cref="DbConnection"/> que captura o último
/// comando SQL executado pelo interceptor, sem dependência de banco real.
/// </summary>
internal sealed class FakeDbConnection : DbConnection
{
    public string UltimoComandoExecutado { get; private set; }
    public string UltimoParametroValor { get; private set; }

#pragma warning disable CS8765 // Nullability mismatch with base — DbConnection.ConnectionString setter uses non-nullable in older targets
    public override string ConnectionString { get; set; } = string.Empty;
#pragma warning restore CS8765
    public override string Database => "fake";
    public override string DataSource => "fake";
    public override string ServerVersion => "fake";
    public override ConnectionState State => ConnectionState.Open;

    public override void ChangeDatabase(string databaseName) { }
    public override void Close() { }
    public override void Open() { }

    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
        => throw new NotSupportedException();

    protected override DbCommand CreateDbCommand() => new FakeDbCommand(this);

    internal void RegistrarComando(string commandText, string parametroValor)
    {
        UltimoComandoExecutado = commandText;
        UltimoParametroValor = parametroValor;
    }
}

internal sealed class FakeDbCommand(FakeDbConnection owner) : DbCommand
{
    private readonly FakeDbParameterCollection _params = new();

    public override string CommandText { get; set; } = string.Empty;
    public override int CommandTimeout { get; set; }
    public override CommandType CommandType { get; set; }
    public override bool DesignTimeVisible { get; set; }
    public override UpdateRowSource UpdatedRowSource { get; set; }

    protected override DbConnection DbConnection { get; set; } = owner;
    protected override DbParameterCollection DbParameterCollection => _params;
    protected override DbTransaction DbTransaction { get; set; }

    public override void Cancel() { }
    public override void Prepare() { }

    public override int ExecuteNonQuery()
    {
        owner.RegistrarComando(CommandText, _params.PrimeiroValor);
        return 0;
    }

    public override Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken)
    {
        owner.RegistrarComando(CommandText, _params.PrimeiroValor);
        return Task.FromResult(0);
    }

    public override object ExecuteScalar() => null;

    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        => throw new NotSupportedException();

    protected override DbParameter CreateDbParameter() => new FakeDbParameter();
}

internal sealed class FakeDbParameter : DbParameter
{
    public override DbType DbType { get; set; }
    public override ParameterDirection Direction { get; set; }
    public override bool IsNullable { get; set; }
    public override string ParameterName { get; set; } = string.Empty;
    public override int Size { get; set; }
    public override string SourceColumn { get; set; } = string.Empty;
    public override bool SourceColumnNullMapping { get; set; }
    public override object Value { get; set; }
    public override void ResetDbType() { }
}

/// <summary>
/// Coleção mínima de parâmetros que apenas armazena itens sem validação alguma.
/// </summary>
internal sealed class FakeDbParameterCollection : DbParameterCollection
{
    private readonly List<DbParameter> _items = [];

    public string PrimeiroValor => _items.Count > 0 ? _items[0].Value?.ToString() : null;

    public override int Count => _items.Count;
    public override object SyncRoot => _items;

    public override int Add(object value) { _items.Add((DbParameter)value); return _items.Count - 1; }
    public override void AddRange(Array values)
    {
        foreach (object v in values)
        {
            _items.Add((DbParameter)v);
        }
    }
    public override void Clear() => _items.Clear();
    public override bool Contains(object value) => _items.Contains((DbParameter)value);
    public override bool Contains(string value) => _items.Any(p => p.ParameterName == value);
    public override void CopyTo(Array array, int index) => ((ICollection)_items).CopyTo(array, index);
    public override IEnumerator GetEnumerator() => _items.GetEnumerator();
    public override int IndexOf(object value) => _items.IndexOf((DbParameter)value);
    public override int IndexOf(string parameterName) => _items.FindIndex(p => p.ParameterName == parameterName);
    public override void Insert(int index, object value) => _items.Insert(index, (DbParameter)value);
    public override void Remove(object value) => _items.Remove((DbParameter)value);
    public override void RemoveAt(int index) => _items.RemoveAt(index);
    public override void RemoveAt(string parameterName) => _items.RemoveAt(IndexOf(parameterName));

    protected override DbParameter GetParameter(int index) => _items[index];
    protected override DbParameter GetParameter(string parameterName) => _items.First(p => p.ParameterName == parameterName);
    protected override void SetParameter(int index, DbParameter value) => _items[index] = value;
    protected override void SetParameter(string parameterName, DbParameter value) => _items[IndexOf(parameterName)] = value;
}
