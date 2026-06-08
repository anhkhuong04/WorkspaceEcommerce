import { useQuery } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { BannerCarousel } from "../../components/ui/BannerCarousel";
import { ProductCard } from "../../components/ui/ProductCard";
import { getApiErrorMessage } from "../../services/api/errors";
import { storefrontApi } from "../../services/api/storefrontApi";

function CategorySkeleton() {
  return (
    <div className="flex h-24 animate-pulse flex-col items-center justify-center gap-2 rounded-2xl bg-slate-100 p-4" />
  );
}

function ProductSkeleton() {
  return (
    <div className="overflow-hidden rounded-[1.5rem] border border-slate-200 bg-white">
      <div className="aspect-[4/3] animate-pulse bg-slate-100" />
      <div className="p-5 space-y-2">
        <div className="h-3 w-1/3 animate-pulse rounded-full bg-slate-100" />
        <div className="h-4 w-2/3 animate-pulse rounded-full bg-slate-100" />
        <div className="h-4 w-1/4 animate-pulse rounded-full bg-slate-100 mt-4" />
      </div>
    </div>
  );
}

interface SectionHeaderProps {
  eyebrow: string;
  title: string;
  linkTo: string;
  linkLabel: string;
}

function SectionHeader({ eyebrow, title, linkTo, linkLabel }: SectionHeaderProps) {
  return (
    <div className="flex items-end justify-between">
      <div>
        <p className="text-xs font-bold uppercase tracking-[0.2em] text-[var(--brand)]">{eyebrow}</p>
        <h2 className="mt-1.5 text-2xl font-black tracking-tight text-slate-950 lg:text-3xl">{title}</h2>
      </div>
      <Link
        to={linkTo}
        className="inline-flex items-center gap-1.5 text-sm font-bold text-[var(--brand)] transition hover:opacity-70"
      >
        {linkLabel}
        <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5}>
          <path strokeLinecap="round" strokeLinejoin="round" d="M13.5 4.5L21 12m0 0l-7.5 7.5M21 12H3" />
        </svg>
      </Link>
    </div>
  );
}

const CATEGORY_COLORS = [
  "from-[#e0f2fe] to-[#bae6fd] text-sky-800 hover:ring-sky-300",
  "from-[#dcfce7] to-[#bbf7d0] text-emerald-800 hover:ring-emerald-300",
  "from-[#fef9c3] to-[#fde68a] text-amber-800 hover:ring-amber-300",
  "from-[#fce7f3] to-[#fbcfe8] text-pink-800 hover:ring-pink-300",
  "from-[#ede9fe] to-[#ddd6fe] text-violet-800 hover:ring-violet-300",
  "from-[#ffedd5] to-[#fed7aa] text-orange-800 hover:ring-orange-300",
];

interface CategoryPillProps {
  name: string;
  slug: string;
  index: number;
}

