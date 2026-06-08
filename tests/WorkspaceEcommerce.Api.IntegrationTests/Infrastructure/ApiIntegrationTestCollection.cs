namespace WorkspaceEcommerce.Api.IntegrationTests.Infrastructure;

[CollectionDefinition(Name)]
public sealed class ApiIntegrationTestCollection : ICollectionFixture<ApiIntegrationTestFixture>
{
    public const string Name = "ApiIntegrationTests";
}
