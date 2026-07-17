import { useQuery } from "@tanstack/react-query";
import { adminApi } from "../../services/api/adminApi";

export function useAdminCategories() {
  return useQuery({
    queryKey: ["admin-categories"],
    queryFn: adminApi.getCategories
  });
}
