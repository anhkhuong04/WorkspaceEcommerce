// Public merchant information only. Never put provider credentials in VITE_*.
export const storefrontInfo = {
  supportEmail: String(import.meta.env.VITE_SHOP_SUPPORT_EMAIL ?? "").trim(),
  supportPhone: String(import.meta.env.VITE_SHOP_SUPPORT_PHONE ?? "").trim(),
  bankName: String(import.meta.env.VITE_SHOP_BANK_NAME ?? "").trim(),
  bankAccountName: String(import.meta.env.VITE_SHOP_BANK_ACCOUNT_NAME ?? "").trim(),
  bankAccountNumber: String(import.meta.env.VITE_SHOP_BANK_ACCOUNT_NUMBER ?? "").trim(),
};
export const bankTransferConfigured = Boolean(storefrontInfo.bankName && storefrontInfo.bankAccountName && storefrontInfo.bankAccountNumber);
