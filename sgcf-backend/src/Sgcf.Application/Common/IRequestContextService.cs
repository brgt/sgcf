namespace Sgcf.Application.Common;

public interface IRequestContextService
{
    public string Source { get; }
    public Guid RequestId { get; }
    public byte[]? IpHash { get; }
}
