import type {
  AdminCategoryDto,
  AdminProductDto,
  AdminProductImageDto,
  AdminProductImageUpsertRequest,
  AdminProductSpecificationDto,
  AdminProductSpecificationUpsertRequest,
  AdminProductUpsertRequest,
  AdminProductVariantDto,
  AdminProductVariantUpsertRequest
} from "@workspace-ecommerce/api-types";
import { z } from "zod";
import { formatLocalizedText } from "../../utils/localizedText";

export const productSchema = z.object({
  categoryId: z.string().min(1, "Category is required."),
  name: z.string().trim().min(1, "Name is required.").max(250, "Name is too long."),
  slug: z.string().trim().min(1, "Slug is required.").max(250, "Slug is too long.").regex(/^[a-z0-9]+(?:-[a-z0-9]+)*$/, "Slug must use lowercase letters, numbers, and hyphens."),
  description: z.string().trim().optional(),
  isFeatured: z.boolean(),
  isActive: z.boolean()
});

export const variantSchema = z.object({
  sku: z.string().trim().min(1, "SKU is required.").max(100, "SKU is too long.").regex(/^[A-Za-z0-9][A-Za-z0-9._-]*$/, "SKU must use letters, numbers, dots, underscores, or hyphens."),
  name: z.string().trim().min(1, "Variant name is required.").max(250, "Variant name is too long."),
  color: z.string().trim().max(100, "Color is too long.").optional(),
  size: z.string().trim().max(100, "Size is too long.").optional(),
  price: z.number().min(0, "Price cannot be negative."),
  compareAtPrice: z.number().min(0, "Compare-at price cannot be negative.").nullable(),
  stockQuantity: z.number().int("Stock must be an integer.").min(0, "Stock cannot be negative."),
  requiresInstallation: z.boolean(),
  isActive: z.boolean()
}).refine((values) => values.compareAtPrice === null || values.compareAtPrice >= values.price, { path: ["compareAtPrice"], message: "Compare-at price cannot be lower than price." });

export const imageSchema = z.object({
  imageUrl: z.string().trim().min(1, "Image URL is required.").max(1000, "Image URL is too long."),
  altText: z.string().trim().max(250, "Alt text is too long.").optional(),
  sortOrder: z.number().int("Sort order must be an integer.")
});

export const specificationSchema = z.object({
  name: z.string().trim().min(1, "Name is required.").max(200, "Name is too long."),
  value: z.string().trim().min(1, "Value is required.").max(1000, "Value is too long."),
  sortOrder: z.number().int("Sort order must be an integer.")
});

export type ProductFormValues = z.infer<typeof productSchema>;
export type VariantFormValues = z.infer<typeof variantSchema>;
export type ImageFormValues = z.infer<typeof imageSchema>;
export type SpecificationFormValues = z.infer<typeof specificationSchema>;
export type CategoryOption = { id: string; label: string; level: number };
export type NoticeState = { type: "success" | "error"; message: string } | null;
export type DeleteTarget =
  | { type: "product"; item: AdminProductDto }
  | { type: "image"; item: AdminProductImageDto }
  | { type: "specification"; item: AdminProductSpecificationDto };

export const productDefaultValues: ProductFormValues = { categoryId: "", name: "", slug: "", description: "", isFeatured: false, isActive: true };
export const variantDefaultValues: VariantFormValues = { sku: "", name: "", color: "", size: "", price: 0, compareAtPrice: null, stockQuantity: 0, requiresInstallation: false, isActive: true };
export const imageDefaultValues: ImageFormValues = { imageUrl: "", altText: "", sortOrder: 1 };
export const specificationDefaultValues: SpecificationFormValues = { name: "", value: "", sortOrder: 1 };

export function flattenCategories(categories: AdminCategoryDto[], level = 0): CategoryOption[] {
  return categories.flatMap((category) => [{ id: category.id, label: formatLocalizedText(category.name), level }, ...flattenCategories(category.children, level + 1)]);
}

export function toProductFormValues(product: AdminProductDto): ProductFormValues {
  return { categoryId: product.categoryId, name: formatLocalizedText(product.name, ""), slug: product.slug, description: formatLocalizedText(product.description, ""), isFeatured: product.isFeatured, isActive: product.isActive };
}

export function toProductRequest(values: ProductFormValues): AdminProductUpsertRequest {
  return { categoryId: values.categoryId, name: values.name.trim(), slug: values.slug.trim(), description: values.description?.trim() ? values.description.trim() : null, isFeatured: values.isFeatured, isActive: values.isActive };
}

export function toVariantFormValues(variant: AdminProductVariantDto): VariantFormValues {
  return { sku: variant.sku, name: variant.name, color: variant.color ?? "", size: variant.size ?? "", price: variant.price, compareAtPrice: variant.compareAtPrice, stockQuantity: variant.stockQuantity, requiresInstallation: variant.requiresInstallation, isActive: variant.isActive };
}

export function toVariantRequest(values: VariantFormValues): AdminProductVariantUpsertRequest {
  return { sku: values.sku.trim(), name: values.name.trim(), color: values.color?.trim() ? values.color.trim() : null, size: values.size?.trim() ? values.size.trim() : null, price: values.price, compareAtPrice: values.compareAtPrice, stockQuantity: values.stockQuantity, requiresInstallation: values.requiresInstallation, isActive: values.isActive };
}

export function toImageFormValues(image: AdminProductImageDto): ImageFormValues {
  return { imageUrl: image.imageUrl, altText: image.altText ?? "", sortOrder: image.sortOrder };
}

export function toImageRequest(values: ImageFormValues): AdminProductImageUpsertRequest {
  return { imageUrl: values.imageUrl.trim(), altText: values.altText?.trim() ? values.altText.trim() : null, sortOrder: values.sortOrder };
}

export function toSpecificationFormValues(specification: AdminProductSpecificationDto): SpecificationFormValues {
  return { name: specification.name, value: specification.value, sortOrder: specification.sortOrder };
}

export function toSpecificationRequest(values: SpecificationFormValues): AdminProductSpecificationUpsertRequest {
  return { name: values.name.trim(), value: values.value.trim(), sortOrder: values.sortOrder };
}
