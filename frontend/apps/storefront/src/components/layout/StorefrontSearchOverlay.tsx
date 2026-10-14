import { useQuery } from "@tanstack/react-query";
import type { StorefrontBlogPostDto, StorefrontProductListItemDto } from "@workspace-ecommerce/api-types";
import { formatMoney } from "@workspace-ecommerce/shared-utils";
import type { ChangeEvent } from "react";
import { useEffect, useMemo, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router-dom";
import { getApiErrorMessage } from "../../services/api/errors";
import { storefrontApi } from "../../services/api/storefrontApi";
import { useModalDialog } from "../../hooks/useModalDialog";

type SearchTab = "products" | "blogs";

interface StorefrontSearchOverlayProps {
  isOpen: boolean;
  onClose: () => void;
}

export function StorefrontSearchOverlay({ isOpen, onClose }: StorefrontSearchOverlayProps) {
  if (!isOpen) {
    return null;
  }

  return <StorefrontSearchOverlayContent onClose={onClose} />;
}

function StorefrontSearchOverlayContent({ onClose }: Pick<StorefrontSearchOverlayProps, "onClose">) {
  const { t } = useTranslation();
  const inputRef = useRef<HTMLInputElement>(null);
  const dialog = useModalDialog(true, onClose, inputRef);
  const [query, setQuery] = useState("");
  const [debouncedQuery, setDebouncedQuery] = useState("");
  const [activeTab, setActiveTab] = useState<SearchTab>("products");
  const normalizedQuery = debouncedQuery.trim();

  useEffect(() => {
    const timer = window.setTimeout(() => {
      setDebouncedQuery(query);
    }, 180);

    return () => window.clearTimeout(timer);
  }, [query]);

  const productsQuery = useQuery({
    queryKey: ["storefront", "header-search", "products", normalizedQuery],
    queryFn: () => storefrontApi.getProducts({ search: normalizedQuery, pageNumber: 1, pageSize: 6 }),
    enabled: normalizedQuery.length > 0
  });

  const blogsQuery = useQuery({
    queryKey: ["storefront", "header-search", "blogs"],
    queryFn: storefrontApi.getBlogPosts,
    enabled: true
  });

  const products = productsQuery.data?.items ?? [];
  const blogs = useMemo(
    () => filterBlogs(blogsQuery.data ?? [], normalizedQuery).slice(0, 6),
    [blogsQuery.data, normalizedQuery]
  );
  const hasQuery = normalizedQuery.length > 0;
  const activeResultsCount = activeTab === "products" ? products.length : blogs.length;

  function handleQueryChange(event: ChangeEvent<HTMLInputElement>) {
    setQuery(event.target.value);
  }

  function clearSearch() {
    setQuery("");
    setDebouncedQuery("");
    inputRef.current?.focus();
  }

  return (
      <dialog {...dialog}
        className="storefront-modal flex max-h-[min(90dvh,760px)] w-[calc(100%-2rem)] max-w-[920px] flex-col overflow-hidden rounded-2xl"
        aria-labelledby="storefront-search-title"
      >
        <div className="px-5 py-5 sm:px-8 sm:py-6">
          <h2 id="storefront-search-title" className="mb-4 text-sm font-medium text-slate-600">{t("header.search")}</h2>
          <div className="flex items-center gap-4">
            <input
              ref={inputRef}
              value={query}
              onChange={handleQueryChange}
              className="min-w-0 flex-1 border-0 border-b-2 border-slate-900 bg-transparent px-0 pb-3 text-slate-900 placeholder:text-slate-500"
              placeholder={t("search.placeholder")}
              aria-label={t("search.placeholder")}
            />
            {query ? (
              <button type="button" onClick={clearSearch} className="min-h-11 text-sm font-semibold text-slate-600 transition hover:text-slate-900">
                {t("common.clear")}
              </button>
            ) : null}
            <button
              type="button"
              onClick={onClose}
              className="grid h-11 w-11 shrink-0 place-items-center rounded-full text-slate-900 transition hover:bg-slate-100"
              aria-label={t("search.close")}
            >
              <svg className="h-7 w-7" viewBox="0 0 24 24" fill="none" aria-hidden="true">
                <path d="M6 6l12 12M18 6 6 18" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" />
              </svg>
            </button>
          </div>
        </div>

        <div className="min-h-0 flex-1 overflow-y-auto px-5 pb-6 sm:px-8 sm:pb-8">
          <div className="mb-6 flex flex-wrap items-center gap-6">
            <SearchTabButton active={activeTab === "products"} onClick={() => setActiveTab("products")}>
              {t("header.products")}
            </SearchTabButton>
            <SearchTabButton active={activeTab === "blogs"} onClick={() => setActiveTab("blogs")}>
              {t("search.blogPosts")}
            </SearchTabButton>
          </div>

          {!hasQuery ? (
            <SearchStateMessage>{t("search.startTyping")}</SearchStateMessage>
          ) : activeTab === "products" ? (
            <ProductSearchResults
              isLoading={productsQuery.isFetching}
              error={productsQuery.error}
              products={products}
              onClose={onClose}
            />
          ) : (
            <BlogSearchResults
              isLoading={blogsQuery.isFetching}
              error={blogsQuery.error}
              posts={blogs}
              onClose={onClose}
            />
          )}

          {hasQuery && activeResultsCount > 0 ? (
            <div className="mt-7 border-t border-slate-100 pt-5">
              <Link
                to={activeTab === "products" ? `/products?search=${encodeURIComponent(normalizedQuery)}` : "/news"}
                onClick={onClose}
                className="inline-flex h-11 items-center rounded-full bg-slate-950 px-5 text-sm font-bold text-white transition hover:bg-black"
              >
                {activeTab === "products" ? t("search.viewAllProducts") : t("search.viewAllBlogPosts")}
              </Link>
            </div>
          ) : null}
        </div>
      </dialog>
  );
}

function SearchTabButton({ active, children, onClick }: { active: boolean; children: string; onClick: () => void }) {
  return (
    <button
      type="button"
      onClick={onClick}
      aria-pressed={active}
      className={`min-h-11 border-b-2 text-lg font-semibold transition ${active ? "border-slate-950 text-slate-950" : "border-transparent text-slate-500 hover:text-slate-950"}`}
    >
      {children}
    </button>
  );
}

function ProductSearchResults({
  isLoading,
  error,
  products,
  onClose
}: {
  isLoading: boolean;
  error: unknown;
  products: StorefrontProductListItemDto[];
  onClose: () => void;
}) {
  const { t } = useTranslation();
  if (isLoading) {
    return <SearchStateMessage>{t("search.searchingProducts")}</SearchStateMessage>;
  }

  if (error) {
    return <SearchStateMessage tone="error">{getApiErrorMessage(error)}</SearchStateMessage>;
  }

  if (products.length === 0) {
    return <SearchStateMessage>{t("search.noProducts")}</SearchStateMessage>;
  }

  return (
    <div className="grid gap-5">
      {products.map((product) => (
        <Link key={product.id} to={`/products/${product.slug}`} onClick={onClose} className="grid grid-cols-[96px_minmax(0,1fr)] gap-5 rounded-xl transition hover:bg-slate-50 sm:grid-cols-[144px_minmax(0,1fr)]">
          <div className="grid aspect-square place-items-center overflow-hidden rounded-md bg-[#f1f1f1]">
            {product.imageUrl ? (
              <img src={product.imageUrl} alt={product.name} className="h-full w-full object-contain p-3" />
            ) : (
              <span className="text-xs font-bold text-slate-400">WorkspaceEcom</span>
            )}
          </div>
          <div className="min-w-0 self-center pr-2">
            <p className="truncate text-sm font-semibold text-slate-500 sm:text-base">{product.categoryName}</p>
            <h3 className="mt-1 line-clamp-2 text-base font-semibold text-slate-950 sm:text-lg">{product.name}</h3>
            <p className="mt-2 text-lg font-semibold text-slate-950">{product.minPrice === null ? t("common.contactUs") : formatMoney(product.minPrice)}</p>
          </div>
        </Link>
      ))}
    </div>
  );
}

function BlogSearchResults({
  isLoading,
  error,
  posts,
  onClose
}: {
  isLoading: boolean;
  error: unknown;
  posts: StorefrontBlogPostDto[];
  onClose: () => void;
}) {
  const { t } = useTranslation();
  if (isLoading) {
    return <SearchStateMessage>{t("search.searchingBlogs")}</SearchStateMessage>;
  }

  if (error) {
    return <SearchStateMessage tone="error">{getApiErrorMessage(error)}</SearchStateMessage>;
  }

  if (posts.length === 0) {
    return <SearchStateMessage>{t("search.noBlogs")}</SearchStateMessage>;
  }

  return (
    <div className="grid gap-5">
      {posts.map((post) => (
        <Link key={post.id} to={`/news/${post.slug}`} onClick={onClose} className="grid grid-cols-[96px_minmax(0,1fr)] gap-5 rounded-xl transition hover:bg-slate-50 sm:grid-cols-[144px_minmax(0,1fr)]">
          <div className="grid aspect-[4/3] place-items-center overflow-hidden rounded-md bg-[#f1f1f1]">
            {post.imageUrl ? (
              <img src={post.imageUrl} alt={post.title} className="h-full w-full object-cover" />
            ) : (
              <span className="text-xs font-bold text-slate-400">{t("search.article")}</span>
            )}
          </div>
          <div className="min-w-0 self-center pr-2">
            <p className="text-sm font-semibold text-slate-500">{t("search.blogPost")}</p>
            <h3 className="mt-1 line-clamp-1 text-lg font-black text-slate-800 sm:text-2xl">{post.title}</h3>
            <p className="mt-2 line-clamp-2 text-sm font-semibold leading-6 text-slate-500">{post.summary}</p>
          </div>
        </Link>
      ))}
    </div>
  );
}

function SearchStateMessage({ children, tone = "info" }: { children: string; tone?: "info" | "error" }) {
  return (
    <div className={`rounded-xl px-5 py-4 text-sm font-semibold ${tone === "error" ? "bg-red-50 text-red-700" : "bg-slate-50 text-slate-500"}`}>
      {children}
    </div>
  );
}

function filterBlogs(posts: StorefrontBlogPostDto[], query: string) {
  const normalizedQuery = query.trim().toLowerCase();
  if (!normalizedQuery) {
    return [];
  }

  return posts.filter((post) =>
    post.title.toLowerCase().includes(normalizedQuery) ||
    post.summary.toLowerCase().includes(normalizedQuery) ||
    post.content.toLowerCase().includes(normalizedQuery)
  );
}
