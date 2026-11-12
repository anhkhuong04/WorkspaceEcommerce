using System.Security.Cryptography;
using System.Text.Json;
using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Application.Abstractions.Notifications;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Common.Persistence;
using WorkspaceEcommerce.Domain.Common;
using WorkspaceEcommerce.Domain.Modules.Loyalty;
using WorkspaceEcommerce.Domain.Modules.Ordering;
using WorkspaceEcommerce.Domain.Modules.Warranties;

namespace WorkspaceEcommerce.Application.Modules.Ordering.Returns;

internal sealed class OrderReturnService(IAppDbContext db, TimeProvider clock, ICustomerEmailOutbox? emailOutbox = null) : IOrderReturnService
{
    public async Task<Result<OrderReturnSummaryDto>> GetAsync(Guid orderId, int pageNumber, CancellationToken ct)
    {
        if (pageNumber is < 1 or > 100000) return Result<OrderReturnSummaryDto>.Validation(["Invalid return page."]);
        var order = await db.Orders.AsNoTrackingIfEf().Where(o => o.Id == orderId).FirstOrDefaultAsyncSafe(ct);
        if (order is null) return Result<OrderReturnSummaryDto>.NotFound("Order was not found.");
        var items = await ItemsAsync(orderId, ct);
        var reserved = await ReservedAsync(orderId, ct);
        var itemIds = items.Select(i => i.Id).ToArray();
        var units = await db.SerializedProductUnits.AsNoTrackingIfEf().Where(u => u.OrderItemId.HasValue && itemIds.Contains(u.OrderItemId.Value)).ToArrayAsyncSafe(ct);
        var query = db.OrderReturns.AsNoTrackingIfEf().Where(r => r.OrderId == orderId);
        var total = await query.CountAsyncSafe(ct);
        const int size = 10;
        var cases = await query.OrderByDescending(r => r.CreatedAt).ThenBy(r => r.Id).Skip((pageNumber - 1) * size).Take(size).ToArrayAsyncSafe(ct);
        var caseIds = cases.Select(r => r.Id).ToArray();
        var lines = await db.OrderReturnLines.AsNoTrackingIfEf().Where(l => caseIds.Contains(l.ReturnCaseId)).OrderBy(l => l.OrderItemId).ToArrayAsyncSafe(ct);
        var audit = await db.OrderReturnAudits.AsNoTrackingIfEf().Where(a => caseIds.Contains(a.ReturnCaseId)).OrderBy(a => a.OccurredAt).ThenBy(a => a.Id).ToArrayAsyncSafe(ct);
        var byId = items.ToDictionary(i => i.Id);
        var net = NetLines(order, items);
        var eligible = Eligibility(order, clock.GetUtcNow());
        var available = items.Select(i => new ReturnableOrderItemDto(i.Id, i.ProductNameSnapshot, i.SkuSnapshot, i.Quantity,
            Math.Max(0, i.Quantity - reserved.GetValueOrDefault(i.Id)), net[i.Id],
            units.Where(u => u.OrderItemId == i.Id && u.Status is SerializedProductUnitStatus.Assigned or SerializedProductUnitStatus.Activated or SerializedProductUnitStatus.Voided)
                .Select(u => new ReturnUnitDto(u.Id, u.MaskedIdentifier, u.Status)).ToArray())).ToArray();
        var dtos = cases.Select(r => new OrderReturnCaseDto(r.Id, r.Status, r.Revision, r.Reason, r.CreatedAt, r.ReceivedAt,
            r.MerchandiseRefund, r.ShippingRefund, r.RefundAmount, r.RefundReference, r.RefundedAt, r.RejectionReason,
            lines.Where(l => l.ReturnCaseId == r.Id).Select(l => new OrderReturnLineDto(l.OrderItemId, byId[l.OrderItemId].ProductNameSnapshot,
                byId[l.OrderItemId].SkuSnapshot, l.Quantity, l.ReceivedQuantity, l.RestockQuantity, l.RefundAmount, l.Inspection, l.SerializedUnitIds)).ToArray(),
            audit.Where(a => a.ReturnCaseId == r.Id).Select(a => new OrderReturnAuditDto(a.Action, a.Actor, a.Reason, a.OccurredAt)).ToArray())).ToArray();
        return Result<OrderReturnSummaryDto>.Success(new(orderId, order.CurrencyCode, eligible is null && available.Any(i => i.AvailableQuantity > 0), eligible,
            order.ReturnRequestedAt, order.ReturnReviewedAt, order.ReturnDecision, order.ReturnDecisionReason, available, new(dtos, pageNumber, size, total)));
    }

