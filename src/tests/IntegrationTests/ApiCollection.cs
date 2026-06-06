using Xunit;

namespace Saas.IntegrationTests;

[CollectionDefinition("api")]
public sealed class ApiCollection : ICollectionFixture<PostgresApiFactory>;
