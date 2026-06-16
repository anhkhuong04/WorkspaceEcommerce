import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import type { CartDto, CartItemDto } from "@workspace-ecommerce/api-types";
import type { ReactNode, RefObject } from "react";
import { forwardRef, useCallback, useEffect, useMemo, useRef, useState } from "react";
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
  const hasItems = items.length > 0;

  return (
    <div className="fixed inset-0 z-[80] grid place-items-center bg-black/35 p-3 sm:p-6" role="presentation">
      <aside
        className="flex h-[min(88vh,760px)] w-full max-w-[min(94vw,900px)] flex-col overflow-hidden rounded-2xl bg-white shadow-2xl outline-none"
        role="dialog"
        aria-modal="true"
        aria-labelledby="shopping-cart-drawer-title"
      >
        {hasItems ? (
          <CartDrawerHeader
            closeButtonRef={closeButtonRef}
            isBusy={isBusy}
            itemCount={totalQuantity}
            onClear={() => clearCartMutation.mutate(items)}
            onClose={onClose}
          />
        ) : (
          <div className="flex justify-end px-5 py-5 sm:px-8 sm:py-6">
            <CartCloseButton ref={closeButtonRef} onClose={onClose} />
          </div>
        )}

        {hasItems ? (
          <>
            <div className="min-h-0 flex-1 overflow-y-auto px-5 pb-5 sm:px-8 sm:pb-6">
              <CartStatusMessages cartError={cartError} isLoading={isLoading} mutationError={mutationError} />

              <div className="grid gap-5">
                {items.map((item) => (
                  <article key={item.id} className="grid grid-cols-[86px_minmax(0,1fr)_60px] gap-3 sm:grid-cols-[118px_minmax(0,1fr)_76px] sm:gap-5">
                    <CartItemImage item={item} />
                    <div className="min-w-0 py-0.5 sm:py-3">
                      <p className="truncate text-sm font-medium text-slate-500 sm:text-base">{item.productName}</p>
                      <h3 className="mt-0.5 truncate text-base font-bold text-slate-800 sm:text-lg">{item.variantName}</h3>
                      <p className="mt-2 text-sm font-medium text-slate-500 sm:text-base">{formatCartMoney(item.unitPriceSnapshot, false)}</p>
                      {formatVariantOptions(item) ? (
                        <p className="mt-1 truncate text-sm font-medium text-slate-500 sm:text-base">{formatVariantOptions(item)}</p>
                      ) : null}
                    </div>
                    <div className="grid content-start justify-items-end gap-2 py-0.5 sm:py-5">
                      <label className="sr-only" htmlFor={`cart-quantity-${item.id}`}>Quantity</label>
                      <input
                        id={`cart-quantity-${item.id}`}
                        type="text"
                        inputMode="numeric"
                        value={item.quantity}
                        disabled={isBusy}
                        onChange={(event) => {
                          const quantity = Number(event.target.value);
                          if (Number.isFinite(quantity) && quantity > 0) {
                            updateItemMutation.mutate({ itemId: item.id, quantity });
                          }
                        }}
                        className="h-10 w-14 rounded-lg border border-slate-200 bg-white text-center text-sm font-medium text-slate-500 outline-none transition focus:border-slate-400 disabled:bg-slate-100 sm:h-12 sm:w-16 sm:text-base"
                      />
                      <button
                        type="button"
                        disabled={isBusy}
                        onClick={() => removeItemMutation.mutate(item.id)}
                        className="text-xs font-bold text-slate-500 transition hover:text-red-600 disabled:cursor-not-allowed disabled:opacity-40 sm:text-sm"
                      >
                        Remove
                      </button>
                    </div>
                  </article>
                ))}
              </div>
            </div>

            <CartDrawerFooter totalAmount={totalAmount} onClose={onClose} />
          </>
        ) : (
          <div className="grid min-h-0 flex-1 place-items-center px-5 pb-16 text-center sm:px-8 sm:pb-20">
            <div className="w-full max-w-sm">
              <CartStatusMessages cartError={cartError} isLoading={isLoading} mutationError={mutationError} />
              {!isLoading && !cartError ? <EmptyCartState onClose={onClose} /> : null}
            </div>
          </div>
        )}
      </aside>
    </div>
  );
}

function CartDrawerHeader({
  closeButtonRef,
  isBusy,
  itemCount,
  onClear,
  onClose
}: {
  closeButtonRef: RefObject<HTMLButtonElement | null>;
  isBusy: boolean;
  itemCount: number;
  onClear: () => void;
  onClose: () => void;
}) {
  return (
    <div className="flex items-center justify-between gap-4 px-5 py-5 sm:px-8 sm:py-6">
      <div className="flex items-center gap-2.5">
        <h2 id="shopping-cart-drawer-title" className="text-2xl font-medium tracking-tight text-slate-800">Cart</h2>
        <span className="grid h-7 min-w-7 place-items-center rounded-full bg-[#171717] px-2 text-sm font-bold text-white">{itemCount}</span>
      </div>
      <div className="flex items-center gap-4 sm:gap-6">
        <button
          type="button"
          disabled={isBusy}
          onClick={onClear}
          className="text-sm font-medium text-slate-400 transition hover:text-slate-700 disabled:cursor-not-allowed disabled:opacity-40 sm:text-base"
        >
          Xoá tất cả
        </button>
        <CartCloseButton ref={closeButtonRef} onClose={onClose} />
      </div>
    </div>
  );
}

