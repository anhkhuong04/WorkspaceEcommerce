import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
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
import { formatMoney } from "@workspace-ecommerce/shared-utils";
import { Fragment, useEffect, useMemo, useRef, useState } from "react";
import { Controller, useForm } from "react-hook-form";
import { useSearchParams } from "react-router-dom";
import { z } from "zod";
import { AdminPageHeader } from "../../components/ui/AdminPageHeader";
import { Button, ConfirmDialog, EmptyState, Field, Modal, Notice, Pill, SelectInput, TextArea, TextInput, Toggle } from "../../components/ui/AdminUi";
import { adminApi } from "../../services/api/adminApi";
import { getApiErrorMessage } from "../../services/api/errors";
import { cx } from "../../components/ui/cx";

const productSchema = z.object({
  categoryId: z.string().min(1, "Category is required."),
  name: z.string().trim().min(1, "Name is required.").max(250, "Name is too long."),
  slug: z.string().trim().min(1, "Slug is required.").max(250, "Slug is too long.").regex(/^[a-z0-9]+(?:-[a-z0-9]+)*$/, "Slug must use lowercase letters, numbers, and hyphens."),
  description: z.string().trim().optional(),
  isFeatured: z.boolean(),
  isActive: z.boolean()
});

const variantSchema = z.object({
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

const imageSchema = z.object({
  imageUrl: z.string().trim().min(1, "Image URL is required.").max(1000, "Image URL is too long."),
  altText: z.string().trim().max(250, "Alt text is too long.").optional(),
  sortOrder: z.number().int("Sort order must be an integer.")
});

const specificationSchema = z.object({
  name: z.string().trim().min(1, "Name is required.").max(200, "Name is too long."),
  value: z.string().trim().min(1, "Value is required.").max(1000, "Value is too long."),
  sortOrder: z.number().int("Sort order must be an integer.")
});

type ProductFormValues = z.infer<typeof productSchema>;
type VariantFormValues = z.infer<typeof variantSchema>;
type ImageFormValues = z.infer<typeof imageSchema>;
type SpecificationFormValues = z.infer<typeof specificationSchema>;
type CategoryOption = { id: string; label: string; level: number };
type DeleteTarget =
  | { type: "product"; item: AdminProductDto }
  | { type: "image"; item: AdminProductImageDto }
  | { type: "specification"; item: AdminProductSpecificationDto };

const productDefaultValues: ProductFormValues = { categoryId: "", name: "", slug: "", description: "", isFeatured: false, isActive: true };
const variantDefaultValues: VariantFormValues = { sku: "", name: "", color: "", size: "", price: 0, compareAtPrice: null, stockQuantity: 0, requiresInstallation: false, isActive: true };
const imageDefaultValues: ImageFormValues = { imageUrl: "", altText: "", sortOrder: 1 };
const specificationDefaultValues: SpecificationFormValues = { name: "", value: "", sortOrder: 1 };

function flattenCategories(categories: AdminCategoryDto[], level = 0): CategoryOption[] {
  return categories.flatMap((category) => [{ id: category.id, label: category.name, level }, ...flattenCategories(category.children, level + 1)]);
}

function toProductFormValues(product: AdminProductDto): ProductFormValues {
  return { categoryId: product.categoryId, name: product.name, slug: product.slug, description: product.description ?? "", isFeatured: product.isFeatured, isActive: product.isActive };
}

function toProductRequest(values: ProductFormValues): AdminProductUpsertRequest {
  return { categoryId: values.categoryId, name: values.name.trim(), slug: values.slug.trim(), description: values.description?.trim() ? values.description.trim() : null, isFeatured: values.isFeatured, isActive: values.isActive };
}

function toVariantFormValues(variant: AdminProductVariantDto): VariantFormValues {
  return { sku: variant.sku, name: variant.name, color: variant.color ?? "", size: variant.size ?? "", price: variant.price, compareAtPrice: variant.compareAtPrice, stockQuantity: variant.stockQuantity, requiresInstallation: variant.requiresInstallation, isActive: variant.isActive };
}

function toVariantRequest(values: VariantFormValues): AdminProductVariantUpsertRequest {
  return { sku: values.sku.trim(), name: values.name.trim(), color: values.color?.trim() ? values.color.trim() : null, size: values.size?.trim() ? values.size.trim() : null, price: values.price, compareAtPrice: values.compareAtPrice, stockQuantity: values.stockQuantity, requiresInstallation: values.requiresInstallation, isActive: values.isActive };
}

function toImageFormValues(image: AdminProductImageDto): ImageFormValues {
  return { imageUrl: image.imageUrl, altText: image.altText ?? "", sortOrder: image.sortOrder };
}

function toImageRequest(values: ImageFormValues): AdminProductImageUpsertRequest {
  return { imageUrl: values.imageUrl.trim(), altText: values.altText?.trim() ? values.altText.trim() : null, sortOrder: values.sortOrder };
}

function toSpecificationFormValues(specification: AdminProductSpecificationDto): SpecificationFormValues {
  return { name: specification.name, value: specification.value, sortOrder: specification.sortOrder };
}

function toSpecificationRequest(values: SpecificationFormValues): AdminProductSpecificationUpsertRequest {
  return { name: values.name.trim(), value: values.value.trim(), sortOrder: values.sortOrder };
}

export function ProductsPage() {
  const queryClient = useQueryClient();
  const [searchParams, setSearchParams] = useSearchParams();
  const productsQuery = useQuery({ queryKey: ["admin-products"], queryFn: adminApi.getProducts });
  const categoriesQuery = useQuery({ queryKey: ["admin-categories"], queryFn: adminApi.getCategories });
  const [notice, setNotice] = useState<{ type: "success" | "error"; message: string } | null>(null);
  const [editingProduct, setEditingProduct] = useState<AdminProductDto | null>(null);
  const [variantProduct, setVariantProduct] = useState<AdminProductDto | null>(null);
  const [editingVariant, setEditingVariant] = useState<AdminProductVariantDto | null>(null);
  const [imageProduct, setImageProduct] = useState<AdminProductDto | null>(null);
  const [editingImage, setEditingImage] = useState<AdminProductImageDto | null>(null);
  const [specificationProduct, setSpecificationProduct] = useState<AdminProductDto | null>(null);
  const [editingSpecification, setEditingSpecification] = useState<AdminProductSpecificationDto | null>(null);
  const [isProductModalOpen, setIsProductModalOpen] = useState(false);
  const [isVariantModalOpen, setIsVariantModalOpen] = useState(false);
  const [isImageModalOpen, setIsImageModalOpen] = useState(false);
  const [isSpecificationModalOpen, setIsSpecificationModalOpen] = useState(false);
  const [deleteTarget, setDeleteTarget] = useState<DeleteTarget | null>(null);
  const [expandedProductIds, setExpandedProductIds] = useState<Set<string>>(new Set());
  const handledTargetRef = useRef<string | null>(null);

  const productForm = useForm<ProductFormValues>({ resolver: zodResolver(productSchema), defaultValues: productDefaultValues });
  const variantForm = useForm<VariantFormValues>({ resolver: zodResolver(variantSchema), defaultValues: variantDefaultValues });
  const imageForm = useForm<ImageFormValues>({ resolver: zodResolver(imageSchema), defaultValues: imageDefaultValues });
  const specificationForm = useForm<SpecificationFormValues>({ resolver: zodResolver(specificationSchema), defaultValues: specificationDefaultValues });
  const categoryOptions = useMemo(() => flattenCategories(categoriesQuery.data ?? []), [categoriesQuery.data]);
  const targetProductId = searchParams.get("productId");
  const targetVariantId = searchParams.get("variantId");
  const targetProduct = useMemo(() => {
    if (!targetProductId) return null;
    const product = productsQuery.data?.find((item) => item.id === targetProductId) ?? null;
    if (targetVariantId && !product?.variants.some((variant) => variant.id === targetVariantId)) return null;
    return product;
  }, [productsQuery.data, targetProductId, targetVariantId]);

  useEffect(() => {
    if (!targetProduct) {
      handledTargetRef.current = null;
      return;
    }

    const targetKey = `${targetProduct.id}:${targetVariantId ?? ""}`;
    if (handledTargetRef.current === targetKey) return;

    handledTargetRef.current = targetKey;
    window.setTimeout(() => {
      document.getElementById(targetVariantId ? `variant-${targetVariantId}` : `product-${targetProduct.id}`)?.scrollIntoView({ behavior: "smooth", block: "center" });
    }, 0);
  }, [targetProduct, targetVariantId]);

  async function refreshProducts() {
    await queryClient.invalidateQueries({ queryKey: ["admin-products"] });
  }

  const productSaveMutation = useMutation({
    mutationFn: (values: ProductFormValues) => {
      const request = toProductRequest(values);
      return editingProduct ? adminApi.updateProduct(editingProduct.id, request) : adminApi.createProduct(request);
    },
    onSuccess: async () => {
      await Promise.all([queryClient.invalidateQueries({ queryKey: ["admin-products"] }), queryClient.invalidateQueries({ queryKey: ["admin-dashboard"] })]);
      setIsProductModalOpen(false);
      setEditingProduct(null);
      productForm.reset(productDefaultValues);
      setNotice({ type: "success", message: "Product saved." });
    },
    onError: (error) => setNotice({ type: "error", message: getApiErrorMessage(error) })
  });

  const productToggleMutation = useMutation({
    mutationFn: (product: AdminProductDto) => adminApi.updateProduct(product.id, { ...toProductRequest(toProductFormValues(product)), isActive: !product.isActive }),
    onSuccess: async () => { await refreshProducts(); setNotice({ type: "success", message: "Product status updated." }); },
    onError: (error) => setNotice({ type: "error", message: getApiErrorMessage(error) })
  });

  const productDeleteMutation = useMutation({
    mutationFn: (product: AdminProductDto) => adminApi.deleteProduct(product.id),
    onSuccess: async (_, product) => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ["admin-products"] }),
        queryClient.invalidateQueries({ queryKey: ["admin-dashboard"] })
      ]);
      setExpandedProductIds((current) => {
        const next = new Set(current);
        next.delete(product.id);
        return next;
      });
      setNotice({ type: "success", message: "Product deleted." });
    },
    onError: (error) => setNotice({ type: "error", message: getApiErrorMessage(error) })
  });

  const variantSaveMutation = useMutation({
    mutationFn: (values: VariantFormValues) => {
      const request = toVariantRequest(values);
      if (editingVariant) return adminApi.updateProductVariant(editingVariant.id, request);
      if (!variantProduct) throw new Error("Product is required for a new variant.");
      return adminApi.createProductVariant(variantProduct.id, request);
    },
    onSuccess: async () => {
      await Promise.all([queryClient.invalidateQueries({ queryKey: ["admin-products"] }), queryClient.invalidateQueries({ queryKey: ["admin-dashboard"] })]);
      setIsVariantModalOpen(false);
      setVariantProduct(null);
      setEditingVariant(null);
      variantForm.reset(variantDefaultValues);
      setNotice({ type: "success", message: "Variant saved." });
    },
    onError: (error) => setNotice({ type: "error", message: getApiErrorMessage(error) })
  });

  const variantToggleMutation = useMutation({
    mutationFn: (variant: AdminProductVariantDto) => adminApi.updateProductVariant(variant.id, { ...toVariantRequest(toVariantFormValues(variant)), isActive: !variant.isActive }),
    onSuccess: async () => { await refreshProducts(); setNotice({ type: "success", message: "Variant status updated." }); },
    onError: (error) => setNotice({ type: "error", message: getApiErrorMessage(error) })
  });

  const imageSaveMutation = useMutation({
    mutationFn: (values: ImageFormValues) => {
      const request = toImageRequest(values);
      if (editingImage) return adminApi.updateProductImage(editingImage.id, request);
      if (!imageProduct) throw new Error("Product is required for a new image.");
      return adminApi.createProductImage(imageProduct.id, request);
    },
    onSuccess: async () => {
      await refreshProducts();
      setIsImageModalOpen(false);
      setImageProduct(null);
      setEditingImage(null);
      imageForm.reset(imageDefaultValues);
      setNotice({ type: "success", message: "Product image saved." });
    },
    onError: (error) => setNotice({ type: "error", message: getApiErrorMessage(error) })
  });

  const imageDeleteMutation = useMutation({
    mutationFn: (image: AdminProductImageDto) => adminApi.deleteProductImage(image.id),
    onSuccess: async () => { await refreshProducts(); setNotice({ type: "success", message: "Product image deleted." }); },
    onError: (error) => setNotice({ type: "error", message: getApiErrorMessage(error) })
  });

  const specificationSaveMutation = useMutation({
    mutationFn: (values: SpecificationFormValues) => {
      const request = toSpecificationRequest(values);
      if (editingSpecification) return adminApi.updateProductSpecification(editingSpecification.id, request);
      if (!specificationProduct) throw new Error("Product is required for a new specification.");
      return adminApi.createProductSpecification(specificationProduct.id, request);
    },
    onSuccess: async () => {
      await refreshProducts();
      setIsSpecificationModalOpen(false);
      setSpecificationProduct(null);
      setEditingSpecification(null);
      specificationForm.reset(specificationDefaultValues);
      setNotice({ type: "success", message: "Product specification saved." });
    },
    onError: (error) => setNotice({ type: "error", message: getApiErrorMessage(error) })
  });

  const specificationDeleteMutation = useMutation({
    mutationFn: (specification: AdminProductSpecificationDto) => adminApi.deleteProductSpecification(specification.id),
    onSuccess: async () => { await refreshProducts(); setNotice({ type: "success", message: "Product specification deleted." }); },
    onError: (error) => setNotice({ type: "error", message: getApiErrorMessage(error) })
  });

  function openCreateProductModal() { setEditingProduct(null); productForm.reset({ ...productDefaultValues, categoryId: categoryOptions[0]?.id ?? "" }); setIsProductModalOpen(true); }
  function openEditProductModal(product: AdminProductDto) { setEditingProduct(product); productForm.reset(toProductFormValues(product)); setIsProductModalOpen(true); }
  function openCreateVariantModal(product: AdminProductDto) { setVariantProduct(product); setEditingVariant(null); variantForm.reset(variantDefaultValues); setIsVariantModalOpen(true); }
  function openEditVariantModal(product: AdminProductDto, variant: AdminProductVariantDto) { setVariantProduct(product); setEditingVariant(variant); variantForm.reset(toVariantFormValues(variant)); setIsVariantModalOpen(true); }
  function openCreateImageModal(product: AdminProductDto) { setImageProduct(product); setEditingImage(null); imageForm.reset({ ...imageDefaultValues, sortOrder: product.images.length + 1 }); setIsImageModalOpen(true); }
  function openEditImageModal(product: AdminProductDto, image: AdminProductImageDto) { setImageProduct(product); setEditingImage(image); imageForm.reset(toImageFormValues(image)); setIsImageModalOpen(true); }
  function openCreateSpecificationModal(product: AdminProductDto) { setSpecificationProduct(product); setEditingSpecification(null); specificationForm.reset({ ...specificationDefaultValues, sortOrder: product.specifications.length + 1 }); setIsSpecificationModalOpen(true); }
  function openEditSpecificationModal(product: AdminProductDto, specification: AdminProductSpecificationDto) { setSpecificationProduct(product); setEditingSpecification(specification); specificationForm.reset(toSpecificationFormValues(specification)); setIsSpecificationModalOpen(true); }
  function toggleExpanded(productId: string) {
    if (targetProduct?.id === productId) {
      const nextParams = new URLSearchParams(searchParams);
      nextParams.delete("productId");
      nextParams.delete("variantId");
      setSearchParams(nextParams, { replace: true });
      return;
    }

    setExpandedProductIds((current) => {
      const next = new Set(current);
      if (next.has(productId)) next.delete(productId);
      else next.add(productId);
      return next;
    });
  }
  function isProductExpanded(productId: string) { return expandedProductIds.has(productId) || targetProduct?.id === productId; }

  function confirmDeleteTarget() {
    if (!deleteTarget) {
      return;
    }

    if (deleteTarget.type === "product") {
      productDeleteMutation.mutate(deleteTarget.item, { onSuccess: () => setDeleteTarget(null) });
      return;
    }

    if (deleteTarget.type === "image") {
      imageDeleteMutation.mutate(deleteTarget.item, { onSuccess: () => setDeleteTarget(null) });
      return;
    }

    specificationDeleteMutation.mutate(deleteTarget.item, { onSuccess: () => setDeleteTarget(null) });
  }

  return (
    <div className="admin-page-grid">
      <AdminPageHeader title="Products" description="Manage products, visibility, featured state, variants, images, specifications, pricing, stock, and installation flags." actions={<Button type="button" variant="primary" disabled={categoryOptions.length === 0} onClick={openCreateProductModal}>New product</Button>} />
      {notice ? <Notice type={notice.type} title={notice.message} /> : null}
      {productsQuery.isError ? <Notice type="error" title="Products could not be loaded">{getApiErrorMessage(productsQuery.error)}</Notice> : null}
      {categoriesQuery.isError ? <Notice type="warning" title="Categories could not be loaded">Product forms need categories before creating or editing products.</Notice> : null}

      <section className="rounded-3xl border border-slate-200 bg-white p-5 shadow-sm">
        {productsQuery.isLoading ? <div className="grid gap-3">{[0, 1, 2].map((item) => <div key={item} className="h-16 animate-pulse rounded-2xl bg-slate-100" />)}</div> : productsQuery.data?.length ? (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[1120px] text-left text-sm">
              <thead className="text-xs uppercase tracking-wide text-slate-500"><tr className="border-b border-slate-100"><th className="py-3 pr-4">Product</th><th className="py-3 pr-4">Category</th><th className="py-3 pr-4">Variants</th><th className="py-3 pr-4">Assets</th><th className="py-3 pr-4">Featured</th><th className="py-3 pr-4">Status</th><th className="py-3 pr-4">Actions</th></tr></thead>
              <tbody>
                {productsQuery.data.map((product) => (
                  <Fragment key={product.id}>
                    <tr id={`product-${product.id}`} key={product.id} className={cx("border-b border-slate-100 transition-colors", targetProduct?.id === product.id && "bg-teal-50/80")}>
                      <td className="py-3 pr-4"><button type="button" className="mr-2 rounded font-black text-teal-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-teal-600 focus-visible:ring-offset-2" onClick={() => toggleExpanded(product.id)} aria-label={`${isProductExpanded(product.id) ? "Collapse" : "Expand"} ${product.name}`}>{isProductExpanded(product.id) ? "-" : "+"}</button><span className="font-bold text-slate-900">{product.name}</span><p className="mt-0.5 text-xs text-slate-500">{product.slug}</p></td>
                      <td className="py-3 pr-4 text-slate-600">{product.categoryName ?? "-"}</td>
                      <td className="py-3 pr-4 text-slate-600">{product.variants.length}</td>
                      <td className="py-3 pr-4 text-slate-600">{product.images.length} images / {product.specifications.length} specs</td>
                      <td className="py-3 pr-4"><Pill tone={product.isFeatured ? "blue" : "slate"}>{product.isFeatured ? "Featured" : "Standard"}</Pill></td>
                      <td className="py-3 pr-4"><div className="flex items-center gap-3"><Toggle checked={product.isActive} disabled={productToggleMutation.isPending} onChange={() => productToggleMutation.mutate(product)} /><Pill tone={product.isActive ? "green" : "slate"}>{product.isActive ? "Active" : "Inactive"}</Pill></div></td>
                      <td className="py-3 pr-4"><div className="flex flex-wrap gap-2"><Button type="button" onClick={() => openEditProductModal(product)}>Edit</Button><Button type="button" onClick={() => openCreateVariantModal(product)}>Add SKU</Button><Button type="button" onClick={() => openCreateImageModal(product)}>Add image</Button><Button type="button" onClick={() => openCreateSpecificationModal(product)}>Add spec</Button><Button type="button" variant="danger" disabled={productDeleteMutation.isPending} onClick={() => setDeleteTarget({ type: "product", item: product })}>Delete</Button></div></td>
                    </tr>
                    {isProductExpanded(product.id) ? (
                      <tr key={`${product.id}-assets`} className="border-b border-slate-100 bg-slate-50/70"><td colSpan={7} className="p-4">
                        <div className="grid gap-5">
                          <section className="rounded-2xl border border-slate-200 bg-white p-4">
                            <div className="mb-3 flex items-center justify-between gap-3">
                              <h3 className="text-sm font-black text-slate-900">Variants</h3>
                              <Button type="button" onClick={() => openCreateVariantModal(product)}>Add SKU</Button>
                            </div>
                            {product.variants.length ? <div className="overflow-x-auto"><table className="w-full min-w-[920px] text-left text-xs"><thead className="uppercase tracking-wide text-slate-500"><tr><th className="py-2 pr-3">SKU</th><th className="py-2 pr-3">Variant</th><th className="py-2 pr-3">Color</th><th className="py-2 pr-3">Size</th><th className="py-2 pr-3">Price</th><th className="py-2 pr-3">Compare at</th><th className="py-2 pr-3">Stock</th><th className="py-2 pr-3">Install</th><th className="py-2 pr-3">Active</th><th className="py-2 pr-3">Actions</th></tr></thead><tbody>{product.variants.map((variant) => <tr id={`variant-${variant.id}`} key={variant.id} className={cx("border-t border-slate-200 transition-colors", targetVariantId === variant.id && "bg-amber-100/80 outline outline-2 outline-amber-300 outline-offset-[-2px]")}><td className="py-2 pr-3 font-bold">{variant.sku}</td><td className="py-2 pr-3">{variant.name}</td><td className="py-2 pr-3">{variant.color || "-"}</td><td className="py-2 pr-3">{variant.size || "-"}</td><td className="py-2 pr-3">{formatMoney(variant.price)}</td><td className="py-2 pr-3">{variant.compareAtPrice === null ? "-" : formatMoney(variant.compareAtPrice)}</td><td className="py-2 pr-3"><Pill tone={variant.stockQuantity <= 5 ? "red" : variant.stockQuantity <= 10 ? "orange" : "green"}>{variant.stockQuantity}</Pill></td><td className="py-2 pr-3"><Pill tone={variant.requiresInstallation ? "blue" : "slate"}>{variant.requiresInstallation ? "Required" : "None"}</Pill></td><td className="py-2 pr-3"><Toggle checked={variant.isActive} disabled={variantToggleMutation.isPending} onChange={() => variantToggleMutation.mutate(variant)} /></td><td className="py-2 pr-3"><Button type="button" onClick={() => openEditVariantModal(product, variant)}>Edit</Button></td></tr>)}</tbody></table></div> : <EmptyState>No variants for this product</EmptyState>}
                          </section>

                          <section className="rounded-2xl border border-slate-200 bg-white p-4">
                            <div className="mb-3 flex items-center justify-between gap-3">
                              <h3 className="text-sm font-black text-slate-900">Product images</h3>
                              <Button type="button" onClick={() => openCreateImageModal(product)}>Add image</Button>
                            </div>
                            {product.images.length ? <div className="grid gap-3 md:grid-cols-2">{product.images.map((image) => <div key={image.id} className="rounded-2xl border border-slate-200 p-4"><div className="flex items-start justify-between gap-3"><div className="min-w-0"><p className="truncate text-sm font-black text-slate-900">{image.imageUrl}</p><p className="mt-1 text-xs text-slate-500">Alt: {image.altText || "-"}</p><p className="mt-1 text-xs text-slate-500">Sort order: {image.sortOrder}</p></div><Pill tone="teal">Image</Pill></div><div className="mt-3 flex gap-2"><Button type="button" onClick={() => openEditImageModal(product, image)}>Edit</Button><Button type="button" variant="danger" disabled={imageDeleteMutation.isPending} onClick={() => setDeleteTarget({ type: "image", item: image })}>Delete</Button></div></div>)}</div> : <EmptyState>No images for this product</EmptyState>}
                          </section>

                          <section className="rounded-2xl border border-slate-200 bg-white p-4">
                            <div className="mb-3 flex items-center justify-between gap-3">
                              <h3 className="text-sm font-black text-slate-900">Specifications</h3>
                              <Button type="button" onClick={() => openCreateSpecificationModal(product)}>Add spec</Button>
                            </div>
                            {product.specifications.length ? <div className="overflow-x-auto"><table className="w-full min-w-[720px] text-left text-xs"><thead className="uppercase tracking-wide text-slate-500"><tr><th className="py-2 pr-3">Name</th><th className="py-2 pr-3">Value</th><th className="py-2 pr-3">Sort</th><th className="py-2 pr-3">Actions</th></tr></thead><tbody>{product.specifications.map((specification) => <tr key={specification.id} className="border-t border-slate-200"><td className="py-2 pr-3 font-bold text-slate-900">{specification.name}</td><td className="py-2 pr-3 text-slate-600">{specification.value}</td><td className="py-2 pr-3 text-slate-600">{specification.sortOrder}</td><td className="py-2 pr-3"><div className="flex gap-2"><Button type="button" onClick={() => openEditSpecificationModal(product, specification)}>Edit</Button><Button type="button" variant="danger" disabled={specificationDeleteMutation.isPending} onClick={() => setDeleteTarget({ type: "specification", item: specification })}>Delete</Button></div></td></tr>)}</tbody></table></div> : <EmptyState>No specifications for this product</EmptyState>}
                          </section>
                        </div>
                      </td></tr>
                    ) : null}
                  </Fragment>
                ))}
              </tbody>
            </table>
          </div>
        ) : <EmptyState>No products yet</EmptyState>}
      </section>

      <Modal title={editingProduct ? "Edit product" : "New product"} open={isProductModalOpen} onClose={() => setIsProductModalOpen(false)} widthClass="max-w-2xl" footer={<><Button type="button" onClick={() => setIsProductModalOpen(false)}>Cancel</Button><Button type="button" variant="primary" disabled={productSaveMutation.isPending} onClick={productForm.handleSubmit((values) => productSaveMutation.mutate(values))}>{productSaveMutation.isPending ? "Saving..." : "Save"}</Button></>}>
        <form className="grid gap-4" noValidate>
          <Controller control={productForm.control} name="categoryId" render={({ field, fieldState }) => <Field label="Category" error={fieldState.error?.message}><SelectInput value={field.value} onChange={field.onChange}><option value="">Select category</option>{categoryOptions.map((option) => <option key={option.id} value={option.id}>{`${"  ".repeat(option.level)}${option.label}`}</option>)}</SelectInput></Field>} />
          <Controller control={productForm.control} name="name" render={({ field, fieldState }) => <Field label="Name" error={fieldState.error?.message}><TextInput {...field} placeholder="Height adjustable desk" /></Field>} />
          <Controller control={productForm.control} name="slug" render={({ field, fieldState }) => <Field label="Slug" error={fieldState.error?.message}><TextInput {...field} placeholder="height-adjustable-desk" /></Field>} />
          <Controller control={productForm.control} name="description" render={({ field, fieldState }) => <Field label="Description" error={fieldState.error?.message}><TextArea {...field} rows={4} /></Field>} />
          <div className="grid gap-4 sm:grid-cols-2"><Controller control={productForm.control} name="isFeatured" render={({ field }) => <Field label="Featured"><Toggle checked={field.value} onChange={field.onChange} /></Field>} /><Controller control={productForm.control} name="isActive" render={({ field }) => <Field label="Active"><Toggle checked={field.value} onChange={field.onChange} /></Field>} /></div>
        </form>
      </Modal>

      <Modal title={editingVariant ? "Edit variant" : `New SKU${variantProduct ? ` for ${variantProduct.name}` : ""}`} open={isVariantModalOpen} onClose={() => setIsVariantModalOpen(false)} widthClass="max-w-3xl" footer={<><Button type="button" onClick={() => setIsVariantModalOpen(false)}>Cancel</Button><Button type="button" variant="primary" disabled={variantSaveMutation.isPending} onClick={variantForm.handleSubmit((values) => variantSaveMutation.mutate(values))}>{variantSaveMutation.isPending ? "Saving..." : "Save"}</Button></>}>
        <form className="grid gap-4" noValidate>
          <Controller control={variantForm.control} name="sku" render={({ field, fieldState }) => <Field label="SKU" error={fieldState.error?.message}><TextInput {...field} placeholder="DESK-PRO-BLK" /></Field>} />
          <Controller control={variantForm.control} name="name" render={({ field, fieldState }) => <Field label="Variant name" error={fieldState.error?.message}><TextInput {...field} placeholder="Black frame / 140cm" /></Field>} />
          <div className="grid gap-4 md:grid-cols-3"><Controller control={variantForm.control} name="color" render={({ field, fieldState }) => <Field label="Color" error={fieldState.error?.message}><TextInput {...field} placeholder="Black" /></Field>} /><Controller control={variantForm.control} name="size" render={({ field, fieldState }) => <Field label="Size" error={fieldState.error?.message}><TextInput {...field} placeholder="140cm" /></Field>} /><Controller control={variantForm.control} name="stockQuantity" render={({ field, fieldState }) => <Field label="Stock" error={fieldState.error?.message}><TextInput type="number" min={0} value={field.value} onChange={(event) => field.onChange(Number(event.target.value))} /></Field>} /></div>
          <div className="grid gap-4 md:grid-cols-2"><Controller control={variantForm.control} name="price" render={({ field, fieldState }) => <Field label="Price" error={fieldState.error?.message}><TextInput type="number" min={0} value={field.value} onChange={(event) => field.onChange(Number(event.target.value))} /></Field>} /><Controller control={variantForm.control} name="compareAtPrice" render={({ field, fieldState }) => <Field label="Compare at" error={fieldState.error?.message}><TextInput type="number" min={0} value={field.value ?? ""} onChange={(event) => field.onChange(event.target.value === "" ? null : Number(event.target.value))} /></Field>} /></div>
          <div className="grid gap-4 sm:grid-cols-2"><Controller control={variantForm.control} name="requiresInstallation" render={({ field }) => <Field label="Requires installation"><Toggle checked={field.value} onChange={field.onChange} /></Field>} /><Controller control={variantForm.control} name="isActive" render={({ field }) => <Field label="Active"><Toggle checked={field.value} onChange={field.onChange} /></Field>} /></div>
        </form>
      </Modal>

      <Modal title={editingImage ? "Edit product image" : `New image${imageProduct ? ` for ${imageProduct.name}` : ""}`} open={isImageModalOpen} onClose={() => setIsImageModalOpen(false)} widthClass="max-w-2xl" footer={<><Button type="button" onClick={() => setIsImageModalOpen(false)}>Cancel</Button><Button type="button" variant="primary" disabled={imageSaveMutation.isPending} onClick={imageForm.handleSubmit((values) => imageSaveMutation.mutate(values))}>{imageSaveMutation.isPending ? "Saving..." : "Save"}</Button></>}>
        <form className="grid gap-4" noValidate>
          <Controller control={imageForm.control} name="imageUrl" render={({ field, fieldState }) => <Field label="Image URL" error={fieldState.error?.message}><TextInput {...field} placeholder="https://example.test/product.jpg" /></Field>} />
          <Controller control={imageForm.control} name="altText" render={({ field, fieldState }) => <Field label="Alt text" error={fieldState.error?.message}><TextInput {...field} placeholder="Standing desk front view" /></Field>} />
          <Controller control={imageForm.control} name="sortOrder" render={({ field, fieldState }) => <Field label="Sort order" error={fieldState.error?.message}><TextInput type="number" value={field.value} onChange={(event) => field.onChange(Number(event.target.value))} /></Field>} />
        </form>
      </Modal>

      <Modal title={editingSpecification ? "Edit specification" : `New specification${specificationProduct ? ` for ${specificationProduct.name}` : ""}`} open={isSpecificationModalOpen} onClose={() => setIsSpecificationModalOpen(false)} widthClass="max-w-2xl" footer={<><Button type="button" onClick={() => setIsSpecificationModalOpen(false)}>Cancel</Button><Button type="button" variant="primary" disabled={specificationSaveMutation.isPending} onClick={specificationForm.handleSubmit((values) => specificationSaveMutation.mutate(values))}>{specificationSaveMutation.isPending ? "Saving..." : "Save"}</Button></>}>
        <form className="grid gap-4" noValidate>
          <Controller control={specificationForm.control} name="name" render={({ field, fieldState }) => <Field label="Name" error={fieldState.error?.message}><TextInput {...field} placeholder="Material" /></Field>} />
          <Controller control={specificationForm.control} name="value" render={({ field, fieldState }) => <Field label="Value" error={fieldState.error?.message}><TextInput {...field} placeholder="Solid wood" /></Field>} />
          <Controller control={specificationForm.control} name="sortOrder" render={({ field, fieldState }) => <Field label="Sort order" error={fieldState.error?.message}><TextInput type="number" value={field.value} onChange={(event) => field.onChange(Number(event.target.value))} /></Field>} />
        </form>
      </Modal>

      <ConfirmDialog
        open={deleteTarget !== null}
        title={deleteTarget?.type === "product" ? "Delete product" : deleteTarget?.type === "image" ? "Delete product image" : "Delete product specification"}
        message={deleteTarget?.type === "product" ? "This permanently removes the product, variants, images, and specifications. Products with order history cannot be deleted; deactivate them instead." : deleteTarget?.type === "image" ? "This image will be removed from the product gallery." : "This specification will be removed from the product detail data."}
        confirmLabel="Delete"
        busy={productDeleteMutation.isPending || imageDeleteMutation.isPending || specificationDeleteMutation.isPending}
        onCancel={() => setDeleteTarget(null)}
        onConfirm={confirmDeleteTarget}
      />
    </div>
  );
}
