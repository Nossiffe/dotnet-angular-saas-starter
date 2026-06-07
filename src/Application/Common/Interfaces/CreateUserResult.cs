namespace Saas.Application.Common.Interfaces;

public sealed record CreateUserResult(
    bool Succeeded,
    Guid UserId,
    IReadOnlyDictionary<string, string[]> Errors);
