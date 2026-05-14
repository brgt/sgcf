using Sgcf.Application.Common;

namespace Sgcf.Infrastructure.Services;

internal sealed class SystemCurrentUserService : ICurrentUserService
{
    public string ActorSub  => "system";
    public string ActorRole => "system";
}
