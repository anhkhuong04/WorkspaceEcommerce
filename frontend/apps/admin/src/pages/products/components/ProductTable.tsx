import type { AdminProductDto, AdminProductImageDto, AdminProductSpecificationDto, AdminProductVariantDto } from "@workspace-ecommerce/api-types";
import { formatMoney } from "@workspace-ecommerce/shared-utils";
import { Fragment } from "react";
import { Button, EmptyState, Pill, Toggle } from "../../../components/ui/AdminUi";
import { cx } from "../../../components/ui/cx";

interface ProductTableProps {
  products: AdminProductDto[];
  isLoading: boolean;
  targetProduct: AdminProductDto | null;
  targetVariantId: string | null;
  productTogglePending: boolean;
  variantTogglePending: boolean;
  productDeletePending: boolean;
  imageDeletePending: boolean;
  specificationDeletePending: boolean;
  isProductExpanded: (productId: string) => boolean;
  onToggleExpanded: (productId: string) => void;
  onToggleProductStatus: (product: AdminProductDto) => void;
  onToggleVariantStatus: (variant: AdminProductVariantDto) => void;
  onEditProduct: (product: AdminProductDto) => void;
  onCreateVariant: (product: AdminProductDto) => void;
  onEditVariant: (product: AdminProductDto, variant: AdminProductVariantDto) => void;
  onCreateImage: (product: AdminProductDto) => void;
  onEditImage: (product: AdminProductDto, image: AdminProductImageDto) => void;
  onCreateSpecification: (product: AdminProductDto) => void;
  onEditSpecification: (product: AdminProductDto, specification: AdminProductSpecificationDto) => void;
  onDeleteProduct: (product: AdminProductDto) => void;
  onDeleteImage: (image: AdminProductImageDto) => void;
  onDeleteSpecification: (specification: AdminProductSpecificationDto) => void;
}

