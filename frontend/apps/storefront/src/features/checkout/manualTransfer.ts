import type { OrderDto } from "@workspace-ecommerce/api-types";

export function buildManualTransferContent(orderCode: string): string {
  return `WSE ${orderCode}`;
}

export function getManualTransferContent(order: Pick<OrderDto, "orderCode">): string {
  return buildManualTransferContent(order.orderCode);
}
