import type {
  AddCartItemRequest,
  AdminBannerDto,
  AdminBannerUpsertRequest,
  AdminCategoryDto,
  AdminCategoryUpsertRequest,
  AdminDashboardDto,
  AdminLoginRequest,
  AdminLoginResponse,
  AdminOrderDto,
  AdminOrderListItemDto,
  AdminOrderListRequest,
  AdminProductDto,
  AdminProductUpsertRequest,
  AdminProductVariantDto,
  AdminProductVariantUpsertRequest,
  CartDto,
  CheckoutRequest,
  CheckoutResponse,
  OrderLookupRequest,
  OrderLookupResponse,
  PagedResult,
  ProductListRequest,
  StorefrontBannerDto,
  StorefrontCategoryDto,
  StorefrontProductDetailDto,
  StorefrontProductListItemDto,
  UpdateCartItemRequest,
  UpdateOrderStatusRequest
} from "@workspace-ecommerce/api-types";
import { ApiClient } from "./httpClient";

function buildQuery(params: object): string {
  const query = new URLSearchParams();

  for (const [key, value] of Object.entries(params)) {
    if (
      (typeof value === "string" || typeof value === "number" || typeof value === "boolean") &&
      value !== ""
    ) {
      query.set(key, String(value));
    }
  }

  const value = query.toString();
  return value ? `?${value}` : "";
}

export function createStorefrontApi(client: ApiClient) {
  return {
    getBanners: () => client.get<StorefrontBannerDto[]>("/api/banners"),
    getCategories: () => client.get<StorefrontCategoryDto[]>("/api/categories"),
    getProducts: (request: ProductListRequest = {}) =>
      client.get<PagedResult<StorefrontProductListItemDto>>(`/api/products${buildQuery(request)}`),
    getProduct: (slug: string) => client.get<StorefrontProductDetailDto>(`/api/products/${slug}`),
    getCart: (sessionId: string) => client.get<CartDto>(`/api/cart${buildQuery({ sessionId })}`),
    addCartItem: (request: AddCartItemRequest) => client.post<CartDto, AddCartItemRequest>("/api/cart/items", request),
    updateCartItem: (itemId: string, request: UpdateCartItemRequest) =>
      client.put<CartDto, UpdateCartItemRequest>(`/api/cart/items/${itemId}`, request),
    removeCartItem: (itemId: string, sessionId: string) =>
      client.delete<CartDto>(`/api/cart/items/${itemId}${buildQuery({ sessionId })}`),
    checkout: (request: CheckoutRequest) => client.post<CheckoutResponse, CheckoutRequest>("/api/checkout", request),
    lookupOrder: (request: OrderLookupRequest) => client.get<OrderLookupResponse>(`/api/orders/lookup${buildQuery(request)}`)
  };
}

export function createAdminApi(client: ApiClient) {
  return {
    login: (request: AdminLoginRequest) => client.post<AdminLoginResponse, AdminLoginRequest>("/api/admin/auth/login", request),
    getCategories: () => client.get<AdminCategoryDto[]>("/api/admin/categories"),
    createCategory: (request: AdminCategoryUpsertRequest) =>
      client.post<AdminCategoryDto, AdminCategoryUpsertRequest>("/api/admin/categories", request),
    updateCategory: (id: string, request: AdminCategoryUpsertRequest) =>
      client.put<AdminCategoryDto, AdminCategoryUpsertRequest>(`/api/admin/categories/${id}`, request),
    getProducts: () => client.get<AdminProductDto[]>("/api/admin/products"),
    createProduct: (request: AdminProductUpsertRequest) =>
      client.post<AdminProductDto, AdminProductUpsertRequest>("/api/admin/products", request),
    updateProduct: (id: string, request: AdminProductUpsertRequest) =>
      client.put<AdminProductDto, AdminProductUpsertRequest>(`/api/admin/products/${id}`, request),
    createProductVariant: (productId: string, request: AdminProductVariantUpsertRequest) =>
      client.post<AdminProductVariantDto, AdminProductVariantUpsertRequest>(`/api/admin/products/${productId}/variants`, request),
    updateProductVariant: (id: string, request: AdminProductVariantUpsertRequest) =>
      client.put<AdminProductVariantDto, AdminProductVariantUpsertRequest>(`/api/admin/variants/${id}`, request),
    getOrders: (request: AdminOrderListRequest = {}) =>
      client.get<PagedResult<AdminOrderListItemDto>>(`/api/admin/orders${buildQuery(request)}`),
    getOrder: (id: string) => client.get<AdminOrderDto>(`/api/admin/orders/${id}`),
    updateOrderStatus: (id: string, request: UpdateOrderStatusRequest) =>
      client.put<AdminOrderDto, UpdateOrderStatusRequest>(`/api/admin/orders/${id}/status`, request),
    getBanners: () => client.get<AdminBannerDto[]>("/api/admin/banners"),
    createBanner: (request: AdminBannerUpsertRequest) =>
      client.post<AdminBannerDto, AdminBannerUpsertRequest>("/api/admin/banners", request),
    updateBanner: (id: string, request: AdminBannerUpsertRequest) =>
      client.put<AdminBannerDto, AdminBannerUpsertRequest>(`/api/admin/banners/${id}`, request),
    getDashboard: () => client.get<AdminDashboardDto>("/api/admin/dashboard")
  };
}
