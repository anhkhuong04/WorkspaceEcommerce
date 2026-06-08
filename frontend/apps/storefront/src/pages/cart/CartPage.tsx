import { PageHeader } from "../../components/ui/PageHeader";

export function CartPage() {
  return (
    <div className="grid gap-6">
      <PageHeader
        eyebrow="Cart"
        title="Cart foundation for quantity updates, totals, and checkout handoff."
        description="Use session id demo-checkout-session after running demo seed to smoke-test the backend checkout flow."
      />
      <section className="rounded-[2rem] border border-slate-200 bg-white p-8 shadow-sm">
        <div className="rounded-2xl bg-slate-50 p-6 text-slate-600">Cart UI shell ready for API-bound line items.</div>
      </section>
    </div>
  );
}
