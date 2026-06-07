using Saas.Application.Common.Interfaces;

namespace Saas.Infrastructure.Persistence.Repositories;

public sealed class UnitOfWork(AppDbContext db) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) =>
        db.SaveChangesAsync(cancellationToken);

    public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken) =>
        db.ExecuteInTransactionAsync(operation, cancellationToken);
}
