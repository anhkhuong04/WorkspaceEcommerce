import type { PaymentMethod, PaymentStatus } from "./orders";

export type PaymentProvider = 0;
export type PaymentTransactionStatus = 0 | 1 | 2 | 3;

export interface PaymentTransactionDto {
  id: string;
  provider: PaymentProvider;
  status: PaymentTransactionStatus;
  amount: number;
  currencyCode: string;
  txnRef: string;
  gatewayTransactionNo: string | null;
  gatewayResponseCode: string | null;
  gatewayResponseMessage: string | null;
  createdAt: string;
  processedAt: string | null;
}

export interface PaymentResultDto {
  orderId: string;
  orderCode: string;
  paymentMethod: PaymentMethod;
  paymentStatus: PaymentStatus;
  paidAt: string | null;
  shipmentCreated: boolean;
  shipmentId: string | null;
  trackingCode: string | null;
  transaction: PaymentTransactionDto | null;
  gatewayResponseCode: string | null;
  message: string | null;
}
