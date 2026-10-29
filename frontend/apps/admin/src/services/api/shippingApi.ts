import { ApiClient } from "@workspace-ecommerce/api-client";
import type { CloneShippingRatePlanRequest, PagedResult, PublishShippingRatePlanRequest, SaveShippingRatePlanRequest, ShippingCatalogSummary, ShippingRateActionRequest, ShippingRateAudit, ShippingRatePlan, ShippingRatePlanPage, ShippingRatePreview, ShippingRatePreviewRequest, ShippingWard, ShippingWeightIssue } from "@workspace-ecommerce/api-types";
import { getAdminToken } from "./adminApi";

const client = new ApiClient({ baseUrl: import.meta.env.VITE_API_BASE_URL ?? "", getAccessToken: getAdminToken });
const root = "/api/admin/shipping";
export const shippingApi = {
  catalog: () => client.get<ShippingCatalogSummary>(`${root}/catalog`),
  wards: (province: string, page = 1) => client.get<PagedResult<ShippingWard>>(`${root}/catalog/provinces/${encodeURIComponent(province)}/wards?pageNumber=${page}&pageSize=100`),
  weights: (page: number) => client.get<PagedResult<ShippingWeightIssue>>(`${root}/catalog/weight-issues?pageNumber=${page}&pageSize=20`),
  plans: (page = 1, status?: number) => client.get<ShippingRatePlanPage>(`${root}/rate-plans?pageNumber=${page}&pageSize=20${status === undefined ? "" : `&status=${status}`}`),
  plan: (id: string) => client.get<ShippingRatePlan>(`${root}/rate-plans/${id}`),
  save: (id: string | null, body: SaveShippingRatePlanRequest) => id
    ? client.put<ShippingRatePlan, SaveShippingRatePlanRequest>(`${root}/rate-plans/${id}`, body)
    : client.post<ShippingRatePlan, SaveShippingRatePlanRequest>(`${root}/rate-plans`, body),
  clone: (id: string, body: CloneShippingRatePlanRequest) => client.post<ShippingRatePlan, CloneShippingRatePlanRequest>(`${root}/rate-plans/${id}/clone`, body),
  publish: (id: string, body: PublishShippingRatePlanRequest) => client.post<ShippingRatePlan, PublishShippingRatePlanRequest>(`${root}/rate-plans/${id}/publish`, body),
  retire: (id: string, body: ShippingRateActionRequest) => client.post<ShippingRatePlan, ShippingRateActionRequest>(`${root}/rate-plans/${id}/retire`, body),
  preview: (id: string, body: ShippingRatePreviewRequest) => client.post<ShippingRatePreview, ShippingRatePreviewRequest>(`${root}/rate-plans/${id}/preview`, body),
  history: (id: string, page: number) => client.get<PagedResult<ShippingRateAudit>>(`${root}/rate-plans/${id}/history?pageNumber=${page}&pageSize=20`),
};
