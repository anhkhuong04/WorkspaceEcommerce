import { useQuery } from "@tanstack/react-query";
import { formatMoney } from "@workspace-ecommerce/shared-utils";
import { useParams } from "react-router-dom";
import { PageHeader } from "../../components/ui/PageHeader";
import { storefrontApi } from "../../services/api/storefrontApi";

export function ProductDetailPage() {
  const { slug = "" } = useParams();
  const productQuery = useQuery({
    queryKey: ["product", slug],
    queryFn: () => storefrontApi.getProduct(slug),
    enabled: slug.length > 0
  });

  if (productQuery.isLoading) {
    return <div className="rounded-3xl bg-white p-8 text-slate-500 shadow-sm">Loading product...</div>;
  }

  if (productQuery.isError || !productQuery.data) {
    return <div className="rounded-3xl bg-red-50 p-8 font-semibold text-red-700">Product was not found.</div>;
  }

  const product = productQuery.data;

  return (
    <div className="grid gap-6">
      <PageHeader eyebrow="Product detail" title={product.name} description={product.description ?? "Product detail is ready for gallery, specs, variants, and cart actions."} />
      <section className="grid gap-6 lg:grid-cols-[0.9fr_1.1fr]">
        <div className="aspect-square rounded-[2rem] bg-slate-100 shadow-inner" />
        <div className="rounded-[2rem] border border-slate-200 bg-white p-8 shadow-sm">
          <h2 className="text-2xl font-black text-slate-950">Variants</h2>
          <div className="mt-5 grid gap-3">
            {product.variants.map((variant) => (
              <div key={variant.id} className="flex items-center justify-between rounded-2xl border border-slate-100 bg-slate-50 p-4">
                <div>
                  <p className="font-bold text-slate-950">{variant.name}</p>
                  <p className="text-sm text-slate-500">{variant.sku} | Stock {variant.stockQuantity}</p>
                </div>
                <p className="font-black text-[var(--brand)]">{formatMoney(variant.price)}</p>
              </div>
            ))}
          </div>
        </div>
      </section>
    </div>
  );
}
