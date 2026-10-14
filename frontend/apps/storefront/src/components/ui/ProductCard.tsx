import { useRef, useState } from "react";
import { Link } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { formatMoney } from "@workspace-ecommerce/shared-utils";
import type { StorefrontProductListItemDto } from "@workspace-ecommerce/api-types";
import { useAddProductToCart } from "../../features/cart/useAddProductToCart";
import { getApiErrorMessage } from "../../services/api/errors";

interface ProductCardProps {
  product: StorefrontProductListItemDto;
  variant?: "default" | "home";
}

export function ProductCard({ product }: ProductCardProps) {
  const { t } = useTranslation();
  const addButtonRef = useRef<HTMLButtonElement>(null);
  const addProductMutation = useAddProductToCart(addButtonRef);
  const [failedImage, setFailedImage] = useState<string | null>(null);
  const canAdd = product.isInStock && product.minPrice !== null;
  const hasDiscount = product.minPrice !== null && product.compareAtPrice !== null && product.compareAtPrice > product.minPrice;

  return (
    <article className="group min-w-0">
      <div className="relative aspect-square overflow-hidden rounded-[var(--radius-card)] bg-[var(--brand-soft)]">
        <Link to={`/products/${product.slug}`} className="block h-full w-full" aria-label={product.name}>
          {product.imageUrl && failedImage !== product.imageUrl ? (
            <img src={product.imageUrl} alt={product.name} width={480} height={480} loading="lazy"
              onError={() => setFailedImage(product.imageUrl)}
              className="h-full w-full object-contain p-4 transition-transform duration-200 group-hover:scale-[1.03] sm:p-6" />
          ) : <span className="ui-caption grid h-full place-items-center px-4 text-center text-[var(--muted-strong)]">{product.name}</span>}
        </Link>
        <div className="pointer-events-none absolute left-2 right-2 top-2 flex flex-wrap gap-1.5 sm:left-3 sm:top-3">
          {!product.isInStock ? (
            <span className="rounded-full bg-[var(--ink)] px-2.5 py-1 text-xs font-semibold text-white">{t("product.soldOut")}</span>
          ) : hasDiscount ? (
            <span className="rounded-full bg-[var(--accent)] px-2.5 py-1 text-xs font-semibold text-white">{t("product.sale")}</span>
          ) : product.isFeatured ? (
            <span className="rounded-full bg-[var(--ink)] px-2.5 py-1 text-xs font-semibold text-white">{t("product.featured")}</span>
          ) : null}
        </div>
        {canAdd && (
          <button ref={addButtonRef} type="button" disabled={addProductMutation.isPending}
            onClick={() => addProductMutation.mutate(product.slug)}
            className="product-card-action absolute bottom-3 right-3 inline-flex min-h-11 items-center justify-center gap-1.5 rounded-full bg-[var(--ink)] px-4 text-sm font-semibold text-white hover:bg-slate-800 disabled:cursor-wait disabled:opacity-70"
            aria-label={t("product.addToCartLabel", { name: product.name })}>
            <span className="text-lg leading-none" aria-hidden="true">+</span>
            {addProductMutation.isPending ? t("product.adding") : t("product.add")}
          </button>
        )}
      </div>
      <Link to={`/products/${product.slug}`} className="block pt-4">
        <p className="ui-caption truncate text-[var(--muted-strong)]">{product.categoryName}</p>
        <h3 className="mt-1 line-clamp-2 min-h-11 text-sm font-semibold leading-[1.4] text-[var(--ink)] sm:text-base">{product.name}</h3>
        <div className="mt-2 flex flex-wrap items-baseline gap-x-2 gap-y-1">
          <span className="text-sm font-semibold text-[var(--ink)] sm:text-base">
            {product.minPrice === null ? t("common.contactUs") : t("product.fromPrice", { price: formatMoney(product.minPrice) })}
          </span>
          {hasDiscount && <span className="ui-caption text-[var(--muted-strong)] line-through">{formatMoney(product.compareAtPrice!)}</span>}
        </div>
      </Link>
      {addProductMutation.isError && <p role="alert" className="mt-3 rounded-lg bg-red-50 p-3 text-sm text-red-700">{getApiErrorMessage(addProductMutation.error)}</p>}
    </article>
  );
}
