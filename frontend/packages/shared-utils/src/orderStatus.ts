import type { OrderStatus, PaymentMethod, PaymentStatus } from "@workspace-ecommerce/api-types";

const orderStatusLabels: Record<OrderStatus, string> = {
  0: "Pending",
  1: "Confirmed",
  2: "Processing",
  3: "Shipping",
  4: "Completed",
  5: "Failed delivery",
  6: "Cancelled",
  7: "Returned"
};

const paymentMethodLabels: Record<PaymentMethod, string> = {
  0: "COD",
  1: "Manual bank transfer",
  2: "VNPay"
};

const paymentStatusLabels: Record<PaymentStatus, string> = {
  0: "Unpaid",
  1: "Pending",
  2: "Paid",
  3: "Failed",
  4: "Cancelled"
};

export function formatOrderStatus(status: OrderStatus): string {
  return orderStatusLabels[status] ?? "Unknown";
}

export function formatPaymentMethod(paymentMethod: PaymentMethod): string {
  return paymentMethodLabels[paymentMethod] ?? "Unknown";
}

export function formatPaymentStatus(paymentStatus: PaymentStatus): string {
  return paymentStatusLabels[paymentStatus] ?? "Unknown";
}
