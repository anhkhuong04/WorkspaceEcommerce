import { useQuery } from "@tanstack/react-query";
import type { StorefrontCategoryDto, StorefrontProductListItemDto } from "@workspace-ecommerce/api-types";
import { formatMoney } from "@workspace-ecommerce/shared-utils";
import { useEffect, useMemo, useRef, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { useAddProductToCart } from "../../features/cart/useAddProductToCart";
import { getApiErrorMessage } from "../../services/api/errors";
import { storefrontApi } from "../../services/api/storefrontApi";

const pageSize = 12;

interface SelectableCategory {
  id: string;
  name: string;
  slug: string;
  groupName: string | null;
}

interface FilterPanelProps {
  categories: SelectableCategory[];
  categorySlug: string;
  search: string;
  inStock: boolean;
  showHeader?: boolean;
  onClear: () => void;
  onCategoryChange: (categorySlug: string) => void;
  onSearchChange: (value: string) => void;
  onInStockChange: (value: boolean) => void;
}

export function ProductListPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const [isFilterOpen, setIsFilterOpen] = useState(false);
  const categorySlug = searchParams.get("categorySlug") ?? "";
  const search = searchParams.get("search") ?? "";
  const sortBy = normalizeSortBy(searchParams.get("sortBy"));
  const inStock = searchParams.get("inStock") === "true";
  const [draftSearch, setDraftSearch] = useState(search);
  const textFilterTimerRef = useRef<number | undefined>(undefined);
  const parsedPageNumber = Number(searchParams.get("pageNumber") ?? "1");
  const pageNumber = Number.isFinite(parsedPageNumber) && parsedPageNumber > 0 ? parsedPageNumber : 1;

  const categoriesQuery = useQuery({
    queryKey: ["storefront", "categories"],
    queryFn: storefrontApi.getCategories
  });
  const productsQuery = useQuery({
    queryKey: ["storefront", "products", { categorySlug, search, sortBy, inStock, pageNumber, pageSize }],
    queryFn: () =>
      storefrontApi.getProducts({
        categorySlug,
        search,
        sortBy,
        inStock: inStock ? true : undefined,
        pageNumber,
        pageSize
      })
  });

  const selectableCategories = useMemo(
    () => flattenSelectableCategories(categoriesQuery.data ?? []),
    [categoriesQuery.data]
  );
  const selectedCategory = useMemo(
    () => findCategoryBySlug(categoriesQuery.data ?? [], categorySlug),
    [categoriesQuery.data, categorySlug]
  );
  const heroProducts = useMemo(
    () => productsQuery.data?.items.filter((product) => product.imageUrl).slice(0, 8) ?? [],
    [productsQuery.data]
  );
  const activeFilterCount = [categorySlug, search, inStock ? "inStock" : ""].filter(Boolean).length;
  const filterKey = `${categorySlug}|${search}|${inStock}`;

  useEffect(() => {
    if (!isFilterOpen) {
      return;
    }

    const originalOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";

    return () => {
      document.body.style.overflow = originalOverflow;
    };
  }, [isFilterOpen]);

  useEffect(() => {
    setDraftSearch(search);
  }, [search]);

  useEffect(() => {
    return () => {
      clearPendingTextFilterUpdate();
    };
  }, []);

  function clearPendingTextFilterUpdate() {
    if (textFilterTimerRef.current !== undefined) {
      window.clearTimeout(textFilterTimerRef.current);
      textFilterTimerRef.current = undefined;
    }
  }

  function queueTextFilterUpdate(nextSearch: string) {
    clearPendingTextFilterUpdate();

    textFilterTimerRef.current = window.setTimeout(() => {
      textFilterTimerRef.current = undefined;
      setSearchParams(
        createFilterSearchParams({
          categorySlug,
          search: nextSearch.trim(),
          sortBy,
          inStock
        })
      );
    }, 450);
  }

  function updateSearchDraft(value: string) {
    setDraftSearch(value);
    queueTextFilterUpdate(value);
  }

  function updateCategory(nextCategorySlug: string) {
    clearPendingTextFilterUpdate();
    setSearchParams(
      createFilterSearchParams({
        categorySlug: nextCategorySlug,
        search: draftSearch.trim(),
        sortBy,
        inStock
      })
    );
  }

  function updateInStock(nextInStock: boolean) {
    clearPendingTextFilterUpdate();
    setSearchParams(
      createFilterSearchParams({
        categorySlug,
        search: draftSearch.trim(),
        sortBy,
        inStock: nextInStock
      })
    );
  }

  function updateSort(nextSortBy: string) {
    clearPendingTextFilterUpdate();
    setSearchParams(
      createFilterSearchParams({
        categorySlug,
        search: draftSearch.trim(),
        sortBy: nextSortBy,
        inStock
      })
    );
  }

  function clearFilters() {
    clearPendingTextFilterUpdate();
    setDraftSearch("");
    setSearchParams(
      createFilterSearchParams({
        categorySlug: "",
        search: "",
        sortBy,
        inStock: false
      })
    );
  }

  function goToPage(nextPageNumber: number) {
    const next = new URLSearchParams(searchParams);
    next.set("pageNumber", String(nextPageNumber));
    setSearchParams(next);
  }

  function createFilterSearchParams(filters: {
    categorySlug: string;
    search: string;
    sortBy: string;
    inStock: boolean;
  }) {
    const next = new URLSearchParams();
    const nextSearch = filters.search.trim();
    const nextCategorySlug = filters.categorySlug;
    const nextSortBy = filters.sortBy.trim();

    if (nextSearch) {
      next.set("search", nextSearch);
    }

    if (nextCategorySlug) {
      next.set("categorySlug", nextCategorySlug);
    }

    if (nextSortBy && nextSortBy !== "name-asc") {
      next.set("sortBy", nextSortBy);
    }

    if (filters.inStock) {
      next.set("inStock", "true");
    }

    return next;
  }

  return (
    <div className="grid gap-8">
      <CatalogHero
        title={selectedCategory?.name ?? "All products"}
        subtitle="Browse workspace furniture, accessories, and setup essentials from the live catalog."
        products={heroProducts}
      />

      <section className="grid gap-6 lg:grid-cols-[260px_minmax(0,1fr)] lg:items-start">
        <aside className="hidden lg:block">
          <div className="sticky top-28">
            <FilterPanel
              key={`desktop-${filterKey}`}
              categories={selectableCategories}
              categorySlug={categorySlug}
              search={draftSearch}
              inStock={inStock}
              onClear={clearFilters}
              onCategoryChange={updateCategory}
              onSearchChange={updateSearchDraft}
              onInStockChange={updateInStock}
            />
          </div>
        </aside>

        <div className="min-w-0">
          <div className="mb-5 flex flex-col gap-3 border-b border-slate-200 pb-4 sm:flex-row sm:items-center sm:justify-between">
            <div className="flex flex-wrap items-center gap-3">
              <button
                type="button"
                onClick={() => setIsFilterOpen(true)}
                className="inline-flex h-10 items-center gap-2 rounded-md border border-slate-200 bg-white px-3 text-sm font-semibold text-slate-950 shadow-sm transition hover:border-slate-400 lg:hidden"
              >
                <FilterIcon />
                Filters
                {activeFilterCount > 0 && (
                  <span className="flex h-5 min-w-5 items-center justify-center rounded-full bg-slate-950 px-1.5 text-[11px] font-bold text-white">
                    {activeFilterCount}
                  </span>
                )}
              </button>
              <p className="text-sm font-medium text-slate-500">{formatResultSummary(productsQuery.data)}</p>
            </div>

            <label className="flex items-center gap-2 text-sm font-semibold text-slate-700">
              <span className="text-slate-400">Sort by:</span>
              <select
                value={sortBy}
                onChange={(event) => updateSort(event.target.value)}
                className="h-10 rounded-md border border-slate-200 bg-white px-3 text-sm font-bold text-slate-950 outline-none transition hover:border-slate-400 focus:border-slate-950"
              >
                <option value="name-asc">A-Z</option>
                <option value="price-asc">Price: low to high</option>
                <option value="price-desc">Price: high to low</option>
                <option value="updated-desc">Recently updated</option>
              </select>
            </label>
          </div>

          {productsQuery.isLoading && <ProductGridSkeleton />}
          {productsQuery.isError && (
            <div className="rounded-lg bg-red-50 p-6 text-sm font-semibold text-red-700">{getApiErrorMessage(productsQuery.error)}</div>
          )}
          {productsQuery.data && productsQuery.data.items.length === 0 && (
            <div className="rounded-lg border border-slate-200 bg-white p-8 text-center">
              <h2 className="text-lg font-bold text-slate-950">No products found</h2>
              <p className="mt-2 text-sm text-slate-500">Try clearing filters or using a broader search term.</p>
              <button
                type="button"
                onClick={clearFilters}
                className="mt-5 h-10 rounded-md bg-slate-950 px-4 text-sm font-bold text-white transition hover:bg-slate-800"
              >
                Clear filters
              </button>
            </div>
          )}

          {productsQuery.data && productsQuery.data.items.length > 0 && (
            <div className="grid grid-cols-2 gap-x-4 gap-y-8 md:grid-cols-3 xl:grid-cols-4 xl:gap-x-5">
              {productsQuery.data.items.map((product) => (
                <CatalogProductCard key={product.id} product={product} />
              ))}
            </div>
          )}

          {productsQuery.data && productsQuery.data.totalPages > 1 && (
            <div className="mt-10 flex items-center justify-between border-t border-slate-200 pt-5">
              <button
                type="button"
                disabled={!productsQuery.data.hasPreviousPage}
                onClick={() => goToPage(productsQuery.data.pageNumber - 1)}
                className="h-10 rounded-md border border-slate-200 px-4 text-sm font-bold text-slate-700 transition hover:border-slate-400 disabled:cursor-not-allowed disabled:opacity-40"
              >
                Previous
              </button>
              <p className="text-sm font-semibold text-slate-500">
                Page {productsQuery.data.pageNumber} of {productsQuery.data.totalPages}
              </p>
              <button
                type="button"
                disabled={!productsQuery.data.hasNextPage}
                onClick={() => goToPage(productsQuery.data.pageNumber + 1)}
                className="h-10 rounded-md border border-slate-200 px-4 text-sm font-bold text-slate-700 transition hover:border-slate-400 disabled:cursor-not-allowed disabled:opacity-40"
              >
                Next
              </button>
            </div>
          )}
        </div>
      </section>

      {isFilterOpen && (
        <div className="fixed inset-0 z-50 lg:hidden" role="dialog" aria-modal="true" aria-label="Product filters">
          <button type="button" aria-label="Close filters" className="absolute inset-0 bg-black/45" onClick={() => setIsFilterOpen(false)} />
          <aside className="relative ml-auto flex h-full w-[min(92vw,380px)] flex-col bg-white shadow-2xl">
            <div className="flex h-16 shrink-0 items-center justify-between border-b border-slate-200 px-5">
              <div className="flex items-center gap-2 text-sm font-bold text-slate-950">
                <FilterIcon />
                Filters
              </div>
              <button
                type="button"
                aria-label="Close filters"
                onClick={() => setIsFilterOpen(false)}
                className="flex h-9 w-9 items-center justify-center rounded-md border border-slate-200 text-slate-700 transition hover:border-slate-400"
              >
                <CloseIcon />
              </button>
            </div>
            <div className="min-h-0 flex-1 overflow-y-auto p-5">
              <FilterPanel
                key={`mobile-${filterKey}`}
                categories={selectableCategories}
                categorySlug={categorySlug}
                search={draftSearch}
                inStock={inStock}
                showHeader={false}
                onClear={clearFilters}
                onCategoryChange={updateCategory}
                onSearchChange={updateSearchDraft}
                onInStockChange={updateInStock}
              />
            </div>
          </aside>
        </div>
      )}
    </div>
  );
}

