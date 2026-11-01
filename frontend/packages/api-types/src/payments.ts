import type { PaymentMethod, PaymentStatus } from "./orders";

export interface PaymentResultDto {
  shipping?: import("./shipping").OrderShippingDetailsDto | null;
  totalAmount?: number | null;
  orderCode: string;
  paymentMethod: PaymentMethod;
  paymentStatus: PaymentStatus;
  paidAt: string | null;
  shipmentCreated: boolean;
  trackingCode: string | null;
  message: string;
}
