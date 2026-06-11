import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import type { CartDto, CartItemDto } from "@workspace-ecommerce/api-types";
import { formatMoney } from "@workspace-ecommerce/shared-utils";
import type { ReactNode } from "react";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { getApiErrorMessage } from "../../services/api/errors";
import { storefrontApi } from "../../services/api/storefrontApi";
import { getCartSessionId } from "../../services/cartSession";
import { StorefrontCartContext, type CartQueryKey, type StorefrontCartContextValue } from "./StorefrontCartContext";

function createCartQueryKey(sessionId: string): CartQueryKey {
  return ["storefront", "cart", sessionId];
}

export function StorefrontCartProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient();
  const cartSessionId = useMemo(() => getCartSessionId(), []);
  const cartQueryKey = useMemo(() => createCartQueryKey(cartSessionId), [cartSessionId]);
  const [isCartDrawerOpen, setIsCartDrawerOpen] = useState(false);

  const cartQuery = useQuery({
    queryKey: cartQueryKey,
    queryFn: () => storefrontApi.getCart(cartSessionId)
  });

  const openCartDrawer = useCallback((cartData?: CartDto) => {
    if (cartData) {
      queryClient.setQueryData(cartQueryKey, cartData);
    }

    setIsCartDrawerOpen(true);
  }, [cartQueryKey, queryClient]);

  const closeCartDrawer = useCallback(() => {
    setIsCartDrawerOpen(false);
  }, []);

  const contextValue = useMemo<StorefrontCartContextValue>(() => ({
    cart: cartQuery.data,
    cartItemCount: cartQuery.data?.totalQuantity ?? 0,
    cartQueryKey,
    cartSessionId,
    isCartDrawerOpen,
    openCartDrawer,
    closeCartDrawer
  }), [cartQuery.data, cartQueryKey, cartSessionId, closeCartDrawer, isCartDrawerOpen, openCartDrawer]);

  return (
    <StorefrontCartContext.Provider value={contextValue}>
      {children}
      <ShoppingCartDrawer
        cart={cartQuery.data}
        cartError={cartQuery.error}
        cartQueryKey={cartQueryKey}
        cartSessionId={cartSessionId}
        isLoading={cartQuery.isLoading}
        isOpen={isCartDrawerOpen}
        onClose={closeCartDrawer}
      />
    </StorefrontCartContext.Provider>
  );
}
interface ShoppingCartDrawerProps {
  cart: CartDto | undefined;
  cartError: unknown;
  cartQueryKey: CartQueryKey;
  cartSessionId: string;
  isLoading: boolean;
  isOpen: boolean;
  onClose: () => void;
}