    public async Task<Result<OrderReturnSummaryDto>> CreateAsync(Guid orderId, CreateOrderReturnRequest request, string actor, CancellationToken ct)
    {
        var validation = await new CreateOrderReturnValidator().ValidateAsync(request, ct);
        if (!validation.IsValid) return Result<OrderReturnSummaryDto>.Validation(validation.Errors.Select(e => e.ErrorMessage));
        return await MutateAsync(orderId, null, false, actor, async (order, _, token) => {
            var fingerprint = Fingerprint(new { request.Reason, Items = request.Items.OrderBy(i => i.OrderItemId) });
            var existing = await db.OrderReturns.Where(r => r.OrderId == orderId && r.RequestId == request.RequestId).FirstOrDefaultAsyncSafe(token);
            if (existing is not null) { if (existing.RequestFingerprint != fingerprint) throw new DomainException("This return request ID was already used with different data."); return; }
            var eligibility = Eligibility(order, clock.GetUtcNow()); if (eligibility is not null) throw new DomainException(eligibility);
            var items = (await ItemsAsync(orderId, token)).ToDictionary(i => i.Id);
            var reserved = await ReservedAsync(orderId, token);
            foreach (var line in request.Items)
                if (!items.TryGetValue(line.OrderItemId, out var item) || line.Quantity > item.Quantity - reserved.GetValueOrDefault(item.Id))
                    throw new DomainException("A requested item or quantity is unavailable for return.");
            var now = clock.GetUtcNow(); var created = new OrderReturnCase(Guid.NewGuid(), orderId, request.RequestId, fingerprint, request.Reason, actor, now);
            db.Add(created);
            foreach (var line in request.Items) db.Add(new OrderReturnLine(Guid.NewGuid(), created.Id, line.OrderItemId, line.Quantity));
            if (order.ReturnRequestedAt.HasValue && !order.ReturnReviewedAt.HasValue) order.ReviewReturnRequest(true, request.Reason, now);
            Audit(created.Id, "Approved", actor, request.Reason, now);
            Notify(order, "Your return quantities were accepted for physical receipt and inspection. " + request.Reason);
        }, ct);
    }

