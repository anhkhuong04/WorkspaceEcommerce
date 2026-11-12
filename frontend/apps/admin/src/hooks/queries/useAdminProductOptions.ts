import { useQuery, useQueryClient } from "@tanstack/react-query";
import type { AdminProductDto } from "@workspace-ecommerce/api-types";
import { adminApi } from "../../services/api/adminApi";

// Coupon/article pickers need every product, rather than the first API page.
export function useAdminProductOptions() {
  const queryClient = useQueryClient();
  return useQuery({
    queryKey: ["admin-products", "options"],
    queryFn: async ({ signal }) => {
      const products = new Map<string, AdminProductDto>();
      let pageNumber = 1;
      while (!signal.aborted) {
        const request = { pageNumber, pageSize: 100 };
        const result = await queryClient.fetchQuery({
          queryKey: ["admin-products", request],
          queryFn: () => adminApi.getProducts(request),
        });
        if (signal.aborted)
          throw new DOMException("Picker cancelled", "AbortError");
        result.items.forEach((product) => products.set(product.id, product));
        if (!result.hasNextPage)
          return {
            items: [...products.values()],
            totalCount: result.totalCount,
          };
        pageNumber += 1;
      }
      throw new DOMException("Picker cancelled", "AbortError");
    },
  });
}
