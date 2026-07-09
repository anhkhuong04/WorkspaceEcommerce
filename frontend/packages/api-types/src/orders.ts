export type PaymentMethod = 0 | 1;
export type OrderStatus = 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7;

export interface CheckoutRequest {
  sessionId: string;
  customerName: string;
  customerPhone: string;
  customerEmail?: string | null;
  shippingAddress: string;
  shippingStreet: string;
  shippingWard: string;
  shippingProvince: string;
  note?: string | null;
  couponCode?: string | null;
  paymentMethod: PaymentMethod;
}

export interface GetShippingQuoteRequest {
  sessionId: string;
  street: string;
  ward: string;
  province: string;
}

export interface GetShippingQuoteResponse {
  totalFeeAmount: number;
  baseFeeAmount: number;
  extraWeightFeeAmount: number;
  insuranceFeeAmount: number;
  routeType: string;
  currency: string;
}

export interface CheckoutResponse {
  order: OrderDto;
}

export interface ValidateCheckoutCouponRequest {
  sessionId: string;
  couponCode: string;
}

export interface CheckoutCouponValidationResponse {
  couponCode: string;
  discountAmount: number;
  eligibleSubtotal: number;
  subtotal: number;
  totalAmount: number;
  message: string;
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
  customerId: string | null;
  customerName: string;
  customerPhone: string;
  customerEmail: string | null;
  shippingAddress: string;
  note: string | null;
  couponId: string | null;
  couponCodeSnapshot: string | null;
  couponNameSnapshot: string | null;
  subtotal: number;
  shippingFee: number;
  discountAmount: number;
  totalAmount: number;
  status: OrderStatus;
  paymentMethod: PaymentMethod;
  createdAt: string;
  updatedAt: string;
  trackingCode?: string | null;
  shipmentId?: string | null;
  items: OrderItemDto[];
}

export interface OrderLookupRequest {
  orderCode: string;
  phone: string;
}

export interface OrderLookupResponse {
  order: OrderDto;
}
