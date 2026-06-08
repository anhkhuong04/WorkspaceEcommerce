import { Link } from "react-router-dom";
import { PageHeader } from "../../components/ui/PageHeader";

const categories = ["Standing Desks", "Ergonomic Chairs", "Desk Accessories"];

export function HomePage() {
  return (
    <div className="grid gap-6">
      <PageHeader
        eyebrow="Storefront"
        title="Build a workspace that feels precise, calm, and ready for deep work."
        description="Frontend foundation for the public shopping flow. It is prepared for real Catalog, Banner, Cart, Checkout, and Order Lookup API integration."
      />
      <section className="grid gap-6 lg:grid-cols-[1.3fr_0.7fr]">
        <div className="rounded-[2rem] bg-[linear-gradient(135deg,#fefefe,#e8f6f2)] p-8 shadow-sm ring-1 ring-slate-200">
          <p className="text-sm font-bold uppercase tracking-[0.2em] text-emerald-700">Demo ready</p>
          <h2 className="mt-8 max-w-2xl text-4xl font-black tracking-tight text-slate-950">
            Seeded products and banners can power this page once API integration starts.
          </h2>
          <Link
            to="/products"
            className="mt-8 inline-flex rounded-full bg-slate-950 px-6 py-3 text-sm font-bold text-white transition hover:bg-slate-800"
          >
            Browse products
          </Link>
        </div>
        <div className="grid gap-3 rounded-[2rem] border border-slate-200 bg-white p-6 shadow-sm">
          <p className="text-sm font-bold text-slate-500">Featured categories</p>
          {categories.map((category) => (
            <div key={category} className="rounded-2xl border border-slate-100 bg-slate-50 px-5 py-4 font-bold text-slate-800">
              {category}
            </div>
          ))}
        </div>
      </section>
    </div>
  );
}
