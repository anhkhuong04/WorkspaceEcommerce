import { useQuery } from "@tanstack/react-query";
import type { StorefrontBlogPostDto, StorefrontProductListItemDto } from "@workspace-ecommerce/api-types";
import { formatMoney } from "@workspace-ecommerce/shared-utils";
import type { ChangeEvent } from "react";
import { useEffect, useMemo, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { getApiErrorMessage } from "../../services/api/errors";
import { storefrontApi } from "../../services/api/storefrontApi";

type SearchTab = "products" | "blogs";

interface StorefrontSearchOverlayProps {
  isOpen: boolean;
  onClose: () => void;
}

export function StorefrontSearchOverlay({ isOpen, onClose }: StorefrontSearchOverlayProps) {
  const inputRef = useRef<HTMLInputElement>(null);
  const [query, setQuery] = useState("");
  const [debouncedQuery, setDebouncedQuery] = useState("");
  const [activeTab, setActiveTab] = useState<SearchTab>("products");
  const normalizedQuery = debouncedQuery.trim();

  useEffect(() => {
    if (!isOpen) {
      return;
    }

    const originalOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    window.setTimeout(() => inputRef.current?.focus(), 0);

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === "Escape") {
        onClose();
      }
    }

    document.addEventListener("keydown", handleKeyDown);
    return () => {
      document.body.style.overflow = originalOverflow;
      document.removeEventListener("keydown", handleKeyDown);
    };
  }, [isOpen, onClose]);

  useEffect(() => {
    const timer = window.setTimeout(() => {
      setDebouncedQuery(query);
    }, 180);

    return () => window.clearTimeout(timer);
  }, [query]);

  useEffect(() => {
    if (!isOpen) {
      setQuery("");
      setDebouncedQuery("");
      setActiveTab("products");
    }
  }, [isOpen]);

  const productsQuery = useQuery({
    queryKey: ["storefront", "header-search", "products", normalizedQuery],
    queryFn: () => storefrontApi.getProducts({ search: normalizedQuery, pageNumber: 1, pageSize: 6 }),
    enabled: isOpen && normalizedQuery.length > 0
  });

  const blogsQuery = useQuery({
    queryKey: ["storefront", "header-search", "blogs"],
    queryFn: storefrontApi.getBlogPosts,
    enabled: isOpen
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

  if (!isOpen) {
    return null;
  }

  return (
    <div className="fixed inset-0 z-[80] grid place-items-start bg-black/35 p-3 pt-16 sm:p-6 sm:pt-20" role="presentation">
      <aside
        className="mx-auto flex max-h-[min(84vh,760px)] w-full max-w-[min(94vw,920px)] flex-col overflow-hidden rounded-2xl bg-white shadow-2xl outline-none"
        role="dialog"
        aria-modal="true"
        aria-labelledby="storefront-search-title"
      >
        <div className="px-5 py-5 sm:px-8 sm:py-6">
          <div className="flex items-center gap-4">
            <input
              ref={inputRef}
              value={query}
              onChange={handleQueryChange}
              className="min-w-0 flex-1 border-0 border-b-2 border-slate-900 bg-transparent px-0 pb-4 text-3xl font-black tracking-tight text-slate-900 outline-none placeholder:text-slate-300 sm:text-4xl"
              placeholder="Search products or blogs"
              aria-label="Search products or blogs"
            />
            {query ? (
              <button type="button" onClick={clearSearch} className="text-base font-semibold text-slate-500 transition hover:text-slate-900">
                Clear
              </button>
            ) : null}
            <button
              type="button"
              onClick={onClose}
              className="grid h-10 w-10 place-items-center rounded-lg text-slate-900 transition hover:bg-slate-100"
              aria-label="Close search"
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
              Products
            </SearchTabButton>
            <SearchTabButton active={activeTab === "blogs"} onClick={() => setActiveTab("blogs")}>
              Blog posts
            </SearchTabButton>
          </div>

          {!hasQuery ? (
            <SearchStateMessage>Start typing to search products and blog posts.</SearchStateMessage>
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
                {activeTab === "products" ? "View all products" : "View all blog posts"}
              </Link>
            </div>
          ) : null}
        </div>
      </aside>
    </div>
  );
}

function SearchTabButton({ active, children, onClick }: { active: boolean; children: string; onClick: () => void }) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={`text-2xl font-black transition sm:text-3xl ${active ? "text-slate-900" : "text-slate-300 hover:text-slate-500"}`}
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
  if (isLoading) {
    return <SearchStateMessage>Searching products...</SearchStateMessage>;
  }

  if (error) {
    return <SearchStateMessage tone="error">{getApiErrorMessage(error)}</SearchStateMessage>;
  }

  if (products.length === 0) {
    return <SearchStateMessage>No matching products.</SearchStateMessage>;
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
            <h3 className="mt-1 truncate text-lg font-black text-slate-800 sm:text-2xl">{product.name}</h3>
            <p className="mt-2 text-lg font-black text-slate-500 sm:text-2xl">{product.minPrice === null ? "Contact us" : formatMoney(product.minPrice)}</p>
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
  if (isLoading) {
    return <SearchStateMessage>Searching blog posts...</SearchStateMessage>;
  }

  if (error) {
    return <SearchStateMessage tone="error">{getApiErrorMessage(error)}</SearchStateMessage>;
  }

  if (posts.length === 0) {
    return <SearchStateMessage>No matching blog posts.</SearchStateMessage>;
  }

  return (
    <div className="grid gap-5">
      {posts.map((post) => (
        <Link key={post.id} to={`/news/${post.slug}`} onClick={onClose} className="grid grid-cols-[96px_minmax(0,1fr)] gap-5 rounded-xl transition hover:bg-slate-50 sm:grid-cols-[144px_minmax(0,1fr)]">
          <div className="grid aspect-[4/3] place-items-center overflow-hidden rounded-md bg-[#f1f1f1]">
            {post.imageUrl ? (
              <img src={post.imageUrl} alt={post.title} className="h-full w-full object-cover" />
            ) : (
              <span className="text-xs font-bold text-slate-400">Article</span>
            )}
          </div>
          <div className="min-w-0 self-center pr-2">
            <p className="text-sm font-semibold text-slate-500">Blog post</p>
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
