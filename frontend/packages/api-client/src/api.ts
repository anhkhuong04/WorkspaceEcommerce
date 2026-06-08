import type {
  AddCartItemRequest,
  AdminBannerDto,
  AdminCategoryDto,
  AdminDashboardDto,
  AdminLoginRequest,
  AdminLoginResponse,
  AdminOrderDto,
  AdminOrderListItemDto,
  AdminProductDto,
  CartDto,
  CheckoutRequest,
  CheckoutResponse,
  OrderDto,
  OrderLookupRequest,
  PagedResult,
  ProductListRequest,
  StorefrontCategoryDto,
  StorefrontProductDetailDto,
  StorefrontProductListItemDto,
  UpdateCartItemRequest
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
    lookupOrder: (request: OrderLookupRequest) => client.get<OrderDto>(`/api/orders/lookup${buildQuery(request)}`)
  };
}

export function createAdminApi(client: ApiClient) {
  return {
    login: (request: AdminLoginRequest) => client.post<AdminLoginResponse, AdminLoginRequest>("/api/admin/auth/login", request),
    getCategories: () => client.get<AdminCategoryDto[]>("/api/admin/categories"),
    getProducts: () => client.get<AdminProductDto[]>("/api/admin/products"),
    getOrders: () => client.get<PagedResult<AdminOrderListItemDto>>("/api/admin/orders"),
    getOrder: (id: string) => client.get<AdminOrderDto>(`/api/admin/orders/${id}`),
    getBanners: () => client.get<AdminBannerDto[]>("/api/admin/banners"),
    getDashboard: () => client.get<AdminDashboardDto>("/api/admin/dashboard")
  };
}
