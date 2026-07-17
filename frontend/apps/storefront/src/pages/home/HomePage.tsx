import { useQuery } from "@tanstack/react-query";
import type {
  StorefrontBlogPostDto,
  StorefrontCategoryDto,
} from "@workspace-ecommerce/api-types";
import { Link } from "react-router-dom";
import { BannerCarousel } from "../../components/ui/BannerCarousel";
import { ProductCard } from "../../components/ui/ProductCard";
import { getApiErrorMessage } from "../../services/api/errors";
import { storefrontApi } from "../../services/api/storefrontApi";

const CATEGORY_BACKGROUNDS = [
  "from-[#ececec] via-[#f7f7f7] to-[#dedede]",
  "from-[#e7e7e5] via-[#f5f5f3] to-[#d7d7d4]",
  "from-[#eeeeec] via-[#fafaf8] to-[#d8d9d5]",
  "from-[#ededed] via-[#f8f8f8] to-[#dfdfdf]",
];

const CATEGORY_IMAGES = [
  "/demo/categories1.svg",
  "/demo/cagetories2.webp",
  "/demo/categories3.svg",
  "/demo/categories4.webp",
];

function CategorySkeleton() {
  return <div className="aspect-[4/5] animate-pulse rounded-xl bg-slate-100" />;
}

function ProductSkeleton() {
  return (
    <div>
      <div className="aspect-square animate-pulse rounded-lg bg-slate-100" />
      <div className="mt-3 h-2.5 w-2/5 animate-pulse rounded-full bg-slate-100" />
      <div className="mt-2 h-3.5 w-4/5 animate-pulse rounded-full bg-slate-100" />
      <div className="mt-2 h-3 w-1/3 animate-pulse rounded-full bg-slate-100" />
    </div>
  );
}

function BlogPostSkeleton() {
  return (
    <div>
      <div className="aspect-[16/10] animate-pulse rounded-2xl bg-slate-100" />
      <div className="mt-5 h-6 w-4/5 animate-pulse rounded-full bg-slate-100" />
      <div className="mt-3 h-3.5 w-full animate-pulse rounded-full bg-slate-100" />
      <div className="mt-2 h-3.5 w-3/4 animate-pulse rounded-full bg-slate-100" />
      <div className="mt-6 h-4 w-1/3 animate-pulse rounded-full bg-slate-100" />
    </div>
  );
}

function CategoryCard({
  category,
  index,
}: {
  category: StorefrontCategoryDto;
  index: number;
}) {
  const imageUrl = CATEGORY_IMAGES[index % CATEGORY_IMAGES.length];

  return (
    <Link
      to={`/products?categorySlug=${category.slug}`}
      className={`group relative aspect-[4/5] overflow-hidden rounded-xl bg-gradient-to-br ${CATEGORY_BACKGROUNDS[index % CATEGORY_BACKGROUNDS.length]}`}
    >
      <div
        className="absolute inset-0 grid place-items-center"
        aria-hidden="true"
      >
        <span className="max-w-[80%] break-words text-center text-5xl font-black uppercase leading-none tracking-[-0.08em] text-black/[0.05] lg:text-7xl">
          {category.name}
        </span>
      </div>
      <img
        src={imageUrl}
        alt={category.name}
        className="absolute inset-0 h-full w-full object-cover transition duration-500 group-hover:scale-[1.03]"
        loading="lazy"
        onError={(event) => {
          event.currentTarget.style.display = "none";
        }}
      />
      <div className="absolute inset-x-0 bottom-0 h-2/5 bg-gradient-to-t from-white/85 to-transparent" />
      <h3 className="ui-caption absolute bottom-5 left-5 right-5 font-semibold text-slate-950 sm:bottom-6 sm:left-6">
        {category.name}
      </h3>
    </Link>
  );
}