    public async Task<Result<OrderReturnSummaryDto>> ReceiveAsync(Guid orderId, Guid id, ReceiveOrderReturnRequest request, string actor, CancellationToken ct)
    {
        var validation = await new ReceiveOrderReturnValidator().ValidateAsync(request, ct);
        if (!validation.IsValid) return Result<OrderReturnSummaryDto>.Validation(validation.Errors.Select(e => e.ErrorMessage));
        return await MutateAsync(orderId, id, true, actor, async (order, current, token) => {
            var value = current!;
            var fingerprint = Fingerprint(new { request.Reason, request.RefundShipping, Items = request.Items.OrderBy(i => i.OrderItemId).Select(i => new { i.OrderItemId, i.ReceivedQuantity, i.RestockQuantity, i.Inspection, Units = i.SerializedUnitIds.OrderBy(u => u) }) });
            if (value.ReceiptFingerprint == fingerprint) return;
            RequireRevision(value, request.ExpectedRevision, OrderReturnStatus.AwaitingReceipt);
            var lines = await db.OrderReturnLines.Where(l => l.ReturnCaseId == id).ToArrayAsyncSafe(token);
            if (request.Items.Length != lines.Length || !lines.All(l => request.Items.Any(i => i.OrderItemId == l.OrderItemId)))
                throw new DomainException("Inspect every approved return line exactly once.");
            var items = await ItemsAsync(orderId, token); var byId = items.ToDictionary(i => i.Id); var net = NetLines(order, items);
            var previous = await ReceivedAsync(orderId, id, token);
            var itemIds = lines.Select(l => l.OrderItemId).ToArray();
            var units = await db.SerializedProductUnits.Where(u => u.OrderItemId.HasValue && itemIds.Contains(u.OrderItemId.Value)).ToArrayAsyncSafe(token);
            decimal merchandise = 0;
            foreach (var input in request.Items)
            {
                var line = lines.Single(l => l.OrderItemId == input.OrderItemId); var item = byId[line.OrderItemId];
                if (input.ReceivedQuantity > line.Quantity) throw new DomainException("Received quantity exceeds the approved return quantity.");
                var serialized = units.Where(u => u.OrderItemId == item.Id).ToArray();
                if (serialized.Length > 0)
                {
                    if (input.RestockQuantity > 0) throw new DomainException("Serialized returns must enter quarantine. Reprovisioning for resale requires a separate inspected stock process.");
                    if (input.SerializedUnitIds.Length != input.ReceivedQuantity || input.SerializedUnitIds.Distinct().Count() != input.SerializedUnitIds.Length ||
                        input.SerializedUnitIds.Any(unitId => !serialized.Any(u => u.Id == unitId && u.Status is SerializedProductUnitStatus.Assigned or SerializedProductUnitStatus.Activated or SerializedProductUnitStatus.Voided)))
                        throw new DomainException("Select each physical returned unit once from this order item.");
                }
                else if (input.SerializedUnitIds.Length != 0) throw new DomainException("This order item has no assigned serialized units.");
                var before = previous.GetValueOrDefault(item.Id);
                if (before + input.ReceivedQuantity > item.Quantity) throw new DomainException("Received quantities exceed the purchased quantity.");
                merchandise += decimal.Floor(net[item.Id] * (before + input.ReceivedQuantity) / item.Quantity) - decimal.Floor(net[item.Id] * before / item.Quantity);
            }
            if (request.Items.Sum(i => i.ReceivedQuantity) <= 0) throw new DomainException("Record received goods or reject the return; no goods have been received.");
            var allReceived = items.All(item => previous.GetValueOrDefault(item.Id) + request.Items.Where(i => i.OrderItemId == item.Id).Sum(i => i.ReceivedQuantity) == item.Quantity);
            if (request.RefundShipping && !allReceived) throw new DomainException("Delivery fees can only be refunded after all order items have been received.");
            var otherCases = await db.OrderReturns.Where(r => r.OrderId == orderId && r.Id != id && r.Status != OrderReturnStatus.Rejected).ToArrayAsyncSafe(token);
            var netShipping = Math.Max(0m, order.ShippingFee - Math.Max(0m, order.DiscountAmount - order.Subtotal));
            var shippingRefund = request.RefundShipping ? Math.Max(0m, netShipping - otherCases.Sum(r => r.ShippingRefund)) : 0m;
            if (otherCases.Sum(r => r.RefundAmount) + merchandise + shippingRefund > order.TotalAmount)
                throw new DomainException("Refund allocation exceeds the frozen order total.");
            var restockIds = request.Items.Where(i => i.RestockQuantity > 0).Select(i => byId[i.OrderItemId].ProductVariantId).Distinct().ToArray();
            var variants = (await db.FindProductVariantsForUpdateAsync(restockIds, token)).ToDictionary(v => v.Id);
            var now = clock.GetUtcNow();
            foreach (var input in request.Items)
            {
                var line = lines.Single(l => l.OrderItemId == input.OrderItemId); var item = byId[line.OrderItemId]; var before = previous.GetValueOrDefault(item.Id);
                var amount = decimal.Floor(net[item.Id] * (before + input.ReceivedQuantity) / item.Quantity) - decimal.Floor(net[item.Id] * before / item.Quantity);
                line.Inspect(input.ReceivedQuantity, input.RestockQuantity, amount, input.Inspection, input.SerializedUnitIds);
                if (input.RestockQuantity > 0) { if (!variants.TryGetValue(item.ProductVariantId, out var variant)) throw new DomainException("Returned SKU was not found."); variant.RestoreStock(input.RestockQuantity); }
                foreach (var unitId in input.SerializedUnitIds)
                {
                    var unit = units.Single(u => u.Id == unitId);
                    var entitlement = await db.WarrantyEntitlements.Where(e => e.SerializedProductUnitId == unitId).FirstOrDefaultAsyncSafe(token);
                    if (entitlement is not null && entitlement.Status is WarrantyEntitlementStatus.Active or WarrantyEntitlementStatus.PendingActivation) entitlement.Void(now);
                    unit.MarkReturned(now);
                    db.Add(new WarrantyAuditEvent(Guid.NewGuid(), entitlement?.Id, unitId, WarrantyAuditAction.Returned, "Admin", actor, input.Inspection, $"return:{id}", now));
                }
            }
            value.Receive(merchandise, shippingRefund, fingerprint, now);
            if (value.RefundAmount > 0) order.MarkPaymentRefundPending();
            if (allReceived && order.Status == OrderStatus.Completed)
                db.Add(order.ChangeStatus(Guid.NewGuid(), OrderStatus.Returned, "All order items received and inspected through returns.", actor));
            Audit(id, "Received", actor, request.Reason, now);
            Notify(order, $"Returned goods were received and inspected. Approved refund: {value.RefundAmount:0} VND. A refund is recorded only after verified payment evidence.");
        }, ct);
    }

