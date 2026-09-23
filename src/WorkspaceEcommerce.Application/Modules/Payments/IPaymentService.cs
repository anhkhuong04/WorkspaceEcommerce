using WorkspaceEcommerce.Application.Common.Models;

namespace WorkspaceEcommerce.Application.Modules.Payments;

public interface IPaymentService
{
    Task<Result<PaymentResultDto>> HandleVNPayReturnAsync(
        VNPayCallbackRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<VNPayIpnResponseDto>> HandleVNPayIpnAsync(
        VNPayCallbackRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<PublicPaymentResultDto>> GetPaymentResultAsync(
        string orderCode,
        string? resultAccessToken = null,
        CancellationToken cancellationToken = default);
}
