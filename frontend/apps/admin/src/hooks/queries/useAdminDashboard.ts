import { useQuery } from "@tanstack/react-query";
import { adminApi } from "../../services/api/adminApi";

export function useAdminDashboard() {
  return useQuery({
    queryKey: ["admin-dashboard"],
    queryFn: adminApi.getDashboard
  });
}
