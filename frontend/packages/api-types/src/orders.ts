export type PaymentMethod = 0 | 1;
export type OrderStatus = 0 | 1 | 2 | 3 | 4 | 5 | 6;

export interface CheckoutRequest {
  sessionId: string;
  customerName: string;
  customerPhone: string;
  customerEmail?: string | null;
  shippingAddress: string;
  note?: string | null;
  paymentMethod: PaymentMethod;
}

export interface CheckoutResponse {
  order: OrderDto;
}

export interface OrderItemDto {
  id: string;
  productVariantId: string;
  productNameSnapshot: string;
  skuSnapshot: string;
  unitPrice: number;
  quantity: number;
  lineTotal: number;
  requiresInstallation: boolean;
}

export interface OrderDto {
  id: string;
  orderCode: string;
  customerName: string;
  customerPhone: string;
  customerEmail: string | null;
  shippingAddress: string;
  note: string | null;
  subtotal: number;
  shippingFee: number;
  discountAmount: number;
  totalAmount: number;
  status: OrderStatus;
  paymentMethod: PaymentMethod;
  createdAt: string;
  items: OrderItemDto[];
}

export interface OrderLookupRequest {
  orderCode: string;
  phone: string;
}

export interface OrderLookupResponse {
  order: OrderDto;
}
