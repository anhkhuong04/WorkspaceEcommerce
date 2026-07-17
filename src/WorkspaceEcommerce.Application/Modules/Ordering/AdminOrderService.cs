using FluentValidation;
using Microsoft.Extensions.Logging;
using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Application.Common.Localization;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Modules.Loyalty;
using WorkspaceEcommerce.Domain.Modules.Catalog;
using WorkspaceEcommerce.Domain.Common;
using WorkspaceEcommerce.Domain.Modules.Ordering;

namespace WorkspaceEcommerce.Application.Modules.Ordering;

internal sealed class AdminOrderService(
    IAppDbContext dbContext,
    IValidator<AdminOrderListRequest> listValidator,
    IValidator<UpdateOrderStatusRequest> updateStatusValidator,
    ILoyaltyService loyaltyService,
    ICurrentLanguageProvider languageProvider,
    ILogger<AdminOrderService> logger) : IAdminOrderService
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

        var normalizedSearch = NormalizeOptional(request.Search);
        var status = request.Status;
        var orders = dbContext.Orders
            .Where(order => !status.HasValue || order.Status == status.Value)
            .ToArray()
            .Where(order => normalizedSearch is null || MatchesSearch(order, normalizedSearch))
            .OrderByDescending(order => order.CreatedAt)
            .ThenByDescending(order => order.OrderCode)
            .ToArray();

        var itemCountsByOrderId = dbContext.OrderItems
            .GroupBy(item => item.OrderId)
            .ToDictionary(group => group.Key, group => group.Count());
        var pageNumber = request.NormalizedPageNumber;
        var pageSize = request.NormalizedPageSize;
        var items = orders
            .Skip(request.Skip)
            .Take(pageSize)
            .Select(order => ToListItemDto(order, itemCountsByOrderId.GetValueOrDefault(order.Id)))
            .ToArray();
        var page = new PagedResult<AdminOrderListItemDto>(
            items,
            pageNumber,
            pageSize,
            orders.Length);

        return Result<PagedResult<AdminOrderListItemDto>>.Success(page);
    }

    public Task<Result<AdminOrderDto>> GetOrderByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var order = dbContext.Orders.FirstOrDefault(existing => existing.Id == id);
        if (order is null)
        {
            return Task.FromResult(Result<AdminOrderDto>.NotFound("Order was not found."));
        }

        return Task.FromResult(Result<AdminOrderDto>.Success(ToDetailDto(order)));
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

        var order = dbContext.Orders.FirstOrDefault(existing => existing.Id == id);
        if (order is null)
        {
            return Result<AdminOrderDto>.NotFound("Order was not found.");
        }

        try
        {
            var history = order.ChangeStatus(
                Guid.NewGuid(),
                request.Status,
                request.Note,
                NormalizeOptional(changedBy));

            dbContext.Add(history);
            await dbContext.SaveChangesAsync(cancellationToken);

            if (request.Status == OrderStatus.Completed)
            {
                await TryEarnLoyaltyPointsAsync(order.Id, cancellationToken);
            }

            return Result<AdminOrderDto>.Success(ToDetailDto(order));
        }
        catch (DomainException exception)
        {
            return Result<AdminOrderDto>.Conflict(exception.Message);
        }
    }

    public async Task<Result<AdminOrderImportPreviewDto>> PreviewImportAsync(
        Stream content,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        var planResult = await BuildImportPlanAsync(content, fileName, cancellationToken);
        return planResult.IsFailure
            ? Result<AdminOrderImportPreviewDto>.Validation(planResult.Errors)
            : Result<AdminOrderImportPreviewDto>.Success(planResult.Value!.Preview);
    }

    public async Task<Result<AdminOrderImportCommitResultDto>> CommitImportAsync(
        Stream content,
        string fileName,
        string? changedBy,
        CancellationToken cancellationToken = default)
    {
        var planResult = await BuildImportPlanAsync(content, fileName, cancellationToken);
        if (planResult.IsFailure)
        {
            return Result<AdminOrderImportCommitResultDto>.Validation(planResult.Errors);
        }

        var plan = planResult.Value!;
        if (plan.Preview.ErrorRows > 0)
        {
            return Result<AdminOrderImportCommitResultDto>.Success(new AdminOrderImportCommitResultDto(
                0,
                Array.Empty<AdminOrderImportCreatedOrderDto>(),
                plan.Preview));
        }

        if (plan.Orders.Count == 0)
        {
            return Result<AdminOrderImportCommitResultDto>.Validation(["Import file does not contain valid orders."]);
        }

        var createdOrders = new List<AdminOrderImportCreatedOrderDto>();
        try
        {
            await dbContext.ExecuteInTransactionAsync(async transactionCancellationToken =>
            {
                foreach (var orderPlan in plan.Orders)
                {
                    var order = new Order(
                        Guid.NewGuid(),
                        await GenerateOrderCodeAsync(transactionCancellationToken),
                        customerId: null,
                        orderPlan.CustomerName,
                        orderPlan.CustomerPhone,
                        orderPlan.CustomerEmail,
                        orderPlan.ShippingAddress,
                        orderPlan.Note,
                        orderPlan.PaymentMethod,
                        GetCurrencyCode(),
                        GetExchangeRate());

                    order.SetShippingAddressDetails(
                        orderPlan.ShippingStreet,
                        orderPlan.ShippingWard,
                        orderPlan.ShippingProvince);
                    order.SetShippingFee(orderPlan.ShippingFee);

                    foreach (var item in orderPlan.Items)
                    {
                        item.Variant.DecreaseStock(item.Quantity);
                        dbContext.Update(item.Variant);
                        order.AddItem(
                            Guid.NewGuid(),
                            item.Variant.Id,
                            item.ProductNameSnapshot,
                            item.Variant.Sku,
                            item.UnitPrice,
                            item.Quantity,
                            item.Variant.RequiresInstallation);
                    }

                    order.RecordCreated(Guid.NewGuid(), $"Created by admin import ({orderPlan.ExternalOrderCode}).", NormalizeOptional(changedBy));
                    dbContext.Add(order);
                    createdOrders.Add(new AdminOrderImportCreatedOrderDto(order.Id, order.OrderCode, orderPlan.ExternalOrderCode));
                }

                await dbContext.SaveChangesAsync(transactionCancellationToken);
            }, cancellationToken);
        }
        catch (DomainException exception)
        {
            return Result<AdminOrderImportCommitResultDto>.Conflict(exception.Message);
        }

        return Result<AdminOrderImportCommitResultDto>.Success(new AdminOrderImportCommitResultDto(
            createdOrders.Count,
            createdOrders,
            plan.Preview));
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
        CancellationToken cancellationToken)
    {
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

        var errorsByRow = fileRows.ToDictionary(row => row.RowNumber, _ => new List<string>());
        var quantitiesByRow = new Dictionary<int, int?>();
        var draftItems = new List<AdminOrderImportItemPlan>();
        var variantsBySku = dbContext.ProductVariants
            .ToArray()
            .GroupBy(variant => variant.Sku, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var productsById = dbContext.Products.ToDictionary(product => product.Id);
        var categoriesById = dbContext.Categories.ToDictionary(category => category.Id);

        foreach (var row in fileRows)
        {
            ValidateRequired(row, errorsByRow[row.RowNumber]);

            var quantity = TryParsePositiveInt(row.Quantity);
            quantitiesByRow[row.RowNumber] = quantity;
            if (quantity is null)
            {
                errorsByRow[row.RowNumber].Add("Quantity must be a positive integer.");
            }

            var unitPrice = TryParseOptionalMoney(row.UnitPrice);
            if (!string.IsNullOrWhiteSpace(row.UnitPrice) && unitPrice is null)
            {
                errorsByRow[row.RowNumber].Add("Unit price must be a non-negative number.");
            }

            var shippingFee = TryParseOptionalMoney(row.ShippingFee);
            if (!string.IsNullOrWhiteSpace(row.ShippingFee) && shippingFee is null)
            {
                errorsByRow[row.RowNumber].Add("Shipping fee must be a non-negative number.");
            }

            var paymentMethod = ParsePaymentMethod(row.PaymentMethod);
            if (paymentMethod is null)
            {
                errorsByRow[row.RowNumber].Add("Payment method must be COD, ManualBankTransfer, or VNPay.");
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
                errorsByRow[row.RowNumber].Distinct(StringComparer.Ordinal).ToArray()))
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

        return Result<AdminOrderImportPlan>.Success(new AdminOrderImportPlan(preview, orders));
    }

    private static void ValidateRequired(AdminOrderImportFileRow row, ICollection<string> errors)
    {
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
            var requestedQuantity = group.Sum(item => item.Quantity);
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
            if (!dbContext.Orders.Any(order => order.OrderCode == orderCode))
            {
                return orderCode;
            }
        }

        await Task.CompletedTask;
        throw new DomainException("Could not generate a unique order code.");
    }

    private string GetCurrencyCode()
    {
        return languageProvider.CurrentLanguage == "vi" ? "VND" : "USD";
    }

    private decimal GetExchangeRate()
    {
        return languageProvider.CurrentLanguage == "vi" ? 26000m : 1m;
    }

    private static int? TryParsePositiveInt(string value)
    {
        if (int.TryParse(value, out var integer) && integer > 0)
        {
            return integer;
        }

        if (decimal.TryParse(value, out var number) &&
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

        return decimal.TryParse(value, out var amount) && amount >= 0m
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

    private AdminOrderDto ToDetailDto(Order order)
    {
        var items = dbContext.OrderItems
            .Where(item => item.OrderId == order.Id)
            .OrderBy(item => item.SkuSnapshot)
            .ThenBy(item => item.Id)
            .ToArray();
        var statusHistory = dbContext.OrderStatusHistories
            .Where(history => history.OrderId == order.Id)
            .OrderBy(history => history.ChangedAt)
            .ThenBy(history => history.Id)
            .ToArray();

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
            items.Select(ToItemDto).ToArray(),
            statusHistory.Select(ToStatusHistoryDto).ToArray());
    }

    private static AdminOrderListItemDto ToListItemDto(Order order, int itemCount)
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
            itemCount);
    }

    private static OrderItemDto ToItemDto(OrderItem item)
    {
        return new OrderItemDto(
            item.Id,
            item.ProductVariantId,
            item.ProductNameSnapshot,
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
            history.ChangedBy,
            history.ChangedAt);
    }

    private static bool MatchesSearch(Order order, string search)
    {
        return order.OrderCode.Contains(search, StringComparison.OrdinalIgnoreCase) ||
            order.CustomerName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
            order.CustomerPhone.Contains(search, StringComparison.OrdinalIgnoreCase);
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
    IReadOnlyCollection<AdminOrderImportOrderPlan> Orders);

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
    int Quantity,
    decimal UnitPrice,
    decimal ShippingFee,
    PaymentMethod PaymentMethod);
