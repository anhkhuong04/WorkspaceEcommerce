import { useQuery } from "@tanstack/react-query";
import { adminApi } from "../../services/api/adminApi";

export function useAdminReviews(pageNumber: number, pageSize: number) {
  return useQuery({
    queryKey: ["admin-reviews", pageNumber, pageSize],
    queryFn: () => adminApi.getReviews(pageNumber, pageSize)
  });
}
