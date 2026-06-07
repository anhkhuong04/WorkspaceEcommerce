using Microsoft.Extensions.Configuration;
using WorkspaceEcommerce.Infrastructure.Configuration;

namespace WorkspaceEcommerce.Infrastructure.Tests.Configuration;

public sealed class ConnectionStringValidatorTests
{
    [Fact]
    public void GetValidatedDefaultConnectionString_WhenConnectionStringIsValid_ReturnsConnectionString()
    {
        var connectionString = "Host=localhost;Port=5432;Database=workspace_ecommerce;Username=postgres;Password=local_dev";
        var configuration = BuildConfiguration(connectionString);

        var result = configuration.GetValidatedDefaultConnectionString();

        Assert.Equal(connectionString, result);
    }

    [Fact]
    public void GetValidatedDefaultConnectionString_WhenConnectionStringIsMissing_Throws()
    {
        var configuration = BuildConfiguration(null);

        var exception = Assert.Throws<InvalidOperationException>(
            configuration.GetValidatedDefaultConnectionString);

        Assert.Contains("required", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Host=localhost;Database=workspace_ecommerce;Username=postgres;Password=CHANGE_ME")]
    [InlineData("Host=localhost;Database=workspace_ecommerce;Username=YOUR_USER;Password=local_dev")]
    [InlineData("Host=<host>;Database=workspace_ecommerce;Username=postgres;Password=local_dev")]
    public void GetValidatedDefaultConnectionString_WhenConnectionStringContainsPlaceholder_Throws(
        string connectionString)
    {
        var configuration = BuildConfiguration(connectionString);

        var exception = Assert.Throws<InvalidOperationException>(
            configuration.GetValidatedDefaultConnectionString);

        Assert.Contains("placeholder", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetValidatedDefaultConnectionString_WhenConnectionStringIsInvalid_Throws()
    {
        var configuration = BuildConfiguration("DefinitelyNotAConnectionString");

        var exception = Assert.Throws<InvalidOperationException>(
            configuration.GetValidatedDefaultConnectionString);

        Assert.Contains("invalid", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetValidatedDefaultConnectionString_WhenHostIsMissing_Throws()
    {
        var configuration = BuildConfiguration("Database=workspace_ecommerce;Username=postgres;Password=local_dev");

        var exception = Assert.Throws<InvalidOperationException>(
            configuration.GetValidatedDefaultConnectionString);

        Assert.Contains("Host", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GetValidatedDefaultConnectionString_WhenDatabaseIsMissing_Throws()
    {
        var configuration = BuildConfiguration("Host=localhost;Username=postgres;Password=local_dev");

        var exception = Assert.Throws<InvalidOperationException>(
            configuration.GetValidatedDefaultConnectionString);

        Assert.Contains("Database", exception.Message, StringComparison.Ordinal);
    }

    private static IConfiguration BuildConfiguration(string? defaultConnection)
    {
        var values = new Dictionary<string, string?>();
        if (defaultConnection is not null)
        {
            values["ConnectionStrings:DefaultConnection"] = defaultConnection;
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