function CatalogHero({
  title,
  subtitle,
  products
}: {
  title: string;
  subtitle: string;
  products: StorefrontProductListItemDto[];
}) {
  return (
    <section className="-mt-8 ml-[calc(50%-50vw)] w-screen overflow-hidden bg-slate-950">
      <div className="relative min-h-[220px] px-5 py-12 sm:px-8 lg:min-h-[270px] lg:px-10">
        <div className="absolute inset-0 grid grid-cols-4 grid-rows-2 gap-px opacity-45 sm:grid-cols-6">
          {products.length > 0
            ? products.map((product, index) => (
                <div key={`${product.id}-${index}`} className="min-h-0 bg-slate-800">
                  <img src={product.imageUrl ?? ""} alt="" className="h-full w-full object-cover" loading="eager" />
                </div>
              ))
            : Array.from({ length: 8 }).map((_, index) => (
                <div key={index} className="bg-gradient-to-br from-slate-800 via-slate-700 to-slate-900" />
              ))}
        </div>
        <div className="absolute inset-0 bg-black/55" />
        <div className="relative mx-auto flex min-h-[124px] max-w-[1440px] flex-col justify-end">
          <p className="mb-2 text-sm font-semibold text-white/70">Catalog</p>
          <h1 className="max-w-3xl text-4xl font-black leading-tight text-white sm:text-5xl">{title}</h1>
          <p className="mt-3 max-w-xl text-sm leading-6 text-white/75 sm:text-base">{subtitle}</p>
        </div>
      </div>
    </section>
  );
}

