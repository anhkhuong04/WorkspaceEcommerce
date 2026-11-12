import { useEffect } from "react";
import { Controller, useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { Search, RotateCcw } from "lucide-react";
import { z } from "zod";
import type { AdminProductListRequest } from "@workspace-ecommerce/api-types";
import {
  Button,
  Field,
  SelectInput,
  TextInput,
} from "../../../components/ui/AdminUi";
import type { CategoryOption } from "../productTypes";

const money = z
  .string()
  .regex(/^\d*$/, "Enter a whole VND amount.")
  .refine(
    (value) => !value || Number.isSafeInteger(Number(value)),
    "Price is too large.",
  );
const schema = z
  .object({
    search: z.string().trim().max(150),
    categoryId: z.string(),
    visibility: z.enum(["all", "true", "false"]),
    featured: z.enum(["all", "true", "false"]),
    stock: z.enum(["all", "in", "out", "low"]),
    minPrice: money,
    maxPrice: money,
    sortBy: z.enum([
      "slug",
      "created-desc",
      "price-asc",
      "price-desc",
      "stock-asc",
    ]),
  })
  .superRefine((values, ctx) => {
    if (
      values.minPrice &&
      values.maxPrice &&
      Number(values.minPrice) > Number(values.maxPrice)
    )
      ctx.addIssue({
        code: "custom",
        path: ["maxPrice"],
        message:
          "Maximum price must be greater than or equal to minimum price.",
      });
  });
type Values = z.infer<typeof schema>;
export function ProductFilters({
  request,
  categories,
  busy,
  onApply,
  onReset,
}: {
  request: AdminProductListRequest;
  categories: CategoryOption[];
  busy: boolean;
  onApply: (value: AdminProductListRequest) => void;
  onReset: () => void;
}) {
  const form = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: defaults(request),
  });
  useEffect(() => {
    form.reset(defaults(request));
  }, [request, form]);
  return (
    <form
      className="admin-panel grid gap-4"
      noValidate
      onSubmit={form.handleSubmit((values) =>
        onApply({
          search: values.search || undefined,
          categoryId: values.categoryId || undefined,
          isActive:
            values.visibility === "all"
              ? undefined
              : values.visibility === "true",
          isFeatured:
            values.featured === "all" ? undefined : values.featured === "true",
          stock: values.stock === "all" ? undefined : values.stock,
          minPrice: values.minPrice ? Number(values.minPrice) : undefined,
          maxPrice: values.maxPrice ? Number(values.maxPrice) : undefined,
          sortBy: values.sortBy,
        }),
      )}
    >
      <div className="grid gap-3 md:grid-cols-[minmax(200px,2fr)_1fr_1fr]">
        <Controller
          control={form.control}
          name="search"
          render={({ field, fieldState }) => (
            <Field label="Search products" error={fieldState.error?.message}>
              <TextInput
                {...field}
                type="search"
                placeholder="Product name, slug or SKU"
              />
            </Field>
          )}
        />
        <Controller
          control={form.control}
          name="categoryId"
          render={({ field }) => (
            <Field label="Category">
              <SelectInput {...field}>
                <option value="">All categories</option>
                {categories.map((c) => (
                  <option
                    key={c.id}
                    value={c.id}
                  >{`${"— ".repeat(c.level)}${c.label}`}</option>
                ))}
              </SelectInput>
            </Field>
          )}
        />
        <Controller
          control={form.control}
          name="visibility"
          render={({ field }) => (
            <Field label="Visibility">
              <SelectInput {...field}>
                <option value="all">All visibility</option>
                <option value="true">Active</option>
                <option value="false">Inactive</option>
              </SelectInput>
            </Field>
          )}
        />
      </div>
      <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-5">
        <Controller
          control={form.control}
          name="stock"
          render={({ field }) => (
            <Field label="Inventory">
              <SelectInput {...field}>
                <option value="all">All inventory</option>
                <option value="in">In stock</option>
                <option value="out">Out of stock</option>
                <option value="low">Has SKU at ≤ 5 units</option>
              </SelectInput>
            </Field>
          )}
        />
        <Controller
          control={form.control}
          name="featured"
          render={({ field }) => (
            <Field label="Featured">
              <SelectInput {...field}>
                <option value="all">All products</option>
                <option value="true">Featured only</option>
                <option value="false">Not featured</option>
              </SelectInput>
            </Field>
          )}
        />
        {(["minPrice", "maxPrice"] as const).map((name) => (
          <Controller
            key={name}
            control={form.control}
            name={name}
            render={({ field, fieldState }) => (
              <Field
                label={
                  name === "minPrice"
                    ? "Minimum price (VND)"
                    : "Maximum price (VND)"
                }
                error={fieldState.error?.message}
              >
                <TextInput {...field} type="number" min="0" step="1" />
              </Field>
            )}
          />
        ))}
        <Controller
          control={form.control}
          name="sortBy"
          render={({ field }) => (
            <Field label="Sort products">
              <SelectInput {...field}>
                <option value="slug">Slug A–Z</option>
                <option value="created-desc">Newest first</option>
                <option value="price-asc">Price: low to high</option>
                <option value="price-desc">Price: high to low</option>
                <option value="stock-asc">Stock: low to high</option>
              </SelectInput>
            </Field>
          )}
        />
      </div>
      <div className="flex flex-wrap gap-2">
        <Button type="submit" variant="primary" disabled={busy}>
          <Search size={16} aria-hidden="true" />
          Apply filters
        </Button>
        <Button onClick={onReset} disabled={busy}>
          <RotateCcw size={16} aria-hidden="true" />
          Reset
        </Button>
      </div>
    </form>
  );
}
function defaults(request: AdminProductListRequest): Values {
  return {
    search: request.search ?? "",
    categoryId: request.categoryId ?? "",
    visibility:
      request.isActive === undefined
        ? "all"
        : (String(request.isActive) as Values["visibility"]),
    featured:
      request.isFeatured === undefined
        ? "all"
        : (String(request.isFeatured) as Values["featured"]),
    stock: request.stock ?? "all",
    minPrice: request.minPrice === undefined ? "" : String(request.minPrice),
    maxPrice: request.maxPrice === undefined ? "" : String(request.maxPrice),
    sortBy: request.sortBy ?? "slug",
  };
}
