using Xunit;

namespace Saas.IntegrationTests;

[CollectionDefinition("hangfire")]
public sealed class HangfireCollection : ICollectionFixture<NonDevelopmentApiFactory>;
