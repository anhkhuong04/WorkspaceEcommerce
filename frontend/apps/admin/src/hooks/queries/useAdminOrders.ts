import { useQuery } from "@tanstack/react-query";
import type { AdminOrderListRequest } from "@workspace-ecommerce/api-types";
import { adminApi } from "../../services/api/adminApi";

export function useAdminOrders(request: AdminOrderListRequest) {
  return useQuery({
    queryKey: ["admin-orders", request],
    queryFn: () => adminApi.getOrders(request)
  });
}

export function useAdminOrder(orderId: string | null) {
  return useQuery({
    queryKey: ["admin-order", orderId],
    queryFn: () => adminApi.getOrder(orderId ?? ""),
    enabled: orderId !== null
  });
}
