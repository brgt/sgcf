namespace Sgcf.Application.Common;

public interface ICurrentUserService
{
    public string ActorSub { get; }
    public string ActorRole { get; }
}
