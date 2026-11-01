import { ApiClient } from "@workspace-ecommerce/api-client";
import type {
  PagedResult,
  WorkspaceShipmentDto,
  WorkspaceShipmentRequest,
  WorkspaceShipmentAuditDto,
} from "@workspace-ecommerce/api-types";
import { getAdminToken } from "./adminApi";
const client = new ApiClient({
  baseUrl: import.meta.env.VITE_API_BASE_URL ?? "",
  getAccessToken: getAdminToken,
});
const path = (id: string) => `/api/admin/orders/${id}/workspace-shipment`;
export const workspaceShipmentApi = {
  get: (id: string) => client.get<WorkspaceShipmentDto>(path(id)),
  act: (id: string, body: WorkspaceShipmentRequest) =>
    client.post<WorkspaceShipmentDto, WorkspaceShipmentRequest>(
      `${path(id)}/actions`,
      body,
    ),
  history: (id: string, page: number) =>
    client.get<PagedResult<WorkspaceShipmentAuditDto>>(
      `${path(id)}/history?pageNumber=${page}`,
    ),
};
