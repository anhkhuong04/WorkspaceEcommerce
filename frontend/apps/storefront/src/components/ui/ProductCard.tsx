import { Link } from "react-router-dom";
import { formatMoney } from "@workspace-ecommerce/shared-utils";
import type { StorefrontProductListItemDto } from "@workspace-ecommerce/api-types";

interface ProductCardProps {
  product: StorefrontProductListItemDto;
  variant?: "default" | "home";
}

export function ProductCard({ product, variant = "default" }: ProductCardProps) {
  if (variant === "home") {
    const hasDiscount = product.minPrice !== null && product.compareAtPrice !== null && product.compareAtPrice > product.minPrice;

    return (
      <Link to={`/products/${product.slug}`} className="group min-w-0">
        <div className="relative aspect-square overflow-hidden rounded-lg bg-[#f1f1f1]">
          {product.imageUrl ? (
            <img
              src={product.imageUrl}
              alt={product.name}
              className="h-full w-full object-contain p-3 transition duration-300 group-hover:scale-[1.04]"
              loading="lazy"
            />
          ) : (
            <div className="h-full w-full bg-gradient-to-br from-[#f5f5f5] to-[#e8e8e8]" />
          )}
          {hasDiscount && (
            <span className="ui-caption absolute left-2 top-2 rounded-full bg-[#e52b1f] px-2 py-0.5 font-semibold text-white">
              Sale
            </span>
          )}
          {!product.isInStock && (
            <span className="ui-caption absolute left-2 top-2 rounded-full bg-slate-900 px-2 py-0.5 font-semibold text-white">
              Sold out
            </span>
          )}
        </div>

        <div className="pt-3">
          <p className="ui-caption truncate text-slate-400">{product.categoryName}</p>
          <h3 className="mt-1 truncate text-[13px] font-semibold text-slate-950 transition group-hover:text-[var(--brand)] sm:text-sm">
            {product.name}
          </h3>
          <div className="mt-1 flex flex-wrap items-baseline gap-x-2 gap-y-0.5 text-[12px] sm:text-[13px]">
            {product.minPrice !== null ? (
              <>
                <span className="font-medium text-slate-700">From {formatMoney(product.minPrice)}</span>
                {hasDiscount && <span className="text-slate-400 line-through">{formatMoney(product.compareAtPrice!)}</span>}
              </>
            ) : (
              <span className="font-medium text-slate-500">Contact us</span>
            )}
          </div>
        </div>
      </Link>
    );
  }

  return (
    <Link
      to={`/products/${product.slug}`}
      className="ui-card ui-card-hover group flex flex-col overflow-hidden border border-slate-100 bg-white"
    >
      <div className="relative overflow-hidden bg-slate-50">
        {product.imageUrl ? (
          <img
            src={product.imageUrl}
            alt={product.name}
            className="aspect-[4/3] w-full object-cover transition duration-300 group-hover:scale-105"
            loading="lazy"
          />
        ) : (
          <div className="aspect-[4/3] w-full bg-gradient-to-br from-slate-100 to-[var(--brand-soft)]" />
        )}
        {!product.isInStock && (
          <span className="ui-caption absolute left-3 top-3 rounded-full bg-slate-800/80 px-2.5 py-0.5 text-white backdrop-blur">
            Out of stock
          </span>
        )}
        {product.isFeatured && product.isInStock && (
          <span className="ui-caption absolute left-3 top-3 rounded-full bg-[var(--brand)] px-2.5 py-0.5 text-white">
            Featured
          </span>
        )}
      </div>

      <div className="flex flex-1 flex-col gap-2 p-4">
        <p className="ui-caption uppercase tracking-wide text-slate-400">
          {product.categoryName}
        </p>
        <h3 className="ui-h3 text-slate-950 transition-colors group-hover:text-[var(--brand)]">
          {product.name}
        </h3>
        <div className="mt-auto flex items-baseline gap-2 pt-3">
          {product.minPrice !== null ? (
            <>
              <span className="ui-price text-[var(--brand)]">
                {formatMoney(product.minPrice)}
              </span>
              {product.compareAtPrice !== null && product.compareAtPrice > product.minPrice && (
                <span className="ui-body text-slate-400 line-through">
                  {formatMoney(product.compareAtPrice)}
                </span>
              )}
            </>
          ) : (
            <span className="ui-control text-slate-400">Contact us</span>
          )}
        </div>
      </div>
    </Link>
  );
}