function ShoppingCartDrawer({ cart, cartError, cartQueryKey, cartSessionId, isLoading, isOpen, onClose }: ShoppingCartDrawerProps) {
  const queryClient = useQueryClient();
  const closeButtonRef = useRef<HTMLButtonElement>(null);
  const items = cart?.items ?? [];
  const totalQuantity = cart?.totalQuantity ?? 0;
  const totalAmount = cart?.totalAmount ?? 0;

  const updateItemMutation = useMutation({
    mutationFn: ({ itemId, quantity }: { itemId: string; quantity: number }) =>
      storefrontApi.updateCartItem(itemId, { sessionId: cartSessionId, quantity }),
    onSuccess: (nextCart) => {
      queryClient.setQueryData(cartQueryKey, nextCart);
    }
  });

  const removeItemMutation = useMutation({
    mutationFn: (itemId: string) => storefrontApi.removeCartItem(itemId, cartSessionId),
    onSuccess: (nextCart) => {
      queryClient.setQueryData(cartQueryKey, nextCart);
    }
  });

  const clearCartMutation = useMutation({
    mutationFn: async (cartItems: CartItemDto[]) => {
      let nextCart: CartDto | undefined;
      for (const item of cartItems) {
        nextCart = await storefrontApi.removeCartItem(item.id, cartSessionId);
      }
      return nextCart;
    },
    onSuccess: (nextCart) => {
      if (nextCart) {
        queryClient.setQueryData(cartQueryKey, nextCart);
      }
    }
  });

  useEffect(() => {
    if (!isOpen) {
      return;
    }

    const originalOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    window.setTimeout(() => closeButtonRef.current?.focus(), 0);

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === "Escape") {
        onClose();
      }
    }

    document.addEventListener("keydown", handleKeyDown);
    return () => {
      document.body.style.overflow = originalOverflow;
      document.removeEventListener("keydown", handleKeyDown);
    };
  }, [isOpen, onClose]);

  if (!isOpen) {
    return null;
  }

  const mutationError = updateItemMutation.error ?? removeItemMutation.error ?? clearCartMutation.error;
  const isBusy = updateItemMutation.isPending || removeItemMutation.isPending || clearCartMutation.isPending;

  return (
    <div className="fixed inset-0 z-[80] bg-slate-950/55 backdrop-blur-[2px]" role="presentation">
      <aside
        className="ml-auto flex h-full w-full max-w-[min(90vw,620px)] flex-col rounded-l-[var(--radius-card)] bg-white shadow-2xl outline-none"
        role="dialog"
        aria-modal="true"
        aria-labelledby="shopping-cart-drawer-title"
      >
        <div className="flex items-center justify-between gap-5 px-7 py-7 sm:px-10">
          <div className="flex items-center gap-3">
            <h2 id="shopping-cart-drawer-title" className="ui-h2 tracking-[-0.04em] text-slate-950">Cart</h2>
            <span className="ui-control grid h-9 min-w-9 place-items-center rounded-full bg-slate-950 px-3 text-white">{totalQuantity}</span>
          </div>
          <div className="flex items-center gap-5">
            <button
              type="button"
              disabled={items.length === 0 || isBusy}
              onClick={() => clearCartMutation.mutate(items)}
              className="ui-control text-slate-400 transition hover:text-slate-700 disabled:cursor-not-allowed disabled:opacity-40"
            >
              Clear all
            </button>
            <button
              ref={closeButtonRef}
              type="button"
              onClick={onClose}
              className="grid h-11 w-11 place-items-center rounded-[var(--radius-control)] text-slate-900 transition hover:bg-slate-100"
              aria-label="Close cart"
            >
              <svg className="h-6 w-6" viewBox="0 0 24 24" fill="none" aria-hidden="true">
                <path d="M6 6l12 12M18 6 6 18" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
              </svg>
            </button>
          </div>
        </div>

        <div className="min-h-0 flex-1 overflow-y-auto px-7 pb-6 sm:px-10">
          {isLoading ? <CartDrawerMessage>Loading cart...</CartDrawerMessage> : null}
          {cartError ? <CartDrawerMessage tone="error">{getApiErrorMessage(cartError)}</CartDrawerMessage> : null}
          {mutationError ? <CartDrawerMessage tone="error">{getApiErrorMessage(mutationError)}</CartDrawerMessage> : null}

          {!isLoading && !cartError && items.length === 0 ? (
            <div className="grid h-full min-h-[320px] place-items-center rounded-[var(--radius-card)] border border-dashed border-slate-200 bg-slate-50 px-6 text-center">
              <div>
                <p className="ui-h3 text-slate-950">Your cart is empty.</p>
                <p className="ui-body mt-2 text-slate-500">Add products to continue checkout.</p>
                <Link
                  to="/products"
                  onClick={onClose}
                  className="ui-control mt-5 inline-flex rounded-[var(--radius-control)] bg-slate-950 px-6 py-3 text-white transition hover:bg-slate-800"
                >
                  Browse products
                </Link>
              </div>
            </div>
          ) : null}

          <div className="grid gap-6">
            {items.map((item) => (
              <article key={item.id} className="grid grid-cols-[112px_1fr_auto] gap-5 sm:grid-cols-[128px_1fr_88px]">
                <div className="grid aspect-square place-items-center rounded-[var(--radius-card)] bg-slate-100 text-slate-400">
                  <svg className="h-10 w-10" viewBox="0 0 24 24" fill="none" aria-hidden="true">
                    <path d="M4 8h16v11H4z" stroke="currentColor" strokeWidth="1.7" />
                    <path d="M8 8a4 4 0 0 1 8 0" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" />
                  </svg>
                </div>
                <div className="min-w-0 py-2">
                  <p className="ui-body text-slate-500">Variant</p>
                  <h3 className="ui-h3 mt-2 truncate text-slate-950">{shortenVariantId(item.productVariantId)}</h3>
                  <p className="ui-price mt-3 text-slate-500">{formatMoney(item.unitPriceSnapshot)}</p>
                </div>
                <div className="grid justify-items-end gap-3 py-2">
                  <label className="sr-only" htmlFor={`cart-quantity-${item.id}`}>Quantity</label>
                  <input
                    id={`cart-quantity-${item.id}`}
                    type="number"
                    min={1}
                    value={item.quantity}
                    disabled={isBusy}
                    onChange={(event) => {
                      const quantity = Number(event.target.value);
                      if (Number.isFinite(quantity) && quantity > 0) {
                        updateItemMutation.mutate({ itemId: item.id, quantity });
                      }
                    }}
                    className="ui-control h-14 w-[72px] rounded-[var(--radius-control)] border border-slate-200 bg-white text-center text-slate-600 outline-none transition focus:border-slate-400 disabled:bg-slate-100"
                  />
                  <button
                    type="button"
                    disabled={isBusy}
                    onClick={() => removeItemMutation.mutate(item.id)}
                    className="ui-control text-slate-500 transition hover:text-red-600 disabled:cursor-not-allowed disabled:opacity-40"
                  >
                    Remove
                  </button>
                </div>
              </article>
            ))}
          </div>
        </div>

        <div className="border-t border-slate-200 px-7 py-7 sm:px-10">
          <div className="flex items-center justify-between gap-6">
            <p className="ui-h2 tracking-[-0.04em] text-slate-950">Total</p>
            <p className="ui-h2 tracking-[-0.04em] text-slate-950">{formatMoney(totalAmount)}</p>
          </div>

          <div className="mt-7 flex h-14 items-center rounded-[var(--radius-control)] border border-slate-200 bg-white pl-6 pr-2 text-slate-400">
            <span className="ui-control flex-1">Enter discount code</span>
            <button type="button" className="grid h-11 w-11 place-items-center rounded-full bg-slate-200 text-white" aria-label="Apply discount code" disabled>
              <svg className="h-5 w-5" viewBox="0 0 24 24" fill="none" aria-hidden="true">
                <path d="M5 12h14m-6-6 6 6-6 6" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" />
              </svg>
            </button>
          </div>

          <Link
            to="/checkout"
            onClick={onClose}
            aria-disabled={items.length === 0}
            className={`ui-control mt-7 inline-flex h-16 w-full items-center justify-center gap-3 rounded-[var(--radius-control)] px-6 transition ${
              items.length === 0 ? "pointer-events-none bg-slate-200 text-slate-400" : "bg-[#171717] text-white hover:bg-black"
            }`}
          >
            <svg className="h-5 w-5" viewBox="0 0 24 24" fill="none" aria-hidden="true">
              <path d="M7 10V8a5 5 0 0 1 10 0v2" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
              <rect x="5" y="10" width="14" height="10" rx="3" stroke="currentColor" strokeWidth="2" />
            </svg>
            Checkout
          </Link>
        </div>
      </aside>
    </div>
  );
}

function CartDrawerMessage({ children, tone = "info" }: { children: ReactNode; tone?: "info" | "error" }) {
  return (
    <div className={`ui-control mb-4 rounded-[var(--radius-card)] px-5 py-4 ${tone === "error" ? "bg-red-50 text-red-700" : "bg-slate-50 text-slate-500"}`}>
      {children}
    </div>
  );
}

function shortenVariantId(value: string) {
  return value.length <= 12 ? value : `${value.slice(0, 8)}...${value.slice(-4)}`;
}