function CategoryPill({ name, slug, index }: CategoryPillProps) {
  const colorClass = CATEGORY_COLORS[index % CATEGORY_COLORS.length];
  return (
    <Link
      to={`/products?categorySlug=${slug}`}
      className={`flex items-center justify-center rounded-2xl bg-gradient-to-br px-4 py-5 text-center text-sm font-bold ring-1 ring-transparent transition hover:ring-2 lg:py-6 ${colorClass}`}
    >
      {name}
    </Link>
  );
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

  const featuredProductsQuery = useQuery({
    queryKey: ["storefront", "products", "featured"],
    queryFn: () => storefrontApi.getProducts({ pageNumber: 1, pageSize: 8 }),
  });

  const featuredProducts = featuredProductsQuery.data?.items ?? [];

  return (
    <div className="flex flex-col gap-8 pb-8">
      <section aria-label="Hero banners">
        <BannerCarousel
          banners={bannersQuery.data ?? []}
          isLoading={bannersQuery.isLoading}
        />
        {bannersQuery.isError && (
          <p className="mt-2 rounded-xl bg-red-50 px-4 py-2 text-sm font-semibold text-red-600">
            {getApiErrorMessage(bannersQuery.error)}
          </p>
        )}
      </section>

      <section aria-label="Danh mục nổi bật">
        <SectionHeader
          eyebrow="Danh mục"
          title="Khám phá theo nhóm sản phẩm"
          linkTo="/products"
          linkLabel="Tất cả sản phẩm"
        />

        <div className="mt-5 grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-6">
          {categoriesQuery.isLoading && (
            Array.from({ length: 6 }).map((_, i) => <CategorySkeleton key={i} />)
          )}
          {categoriesQuery.isError && (
            <div className="col-span-full rounded-2xl bg-red-50 px-5 py-4 text-sm font-semibold text-red-600">
              {getApiErrorMessage(categoriesQuery.error)}
            </div>
          )}
          {!categoriesQuery.isLoading && !categoriesQuery.isError && categoriesQuery.data?.length === 0 && (
            <div className="col-span-full rounded-2xl bg-slate-50 px-5 py-4 text-sm text-slate-500">
              Chưa có danh mục nào.
            </div>
          )}
          {categoriesQuery.data?.map((cat, i) => (
            <CategoryPill key={cat.id} name={cat.name} slug={cat.slug} index={i} />
          ))}
        </div>
      </section>

      <section aria-label="Sản phẩm nổi bật">
        <SectionHeader
          eyebrow="Sản phẩm"
          title="Sản phẩm nổi bật"
          linkTo="/products"
          linkLabel="Xem tất cả"
        />

        {featuredProductsQuery.isError && (
          <div className="mt-5 rounded-2xl bg-red-50 px-5 py-4 text-sm font-semibold text-red-600">
            {getApiErrorMessage(featuredProductsQuery.error)}
          </div>
        )}

        <div className="mt-5 grid grid-cols-1 gap-5 sm:grid-cols-2 lg:grid-cols-4">
          {featuredProductsQuery.isLoading &&
            Array.from({ length: 8 }).map((_, i) => <ProductSkeleton key={i} />)
          }
          {!featuredProductsQuery.isLoading && !featuredProductsQuery.isError && featuredProducts.length === 0 && (
            <div className="col-span-full rounded-2xl bg-slate-50 px-5 py-8 text-center text-sm text-slate-500">
              <p className="font-semibold">Chưa có sản phẩm nào.</p>
              <p className="mt-1">Hãy thêm sản phẩm trong Admin Portal và kích hoạt chúng.</p>
            </div>
          )}
          {featuredProducts.map((product) => (
            <ProductCard key={product.id} product={product} />
          ))}
        </div>
      </section>

      <section
        aria-label="Call to action"
        className="flex flex-col items-center gap-4 rounded-[2rem] bg-gradient-to-br from-[#0f9f7a] to-[#0d8569] px-8 py-10 text-center text-white shadow-md lg:flex-row lg:justify-between lg:text-left"
      >
        <div>
          <h2 className="text-2xl font-black tracking-tight lg:text-3xl">
            Bạn đã sẵn sàng thiết lập góc làm việc lý tưởng?
          </h2>
          <p className="mt-2 text-sm text-white/80">
            Khám phá bàn, ghế, và phụ kiện được chọn lọc kỹ càng cho không gian làm việc tập trung.
          </p>
        </div>
        <div className="flex shrink-0 flex-wrap justify-center gap-3 lg:justify-end">
          <Link
            to="/products"
            id="cta-browse-products"
            className="rounded-full bg-white px-7 py-3 text-sm font-bold text-[var(--brand)] shadow transition hover:bg-slate-50"
          >
            Xem sản phẩm
          </Link>
          <Link
            to="/cart"
            id="cta-view-cart"
            className="rounded-full border border-white/30 px-7 py-3 text-sm font-bold text-white transition hover:bg-white/10"
          >
            Giỏ hàng
          </Link>
        </div>
      </section>
    </div>
  );
}