export function ProductTable({
  products,
  isLoading,
  targetProduct,
  targetVariantId,
  productTogglePending,
  variantTogglePending,
  productDeletePending,
  imageDeletePending,
  specificationDeletePending,
  isProductExpanded,
  onToggleExpanded,
  onToggleProductStatus,
  onToggleVariantStatus,
  onEditProduct,
  onCreateVariant,
  onEditVariant,
  onCreateImage,
  onEditImage,
  onCreateSpecification,
  onEditSpecification,
  onDeleteProduct,
  onDeleteImage,
  onDeleteSpecification
}: ProductTableProps) {
  return (
    <section className="rounded-3xl border border-slate-200 bg-white p-5 shadow-sm">
      {isLoading ? (
        <div className="grid gap-3">{[0, 1, 2].map((item) => <div key={item} className="h-16 animate-pulse rounded-2xl bg-slate-100" />)}</div>
      ) : products.length ? (
        <div className="overflow-x-auto">
          <table className="w-full min-w-[1120px] text-left text-sm">
            <thead className="text-xs uppercase tracking-wide text-slate-500">
              <tr className="border-b border-slate-100">
                <th className="py-3 pr-4">Product</th>
                <th className="py-3 pr-4">Category</th>
                <th className="py-3 pr-4">Variants</th>
                <th className="py-3 pr-4">Assets</th>
                <th className="py-3 pr-4">Featured</th>
                <th className="py-3 pr-4">Status</th>
                <th className="py-3 pr-4">Actions</th>
              </tr>
            </thead>
            <tbody>
              {products.map((product) => (
                <Fragment key={product.id}>
                  <tr id={`product-${product.id}`} className={cx("border-b border-slate-100 transition-colors", targetProduct?.id === product.id && "bg-slate-50")}>
                    <td className="py-3 pr-4">
                      <button type="button" className="mr-2 rounded font-black text-slate-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-slate-600 focus-visible:ring-offset-2" onClick={() => onToggleExpanded(product.id)} aria-label={`${isProductExpanded(product.id) ? "Collapse" : "Expand"} ${product.name}`}>
                        {isProductExpanded(product.id) ? "-" : "+"}
                      </button>
                      <span className="font-bold text-slate-900">{product.name}</span>
                      <p className="mt-0.5 text-xs text-slate-500">{product.slug}</p>
                    </td>
                    <td className="py-3 pr-4 text-slate-600">{product.categoryName ?? "-"}</td>
                    <td className="py-3 pr-4 text-slate-600">{product.variants.length}</td>
                    <td className="py-3 pr-4 text-slate-600">{product.images.length} images / {product.specifications.length} specs</td>
                    <td className="py-3 pr-4"><Pill tone={product.isFeatured ? "blue" : "slate"}>{product.isFeatured ? "Featured" : "Standard"}</Pill></td>
                    <td className="py-3 pr-4">
                      <div className="flex items-center gap-3">
                        <Toggle checked={product.isActive} disabled={productTogglePending} onChange={() => onToggleProductStatus(product)} />
                        <Pill tone={product.isActive ? "green" : "slate"}>{product.isActive ? "Active" : "Inactive"}</Pill>
                      </div>
                    </td>
                    <td className="py-3 pr-4">
                      <div className="flex flex-wrap gap-2">
                        <Button type="button" onClick={() => onEditProduct(product)}>Edit</Button>
                        <Button type="button" onClick={() => onCreateVariant(product)}>Add SKU</Button>
                        <Button type="button" onClick={() => onCreateImage(product)}>Add image</Button>
                        <Button type="button" onClick={() => onCreateSpecification(product)}>Add spec</Button>
                        <Button type="button" variant="danger" disabled={productDeletePending} onClick={() => onDeleteProduct(product)}>Delete</Button>
                      </div>
                    </td>
                  </tr>
                  {isProductExpanded(product.id) ? (
                    <tr className="border-b border-slate-100 bg-slate-50/70">
                      <td colSpan={7} className="p-4">
                        <div className="grid gap-5">
                          <section className="rounded-2xl border border-slate-200 bg-white p-4">
                            <div className="mb-3 flex items-center justify-between gap-3">
                              <h3 className="text-sm font-black text-slate-900">Variants</h3>
                              <Button type="button" onClick={() => onCreateVariant(product)}>Add SKU</Button>
                            </div>
                            {product.variants.length ? (
                              <div className="overflow-x-auto">
                                <table className="w-full min-w-[920px] text-left text-xs">
                                  <thead className="uppercase tracking-wide text-slate-500">
                                    <tr>
                                      <th className="py-2 pr-3">SKU</th>
                                      <th className="py-2 pr-3">Variant</th>
                                      <th className="py-2 pr-3">Color</th>
                                      <th className="py-2 pr-3">Size</th>
                                      <th className="py-2 pr-3">Price</th>
                                      <th className="py-2 pr-3">Compare at</th>
                                      <th className="py-2 pr-3">Stock</th>
                                      <th className="py-2 pr-3">Install</th>
                                      <th className="py-2 pr-3">Active</th>
                                      <th className="py-2 pr-3">Actions</th>
                                    </tr>
                                  </thead>
                                  <tbody>
                                    {product.variants.map((variant) => (
                                      <tr id={`variant-${variant.id}`} key={variant.id} className={cx("border-t border-slate-200 transition-colors", targetVariantId === variant.id && "bg-amber-100/80 outline outline-2 outline-amber-300 outline-offset-[-2px]")}>
                                        <td className="py-2 pr-3 font-bold">{variant.sku}</td>
                                        <td className="py-2 pr-3">{variant.name}</td>
                                        <td className="py-2 pr-3">{variant.color || "-"}</td>
                                        <td className="py-2 pr-3">{variant.size || "-"}</td>
                                        <td className="py-2 pr-3">{formatMoney(variant.price)}</td>
                                        <td className="py-2 pr-3">{variant.compareAtPrice === null ? "-" : formatMoney(variant.compareAtPrice)}</td>
                                        <td className="py-2 pr-3"><Pill tone={variant.stockQuantity <= 5 ? "red" : variant.stockQuantity <= 10 ? "orange" : "green"}>{variant.stockQuantity}</Pill></td>
                                        <td className="py-2 pr-3"><Pill tone={variant.requiresInstallation ? "blue" : "slate"}>{variant.requiresInstallation ? "Required" : "None"}</Pill></td>
                                        <td className="py-2 pr-3"><Toggle checked={variant.isActive} disabled={variantTogglePending} onChange={() => onToggleVariantStatus(variant)} /></td>
                                        <td className="py-2 pr-3"><Button type="button" onClick={() => onEditVariant(product, variant)}>Edit</Button></td>
                                      </tr>
                                    ))}
                                  </tbody>
                                </table>
                              </div>
                            ) : <EmptyState>No variants for this product</EmptyState>}
                          </section>

                          <section className="rounded-2xl border border-slate-200 bg-white p-4">
                            <div className="mb-3 flex items-center justify-between gap-3">
                              <h3 className="text-sm font-black text-slate-900">Product images</h3>
                              <Button type="button" onClick={() => onCreateImage(product)}>Add image</Button>
                            </div>
                            {product.images.length ? (
                              <div className="grid gap-3 md:grid-cols-2">
                                {product.images.map((image) => (
                                  <div key={image.id} className="rounded-2xl border border-slate-200 p-4">
                                    <div className="flex items-start justify-between gap-3">
                                      <div className="min-w-0">
                                        <p className="truncate text-sm font-black text-slate-900">{image.imageUrl}</p>
                                        <p className="mt-1 text-xs text-slate-500">Alt: {image.altText || "-"}</p>
                                        <p className="mt-1 text-xs text-slate-500">Sort order: {image.sortOrder}</p>
                                      </div>
                                      <Pill tone="slate">Image</Pill>
                                    </div>
                                    <div className="mt-3 flex gap-2">
                                      <Button type="button" onClick={() => onEditImage(product, image)}>Edit</Button>
                                      <Button type="button" variant="danger" disabled={imageDeletePending} onClick={() => onDeleteImage(image)}>Delete</Button>
                                    </div>
                                  </div>
                                ))}
                              </div>
                            ) : <EmptyState>No images for this product</EmptyState>}
                          </section>

                          <section className="rounded-2xl border border-slate-200 bg-white p-4">
                            <div className="mb-3 flex items-center justify-between gap-3">
                              <h3 className="text-sm font-black text-slate-900">Specifications</h3>
                              <Button type="button" onClick={() => onCreateSpecification(product)}>Add spec</Button>
                            </div>
                            {product.specifications.length ? (
                              <div className="overflow-x-auto">
                                <table className="w-full min-w-[720px] text-left text-xs">
                                  <thead className="uppercase tracking-wide text-slate-500">
                                    <tr>
                                      <th className="py-2 pr-3">Name</th>
                                      <th className="py-2 pr-3">Value</th>
                                      <th className="py-2 pr-3">Sort</th>
                                      <th className="py-2 pr-3">Actions</th>
                                    </tr>
                                  </thead>
                                  <tbody>
                                    {product.specifications.map((specification) => (
                                      <tr key={specification.id} className="border-t border-slate-200">
                                        <td className="py-2 pr-3 font-bold text-slate-900">{specification.name}</td>
                                        <td className="py-2 pr-3 text-slate-600">{specification.value}</td>
                                        <td className="py-2 pr-3 text-slate-600">{specification.sortOrder}</td>
                                        <td className="py-2 pr-3">
                                          <div className="flex gap-2">
                                            <Button type="button" onClick={() => onEditSpecification(product, specification)}>Edit</Button>
                                            <Button type="button" variant="danger" disabled={specificationDeletePending} onClick={() => onDeleteSpecification(specification)}>Delete</Button>
                                          </div>
                                        </td>
                                      </tr>
                                    ))}
                                  </tbody>
                                </table>
                              </div>
                            ) : <EmptyState>No specifications for this product</EmptyState>}
                          </section>
                        </div>
                      </td>
                    </tr>
                  ) : null}
                </Fragment>
              ))}
            </tbody>
          </table>
        </div>
      ) : <EmptyState>No products yet</EmptyState>}
    </section>
  );
}
