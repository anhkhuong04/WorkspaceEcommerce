import { useEffect, useRef, useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { normalizeLanguage, type SupportedLanguage } from "../../i18n";

const languages: ReadonlyArray<{
  code: SupportedLanguage;
  labelKey: "language.english" | "language.vietnamese";
  image: string;
}> = [
  { code: "en", labelKey: "language.english", image: "/demo/united-states.png" },
  { code: "vi", labelKey: "language.vietnamese", image: "/demo/vietnam.png" },
];

interface LanguageSwitcherProps {
  isHeaderSolid: boolean;
}

export function LanguageSwitcher({ isHeaderSolid }: LanguageSwitcherProps) {
  const { t, i18n } = useTranslation();
  const queryClient = useQueryClient();
  const containerRef = useRef<HTMLDivElement>(null);
  const [isOpen, setIsOpen] = useState(false);
  const currentLanguage = normalizeLanguage(i18n.resolvedLanguage ?? i18n.language);
  const selectedLanguage = languages.find((language) => language.code === currentLanguage) ?? languages[0];

  useEffect(() => {
    if (!isOpen) {
      return;
    }

    function handlePointerDown(event: PointerEvent) {
      if (!containerRef.current?.contains(event.target as Node)) {
        setIsOpen(false);
      }
    }

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === "Escape") {
        setIsOpen(false);
      }
    }

    document.addEventListener("pointerdown", handlePointerDown);
    document.addEventListener("keydown", handleKeyDown);
    return () => {
      document.removeEventListener("pointerdown", handlePointerDown);
      document.removeEventListener("keydown", handleKeyDown);
    };
  }, [isOpen]);

  async function selectLanguage(language: SupportedLanguage) {
    setIsOpen(false);
    if (language === currentLanguage) {
      return;
    }

    await i18n.changeLanguage(language);
    await queryClient.invalidateQueries();
  }

  return (
    <div ref={containerRef} className="relative">
      <button
        type="button"
        onClick={() => setIsOpen((open) => !open)}
        className={`grid h-10 w-10 place-items-center rounded-full transition ${isHeaderSolid ? "hover:bg-slate-100" : "hover:bg-white/20"}`}
        aria-label={t("language.select")}
        aria-haspopup="menu"
        aria-expanded={isOpen}
      >
        <img
          src={selectedLanguage.image}
          alt=""
          className="h-6 w-6 rounded-full object-cover ring-1 ring-black/10"
        />
      </button>

      {isOpen ? (
        <div
          role="menu"
          aria-label={t("language.select")}
          className="absolute right-0 top-full z-[70] mt-3 w-44 overflow-hidden rounded-2xl border border-slate-100 bg-white p-2 text-slate-950 shadow-[0_18px_45px_rgba(15,23,42,0.18)]"
        >
          {languages.map((language) => {
            const isSelected = language.code === currentLanguage;
            return (
              <button
                key={language.code}
                type="button"
                role="menuitemradio"
                aria-checked={isSelected}
                onClick={() => void selectLanguage(language.code)}
                className={`flex w-full items-center gap-3 rounded-xl px-3 py-2.5 text-left text-sm font-semibold transition hover:bg-slate-50 ${isSelected ? "bg-slate-100 text-[var(--brand)]" : ""}`}
              >
                <img src={language.image} alt="" className="h-6 w-6 rounded-full object-cover ring-1 ring-black/10" />
                <span>{t(language.labelKey)}</span>
                {isSelected ? <span className="ml-auto" aria-hidden="true">✓</span> : null}
              </button>
            );
          })}
        </div>
      ) : null}
    </div>
  );
}
