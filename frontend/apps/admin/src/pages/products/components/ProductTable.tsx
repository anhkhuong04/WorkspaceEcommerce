import type {
  AdminProductDto,
  AdminProductImageDto,
  AdminProductSpecificationDto,
  AdminProductVariantDto,
} from "@workspace-ecommerce/api-types";
import { formatMoney } from "@workspace-ecommerce/shared-utils";
import {
  ChevronDown,
  ChevronRight,
  ImagePlus,
  ListPlus,
  PackagePlus,
  Pencil,
  Plus,
  Star,
  Trash2,
} from "lucide-react";
import { Fragment, useEffect, useState } from "react";
import {
  AdminTabs,
  Button,
  EmptyState,
  IconButton,
  Pill,
  RowActions,
  TableViewport,
  Toggle,
} from "../../../components/ui/AdminUi";
import { cx } from "../../../components/ui/cx";
import { formatLocalizedText } from "../../../utils/localizedText";

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
  isProductExpanded: (id: string) => boolean;
  onToggleExpanded: (id: string) => void;
  onToggleProductStatus: (product: AdminProductDto) => void;
  onToggleVariantStatus: (variant: AdminProductVariantDto) => void;
  onEditProduct: (product: AdminProductDto) => void;
  onCreateVariant: (product: AdminProductDto) => void;
  onEditVariant: (
    product: AdminProductDto,
    variant: AdminProductVariantDto,
  ) => void;
  onCreateImage: (product: AdminProductDto) => void;
  onEditImage: (product: AdminProductDto, image: AdminProductImageDto) => void;
  onCreateSpecification: (product: AdminProductDto) => void;
  onEditSpecification: (
    product: AdminProductDto,
    spec: AdminProductSpecificationDto,
  ) => void;
  onDeleteProduct: (product: AdminProductDto) => void;
  onDeleteImage: (image: AdminProductImageDto) => void;
  onDeleteSpecification: (spec: AdminProductSpecificationDto) => void;
}

