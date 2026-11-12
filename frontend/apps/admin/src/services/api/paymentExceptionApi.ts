import { ApiClient } from "@workspace-ecommerce/api-client";
import type { PagedResult } from "@workspace-ecommerce/api-types";
import { getAdminToken } from "./adminApi";

export interface PaymentExceptionDto {
  id: string;
  expectedAmount: number;
  actualAmount: number;
  reference: string;
  reason: string;
  actor: string;
  recordedAt: string;
  settlement: string | null;
  settlementAmount: number | null;
  settlementReference: string | null;
  settlementReason: string | null;
  settledBy: string | null;
  settledAt: string | null;
  requiredSettlement: string;
  requiredAmount: number;
}
export interface PaymentExceptionsDto {
  canRecord: boolean;
  currency: string;
  exceptions: PagedResult<PaymentExceptionDto>;
}
export interface RecordPaymentExceptionRequest {
  requestId: string;
  actualAmount: number;
  reference: string;
  reason: string;
}
export interface ResolvePaymentExceptionRequest {
  amount: number;
  reference: string;
  reason: string;
}
const client = new ApiClient({
  baseUrl: import.meta.env.VITE_API_BASE_URL ?? "",
  getAccessToken: getAdminToken,
});
export const paymentExceptionApi = {
  list: (orderId: string, page: number) =>
    client.get<PaymentExceptionsDto>(
      `/api/admin/orders/${orderId}/payment/exceptions?pageNumber=${page}`,
    ),
  record: (orderId: string, body: RecordPaymentExceptionRequest) =>
    client.post<PaymentExceptionsDto, RecordPaymentExceptionRequest>(
      `/api/admin/orders/${orderId}/payment/exceptions`,
      body,
    ),
  resolve: (
    orderId: string,
    id: string,
    body: ResolvePaymentExceptionRequest,
  ) =>
    client.post<PaymentExceptionsDto, ResolvePaymentExceptionRequest>(
      `/api/admin/orders/${orderId}/payment/exceptions/${id}/resolve`,
      body,
    ),
};
