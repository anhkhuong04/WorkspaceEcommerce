namespace WorkspaceEcommerce.Infrastructure.Configuration;

public sealed class OrderReceiptOptions
{
    public const string SectionName = "OrderReceipt";

    public string SellerName { get; init; } = "Workspace Ecommerce";

    public string? SupportEmail { get; init; }

    public string? SupportPhone { get; init; }

    /// <summary>
    /// QuestPDF requires an explicit license declaration. Supported values are
    /// Community, Professional, and Enterprise. Confirm eligibility before deployment.
    /// </summary>
    public string QuestPdfLicense { get; init; } = "Community";
}
