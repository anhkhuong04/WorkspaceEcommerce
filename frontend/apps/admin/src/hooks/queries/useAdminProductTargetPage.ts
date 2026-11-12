import { useQuery, useQueryClient } from "@tanstack/react-query";
import { adminApi } from "../../services/api/adminApi";

// The current API exposes paged products, without an admin lookup by ID.
// Reuse those pages only when following an inventory link from the dashboard.
export function useAdminProductTargetPage(
  productId: string | null,
  pageSize: number,
) {
  const queryClient = useQueryClient();
  return useQuery({
    queryKey: ["admin-products", "target-page", productId],
    enabled: Boolean(productId),
    retry: false,
    queryFn: async ({ signal }) => {
      let pageNumber = 1;
      while (!signal.aborted) {
        const request = { pageNumber, pageSize };
        const result = await queryClient.fetchQuery({
          queryKey: ["admin-products", request],
          queryFn: () => adminApi.getProducts(request),
        });
        if (signal.aborted)
          throw new DOMException("Lookup cancelled", "AbortError");
        if (result.items.some((product) => product.id === productId))
          return pageNumber;
        if (!result.hasNextPage) break;
        pageNumber += 1;
      }
      throw new Error(
        "This product is no longer available. Return to the catalog.",
      );
    },
  });
}
