using FluentAssertions;
using Sgcf.Domain.Auditoria;
using Xunit;

namespace Sgcf.Domain.Tests.Auditoria;

/// <summary>
/// Testes de regressão para <see cref="AuditConstants"/>.
///
/// Por que testar uma constante?
/// O valor de <c>SystemActor</c> é gravado em <c>audit_log.actor_sub</c> em produção.
/// Uma mudança inadvertida no valor quebraria queries forenses e dashboards de monitoramento
/// que filtram por esse valor. Este teste é a guarda contra refatorações descuidadas.
/// </summary>
[Trait("Category", "Unit")]
public sealed class AuditConstantsTests
{
    [Fact]
    public void SystemActor_DeveSerSYSTEM_EmMaiusculas()
    {
        // O valor "SYSTEM" (maiúsculo) distingue visualmente seeds/migrations
        // de valores de usuários reais (que vêm do provedor de identidade em lowercase).
        AuditConstants.SystemActor.Should().Be("SYSTEM",
            "o valor é gravado em audit_log.actor_sub e referenciado em queries forenses — " +
            "qualquer alteração precisa de migração de dados e atualização de dashboards");
    }

    [Fact]
    public void SystemActor_NaoDeveSerNuloOuVazio()
    {
        AuditConstants.SystemActor.Should().NotBeNullOrWhiteSpace(
            "actor_sub nulo em audit_log impede a distinção entre bug de autenticação e alteração administrativa");
    }
}
