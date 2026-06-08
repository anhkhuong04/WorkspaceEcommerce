import { useQuery } from "@tanstack/react-query";
import { formatMoney } from "@workspace-ecommerce/shared-utils";
import type { FormEvent } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { PageHeader } from "../../components/ui/PageHeader";
import { getApiErrorMessage } from "../../services/api/errors";
import { storefrontApi } from "../../services/api/storefrontApi";

const pageSize = 12;

export function ProductListPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const categorySlug = searchParams.get("categorySlug") ?? "";
  const search = searchParams.get("search") ?? "";
  const inStock = searchParams.get("inStock") === "true" ? true : undefined;
  const parsedPageNumber = Number(searchParams.get("pageNumber") ?? "1");
  const pageNumber = Number.isFinite(parsedPageNumber) && parsedPageNumber > 0 ? parsedPageNumber : 1;

  const categoriesQuery = useQuery({
    queryKey: ["storefront", "categories"],
    queryFn: storefrontApi.getCategories
  });
  const productsQuery = useQuery({
    queryKey: ["storefront", "products", { categorySlug, search, inStock, pageNumber, pageSize }],
    queryFn: () =>
      storefrontApi.getProducts({
        categorySlug,
        search,
        inStock,
        pageNumber,
        pageSize
      })
  });

  function updateFilters(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    const next = new URLSearchParams();
    const nextSearch = String(form.get("search") ?? "").trim();
    const nextCategorySlug = String(form.get("categorySlug") ?? "");
    const nextInStock = form.get("inStock") === "on";

    if (nextSearch) {
      next.set("search", nextSearch);
    }

    if (nextCategorySlug) {
      next.set("categorySlug", nextCategorySlug);
    }

    if (nextInStock) {
      next.set("inStock", "true");
    }

    setSearchParams(next);
  }

  function goToPage(nextPageNumber: number) {
    const next = new URLSearchParams(searchParams);
    next.set("pageNumber", String(nextPageNumber));
    setSearchParams(next);
  }

  return (
    <div className="grid gap-6">
      <PageHeader
        eyebrow="Catalog"
        title="Browse live products from the backend catalog."
        description="Filters are stored in the URL and mapped directly to /api/products query parameters."
      />

      <form onSubmit={updateFilters} className="grid gap-4 rounded-[2rem] border border-slate-200 bg-white p-5 shadow-sm lg:grid-cols-[1fr_240px_auto_auto]">
        <label className="grid gap-2">
          <span className="text-xs font-bold uppercase tracking-[0.16em] text-slate-500">Search</span>
          <input
            name="search"
            defaultValue={search}
            placeholder="Desk, chair, lamp..."
            className="h-12 rounded-2xl border border-slate-200 px-4 font-semibold outline-none transition focus:border-emerald-400"
          />
        </label>
        <label className="grid gap-2">
          <span className="text-xs font-bold uppercase tracking-[0.16em] text-slate-500">Category</span>
          <select
            name="categorySlug"
            defaultValue={categorySlug}
            className="h-12 rounded-2xl border border-slate-200 px-4 font-semibold outline-none transition focus:border-emerald-400"
          >
            <option value="">All categories</option>
            {categoriesQuery.data?.map((category) => (
              <option key={category.id} value={category.slug}>
                {category.name}
              </option>
            ))}
          </select>
        </label>
        <label className="flex items-end gap-3 pb-3 text-sm font-bold text-slate-700">
          <input name="inStock" type="checkbox" defaultChecked={inStock} className="h-5 w-5 accent-emerald-600" />
          In stock only
        </label>
        <div className="flex items-end">
          <button type="submit" className="h-12 rounded-full bg-slate-950 px-6 text-sm font-black text-white transition hover:bg-slate-800">
            Apply
          </button>
        </div>
      </form>

      {productsQuery.isLoading && <div className="rounded-3xl bg-white p-8 text-slate-500 shadow-sm">Loading products...</div>}
      {productsQuery.isError && (
        <div className="rounded-3xl bg-red-50 p-8 font-semibold text-red-700">{getApiErrorMessage(productsQuery.error)}</div>
      )}
      {productsQuery.data && productsQuery.data.items.length === 0 && (
        <div className="rounded-3xl bg-white p-8 text-slate-500 shadow-sm">No active products matched these filters.</div>
      )}

      <div className="grid gap-5 md:grid-cols-2 xl:grid-cols-4">
        {productsQuery.data?.items.map((product) => (
          <Link
            key={product.id}
            to={`/products/${product.slug}`}
            className="rounded-[1.5rem] border border-slate-200 bg-white p-5 shadow-sm transition hover:-translate-y-1 hover:shadow-md"
          >
            {product.imageUrl ? (
              <img src={product.imageUrl} alt={product.name} className="aspect-[4/3] w-full rounded-2xl object-cover" />
            ) : (
              <div className="aspect-[4/3] rounded-2xl bg-slate-100" />
            )}
            <div className="mt-4 flex items-start justify-between gap-3">
              <div>
                <p className="text-xs font-bold uppercase tracking-[0.14em] text-slate-400">{product.categoryName}</p>
                <h2 className="mt-1 text-lg font-black text-slate-950">{product.name}</h2>
              </div>
              <span className={`rounded-full px-3 py-1 text-xs font-black ${product.isInStock ? "bg-emerald-50 text-emerald-700" : "bg-slate-100 text-slate-500"}`}>
                {product.isInStock ? "In stock" : "Out"}
              </span>
            </div>
            <p className="mt-2 line-clamp-2 text-sm leading-6 text-slate-500">{product.description ?? "No description."}</p>
            <div className="mt-4 flex items-center gap-2">
              <p className="text-sm font-black text-[var(--brand)]">
                {product.minPrice === null ? "Contact for price" : `From ${formatMoney(product.minPrice)}`}
              </p>
              {product.compareAtPrice !== null && <p className="text-xs font-bold text-slate-400 line-through">{formatMoney(product.compareAtPrice)}</p>}
            </div>
          </Link>
        ))}
      </div>

      {productsQuery.data && productsQuery.data.totalPages > 1 && (
        <div className="flex items-center justify-between rounded-[1.5rem] border border-slate-200 bg-white p-4 shadow-sm">
          <button
            type="button"
            disabled={!productsQuery.data.hasPreviousPage}
            onClick={() => goToPage(productsQuery.data.pageNumber - 1)}
            className="rounded-full border border-slate-200 px-4 py-2 text-sm font-black text-slate-700 disabled:cursor-not-allowed disabled:opacity-40"
          >
            Previous
          </button>
          <p className="text-sm font-bold text-slate-500">
            Page {productsQuery.data.pageNumber} of {productsQuery.data.totalPages}
          </p>
          <button
            type="button"
            disabled={!productsQuery.data.hasNextPage}
            onClick={() => goToPage(productsQuery.data.pageNumber + 1)}
            className="rounded-full border border-slate-200 px-4 py-2 text-sm font-black text-slate-700 disabled:cursor-not-allowed disabled:opacity-40"
          >
            Next
          </button>
        </div>
      )}
    </div>
  );
}
