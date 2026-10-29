import type { PagedResult } from "./common";

export enum ShippingRatePlanStatus { Draft = 0, Published = 1, Retired = 2 }
export interface ShippingWard { code: string; name: string; }
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
export interface ShippingRateActionRequest { expectedRevision: number; reason: string; }
export interface PublishShippingRatePlanRequest extends ShippingRateActionRequest {
  expectedActivePlanId: string | null;
  expectedActiveRevision: number | null;
}
export interface CloneShippingRatePlanRequest extends ShippingRateActionRequest { name: string; }
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
export interface ShippingWeightLine { weightKg: number | null; quantity: number; }
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
  destination: { catalogVersion: string; provinceCode: string; provinceName: string; wardCode: string; wardName: string };
  calculation: { weightGrams: number; extraSteps: number; baseFee: number; extraWeightFee: number; totalFee: number; currency: string };
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
