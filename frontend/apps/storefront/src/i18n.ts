import i18n from "i18next";
import { initReactI18next } from "react-i18next";
import en from "./locales/en.json";
import vi from "./locales/vi.json";

export const supportedLanguages = ["en", "vi"] as const;
export type SupportedLanguage = (typeof supportedLanguages)[number];

export const languageStorageKey = "workspace-ecommerce-language";

export function normalizeLanguage(language?: string | null): SupportedLanguage {
  return language?.toLowerCase().startsWith("vi") ? "vi" : "en";
}

function getInitialLanguage(): SupportedLanguage {
  if (typeof window === "undefined") {
    return "en";
  }

  try {
    return normalizeLanguage(window.localStorage.getItem(languageStorageKey));
  } catch {
    return "en";
  }
}

function syncDocumentLanguage(language?: string | null) {
  if (typeof document !== "undefined") {
    document.documentElement.lang = normalizeLanguage(language);
  }
}

void i18n.use(initReactI18next).init({
  resources: {
    en: { translation: en },
    vi: { translation: vi },
  },
  lng: getInitialLanguage(),
  fallbackLng: "en",
  supportedLngs: supportedLanguages,
  load: "languageOnly",
  interpolation: {
    escapeValue: false,
  },
});

syncDocumentLanguage(i18n.language);
i18n.on("languageChanged", (language) => {
  const normalizedLanguage = normalizeLanguage(language);
  syncDocumentLanguage(normalizedLanguage);

  try {
    window.localStorage.setItem(languageStorageKey, normalizedLanguage);
  } catch {
    // Language switching still works when storage is unavailable.
  }
});

export default i18n;
