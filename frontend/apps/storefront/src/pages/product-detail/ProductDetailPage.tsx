import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import type { StorefrontCategoryDto } from "@workspace-ecommerce/api-types";
import { formatMoney } from "@workspace-ecommerce/shared-utils";
import type { FormEvent } from "react";
import { useEffect, useMemo, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { useStorefrontCart } from "../../features/cart/StorefrontCartContext";
import { getApiErrorMessage } from "../../services/api/errors";
import { storefrontApi } from "../../services/api/storefrontApi";

export function ProductDetailPage() {
  const { slug = "" } = useParams();
  const queryClient = useQueryClient();
  const { cartQueryKey, cartSessionId, openCartDrawer } = useStorefrontCart();
  const [selectedVariantId, setSelectedVariantId] = useState("");
  const [selectedImageIndex, setSelectedImageIndex] = useState(0);
  const [quantity, setQuantity] = useState(1);

  const productQuery = useQuery({
    queryKey: ["storefront", "product", slug],
    queryFn: () => storefrontApi.getProduct(slug),
    enabled: slug.length > 0
  });
  const categoriesQuery = useQuery({
    queryKey: ["storefront", "categories"],
    queryFn: storefrontApi.getCategories
  });

  const product = productQuery.data;
  const categorySlug = useMemo(
    () => (product ? findCategorySlug(categoriesQuery.data ?? [], product.categoryId) : undefined),
    [categoriesQuery.data, product]
  );
  const selectedVariant = useMemo(() => {
    if (!product) {
      return undefined;
    }

    return (
      product.variants.find((variant) => variant.id === selectedVariantId) ??
      product.variants.find((variant) => variant.stockQuantity > 0) ??
      product.variants[0]
    );
  }, [product, selectedVariantId]);

  const addToCartMutation = useMutation({
    mutationFn: () =>
      storefrontApi.addCartItem({
        sessionId: cartSessionId,
        productVariantId: selectedVariant?.id ?? "",
        quantity
      }),
    onSuccess: (cart) => {
      queryClient.setQueryData(cartQueryKey, cart);
      openCartDrawer(cart);
    }
  });

  useEffect(() => {
    setSelectedVariantId("");
    setSelectedImageIndex(0);
    setQuantity(1);
  }, [product?.id]);

  useEffect(() => {
    if (!selectedVariant) {
      return;
    }

    const maxQuantity = Math.max(selectedVariant.stockQuantity, 1);
    setQuantity((current) => Math.min(Math.max(current, 1), maxQuantity));
  }, [selectedVariant?.id, selectedVariant?.stockQuantity]);

  function setSafeQuantity(nextQuantity: number) {
    const maxQuantity = Math.max(selectedVariant?.stockQuantity ?? 1, 1);
    setQuantity(Math.min(Math.max(nextQuantity, 1), maxQuantity));
  }

  function addToCart(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!selectedVariant || selectedVariant.stockQuantity <= 0) {
      return;
    }

    addToCartMutation.mutate();
  }

  if (productQuery.isLoading) {
    return (
      <div className="grid gap-8">
        <div className="h-4 w-72 animate-pulse rounded bg-slate-100" />
        <div className="grid gap-8 lg:grid-cols-[minmax(0,1fr)_minmax(380px,460px)]">
          <div className="aspect-square animate-pulse rounded-[var(--radius-card)] bg-slate-100" />
          <div className="h-[520px] animate-pulse rounded-[var(--radius-card)] bg-slate-100" />
        </div>
      </div>
    );
  }

  if (productQuery.isError || !product) {
    return (
      <div className="rounded-[var(--radius-card)] bg-red-50 p-8 font-semibold text-red-700">
        {productQuery.isError ? getApiErrorMessage(productQuery.error) : "Product was not found."}
      </div>
    );
  }

  const selectedImage = product.images[selectedImageIndex] ?? product.images[0];
  const isOutOfStock = !selectedVariant || selectedVariant.stockQuantity <= 0;
  const hasDiscount =
    selectedVariant?.compareAtPrice !== null &&
    selectedVariant?.compareAtPrice !== undefined &&
    selectedVariant.compareAtPrice > selectedVariant.price;

  return (
    <div className="grid gap-8">
      <nav className="ui-caption flex min-w-0 flex-wrap items-center gap-2 text-slate-500" aria-label="Breadcrumb">
        <Link to="/" className="font-semibold transition hover:text-slate-950">
          Home
        </Link>
        <span aria-hidden="true">/</span>
        <Link to="/products" className="font-semibold transition hover:text-slate-950">
          Products
        </Link>
        <span aria-hidden="true">/</span>
        <Link to={categorySlug ? `/products?categorySlug=${encodeURIComponent(categorySlug)}` : "/products"} className="font-semibold transition hover:text-slate-950">
          {product.categoryName}
        </Link>
        <span aria-hidden="true">/</span>
        <span className="min-w-0 max-w-full truncate text-slate-950">{product.name}</span>
      </nav>

      <section className="grid gap-8 lg:grid-cols-[minmax(0,1fr)_minmax(380px,460px)] lg:items-start">
        <div className="grid min-w-0 gap-4">
          <div className="relative overflow-hidden rounded-[var(--radius-card)] border border-slate-100 bg-[#f6f6f6]">
            {selectedImage ? (
              <img
                src={selectedImage.imageUrl}
                alt={selectedImage.altText ?? product.name}
                className="aspect-square w-full object-contain p-4 sm:p-8"
              />
            ) : (
              <div className="grid aspect-square place-items-center bg-slate-100 p-8 text-center text-sm font-semibold text-slate-500">
                No image available
              </div>
            )}
            {hasDiscount ? (
              <span className="ui-caption absolute left-4 top-4 rounded-full bg-[#e52b1f] px-3 py-1 font-semibold text-white">
                Sale
              </span>
            ) : null}
          </div>

          {product.images.length > 1 ? (
            <div className="grid grid-cols-4 gap-3 sm:grid-cols-6">
              {product.images.map((image, index) => (
                <button
                  key={image.id}
                  type="button"
                  onClick={() => setSelectedImageIndex(index)}
                  className={`aspect-square overflow-hidden rounded-[var(--radius-control)] border bg-[#f6f6f6] transition ${
                    selectedImageIndex === index ? "border-slate-950 ring-2 ring-slate-950/10" : "border-slate-200 hover:border-slate-400"
                  }`}
                  aria-label={`View image ${index + 1}`}
                >
                  <img
                    src={image.imageUrl}
                    alt={image.altText ?? product.name}
                    className="h-full w-full object-contain p-2"
                    loading="lazy"
                  />
                </button>
              ))}
            </div>
          ) : null}
        </div>

        <aside className="lg:sticky lg:top-28">
          <form onSubmit={addToCart} className="rounded-[var(--radius-card)] border border-slate-100 bg-white p-5 shadow-[var(--shadow-card)] md:p-7">
            <Link
              to={categorySlug ? `/products?categorySlug=${encodeURIComponent(categorySlug)}` : "/products"}
              className="ui-caption font-bold uppercase tracking-[0.16em] text-slate-500 transition hover:text-slate-950"
            >
              {product.categoryName}
            </Link>

            <h1 className="mt-3 text-[28px] font-black leading-tight text-slate-950 sm:text-[34px]">{product.name}</h1>

            {product.description ? (
              <p className="mt-4 line-clamp-4 text-sm leading-6 text-slate-600">{product.description}</p>
            ) : null}

            <div className="mt-6 flex flex-wrap items-end justify-between gap-4 border-y border-slate-100 py-5">
              <div>
                {selectedVariant ? (
                  <div className="flex flex-wrap items-baseline gap-3">
                    <p className="text-[28px] font-black leading-none text-slate-950">{formatMoney(selectedVariant.price)}</p>
                    {hasDiscount ? (
                      <p className="text-base font-bold text-slate-400 line-through">{formatMoney(selectedVariant.compareAtPrice!)}</p>
                    ) : null}
                  </div>
                ) : (
                  <p className="text-[28px] font-black leading-none text-slate-950">Contact us</p>
                )}
              </div>
              <span
                className={`rounded-full px-3 py-1 text-xs font-black ${
                  isOutOfStock ? "bg-slate-100 text-slate-500" : "bg-emerald-50 text-emerald-700"
                }`}
              >
                {isOutOfStock ? "Out of stock" : `${selectedVariant.stockQuantity} in stock`}
              </span>
            </div>

            <div className="mt-6 grid gap-3">
              <div className="flex items-center justify-between gap-4">
                <h2 className="ui-h3 text-slate-950">Configuration</h2>
                {selectedVariant ? <p className="ui-caption text-slate-500">SKU {selectedVariant.sku}</p> : null}
              </div>

              <div className="grid max-h-[320px] gap-3 overflow-y-auto pr-1" role="radiogroup" aria-label="Product variants">
                {product.variants.map((variant) => {
                  const variantHasDiscount = variant.compareAtPrice !== null && variant.compareAtPrice > variant.price;
                  const variantIsSelected = selectedVariant?.id === variant.id;
                  const variantIsOutOfStock = variant.stockQuantity <= 0;

                  return (
                    <button
                      key={variant.id}
                      type="button"
                      role="radio"
                      aria-checked={variantIsSelected}
                      disabled={variantIsOutOfStock}
                      onClick={() => setSelectedVariantId(variant.id)}
                      className={`flex min-w-0 items-start justify-between gap-4 rounded-[var(--radius-control)] border p-4 text-left transition disabled:cursor-not-allowed disabled:opacity-50 ${
                        variantIsSelected ? "border-slate-950 bg-slate-50" : "border-slate-200 hover:border-slate-400"
                      }`}
                    >
                      <span className="min-w-0">
                        <span className="block truncate text-sm font-black text-slate-950">{variant.name}</span>
                        <span className="mt-1 block text-xs font-semibold text-slate-500">
                          {variant.color ?? "Default"}
                          {variant.size ? ` / ${variant.size}` : ""} / {variant.stockQuantity > 0 ? `${variant.stockQuantity} left` : "Sold out"}
                        </span>
                      </span>
                      <span className="shrink-0 text-right">
                        <span className="block text-sm font-black text-slate-950">{formatMoney(variant.price)}</span>
                        {variantHasDiscount ? (
                          <span className="block text-xs font-semibold text-slate-400 line-through">{formatMoney(variant.compareAtPrice!)}</span>
                        ) : null}
                      </span>
                    </button>
                  );
                })}
              </div>
            </div>

            <div className="mt-6 grid gap-4 sm:grid-cols-[150px_1fr]">
              <label className="grid gap-2">
                <span className="ui-caption font-bold uppercase tracking-[0.16em] text-slate-500">Quantity</span>
                <span className="grid h-12 grid-cols-[40px_1fr_40px] overflow-hidden rounded-[var(--radius-control)] border border-slate-200">
                  <button
                    type="button"
                    onClick={() => setSafeQuantity(quantity - 1)}
                    disabled={quantity <= 1}
                    className="grid place-items-center border-r border-slate-200 text-lg font-bold text-slate-700 transition hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-40"
                    aria-label="Decrease quantity"
                  >
                    -
                  </button>
                  <input
                    type="text"
                    inputMode="numeric"
                    pattern="[0-9]*"
                    value={quantity}
                    onChange={(event) => {
                      const nextQuantity = Number(event.target.value);
                      setSafeQuantity(Number.isFinite(nextQuantity) ? nextQuantity : 1);
                    }}
                    className="min-w-0 border-0 px-2 text-center text-sm font-black outline-none"
                  />
                  <button
                    type="button"
                    onClick={() => setSafeQuantity(quantity + 1)}
                    disabled={!selectedVariant || quantity >= selectedVariant.stockQuantity}
                    className="grid place-items-center border-l border-slate-200 text-lg font-bold text-slate-700 transition hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-40"
                    aria-label="Increase quantity"
                  >
                    +
                  </button>
                </span>
              </label>

              <div className="flex items-end">
                <button
                  type="submit"
                  disabled={!selectedVariant || selectedVariant.stockQuantity <= 0 || addToCartMutation.isPending}
                  className="ui-control h-12 w-full rounded-[var(--radius-control)] bg-slate-950 px-6 font-black text-white transition hover:bg-slate-800 disabled:cursor-not-allowed disabled:opacity-50"
                >
                  {addToCartMutation.isPending ? "Adding..." : isOutOfStock ? "Out of stock" : "Add to cart"}
                </button>
              </div>
            </div>

            {selectedVariant?.requiresInstallation ? (
              <div className="mt-5 rounded-[var(--radius-control)] bg-amber-50 px-4 py-3 text-sm font-semibold text-amber-800">
                Installation required for this configuration.
              </div>
            ) : null}

            {addToCartMutation.isSuccess ? (
              <div className="ui-control mt-5 rounded-[var(--radius-control)] bg-emerald-50 p-4 text-emerald-700">
                Added to cart.{" "}
                <button type="button" onClick={() => openCartDrawer()} className="font-black underline">
                  Review cart
                </button>
              </div>
            ) : null}
            {addToCartMutation.isError ? (
              <div className="ui-control mt-5 rounded-[var(--radius-control)] bg-red-50 p-4 text-red-700">
                {getApiErrorMessage(addToCartMutation.error)}
              </div>
            ) : null}
          </form>
        </aside>
      </section>

      <section className="grid gap-8 border-t border-slate-100 pt-8 lg:grid-cols-[minmax(0,1fr)_420px]">
        <div className="min-w-0">
          <h2 className="ui-h2 text-slate-950">Description</h2>
          <p className="mt-4 max-w-3xl text-sm leading-7 text-slate-600">
            {product.description ?? "No description available."}
          </p>
        </div>

        <div className="min-w-0">
          <h2 className="ui-h2 text-slate-950">Specifications</h2>
          {product.specifications.length === 0 ? (
            <p className="mt-4 text-sm leading-7 text-slate-500">No specifications available.</p>
          ) : (
            <div className="mt-4 divide-y divide-slate-100 border-y border-slate-100">
              {product.specifications.map((specification) => (
                <div key={specification.id} className="grid grid-cols-[minmax(0,0.9fr)_minmax(0,1.1fr)] gap-5 py-4">
                  <p className="text-sm font-bold text-slate-500">{specification.name}</p>
                  <p className="text-right text-sm font-semibold text-slate-950">{specification.value}</p>
                </div>
              ))}
            </div>
          )}
        </div>
      </section>
    </div>
  );
}

function findCategorySlug(categories: StorefrontCategoryDto[], categoryId: string): string | undefined {
  for (const category of categories) {
    if (category.id === categoryId) {
      return category.slug;
    }

    const childSlug = findCategorySlug(category.children, categoryId);
    if (childSlug) {
      return childSlug;
    }
  }

  return undefined;
}
