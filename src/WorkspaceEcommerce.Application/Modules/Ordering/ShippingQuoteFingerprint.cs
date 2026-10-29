using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WorkspaceEcommerce.Application.Modules.Shipments;

namespace WorkspaceEcommerce.Application.Modules.Ordering;

internal static class ShippingQuoteFingerprint
{
    public static string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));
    public static string Subject(Guid? customerId, string session) => Hash(new { customerId, session = session.Trim() });
    public static string Inputs(ShippingQuoteInput input, string catalogVersion, decimal discount, IEnumerable<CheckoutItemSnapshot> items) => Hash(new
    {
        session = input.SessionId.Trim(),
        street = input.ShippingStreet.Trim(),
        input.ProvinceCode,
        input.WardCode,
        input.Method,
        input.PaymentMethod,
        coupon = CheckoutCouponApplier.NormalizeCouponCode(input.CouponCode),
        catalogVersion,
        discount,
        items = items.OrderBy(x => x.Variant.Id).Select(x => new { x.Variant.Id, x.Quantity, x.UnitPrice, x.WeightKg, x.LengthCm, x.WidthCm, x.HeightCm })
    });
    public static string Acceptance(CheckoutRequest request) => Hash(new
    {
        session = request.SessionId.Trim(),
        request.ShippingQuoteId,
        request.ShippingMethod,
        street = request.ShippingStreet.Trim(),
        request.ShippingProvinceCode,
        request.ShippingWardCode,
        name = request.CustomerName.Trim(),
        phone = request.CustomerPhone.Trim(),
        email = request.CustomerEmail?.Trim(),
        request.Note,
        request.PaymentMethod,
        coupon = CheckoutCouponApplier.NormalizeCouponCode(request.CouponCode)
    });
}
