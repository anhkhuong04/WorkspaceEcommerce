import { ApiClient } from "@workspace-ecommerce/api-client";
import type {
  ConfirmOrderPaymentRequest,
  OrderPaymentSummaryDto,
} from "@workspace-ecommerce/api-types";
import { getAdminToken } from "./adminApi";
const client = new ApiClient({
  baseUrl: import.meta.env.VITE_API_BASE_URL ?? "",
  getAccessToken: getAdminToken,
});
export type PaymentConfirmationAction =
  | "confirm-transfer"
  | "collect-cod"
  | "remit-cod";
export const orderPaymentApi = {
  summary: (id: string) =>
    client.get<OrderPaymentSummaryDto>(`/api/admin/orders/${id}/payment`),
  confirm: (
    id: string,
    action: PaymentConfirmationAction,
    body: ConfirmOrderPaymentRequest,
  ) =>
    client.post<OrderPaymentSummaryDto, ConfirmOrderPaymentRequest>(
      `/api/admin/orders/${id}/payment/${action}`,
      body,
    ),
};
