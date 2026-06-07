namespace Saas.IntegrationTests;

public sealed class RealJwtApiFactory : PostgresApiFactory
{
    protected override bool UseTestAuth => false;
}