    public Task<Result<OrderReturnSummaryDto>> RejectAsync(Guid orderId, Guid id, RejectOrderReturnRequest request, string actor, CancellationToken ct)
    {
        if (!ReasonValid(request.Reason) || request.ExpectedRevision < 1) return Task.FromResult(Result<OrderReturnSummaryDto>.Validation(["Revision and a reason of up to 500 characters are required."]));
        return MutateAsync(orderId, id, false, actor, (order, current, _) => {
            var value = current!; if (value.Status == OrderReturnStatus.Rejected && value.RejectionReason == request.Reason.Trim()) return Task.CompletedTask;
            RequireRevision(value, request.ExpectedRevision, OrderReturnStatus.AwaitingReceipt); value.Reject(request.Reason);
            Audit(id, "Rejected", actor, request.Reason, clock.GetUtcNow()); return Task.CompletedTask;
        }, ct);
    }

    public async Task<Result<OrderReturnSummaryDto>> RefundAsync(Guid orderId, Guid id, RefundOrderReturnRequest request, string actor, CancellationToken ct)
    {
        var validation = await new RefundOrderReturnValidator().ValidateAsync(request, ct);
        if (!validation.IsValid) return Result<OrderReturnSummaryDto>.Validation(validation.Errors.Select(e => e.ErrorMessage));
        return await MutateAsync(orderId, id, false, actor, async (order, current, token) => {
            var value = current!;
            if (value.Status == OrderReturnStatus.Refunded && value.RefundReference == request.Reference.Trim() && request.Amount == value.RefundAmount) return;
            RequireRevision(value, request.ExpectedRevision, OrderReturnStatus.Received);
            if (await db.OrderReturns.Where(r => r.OrderId == orderId && r.Id != id && r.RefundReference == request.Reference.Trim()).AnyAsyncSafe(token))
                throw new DomainException("This refund reference was already used for another return.");
            value.RecordRefund(request.Amount, request.Reference, request.Reason, actor, clock.GetUtcNow());
            var anotherPending = await db.OrderReturns.Where(r => r.OrderId == orderId && r.Id != id && r.Status == OrderReturnStatus.Received).AnyAsyncSafe(token);
            order.RecordReturnRefund(request.Amount, anotherPending);
            await ReversePointsAsync(order, value, token);
            Audit(id, "RefundRecorded", actor, request.Reason, clock.GetUtcNow());
            Notify(order, $"A refund of {value.RefundAmount:0} VND was recorded. Reference: {value.RefundReference}.");
        }, ct);
    }

    public Task<Result<OrderReturnSummaryDto>> RejectRequestAsync(Guid orderId, ReviewReturnRequest request, string actor, CancellationToken ct)
    {
        if (!ReasonValid(request.Reason)) return Task.FromResult(Result<OrderReturnSummaryDto>.Validation(["A reason of up to 500 characters is required."]));
        return MutateAsync(orderId, null, false, actor, (order, _, _) => {
            if (order.ReturnDecision == "Rejected" && order.ReturnDecisionReason == request.Reason.Trim()) return Task.CompletedTask;
            order.ReviewReturnRequest(false, request.Reason, clock.GetUtcNow());
            Notify(order, "Your return request was declined. " + request.Reason);
            db.Add(new OrderStatusHistory(Guid.NewGuid(), order.Id, order.Status, order.Status, "Return request rejected: " + request.Reason, actor));
            return Task.CompletedTask;
        }, ct);
    }

