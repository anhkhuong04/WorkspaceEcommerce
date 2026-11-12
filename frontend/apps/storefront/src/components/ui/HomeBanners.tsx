import type { StorefrontBannerDto } from "@workspace-ecommerce/api-types";
import { useTranslation } from "react-i18next";
import { BannerArtwork } from "./BannerArtwork";
import { BannerCarousel } from "./BannerCarousel";

export function HomeBanners({
  banners,
  isLoading = false,
}: {
  banners: StorefrontBannerDto[];
  isLoading?: boolean;
}) {
  const { t } = useTranslation();
  if (isLoading)
    return (
      <div
        className="aspect-[5/2] animate-pulse bg-[var(--brand-soft)]"
        role="status"
        aria-label={t("banner.loading")}
      />
    );
  if (!banners.length) return null;
  const [hero] = banners;
  const features = banners.slice(1, 3);
  const highlights = banners.slice(3);
  return (
    <div className="home-banner-stack">
      <section aria-label={t("home.heroBanners")}>
        <BannerArtwork banner={hero} priority />
      </section>
      {features.length ? (
        <section
          aria-label={t("home.featureBanners")}
          className="grid min-w-0 grid-cols-1 gap-2 md:grid-cols-2"
        >
          {features.map((banner, index) => (
            <BannerArtwork
              key={banner.id}
              banner={banner}
              number={index + 2}
              frame="feature"
            />
          ))}
        </section>
      ) : null}
      {highlights.length ? (
        <BannerCarousel
          banners={highlights}
          label={t("home.promotionBanners")}
        />
      ) : null}
    </div>
  );
}