export function ProductTable(props: ProductTableProps) {
  const { products, isLoading } = props;
  const [mobile, setMobile] = useState(
    () => window.matchMedia("(max-width: 639px)").matches,
  );
  useEffect(() => {
    const query = window.matchMedia("(max-width: 639px)");
    const update = () => setMobile(query.matches);
    query.addEventListener("change", update);
    return () => query.removeEventListener("change", update);
  }, []);
  return (
    <section className="admin-panel !p-0 overflow-hidden">
      {isLoading ? (
        <div
          className="grid gap-3 p-5"
          role="status"
          aria-label="Loading products"
        >
          {[0, 1, 2].map((i) => (
            <div
              key={i}
              className="h-16 animate-pulse rounded-lg bg-slate-100"
            />
          ))}
        </div>
      ) : products.length && mobile ? (
        <div>
          {products.map((p) => {
            const name = formatLocalizedText(p.name);
            const expanded = props.isProductExpanded(p.id);
            const primary = [...p.images].sort(
              (a, b) => a.sortOrder - b.sortOrder || a.id.localeCompare(b.id),
            )[0];
            const units = p.variants.reduce(
              (total, variant) => total + variant.stockQuantity,
              0,
            );
            return (
              <article
                key={p.id}
                id={`product-${p.id}`}
                aria-labelledby={`product-name-${p.id}`}
                className={cx(
                  "min-w-0 border-b border-slate-200 p-4 last:border-0",
                  props.targetProduct?.id === p.id && "bg-teal-50/50",
                )}
              >
                <div className="flex min-w-0 items-start gap-3">
                  {primary ? (
                    <img
                      src={primary.imageUrl}
                      alt=""
                      width={56}
                      height={56}
                      loading="lazy"
                      className="h-14 w-14 shrink-0 rounded-lg border border-slate-100 bg-slate-50 object-contain"
                    />
                  ) : (
                    <span className="grid h-14 w-14 shrink-0 place-items-center rounded-lg bg-slate-100 text-slate-500">
                      <PackagePlus size={22} aria-hidden="true" />
                    </span>
                  )}
                  <div className="min-w-0">
                    <h2
                      id={`product-name-${p.id}`}
                      className="break-words text-sm font-medium leading-5"
                    >
                      {name}
                    </h2>
                    <p className="mt-1 text-xs text-[var(--muted)]">
                      {p.categoryName ?? "Uncategorized"}
                      {p.isFeatured ? (
                        <Star
                          size={13}
                          className="ml-2 inline text-amber-700"
                          aria-label="Featured product"
                        />
                      ) : null}
                    </p>
                  </div>
                </div>
                <div className="mt-4 flex flex-wrap items-center justify-between gap-3">
                  <p className="text-sm font-medium tabular-nums">
                    <ProductPrice product={p} />
                  </p>
                  <p className="text-xs text-[var(--muted)]">
                    {units} units · {p.variants.length} variants
                  </p>
                </div>
                <div className="mt-3 flex flex-wrap items-center justify-between gap-2">
                  <div className="flex items-center gap-2">
                    <Toggle
                      label={`Visibility of ${name}`}
                      checked={p.isActive}
                      disabled={props.productTogglePending}
                      onChange={() => props.onToggleProductStatus(p)}
                    />
                    <span className="text-xs text-[var(--muted-strong)]">
                      {p.isActive ? "Active" : "Inactive"}
                    </span>
                  </div>
                  <ProductActions {...props} product={p} />
                </div>
                <Button
                  className="mt-3 w-full"
                  aria-label={`${expanded ? "Collapse" : "Expand"} ${name}`}
                  aria-expanded={expanded}
                  aria-controls={
                    expanded ? `product-details-${p.id}` : undefined
                  }
                  onClick={() => props.onToggleExpanded(p.id)}
                >
                  {expanded ? (
                    <ChevronDown size={17} aria-hidden="true" />
                  ) : (
                    <ChevronRight size={17} aria-hidden="true" />
                  )}
                  {expanded
                    ? "Hide details"
                    : "Variants, images & specifications"}
                </Button>
                {expanded ? (
                  <div
                    id={`product-details-${p.id}`}
                    className="mt-4 border-t border-slate-200 pt-4"
                  >
                    <ProductDetails
                      key={props.targetVariantId ?? p.id}
                      {...props}
                      product={p}
                    />
                  </div>
                ) : null}
              </article>
            );
          })}
        </div>
      ) : products.length ? (
        <TableViewport label="Products">
          <table className="w-full min-w-[800px]">
            <thead>
              <tr>
                <th>Product</th>
                <th>Price</th>
                <th>Inventory</th>
                <th>Visibility</th>
                <th>
                  <span className="sr-only">Actions</span>
                </th>
              </tr>
            </thead>
            <tbody>
              {products.map((p) => {
                const name = formatLocalizedText(p.name);
                const expanded = props.isProductExpanded(p.id);
                const units = p.variants.reduce(
                  (n, v) => n + v.stockQuantity,
                  0,
                );
                const primary = [...p.images].sort(
                  (a, b) =>
                    a.sortOrder - b.sortOrder || a.id.localeCompare(b.id),
                )[0];
                return (
                  <Fragment key={p.id}>
                    <tr
                      id={`product-${p.id}`}
                      className={cx(
                        "border-b border-slate-100",
                        props.targetProduct?.id === p.id && "bg-teal-50/50",
                      )}
                    >
                      <td className="w-[45%]">
                        <div className="flex items-center gap-2">
                          <IconButton
                            label={`${expanded ? "Collapse" : "Expand"} ${name}`}
                            aria-expanded={expanded}
                            aria-controls={
                              expanded ? `product-details-${p.id}` : undefined
                            }
                            onClick={() => props.onToggleExpanded(p.id)}
                          >
                            {expanded ? (
                              <ChevronDown size={17} aria-hidden="true" />
                            ) : (
                              <ChevronRight size={17} aria-hidden="true" />
                            )}
                          </IconButton>
                          {primary ? (
                            <img
                              src={primary.imageUrl}
                              alt=""
                              width={48}
                              height={48}
                              loading="lazy"
                              className="h-12 w-12 shrink-0 rounded-lg border border-slate-100 bg-slate-50 object-contain"
                            />
                          ) : (
                            <span className="grid h-12 w-12 shrink-0 place-items-center rounded-lg bg-slate-100 text-slate-500">
                              <PackagePlus size={20} aria-hidden="true" />
                            </span>
                          )}
                          <div className="min-w-0">
                            <p
                              className="line-clamp-2 font-medium"
                              title={name}
                            >
                              {name}
                            </p>
                            <p className="mt-1 text-xs text-[var(--muted)]">
                              {p.categoryName ?? "Uncategorized"}
                              {p.isFeatured ? (
                                <Star
                                  size={13}
                                  className="ml-2 inline text-amber-700"
                                  aria-label="Featured product"
                                />
                              ) : null}
                            </p>
                          </div>
                        </div>
                      </td>
                      <td className="whitespace-nowrap font-medium tabular-nums">
                        <ProductPrice product={p} />
                      </td>
                      <td className="whitespace-nowrap tabular-nums">
                        <p className="font-medium">{units} units</p>
                        <p className="mt-1 text-xs text-[var(--muted)]">
                          {p.variants.length} variants
                        </p>
                      </td>
                      <td>
                        <div className="flex items-center gap-2">
                          <Toggle
                            label={`Visibility of ${name}`}
                            checked={p.isActive}
                            disabled={props.productTogglePending}
                            onChange={() => props.onToggleProductStatus(p)}
                          />
                          <span className="text-xs text-[var(--muted-strong)]">
                            {p.isActive ? "Active" : "Inactive"}
                          </span>
                        </div>
                      </td>
                      <td>
                        <ProductActions {...props} product={p} />
                      </td>
                    </tr>
                    {expanded ? (
                      <tr>
                        <td colSpan={5} className="!p-5 bg-slate-50/60">
                          <div id={`product-details-${p.id}`}>
                            <ProductDetails
                              key={props.targetVariantId ?? p.id}
                              {...props}
                              product={p}
                            />
                          </div>
                        </td>
                      </tr>
                    ) : null}
                  </Fragment>
                );
              })}
            </tbody>
          </table>
        </TableViewport>
      ) : (
        <EmptyState>
          No products yet. Add your first product to start building the catalog.
        </EmptyState>
      )}
    </section>
  );
}

