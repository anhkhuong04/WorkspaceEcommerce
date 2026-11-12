import { useState } from "react";
import { useTranslation } from "react-i18next";
import type { StorefrontBannerDto } from "@workspace-ecommerce/api-types";
import { BannerArtwork } from "./BannerArtwork";

interface BannerCarouselProps {
  banners: StorefrontBannerDto[];
  isLoading?: boolean;
  label?: string;
}
export function BannerCarousel({
  banners,
  isLoading = false,
  label,
}: BannerCarouselProps) {
  const { t } = useTranslation();
  const [currentIndex, setCurrentIndex] = useState(0);
  const total = banners.length;
  const activeIndex = currentIndex % Math.max(total, 1);

  function goTo(index: number) {
    setCurrentIndex((index + total) % total);
  }

  if (isLoading)
    return (
      <div
        className="aspect-[5/2] animate-pulse bg-[var(--brand-soft)]"
        role="status"
        aria-label={t("banner.loading")}
      />
    );
  if (total === 0) return null;

  return (
    <section
      className="min-w-0 bg-white"
      aria-label={label ?? t("home.heroBanners")}
      aria-roledescription={t("banner.carousel")}
    >
      <div className="banner-carousel-viewport">
        {banners.map((banner, index) => {
          let offset = (index - activeIndex + total) % total;
          if (offset > total / 2) offset -= total;
          return (
            <div
              key={banner.id}
              inert={index !== activeIndex}
              aria-hidden={index !== activeIndex}
              data-active={index === activeIndex}
              className="banner-carousel-slide"
              style={{
                transform: `translateX(calc(${offset * 100}% + ${offset * 8}px))`,
              }}
            >
              <BannerArtwork
                banner={banner}
                number={index + 4}
                frame="highlight"
              />
            </div>
          );
        })}
        {total === 2 ? (
          <div
            inert
            aria-hidden="true"
            data-active="false"
            className="banner-carousel-slide"
            style={{ transform: "translateX(calc(-100% - 8px))" }}
          >
            <BannerArtwork
              banner={banners[(activeIndex + 1) % total]}
              number={((activeIndex + 1) % total) + 4}
              frame="highlight"
            />
          </div>
        ) : null}
      </div>
      {total > 1 && (
        <div className="flex items-center justify-center gap-5 px-4 py-3" aria-label={t("banner.slides")}>
          <button type="button" onClick={() => goTo(activeIndex - 1)} aria-label={t("banner.previous")}
            className="grid h-14 w-14 shrink-0 place-items-center rounded-full bg-neutral-100 text-neutral-600 transition-colors hover:bg-neutral-200">
            <svg viewBox="0 0 24 24" className="h-5 w-5" fill="currentColor" aria-hidden="true"><path d="M14.5 5.5a1 1 0 0 1 1.5.86v11.28a1 1 0 0 1-1.5.86l-8.46-5.64a1 1 0 0 1 0-1.72z" /></svg>
          </button>
          <div className="flex max-w-[60%] items-center overflow-x-auto rounded-full bg-neutral-100 px-4 py-1">
            {banners.map((banner, index) => (
              <button key={banner.id} type="button" onClick={() => goTo(index)}
                className="grid h-11 w-7 shrink-0 place-items-center rounded-full"
                aria-label={t("banner.goTo", { number: index + 1 })}
                aria-current={index === activeIndex ? "true" : undefined}>
                <span className={`h-3 w-3 rounded-full ${index === activeIndex ? "bg-neutral-600" : "bg-neutral-500"}`} />
              </button>
            ))}
          </div>
          <button type="button" onClick={() => goTo(activeIndex + 1)} aria-label={t("banner.next")}
            className="grid h-14 w-14 shrink-0 place-items-center rounded-full bg-neutral-100 text-neutral-600 transition-colors hover:bg-neutral-200">
            <svg viewBox="0 0 24 24" className="h-5 w-5" fill="currentColor" aria-hidden="true"><path d="M9.5 5.5A1 1 0 0 0 8 6.36v11.28a1 1 0 0 0 1.5.86l8.46-5.64a1 1 0 0 0 0-1.72z" /></svg>
          </button>
        </div>
      )}
    </section>
  );
}
