import { useState } from "react";
import { Link } from "react-router-dom";
import { useTranslation } from "react-i18next";
import type { StorefrontBannerDto } from "@workspace-ecommerce/api-types";

// Keep artwork and caption separate: text contrast must not recolor the image.
export function BannerArtwork({
  banner,
  number = 1,
  priority = false,
  frame = "natural",
}: {
  banner: StorefrontBannerDto;
  number?: number;
  priority?: boolean;
  frame?: "natural" | "feature" | "highlight";
}) {
  const { t } = useTranslation();
  const [failedUrl, setFailedUrl] = useState<string | null>(null);
  const title = banner.title.trim();
  const label = title || t("banner.artwork", { number });
  const image =
    failedUrl === banner.imageUrl ? (
      <div
        className="flex h-full aspect-[5/2] items-center justify-center bg-[var(--brand-soft)] p-6 text-center text-sm text-[var(--muted-strong)]"
        role="status"
      >
        {t("banner.imageUnavailable")}
      </div>
    ) : (
      <img
        src={banner.imageUrl}
        alt={banner.linkUrl || title ? "" : label}
        width={1600}
        height={640}
        loading={priority ? "eager" : "lazy"}
        fetchPriority={priority ? "high" : "auto"}
        className="banner-image"
        onError={() => setFailedUrl(banner.imageUrl)}
      />
    );
  return (
    <div className="banner-artwork min-w-0">
      {banner.linkUrl ? (
        <Link
          to={banner.linkUrl}
          aria-label={t("banner.open", { title: label })}
          className="block"
        >
          <div className={`banner-image-frame banner-image-frame-${frame}`}>
            {image}
          </div>
        </Link>
      ) : (
        <div className={`banner-image-frame banner-image-frame-${frame}`}>
          {image}
        </div>
      )}
      {title ? (
        <div className="banner-caption">
          <h2 className="min-w-0 break-words text-xl font-semibold leading-tight sm:text-2xl">
            {title}
          </h2>
          {banner.linkUrl ? (
            <Link
              to={banner.linkUrl}
              className="inline-flex min-h-11 shrink-0 items-center justify-center gap-3 rounded-full bg-[var(--ink)] px-6 py-3 text-sm font-semibold text-white transition hover:bg-neutral-800"
            >
              {t("common.viewMore")}
              <span aria-hidden="true">↗</span>
            </Link>
          ) : null}
        </div>
      ) : null}
    </div>
  );
}
