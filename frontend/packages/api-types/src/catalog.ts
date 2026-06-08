export interface StorefrontCategoryDto {
  id: string;
  parentId: string | null;
  name: string;
  slug: string;
  sortOrder: number;
  children: StorefrontCategoryDto[];
}

export interface StorefrontProductListItemDto {
  id: string;
  categoryId: string;
  categoryName: string | null;
  name: string;
  slug: string;
  description: string | null;
  isFeatured: boolean;
  minPrice: number | null;
  maxPrice: number | null;
  primaryImageUrl: string | null;
  inStock: boolean;
}

export interface StorefrontProductVariantDto {
  id: string;
  sku: string;
  name: string;
  color: string | null;
  size: string | null;
  price: number;
  compareAtPrice: number | null;
  stockQuantity: number;
  requiresInstallation: boolean;
}

export interface StorefrontProductImageDto {
  id: string;
  imageUrl: string;
  altText: string | null;
  sortOrder: number;
}

export interface StorefrontProductSpecificationDto {
  id: string;
  name: string;
  value: string;
  sortOrder: number;
}

export interface StorefrontProductDetailDto extends StorefrontProductListItemDto {
  variants: StorefrontProductVariantDto[];
  images: StorefrontProductImageDto[];
  specifications: StorefrontProductSpecificationDto[];
}

export interface ProductListRequest {
  categorySlug?: string;
  search?: string;
  minPrice?: number;
  maxPrice?: number;
  inStock?: boolean;
  pageNumber?: number;
  pageSize?: number;
}