function ProductPrice({ product }: { product: AdminProductDto }) {
  const prices = product.variants.map((variant) => variant.price);
  if (!prices.length)
    return <span className="text-[var(--muted)]">No price</span>;
  return (
    <>
      {Math.min(...prices) !== Math.max(...prices) ? (
        <span className="mr-1 text-xs font-normal text-[var(--muted)]">
          From
        </span>
      ) : null}
      {formatMoney(Math.min(...prices))}
    </>
  );
}

function ProductActions(
  props: ProductTableProps & { product: AdminProductDto },
) {
  const p = props.product;
  const name = formatLocalizedText(p.name);
  return (
    <div className="flex items-center gap-1">
      <IconButton label={`Edit ${name}`} onClick={() => props.onEditProduct(p)}>
        <Pencil size={17} aria-hidden="true" />
      </IconButton>
      <RowActions
        label={`More actions for ${name}`}
        actions={[
          {
            label: "Add variant",
            icon: PackagePlus,
            onSelect: () => props.onCreateVariant(p),
          },
          {
            label: "Add image",
            icon: ImagePlus,
            onSelect: () => props.onCreateImage(p),
          },
          {
            label: "Add specification",
            icon: ListPlus,
            onSelect: () => props.onCreateSpecification(p),
          },
          {
            label: "Delete product",
            icon: Trash2,
            danger: true,
            disabled: props.productDeletePending,
            onSelect: () => props.onDeleteProduct(p),
          },
        ]}
      />
    </div>
  );
}

