import { useQuery } from "@tanstack/react-query";
import { formatMoney } from "@workspace-ecommerce/shared-utils";
import { Link } from "react-router-dom";
import { PageHeader } from "../../components/ui/PageHeader";
import { storefrontApi } from "../../services/api/storefrontApi";

export function ProductListPage() {
  const productsQuery = useQuery({
    queryKey: ["products"],
    queryFn: () => storefrontApi.getProducts({ pageNumber: 1, pageSize: 12 })
  });

  return (
    <div className="grid gap-6">
      <PageHeader
        eyebrow="Catalog"
        title="Products with space for filters, cards, loading, empty, and error states."
        description="This page already uses the shared API client. When the backend is running, it renders live product data from /api/products."
      />
      {productsQuery.isLoading && <div className="rounded-3xl bg-white p-8 text-slate-500 shadow-sm">Loading products...</div>}
      {productsQuery.isError && <div className="rounded-3xl bg-red-50 p-8 font-semibold text-red-700">Could not load products.</div>}
      {productsQuery.data && productsQuery.data.items.length === 0 && (
        <div className="rounded-3xl bg-white p-8 text-slate-500 shadow-sm">No active products found.</div>
      )}
      <div className="grid gap-5 md:grid-cols-2 xl:grid-cols-4">
        {productsQuery.data?.items.map((product) => (
          <Link key={product.id} to={`/products/${product.slug}`} className="rounded-[1.5rem] border border-slate-200 bg-white p-5 shadow-sm transition hover:-translate-y-1 hover:shadow-md">
            <div className="aspect-[4/3] rounded-2xl bg-slate-100" />
            <h2 className="mt-4 text-lg font-black text-slate-950">{product.name}</h2>
            <p className="mt-2 line-clamp-2 text-sm leading-6 text-slate-500">{product.description ?? "No description."}</p>
            <p className="mt-4 text-sm font-bold text-[var(--brand)]">
              {product.minPrice === null ? "Contact for price" : `From ${formatMoney(product.minPrice)}`}
            </p>
          </Link>
        ))}
      </div>
    </div>
  );
}