function FilterPanel({
  categories,
  categorySlug,
  search,
  inStock,
  showHeader = true,
  onClear,
  onCategoryChange,
  onSearchChange,
  onInStockChange
}: FilterPanelProps) {
  return (
    <div className="grid gap-6">
      {showHeader && (
        <div className="flex items-center justify-between border-b border-slate-200 pb-4">
          <div className="flex items-center gap-2 text-sm font-bold text-slate-950">
            <FilterIcon />
            Filters
          </div>
          <button type="button" onClick={onClear} className="text-xs font-bold text-slate-500 transition hover:text-slate-950">
            Reset
          </button>
        </div>
      )}

      {!showHeader && (
        <div className="flex justify-end">
          <button type="button" onClick={onClear} className="text-xs font-bold text-slate-500 transition hover:text-slate-950">
            Reset
          </button>
        </div>
      )}

      <label className="grid gap-2">
        <span className="text-sm font-bold text-slate-950">Search</span>
        <span className="flex h-11 items-center gap-2 rounded-md border border-slate-200 bg-white px-3 transition focus-within:border-slate-950">
          <SearchIcon />
          <input
            value={search}
            onChange={(event) => onSearchChange(event.target.value)}
            placeholder="Desk, chair, lamp..."
            className="min-w-0 flex-1 bg-transparent text-sm font-medium text-slate-950 outline-none placeholder:text-slate-400"
          />
        </span>
      </label>

      <div className="grid gap-3 border-b border-slate-200 pb-6">
        <h2 className="text-sm font-bold text-slate-950">Product type</h2>
        <div className="grid grid-cols-3 gap-3">
          <CategoryButton category={null} isActive={!categorySlug} onSelect={onCategoryChange} />
          {categories.map((category) => (
            <CategoryButton key={category.id} category={category} isActive={category.slug === categorySlug} onSelect={onCategoryChange} />
          ))}
        </div>
      </div>

      <label className="flex cursor-pointer items-center justify-between border-b border-slate-200 pb-6">
        <span className="text-sm font-bold text-slate-950">Show in stock only</span>
        <span className="relative inline-flex h-6 w-11 items-center">
          <input
            type="checkbox"
            checked={inStock}
            onChange={(event) => onInStockChange(event.target.checked)}
            className="peer sr-only"
          />
          <span className="absolute inset-0 rounded-full bg-slate-200 transition peer-checked:bg-slate-950" />
          <span className="relative ml-1 h-4 w-4 rounded-full bg-white transition peer-checked:translate-x-5" />
        </span>
      </label>
    </div>
  );
}