    private async Task<Result<OrderReturnSummaryDto>> MutateAsync(Guid orderId, Guid? caseId, bool lockUnits, string actor,
        Func<Order, OrderReturnCase?, CancellationToken, Task> operation, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(actor) || actor.Length > 256) return Result<OrderReturnSummaryDto>.Unauthorized("An authenticated administrator is required.");
        Result<OrderReturnSummaryDto>? result = null;
        try
        {
            await db.ExecuteInTransactionAsync(async token => {
                await db.FindPendingPaymentTransactionsForOrderForUpdateAsync(orderId, token);
                Guid[] lockedUnitIds = [];
                if (lockUnits)
                {
                    var itemIds = db.OrderItems.Where(i => i.OrderId == orderId).Select(i => i.Id);
                    var ids = await db.SerializedProductUnits.AsNoTrackingIfEf().Where(u => u.OrderItemId.HasValue && itemIds.Contains(u.OrderItemId.Value)).OrderBy(u => u.Id).Select(u => u.Id).ToArrayAsyncSafe(token);
                    lockedUnitIds = ids;
                    foreach (var id in ids) await db.FindSerializedProductUnitByIdForUpdateAsync(id, token);
                    foreach (var id in ids) await db.FindWarrantyEntitlementByUnitIdForUpdateAsync(id, token);
                }
                var order = await db.FindOrderForUpdateAsync(orderId, token);
                if (order is null) { result = Result<OrderReturnSummaryDto>.NotFound("Order was not found."); return; }
                if (lockUnits)
                {
                    // A replacement/packing operation can add a unit while this
                    // request waits for its initial unit locks. Never touch an
                    // unheld new unit lock after taking the order lock.
                    var itemIds = db.OrderItems.Where(i => i.OrderId == orderId).Select(i => i.Id);
                    var currentIds = await db.SerializedProductUnits.AsNoTrackingIfEf().Where(u => u.OrderItemId.HasValue && itemIds.Contains(u.OrderItemId.Value)).Select(u => u.Id).ToArrayAsyncSafe(token);
                    if (currentIds.Except(lockedUnitIds).Any()) throw new DomainException("Physical unit assignments changed. Reload the return before recording receipt.");
                }
                var value = caseId.HasValue ? await db.FindReturnForUpdateAsync(caseId.Value, token) : null;
                if (caseId.HasValue && value?.OrderId != orderId) { result = Result<OrderReturnSummaryDto>.NotFound("Return was not found for this order."); return; }
                await operation(order, value, token); await db.SaveChangesAsync(token);
                result = await GetAsync(orderId, 1, token);
            }, ct);
        }
        catch (DomainException e) { return Result<OrderReturnSummaryDto>.Conflict(e.Message); }
        catch (PersistenceConcurrencyException) { return Result<OrderReturnSummaryDto>.Conflict("Return or loyalty data changed. Reload and retry the same operation."); }
        return result!;
    }
    private async Task ReversePointsAsync(Order order, OrderReturnCase current, CancellationToken ct)
    {
        if (!order.CustomerId.HasValue || order.Subtotal - order.DiscountAmount <= 0) return;
        var earned = await db.LoyaltyTransactions.Where(t => t.OrderId == order.Id && t.Type == LoyaltyTransactionType.Earn).Select(t => t.Points).FirstOrDefaultAsyncSafe(ct);
        if (earned <= 0) return;
        var refunded = await db.OrderReturns.Where(r => r.OrderId == order.Id && r.Id != current.Id && r.Status == OrderReturnStatus.Refunded).Select(r => r.MerchandiseRefund).ToArrayAsyncSafe(ct);
        var prior = await db.LoyaltyTransactions.Where(t => t.OrderId == order.Id && t.Type == LoyaltyTransactionType.Adjust && t.Description.StartsWith("Return reversal ")).Select(t => t.Points).ToArrayAsyncSafe(ct);
        var target = (int)decimal.Floor(earned * Math.Min(1m, (refunded.Sum() + current.MerchandiseRefund) / (order.Subtotal - order.DiscountAmount)));
        var difference = target + prior.Sum(); if (difference <= 0) return;
        var account = await db.FindLoyaltyAccountForUpdateAsync(order.CustomerId.Value, ct);
        if (account is null) throw new DomainException("The earned-points account could not be reconciled.");
        var tiers = await db.LoyaltyTiers.AsNoTrackingIfEf().ToArrayAsyncSafe(ct);
        db.Add(account.ReverseEarnedPoints(difference, order.Id, $"Return reversal {current.Id}: verified refund for {order.OrderCode}.", tiers, clock.GetUtcNow()));
    }
    private Task<OrderItem[]> ItemsAsync(Guid orderId, CancellationToken ct) => db.OrderItems.AsNoTrackingIfEf().Where(i => i.OrderId == orderId).OrderBy(i => i.Id).ToArrayAsyncSafe(ct);
    private async Task<Dictionary<Guid, int>> ReservedAsync(Guid orderId, CancellationToken ct)
    {
        var values = await (from line in db.OrderReturnLines.AsNoTrackingIfEf() join r in db.OrderReturns.AsNoTrackingIfEf() on line.ReturnCaseId equals r.Id
            where r.OrderId == orderId && r.Status != OrderReturnStatus.Rejected group new { line, r } by line.OrderItemId into g
            select new { Id = g.Key, Quantity = g.Sum(x => x.r.Status == OrderReturnStatus.AwaitingReceipt ? x.line.Quantity : x.line.ReceivedQuantity) }).ToArrayAsyncSafe(ct);
        return values.ToDictionary(x => x.Id, x => x.Quantity);
    }
    private async Task<Dictionary<Guid, int>> ReceivedAsync(Guid orderId, Guid except, CancellationToken ct)
    {
        var values = await (from line in db.OrderReturnLines.AsNoTrackingIfEf() join r in db.OrderReturns.AsNoTrackingIfEf() on line.ReturnCaseId equals r.Id
            where r.OrderId == orderId && r.Id != except && r.Status != OrderReturnStatus.Rejected group line by line.OrderItemId into g
            select new { Id = g.Key, Quantity = g.Sum(x => x.ReceivedQuantity) }).ToArrayAsyncSafe(ct);
        return values.ToDictionary(x => x.Id, x => x.Quantity);
    }
    private static Dictionary<Guid, decimal> NetLines(Order order, OrderItem[] items)
    {
        decimal prefix = 0, priorDiscount = 0; var result = new Dictionary<Guid, decimal>();
        foreach (var item in items.OrderBy(i => i.Id)) { prefix += item.LineTotal; var allocated = order.Subtotal > 0 ? decimal.Floor(Math.Min(order.DiscountAmount, order.Subtotal) * prefix / order.Subtotal) : 0;
            result[item.Id] = item.LineTotal - (allocated - priorDiscount); priorDiscount = allocated; }
        return result;
    }
    private static string? Eligibility(Order order, DateTimeOffset now)
    {
        if (order.ReturnDecision == "Rejected") return "The customer return request was rejected; its reviewed decision is final.";
        if (order.CurrencyCode != "VND" || order.Status != OrderStatus.Completed || order.PaidAt is null) return "Returns require a verified paid, completed VND order.";
        if (order.CompletedAt is null) return "Original completion time is unavailable; review legacy data before processing a return.";
        var timelyRequest = order.ReturnRequestedAt.HasValue && !order.ReturnReviewedAt.HasValue && order.ReturnRequestedAt <= order.CompletedAt.Value.AddDays(7);
        return now <= order.CompletedAt.Value.AddDays(7) || timelyRequest ? null : "The 7-day return request window has expired.";
    }
    private static bool ReasonValid(string? reason) => !string.IsNullOrWhiteSpace(reason) && reason.Trim().Length <= 500;
    private static string Fingerprint<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
    private static void RequireRevision(OrderReturnCase value, int revision, OrderReturnStatus status)
    { if (value.Revision != revision || value.Status != status) throw new DomainException("Return revision or status changed. Reload before retrying."); }
    private void Audit(Guid id, string action, string actor, string reason, DateTimeOffset now) => db.Add(new OrderReturnAudit(Guid.NewGuid(), id, action, actor, reason.Trim(), now));
    private void Notify(Order order, string message)
    {
        if (!string.IsNullOrWhiteSpace(order.CustomerEmail)) emailOutbox?.Enqueue(new(order.CustomerEmail, $"Return update for order {order.OrderCode}", message));
    }
}