const CartCloseButton = forwardRef<HTMLButtonElement, { onClose: () => void }>(function CartCloseButton({ onClose }, ref) {
  return (
    <button
      ref={ref}
      type="button"
      onClick={onClose}
      className="grid h-9 w-9 place-items-center rounded-lg text-slate-900 transition hover:bg-slate-100"
      aria-label="Close cart"
    >
      <svg className="h-6 w-6" viewBox="0 0 24 24" fill="none" aria-hidden="true">
        <path d="M6 6l12 12M18 6 6 18" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
      </svg>
    </button>
  );
});

function EmptyCartState({ onClose }: { onClose: () => void }) {
  return (
    <div className="grid justify-items-center">
      <div className="relative h-16 w-16 text-slate-800">
        <svg className="absolute left-2.5 top-4 h-11 w-11" viewBox="0 0 64 64" fill="none" aria-hidden="true">
          <path d="M18 24h30l-3 29H21z" stroke="currentColor" strokeWidth="4" strokeLinejoin="round" />
          <path d="M25 25c0 8 14 8 14 0" stroke="currentColor" strokeWidth="4" strokeLinecap="round" />
        </svg>
        <span className="absolute right-0.5 top-1.5 grid h-7 w-7 place-items-center rounded-full bg-[#171717] text-sm font-bold text-white">0</span>
      </div>
      <p className="mt-6 text-xl font-medium text-slate-800">Giỏ hàng đang trống</p>
      <Link
        to="/products"
        onClick={onClose}
        className="mt-8 inline-flex h-14 min-w-[220px] items-center justify-center rounded-full bg-[#171717] px-8 text-base font-bold text-white transition hover:bg-black"
      >
        Tiếp tục mua sắm
      </Link>
    </div>
  );
}

function CartStatusMessages({
  cartError,
  isLoading,
  mutationError
}: {
  cartError: unknown;
  isLoading: boolean;
  mutationError: unknown;
}) {
  return (
    <>
      {isLoading ? <CartDrawerMessage>Loading cart...</CartDrawerMessage> : null}
      {cartError ? <CartDrawerMessage tone="error">{getApiErrorMessage(cartError)}</CartDrawerMessage> : null}
      {mutationError ? <CartDrawerMessage tone="error">{getApiErrorMessage(mutationError)}</CartDrawerMessage> : null}
    </>
  );
}

function CartItemImage({ item }: { item: CartItemDto }) {
  return (
    <div className="grid aspect-square place-items-center overflow-hidden rounded-md bg-[#f1f1f1]">
      {item.imageUrl ? (
        <img src={item.imageUrl} alt={item.productName} className="h-full w-full object-contain p-3" />
      ) : (
        <svg className="h-10 w-10 text-slate-400" viewBox="0 0 24 24" fill="none" aria-hidden="true">
          <path d="M4 8h16v11H4z" stroke="currentColor" strokeWidth="1.7" />
          <path d="M8 8a4 4 0 0 1 8 0" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" />
        </svg>
      )}
    </div>
  );
}

function CartDrawerFooter({ totalAmount, onClose }: { totalAmount: number; onClose: () => void }) {
  return (
    <div className="border-t border-slate-200 px-5 py-5 sm:px-8 sm:py-6">
      <div className="flex items-center justify-between gap-6">
        <p className="text-2xl font-bold tracking-tight text-slate-800">Total</p>
        <p className="text-2xl font-bold tracking-tight text-slate-800">{formatCartMoney(totalAmount)}</p>
      </div>

      <div className="mt-6 flex h-12 items-center rounded-full border border-slate-200 bg-white pl-5 pr-1.5 text-slate-400">
        <span className="min-w-0 flex-1 truncate text-sm font-medium sm:text-base">Nhập mã giảm giá khác</span>
        <button type="button" className="grid h-9 w-9 place-items-center rounded-full bg-slate-200 text-white" aria-label="Apply discount code" disabled>
          <svg className="h-5 w-5" viewBox="0 0 24 24" fill="none" aria-hidden="true">
            <path d="M5 12h14m-6-6 6 6-6 6" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" />
          </svg>
        </button>
      </div>

      <Link
        to="/checkout"
        onClick={onClose}
        className="mt-5 inline-flex h-14 w-full items-center justify-center gap-3 rounded-full bg-[#171717] px-6 text-base font-bold text-white transition hover:bg-black"
      >
        <svg className="h-5 w-5" viewBox="0 0 24 24" fill="none" aria-hidden="true">
          <path d="M7 10V8a5 5 0 0 1 10 0v2" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
          <rect x="5" y="10" width="14" height="10" rx="3" stroke="currentColor" strokeWidth="2" />
          <circle cx="12" cy="15" r="1.5" fill="currentColor" />
        </svg>
        Checkout
      </Link>
    </div>
  );
}

function CartDrawerMessage({ children, tone = "info" }: { children: ReactNode; tone?: "info" | "error" }) {
  return (
    <div className={`mb-4 rounded-lg px-5 py-4 text-sm font-semibold ${tone === "error" ? "bg-red-50 text-red-700" : "bg-slate-50 text-slate-500"}`}>
      {children}
    </div>
  );
}

function formatVariantOptions(item: CartItemDto) {
  return [item.variantColor, item.variantSize].filter(Boolean).join(" / ");
}

function formatCartMoney(value: number, includeCurrency = true) {
  const formatted = new Intl.NumberFormat("vi-VN", {
    maximumFractionDigits: 0
  }).format(value);

  return includeCurrency ? `${formatted} VND` : formatted;
}



