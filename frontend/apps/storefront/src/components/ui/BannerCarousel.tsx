import { useEffect, useRef, useState } from "react";
import { Link } from "react-router-dom";
import type { StorefrontBannerDto } from "@workspace-ecommerce/api-types";

interface BannerCarouselProps {
  banners: StorefrontBannerDto[];
  isLoading?: boolean;
}

export function BannerCarousel({ banners, isLoading = false }: BannerCarouselProps) {
  const [activeIndex, setActiveIndex] = useState(0);
  const [isPaused, setIsPaused] = useState(false);
  const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  const total = banners.length;

  useEffect(() => {
    if (total <= 1 || isPaused) {
      return;
    }

    timerRef.current = setTimeout(() => {
      setActiveIndex((prev) => (prev + 1) % total);
    }, 5000);

    return () => {
      if (timerRef.current) {
        clearTimeout(timerRef.current);
      }
    };
  }, [activeIndex, total, isPaused]);

  function goTo(index: number) {
    setActiveIndex((index + total) % total);
    if (timerRef.current) {
      clearTimeout(timerRef.current);
    }
  }

  function goPrev() {
    goTo(activeIndex - 1);
  }

  function goNext() {
    goTo(activeIndex + 1);
  }

  if (isLoading) {
    return (
      <div className="relative h-[500px] w-full overflow-hidden bg-slate-100 lg:h-[720px]">
        <div className="absolute inset-0 animate-pulse bg-gradient-to-br from-slate-100 to-slate-200" />
        <div className="absolute inset-0 flex items-center justify-center">
          <div className="h-12 w-12 animate-spin rounded-full border-4 border-slate-200 border-t-[var(--brand)]" />
        </div>
      </div>
    );
  }

  if (total === 0) {
    return (
      <div className="relative flex h-[500px] w-full items-center justify-center overflow-hidden bg-gradient-to-br from-white to-slate-100 lg:h-[720px]">
        <div className="text-center">
          <div className="mx-auto mb-4 flex h-16 w-16 items-center justify-center rounded-full bg-[var(--brand-soft)]">
            <svg className="h-8 w-8 text-[var(--brand)]" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.5}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M2.25 15.75l5.159-5.159a2.25 2.25 0 013.182 0l5.159 5.159m-1.5-1.5l1.409-1.409a2.25 2.25 0 013.182 0l2.909 2.909M3 21h18M3.75 3h16.5M4.5 3v18m15-18v18" />
            </svg>
          </div>
          <p className="ui-control text-slate-500">No banners yet</p>
          <p className="ui-body mt-1 text-slate-400">Add banners in the Admin Portal</p>
        </div>
      </div>
    );
  }

  return (
    <div
      className="group relative h-[500px] w-full overflow-hidden lg:h-[720px]"
      onMouseEnter={() => setIsPaused(true)}
      onMouseLeave={() => setIsPaused(false)}
    >
      {banners.map((banner, i) => (
        <div
          key={banner.id}
          aria-hidden={i !== activeIndex}
          className={`absolute inset-0 transition-opacity duration-700 ${
            i === activeIndex ? "opacity-100" : "opacity-0 pointer-events-none"
          }`}
        >
          {banner.imageUrl ? (
            <img
              src={banner.imageUrl}
              alt={banner.title}
              className="h-full w-full object-cover"
              loading={i === 0 ? "eager" : "lazy"}
            />
          ) : (
            <div
              className="h-full w-full"
              style={{
                background: `linear-gradient(135deg, hsl(${(i * 60) % 360} 70% 92%), hsl(${(i * 60 + 40) % 360} 80% 85%))`
              }}
            />
          )}
          <div className="absolute inset-0 bg-gradient-to-t from-black/60 via-black/20 to-transparent" />

          <div className="absolute bottom-0 left-0 right-0 p-8 pb-16 lg:p-12 lg:pb-20">
            <h2 className="ui-h1 max-w-xl tracking-tight text-white">
              {banner.title}
            </h2>
            {banner.linkUrl && (
              <Link
                to={banner.linkUrl}
                className="ui-control mt-4 inline-flex items-center gap-2 rounded-[var(--radius-control)] bg-white px-6 py-3 text-slate-900 shadow-[var(--shadow-card)] transition hover:bg-slate-50 hover:shadow-[var(--shadow-card-hover)]"
              >
                Shop now
                <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5}>
                  <path strokeLinecap="round" strokeLinejoin="round" d="M13.5 4.5L21 12m0 0l-7.5 7.5M21 12H3" />
                </svg>
              </Link>
            )}
          </div>
        </div>
      ))}

      {total > 1 && (
        <>
          <button
            type="button"
            onClick={goPrev}
            aria-label="Previous banner"
            className="absolute left-4 top-1/2 -translate-y-1/2 flex h-10 w-10 items-center justify-center rounded-full bg-white/80 text-slate-800 shadow opacity-0 backdrop-blur transition group-hover:opacity-100 hover:bg-white"
          >
            <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M15.75 19.5L8.25 12l7.5-7.5" />
            </svg>
          </button>
          <button
            type="button"
            onClick={goNext}
            aria-label="Next banner"
            className="absolute right-4 top-1/2 -translate-y-1/2 flex h-10 w-10 items-center justify-center rounded-full bg-white/80 text-slate-800 shadow opacity-0 backdrop-blur transition group-hover:opacity-100 hover:bg-white"
          >
            <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M8.25 4.5l7.5 7.5-7.5 7.5" />
            </svg>
          </button>
        </>
      )}

      {total > 1 && (
        <div className="absolute bottom-5 left-1/2 flex -translate-x-1/2 gap-2">
          {banners.map((_, i) => (
            <button
              type="button"
              key={i}
              onClick={() => goTo(i)}
              aria-label={`Go to banner ${i + 1}`}
              className={`h-2 rounded-full transition-all duration-300 ${
                i === activeIndex ? "w-6 bg-white" : "w-2 bg-white/50 hover:bg-white/80"
              }`}
            />
          ))}
        </div>
      )}

      {total > 1 && !isPaused && (
        <div
          key={`progress-${activeIndex}`}
          className="absolute bottom-0 left-0 h-0.5 bg-white/60 banner-progress"
        />
      )}
    </div>
  );
}
