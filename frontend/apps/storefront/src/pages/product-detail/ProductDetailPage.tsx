import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { formatMoney } from "@workspace-ecommerce/shared-utils";
import type { FormEvent } from "react";
import { useState } from "react";
import { useParams } from "react-router-dom";
import { useStorefrontCart } from "../../features/cart/StorefrontCartContext";
import { PageHeader } from "../../components/ui/PageHeader";
import { getApiErrorMessage } from "../../services/api/errors";
import { storefrontApi } from "../../services/api/storefrontApi";

export function ProductDetailPage() {
  const { slug = "" } = useParams();
  const queryClient = useQueryClient();
  const { cartQueryKey, cartSessionId, openCartDrawer } = useStorefrontCart();
  const [selectedVariantId, setSelectedVariantId] = useState("");
  const [quantity, setQuantity] = useState(1);

  const productQuery = useQuery({
    queryKey: ["storefront", "product", slug],
    queryFn: () => storefrontApi.getProduct(slug),
    enabled: slug.length > 0
  });

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

  const product = productQuery.data;
  const selectedVariant = product?.variants.find((variant) => variant.id === selectedVariantId) ?? product?.variants[0];

  function addToCart(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!selectedVariant || selectedVariant.stockQuantity <= 0) {
      return;
    }

    addToCartMutation.mutate();
  }

  if (productQuery.isLoading) {
    return <div className="ui-card p-8 text-slate-500">Loading product...</div>;
  }

  if (productQuery.isError || !product) {
    return (
      <div className="rounded-[var(--radius-card)] bg-red-50 p-8 font-semibold text-red-700">
        {productQuery.isError ? getApiErrorMessage(productQuery.error) : "Product was not found."}
      </div>
    );
  }

  const primaryImage = product.images[0];

  return (
    <div className="grid gap-6">
      <PageHeader eyebrow={product.categoryName} title={product.name} description={product.description ?? "Product details from the live catalog API."} />
      <section className="grid gap-6 lg:grid-cols-[0.9fr_1.1fr]">
        <div className="grid gap-4">
          {primaryImage ? (
            <img
              src={primaryImage.imageUrl}
              alt={primaryImage.altText ?? product.name}
              className="aspect-square w-full rounded-[var(--radius-card)] object-cover shadow-[var(--shadow-card)] ring-1 ring-slate-200"
            />
          ) : (
            <div className="aspect-square rounded-[var(--radius-card)] bg-[radial-gradient(circle_at_top_left,#bbf7d0,#f8fafc_55%,#dbeafe)] shadow-inner" />
          )}
          {product.images.length > 1 && (
            <div className="grid grid-cols-4 gap-3">
              {product.images.slice(1, 5).map((image) => (
                <img key={image.id} src={image.imageUrl} alt={image.altText ?? product.name} className="aspect-square rounded-[var(--radius-card)] object-cover ring-1 ring-slate-200" />
              ))}
            </div>
          )}
        </div>

        <div className="grid gap-5">
          <form onSubmit={addToCart} className="ui-card border border-slate-100 p-4 md:p-8">
            <div className="flex items-start justify-between gap-5">
              <div>
          <p className="ui-caption uppercase tracking-[0.18em] text-slate-700">Variant</p>
                <h2 className="ui-h2 mt-2 text-slate-950">Choose configuration</h2>
              </div>
              {selectedVariant && <p className="ui-price text-[var(--brand)]">{formatMoney(selectedVariant.price)}</p>}
            </div>

            <div className="mt-6 grid gap-3">
              {product.variants.map((variant) => (
                <label
                  key={variant.id}
                  className={`flex cursor-pointer items-center justify-between gap-4 rounded-[var(--radius-card)] border p-4 transition ${
                    selectedVariant?.id === variant.id ? "border-slate-950 bg-slate-100" : "border-slate-100 bg-slate-50 hover:border-slate-300"
                  }`}
                >
                  <span className="flex items-start gap-3">
                    <input
                      type="radio"
                      name="variant"
                      value={variant.id}
                      checked={selectedVariant?.id === variant.id}
                      onChange={() => setSelectedVariantId(variant.id)}
                    className="mt-1 h-4 w-4 accent-slate-950"
                    />
                    <span>
                      <span className="ui-h3 block text-slate-950">{variant.name}</span>
                      <span className="ui-body text-slate-500">
                        {variant.sku} | {variant.color ?? "Default"} {variant.size ? `| ${variant.size}` : ""} | Stock {variant.stockQuantity}
                      </span>
                    </span>
                  </span>
                  <span className="text-right">
                    <span className="ui-control block text-slate-950">{formatMoney(variant.price)}</span>
                    {variant.compareAtPrice !== null && <span className="ui-caption text-slate-400 line-through">{formatMoney(variant.compareAtPrice)}</span>}
                  </span>
                </label>
              ))}
            </div>

            <div className="mt-6 grid gap-4 sm:grid-cols-[160px_1fr]">
              <label className="grid gap-2">
                <span className="ui-caption uppercase tracking-[0.16em] text-slate-500">Quantity</span>
                <input
                  type="number"
                  min={1}
                  max={selectedVariant?.stockQuantity ?? 1}
                  value={quantity}
                  onChange={(event) => {
                    const nextQuantity = Number(event.target.value);
                    setQuantity(Number.isFinite(nextQuantity) ? Math.max(1, nextQuantity) : 1);
                  }}
              className="ui-control h-12 rounded-[var(--radius-control)] border border-slate-200 px-4 outline-none transition focus:border-slate-950"
                />
              </label>
              <div className="flex items-end">
                <button
                  type="submit"
                  disabled={!selectedVariant || selectedVariant.stockQuantity <= 0 || addToCartMutation.isPending}
                  className="ui-control h-12 w-full rounded-[var(--radius-control)] bg-slate-950 px-6 text-white transition hover:bg-slate-800 disabled:cursor-not-allowed disabled:opacity-50"
                >
                  {addToCartMutation.isPending ? "Adding..." : "Add to cart"}
                </button>
              </div>
            </div>

            {addToCartMutation.isSuccess && (
              <div className="ui-control mt-5 rounded-[var(--radius-card)] bg-emerald-50 p-4 text-emerald-700">
                Added to cart. <button type="button" onClick={() => openCartDrawer()} className="underline">Review cart</button>
              </div>
            )}
            {addToCartMutation.isError && (
              <div className="ui-control mt-5 rounded-[var(--radius-card)] bg-red-50 p-4 text-red-700">
                {getApiErrorMessage(addToCartMutation.error)}
              </div>
            )}
          </form>

          <div className="ui-card border border-slate-100 p-4 md:p-8">
            <h2 className="ui-h2 text-slate-950">Specifications</h2>
            {product.specifications.length === 0 && <p className="ui-body mt-4 text-slate-500">No specifications available.</p>}
            <div className="mt-4 grid gap-3">
              {product.specifications.map((specification) => (
                <div key={specification.id} className="flex justify-between gap-5 rounded-[var(--radius-card)] bg-slate-50 px-5 py-4">
                  <p className="ui-control text-slate-500">{specification.name}</p>
                  <p className="ui-control text-right text-slate-950">{specification.value}</p>
                </div>
              ))}
            </div>
          </div>
        </div>
      </section>
    </div>
  );
}
