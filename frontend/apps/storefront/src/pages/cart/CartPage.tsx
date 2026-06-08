import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { formatMoney } from "@workspace-ecommerce/shared-utils";
import { Link } from "react-router-dom";
import { PageHeader } from "../../components/ui/PageHeader";
import { getApiErrorMessage } from "../../services/api/errors";
import { storefrontApi } from "../../services/api/storefrontApi";
import { getCartSessionId, resetCartSessionId } from "../../services/cartSession";

export function CartPage() {
  const queryClient = useQueryClient();
  const cartSessionId = getCartSessionId();
  const cartQueryKey = ["storefront", "cart", cartSessionId];

  const cartQuery = useQuery({
    queryKey: cartQueryKey,
    queryFn: () => storefrontApi.getCart(cartSessionId)
  });

  const updateItemMutation = useMutation({
    mutationFn: ({ itemId, quantity }: { itemId: string; quantity: number }) =>
      storefrontApi.updateCartItem(itemId, { sessionId: cartSessionId, quantity }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: cartQueryKey });
    }
  });

  const removeItemMutation = useMutation({
    mutationFn: (itemId: string) => storefrontApi.removeCartItem(itemId, cartSessionId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: cartQueryKey });
    }
  });

  function startNewCart() {
    resetCartSessionId();
    window.location.reload();
  }

  return (
    <div className="grid gap-6">
      <PageHeader
        eyebrow="Cart"
        title="Review live cart items before checkout."
        description="Cart data is loaded from /api/cart using the current browser session id."
      />

      <section className="rounded-[2rem] border border-slate-200 bg-white p-6 shadow-sm">
        <div className="flex flex-wrap items-center justify-between gap-3 border-b border-slate-100 pb-5">
          <div>
            <p className="text-xs font-bold uppercase tracking-[0.16em] text-slate-500">Session</p>
            <p className="mt-1 break-all font-mono text-sm font-bold text-slate-800">{cartSessionId}</p>
          </div>
          <button
            type="button"
            onClick={startNewCart}
            className="rounded-full border border-slate-200 px-4 py-2 text-sm font-black text-slate-700 transition hover:border-slate-300"
          >
            Start new cart
          </button>
        </div>

        {cartQuery.isLoading && <div className="mt-6 rounded-2xl bg-slate-50 p-6 text-slate-500">Loading cart...</div>}
        {cartQuery.isError && <div className="mt-6 rounded-2xl bg-red-50 p-6 font-semibold text-red-700">{getApiErrorMessage(cartQuery.error)}</div>}

        {cartQuery.data && cartQuery.data.items.length === 0 && (
          <div className="mt-6 rounded-2xl bg-slate-50 p-8 text-center">
            <p className="font-bold text-slate-700">Your cart is empty.</p>
            <Link to="/products" className="mt-4 inline-flex rounded-full bg-slate-950 px-5 py-3 text-sm font-black text-white">
              Browse products
            </Link>
          </div>
        )}

        {cartQuery.data && cartQuery.data.items.length > 0 && (
          <div className="mt-6 grid gap-6 lg:grid-cols-[1fr_360px]">
            <div className="grid gap-3">
              {cartQuery.data.items.map((item) => (
                <div key={item.id} className="grid gap-4 rounded-2xl border border-slate-100 bg-slate-50 p-5 md:grid-cols-[1fr_160px_120px_auto] md:items-center">
                  <div>
                    <p className="text-xs font-bold uppercase tracking-[0.14em] text-slate-400">Variant</p>
                    <p className="mt-1 break-all font-mono text-sm font-black text-slate-900">{item.productVariantId}</p>
                    <p className="mt-2 text-sm font-semibold text-slate-500">Unit price {formatMoney(item.unitPriceSnapshot)}</p>
                  </div>
                  <label className="grid gap-2">
                    <span className="text-xs font-bold uppercase tracking-[0.14em] text-slate-400">Quantity</span>
                    <input
                      type="number"
                      min={1}
                      defaultValue={item.quantity}
                      onBlur={(event) => {
                        const quantity = Number(event.target.value);
                        if (quantity > 0 && quantity !== item.quantity) {
                          updateItemMutation.mutate({ itemId: item.id, quantity });
                        }
                      }}
                      className="h-11 rounded-2xl border border-slate-200 bg-white px-4 font-bold outline-none transition focus:border-emerald-400"
                    />
                  </label>
                  <div>
                    <p className="text-xs font-bold uppercase tracking-[0.14em] text-slate-400">Line total</p>
                    <p className="mt-1 text-lg font-black text-[var(--brand)]">{formatMoney(item.lineTotal)}</p>
                  </div>
                  <button
                    type="button"
                    onClick={() => removeItemMutation.mutate(item.id)}
                    disabled={removeItemMutation.isPending}
                    className="rounded-full border border-red-100 bg-white px-4 py-2 text-sm font-black text-red-600 transition hover:bg-red-50 disabled:cursor-not-allowed disabled:opacity-50"
                  >
                    Remove
                  </button>
                </div>
              ))}
              {(updateItemMutation.isError || removeItemMutation.isError) && (
                <div className="rounded-2xl bg-red-50 p-4 text-sm font-bold text-red-700">
                  {getApiErrorMessage(updateItemMutation.error ?? removeItemMutation.error)}
                </div>
              )}
            </div>

            <aside className="h-fit rounded-[1.5rem] border border-slate-200 bg-white p-6 shadow-sm">
              <p className="text-sm font-bold uppercase tracking-[0.18em] text-emerald-700">Summary</p>
              <div className="mt-5 grid gap-3 border-b border-slate-100 pb-5">
                <div className="flex justify-between text-sm font-bold text-slate-500">
                  <span>Total quantity</span>
                  <span>{cartQuery.data.totalQuantity}</span>
                </div>
                <div className="flex justify-between text-sm font-bold text-slate-500">
                  <span>Total amount</span>
                  <span>{formatMoney(cartQuery.data.totalAmount)}</span>
                </div>
              </div>
              <Link
                to="/checkout"
                className="mt-5 inline-flex h-12 w-full items-center justify-center rounded-full bg-slate-950 px-6 text-sm font-black text-white transition hover:bg-slate-800"
              >
                Continue to checkout
              </Link>
            </aside>
          </div>
        )}
      </section>
    </div>
  );
}