function BlogPostCard({ post }: { post: StorefrontBlogPostDto }) {
  return (
    <article className="group min-w-0">
      <Link
        to={`/news/${post.slug}`}
        className="block aspect-[16/10] overflow-hidden rounded-2xl bg-slate-100"
      >
        {post.imageUrl ? (
          <img
            src={post.imageUrl}
            alt={post.title}
            className="h-full w-full object-cover transition duration-500 group-hover:scale-[1.03]"
            loading="lazy"
          />
        ) : (
          <div className="grid h-full w-full place-items-center bg-gradient-to-br from-slate-100 to-slate-200 text-lg font-black text-slate-400">
            Workspace Ecom
          </div>
        )}
      </Link>

      <Link to={`/news/${post.slug}`} className="mt-5 block">
        <h3 className="line-clamp-2 text-xl font-black leading-tight tracking-tight text-slate-950 transition group-hover:text-[var(--brand)] sm:text-2xl">
          {post.title}
        </h3>
      </Link>
      <p className="mt-4 line-clamp-3 text-base font-medium leading-7 text-slate-600">
        {post.summary}
      </p>
      {post.publishedAt ? (
        <time
          dateTime={post.publishedAt}
          className="mt-6 flex items-center gap-3 text-sm font-semibold text-slate-500"
        >
          <svg
            className="h-5 w-5 text-slate-400"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="1.8"
            aria-hidden="true"
          >
            <rect x="4" y="5" width="16" height="15" rx="2" />
            <path d="M8 3v4M16 3v4M4 10h16" strokeLinecap="round" />
          </svg>
          {formatBlogDate(post.publishedAt)}
        </time>
      ) : null}
    </article>
  );
}

function formatBlogDate(value: string) {
  return new Intl.DateTimeFormat("en", {
    month: "short",
    day: "numeric",
    year: "numeric",
  }).format(new Date(value));
}

