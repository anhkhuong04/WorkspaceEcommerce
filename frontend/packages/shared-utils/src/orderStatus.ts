import type { OrderStatus, PaymentMethod } from "@workspace-ecommerce/api-types";

const orderStatusLabels: Record<OrderStatus, string> = {
  0: "Pending",
  1: "Confirmed",
  2: "Processing",
  3: "Shipping",
  4: "Completed",
  5: "Failed delivery",
  6: "Cancelled"
};

const paymentMethodLabels: Record<PaymentMethod, string> = {
  0: "COD",
  1: "Manual bank transfer"
};

export function formatOrderStatus(status: OrderStatus): string {
  return orderStatusLabels[status] ?? "Unknown";
}

export function formatPaymentMethod(paymentMethod: PaymentMethod): string {
  return paymentMethodLabels[paymentMethod] ?? "Unknown";
}