function CategoryButton({
  category,
  isActive,
  onSelect
}: {
  category: SelectableCategory | null;
  isActive: boolean;
  onSelect: (categorySlug: string) => void;
}) {
  const label = category?.name ?? "All";
  const value = isActive ? "" : category?.slug ?? "";

  return (
    <button
      type="button"
      onClick={() => onSelect(value)}
      title={category?.groupName ? `${category.groupName} / ${label}` : label}
      className={`relative flex aspect-square min-h-[74px] flex-col items-center justify-center gap-2 rounded-lg border p-2 text-center transition ${
        isActive ? "border-slate-950 bg-slate-950 text-white" : "border-slate-200 bg-white text-slate-700 hover:border-slate-400"
      }`}
    >
      <CategoryGlyph category={category} />
      <span className="line-clamp-2 text-[11px] font-semibold leading-4">{label}</span>
    </button>
  );
}

function CatalogProductCard({ product }: { product: StorefrontProductListItemDto }) {
  const hasDiscount = product.minPrice !== null && product.compareAtPrice !== null && product.compareAtPrice > product.minPrice;
  const addProductMutation = useAddProductToCart();
  const canAdd = product.isInStock && product.minPrice !== null;

  return (
    <article className="group min-w-0">
      <div className="relative aspect-square overflow-hidden rounded-lg bg-[#f1f1f1]">
        <Link to={`/products/${product.slug}`} className="block h-full w-full">
          {product.imageUrl ? (
            <img
              src={product.imageUrl}
              alt={product.name}
              className="h-full w-full object-contain p-4 transition duration-300 group-hover:scale-[1.03]"
              loading="lazy"
            />
          ) : (
            <div className="h-full w-full bg-gradient-to-br from-[#f6f6f6] to-[#e8e8e8]" />
          )}
        </Link>

        {!product.isInStock ? (
          <span className="absolute left-3 top-3 rounded-md bg-slate-950 px-2 py-1 text-[11px] font-bold text-white">Sold out</span>
        ) : hasDiscount ? (
          <span className="absolute left-3 top-3 rounded-md bg-[#e52b1f] px-2 py-1 text-[11px] font-bold text-white">Sale</span>
        ) : product.isFeatured ? (
          <span className="absolute left-3 top-3 rounded-md bg-[#e52b1f] px-2 py-1 text-[11px] font-bold text-white">Featured</span>
        ) : null}

        {canAdd ? (
          <button
            type="button"
            disabled={addProductMutation.isPending}
            onClick={() => addProductMutation.mutate(product.slug)}
            className="absolute bottom-4 right-4 inline-flex h-12 translate-y-3 items-center justify-center rounded-full bg-[#3a3a3a] px-6 text-base font-bold text-white opacity-0 shadow-lg transition duration-200 hover:bg-[#242424] disabled:cursor-wait disabled:opacity-70 group-hover:translate-y-0 group-hover:opacity-100 focus:translate-y-0 focus:opacity-100"
            aria-label={`Add ${product.name} to cart`}
          >
            <span className="mr-1 text-xl leading-none">+</span>
            {addProductMutation.isPending ? "Adding" : "Add"}
          </button>
        ) : null}
      </div>

      <Link to={`/products/${product.slug}`} className="block pt-3">
        <p className="truncate text-xs font-medium text-slate-500">{product.categoryName}</p>
        <h2 className="mt-1 min-h-10 text-sm font-bold leading-5 text-slate-950 transition group-hover:text-[#e52b1f]">{product.name}</h2>
        <div className="mt-2 flex flex-wrap items-baseline gap-x-2 gap-y-1">
          {product.minPrice !== null ? (
            <>
              <span className={`text-sm font-bold ${hasDiscount ? "text-[#e52b1f]" : "text-slate-950"}`}>
                From {formatMoney(product.minPrice)}
              </span>
              {hasDiscount && <span className="text-xs font-medium text-slate-400 line-through">{formatMoney(product.compareAtPrice!)}</span>}
            </>
          ) : (
            <span className="text-sm font-bold text-slate-500">Contact for price</span>
          )}
        </div>
      </Link>
    </article>
  );
}

