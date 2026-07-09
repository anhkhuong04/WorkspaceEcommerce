import type {
  AddCartItemRequest,
  AdminBannerDto,
  AdminBannerUpsertRequest,
  AdminCategoryDto,
  AdminCategoryUpsertRequest,
  AdminCouponDto,
  AdminCouponListRequest,
  AdminCouponUpsertRequest,
  AdminDashboardDto,
  AdminLoginRequest,
  AdminLoginResponse,
  AdminOrderDto,
  AdminOrderListItemDto,
  AdminOrderListRequest,
  AdminProductDto,
  AdminProductImageDto,
  AdminProductImageUpsertRequest,
  AdminProductSpecificationDto,
  AdminProductSpecificationUpsertRequest,
  AdminProductUpsertRequest,
  AdminProductVariantDto,
  AdminProductVariantUpsertRequest,
  CartDto,
  CheckoutCouponValidationResponse,
  CheckoutRequest,
  CheckoutResponse,
  CustomerAuthResponse,
  CustomerLoginRequest,
  CustomerOrderDto,
  CustomerOrderListItemDto,
  CustomerOrderListRequest,
  CustomerProfileDto,
  CustomerRegisterRequest,
  CustomerGoogleLoginRequest,
  OrderLookupRequest,
  OrderLookupResponse,
  PagedResult,
  ProductListRequest,
  StorefrontBannerDto,
  StorefrontCategoryDto,
  StorefrontProductDetailDto,
  StorefrontProductListItemDto,
  UpdateCouponStatusRequest,
  UpdateCustomerProfileRequest,
  UpdateCartItemRequest,
  UpdateOrderStatusRequest,
  ValidateCheckoutCouponRequest,
  GetShippingQuoteRequest,
  GetShippingQuoteResponse,
  AdminBlogPostDto,
  StorefrontBlogPostDto,
  BlogCommentDto,
  CreateBlogPostRequest,
  UpdateBlogPostRequest,
  CreateCommentRequest,
  ProductReviewSummaryDto,
  AdminReviewListItemDto,
  CreateReviewRequest,
  ReviewDto
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
    validateCheckoutCoupon: (request: ValidateCheckoutCouponRequest) =>
      client.post<CheckoutCouponValidationResponse, ValidateCheckoutCouponRequest>("/api/checkout/coupons/validate", request),
    getShippingQuote: (request: GetShippingQuoteRequest) =>
      client.post<GetShippingQuoteResponse, GetShippingQuoteRequest>("/api/checkout/shipping-quote", request),
    lookupOrder: (request: OrderLookupRequest) => client.get<OrderLookupResponse>(`/api/orders/lookup${buildQuery(request)}`),
    registerCustomer: (request: CustomerRegisterRequest) =>
      client.post<CustomerAuthResponse, CustomerRegisterRequest>("/api/customer/auth/register", request),
    loginCustomer: (request: CustomerLoginRequest) =>
      client.post<CustomerAuthResponse, CustomerLoginRequest>("/api/customer/auth/login", request),
    loginWithGoogle: (request: CustomerGoogleLoginRequest) =>
      client.post<CustomerAuthResponse, CustomerGoogleLoginRequest>("/api/customer/auth/google", request),
    getCustomerMe: () => client.get<CustomerProfileDto>("/api/customer/me"),
    updateCustomerMe: (request: UpdateCustomerProfileRequest) =>
      client.put<CustomerProfileDto, UpdateCustomerProfileRequest>("/api/customer/me", request),
    getCustomerOrders: (request: CustomerOrderListRequest = {}) =>
      client.get<PagedResult<CustomerOrderListItemDto>>(`/api/customer/orders${buildQuery(request)}`),
    getCustomerOrder: (id: string) => client.get<CustomerOrderDto>(`/api/customer/orders/${id}`),
    getBlogPosts: () => client.get<StorefrontBlogPostDto[]>("/api/blog-posts"),
    getBlogPost: (slug: string) => client.get<StorefrontBlogPostDto>(`/api/blog-posts/${slug}`),
    submitBlogComment: (slug: string, request: CreateCommentRequest) =>
      client.post<BlogCommentDto, CreateCommentRequest>(`/api/blog-posts/${slug}/comments`, request),
    getProductReviews: (slug: string) => client.get<ProductReviewSummaryDto>(`/api/products/${slug}/reviews`),
    submitReview: (slug: string, request: CreateReviewRequest) =>
      client.post<ReviewDto, CreateReviewRequest>(`/api/products/${slug}/reviews`, request)
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
    deleteCategory: (id: string) => client.delete<AdminCategoryDto>(`/api/admin/categories/${id}`),
    getProducts: () => client.get<AdminProductDto[]>("/api/admin/products"),
    createProduct: (request: AdminProductUpsertRequest) =>
      client.post<AdminProductDto, AdminProductUpsertRequest>("/api/admin/products", request),
    updateProduct: (id: string, request: AdminProductUpsertRequest) =>
      client.put<AdminProductDto, AdminProductUpsertRequest>(`/api/admin/products/${id}`, request),
    deleteProduct: (id: string) => client.delete<AdminProductDto>(`/api/admin/products/${id}`),
    createProductVariant: (productId: string, request: AdminProductVariantUpsertRequest) =>
      client.post<AdminProductVariantDto, AdminProductVariantUpsertRequest>(`/api/admin/products/${productId}/variants`, request),
    updateProductVariant: (id: string, request: AdminProductVariantUpsertRequest) =>
      client.put<AdminProductVariantDto, AdminProductVariantUpsertRequest>(`/api/admin/variants/${id}`, request),
    createProductImage: (productId: string, request: AdminProductImageUpsertRequest) =>
      client.post<AdminProductImageDto, AdminProductImageUpsertRequest>(`/api/admin/products/${productId}/images`, request),
    updateProductImage: (id: string, request: AdminProductImageUpsertRequest) =>
      client.put<AdminProductImageDto, AdminProductImageUpsertRequest>(`/api/admin/product-images/${id}`, request),
    deleteProductImage: (id: string) => client.delete<AdminProductImageDto>(`/api/admin/product-images/${id}`),
    createProductSpecification: (productId: string, request: AdminProductSpecificationUpsertRequest) =>
      client.post<AdminProductSpecificationDto, AdminProductSpecificationUpsertRequest>(`/api/admin/products/${productId}/specifications`, request),
    updateProductSpecification: (id: string, request: AdminProductSpecificationUpsertRequest) =>
      client.put<AdminProductSpecificationDto, AdminProductSpecificationUpsertRequest>(`/api/admin/product-specifications/${id}`, request),
    deleteProductSpecification: (id: string) => client.delete<AdminProductSpecificationDto>(`/api/admin/product-specifications/${id}`),
    getOrders: (request: AdminOrderListRequest = {}) =>
      client.get<PagedResult<AdminOrderListItemDto>>(`/api/admin/orders${buildQuery(request)}`),
    getOrder: (id: string) => client.get<AdminOrderDto>(`/api/admin/orders/${id}`),
    updateOrderStatus: (id: string, request: UpdateOrderStatusRequest) =>
      client.put<AdminOrderDto, UpdateOrderStatusRequest>(`/api/admin/orders/${id}/status`, request),
    getCoupons: (request: AdminCouponListRequest = {}) =>
      client.get<PagedResult<AdminCouponDto>>(`/api/admin/coupons${buildQuery(request)}`),
    getCoupon: (id: string) => client.get<AdminCouponDto>(`/api/admin/coupons/${id}`),
    createCoupon: (request: AdminCouponUpsertRequest) =>
      client.post<AdminCouponDto, AdminCouponUpsertRequest>("/api/admin/coupons", request),
    updateCoupon: (id: string, request: AdminCouponUpsertRequest) =>
      client.put<AdminCouponDto, AdminCouponUpsertRequest>(`/api/admin/coupons/${id}`, request),
    updateCouponStatus: (id: string, request: UpdateCouponStatusRequest) =>
      client.patch<AdminCouponDto, UpdateCouponStatusRequest>(`/api/admin/coupons/${id}/status`, request),
    deleteCoupon: (id: string) => client.delete<AdminCouponDto>(`/api/admin/coupons/${id}`),
    getBanners: () => client.get<AdminBannerDto[]>("/api/admin/banners"),
    createBanner: (request: AdminBannerUpsertRequest) =>
      client.post<AdminBannerDto, AdminBannerUpsertRequest>("/api/admin/banners", request),
    updateBanner: (id: string, request: AdminBannerUpsertRequest) =>
      client.put<AdminBannerDto, AdminBannerUpsertRequest>(`/api/admin/banners/${id}`, request),
    deleteBanner: (id: string) => client.delete<AdminBannerDto>(`/api/admin/banners/${id}`),
    getDashboard: () => client.get<AdminDashboardDto>("/api/admin/dashboard"),
    getBlogPosts: () => client.get<AdminBlogPostDto[]>("/api/admin/blog-posts"),
    getBlogPost: (id: string) => client.get<AdminBlogPostDto>(`/api/admin/blog-posts/${id}`),
    createBlogPost: (request: CreateBlogPostRequest) =>
      client.post<AdminBlogPostDto, CreateBlogPostRequest>("/api/admin/blog-posts", request),
    updateBlogPost: (id: string, request: UpdateBlogPostRequest) =>
      client.put<AdminBlogPostDto, UpdateBlogPostRequest>(`/api/admin/blog-posts/${id}`, request),
    deleteBlogPost: (id: string) => client.delete<AdminBlogPostDto>(`/api/admin/blog-posts/${id}`),
    toggleBlogPostPublish: (id: string) => client.post<AdminBlogPostDto, void>(`/api/admin/blog-posts/${id}/toggle-publish`, undefined),
    getBlogPostComments: (id: string) => client.get<BlogCommentDto[]>(`/api/admin/blog-posts/${id}/comments`),
    deleteBlogComment: (id: string) => client.delete<BlogCommentDto>(`/api/admin/blog-comments/${id}`),
    getReviews: (page = 1, pageSize = 20) => client.get<PagedResult<AdminReviewListItemDto>>(`/api/admin/reviews${buildQuery({ page, pageSize })}`),
    deleteReview: (id: string) => client.delete<void>(`/api/admin/reviews/${id}`)
  };
}
