namespace WorkspaceEcommerce.Domain.Common;

/// <summary>
/// Currency convention for the commerce domain. Monetary amounts are stored as
/// their actual VND value; presentation code must not apply an implicit exchange rate.
/// </summary>
public static class CommerceCurrency
{
    public const string Code = "VND";
    public const decimal BaseExchangeRate = 1m;

    public static decimal RequireValidAmount(decimal amount, string name)
    {
        Guard.NotNegative(amount, name);

        if (amount != decimal.Truncate(amount))
        {
            throw new DomainException($"{name} must be a whole VND amount.");
        }

        return amount;
    }
}