export function HomePage() {
  const bannersQuery = useQuery({
    queryKey: ["storefront", "banners"],
    queryFn: storefrontApi.getBanners,
  });
  const categoriesQuery = useQuery({
    queryKey: ["storefront", "categories"],
    queryFn: storefrontApi.getCategories,
  });
  const newProductsQuery = useQuery({
    queryKey: ["storefront", "products", "new-arrivals"],
    queryFn: () => storefrontApi.getProducts({ pageNumber: 1, pageSize: 10 }),
  });
  const blogPostsQuery = useQuery({
    queryKey: ["storefront", "blog-posts", "home"],
    queryFn: storefrontApi.getBlogPosts,
  });

  const categories = categoriesQuery.data?.slice(0, 4) ?? [];
  const newProducts = newProductsQuery.data?.items ?? [];
  const latestBlogPosts = [...(blogPostsQuery.data ?? [])]
    .sort((a: StorefrontBlogPostDto, b: StorefrontBlogPostDto) => {
      const firstDate = a.publishedAt ? Date.parse(a.publishedAt) : 0;
      const secondDate = b.publishedAt ? Date.parse(b.publishedAt) : 0;

      return secondDate - firstDate;
    })
    .slice(0, 3);

  return (
    <div className="flex flex-col pb-10">
      <section aria-label="Hero banners">
        <BannerCarousel
          banners={bannersQuery.data ?? []}
          isLoading={bannersQuery.isLoading}
        />
        {bannersQuery.isError && (
          <p className="mx-auto mt-3 max-w-[1440px] rounded-xl bg-red-50 px-5 py-3 text-sm font-semibold text-red-600">
            {getApiErrorMessage(bannersQuery.error)}
          </p>
        )}
      </section>

      <div className="mx-auto flex w-full max-w-[1440px] flex-col gap-14 px-5 pt-12 sm:px-8 lg:px-10 lg:pt-16">
        <section aria-labelledby="categories-title">
          <p className="ui-caption font-semibold text-slate-950">
            What do we have?
          </p>
          <h2
            id="categories-title"
            className="ui-h2 mt-2 tracking-tight text-slate-950"
          >
            Product categories
          </h2>

          <div className="mt-7 grid grid-cols-2 gap-3 sm:gap-5 lg:grid-cols-4 lg:gap-6">
            {categoriesQuery.isLoading &&
              Array.from({ length: 4 }).map((_, index) => (
                <CategorySkeleton key={index} />
              ))}
            {categoriesQuery.isError && (
              <div className="ui-control col-span-full rounded-xl bg-red-50 px-5 py-4 text-red-600">
                {getApiErrorMessage(categoriesQuery.error)}
              </div>
            )}
            {!categoriesQuery.isLoading &&
              !categoriesQuery.isError &&
              categories.length === 0 && (
                <div className="ui-body col-span-full rounded-xl bg-slate-50 px-5 py-8 text-center text-slate-500">
                  No categories yet.
                </div>
              )}
            {categories.map((category, index) => (
              <CategoryCard
                key={category.id}
                category={category}
                index={index}
              />
            ))}
          </div>
        </section>

        <section aria-labelledby="new-arrivals-title">
          <div className="flex items-end justify-between gap-4">
            <h2
              id="new-arrivals-title"
              className="ui-h2 tracking-tight text-slate-950"
            >
              New Arrivals
            </h2>
            <Link
              to="/products"
              className="ui-caption group inline-flex items-center gap-2 font-semibold text-slate-950"
            >
              View all
              <span
                className="grid h-5 w-5 place-items-center rounded-full bg-slate-100 transition group-hover:bg-slate-200"
                aria-hidden="true"
              >
                ›
              </span>
            </Link>
          </div>

          {newProductsQuery.isError && (
            <div className="ui-control mt-6 rounded-xl bg-red-50 px-5 py-4 text-red-600">
              {getApiErrorMessage(newProductsQuery.error)}
            </div>
          )}

          <div className="mt-7 grid grid-cols-2 gap-x-3 gap-y-9 sm:grid-cols-3 sm:gap-x-5 lg:grid-cols-5 lg:gap-x-6">
            {newProductsQuery.isLoading &&
              Array.from({ length: 10 }).map((_, index) => (
                <ProductSkeleton key={index} />
              ))}
            {!newProductsQuery.isLoading &&
              !newProductsQuery.isError &&
              newProducts.length === 0 && (
                <div className="ui-body col-span-full rounded-xl bg-slate-50 px-5 py-8 text-center text-slate-500">
                  <p className="font-semibold">No products yet.</p>
                  <p className="mt-1">
                    Add and activate products in the Admin Portal.
                  </p>
                </div>
              )}
            {newProducts.map((product) => (
              <ProductCard key={product.id} product={product} variant="home" />
            ))}
          </div>
        </section>

        <section aria-labelledby="home-blog-posts-title">
          <div className="flex items-center justify-between gap-4">
            <h2
              id="home-blog-posts-title"
              className="ui-h2 tracking-tight text-slate-950"
            >
              Blog posts
            </h2>
            <Link
              to="/news"
              className="ui-caption group inline-flex items-center gap-3 font-semibold text-slate-950"
            >
              View more
              <span
                className="grid h-8 w-8 place-items-center rounded-full bg-slate-100 text-lg transition group-hover:bg-slate-200"
                aria-hidden="true"
              >
                ›
              </span>
            </Link>
          </div>

          {blogPostsQuery.isError && (
            <div className="ui-control mt-6 rounded-xl bg-red-50 px-5 py-4 text-red-600">
              {getApiErrorMessage(blogPostsQuery.error)}
            </div>
          )}

          <div className="mt-8 grid gap-8 lg:grid-cols-3 lg:gap-10">
            {blogPostsQuery.isLoading &&
              Array.from({ length: 3 }).map((_, index) => (
                <BlogPostSkeleton key={index} />
              ))}
            {!blogPostsQuery.isLoading &&
              !blogPostsQuery.isError &&
              latestBlogPosts.length === 0 && (
                <div className="ui-body col-span-full rounded-xl bg-slate-50 px-5 py-8 text-center text-slate-500">
                  No blog posts published yet.
                </div>
              )}
            {latestBlogPosts.map((post) => (
              <BlogPostCard key={post.id} post={post} />
            ))}
          </div>
        </section>
      </div>
    </div>
  );
}
