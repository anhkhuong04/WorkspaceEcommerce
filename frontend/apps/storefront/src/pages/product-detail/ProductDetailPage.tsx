import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { formatMoney } from "@workspace-ecommerce/shared-utils";
import type { FormEvent } from "react";
import { useState } from "react";
import { Link, useParams } from "react-router-dom";
import { PageHeader } from "../../components/ui/PageHeader";
import { getApiErrorMessage } from "../../services/api/errors";
import { storefrontApi } from "../../services/api/storefrontApi";
import { getCartSessionId } from "../../services/cartSession";

export function ProductDetailPage() {
  const { slug = "" } = useParams();
  const queryClient = useQueryClient();
  const cartSessionId = getCartSessionId();
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
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["storefront", "cart", cartSessionId] });
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
    return <div className="rounded-3xl bg-white p-8 text-slate-500 shadow-sm">Loading product...</div>;
  }

  if (productQuery.isError || !product) {
    return (
      <div className="rounded-3xl bg-red-50 p-8 font-semibold text-red-700">
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
              className="aspect-square w-full rounded-[2rem] object-cover shadow-sm ring-1 ring-slate-200"
            />
          ) : (
            <div className="aspect-square rounded-[2rem] bg-[radial-gradient(circle_at_top_left,#bbf7d0,#f8fafc_55%,#dbeafe)] shadow-inner" />
          )}
          {product.images.length > 1 && (
            <div className="grid grid-cols-4 gap-3">
              {product.images.slice(1, 5).map((image) => (
                <img key={image.id} src={image.imageUrl} alt={image.altText ?? product.name} className="aspect-square rounded-2xl object-cover ring-1 ring-slate-200" />
              ))}
            </div>
          )}
        </div>

        <div className="grid gap-5">
          <form onSubmit={addToCart} className="rounded-[2rem] border border-slate-200 bg-white p-8 shadow-sm">
            <div className="flex items-start justify-between gap-5">
              <div>
                <p className="text-sm font-bold uppercase tracking-[0.18em] text-emerald-700">Variant</p>
                <h2 className="mt-2 text-2xl font-black text-slate-950">Choose configuration</h2>
              </div>
              {selectedVariant && <p className="text-3xl font-black text-[var(--brand)]">{formatMoney(selectedVariant.price)}</p>}
            </div>

            <div className="mt-6 grid gap-3">
              {product.variants.map((variant) => (
                <label
                  key={variant.id}
                  className={`flex cursor-pointer items-center justify-between gap-4 rounded-2xl border p-4 transition ${
                    selectedVariant?.id === variant.id ? "border-emerald-300 bg-emerald-50" : "border-slate-100 bg-slate-50 hover:border-slate-200"
                  }`}
                >
                  <span className="flex items-start gap-3">
                    <input
                      type="radio"
                      name="variant"
                      value={variant.id}
                      checked={selectedVariant?.id === variant.id}
                      onChange={() => setSelectedVariantId(variant.id)}
                      className="mt-1 h-4 w-4 accent-emerald-600"
                    />
                    <span>
                      <span className="block font-black text-slate-950">{variant.name}</span>
                      <span className="text-sm text-slate-500">
                        {variant.sku} | {variant.color ?? "Default"} {variant.size ? `| ${variant.size}` : ""} | Stock {variant.stockQuantity}
                      </span>
                    </span>
                  </span>
                  <span className="text-right">
                    <span className="block font-black text-slate-950">{formatMoney(variant.price)}</span>
                    {variant.compareAtPrice !== null && <span className="text-xs font-bold text-slate-400 line-through">{formatMoney(variant.compareAtPrice)}</span>}
                  </span>
                </label>
              ))}
            </div>

            <div className="mt-6 grid gap-4 sm:grid-cols-[160px_1fr]">
              <label className="grid gap-2">
                <span className="text-xs font-bold uppercase tracking-[0.16em] text-slate-500">Quantity</span>
                <input
                  type="number"
                  min={1}
                  max={selectedVariant?.stockQuantity ?? 1}
                  value={quantity}
                  onChange={(event) => {
                    const nextQuantity = Number(event.target.value);
                    setQuantity(Number.isFinite(nextQuantity) ? Math.max(1, nextQuantity) : 1);
                  }}
                  className="h-12 rounded-2xl border border-slate-200 px-4 font-bold outline-none transition focus:border-emerald-400"
                />
              </label>
              <div className="flex items-end">
                <button
                  type="submit"
                  disabled={!selectedVariant || selectedVariant.stockQuantity <= 0 || addToCartMutation.isPending}
                  className="h-12 w-full rounded-full bg-slate-950 px-6 text-sm font-black text-white transition hover:bg-slate-800 disabled:cursor-not-allowed disabled:opacity-50"
                >
                  {addToCartMutation.isPending ? "Adding..." : "Add to cart"}
                </button>
              </div>
            </div>

            {addToCartMutation.isSuccess && (
              <div className="mt-5 rounded-2xl bg-emerald-50 p-4 text-sm font-bold text-emerald-700">
                Added to cart. <Link to="/cart" className="underline">Review cart</Link>
              </div>
            )}
            {addToCartMutation.isError && (
              <div className="mt-5 rounded-2xl bg-red-50 p-4 text-sm font-bold text-red-700">
                {getApiErrorMessage(addToCartMutation.error)}
              </div>
            )}
          </form>

          <div className="rounded-[2rem] border border-slate-200 bg-white p-8 shadow-sm">
            <h2 className="text-xl font-black text-slate-950">Specifications</h2>
            {product.specifications.length === 0 && <p className="mt-4 text-sm text-slate-500">No specifications available.</p>}
            <div className="mt-4 grid gap-3">
              {product.specifications.map((specification) => (
                <div key={specification.id} className="flex justify-between gap-5 rounded-2xl bg-slate-50 px-5 py-4">
                  <p className="font-bold text-slate-500">{specification.name}</p>
                  <p className="text-right font-black text-slate-950">{specification.value}</p>
                </div>
              ))}
            </div>
          </div>
        </div>
      </section>
    </div>
  );
}
