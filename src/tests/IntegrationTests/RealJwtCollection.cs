using Xunit;

namespace Saas.IntegrationTests;

[CollectionDefinition("real-jwt")]
public sealed class RealJwtCollection : ICollectionFixture<RealJwtApiFactory>;
