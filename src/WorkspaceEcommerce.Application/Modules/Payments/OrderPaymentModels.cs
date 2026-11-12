using FluentValidation;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Domain.Modules.Ordering;
using WorkspaceEcommerce.Domain.Modules.Payments;
using WorkspaceEcommerce.Domain.Modules.Shipments;

namespace WorkspaceEcommerce.Application.Modules.Payments;

public sealed record ConfirmOrderPaymentRequest(decimal Amount, string Currency, string Reference, string Reason);
public sealed class ConfirmOrderPaymentValidator : AbstractValidator<ConfirmOrderPaymentRequest>
{
    public ConfirmOrderPaymentValidator()
    {
        RuleFor(x => x.Amount).GreaterThanOrEqualTo(0).Must(x => x == decimal.Truncate(x) && x <= 999_999_999_999_999_999m);
        RuleFor(x => x.Currency).Equal("VND");
        RuleFor(x => x.Reference).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}
public sealed record OrderPaymentReceiptDto(Guid Id, OrderPaymentReceiptKind Kind, decimal Amount, string Currency,
    string Reference, string Reason, string Actor, DateTimeOffset OccurredAtUtc);
public sealed record OrderPaymentSummaryDto(Guid OrderId, PaymentMethod PaymentMethod, PaymentStatus PaymentStatus,
    decimal TotalAmount, string Currency, ShippingMethod? ShippingMethod, bool CanConfirmTransfer, bool CanCollectCod,
    bool CanRemitCod, IReadOnlyCollection<OrderPaymentReceiptDto> Receipts)
{
    public bool CanConfirmFullRefund { get; init; }
    public string? ImportSource { get; init; }
}
public interface IOrderPaymentService
{
    Task<Result<OrderPaymentSummaryDto>> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<Result<OrderPaymentSummaryDto>> ConfirmAsync(Guid id, OrderPaymentReceiptKind kind,
        ConfirmOrderPaymentRequest request, string actor, CancellationToken cancellationToken);
}
