using FluentValidation;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Domain.Modules.Ordering;
using WorkspaceEcommerce.Domain.Modules.Warranties;

namespace WorkspaceEcommerce.Application.Modules.Ordering.Returns;

public sealed record ReturnItemRequest(Guid OrderItemId, int Quantity);
public sealed record CreateOrderReturnRequest(Guid RequestId, string Reason, ReturnItemRequest[] Items);
public sealed record InspectReturnItemRequest(Guid OrderItemId, int ReceivedQuantity, int RestockQuantity, string Inspection, Guid[] SerializedUnitIds);
public sealed record ReceiveOrderReturnRequest(int ExpectedRevision, string Reason, bool RefundShipping, InspectReturnItemRequest[] Items);
public sealed record RejectOrderReturnRequest(int ExpectedRevision, string Reason);
public sealed record RefundOrderReturnRequest(int ExpectedRevision, decimal Amount, string Reference, string Reason);
public sealed record ReviewReturnRequest(string Reason);
public sealed record ReturnUnitDto(Guid Id, string MaskedIdentifier, SerializedProductUnitStatus Status);
public sealed record ReturnableOrderItemDto(Guid OrderItemId, string ProductName, string Sku, int Quantity, int AvailableQuantity, decimal NetLineAmount, IReadOnlyCollection<ReturnUnitDto> Units);
public sealed record OrderReturnLineDto(Guid OrderItemId, string ProductName, string Sku, int Quantity, int ReceivedQuantity, int RestockQuantity, decimal RefundAmount, string? Inspection, Guid[] SerializedUnitIds);
public sealed record OrderReturnAuditDto(string Action, string Actor, string Reason, DateTimeOffset OccurredAt);
public sealed record OrderReturnCaseDto(Guid Id, OrderReturnStatus Status, int Revision, string Reason, DateTimeOffset CreatedAt,
    DateTimeOffset? ReceivedAt, decimal MerchandiseRefund, decimal ShippingRefund, decimal RefundAmount, string? RefundReference,
    DateTimeOffset? RefundedAt, string? RejectionReason, IReadOnlyCollection<OrderReturnLineDto> Items, IReadOnlyCollection<OrderReturnAuditDto> Audit);
public sealed record OrderReturnSummaryDto(Guid OrderId, string Currency, bool CanCreate, string? EligibilityMessage,
    DateTimeOffset? RequestedAt, DateTimeOffset? ReviewedAt, string? Decision, string? DecisionReason,
    IReadOnlyCollection<ReturnableOrderItemDto> Items, PagedResult<OrderReturnCaseDto> Returns);

public sealed class CreateOrderReturnValidator : AbstractValidator<CreateOrderReturnRequest>
{
    public CreateOrderReturnValidator()
    {
        RuleFor(x => x.RequestId).NotEmpty(); RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Items).NotEmpty().Must(x => x is not null && x.Length <= 100);
        RuleForEach(x => x.Items).ChildRules(item => { item.RuleFor(x => x.OrderItemId).NotEmpty(); item.RuleFor(x => x.Quantity).InclusiveBetween(1, 10000); });
        RuleFor(x => x.Items).Must(x => x is null || x.All(i => i is not null) && x.Select(i => i.OrderItemId).Distinct().Count() == x.Length).WithMessage("Choose each order item once.");
    }
}
public sealed class ReceiveOrderReturnValidator : AbstractValidator<ReceiveOrderReturnRequest>
{
    public ReceiveOrderReturnValidator()
    {
        RuleFor(x => x.ExpectedRevision).GreaterThan(0); RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Items).NotEmpty().Must(x => x is not null && x.Length <= 100);
        RuleForEach(x => x.Items).ChildRules(item => {
            item.RuleFor(x => x.OrderItemId).NotEmpty(); item.RuleFor(x => x.ReceivedQuantity).InclusiveBetween(0, 10000);
            item.RuleFor(x => x.RestockQuantity).InclusiveBetween(0, 10000).LessThanOrEqualTo(x => x.ReceivedQuantity);
            item.RuleFor(x => x.Inspection).NotEmpty().MaximumLength(500);
            item.RuleFor(x => x.SerializedUnitIds).NotNull().Must(ids => ids is null || ids.Length <= 10000);
        });
        RuleFor(x => x.Items).Must(x => x is null || x.All(i => i is not null) && x.Select(i => i.OrderItemId).Distinct().Count() == x.Length).WithMessage("Choose each return item once.");
    }
}
public sealed class RefundOrderReturnValidator : AbstractValidator<RefundOrderReturnRequest>
{
    public RefundOrderReturnValidator()
    {
        RuleFor(x => x.ExpectedRevision).GreaterThan(0); RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Reference).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Amount).GreaterThan(0).Must(x => x == decimal.Truncate(x));
    }
}
public interface IOrderReturnService
{
    Task<Result<OrderReturnSummaryDto>> GetAsync(Guid orderId, int pageNumber, CancellationToken cancellationToken);
    Task<Result<OrderReturnSummaryDto>> CreateAsync(Guid orderId, CreateOrderReturnRequest request, string actor, CancellationToken cancellationToken);
    Task<Result<OrderReturnSummaryDto>> ReceiveAsync(Guid orderId, Guid id, ReceiveOrderReturnRequest request, string actor, CancellationToken cancellationToken);
    Task<Result<OrderReturnSummaryDto>> RejectAsync(Guid orderId, Guid id, RejectOrderReturnRequest request, string actor, CancellationToken cancellationToken);
    Task<Result<OrderReturnSummaryDto>> RefundAsync(Guid orderId, Guid id, RefundOrderReturnRequest request, string actor, CancellationToken cancellationToken);
    Task<Result<OrderReturnSummaryDto>> RejectRequestAsync(Guid orderId, ReviewReturnRequest request, string actor, CancellationToken cancellationToken);
}
