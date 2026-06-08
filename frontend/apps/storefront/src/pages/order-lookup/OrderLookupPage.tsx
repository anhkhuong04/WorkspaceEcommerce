import { useForm } from "react-hook-form";
import { PageHeader } from "../../components/ui/PageHeader";

interface LookupFormValues {
  orderCode: string;
  phone: string;
}

export function OrderLookupPage() {
  const form = useForm<LookupFormValues>({
    defaultValues: {
      orderCode: "ORD-DEMO-PENDING",
      phone: "0900000001"
    }
  });

  return (
    <div className="grid gap-6">
      <PageHeader eyebrow="Order lookup" title="Lookup by order code and phone." description="Prepared for /api/orders/lookup with clear empty and error states." />
      <form className="grid max-w-2xl gap-4 rounded-[2rem] border border-slate-200 bg-white p-8 shadow-sm">
        <label className="grid gap-2 text-sm font-bold text-slate-700">
          Order code
          <input className="rounded-2xl border border-slate-200 px-4 py-3 outline-none focus:border-[var(--brand)]" {...form.register("orderCode")} />
        </label>
        <label className="grid gap-2 text-sm font-bold text-slate-700">
          Phone
          <input className="rounded-2xl border border-slate-200 px-4 py-3 outline-none focus:border-[var(--brand)]" {...form.register("phone")} />
        </label>
      </form>
    </div>
  );
}
