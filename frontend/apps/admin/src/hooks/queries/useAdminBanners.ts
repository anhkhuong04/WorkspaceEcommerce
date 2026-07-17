import { useQuery } from "@tanstack/react-query";
import { adminApi } from "../../services/api/adminApi";

export function useAdminBanners() {
  return useQuery({
    queryKey: ["admin-banners"],
    queryFn: adminApi.getBanners
  });
}
