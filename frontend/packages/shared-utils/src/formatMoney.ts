export function formatMoney(value: number, currency = "USD"): string {
  // Base currency is USD, but let's check what value represents.
  // Actually, wait, if the database stores 100 as 100 USD.
  // If currency is VND, we apply exchange rate. 1 USD = 26,000 VND.
  let convertedValue = value;
  let locale = "en-US";
  let fractionDigits = 0;

  if (currency === "VND") {
    convertedValue = value * 26000;
    locale = "vi-VN";
    fractionDigits = 0;
  } else {
    // Default to USD
    locale = "en-US";
    fractionDigits = 2;
  }

  return new Intl.NumberFormat(locale, {
    style: "currency",
    currency,
    maximumFractionDigits: fractionDigits,
    minimumFractionDigits: fractionDigits,
  }).format(convertedValue);
}
