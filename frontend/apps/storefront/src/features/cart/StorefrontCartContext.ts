import type { CartDto } from "@workspace-ecommerce/api-types";
import { createContext, useContext } from "react";

export type CartQueryKey = ["storefront", "cart", string];

export interface StorefrontCartContextValue {
  cart: CartDto | undefined;
  cartItemCount: number;
  cartQueryKey: CartQueryKey;
  cartSessionId: string;
  isCartDrawerOpen: boolean;
  openCartDrawer: (cartData?: CartDto) => void;
  closeCartDrawer: () => void;
  resetCartSession: () => string;
}

export const StorefrontCartContext = createContext<StorefrontCartContextValue | null>(null);

export function useStorefrontCart() {
  const context = useContext(StorefrontCartContext);
  if (!context) {
    throw new Error("useStorefrontCart must be used inside StorefrontCartProvider.");
  }

  return context;
}
