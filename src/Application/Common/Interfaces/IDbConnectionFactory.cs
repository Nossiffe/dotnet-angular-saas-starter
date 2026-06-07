using System.Data.Common;

namespace Saas.Application.Common.Interfaces;

public interface IDbConnectionFactory
{
    Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken);
}
