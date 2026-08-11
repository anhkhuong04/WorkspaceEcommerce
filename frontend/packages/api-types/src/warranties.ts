import type { PaginationRequest } from "./common";

export type WarrantyIdentifierType = 0 | 1;
export type SerializedProductUnitStatus = 0 | 1 | 2 | 3 | 4 | 5;
export type WarrantyEntitlementStatus = 0 | 1 | 2 | 3;
export type WarrantyActivationSource = 0 | 1;
export type WarrantyAuditAction = 0 | 1 | 2 | 3 | 4 | 5;

export interface WarrantyCoverageDto {
  componentCode: string;
  displayName: string;
  durationMonths: number;
  startsAt: string | null;
  endsAt: string | null;
  sortOrder: number;
}

export interface WarrantyLookupRequest {
  identifierType?: WarrantyIdentifierType | null;
  identifier: string;
}

export interface PublicWarrantyLookupResponse {
  found: boolean;
  productName: string | null;
  maskedIdentifier: string | null;
  identifierType: WarrantyIdentifierType | null;
  status: WarrantyEntitlementStatus | null;
  activatedAt: string | null;
  coverages: WarrantyCoverageDto[];
}

export interface ActivateWarrantyRequest extends WarrantyLookupRequest {}

export interface CustomerWarrantyListRequest extends PaginationRequest {}

export interface CustomerWarrantyListItemDto {
  id: string;
  productName: string;
  maskedIdentifier: string;
  identifierType: WarrantyIdentifierType;
  status: WarrantyEntitlementStatus;
  activatedAt: string | null;
  latestCoverageEndsAt: string | null;
}

export interface CustomerWarrantyDto {
  id: string;
  productName: string;
  maskedIdentifier: string;
  identifierType: WarrantyIdentifierType;
  warrantyPlanName: string;
  status: WarrantyEntitlementStatus;
  purchasedAt: string | null;
  activationDeadline: string | null;
  activatedAt: string | null;
  coverages: WarrantyCoverageDto[];
}

export interface WarrantyPlanCoverageInput {
  componentCode: string;
  displayName: string;
  durationMonths: number;
  sortOrder: number;
}

export interface AdminWarrantyPlanDto {
  id: string;
  code: string;
  name: string;
  activationWindowDays: number;
  termsVersion: string;
  effectiveFrom: string;
  effectiveTo: string | null;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
  coverages: WarrantyCoverageDto[];
}

export interface AdminWarrantyPlanListRequest extends PaginationRequest {
  search?: string;
  isActive?: boolean;
}

export interface CreateWarrantyPlanRequest {
  code: string;
  name: string;
  activationWindowDays: number;
  termsVersion: string;
  effectiveFrom: string;
  effectiveTo?: string | null;
  coverages: WarrantyPlanCoverageInput[];
}

export interface AssignWarrantyPlanRequest {
  warrantyPlanId: string;
  effectiveFrom: string;
  effectiveTo?: string | null;
}

export interface AdminWarrantyUnitListRequest extends PaginationRequest {
  search?: string;
  status?: SerializedProductUnitStatus;
}

export interface AdminWarrantyUnitDto {
  id: string;
  productVariantId: string;
  sku: string;
  variantName: string;
  identifierType: WarrantyIdentifierType;
  maskedIdentifier: string;
  status: SerializedProductUnitStatus;
  orderItemId: string | null;
  orderCode: string | null;
  assignedAt: string | null;
  importBatchId: string;
  createdAt: string;
}

export interface AdminWarrantyImportRowResultDto {
  rowNumber: number;
  sku: string;
  identifierType: WarrantyIdentifierType | null;
  isValid: boolean;
  errors: string[];
}

export interface AdminWarrantyImportResultDto {
  isDryRun: boolean;
  isValid: boolean;
  importBatchId: string | null;
  totalRows: number;
  importedRows: number;
  failedRows: number;
  rows: AdminWarrantyImportRowResultDto[];
}

export interface AssignWarrantyUnitRequest {
  orderItemId: string;
}

export interface AdminWarrantyEntitlementListRequest extends PaginationRequest {
  search?: string;
  status?: WarrantyEntitlementStatus;
}

export interface WarrantyAuditEventDto {
  id: string;
  action: WarrantyAuditAction;
  actorType: string;
  actorId: string;
  reason: string | null;
  occurredAt: string;
}

export interface AdminWarrantyEntitlementDto {
  id: string;
  serializedProductUnitId: string;
  maskedIdentifier: string;
  identifierType: WarrantyIdentifierType;
  warrantyPlanId: string;
  warrantyPlanName: string;
  orderId: string;
  orderCode: string;
  customerId: string | null;
  productName: string;
  status: WarrantyEntitlementStatus;
  purchasedAt: string | null;
  eligibleAt: string | null;
  activationDeadline: string | null;
  activatedAt: string | null;
  activationSource: WarrantyActivationSource | null;
  replacementSerializedProductUnitId: string | null;
  coverages: WarrantyCoverageDto[];
  auditEvents: WarrantyAuditEventDto[];
}

export interface AdminWarrantyReasonRequest {
  reason: string;
}

export interface ReplaceWarrantyRequest extends AdminWarrantyReasonRequest {
  replacementSerializedProductUnitId: string;
}
