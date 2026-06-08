import { useForm } from "react-hook-form";
import { z } from "zod";
import { PageHeader } from "../../components/ui/PageHeader";

const checkoutSchema = z.object({
  sessionId: z.string().min(1),
  customerName: z.string().min(2),
  customerPhone: z.string().min(8),
  shippingAddress: z.string().min(5)
});

type CheckoutFormValues = z.infer<typeof checkoutSchema>;

export function CheckoutPage() {
  const form = useForm<CheckoutFormValues>({
    defaultValues: {
      sessionId: "demo-checkout-session",
      customerName: "",
      customerPhone: "",
      shippingAddress: ""
    }
  });

  function handlePreview(values: CheckoutFormValues) {
    checkoutSchema.parse(values);
  }

  return (
    <div className="grid gap-6">
      <PageHeader eyebrow="Checkout" title="Checkout form shell with explicit defaults and client-side UX validation." description="Backend remains authoritative. This page is ready for integration with /api/checkout." />
      <form
        className="grid max-w-3xl gap-4 rounded-[2rem] border border-slate-200 bg-white p-8 shadow-sm"
        onSubmit={form.handleSubmit(handlePreview)}
      >
        {(["sessionId", "customerName", "customerPhone", "shippingAddress"] as const).map((field) => (
          <label key={field} className="grid gap-2 text-sm font-bold text-slate-700">
            {field}
            <input className="rounded-2xl border border-slate-200 px-4 py-3 font-medium outline-none focus:border-[var(--brand)]" {...form.register(field)} />
          </label>
        ))}
        <button type="submit" className="rounded-full bg-slate-950 px-5 py-3 text-sm font-bold text-white">
          Validate checkout details
        </button>
      </form>
    </div>
  );
}
