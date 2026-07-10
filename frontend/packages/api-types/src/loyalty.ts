import type { PagedResult } from "./common";

export type LoyaltyTierType = 0 | 1 | 2 | 3;
export type LoyaltyTransactionType = 0 | 1 | 2;

export interface LoyaltyAccountDto {
  accountId: string | null;
  customerId: string;
  currentPoints: number;
  totalPointsEarned: number;
  currentTier: LoyaltyTierType;
  discountPercent: number;
  freeShippingEnabled: boolean;
  nextTier: LoyaltyTierType | null;
  pointsToNextTier: number | null;
}

export interface LoyaltyTransactionDto {
  id: string;
  type: LoyaltyTransactionType;
  points: number;
  balanceAfter: number;
  orderId: string | null;
  voucherId: string | null;
  description: string;
  createdAt: string;
}

export interface LoyaltyTierDto {
  id: string;
  type: LoyaltyTierType;
  minTotalPointsEarned: number;
  discountPercent: number;
  freeShippingEnabled: boolean;
}

export interface LoyaltyTransactionListRequest {
  page?: number;
  pageNumber?: number;
  pageSize?: number;
}

export interface RedeemLoyaltyPointsRequest {
  points: number;
}

export interface RedeemLoyaltyPointsResponse {
  voucherId: string;
  voucherCode: string;
  discountAmount: number;
  remainingPoints: number;
  expiresAt: string;
}

export type LoyaltyTransactionPage = PagedResult<LoyaltyTransactionDto>;
