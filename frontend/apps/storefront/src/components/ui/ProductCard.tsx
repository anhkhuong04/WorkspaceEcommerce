import { Link } from "react-router-dom";
import { formatMoney } from "@workspace-ecommerce/shared-utils";
import type { StorefrontProductListItemDto } from "@workspace-ecommerce/api-types";

interface ProductCardProps {
  product: StorefrontProductListItemDto;
}

export function ProductCard({ product }: ProductCardProps) {
  return (
    <Link
      to={`/products/${product.slug}`}
      className="group flex flex-col overflow-hidden rounded-[1.5rem] border border-slate-200 bg-white shadow-sm transition duration-200 hover:-translate-y-1 hover:shadow-md"
    >
      {/* Image */}
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
        {/* Out of stock badge */}
        {!product.isInStock && (
          <span className="absolute left-3 top-3 rounded-full bg-slate-800/80 px-2.5 py-0.5 text-xs font-semibold text-white backdrop-blur">
            Hết hàng
          </span>
        )}
        {/* Featured badge */}
        {product.isFeatured && product.isInStock && (
          <span className="absolute left-3 top-3 rounded-full bg-[var(--brand)] px-2.5 py-0.5 text-xs font-semibold text-white">
            Nổi bật
          </span>
        )}
      </div>

      {/* Info */}
      <div className="flex flex-1 flex-col gap-1 p-5">
        <p className="text-xs font-semibold uppercase tracking-wide text-slate-400">
          {product.categoryName}
        </p>
        <h3 className="text-base font-black leading-snug text-slate-950 group-hover:text-[var(--brand)] transition-colors">
          {product.name}
        </h3>
        <div className="mt-auto flex items-baseline gap-2 pt-3">
          {product.minPrice !== null ? (
            <>
              <span className="text-base font-black text-[var(--brand)]">
                {formatMoney(product.minPrice)}
              </span>
              {product.compareAtPrice !== null && product.compareAtPrice > product.minPrice && (
                <span className="text-sm font-medium text-slate-400 line-through">
                  {formatMoney(product.compareAtPrice)}
                </span>
              )}
            </>
          ) : (
            <span className="text-sm font-semibold text-slate-400">Liên hệ</span>
          )}
        </div>
      </div>
    </Link>
  );
}
