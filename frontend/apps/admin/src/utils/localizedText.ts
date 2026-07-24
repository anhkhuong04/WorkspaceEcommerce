export type LocalizedTextValue = string | Record<string, string> | null | undefined;

export function formatLocalizedText(value: LocalizedTextValue, fallback = "-"): string {
  if (typeof value === "string") {
    return value;
  }

  if (!value || typeof value !== "object") {
    return fallback;
  }

  return value.en || value.vi || Object.values(value).find(Boolean) || fallback;
}
