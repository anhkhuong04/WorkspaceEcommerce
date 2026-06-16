import { useMutation } from "@tanstack/react-query";
import { storefrontApi } from "../../services/api/storefrontApi";
import { useStorefrontCart } from "./StorefrontCartContext";

export function useAddProductToCart() {
  const { cartSessionId, openCartDrawer } = useStorefrontCart();

  return useMutation({
    mutationFn: async (productSlug: string) => {
      const product = await storefrontApi.getProduct(productSlug);
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
      openCartDrawer(nextCart);
    }
  });
}
