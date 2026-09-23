import type { PaymentMethod, PaymentStatus } from "./orders";

export interface PaymentResultDto {
  orderCode: string;
  paymentMethod: PaymentMethod;
  paymentStatus: PaymentStatus;
  paidAt: string | null;
  shipmentCreated: boolean;
  trackingCode: string | null;
  message: string;
}
