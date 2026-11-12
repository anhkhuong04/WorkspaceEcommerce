using FluentValidation;
using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Common.Persistence;
using WorkspaceEcommerce.Domain.Common;
using WorkspaceEcommerce.Domain.Modules.Ordering;
using WorkspaceEcommerce.Domain.Modules.Payments;

namespace WorkspaceEcommerce.Application.Modules.Payments;

public sealed record RecordPaymentExceptionRequest(Guid RequestId, decimal ActualAmount, string Reference, string Reason);
public sealed record ResolvePaymentExceptionRequest(decimal Amount, string Reference, string Reason);
public sealed record PaymentExceptionDto(Guid Id, decimal ExpectedAmount, decimal ActualAmount, string Reference, string Reason, string Actor,
    DateTimeOffset RecordedAt, string? Settlement, decimal? SettlementAmount, string? SettlementReference, string? SettlementReason,
    string? SettledBy, DateTimeOffset? SettledAt, string RequiredSettlement, decimal RequiredAmount);
public sealed record PaymentExceptionsDto(bool CanRecord, string Currency, PagedResult<PaymentExceptionDto> Exceptions);
public interface IOrderPaymentExceptionService
{
    Task<Result<PaymentExceptionsDto>> GetAsync(Guid orderId, int pageNumber, CancellationToken ct);
    Task<Result<PaymentExceptionsDto>> RecordAsync(Guid orderId, RecordPaymentExceptionRequest request, string actor, CancellationToken ct);
    Task<Result<PaymentExceptionsDto>> ResolveAsync(Guid orderId, Guid id, ResolvePaymentExceptionRequest request, string actor, CancellationToken ct);
}
public sealed class RecordPaymentExceptionValidator : AbstractValidator<RecordPaymentExceptionRequest>
{
    public RecordPaymentExceptionValidator()
    {
        RuleFor(x => x.RequestId).NotEmpty(); RuleFor(x => x.ActualAmount).InclusiveBetween(1m, 99999999999999m).Must(x => x == decimal.Truncate(x));
        RuleFor(x => x.Reference).NotEmpty().MaximumLength(120); RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}
public sealed class ResolvePaymentExceptionValidator : AbstractValidator<ResolvePaymentExceptionRequest>
{
    public ResolvePaymentExceptionValidator()
    {
        RuleFor(x => x.Amount).InclusiveBetween(1m, 99999999999999m).Must(x => x == decimal.Truncate(x));
        RuleFor(x => x.Reference).NotEmpty().MaximumLength(120); RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}
internal sealed class OrderPaymentExceptionService(IAppDbContext db, IShippingCheckoutStore shipping, IOrderPaymentService payments, TimeProvider clock) : IOrderPaymentExceptionService
{
    public async Task<Result<PaymentExceptionsDto>> GetAsync(Guid orderId, int pageNumber, CancellationToken ct)
    {
        if (pageNumber is < 1 or > 100000) return Result<PaymentExceptionsDto>.Validation(["Invalid exceptions page."]);
        var order = await db.Orders.AsNoTrackingIfEf().Where(o => o.Id == orderId).FirstOrDefaultAsyncSafe(ct);
        if (order is null) return Result<PaymentExceptionsDto>.NotFound("Order was not found.");
        var query = db.OrderPaymentExceptions.AsNoTrackingIfEf().Where(e => e.OrderId == orderId);
        var count = await query.CountAsyncSafe(ct); var open = await query.Where(e => e.SettledAt == null).AnyAsyncSafe(ct);
        var values = await query.OrderByDescending(e => e.RecordedAt).ThenBy(e => e.Id).Skip((pageNumber - 1) * 10).Take(10).ToArrayAsyncSafe(ct);
        var operational = order.ImportSource is not null || await shipping.Selections.Where(s => s.OrderId == orderId).AnyAsyncSafe(ct);
        return Result<PaymentExceptionsDto>.Success(new(CanRecord(order) && operational && !open, order.CurrencyCode, new(values.Select(e => {
            var (action, amount) = Required(order, e);
            return new PaymentExceptionDto(e.Id, e.ExpectedAmount, e.ActualAmount, e.Reference, e.Reason, e.Actor, e.RecordedAt, e.Settlement, e.SettlementAmount, e.SettlementReference, e.SettlementReason, e.SettledBy, e.SettledAt, action, amount);
        }).ToArray(), pageNumber, 10, count)));
    }
    public async Task<Result<PaymentExceptionsDto>> RecordAsync(Guid orderId, RecordPaymentExceptionRequest request, string actor, CancellationToken ct)
    {
        var validation = await new RecordPaymentExceptionValidator().ValidateAsync(request, ct);
        if (!validation.IsValid) return Result<PaymentExceptionsDto>.Validation(validation.Errors.Select(e => e.ErrorMessage));
        return await MutateAsync(orderId, actor, async (order, token) => {
            var prior = await db.OrderPaymentExceptions.Where(e => e.OrderId == orderId && (e.RequestId == request.RequestId || e.Reference == request.Reference.Trim())).FirstOrDefaultAsyncSafe(token);
            if (prior is not null) { if (prior.ActualAmount != request.ActualAmount || prior.Reference != request.Reference.Trim() || prior.Reason != request.Reason.Trim()) throw new DomainException("This payment evidence was already recorded with different data."); return; }
            if (!CanRecord(order) || (order.ImportSource is null && !await shipping.Selections.Where(s => s.OrderId == orderId).AnyAsyncSafe(token)) || await db.OrderPaymentExceptions.Where(e => e.OrderId == orderId && e.SettledAt == null).AnyAsyncSafe(token))
                throw new DomainException("Record differences before full payment confirmation. Reconcile the existing open exception first.");
            db.Add(new OrderPaymentException(Guid.NewGuid(), orderId, request.RequestId, order.TotalAmount, request.ActualAmount, request.Reference.Trim(), request.Reason.Trim(), actor, clock.GetUtcNow()));
        }, ct);
    }
    public async Task<Result<PaymentExceptionsDto>> ResolveAsync(Guid orderId, Guid id, ResolvePaymentExceptionRequest request, string actor, CancellationToken ct)
    {
        var validation = await new ResolvePaymentExceptionValidator().ValidateAsync(request, ct);
        if (!validation.IsValid) return Result<PaymentExceptionsDto>.Validation(validation.Errors.Select(e => e.ErrorMessage));
        return await MutateAsync(orderId, actor, async (order, token) => {
            var value = await db.OrderPaymentExceptions.Where(e => e.Id == id && e.OrderId == orderId).FirstOrDefaultAsyncSafe(token) ?? throw new DomainException("Payment exception was not found.");
            if (value.SettledAt.HasValue && value.SettlementAmount == request.Amount && value.SettlementReference == request.Reference.Trim()) return;
            var (action, required) = Required(order, value);
            if (request.Amount != required || required <= 0) throw new DomainException("Record the exact required settlement after verifying the evidence.");
            if (request.Reference.Trim() == value.Reference || await db.OrderPaymentExceptions.Where(e => e.OrderId == orderId && e.Id != id && (e.Reference == request.Reference.Trim() || e.SettlementReference == request.Reference.Trim())).AnyAsyncSafe(token))
                throw new DomainException("Use a distinct, unused settlement transaction reference.");
            value.Resolve(action, request.Amount, request.Reference.Trim(), request.Reason.Trim(), actor, clock.GetUtcNow());
            if (order.Status is not (OrderStatus.Cancelled or OrderStatus.Returned))
            {
                await db.SaveChangesAsync(token);
                var confirmation = await payments.ConfirmAsync(orderId, order.PaymentMethod == PaymentMethod.Cod ? OrderPaymentReceiptKind.CodCollected : OrderPaymentReceiptKind.ManualTransfer,
                    new(order.TotalAmount, "VND", value.Reference, $"Reconciled payment exception {value.Id}; original and settlement evidence verified."), actor, token);
                if (confirmation.IsFailure) throw new DomainException(string.Join(" ", confirmation.Errors));
            }
        }, ct);
    }
    private async Task<Result<PaymentExceptionsDto>> MutateAsync(Guid orderId, string actor, Func<Order, CancellationToken, Task> action, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(actor) || actor.Length > 256) return Result<PaymentExceptionsDto>.Unauthorized("An administrator session is required.");
        Result<PaymentExceptionsDto>? result = null;
        try {
            await db.ExecuteInTransactionAsync(async token => {
                await db.FindPendingPaymentTransactionsForOrderForUpdateAsync(orderId, token);
                var order = await db.FindOrderForUpdateAsync(orderId, token);
                if (order is null) { result = Result<PaymentExceptionsDto>.NotFound("Order was not found."); return; }
                await action(order, token); await db.SaveChangesAsync(token); result = await GetAsync(orderId, 1, token);
            }, ct);
        } catch (DomainException e) { return Result<PaymentExceptionsDto>.Conflict(e.Message); }
        catch (PersistenceConcurrencyException) { return Result<PaymentExceptionsDto>.Conflict("Payment evidence changed. Reload before retrying."); }
        return result!;
    }
    private static bool CanRecord(Order order) => order.CurrencyCode == "VND" &&
        (order.PaymentMethod == PaymentMethod.ManualBankTransfer && order.PaymentStatus == PaymentStatus.Pending && order.Status is not (OrderStatus.Cancelled or OrderStatus.Returned) ||
         order.PaymentMethod == PaymentMethod.Cod && order.Status == OrderStatus.Shipping && order.PaymentStatus == PaymentStatus.Unpaid);
    private static (string, decimal) Required(Order order, OrderPaymentException value) => order.Status is OrderStatus.Cancelled or OrderStatus.Returned
        ? ("Refund received funds", value.ActualAmount)
        : value.ActualAmount < value.ExpectedAmount ? ("Collect shortfall", value.ExpectedAmount - value.ActualAmount) : ("Refund excess", value.ActualAmount - value.ExpectedAmount);
}
