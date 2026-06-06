namespace Saas.Application.Common.Interfaces;

public interface ICurrentUser
{
    string? UserId { get; }
    bool IsAuthenticated { get; }
}
