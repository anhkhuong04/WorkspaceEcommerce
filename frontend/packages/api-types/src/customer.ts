import type { OrderItemDto, OrderStatus, PaymentMethod } from "./orders";

export interface CustomerRegisterRequest {
  fullName: string;
  phoneNumber: string;
  email: string;
  password: string;
}

export interface CustomerLoginRequest {
  email: string;
  password: string;
}

export interface CustomerAuthResponse {
  accessToken: string;
  tokenType: string;
  expiresAt: string;
  customerId: string;
  email: string;
  fullName: string;
  phoneNumber: string;
}

export interface CustomerProfileDto {
  id: string;
  fullName: string;
  phoneNumber: string;
  email: string;
  createdAt: string;
  updatedAt: string;
}

export interface UpdateCustomerProfileRequest {
  fullName: string;
  phoneNumber: string;
}

export interface CustomerOrderListRequest {
  pageNumber?: number;
  pageSize?: number;
  status?: OrderStatus;
}

export interface CustomerOrderListItemDto {
  id: string;
  orderCode: string;
  totalAmount: number;
  status: OrderStatus;
  paymentMethod: PaymentMethod;
  createdAt: string;
  updatedAt: string;
  itemCount: number;
}

export interface CustomerOrderStatusHistoryDto {
  id: string;
  fromStatus: OrderStatus | null;
  toStatus: OrderStatus;
  note: string | null;
  changedAt: string;
}

export interface CustomerOrderDto {
  id: string;
  orderCode: string;
  customerId: string;
  customerName: string;
  customerPhone: string;
  customerEmail: string | null;
  shippingAddress: string;
  note: string | null;
  couponId?: string | null;
  couponCodeSnapshot?: string | null;
  couponNameSnapshot?: string | null;
  subtotal: number;
  shippingFee: number;
  discountAmount: number;
  totalAmount: number;
  status: OrderStatus;
  paymentMethod: PaymentMethod;
  createdAt: string;
  updatedAt: string;
  items: OrderItemDto[];
  statusHistory: CustomerOrderStatusHistoryDto[];
}
