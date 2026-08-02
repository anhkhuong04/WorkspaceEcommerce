export type PaymentMethod = 0 | 1 | 2;
export type PaymentStatus = 0 | 1 | 2 | 3 | 4;
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
  clientIpAddress?: string | null;
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
  paymentRequired: boolean;
  paymentUrl: string | null;
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
  paymentStatus: PaymentStatus;
  paidAt: string | null;
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

export interface ShipmentTimelineEntryDto {
  id: string;
  providerStatus: string;
  note: string | null;
  changedAtUtc: string;
  source: string;
}

export interface ShipmentTrackingDto {
  orderId: string;
  orderCode: string;
  orderStatus: OrderStatus;
  shipmentId: string | null;
  trackingCode: string | null;
  provider: string | null;
  providerStatus: string | null;
  shippingFeeAmount: number | null;
  currency: string | null;
  lastSyncedAtUtc: string | null;
  lastEventAtUtc: string | null;
  canRetry: boolean;
  canRefresh: boolean;
  canCancel: boolean;
  lastCommandError: string | null;
  timeline: ShipmentTimelineEntryDto[];
}
