import { useQuery } from "@tanstack/react-query";
import type { AdminCouponListRequest } from "@workspace-ecommerce/api-types";
import { adminApi } from "../../services/api/adminApi";

export function useAdminCoupons(request: AdminCouponListRequest) {
  return useQuery({
    queryKey: ["admin-coupons", request],
    queryFn: () => adminApi.getCoupons(request)
  });
}
