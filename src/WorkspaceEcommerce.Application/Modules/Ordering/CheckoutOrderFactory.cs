using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Application.Common.Localization;
using WorkspaceEcommerce.Domain.Common;
using WorkspaceEcommerce.Domain.Modules.Ordering;

namespace WorkspaceEcommerce.Application.Modules.Ordering;

internal sealed class CheckoutOrderFactory(
    ICheckoutStore checkoutStore,
    ICurrentLanguageProvider languageProvider)
{
    public async Task<Order> CreateAsync(
        CheckoutRequest request,
        Guid? customerId,
        IReadOnlyCollection<CheckoutItemSnapshot> snapshots,
        CancellationToken cancellationToken)
    {
        var shippingAddress = string.IsNullOrWhiteSpace(request.ShippingAddress)
            ? $"{request.ShippingStreet}, {request.ShippingWard}, {request.ShippingProvince}"
            : request.ShippingAddress;

        var currencyCode = languageProvider.CurrentLanguage == "vi" ? "VND" : "USD";
        var exchangeRate = languageProvider.CurrentLanguage == "vi" ? 26000m : 1m;

        var order = new Order(
            Guid.NewGuid(),
            await GenerateOrderCodeAsync(cancellationToken),
            customerId,
            request.CustomerName,
            request.CustomerPhone,
            request.CustomerEmail,
            shippingAddress,
            request.Note,
            request.PaymentMethod,
            currencyCode,
            exchangeRate);
        order.SetShippingAddressDetails(
            request.ShippingStreet,
            request.ShippingWard,
            request.ShippingProvince);

        foreach (var snapshot in snapshots)
        {
            order.AddItem(
                Guid.NewGuid(),
                snapshot.Variant.Id,
                snapshot.ProductNameSnapshot,
                snapshot.SkuSnapshot,
                snapshot.UnitPrice,
                snapshot.Quantity,
                snapshot.RequiresInstallation);
        }

        order.RecordCreated(Guid.NewGuid(), "Created by checkout.", changedBy: null);

        return order;
    }

    private async Task<string> GenerateOrderCodeAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var orderCode = $"ORD-{DateTimeOffset.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..21].ToUpperInvariant();
            if (!await checkoutStore.OrderCodeExistsAsync(orderCode, cancellationToken))
            {
                return orderCode;
            }
        }

        throw new DomainException("Could not generate a unique order code.");
    }
}
