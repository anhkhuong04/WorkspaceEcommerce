import { useQuery } from "@tanstack/react-query";
import { shippingApi } from "../../services/api/shippingApi";

export const shippingKeys = ["admin-shipping"] as const;
export function useShippingRates(page: number) {
  return useQuery({ queryKey: [...shippingKeys, "plans", page], queryFn: () => shippingApi.plans(page) });
}
export function useShippingCatalog() {
  return useQuery({ queryKey: [...shippingKeys, "catalog"], queryFn: shippingApi.catalog, staleTime: 300_000 });
}