function ProductGridSkeleton() {
  return (
    <div className="grid grid-cols-2 gap-x-4 gap-y-8 md:grid-cols-3 xl:grid-cols-4 xl:gap-x-5" aria-label="Loading products">
      {Array.from({ length: 8 }).map((_, index) => (
        <div key={index} className="animate-pulse">
          <div className="aspect-square rounded-lg bg-slate-100" />
          <div className="mt-3 h-3 w-20 rounded bg-slate-100" />
          <div className="mt-2 h-4 w-4/5 rounded bg-slate-100" />
          <div className="mt-2 h-4 w-24 rounded bg-slate-100" />
        </div>
      ))}
    </div>
  );
}

function CategoryGlyph({ category }: { category: SelectableCategory | null }) {
  const key = `${category?.slug ?? "all"} ${category?.name ?? "all"}`.toLowerCase();
  const strokeClass = "stroke-current";
  const commonProps = {
    className: `h-7 w-7 ${isCategoryGlyphMuted(category) ? "opacity-80" : ""}`,
    viewBox: "0 0 32 32",
    fill: "none",
    strokeWidth: 1.8,
    strokeLinecap: "round" as const,
    strokeLinejoin: "round" as const,
    "aria-hidden": true
  };

  if (key.includes("desk") || key.includes("table")) {
    return (
      <svg {...commonProps}>
        <path className={strokeClass} d="M6 12h20M9 12v12M23 12v12M8 24h4M20 24h4" />
      </svg>
    );
  }

  if (key.includes("chair")) {
    return (
      <svg {...commonProps}>
        <path className={strokeClass} d="M12 6h10v9H12zM10 15h14M13 15v10M22 15v10M11 25h4M20 25h4" />
      </svg>
    );
  }

  if (key.includes("keyboard")) {
    return (
      <svg {...commonProps}>
        <path className={strokeClass} d="M6 11h20v10H6zM10 15h1M14 15h1M18 15h1M22 15h1M10 18h12" />
      </svg>
    );
  }

  if (key.includes("mouse")) {
    return (
      <svg {...commonProps}>
        <path className={strokeClass} d="M16 6c4 0 7 3.5 7 9v2c0 5.5-3 9-7 9s-7-3.5-7-9v-2c0-5.5 3-9 7-9zM16 6v7" />
      </svg>
    );
  }

  if (key.includes("monitor") || key.includes("mount") || key.includes("arm")) {
    return (
      <svg {...commonProps}>
        <path className={strokeClass} d="M7 8h18v11H7zM16 19v5M12 24h8M21 19l4 5" />
      </svg>
    );
  }

  if (key.includes("light") || key.includes("lamp")) {
    return (
      <svg {...commonProps}>
        <path className={strokeClass} d="M18 7l6 6-4 4-6-6zM14 11l-7 7M7 18l4 4M7 26h11" />
      </svg>
    );
  }

  if (key.includes("drawer") || key.includes("shelf") || key.includes("archive")) {
    return (
      <svg {...commonProps}>
        <path className={strokeClass} d="M8 8h16v16H8zM8 14h16M8 20h16M15 11h2M15 17h2M15 23h2" />
      </svg>
    );
  }

  if (key.includes("clean")) {
    return (
      <svg {...commonProps}>
        <path className={strokeClass} d="M12 8h8M14 8l-2 16h8L18 8M10 24h12M22 10l4-4" />
      </svg>
    );
  }

  if (key.includes("wallet")) {
    return (
      <svg {...commonProps}>
        <path className={strokeClass} d="M7 10h18v13H7zM7 13h18M21 17h4v4h-4z" />
      </svg>
    );
  }

  if (key.includes("tumbler") || key.includes("cup")) {
    return (
      <svg {...commonProps}>
        <path className={strokeClass} d="M11 8h10l-1.5 17h-7zM10 8h12M12 13h8" />
      </svg>
    );
  }

  if (key.includes("accessor") || key.includes("pad") || key.includes("wiring")) {
    return (
      <svg {...commonProps}>
        <path className={strokeClass} d="M9 11h14v10H9zM13 15h6M13 18h6M6 16h3M23 16h3" />
      </svg>
    );
  }

  return (
    <svg {...commonProps}>
      <path className={strokeClass} d="M8 9h16v16H8zM11 13h10M11 17h10M11 21h6" />
    </svg>
  );
}

