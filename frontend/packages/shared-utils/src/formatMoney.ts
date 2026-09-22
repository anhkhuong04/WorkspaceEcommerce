const DEFAULT_CURRENCY = "VND";

/**
 * Formats an amount already stored in the requested currency.
 * No implicit exchange-rate conversion is performed here.
 */
export function formatMoney(value: number, currency = DEFAULT_CURRENCY): string {
  const isVietnameseDong = currency === DEFAULT_CURRENCY;

  return new Intl.NumberFormat(isVietnameseDong ? "vi-VN" : "en-US", {
    style: "currency",
    currency,
    minimumFractionDigits: isVietnameseDong ? 0 : 2,
    maximumFractionDigits: isVietnameseDong ? 0 : 2,
  }).format(value);
}
