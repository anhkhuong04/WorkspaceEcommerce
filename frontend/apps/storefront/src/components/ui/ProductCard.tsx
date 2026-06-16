import { Link } from "react-router-dom";
import { formatMoney } from "@workspace-ecommerce/shared-utils";
import type { StorefrontProductListItemDto } from "@workspace-ecommerce/api-types";
import { useAddProductToCart } from "../../features/cart/useAddProductToCart";

interface ProductCardProps {
  product: StorefrontProductListItemDto;
  variant?: "default" | "home";
}

export function ProductCard({ product, variant = "default" }: ProductCardProps) {
  const addProductMutation = useAddProductToCart();
  const canAdd = product.isInStock && product.minPrice !== null;

  if (variant === "home") {
    const hasDiscount = product.minPrice !== null && product.compareAtPrice !== null && product.compareAtPrice > product.minPrice;

    return (
      <article className="group min-w-0">
        <div className="relative aspect-square overflow-hidden rounded-lg bg-[#f1f1f1]">
          <Link to={`/products/${product.slug}`} className="block h-full w-full">
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
          </Link>
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
          {canAdd ? (
            <button
              type="button"
              disabled={addProductMutation.isPending}
              onClick={() => addProductMutation.mutate(product.slug)}
              className="absolute bottom-3 right-3 inline-flex h-11 translate-y-3 items-center justify-center rounded-full bg-[#3a3a3a] px-5 text-sm font-bold text-white opacity-0 shadow-lg transition duration-200 hover:bg-[#242424] disabled:cursor-wait disabled:opacity-70 group-hover:translate-y-0 group-hover:opacity-100 focus:translate-y-0 focus:opacity-100"
              aria-label={`Add ${product.name} to cart`}
            >
              <span className="mr-1 text-lg leading-none">+</span>
              {addProductMutation.isPending ? "Adding" : "Add"}
            </button>
          ) : null}
        </div>

        <Link to={`/products/${product.slug}`} className="block pt-3">
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
        </Link>
      </article>
    );
  }

  return (
    <article className="ui-card ui-card-hover group flex flex-col overflow-hidden border border-slate-100 bg-white">
      <div className="relative overflow-hidden bg-slate-50">
        <Link to={`/products/${product.slug}`} className="block">
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
        </Link>
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
        {canAdd ? (
          <button
            type="button"
            disabled={addProductMutation.isPending}
            onClick={() => addProductMutation.mutate(product.slug)}
            className="absolute bottom-4 right-4 inline-flex h-12 translate-y-3 items-center justify-center rounded-full bg-[#3a3a3a] px-6 text-base font-bold text-white opacity-0 shadow-lg transition duration-200 hover:bg-[#242424] disabled:cursor-wait disabled:opacity-70 group-hover:translate-y-0 group-hover:opacity-100 focus:translate-y-0 focus:opacity-100"
            aria-label={`Add ${product.name} to cart`}
          >
            <span className="mr-1 text-xl leading-none">+</span>
            {addProductMutation.isPending ? "Adding" : "Add"}
          </button>
        ) : null}
      </div>

      <Link to={`/products/${product.slug}`} className="flex flex-1 flex-col gap-2 p-4">
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
      </Link>
    </article>
  );
}
