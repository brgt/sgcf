using Sgcf.Application.Common;

namespace Sgcf.Infrastructure.Services;

internal sealed class SystemRequestContextService : IRequestContextService
{
    public string  Source    => "job";
    public Guid    RequestId => Guid.CreateVersion7();
    public byte[]? IpHash    => null;
}