function ProductDetails(
  props: ProductTableProps & { product: AdminProductDto },
) {
  const { product: p } = props;
  return (
    <AdminTabs
      label={`Manage ${formatLocalizedText(p.name)}`}
      tabs={[
        {
          value: "variants",
          label: `Variants (${p.variants.length})`,
          content: (
            <div>
              <div className="mb-3 flex justify-end">
                <Button onClick={() => props.onCreateVariant(p)}>
                  <Plus size={16} aria-hidden="true" />
                  Add variant
                </Button>
              </div>
              {p.variants.length ? (
                <TableViewport
                  label={`Variants of ${formatLocalizedText(p.name)}`}
                  className="max-h-[420px]"
                >
                  <table className="w-full min-w-[780px]">
                    <thead className="sticky top-0">
                      <tr>
                        <th>Variant / SKU</th>
                        <th>Price</th>
                        <th>Stock</th>
                        <th>Installation</th>
                        <th>Visibility</th>
                        <th>
                          <span className="sr-only">Actions</span>
                        </th>
                      </tr>
                    </thead>
                    <tbody>
                      {p.variants.map((v) => (
                        <tr
                          id={`variant-${v.id}`}
                          key={v.id}
                          className={cx(
                            "border-b border-slate-200",
                            props.targetVariantId === v.id &&
                              "bg-amber-50 outline outline-2 outline-amber-300 outline-offset-[-2px]",
                          )}
                        >
                          <td className="max-w-[320px]">
                            <p className="font-medium">{v.name}</p>
                            <p className="mt-1 font-mono text-xs text-[var(--muted)]">
                              {v.sku}
                            </p>
                            {v.color || v.size ? (
                              <p className="mt-1 text-xs text-[var(--muted)]">
                                {[v.color, v.size].filter(Boolean).join(" / ")}
                              </p>
                            ) : null}
                          </td>
                          <td className="whitespace-nowrap tabular-nums">
                            <p className="font-medium">
                              {formatMoney(v.price)}
                            </p>
                            {v.compareAtPrice !== null ? (
                              <p className="mt-1 text-xs text-[var(--muted)] line-through">
                                {formatMoney(v.compareAtPrice)}
                              </p>
                            ) : null}
                          </td>
                          <td>
                            <Pill
                              tone={
                                v.stockQuantity === 0
                                  ? "red"
                                  : v.stockQuantity <= 10
                                    ? "orange"
                                    : "green"
                              }
                            >
                              {v.stockQuantity}
                            </Pill>
                          </td>
                          <td className="text-xs text-[var(--muted-strong)]">
                            {v.requiresInstallation ? "Required" : "None"}
                          </td>
                          <td>
                            <Toggle
                              label={`Visibility of ${v.sku}`}
                              checked={v.isActive}
                              disabled={props.variantTogglePending}
                              onChange={() => props.onToggleVariantStatus(v)}
                            />
                          </td>
                          <td>
                            <IconButton
                              label={`Edit variant ${v.sku}`}
                              onClick={() => props.onEditVariant(p, v)}
                            >
                              <Pencil size={17} aria-hidden="true" />
                            </IconButton>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </TableViewport>
              ) : (
                <EmptyState>No variants yet</EmptyState>
              )}
            </div>
          ),
        },
        {
          value: "images",
          label: `Images (${p.images.length})`,
          content: (
            <div>
              <div className="mb-3 flex justify-end">
                <Button onClick={() => props.onCreateImage(p)}>
                  <ImagePlus size={16} aria-hidden="true" />
                  Add image
                </Button>
              </div>
              {p.images.length ? (
                <div className="grid max-h-[500px] gap-3 overflow-y-auto sm:grid-cols-2 xl:grid-cols-3">
                  {p.images.map((image, i) => (
                    <article
                      key={image.id}
                      className="overflow-hidden rounded-lg border border-slate-200 bg-white"
                    >
                      <img
                        src={image.imageUrl}
                        alt={image.altText || ""}
                        width={240}
                        height={160}
                        loading="lazy"
                        className="h-40 w-full bg-slate-50 object-contain"
                      />
                      <div className="p-3">
                        <p className="line-clamp-2 text-sm font-medium">
                          {image.altText || `Product image ${i + 1}`}
                        </p>
                        <div className="mt-2 flex items-center justify-between">
                          <span className="text-xs text-[var(--muted)]">
                            Position {image.sortOrder}
                          </span>
                          <div className="flex gap-1">
                            <IconButton
                              label={`Edit image ${i + 1}`}
                              onClick={() => props.onEditImage(p, image)}
                            >
                              <Pencil size={16} aria-hidden="true" />
                            </IconButton>
                            <IconButton
                              label={`Delete image ${i + 1}`}
                              variant="danger"
                              disabled={props.imageDeletePending}
                              onClick={() => props.onDeleteImage(image)}
                            >
                              <Trash2 size={16} aria-hidden="true" />
                            </IconButton>
                          </div>
                        </div>
                      </div>
                    </article>
                  ))}
                </div>
              ) : (
                <EmptyState>No images yet</EmptyState>
              )}
            </div>
          ),
        },
        {
          value: "specifications",
          label: `Specifications (${p.specifications.length})`,
          content: (
            <div>
              <div className="mb-3 flex justify-end">
                <Button onClick={() => props.onCreateSpecification(p)}>
                  <ListPlus size={16} aria-hidden="true" />
                  Add specification
                </Button>
              </div>
              {p.specifications.length ? (
                <TableViewport label="Product specifications">
                  <table className="w-full min-w-[540px]">
                    <thead>
                      <tr>
                        <th>Name</th>
                        <th>Value</th>
                        <th>Position</th>
                        <th>
                          <span className="sr-only">Actions</span>
                        </th>
                      </tr>
                    </thead>
                    <tbody>
                      {p.specifications.map((s) => (
                        <tr key={s.id} className="border-b border-slate-200">
                          <td className="font-medium">{s.name}</td>
                          <td>{s.value}</td>
                          <td>{s.sortOrder}</td>
                          <td>
                            <div className="flex gap-1">
                              <IconButton
                                label={`Edit specification ${s.name}`}
                                onClick={() => props.onEditSpecification(p, s)}
                              >
                                <Pencil size={16} aria-hidden="true" />
                              </IconButton>
                              <IconButton
                                label={`Delete specification ${s.name}`}
                                variant="danger"
                                disabled={props.specificationDeletePending}
                                onClick={() => props.onDeleteSpecification(s)}
                              >
                                <Trash2 size={16} aria-hidden="true" />
                              </IconButton>
                            </div>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </TableViewport>
              ) : (
                <EmptyState>No specifications yet</EmptyState>
              )}
            </div>
          ),
        },
      ]}
    />
  );
}
