using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentValidation;
using Microsoft.Extensions.Logging;
using WorkspaceEcommerce.Application.Abstractions.Notifications;
using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Application.Common.Localization;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Common.Persistence;
using WorkspaceEcommerce.Application.Modules.Loyalty;
using WorkspaceEcommerce.Application.Modules.Shipments;
using WorkspaceEcommerce.Domain.Modules.Catalog;
using WorkspaceEcommerce.Domain.Common;
using WorkspaceEcommerce.Domain.Modules.Ordering;
using WorkspaceEcommerce.Domain.Modules.Payments;

namespace WorkspaceEcommerce.Application.Modules.Ordering;

internal sealed class AdminOrderService(
    IAppDbContext dbContext,
    IValidator<AdminOrderListRequest> listValidator,
    IValidator<UpdateOrderStatusRequest> updateStatusValidator,
    ILoyaltyService loyaltyService,
    ICurrentLanguageProvider languageProvider,
    ILogger<AdminOrderService> logger,
    IOrderShipmentService? shipmentService = null,
    INotificationService? notificationService = null,
    ICustomerEmailOutbox? customerEmailOutbox = null,
    IFulfillmentCoordinator? fulfillment = null, IOrderShippingReader? shippingReader = null, Microsoft.Extensions.Options.IOptions<OrderExpiryOptions>? expiryOptions = null, IShippingCheckoutStore? shippingStore = null) : IAdminOrderService
{
    public async Task<Result<PagedResult<AdminOrderListItemDto>>> GetOrdersAsync(
        AdminOrderListRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await listValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result<PagedResult<AdminOrderListItemDto>>.Validation(
                validationResult.Errors.Select(error => error.ErrorMessage));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var normalizedSearch = NormalizeOptional(request.Search)?.ToLowerInvariant();
        var status = request.Status;
        var query = dbContext.Orders
            .AsNoTrackingIfEf()
            .Where(order => (!status.HasValue || order.Status == status.Value) && (!request.PaymentStatus.HasValue || order.PaymentStatus == request.PaymentStatus));
        query = AdminOrderQueues.Filter(dbContext, query, request.Queue, shippingStore);
        if (normalizedSearch is not null)
        {
            query = query.Where(order =>
                order.OrderCode.ToLower().Contains(normalizedSearch) ||
                order.CustomerName.ToLower().Contains(normalizedSearch) ||
                order.CustomerPhone.ToLower().Contains(normalizedSearch) ||
                (order.TrackingCode != null && order.TrackingCode.ToLower().Contains(normalizedSearch)));
        }

        var totalCount = await query.CountAsyncSafe(cancellationToken);
        var pageNumber = request.NormalizedPageNumber;
        var pageSize = request.NormalizedPageSize;
        var items = await query
            .OrderByDescending(order => order.CreatedAt)
            .ThenByDescending(order => order.OrderCode)
            .Skip(request.Skip)
            .Take(pageSize)
            .Select(order => new AdminOrderListItemDto(
                order.Id,
                order.OrderCode,
                order.CustomerName,
                order.CustomerPhone,
                order.TotalAmount,
                order.Status,
                order.PaymentMethod,
                order.PaymentStatus,
                order.PaidAt,
                order.CreatedAt,
                order.UpdatedAt,
                dbContext.OrderItems.Count(item => item.OrderId == order.Id),
                order.TrackingCode,
                dbContext.OrderShipments
                    .Where(shipment => shipment.OrderId == order.Id)
                    .Select(shipment => shipment.ProviderStatus)
                    .FirstOrDefault()))
            .ToArrayAsyncSafe(cancellationToken);
        if (shippingReader is not null)
        {
            var summaries = await shippingReader.GetManyAsync(items.Select(x => x.Id).ToArray(), cancellationToken);
            items = items.Select(item => summaries.TryGetValue(item.Id, out var summary)
                ? item with { Shipping = summary, TrackingCode = summary.TrackingCode ?? item.TrackingCode, ShipmentStatus = summary.Status ?? item.ShipmentStatus } : item).ToArray();
        }
        var page = new PagedResult<AdminOrderListItemDto>(
            items,
            pageNumber,
            pageSize,
            totalCount);

        return Result<PagedResult<AdminOrderListItemDto>>.Success(page);
    }

    public async Task<Result<AdminOrderDto>> GetOrderByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var order = await dbContext.Orders
            .AsNoTrackingIfEf()
            .Where(existing => existing.Id == id)
            .FirstOrDefaultAsyncSafe(cancellationToken);
        if (order is null)
        {
            return Result<AdminOrderDto>.NotFound("Order was not found.");
        }

        return Result<AdminOrderDto>.Success(await ToDetailDtoAsync(order, cancellationToken));
    }

    public async Task<Result<AdminOrderDto>> UpdateStatusAsync(
        Guid id,
        UpdateOrderStatusRequest request,
        string? changedBy,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await updateStatusValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result<AdminOrderDto>.Validation(validationResult.Errors.Select(error => error.ErrorMessage));
        }

        Order? order = null;
        try
        {
            await dbContext.ExecuteInTransactionAsync(async transactionCancellationToken =>
            {
                var pendingPaymentTransactions = request.Status == OrderStatus.Cancelled
                    ? await dbContext.FindPendingPaymentTransactionsForOrderForUpdateAsync(id, transactionCancellationToken)
                    : [];
                order = await dbContext.FindOrderForUpdateAsync(id, transactionCancellationToken);
                if (order is null)
                {
                    return;
                }

                if (request.ExpectedShipmentRevision.HasValue)
                {
                    var local = await dbContext.FindOrderShipmentForUpdateAsync(order.Id, transactionCancellationToken);
                    if (request.Status != OrderStatus.Cancelled || local?.Provider != "WorkspaceEC" || local.Revision != request.ExpectedShipmentRevision)
                        throw new DomainException("Shipment revision changed or this command is not a local cancellation. Reload before retrying.");
                }
                if (request.Status == OrderStatus.Returned)
                    throw new DomainException("Returns require a received/inspected item workflow. A status edit cannot confirm receipt, refund, loyalty or warranty reversal.");
                if (fulfillment is not null) await fulfillment.GuardStatusAsync(order, request.Status, transactionCancellationToken);
                var previousStatus = order.Status;
                var history = order.ChangeStatus(
                    Guid.NewGuid(),
                    request.Status,
                    NormalizeOptional(request.InternalNote),
                    request.Status == OrderStatus.Cancelled ? NormalizeOptional(request.CancellationReason) : null,
                    NormalizeOptional(request.CustomerMessage),
                    NormalizeOptional(changedBy));
                dbContext.Add(history);

                if (request.Status == OrderStatus.Cancelled)
                {
                    await OrderCancellationEffects.ApplyAsync(dbContext, order, previousStatus, pendingPaymentTransactions,
                        request.CancellationReason!, DateTimeOffset.UtcNow, transactionCancellationToken);
                    EnqueueCancellationEmail(order, request.CancellationReason!, request.CustomerMessage);
                    if (fulfillment is not null && await fulfillment.MethodAsync(order.Id, transactionCancellationToken) is not null)
                        await fulfillment.CancelAsync(order, request.CancellationReason!, transactionCancellationToken, changedBy ?? "Admin order cancellation");
                }

                await dbContext.SaveChangesAsync(transactionCancellationToken);
            }, cancellationToken);

            if (order is null)
            {
                return Result<AdminOrderDto>.NotFound("Order was not found.");
            }

            if (request.Status == OrderStatus.Completed)
            {
                await TryEarnLoyaltyPointsAsync(order.Id, cancellationToken);
            }

            if (request.Status == OrderStatus.Cancelled &&
                !string.IsNullOrWhiteSpace(order.TrackingCode) &&
                shipmentService is not null && (fulfillment is null || await fulfillment.MethodAsync(order.Id, cancellationToken) is null))
            {
                await shipmentService.QueueCancelAsync(
                    order.Id,
                    request.CancellationReason!,
                    cancellationToken);
            }

            await NotifyCustomerAfterCommitAsync(order, request, cancellationToken);

            return Result<AdminOrderDto>.Success(await ToDetailDtoAsync(order, cancellationToken));
        }
        catch (DomainException exception)
        {
            return Result<AdminOrderDto>.Conflict(exception.Message);
        }
    }

    private void EnqueueCancellationEmail(Order order, string cancellationReason, string? customerMessage)
    {
        if (customerEmailOutbox is null || string.IsNullOrWhiteSpace(order.CustomerEmail))
        {
            return;
        }

        var refundMessage = order.PaymentStatus == PaymentStatus.RefundPending
            ? " Your payment has been queued for refund."
            : string.Empty;
        var additionalMessage = NormalizeOptional(customerMessage) is { } message
            ? $" {message}"
            : string.Empty;
        customerEmailOutbox.Enqueue(new CustomerEmailMessage(
            order.CustomerEmail,
            $"Order {order.OrderCode} was cancelled",
            $"Your order {order.OrderCode} was cancelled. Reason: {cancellationReason}.{additionalMessage}{refundMessage}"));
    }

    private async Task NotifyCustomerAfterCommitAsync(
        Order order,
        UpdateOrderStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (notificationService is null || order.CustomerId is null)
        {
            return;
        }

        try
        {
            await notificationService.NotifyCustomerAsync(
                order.CustomerId.Value,
                "order_status_changed",
                new
                {
                    orderId = order.Id,
                    orderCode = order.OrderCode,
                    newStatus = (int)order.Status,
                    paymentStatus = (int)order.PaymentStatus,
                    cancellationReason = NormalizeOptional(request.CancellationReason),
                    customerMessage = NormalizeOptional(request.CustomerMessage)
                },
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Could not send order status notification for order {OrderCode}", order.OrderCode);
        }
    }

    public async Task<Result<AdminOrderImportPreviewDto>> PreviewImportAsync(Stream content, string fileName,
        CancellationToken cancellationToken = default, string source = "manual")
    {
        var plan = await BuildImportPlanAsync(content, fileName, cancellationToken, source);
        return plan.IsFailure ? Result<AdminOrderImportPreviewDto>.Validation(plan.Errors)
            : Result<AdminOrderImportPreviewDto>.Success(plan.Value!.Preview);
    }

    public async Task<Result<AdminOrderImportCommitResultDto>> CommitImportAsync(Stream content, string fileName,
        string? changedBy, CancellationToken cancellationToken = default, string source = "manual")
    {
        source = source.Trim().ToLowerInvariant();
        if (!ValidImportSource(source)) return Result<AdminOrderImportCommitResultDto>.Validation(["Import source must use 1-80 letters, digits, dots, underscores or hyphens."]);
        Result<AdminOrderImportCommitResultDto>? result = null;
        try
        {
            await dbContext.ExecuteInTransactionAsync(async ct =>
            {
                await dbContext.LockOrderImportAsync(source, ct);
                var planResult = await BuildImportPlanAsync(content, fileName, ct, source);
                if (planResult.IsFailure) { result = Result<AdminOrderImportCommitResultDto>.Validation(planResult.Errors); return; }
                var plan = planResult.Value!;
                if (plan.Preview.ErrorRows > 0) { result = Result<AdminOrderImportCommitResultDto>.Success(new(0, [], plan.Preview)); return; }
                var created = new List<AdminOrderImportCreatedOrderDto>();
                var variantIds = plan.Orders.SelectMany(o => o.Items).Select(i => i.Variant.Id).Distinct().ToArray();
                var locked = (await dbContext.FindProductVariantsForUpdateAsync(variantIds, ct)).ToDictionary(v => v.Id);
                foreach (var header in plan.Orders)
                {
                    var order = new Order(Guid.NewGuid(), await GenerateOrderCodeAsync(ct), null,
                        header.CustomerName, header.CustomerPhone, header.CustomerEmail, header.ShippingAddress,
                        header.Note, header.PaymentMethod, GetCurrencyCode(), GetExchangeRate());
                    order.SetImportIdentity(source, header.ExternalOrderCode, ImportFingerprint(header.Items.Select(i => i.Row)));
                    if (order.PaymentMethod != PaymentMethod.Cod)
                        order.SetPaymentDeadline(order.CreatedAt.AddMinutes((expiryOptions?.Value ?? new OrderExpiryOptions()).ManualTransferMinutes));
                    order.SetShippingAddressDetails(header.ShippingStreet, header.ShippingWard, header.ShippingProvince);
                    order.SetShippingFee(header.ShippingFee);
                    foreach (var item in header.Items)
                    {
                        if (!locked.TryGetValue(item.Variant.Id, out var variant) || !variant.IsActive)
                            throw new DomainException("An imported SKU is no longer available. Preview the file again.");
                        variant.DecreaseStock(item.Quantity);
                        order.AddItem(Guid.NewGuid(), variant.Id, item.ProductNameSnapshot, variant.Sku, item.UnitPrice,
                            item.Quantity, variant.RequiresInstallation, item.ProductImageUrlSnapshot);
                    }
                    order.RecordCreated(Guid.NewGuid(), $"New manual order imported from {source} ({header.ExternalOrderCode}).", NormalizeOptional(changedBy));
                    dbContext.Add(order);
                    created.Add(new(order.Id, order.OrderCode, header.ExternalOrderCode));
                }
                await dbContext.SaveChangesAsync(ct);
                result = Result<AdminOrderImportCommitResultDto>.Success(new(created.Count, created.Concat(plan.ExistingOrders).ToArray(), plan.Preview) { SkippedOrders = plan.ExistingOrders.Count });
            }, cancellationToken);
        }
        catch (DomainException e) { return Result<AdminOrderImportCommitResultDto>.Conflict(e.Message); }
        catch (PersistenceConcurrencyException) { return Result<AdminOrderImportCommitResultDto>.Conflict("Import changed concurrently. Retry with the same source and file."); }
        return result!;
    }

    private static bool ValidImportSource(string source) => source.Length is >= 1 and <= 80 &&
        source.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.');

    private static string ImportFingerprint(IEnumerable<AdminOrderImportFileRow> rows)
    {
        var canonical = rows.OrderBy(r => r.Sku.Trim().ToUpperInvariant(), StringComparer.Ordinal).Select(r => new {
            Code = r.ExternalOrderCode.Trim().ToUpperInvariant(), r.CustomerName, r.CustomerPhone, r.CustomerEmail,
            r.ShippingStreet, r.ShippingWard, r.ShippingProvince, r.Note,
            Method = ParsePaymentMethod(r.PaymentMethod), Sku = r.Sku.Trim().ToUpperInvariant(), Quantity = TryParsePositiveInt(r.Quantity),
            UnitPrice = string.IsNullOrWhiteSpace(r.UnitPrice) ? "catalog" : TryParseOptionalMoney(r.UnitPrice)?.ToString(CultureInfo.InvariantCulture),
            ShippingFee = TryParseOptionalMoney(r.ShippingFee) ?? 0m
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(canonical))));
    }

    private async Task TryEarnLoyaltyPointsAsync(Guid orderId, CancellationToken cancellationToken)
    {
        try
        {
            var result = await loyaltyService.EarnForCompletedOrderAsync(orderId, cancellationToken);
            if (result.IsFailure)
            {
                logger.LogWarning(
                    "Could not earn loyalty points for completed order {OrderId}: {Errors}",
                    orderId,
                    string.Join("; ", result.Errors));
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Failed to earn loyalty points for completed order {OrderId}", orderId);
        }
    }

    private async Task<Result<AdminOrderImportPlan>> BuildImportPlanAsync(
        Stream content,
        string fileName,
        CancellationToken cancellationToken, string source = "manual")
    {
        source = source.Trim().ToLowerInvariant();
        if (!ValidImportSource(source)) return Result<AdminOrderImportPlan>.Validation(["Import source must use 1-80 letters, digits, dots, underscores or hyphens."]);
        var parseResult = await OrderImportFileParser.ParseAsync(content, fileName, cancellationToken);
        if (parseResult.IsFailure)
        {
            return Result<AdminOrderImportPlan>.Validation(parseResult.Errors);
        }

        var fileRows = parseResult.Value!.ToArray();
        if (fileRows.Length == 0)
        {
            return Result<AdminOrderImportPlan>.Validation(["Import file does not contain any order rows."]);
        }

        if (fileRows.Length > 1000) return Result<AdminOrderImportPlan>.Validation(["Import at most 1000 rows per file."]);
        var externalCodes = fileRows.Select(r => r.ExternalOrderCode.Trim().ToUpperInvariant()).Distinct().ToArray();
        var existing = (await dbContext.Orders.AsNoTrackingIfEf().Where(o => o.ImportSource == source && externalCodes.Contains(o.ExternalOrderCode!))
            .ToArrayAsyncSafe(cancellationToken)).ToDictionary(o => o.ExternalOrderCode!, StringComparer.OrdinalIgnoreCase);
        var fingerprints = fileRows.GroupBy(r => r.ExternalOrderCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => ImportFingerprint(g), StringComparer.OrdinalIgnoreCase);
        var errorsByRow = fileRows.ToDictionary(row => row.RowNumber, _ => new List<string>());
        var quantitiesByRow = new Dictionary<int, int?>();
        var draftItems = new List<AdminOrderImportItemPlan>();
        var requestedSkus = fileRows.Where(r => !existing.ContainsKey(r.ExternalOrderCode)).Select(r => r.Sku.ToUpperInvariant()).Distinct().ToArray();
        var variantsBySku = (await dbContext.ProductVariants.Where(v => requestedSkus.Contains(v.Sku)).ToArrayAsyncSafe(cancellationToken))
            .GroupBy(variant => variant.Sku, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var productIds = variantsBySku.Values.Select(v => v.ProductId).Distinct().ToArray();
        var productsById = (await dbContext.Products.Where(p => productIds.Contains(p.Id)).ToArrayAsyncSafe(cancellationToken)).ToDictionary(p => p.Id);
        var categoryIds = productsById.Values.Select(p => p.CategoryId).Distinct().ToArray();
        var categoriesById = (await dbContext.Categories.Where(c => categoryIds.Contains(c.Id)).ToArrayAsyncSafe(cancellationToken)).ToDictionary(c => c.Id);
        var primaryImageUrlByProductId = (await dbContext.ProductImages.Where(i => productIds.Contains(i.ProductId)).ToArrayAsyncSafe(cancellationToken))
            .GroupBy(image => image.ProductId)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(image => image.SortOrder).ThenBy(image => image.Id).First().ImageUrl);

        foreach (var row in fileRows)
        {
            if (existing.TryGetValue(row.ExternalOrderCode, out var prior))
            {
                quantitiesByRow[row.RowNumber] = TryParsePositiveInt(row.Quantity);
                if (prior.ImportFingerprint != fingerprints[row.ExternalOrderCode]) errorsByRow[row.RowNumber].Add("This source/external order code was already imported with different data.");
                continue;
            }
            ValidateRequired(row, errorsByRow[row.RowNumber]);

            var quantity = TryParsePositiveInt(row.Quantity);
            quantitiesByRow[row.RowNumber] = quantity;
            if (quantity is null or > 10000)
            {
                errorsByRow[row.RowNumber].Add("Quantity must be an integer between 1 and 10000.");
            }

            var unitPrice = TryParseOptionalMoney(row.UnitPrice);
            if (!string.IsNullOrWhiteSpace(row.UnitPrice) && unitPrice is null)
            {
                errorsByRow[row.RowNumber].Add("Unit price must be a non-negative whole VND amount.");
            }

            var shippingFee = TryParseOptionalMoney(row.ShippingFee);
            if (!string.IsNullOrWhiteSpace(row.ShippingFee) && shippingFee is null)
            {
                errorsByRow[row.RowNumber].Add("Shipping fee must be a non-negative whole VND amount.");
            }

            var paymentMethod = ParsePaymentMethod(row.PaymentMethod);
            if (paymentMethod is null || paymentMethod == PaymentMethod.VNPay)
            {
                errorsByRow[row.RowNumber].Add("Import supports COD or ManualBankTransfer. Use storefront checkout for VNPay gateway orders.");
            }

            if (!variantsBySku.TryGetValue(row.Sku, out var variant))
            {
                errorsByRow[row.RowNumber].Add("SKU was not found.");
                continue;
            }

            if (!variant.IsActive)
            {
                errorsByRow[row.RowNumber].Add("SKU is inactive.");
            }

            if (!productsById.TryGetValue(variant.ProductId, out var product) || !product.IsActive)
            {
                errorsByRow[row.RowNumber].Add("Product for SKU is inactive or missing.");
                continue;
            }

            if (!categoriesById.TryGetValue(product.CategoryId, out var category) || !category.IsActive)
            {
                errorsByRow[row.RowNumber].Add("Category for SKU is inactive or missing.");
            }

            if (quantity is not null && paymentMethod is not null)
            {
                draftItems.Add(new AdminOrderImportItemPlan(
                    row,
                    variant,
                    product.Name.Get(languageProvider.CurrentLanguage),
                    primaryImageUrlByProductId.GetValueOrDefault(product.Id),
                    quantity.Value,
                    unitPrice ?? variant.Price,
                    shippingFee ?? 0m,
                    paymentMethod.Value));
            }
        }

        ValidateOrderGroups(draftItems, errorsByRow);
        ValidateStock(draftItems, errorsByRow);

        var rowResults = fileRows
            .Select(row => new AdminOrderImportRowResultDto(
                row.RowNumber,
                row.ExternalOrderCode,
                row.Sku,
                quantitiesByRow.GetValueOrDefault(row.RowNumber),
                errorsByRow[row.RowNumber].Count == 0,
                errorsByRow[row.RowNumber].Distinct(StringComparer.Ordinal).ToArray()) { AlreadyImported = existing.ContainsKey(row.ExternalOrderCode) })
            .ToArray();

        var validRowNumbers = rowResults
            .Where(row => row.IsValid)
            .Select(row => row.RowNumber)
            .ToHashSet();

        var orders = draftItems
            .GroupBy(item => item.Row.ExternalOrderCode, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.All(item => validRowNumbers.Contains(item.Row.RowNumber)))
            .Select(ToOrderPlan)
            .ToArray();

        var preview = new AdminOrderImportPreviewDto(
            rowResults.Length,
            rowResults.Count(row => row.IsValid),
            rowResults.Count(row => !row.IsValid),
            rowResults);

        return Result<AdminOrderImportPlan>.Success(new AdminOrderImportPlan(preview, orders) {
            ExistingOrders = existing.Values.Where(o => o.ImportFingerprint == fingerprints[o.ExternalOrderCode!])
                .Select(o => new AdminOrderImportCreatedOrderDto(o.Id, o.OrderCode, o.ExternalOrderCode!) { AlreadyImported = true }).ToArray()
        });
    }

    private static void ValidateRequired(AdminOrderImportFileRow row, ICollection<string> errors)
    {
        if (row.ExternalOrderCode.Length > 200 || row.CustomerName.Length > 250 || row.CustomerPhone.Length > 50 ||
            row.CustomerEmail?.Length > 250 || row.ShippingStreet.Length > 500 || row.ShippingWard.Length > 250 ||
            row.ShippingProvince.Length > 250 || row.Note?.Length > 1000)
            errors.Add("An import value exceeds the allowed field length.");
        if (!string.IsNullOrWhiteSpace(row.CustomerEmail) &&
            (!System.Net.Mail.MailAddress.TryCreate(row.CustomerEmail, out var email) || email.Address != row.CustomerEmail))
            errors.Add("Customer email is invalid.");

        if (string.IsNullOrWhiteSpace(row.ExternalOrderCode))
        {
            errors.Add("External order code is required.");
        }

        if (string.IsNullOrWhiteSpace(row.CustomerName))
        {
            errors.Add("Customer name is required.");
        }

        if (string.IsNullOrWhiteSpace(row.CustomerPhone))
        {
            errors.Add("Customer phone is required.");
        }

        if (string.IsNullOrWhiteSpace(row.ShippingStreet))
        {
            errors.Add("Shipping street is required.");
        }

        if (string.IsNullOrWhiteSpace(row.ShippingWard))
        {
            errors.Add("Shipping ward is required.");
        }

        if (string.IsNullOrWhiteSpace(row.ShippingProvince))
        {
            errors.Add("Shipping province is required.");
        }

        if (string.IsNullOrWhiteSpace(row.Sku))
        {
            errors.Add("SKU is required.");
        }
    }

    private static void ValidateOrderGroups(
        IReadOnlyCollection<AdminOrderImportItemPlan> draftItems,
        IReadOnlyDictionary<int, List<string>> errorsByRow)
    {
        foreach (var group in draftItems.GroupBy(item => item.Row.ExternalOrderCode, StringComparer.OrdinalIgnoreCase))
        {
            var first = group.First();
            if (group.Sum(item => item.UnitPrice * item.Quantity) + first.ShippingFee > 99999999999999m)
                foreach (var item in group) errorsByRow[item.Row.RowNumber].Add("Order amount exceeds the supported VND range.");
            foreach (var item in group)
            {
                if (!SameOrderHeader(first, item))
                {
                    errorsByRow[item.Row.RowNumber].Add("Rows with the same external order code must use the same customer, shipping, payment, and note values.");
                }
            }

            foreach (var duplicateSkuGroup in group.GroupBy(item => item.Variant.Sku, StringComparer.OrdinalIgnoreCase).Where(skuGroup => skuGroup.Count() > 1))
            {
                foreach (var item in duplicateSkuGroup)
                {
                    errorsByRow[item.Row.RowNumber].Add("The same SKU cannot appear more than once in an order.");
                }
            }
        }
    }

    private static void ValidateStock(
        IReadOnlyCollection<AdminOrderImportItemPlan> draftItems,
        IReadOnlyDictionary<int, List<string>> errorsByRow)
    {
        foreach (var group in draftItems.GroupBy(item => item.Variant.Id))
        {
            var requestedQuantity = group.Sum(item => (long)item.Quantity);
            var variant = group.First().Variant;
            if (requestedQuantity <= variant.StockQuantity)
            {
                continue;
            }

            foreach (var item in group)
            {
                errorsByRow[item.Row.RowNumber].Add(
                    $"Requested quantity for SKU {variant.Sku} exceeds available stock ({variant.StockQuantity}).");
            }
        }
    }

    private static bool SameOrderHeader(AdminOrderImportItemPlan left, AdminOrderImportItemPlan right)
    {
        return string.Equals(left.Row.CustomerName, right.Row.CustomerName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(left.Row.CustomerPhone, right.Row.CustomerPhone, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(NormalizeOptional(left.Row.CustomerEmail), NormalizeOptional(right.Row.CustomerEmail), StringComparison.OrdinalIgnoreCase) &&
            string.Equals(left.Row.ShippingAddress, right.Row.ShippingAddress, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(left.Row.ShippingStreet, right.Row.ShippingStreet, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(left.Row.ShippingWard, right.Row.ShippingWard, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(left.Row.ShippingProvince, right.Row.ShippingProvince, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(NormalizeOptional(left.Row.Note), NormalizeOptional(right.Row.Note), StringComparison.OrdinalIgnoreCase) &&
            left.PaymentMethod == right.PaymentMethod &&
            left.ShippingFee == right.ShippingFee;
    }

    private static AdminOrderImportOrderPlan ToOrderPlan(IGrouping<string, AdminOrderImportItemPlan> group)
    {
        var first = group.First();
        var shippingAddress = string.IsNullOrWhiteSpace(first.Row.ShippingAddress)
            ? $"{first.Row.ShippingStreet}, {first.Row.ShippingWard}, {first.Row.ShippingProvince}"
            : first.Row.ShippingAddress;

        return new AdminOrderImportOrderPlan(
            first.Row.ExternalOrderCode,
            first.Row.CustomerName,
            first.Row.CustomerPhone,
            NormalizeOptional(first.Row.CustomerEmail),
            shippingAddress,
            first.Row.ShippingStreet,
            first.Row.ShippingWard,
            first.Row.ShippingProvince,
            first.PaymentMethod,
            first.ShippingFee,
            NormalizeOptional(first.Row.Note),
            group.ToArray());
    }

    private async Task<string> GenerateOrderCodeAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var orderCode = $"ORD-{DateTimeOffset.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..21].ToUpperInvariant();
            if (!await dbContext.Orders
                    .Where(order => order.OrderCode == orderCode)
                    .AnyAsyncSafe(cancellationToken))
            {
                return orderCode;
            }
        }
        throw new DomainException("Could not generate a unique order code.");
    }

    private string GetCurrencyCode()
    {
        return CommerceCurrency.Code;
    }

    private decimal GetExchangeRate()
    {
        return CommerceCurrency.BaseExchangeRate;
    }

    private static int? TryParsePositiveInt(string value)
    {
        if (int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var integer) && integer > 0)
        {
            return integer;
        }

        if (decimal.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number) &&
            number > 0 &&
            decimal.Truncate(number) == number &&
            number <= int.MaxValue)
        {
            return (int)number;
        }

        return null;
    }

    private static decimal? TryParseOptionalMoney(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return decimal.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount) && amount >= 0m && amount == decimal.Truncate(amount) && amount <= 999999999999m
            ? amount
            : null;
    }

    private static PaymentMethod? ParsePaymentMethod(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return PaymentMethod.Cod;
        }

        var normalized = value
            .Trim()
            .Replace(" ", "", StringComparison.Ordinal)
            .Replace("_", "", StringComparison.Ordinal)
            .Replace("-", "", StringComparison.Ordinal)
            .ToLowerInvariant();

        return normalized switch
        {
            "0" or "cod" or "cash" or "cashondelivery" => PaymentMethod.Cod,
            "1" or "bank" or "manualbanktransfer" or "banktransfer" => PaymentMethod.ManualBankTransfer,
            "2" or "vnpay" => PaymentMethod.VNPay,
            _ => null
        };
    }

    private async Task<AdminOrderDto> ToDetailDtoAsync(Order order, CancellationToken cancellationToken)
    {
        var items = await dbContext.OrderItems
            .AsNoTrackingIfEf()
            .Where(item => item.OrderId == order.Id)
            .OrderBy(item => item.SkuSnapshot)
            .ThenBy(item => item.Id)
            .Select(item => ToItemDto(item))
            .ToArrayAsyncSafe(cancellationToken);
        var statusHistory = await dbContext.OrderStatusHistories
            .AsNoTrackingIfEf()
            .Where(history => history.OrderId == order.Id)
            .OrderBy(history => history.ChangedAt)
            .ThenBy(history => history.Id)
            .Select(history => ToStatusHistoryDto(history))
            .ToArrayAsyncSafe(cancellationToken);

        return new AdminOrderDto(
            order.Id,
            order.OrderCode,
            order.CustomerId,
            order.CustomerName,
            order.CustomerPhone,
            order.CustomerEmail,
            order.ShippingAddress,
            order.Note,
            order.CouponId,
            order.CouponCodeSnapshot,
            order.CouponNameSnapshot,
            order.Subtotal,
            order.ShippingFee,
            order.DiscountAmount,
            order.TotalAmount,
            order.Status,
            order.PaymentMethod,
            order.PaymentStatus,
            order.PaidAt,
            order.CreatedAt,
            order.UpdatedAt,
            order.TrackingCode,
            order.ShipmentId,
            items,
            statusHistory) { RefundedAmount = order.RefundedAmount, ImportSource = order.ImportSource, PaymentDueAt = order.PaymentDueAt, ReturnRequestedAt = order.ReturnRequestedAt, ReturnReason = order.ReturnReason, Shipping = shippingReader is null ? null : await shippingReader.GetAsync(order.Id, cancellationToken) };
    }

    private static AdminOrderListItemDto ToListItemDto(Order order, int itemCount, string? shipmentStatus = null)
    {
        return new AdminOrderListItemDto(
            order.Id,
            order.OrderCode,
            order.CustomerName,
            order.CustomerPhone,
            order.TotalAmount,
            order.Status,
            order.PaymentMethod,
            order.PaymentStatus,
            order.PaidAt,
            order.CreatedAt,
            order.UpdatedAt,
            itemCount,
            order.TrackingCode,
            shipmentStatus);
    }

    private static OrderItemDto ToItemDto(OrderItem item)
    {
        return new OrderItemDto(
            item.Id,
            item.ProductVariantId,
            item.ProductNameSnapshot,
            item.ProductImageUrlSnapshot,
            item.SkuSnapshot,
            item.UnitPrice,
            item.Quantity,
            item.LineTotal,
            item.RequiresInstallation);
    }

    private static AdminOrderStatusHistoryDto ToStatusHistoryDto(OrderStatusHistory history)
    {
        return new AdminOrderStatusHistoryDto(
            history.Id,
            history.FromStatus,
            history.ToStatus,
            history.Note,
            history.CancellationReason,
            history.CustomerMessage,
            history.ChangedBy,
            history.ChangedAt);
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}

internal sealed record AdminOrderImportPlan(
    AdminOrderImportPreviewDto Preview,
    IReadOnlyCollection<AdminOrderImportOrderPlan> Orders)
{
    public IReadOnlyCollection<AdminOrderImportCreatedOrderDto> ExistingOrders { get; init; } = [];
}

internal sealed record AdminOrderImportOrderPlan(
    string ExternalOrderCode,
    string CustomerName,
    string CustomerPhone,
    string? CustomerEmail,
    string ShippingAddress,
    string ShippingStreet,
    string ShippingWard,
    string ShippingProvince,
    PaymentMethod PaymentMethod,
    decimal ShippingFee,
    string? Note,
    IReadOnlyCollection<AdminOrderImportItemPlan> Items);

internal sealed record AdminOrderImportItemPlan(
    AdminOrderImportFileRow Row,
    ProductVariant Variant,
    string ProductNameSnapshot,
    string? ProductImageUrlSnapshot,
    int Quantity,
    decimal UnitPrice,
    decimal ShippingFee,
    PaymentMethod PaymentMethod);
