import type { PaginationRequest } from "./common";
import type { OrderStatus, PaymentMethod, OrderItemDto } from "./orders";

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

export interface AdminDashboardDto {
  totalOrders: number;
  totalRevenue: number;
  newOrders: number;
  lowStockVariants: LowStockProductVariantDto[];
}
