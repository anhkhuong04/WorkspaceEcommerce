import { useEffect, useState, useSyncExternalStore } from "react";
import { Link } from "react-router-dom";
import { useTranslation } from "react-i18next";
import type { StorefrontBannerDto } from "@workspace-ecommerce/api-types";

interface BannerCarouselProps { banners: StorefrontBannerDto[]; isLoading?: boolean; }
const motionQuery = "(prefers-reduced-motion: reduce)";
function subscribeToMotion(callback: () => void) {
  const media = window.matchMedia(motionQuery);
  media.addEventListener("change", callback);
  return () => media.removeEventListener("change", callback);
}

export function BannerCarousel({ banners, isLoading = false }: BannerCarouselProps) {
  const { t } = useTranslation();
  const [currentIndex, setCurrentIndex] = useState(0);
  const [userPaused, setUserPaused] = useState(false);
  const [isHovered, setIsHovered] = useState(false);
  const reducedMotion = useSyncExternalStore(subscribeToMotion, () => window.matchMedia(motionQuery).matches, () => false);
  const total = banners.length;
  const activeIndex = currentIndex % Math.max(total, 1);
  const isPaused = userPaused || isHovered || reducedMotion;

  useEffect(() => {
    if (isLoading || total <= 1 || isPaused) return;
    const timer = window.setTimeout(() => setCurrentIndex((index) => (index + 1) % total), 5000);
    return () => window.clearTimeout(timer);
  }, [currentIndex, isLoading, isPaused, total]);

  function goTo(index: number) {
    setUserPaused(true);
    setCurrentIndex((index + total) % total);
  }

  if (isLoading) return <div className="storefront-hero animate-pulse bg-[var(--brand-soft)]" role="status" aria-label={t("banner.loading")} />;
  if (total === 0) return (
    <div className="storefront-hero flex items-center justify-center bg-[var(--brand-soft)] px-6 text-center">
      <div><p className="ui-h2">{t("banner.empty")}</p><p className="ui-body mt-3 text-[var(--muted-strong)]">{t("banner.emptyHint")}</p></div>
    </div>
  );

  return (
    <section className="storefront-hero relative overflow-hidden bg-[var(--brand-soft)]"
      aria-label={t("home.heroBanners")} aria-roledescription={t("banner.carousel")}
      onMouseEnter={() => setIsHovered(true)} onMouseLeave={() => setIsHovered(false)}
      onFocusCapture={(event) => { if (!(event.target instanceof Element) || !event.target.closest("[data-carousel-playback]")) setUserPaused(true); }}>
      {banners.map((banner, index) => (
        <div key={banner.id} inert={index !== activeIndex} aria-hidden={index !== activeIndex}
          className={`absolute inset-0 transition-opacity duration-200 ${index === activeIndex ? "opacity-100" : "opacity-0 pointer-events-none"}`}>
          {banner.imageUrl && <img src={banner.imageUrl} alt="" width={1600} height={640}
            className="h-full w-full object-cover" loading={index === 0 ? "eager" : "lazy"} fetchPriority={index === 0 ? "high" : "auto"} />}
          <div className="absolute inset-0 bg-gradient-to-r from-black/65 via-black/20 to-transparent" />
          <div className="storefront-container absolute inset-0 flex h-full items-end pb-24 pt-8 sm:items-center sm:pb-20">
            <div className="max-w-[min(80%,640px)] text-white">
              <h1 className="storefront-hero-title">{banner.title}</h1>
              {banner.linkUrl && <Link to={banner.linkUrl} className="mt-6 inline-flex min-h-12 items-center gap-3 rounded-full bg-white px-6 text-sm font-semibold text-[var(--ink)] transition hover:bg-slate-100">
                {t("banner.shopNow")}<span aria-hidden="true">↗</span>
              </Link>}
            </div>
          </div>
        </div>
      ))}
      {total > 1 && <div className="storefront-container absolute inset-x-0 bottom-4 flex items-center justify-between gap-3 text-white">
        <div className="flex items-center gap-1" aria-label={t("banner.slides")}>
          {banners.map((banner, index) => <button key={banner.id} type="button" onClick={() => goTo(index)}
            className="grid h-11 w-11 place-items-center rounded-full hover:bg-white/15"
            aria-label={t("banner.goTo", { number: index + 1 })} aria-current={index === activeIndex ? "true" : undefined}>
            <span className={`h-1.5 rounded-full ${index === activeIndex ? "w-6 bg-white" : "w-2 bg-white/60"}`} />
          </button>)}
        </div>
        <div className="flex items-center gap-2">
          {!reducedMotion && <button type="button" data-carousel-playback onClick={() => setUserPaused((paused) => !paused)}
            className="grid h-11 w-11 place-items-center rounded-full border border-white/40 bg-black/20 hover:bg-black/40"
            aria-label={userPaused ? t("banner.resume") : t("banner.pause")}>
            <span aria-hidden="true">{userPaused ? "▶" : "Ⅱ"}</span>
          </button>}
          <button type="button" onClick={() => goTo(activeIndex - 1)} aria-label={t("banner.previous")}
            className="grid h-11 w-11 place-items-center rounded-full border border-white/40 bg-black/20 text-xl hover:bg-black/40"><span aria-hidden="true">‹</span></button>
          <button type="button" onClick={() => goTo(activeIndex + 1)} aria-label={t("banner.next")}
            className="grid h-11 w-11 place-items-center rounded-full border border-white/40 bg-black/20 text-xl hover:bg-black/40"><span aria-hidden="true">›</span></button>
        </div>
      </div>}
    </section>
  );
}
