namespace WorkspaceEcommerce.Application.Modules.Loyalty;

public sealed class LoyaltyOptions
{
    public const string SectionName = "Loyalty";

    public decimal MoneyPerPoint { get; init; } = 10000m;

    public decimal VoucherAmountPerPoint { get; init; } = 1000m;

    public int VoucherValidityDays { get; init; } = 30;

    public string[] Validate()
    {
        var errors = new List<string>();

        if (MoneyPerPoint <= 0m)
        {
            errors.Add("Loyalty money per point must be greater than zero.");
        }

        if (VoucherAmountPerPoint <= 0m)
        {
            errors.Add("Loyalty voucher amount per point must be greater than zero.");
        }

        if (VoucherValidityDays <= 0)
        {
            errors.Add("Loyalty voucher validity days must be greater than zero.");
        }

        return errors.ToArray();
    }
}
