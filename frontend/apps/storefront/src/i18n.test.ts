import { afterEach, describe, expect, it } from "vitest";
import i18n, { languageStorageKey, normalizeLanguage } from "./i18n";

describe("storefront language configuration", () => {
  afterEach(async () => {
    window.localStorage.clear();
    await i18n.changeLanguage("en");
  });

  it("normalizes supported Vietnamese language variants", () => {
    expect(normalizeLanguage("vi")).toBe("vi");
    expect(normalizeLanguage("vi-VN")).toBe("vi");
    expect(normalizeLanguage("VI_vi")).toBe("vi");
  });

  it("falls back to English for unsupported or missing languages", () => {
    expect(normalizeLanguage("en-US")).toBe("en");
    expect(normalizeLanguage("fr")).toBe("en");
    expect(normalizeLanguage(null)).toBe("en");
  });

  it("persists a language change and updates the document language", async () => {
    await i18n.changeLanguage("vi");

    expect(window.localStorage.getItem(languageStorageKey)).toBe("vi");
    expect(document.documentElement.lang).toBe("vi");
    expect(i18n.t("header.products")).toBe("Sản phẩm");
  });
});
