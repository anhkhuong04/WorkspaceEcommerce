import { zodResolver } from "@hookform/resolvers/zod";
import type { OrderDto } from "@workspace-ecommerce/api-types";
import { formatDate, formatMoney, formatOrderStatus, formatPaymentMethod } from "@workspace-ecommerce/shared-utils";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { useSearchParams } from "react-router-dom";
import { z } from "zod";
import { PageHeader } from "../../components/ui/PageHeader";
import { getApiErrorMessage } from "../../services/api/errors";
import { storefrontApi } from "../../services/api/storefrontApi";

const lookupSchema = z.object({
  orderCode: z.string().min(1, "Order code is required."),
  phone: z.string().min(8, "Phone number is invalid.")
});

type LookupFormValues = z.infer<typeof lookupSchema>;

const statusStyles: Record<number, string> = {
  0: "bg-yellow-100 text-yellow-800",
  1: "bg-blue-100 text-blue-800",
  2: "bg-indigo-100 text-indigo-800",
  3: "bg-violet-100 text-violet-800",
  4: "bg-emerald-100 text-emerald-800",
  5: "bg-red-100 text-red-800",
  6: "bg-slate-100 text-slate-600"
};

export function OrderLookupPage() {
  const [searchParams] = useSearchParams();
  const [result, setResult] = useState<OrderDto | null>(null);
  const [lookupError, setLookupError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);

  const {
    register,
    handleSubmit,
    formState: { errors }
  } = useForm<LookupFormValues>({
    resolver: zodResolver(lookupSchema),
    defaultValues: {
      orderCode: searchParams.get("orderCode") ?? "",
      phone: searchParams.get("phone") ?? ""
    }
  });

  async function onSubmit(values: LookupFormValues) {
    setIsLoading(true);
    setLookupError(null);
    setResult(null);

    try {
      const response = await storefrontApi.lookupOrder(values);
      setResult(response.order);
    } catch (error) {
      setLookupError(getApiErrorMessage(error));
    } finally {
      setIsLoading(false);
    }
  }

  return (
    <div className="grid gap-6">
      <PageHeader
        eyebrow="Order lookup"
        title="Check your order status"
        description="Enter your order code and phone number to view the latest order details."
      />

      <section className="ui-card border border-slate-100 p-8">
        <form onSubmit={handleSubmit(onSubmit)} className="grid gap-4 sm:grid-cols-[1fr_1fr_auto] sm:items-end">
          <label className="grid gap-1">
            <span className="ui-control text-slate-700">Order code <span className="text-red-500">*</span></span>
            <input
              placeholder="ORD-DEMO-PENDING"
              className={`ui-control h-11 rounded-[var(--radius-control)] border px-4 font-mono outline-none transition focus:ring-2 focus:ring-[var(--brand)]/30 ${errors.orderCode ? "border-red-400 bg-red-50" : "border-slate-200 bg-white focus:border-[var(--brand)]"}`}
              {...register("orderCode")}
            />
            {errors.orderCode && <p className="ui-caption text-red-600">{errors.orderCode.message}</p>}
          </label>

          <label className="grid gap-1">
            <span className="ui-control text-slate-700">Phone number <span className="text-red-500">*</span></span>
            <input
              type="tel"
              placeholder="0900 000 000"
              className={`ui-control h-11 rounded-[var(--radius-control)] border px-4 outline-none transition focus:ring-2 focus:ring-[var(--brand)]/30 ${errors.phone ? "border-red-400 bg-red-50" : "border-slate-200 bg-white focus:border-[var(--brand)]"}`}
              {...register("phone")}
            />
            {errors.phone && <p className="ui-caption text-red-600">{errors.phone.message}</p>}
          </label>

          <button type="submit" disabled={isLoading} className="ui-control h-11 shrink-0 rounded-[var(--radius-control)] bg-[var(--brand)] px-6 text-white transition hover:brightness-110 disabled:cursor-not-allowed disabled:opacity-60">
            {isLoading ? "Searching..." : "Look up"}
          </button>
        </form>

        {lookupError && <div className="ui-control mt-4 rounded-[var(--radius-card)] bg-red-50 px-5 py-4 text-red-700">{lookupError}</div>}
      </section>

      {result && (
        <section className="ui-card border border-slate-100 p-6">
          <div className="flex flex-wrap items-start justify-between gap-3 border-b border-slate-100 pb-5">
            <div>
              <p className="font-mono text-2xl font-black text-slate-950">{result.orderCode}</p>
              <p className="ui-body mt-1 text-slate-500">Created on {formatDate(result.createdAt)}</p>
            </div>
            <span className={`rounded-full px-3 py-1 text-sm font-black ${statusStyles[result.status] ?? "bg-slate-100 text-slate-600"}`}>{formatOrderStatus(result.status)}</span>
          </div>

          <div className="mt-5 grid gap-6 lg:grid-cols-[1fr_320px]">
            <div>
              <h2 className="ui-h3 text-slate-950">Items</h2>
              <div className="mt-3 overflow-x-auto">
                <table className="w-full text-sm">
                  <thead>
                    <tr className="border-b border-slate-100 text-left text-xs font-bold uppercase tracking-[0.14em] text-slate-400">
                      <th className="pb-2 pr-4">Product</th>
                      <th className="pb-2 pr-4 text-center">Qty</th>
                      <th className="pb-2 pr-4 text-right">Unit price</th>
                      <th className="pb-2 text-right">Line total</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-100">
                    {result.items.map((item) => (
                      <tr key={item.id}>
                        <td className="py-3 pr-4">
                          <p className="font-bold text-slate-950">{item.productNameSnapshot}</p>
                          <p className="mt-0.5 font-mono text-xs text-slate-400">{item.skuSnapshot}</p>
                          {item.requiresInstallation && <span className="ui-caption mt-1 inline-block rounded-full bg-amber-100 px-2 py-0.5 text-amber-800">Installation required</span>}
                        </td>
                        <td className="py-3 pr-4 text-center font-bold text-slate-700">{item.quantity}</td>
                        <td className="py-3 pr-4 text-right font-bold text-slate-700">{formatMoney(item.unitPrice)}</td>
                        <td className="py-3 text-right font-black text-slate-950">{formatMoney(item.lineTotal)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>

              <div className="mt-4 grid gap-2 border-t border-slate-200 pt-4">
                <div className="flex justify-between text-sm font-bold text-slate-500"><span>Subtotal</span><span>{formatMoney(result.subtotal)}</span></div>
                {result.shippingFee > 0 && <div className="flex justify-between text-sm font-bold text-slate-500"><span>Shipping fee</span><span>{formatMoney(result.shippingFee)}</span></div>}
                {result.discountAmount > 0 && <div className="flex justify-between text-sm font-bold text-emerald-600"><span>{formatDiscountLabel(result)}</span><span>-{formatMoney(result.discountAmount)}</span></div>}
                <div className="flex justify-between text-xl font-black text-slate-950"><span>Total</span><span className="text-[var(--brand)]">{formatMoney(result.totalAmount)}</span></div>
              </div>
            </div>

            <aside className="grid h-fit gap-3 rounded-[var(--radius-card)] border border-slate-100 bg-slate-50 p-5">
              <h2 className="ui-caption uppercase tracking-[0.18em] text-[var(--brand)]">Recipient details</h2>
              <SideInfoRow label="Full name" value={result.customerName} />
              <SideInfoRow label="Phone" value={result.customerPhone} />
              {result.customerEmail && <SideInfoRow label="Email" value={result.customerEmail} />}
              <SideInfoRow label="Shipping address" value={result.shippingAddress} />
              {result.note && <SideInfoRow label="Note" value={result.note} />}
              {result.couponCodeSnapshot && <SideInfoRow label="Coupon" value={formatCouponSnapshot(result)} />}
              <div className="mt-2 border-t border-slate-200 pt-3"><SideInfoRow label="Payment" value={formatPaymentMethod(result.paymentMethod)} /></div>
            </aside>
          </div>
        </section>
      )}
    </div>
  );
}

function SideInfoRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="grid gap-0.5">
      <span className="ui-caption uppercase tracking-[0.14em] text-slate-400">{label}</span>
      <span className="text-sm font-bold text-slate-800">{value}</span>
    </div>
  );
}

function formatDiscountLabel(order: OrderDto): string {
  return order.couponCodeSnapshot ? `Discount (${order.couponCodeSnapshot})` : "Discount";
}

function formatCouponSnapshot(order: OrderDto): string {
  return order.couponNameSnapshot ? `${order.couponCodeSnapshot} - ${order.couponNameSnapshot}` : order.couponCodeSnapshot ?? "";
}