function FilterIcon() {
  return (
    <svg className="h-4 w-4" viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" aria-hidden="true">
      <path d="M3 5h14M6 10h8M9 15h2" />
      <circle cx="7" cy="5" r="1.5" fill="currentColor" stroke="none" />
      <circle cx="13" cy="10" r="1.5" fill="currentColor" stroke="none" />
    </svg>
  );
}

function SearchIcon() {
  return (
    <svg className="h-4 w-4 shrink-0 text-slate-400" viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" aria-hidden="true">
      <circle cx="9" cy="9" r="5" />
      <path d="m13 13 4 4" />
    </svg>
  );
}

function CloseIcon() {
  return (
    <svg className="h-4 w-4" viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" aria-hidden="true">
      <path d="m5 5 10 10M15 5 5 15" />
    </svg>
  );
}

function flattenSelectableCategories(categories: StorefrontCategoryDto[]): SelectableCategory[] {
  return categories.flatMap((category) => flattenCategory(category, null));
}

function flattenCategory(category: StorefrontCategoryDto, groupName: string | null): SelectableCategory[] {
  if (category.children.length === 0) {
    return [
      {
        id: category.id,
        name: category.name,
        slug: category.slug,
        groupName
      }
    ];
  }

  return category.children.flatMap((child) => flattenCategory(child, groupName ?? category.name));
}

function findCategoryBySlug(categories: StorefrontCategoryDto[], slug: string): StorefrontCategoryDto | null {
  for (const category of categories) {
    if (category.slug === slug) {
      return category;
    }

    const childMatch = findCategoryBySlug(category.children, slug);
    if (childMatch) {
      return childMatch;
    }
  }

  return null;
}

function normalizeSortBy(value: string | null): string {
  return value === "price-asc" || value === "price-desc" || value === "updated-desc" ? value : "name-asc";
}

function formatResultSummary(data: { pageNumber: number; pageSize: number; totalCount: number } | undefined): string {
  if (!data) {
    return "Loading catalog";
  }

  if (data.totalCount === 0) {
    return "No products";
  }

  const start = (data.pageNumber - 1) * data.pageSize + 1;
  const end = Math.min(data.pageNumber * data.pageSize, data.totalCount);
  return `Showing ${start}-${end} of ${data.totalCount} products`;
}

function isCategoryGlyphMuted(category: SelectableCategory | null): boolean {
  return category === null;
}
