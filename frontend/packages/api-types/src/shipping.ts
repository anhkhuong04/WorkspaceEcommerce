import type { PagedResult } from "./common";

export enum ShippingRatePlanStatus {
  Draft = 0,
  Published = 1,
  Retired = 2,
}
export enum ShippingMethod {
  WorkspaceEC = 0,
  MiniLogistics = 1,
}
export enum WorkspaceShipmentAction { Assign = 0, Dispatch = 1, Deliver = 2, FailDelivery = 3, RetryDelivery = 4, Cancel = 5 }
export interface WorkspaceShipmentRequest { requestId: string; expectedRevision: number; action: WorkspaceShipmentAction; reason: string; assignedTo?: string | null; }
export interface WorkspaceShipmentDto {
  id: string; orderId: string; trackingCode: string; status: string; revision: number; assignedTo: string | null;
  orderStatus: number; paymentStatus: number; canAssign: boolean; canDispatch: boolean; canDeliver: boolean;
  canFailDelivery: boolean; canRetryDelivery: boolean; canCancel: boolean;
}
export interface WorkspaceShipmentAuditDto {
  id: string; action: WorkspaceShipmentAction; revision: number; status: string; actor: string; reason: string;
  assignedTo: string | null; occurredAtUtc: string;
}
export interface ShippingQuoteInput {
  sessionId: string;
  shippingStreet: string;
  provinceCode: string;
  wardCode: string;
  paymentMethod: number;
  couponCode?: string | null;
  method: ShippingMethod;
}
export interface ShippingCheckoutCapabilities { enabled: boolean; workspaceEnabled: boolean; externalEnabled: boolean; }
export interface ShippingMethodOption {
  method: ShippingMethod;
  available: boolean;
  reason: string | null;
}
export interface ShippingQuoteDto {
  id: string;
  method: ShippingMethod;
  customerCharge: number;
  currency: string;
  weightGrams: number;
  expiresAtUtc: string;
  ratePlanId: string | null;
}
export type OrderPaymentReceiptKind = 0 | 1 | 2;
export interface ConfirmOrderPaymentRequest {
  amount: number;
  currency: string;
  reference: string;
  reason: string;
}
export interface OrderPaymentReceiptDto {
  id: string;
  kind: OrderPaymentReceiptKind;
  amount: number;
  currency: string;
  reference: string;
  reason: string;
  actor: string;
  occurredAtUtc: string;
}
export interface OrderPaymentSummaryDto {
  orderId: string;
  paymentMethod: number;
  paymentStatus: number;
  totalAmount: number;
  currency: string;
  shippingMethod: ShippingMethod | null;
  canConfirmTransfer: boolean;
  canCollectCod: boolean;
  canRemitCod: boolean;
  receipts: OrderPaymentReceiptDto[];
}
export interface ShippingWard {
  code: string;
  name: string;
}
export interface ShippingCatalogSummary {
  version: string;
  provinces: { code: string; name: string }[];
}
export interface ShippingRateZoneInput {
  code: string;
  name: string;
  baseWeightGrams: number;
  baseFee: number;
  stepWeightGrams: number;
  stepFee: number;
  maximumWeightGrams: number | null;
  provinceCodes: string[];
}
export interface SaveShippingRatePlanRequest {
  name: string;
  catalogVersion: string;
  originProvinceCode: string;
  zones: ShippingRateZoneInput[];
  expectedRevision: number;
  reason: string;
}
export interface ShippingRateActionRequest {
  expectedRevision: number;
  reason: string;
}
export interface PublishShippingRatePlanRequest
  extends ShippingRateActionRequest {
  expectedActivePlanId: string | null;
  expectedActiveRevision: number | null;
}
export interface CloneShippingRatePlanRequest
  extends ShippingRateActionRequest {
  name: string;
}
export interface ShippingRateCoverage {
  isComplete: boolean;
  missingProvinceCodes: string[];
  unknownProvinceCodes: string[];
  errors: string[];
}
export interface ShippingRatePlanSummary {
  id: string;
  name: string;
  catalogVersion: string;
  originProvinceCode: string;
  status: ShippingRatePlanStatus;
  revision: number;
  createdAtUtc: string;
  updatedAtUtc: string;
}
export interface ShippingRatePlan extends ShippingRatePlanSummary {
  publishedAtUtc: string | null;
  retiredAtUtc: string | null;
  zones: ShippingRateZoneInput[];
  coverage: ShippingRateCoverage;
}
export interface ShippingWeightLine {
  weightKg: number | null;
  quantity: number;
}
export interface ShippingRatePreviewRequest {
  provinceCode: string;
  wardCode: string;
  expectedRevision: number;
  items: ShippingWeightLine[];
}
export interface ShippingRatePreview {
  ratePlanId: string;
  revision: number;
  status: ShippingRatePlanStatus;
  isPreview: true;
  canPublish: boolean;
  zoneCode: string;
  destination: {
    catalogVersion: string;
    provinceCode: string;
    provinceName: string;
    wardCode: string;
    wardName: string;
  };
  calculation: {
    weightGrams: number;
    extraSteps: number;
    baseFee: number;
    extraWeightFee: number;
    totalFee: number;
    currency: string;
  };
}
export interface ShippingRateAudit {
  id: string;
  ratePlanId: string;
  revision: number;
  action: string;
  actor: string;
  reason: string;
  occurredAtUtc: string;
  changesJson: string;
}
export type ShippingRatePlanPage = PagedResult<ShippingRatePlanSummary>;
export interface ShippingWeightIssue {
  productVariantId: string;
  productId: string;
  sku: string;
  name: string;
  weightKg: number | null;
  isActive: boolean;
}
