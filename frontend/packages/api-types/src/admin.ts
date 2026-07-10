import type { PaginationRequest } from "./common";
import type { OrderStatus, PaymentMethod, PaymentStatus, OrderItemDto } from "./orders";
import type { CouponDiscountType, CouponSource } from "./coupons";

export interface AdminLoginRequest {
  email: string;
  password: string;
}

export interface AdminLoginResponse {
  accessToken: string;
  tokenType: string;
  expiresAt: string;
  email: string;
}

export interface AdminCategoryDto {
  id: string;
  parentId: string | null;
  name: string;
  slug: string;
  isActive: boolean;
  sortOrder: number;
  children: AdminCategoryDto[];
}

export interface AdminCategoryUpsertRequest {
  parentId?: string | null;
  name: string;
  slug: string;
  sortOrder: number;
  isActive: boolean;
}

export interface AdminProductVariantDto {
  id: string;
  productId: string;
  sku: string;
  name: string;
  color: string | null;
  size: string | null;
  price: number;
  compareAtPrice: number | null;
  stockQuantity: number;
  requiresInstallation: boolean;
  isActive: boolean;
}

export interface AdminProductVariantUpsertRequest {
  sku: string;
  name: string;
  color?: string | null;
  size?: string | null;
  price: number;
  compareAtPrice?: number | null;
  stockQuantity: number;
  requiresInstallation: boolean;
  isActive: boolean;
}

export interface AdminProductImageDto {
  id: string;
  productId: string;
  imageUrl: string;
  altText: string | null;
  sortOrder: number;
}

export interface AdminProductImageUpsertRequest {
  imageUrl: string;
  altText?: string | null;
  sortOrder: number;
}

export interface AdminProductSpecificationDto {
  id: string;
  productId: string;
  name: string;
  value: string;
  sortOrder: number;
}

export interface AdminProductSpecificationUpsertRequest {
  name: string;
  value: string;
  sortOrder: number;
}

export interface AdminProductDto {
  id: string;
  categoryId: string;
  categoryName: string | null;
  name: string;
  slug: string;
  description: string | null;
  isFeatured: boolean;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
  variants: AdminProductVariantDto[];
  images: AdminProductImageDto[];
  specifications: AdminProductSpecificationDto[];
}

export interface AdminProductUpsertRequest {
  categoryId: string;
  name: string;
  slug: string;
  description?: string | null;
  isFeatured: boolean;
  isActive: boolean;
}

export interface AdminOrderListRequest extends PaginationRequest {
  status?: OrderStatus;
  search?: string;
}

export interface AdminOrderListItemDto {
  id: string;
  orderCode: string;
  customerName: string;
  customerPhone: string;
  totalAmount: number;
  status: OrderStatus;
  paymentMethod: PaymentMethod;
  paymentStatus: PaymentStatus;
  paidAt: string | null;
  createdAt: string;
  updatedAt: string;
  itemCount: number;
}

export interface AdminOrderStatusHistoryDto {
  id: string;
  fromStatus: OrderStatus | null;
  toStatus: OrderStatus;
  note: string | null;
  changedBy: string | null;
  changedAt: string;
}

export interface AdminOrderDto extends AdminOrderListItemDto {
  customerId: string | null;
  customerEmail: string | null;
  shippingAddress: string;
  note: string | null;
  couponId: string | null;
  couponCodeSnapshot: string | null;
  couponNameSnapshot: string | null;
  subtotal: number;
  shippingFee: number;
  discountAmount: number;
  items: OrderItemDto[];
  statusHistory: AdminOrderStatusHistoryDto[];
}

export interface UpdateOrderStatusRequest {
  status: OrderStatus;
  note?: string | null;
}

export interface AdminCouponListRequest extends PaginationRequest {
  search?: string;
  isActive?: boolean;
  effectiveAt?: string;
}

export interface AdminCouponDto {
  id: string;
  code: string;
  name: string;
  description: string | null;
  discountType: CouponDiscountType;
  discountValue: number;
  maxDiscountAmount: number | null;
  minimumSubtotal: number | null;
  startsAt: string | null;
  endsAt: string | null;
  usageLimit: number | null;
  customerId: string | null;
  source: CouponSource;
  usedCount: number;
  redemptionCount: number;
  isActive: boolean;
  productTargetIds: string[];
  createdAt: string;
  updatedAt: string;
}

export interface AdminCouponUpsertRequest {
  code: string;
  name: string;
  description?: string | null;
  discountType: CouponDiscountType;
  discountValue: number;
  maxDiscountAmount?: number | null;
  minimumSubtotal?: number | null;
  startsAt?: string | null;
  endsAt?: string | null;
  usageLimit?: number | null;
  isActive: boolean;
  productTargetIds: string[];
}

export interface UpdateCouponStatusRequest {
  isActive: boolean;
}

export interface AdminBannerDto {
  id: string;
  title: string;
  imageUrl: string;
  linkUrl: string | null;
  sortOrder: number;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface AdminBannerUpsertRequest {
  title: string;
  imageUrl: string;
  linkUrl?: string | null;
  sortOrder: number;
  isActive: boolean;
}

export interface LowStockProductVariantDto {
  productId: string;
  productName: string;
  variantId: string;
  sku: string;
  variantName: string;
  stockQuantity: number;
  isActive: boolean;
}

export interface AdminOrderStatusSummaryDto {
  status: OrderStatus;
  count: number;
}

export interface RecentAdminOrderDto {
  id: string;
  orderCode: string;
  customerName: string;
  totalAmount: number;
  status: OrderStatus;
  createdAt: string;
}

export interface AdminDashboardDto {
  totalOrders: number;
  totalRevenue: number;
  newOrders: number;
  lowStockThreshold: number;
  lowStockVariants: LowStockProductVariantDto[];
  orderStatusSummary: AdminOrderStatusSummaryDto[];
  recentOrders: RecentAdminOrderDto[];
}

export interface ReviewDto {
  id: string;
  productId: string;
  customerId: string;
  customerName: string;
  rating: number;
  comment: string | null;
  createdAt: string;
}

export interface ProductReviewSummaryDto {
  averageRating: number;
  reviewCount: number;
  reviews: ReviewDto[];
}

export interface AdminReviewListItemDto {
  id: string;
  productId: string;
  productName: string;
  customerId: string;
  customerName: string;
  rating: number;
  comment: string | null;
  createdAt: string;
}

export interface CreateReviewRequest {
  rating: number;
  comment?: string | null;
}
