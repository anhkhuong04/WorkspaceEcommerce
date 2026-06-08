import { useQuery } from "@tanstack/react-query";
import { formatMoney } from "@workspace-ecommerce/shared-utils";
import { Link } from "react-router-dom";
import { PageHeader } from "../../components/ui/PageHeader";
import { getApiErrorMessage } from "../../services/api/errors";
import { storefrontApi } from "../../services/api/storefrontApi";

export function HomePage() {
  const categoriesQuery = useQuery({
    queryKey: ["storefront", "categories"],
    queryFn: storefrontApi.getCategories
  });
  const featuredProductsQuery = useQuery({
    queryKey: ["storefront", "products", "featured"],
    queryFn: () => storefrontApi.getProducts({ pageNumber: 1, pageSize: 4 })
  });

  const heroProduct = featuredProductsQuery.data?.items[0];

  return (
    <div className="grid gap-6">
      <PageHeader
        eyebrow="Storefront"
        title="Build a workspace that feels precise, calm, and ready for deep work."
        description="Home is now backed by live catalog APIs: categories and featured products come from the running backend."
      />

      <section className="grid gap-6 lg:grid-cols-[1.35fr_0.65fr]">
        <div className="overflow-hidden rounded-[2rem] bg-[linear-gradient(135deg,#fefefe,#e2f7ef)] shadow-sm ring-1 ring-slate-200">
          <div className="grid min-h-[420px] gap-8 p-8 lg:grid-cols-[1fr_0.85fr]">
            <div className="flex flex-col justify-between">
              <div>
                <p className="text-sm font-bold uppercase tracking-[0.2em] text-emerald-700">Live catalog</p>
                <h2 className="mt-8 max-w-2xl text-5xl font-black tracking-tight text-slate-950">
                  Ergonomic essentials for focused workdays.
                </h2>
                <p className="mt-5 max-w-xl text-base leading-7 text-slate-600">
                  Browse active products, check stock, select variants, and add items to the real backend cart.
                </p>
              </div>
              <div className="mt-8 flex flex-wrap gap-3">
                <Link
                  to="/products"
                  className="inline-flex rounded-full bg-slate-950 px-6 py-3 text-sm font-bold text-white transition hover:bg-slate-800"
                >
                  Browse products
                </Link>
                <Link
                  to="/cart"
                  className="inline-flex rounded-full border border-slate-300 bg-white px-6 py-3 text-sm font-bold text-slate-900 transition hover:border-slate-400"
                >
                  View cart
                </Link>
              </div>
            </div>

            <div className="relative rounded-[1.75rem] border border-white/80 bg-white/75 p-5 shadow-sm">
              {heroProduct?.imageUrl ? (
                <img
                  src={heroProduct.imageUrl}
                  alt={heroProduct.name}
                  className="h-72 w-full rounded-[1.25rem] object-cover"
                />
              ) : (
                <div className="h-72 rounded-[1.25rem] bg-[radial-gradient(circle_at_top_left,#9ee6cf,#f8fafc_55%,#dbeafe)]" />
              )}
              <div className="mt-5">
                <p className="text-xs font-bold uppercase tracking-[0.18em] text-slate-500">Featured pick</p>
                <h3 className="mt-2 text-2xl font-black text-slate-950">{heroProduct?.name ?? "Featured products loading"}</h3>
                <p className="mt-2 text-sm leading-6 text-slate-500">
                  {heroProduct?.description ?? "Start the API and seed demo data to render the featured product card."}
                </p>
                {heroProduct && (
                  <Link to={`/products/${heroProduct.slug}`} className="mt-4 inline-flex text-sm font-black text-[var(--brand)]">
                    View detail
                  </Link>
                )}
              </div>
            </div>
          </div>
        </div>

        <div className="grid gap-3 rounded-[2rem] border border-slate-200 bg-white p-6 shadow-sm">
          <p className="text-sm font-bold text-slate-500">Featured categories</p>
          {categoriesQuery.isLoading && <div className="rounded-2xl bg-slate-50 px-5 py-4 text-sm text-slate-500">Loading categories...</div>}
          {categoriesQuery.isError && (
            <div className="rounded-2xl bg-red-50 px-5 py-4 text-sm font-semibold text-red-700">
              {getApiErrorMessage(categoriesQuery.error)}
            </div>
          )}
          {categoriesQuery.data?.map((category) => (
            <Link
              key={category.id}
              to={`/products?categorySlug=${category.slug}`}
              className="rounded-2xl border border-slate-100 bg-slate-50 px-5 py-4 font-bold text-slate-800 transition hover:border-emerald-200 hover:bg-emerald-50"
            >
              {category.name}
            </Link>
          ))}
        </div>
      </section>

      <section className="grid gap-4">
        <div className="flex items-end justify-between">
          <div>
            <p className="text-sm font-bold uppercase tracking-[0.18em] text-emerald-700">Products</p>
            <h2 className="mt-2 text-3xl font-black text-slate-950">Featured catalog</h2>
          </div>
          <Link to="/products" className="text-sm font-black text-[var(--brand)]">
            See all
          </Link>
        </div>

        {featuredProductsQuery.isError && (
          <div className="rounded-3xl bg-red-50 p-6 font-semibold text-red-700">
            {getApiErrorMessage(featuredProductsQuery.error)}
          </div>
        )}
        <div className="grid gap-5 md:grid-cols-2 xl:grid-cols-4">
          {featuredProductsQuery.data?.items.map((product) => (
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
              <h3 className="mt-4 text-lg font-black text-slate-950">{product.name}</h3>
              <p className="mt-2 text-sm font-bold text-[var(--brand)]">
                {product.minPrice === null ? "Contact for price" : `From ${formatMoney(product.minPrice)}`}
              </p>
            </Link>
          ))}
        </div>
      </section>
    </div>
  );
}
