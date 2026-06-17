using WorkspaceEcommerce.Domain.Common;
using WorkspaceEcommerce.Domain.Modules.Customers;

namespace WorkspaceEcommerce.Application.Tests.Domain.Customers;

public sealed class CustomerTests
{
    [Fact]
    public void Constructor_ValidInput_NormalizesEmailAndSetsAuditTimestamps()
    {
        var customer = new Customer(
            Guid.NewGuid(),
            " Nguyen Van A ",
            " 0900000000 ",
            " CUSTOMER@EXAMPLE.COM ",
            "password-hash");

        Assert.Equal("Nguyen Van A", customer.FullName);
        Assert.Equal("0900000000", customer.PhoneNumber);
        Assert.Equal("customer@example.com", customer.Email);
        Assert.False(string.IsNullOrWhiteSpace(customer.PasswordHash));
        Assert.True(customer.CreatedAt <= DateTimeOffset.UtcNow);
        Assert.Equal(customer.CreatedAt, customer.UpdatedAt);
    }

    [Fact]
    public void Constructor_MissingEmail_ThrowsDomainException()
    {
        var exception = Assert.Throws<DomainException>(() =>
            new Customer(
                Guid.NewGuid(),
                "Nguyen Van A",
                "0900000000",
                string.Empty,
                "password-hash"));

        Assert.Equal("Email is required.", exception.Message);
    }

    [Fact]
    public void UpdateProfile_ValidInput_UpdatesContactInfoAndTimestamp()
    {
        var customer = CreateCustomer();
        var createdAt = customer.CreatedAt;

        customer.UpdateProfile(" Tran Thi B ", " 0911111111 ");

        Assert.Equal("Tran Thi B", customer.FullName);
        Assert.Equal("0911111111", customer.PhoneNumber);
        Assert.True(customer.UpdatedAt >= createdAt);
    }

    [Fact]
    public void UpdatePasswordHash_ValidInput_UpdatesPasswordHash()
    {
        var customer = CreateCustomer();

        customer.UpdatePasswordHash("new-password-hash");

        Assert.Equal("new-password-hash", customer.PasswordHash);
    }

    private static Customer CreateCustomer()
    {
        return new Customer(
            Guid.NewGuid(),
            "Nguyen Van A",
            "0900000000",
            "customer@example.com",
            "password-hash");
    }
}
