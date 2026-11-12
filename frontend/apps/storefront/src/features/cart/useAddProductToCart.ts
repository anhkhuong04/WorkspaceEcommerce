import { useMutation } from "@tanstack/react-query";
import type { RefObject } from "react";
import { useNavigate } from "react-router-dom";
import { storefrontApi } from "../../services/api/storefrontApi";
import { useStorefrontCart } from "./StorefrontCartContext";

export function useAddProductToCart(returnFocusRef?: RefObject<HTMLElement | null>) {
  const { cartSessionId, openCartDrawer } = useStorefrontCart();
  const navigate = useNavigate();

  return useMutation({
    mutationFn: async (productSlug: string) => {
      const product = await storefrontApi.getProduct(productSlug);
      if (product.variants.length !== 1) {
        navigate(`/products/${encodeURIComponent(productSlug)}`);
        return null;
      }
      const variant = product.variants.find((item) => item.stockQuantity > 0);

      if (!variant) {
        throw new Error("Product is out of stock.");
      }

      return storefrontApi.addCartItem({
        sessionId: cartSessionId,
        productVariantId: variant.id,
        quantity: 1
      });
    },
    onSuccess: (nextCart) => {
      if (nextCart) openCartDrawer(nextCart, returnFocusRef?.current ?? undefined);
    }
  });
}
