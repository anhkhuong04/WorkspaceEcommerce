import type { OrderItemDto, OrderStatus, PaymentMethod, PaymentStatus } from "./orders";

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

export interface CustomerGoogleLoginRequest {
  idToken: string;
}

export interface RequestEmailVerificationRequest {
  email: string;
}

export interface ConfirmEmailVerificationRequest {
  token: string;
}

export interface ForgotPasswordRequest {
  email: string;
}

export interface ResetPasswordRequest {
  token: string;
  newPassword: string;
}

export interface CustomerAuthResponse {
  accessToken: string | null;
  tokenType: string | null;
  expiresAt: string | null;
  customerId: string;
  email: string;
  fullName: string;
  phoneNumber: string;
  requiresTwoFactor: boolean;
  twoFactorChallengeToken: string | null;
}

export interface CustomerProfileDto {
  id: string;
  fullName: string;
  phoneNumber: string | null;
  email: string;
  avatarUrl: string | null;
  isEmailVerified: boolean;
  rewardPoints: number;
  twoFactorEnabled: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface TwoFactorSetupStartResponse {
  manualEntryKey: string;
  provisioningUri: string;
  expiresAt: string;
}

export interface ConfirmTwoFactorSetupRequest {
  code: string;
}

export interface TwoFactorSetupConfirmationResponse {
  recoveryCodes: string[];
}

export interface DisableTwoFactorRequest {
  code?: string | null;
  recoveryCode?: string | null;
}

export interface VerifyTwoFactorLoginRequest {
  challengeToken: string;
  code: string;
}

export interface VerifyTwoFactorRecoveryRequest {
  challengeToken: string;
  recoveryCode: string;
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
  paymentStatus: PaymentStatus;
  paidAt: string | null;
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
  statusHistory: CustomerOrderStatusHistoryDto[];
}
