import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import type { AdminCategoryDto, AdminProductDto, AdminProductUpsertRequest, AdminProductVariantDto, AdminProductVariantUpsertRequest } from "@workspace-ecommerce/api-types";
import { formatMoney } from "@workspace-ecommerce/shared-utils";
import { useMemo, useState } from "react";
import { Controller, useForm } from "react-hook-form";
import { z } from "zod";
import { AdminPageHeader } from "../../components/ui/AdminPageHeader";
import { Button, EmptyState, Field, Modal, Notice, Pill, SelectInput, TextArea, TextInput, Toggle } from "../../components/ui/AdminUi";
import { adminApi } from "../../services/api/adminApi";
import { getApiErrorMessage } from "../../services/api/errors";

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

type ProductFormValues = z.infer<typeof productSchema>;
type VariantFormValues = z.infer<typeof variantSchema>;
type CategoryOption = { id: string; label: string; level: number };

const productDefaultValues: ProductFormValues = { categoryId: "", name: "", slug: "", description: "", isFeatured: false, isActive: true };
const variantDefaultValues: VariantFormValues = { sku: "", name: "", color: "", size: "", price: 0, compareAtPrice: null, stockQuantity: 0, requiresInstallation: false, isActive: true };

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

export function ProductsPage() {
  const queryClient = useQueryClient();
  const productsQuery = useQuery({ queryKey: ["admin-products"], queryFn: adminApi.getProducts });
  const categoriesQuery = useQuery({ queryKey: ["admin-categories"], queryFn: adminApi.getCategories });
  const [notice, setNotice] = useState<{ type: "success" | "error"; message: string } | null>(null);
  const [editingProduct, setEditingProduct] = useState<AdminProductDto | null>(null);
  const [variantProduct, setVariantProduct] = useState<AdminProductDto | null>(null);
  const [editingVariant, setEditingVariant] = useState<AdminProductVariantDto | null>(null);
  const [isProductModalOpen, setIsProductModalOpen] = useState(false);
  const [isVariantModalOpen, setIsVariantModalOpen] = useState(false);
  const [expandedProductIds, setExpandedProductIds] = useState<Set<string>>(new Set());

  const productForm = useForm<ProductFormValues>({ resolver: zodResolver(productSchema), defaultValues: productDefaultValues });
  const variantForm = useForm<VariantFormValues>({ resolver: zodResolver(variantSchema), defaultValues: variantDefaultValues });
  const categoryOptions = useMemo(() => flattenCategories(categoriesQuery.data ?? []), [categoriesQuery.data]);

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
    onSuccess: async () => { await queryClient.invalidateQueries({ queryKey: ["admin-products"] }); setNotice({ type: "success", message: "Product status updated." }); },
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
    onSuccess: async () => { await queryClient.invalidateQueries({ queryKey: ["admin-products"] }); setNotice({ type: "success", message: "Variant status updated." }); },
    onError: (error) => setNotice({ type: "error", message: getApiErrorMessage(error) })
  });

  function openCreateProductModal() { setEditingProduct(null); productForm.reset({ ...productDefaultValues, categoryId: categoryOptions[0]?.id ?? "" }); setIsProductModalOpen(true); }
  function openEditProductModal(product: AdminProductDto) { setEditingProduct(product); productForm.reset(toProductFormValues(product)); setIsProductModalOpen(true); }
  function openCreateVariantModal(product: AdminProductDto) { setVariantProduct(product); setEditingVariant(null); variantForm.reset(variantDefaultValues); setIsVariantModalOpen(true); }
  function openEditVariantModal(product: AdminProductDto, variant: AdminProductVariantDto) { setVariantProduct(product); setEditingVariant(variant); variantForm.reset(toVariantFormValues(variant)); setIsVariantModalOpen(true); }
  function toggleExpanded(productId: string) { setExpandedProductIds((current) => { const next = new Set(current); if (next.has(productId)) next.delete(productId); else next.add(productId); return next; }); }

  return (
    <div className="admin-page-grid">
      <AdminPageHeader title="Products" description="Manage products, visibility, featured state, variants, pricing, stock, and installation flags." actions={<Button type="button" variant="primary" disabled={categoryOptions.length === 0} onClick={openCreateProductModal}>New product</Button>} />
      {notice ? <Notice type={notice.type} title={notice.message} /> : null}
      {productsQuery.isError ? <Notice type="error" title="Products could not be loaded">{getApiErrorMessage(productsQuery.error)}</Notice> : null}
      {categoriesQuery.isError ? <Notice type="warning" title="Categories could not be loaded">Product forms need categories before creating or editing products.</Notice> : null}

      <section className="rounded-3xl border border-slate-200 bg-white p-5 shadow-sm">
        {productsQuery.isLoading ? <div className="grid gap-3">{[0, 1, 2].map((item) => <div key={item} className="h-16 animate-pulse rounded-2xl bg-slate-100" />)}</div> : productsQuery.data?.length ? (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[980px] text-left text-sm">
              <thead className="text-xs uppercase tracking-wide text-slate-500"><tr className="border-b border-slate-100"><th className="py-3 pr-4">Product</th><th className="py-3 pr-4">Category</th><th className="py-3 pr-4">Variants</th><th className="py-3 pr-4">Featured</th><th className="py-3 pr-4">Status</th><th className="py-3 pr-4">Actions</th></tr></thead>
              <tbody>
                {productsQuery.data.map((product) => (
                  <>
                    <tr key={product.id} className="border-b border-slate-100">
                      <td className="py-3 pr-4"><button type="button" className="mr-2 font-black text-teal-700" onClick={() => toggleExpanded(product.id)}>{expandedProductIds.has(product.id) ? "-" : "+"}</button><span className="font-bold text-slate-900">{product.name}</span><p className="mt-0.5 text-xs text-slate-500">{product.slug}</p></td>
                      <td className="py-3 pr-4 text-slate-600">{product.categoryName ?? "-"}</td>
                      <td className="py-3 pr-4 text-slate-600">{product.variants.length}</td>
                      <td className="py-3 pr-4"><Pill tone={product.isFeatured ? "blue" : "slate"}>{product.isFeatured ? "Featured" : "Standard"}</Pill></td>
                      <td className="py-3 pr-4"><div className="flex items-center gap-3"><Toggle checked={product.isActive} disabled={productToggleMutation.isPending} onChange={() => productToggleMutation.mutate(product)} /><Pill tone={product.isActive ? "green" : "slate"}>{product.isActive ? "Active" : "Inactive"}</Pill></div></td>
                      <td className="py-3 pr-4"><div className="flex gap-2"><Button type="button" onClick={() => openEditProductModal(product)}>Edit</Button><Button type="button" onClick={() => openCreateVariantModal(product)}>Add SKU</Button></div></td>
                    </tr>
                    {expandedProductIds.has(product.id) ? (
                      <tr key={`${product.id}-variants`} className="border-b border-slate-100 bg-slate-50/70"><td colSpan={6} className="p-4">
                        {product.variants.length ? <div className="overflow-x-auto"><table className="w-full min-w-[920px] text-left text-xs"><thead className="uppercase tracking-wide text-slate-500"><tr><th className="py-2 pr-3">SKU</th><th className="py-2 pr-3">Variant</th><th className="py-2 pr-3">Color</th><th className="py-2 pr-3">Size</th><th className="py-2 pr-3">Price</th><th className="py-2 pr-3">Compare at</th><th className="py-2 pr-3">Stock</th><th className="py-2 pr-3">Install</th><th className="py-2 pr-3">Active</th><th className="py-2 pr-3">Actions</th></tr></thead><tbody>{product.variants.map((variant) => <tr key={variant.id} className="border-t border-slate-200"><td className="py-2 pr-3 font-bold">{variant.sku}</td><td className="py-2 pr-3">{variant.name}</td><td className="py-2 pr-3">{variant.color || "-"}</td><td className="py-2 pr-3">{variant.size || "-"}</td><td className="py-2 pr-3">{formatMoney(variant.price)}</td><td className="py-2 pr-3">{variant.compareAtPrice === null ? "-" : formatMoney(variant.compareAtPrice)}</td><td className="py-2 pr-3"><Pill tone={variant.stockQuantity <= 5 ? "red" : variant.stockQuantity <= 10 ? "orange" : "green"}>{variant.stockQuantity}</Pill></td><td className="py-2 pr-3"><Pill tone={variant.requiresInstallation ? "blue" : "slate"}>{variant.requiresInstallation ? "Required" : "None"}</Pill></td><td className="py-2 pr-3"><Toggle checked={variant.isActive} disabled={variantToggleMutation.isPending} onChange={() => variantToggleMutation.mutate(variant)} /></td><td className="py-2 pr-3"><Button type="button" onClick={() => openEditVariantModal(product, variant)}>Edit</Button></td></tr>)}</tbody></table></div> : <EmptyState>No variants for this product</EmptyState>}
                      </td></tr>
                    ) : null}
                  </>
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
    </div>
  );
}
