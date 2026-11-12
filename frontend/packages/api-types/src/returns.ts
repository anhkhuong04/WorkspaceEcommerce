import type { PagedResult } from "./common";
export type OrderReturnStatus = 0 | 1 | 2 | 3 | 4;
export interface ReturnUnitDto {
  id: string;
  maskedIdentifier: string;
  status: number;
}
export interface ReturnableOrderItemDto {
  orderItemId: string;
  productName: string;
  sku: string;
  quantity: number;
  availableQuantity: number;
  netLineAmount: number;
  units: ReturnUnitDto[];
}
export interface ReturnItemRequest {
  orderItemId: string;
  quantity: number;
}
export interface CreateOrderReturnRequest {
  requestId: string;
  reason: string;
  items: ReturnItemRequest[];
}
export interface InspectReturnItemRequest {
  orderItemId: string;
  receivedQuantity: number;
  restockQuantity: number;
  inspection: string;
  serializedUnitIds: string[];
}
export interface ReceiveOrderReturnRequest {
  expectedRevision: number;
  reason: string;
  refundShipping: boolean;
  items: InspectReturnItemRequest[];
}
export interface RejectOrderReturnRequest {
  expectedRevision: number;
  reason: string;
}
export interface RefundOrderReturnRequest {
  expectedRevision: number;
  amount: number;
  reference: string;
  reason: string;
}
export interface OrderReturnLineDto {
  orderItemId: string;
  productName: string;
  sku: string;
  quantity: number;
  receivedQuantity: number;
  restockQuantity: number;
  refundAmount: number;
  inspection: string | null;
  serializedUnitIds: string[];
}
export interface OrderReturnCaseDto {
  id: string;
  status: OrderReturnStatus;
  revision: number;
  reason: string;
  createdAt: string;
  receivedAt: string | null;
  merchandiseRefund: number;
  shippingRefund: number;
  refundAmount: number;
  refundReference: string | null;
  refundedAt: string | null;
  rejectionReason: string | null;
  items: OrderReturnLineDto[];
  audit: {
    action: string;
    actor: string;
    reason: string;
    occurredAt: string;
  }[];
}
export interface OrderReturnSummaryDto {
  orderId: string;
  currency: string;
  canCreate: boolean;
  eligibilityMessage: string | null;
  requestedAt: string | null;
  reviewedAt: string | null;
  decision: string | null;
  decisionReason: string | null;
  items: ReturnableOrderItemDto[];
  returns: PagedResult<OrderReturnCaseDto>;
}
