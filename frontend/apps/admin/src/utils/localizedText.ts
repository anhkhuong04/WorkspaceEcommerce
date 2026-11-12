export type LocalizedTextValue = string | Record<string, string> | null | undefined;

export function localizedValue(value: LocalizedTextValue, language: "vi" | "en"): string {
  return typeof value === "string" ? value : value?.[language] ?? "";
}

export function updateTranslations(original: LocalizedTextValue, vi: string, en: string): Record<string, string> {
  const translations = typeof original === "object" && original ? { ...original } : {};
  translations.vi = vi.trim();
  if (en.trim()) translations.en = en.trim();
  else delete translations.en;
  return translations;
}

export function formatLocalizedText(value: LocalizedTextValue, fallback = "-"): string {
  if (typeof value === "string") {
    return value;
  }

  if (!value || typeof value !== "object") {
    return fallback;
  }

  return value.en || value.vi || Object.values(value).find(Boolean) || fallback;
}
