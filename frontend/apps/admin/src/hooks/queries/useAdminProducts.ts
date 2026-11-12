import { useQuery } from "@tanstack/react-query";
import type { AdminProductListRequest } from "@workspace-ecommerce/api-types";
import { adminApi } from "../../services/api/adminApi";

export function useAdminProducts(request?: AdminProductListRequest, keyScope?: string) {
  return useQuery({
    queryKey: keyScope ? ["admin-products", keyScope, request] : ["admin-products", request],
    queryFn: () => adminApi.getProducts(request)
  });
}
