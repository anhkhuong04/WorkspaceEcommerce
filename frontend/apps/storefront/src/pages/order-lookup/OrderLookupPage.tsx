import { formatDate, formatMoney, formatOrderStatus, formatPaymentMethod } from "@workspace-ecommerce/shared-utils";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { useSearchParams } from "react-router-dom";
import { z } from "zod";
import { zodResolver } from "@hookform/resolvers/zod";
import type { OrderDto } from "@workspace-ecommerce/api-types";
import { getApiErrorMessage } from "../../services/api/errors";
import { storefrontApi } from "../../services/api/storefrontApi";

// ─── Schema ───────────────────────────────────────────────────────────────────

const lookupSchema = z.object({
  orderCode: z.string().min(1, "Vui lòng nhập mã đơn hàng"),
  phone: z.string().min(8, "Số điện thoại không hợp lệ")
});

type LookupFormValues = z.infer<typeof lookupSchema>;

// ─── Status config ────────────────────────────────────────────────────────────

const statusStyles: Record<number, string> = {
  0: "bg-yellow-100 text-yellow-800",  // Pending
  1: "bg-blue-100 text-blue-800",      // Confirmed
  2: "bg-indigo-100 text-indigo-800",  // Processing
  3: "bg-violet-100 text-violet-800",  // Shipping
  4: "bg-emerald-100 text-emerald-800", // Completed
  5: "bg-red-100 text-red-800",        // FailedDelivery
  6: "bg-slate-100 text-slate-600"     // Cancelled
};

// ─── OrderLookupPage ──────────────────────────────────────────────────────────

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
      {/* ── Page header ───────────────────────────────────────────── */}
      <section className="rounded-[2rem] border border-slate-200 bg-white p-8 shadow-sm">
        <p className="text-sm font-bold uppercase tracking-[0.2em] text-[var(--brand)]">Tra cứu đơn hàng</p>
        <div className="mt-3 grid gap-4 lg:grid-cols-[1fr_420px] lg:items-end">
          <h1 className="max-w-4xl text-5xl font-black tracking-tight text-slate-950">
            Kiểm tra trạng thái đơn hàng
          </h1>
          <p className="text-base leading-7 text-slate-600">
            Nhập mã đơn hàng và số điện thoại đặt hàng để tra cứu.
          </p>
        </div>
      </section>

      {/* ── Search form ───────────────────────────────────────────── */}
      <section className="rounded-[2rem] border border-slate-200 bg-white p-8 shadow-sm">
        <form
          onSubmit={handleSubmit(onSubmit)}
          className="grid gap-4 sm:grid-cols-[1fr_1fr_auto] sm:items-end"
        >
          <label className="grid gap-1">
            <span className="text-sm font-bold text-slate-700">
              Mã đơn hàng <span className="text-red-500">*</span>
            </span>
            <input
              placeholder="ORD-DEMO-PENDING"
              className={`h-11 rounded-2xl border px-4 font-mono font-bold outline-none transition focus:ring-2 focus:ring-[var(--brand)]/30 ${
                errors.orderCode
                  ? "border-red-400 bg-red-50"
                  : "border-slate-200 bg-white focus:border-[var(--brand)]"
              }`}
              {...register("orderCode")}
            />
            {errors.orderCode && (
              <p className="text-xs font-semibold text-red-600">{errors.orderCode.message}</p>
            )}
          </label>

          <label className="grid gap-1">
            <span className="text-sm font-bold text-slate-700">
              Số điện thoại <span className="text-red-500">*</span>
            </span>
            <input
              type="tel"
              placeholder="0900 000 000"
              className={`h-11 rounded-2xl border px-4 font-bold outline-none transition focus:ring-2 focus:ring-[var(--brand)]/30 ${
                errors.phone
                  ? "border-red-400 bg-red-50"
                  : "border-slate-200 bg-white focus:border-[var(--brand)]"
              }`}
              {...register("phone")}
            />
            {errors.phone && (
              <p className="text-xs font-semibold text-red-600">{errors.phone.message}</p>
            )}
          </label>

          <button
            type="submit"
            disabled={isLoading}
            className="h-11 shrink-0 rounded-full bg-[var(--brand)] px-6 text-sm font-black text-white transition hover:brightness-110 disabled:cursor-not-allowed disabled:opacity-60"
          >
            {isLoading ? "Đang tìm…" : "Tra cứu"}
          </button>
        </form>

        {/* API error */}
        {lookupError && (
          <div className="mt-4 rounded-2xl bg-red-50 px-5 py-4 text-sm font-semibold text-red-700">
            {lookupError}
          </div>
        )}
      </section>

      {/* ── Result ────────────────────────────────────────────────── */}
      {result && (
        <section className="rounded-[2rem] border border-slate-200 bg-white p-6 shadow-sm">
          {/* Order header */}
          <div className="flex flex-wrap items-start justify-between gap-3 border-b border-slate-100 pb-5">
            <div>
              <p className="font-mono text-2xl font-black text-slate-950">{result.orderCode}</p>
              <p className="mt-1 text-sm text-slate-500">
                Đặt ngày {formatDate(result.createdAt)}
              </p>
            </div>
            <span
              className={`rounded-full px-3 py-1 text-sm font-black ${statusStyles[result.status] ?? "bg-slate-100 text-slate-600"}`}
            >
              {formatOrderStatus(result.status)}
            </span>
          </div>

          <div className="mt-5 grid gap-6 lg:grid-cols-[1fr_320px]">
            {/* Items */}
            <div>
              <h2 className="text-base font-black text-slate-950">Sản phẩm</h2>

              <div className="mt-3 overflow-x-auto">
                <table className="w-full text-sm">
                  <thead>
                    <tr className="border-b border-slate-100 text-left text-xs font-bold uppercase tracking-[0.14em] text-slate-400">
                      <th className="pb-2 pr-4">Sản phẩm</th>
                      <th className="pb-2 pr-4 text-center">SL</th>
                      <th className="pb-2 pr-4 text-right">Đơn giá</th>
                      <th className="pb-2 text-right">Thành tiền</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-100">
                    {result.items.map((item) => (
                      <tr key={item.id}>
                        <td className="py-3 pr-4">
                          <p className="font-bold text-slate-950">{item.productNameSnapshot}</p>
                          <p className="mt-0.5 font-mono text-xs text-slate-400">{item.skuSnapshot}</p>
                          {item.requiresInstallation && (
                            <span className="mt-1 inline-block rounded-full bg-amber-100 px-2 py-0.5 text-xs font-bold text-amber-800">
                              Lắp đặt
                            </span>
                          )}
                        </td>
                        <td className="py-3 pr-4 text-center font-bold text-slate-700">{item.quantity}</td>
                        <td className="py-3 pr-4 text-right font-bold text-slate-700">{formatMoney(item.unitPrice)}</td>
                        <td className="py-3 text-right font-black text-slate-950">{formatMoney(item.lineTotal)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>

              {/* Totals */}
              <div className="mt-4 grid gap-2 border-t border-slate-200 pt-4">
                <div className="flex justify-between text-sm font-bold text-slate-500">
                  <span>Tạm tính</span>
                  <span>{formatMoney(result.subtotal)}</span>
                </div>
                {result.shippingFee > 0 && (
                  <div className="flex justify-between text-sm font-bold text-slate-500">
                    <span>Phí vận chuyển</span>
                    <span>{formatMoney(result.shippingFee)}</span>
                  </div>
                )}
                {result.discountAmount > 0 && (
                  <div className="flex justify-between text-sm font-bold text-emerald-600">
                    <span>Giảm giá</span>
                    <span>−{formatMoney(result.discountAmount)}</span>
                  </div>
                )}
                <div className="flex justify-between text-xl font-black text-slate-950">
                  <span>Tổng cộng</span>
                  <span className="text-[var(--brand)]">{formatMoney(result.totalAmount)}</span>
                </div>
              </div>
            </div>

            {/* Info sidebar */}
            <aside className="grid h-fit gap-3 rounded-[1.5rem] border border-slate-100 bg-slate-50 p-5">
              <h2 className="text-sm font-bold uppercase tracking-[0.18em] text-[var(--brand)]">
                Thông tin người nhận
              </h2>
              <SideInfoRow label="Họ tên" value={result.customerName} />
              <SideInfoRow label="Điện thoại" value={result.customerPhone} />
              {result.customerEmail && (
                <SideInfoRow label="Email" value={result.customerEmail} />
              )}
              <SideInfoRow label="Địa chỉ giao" value={result.shippingAddress} />
              {result.note && (
                <SideInfoRow label="Ghi chú" value={result.note} />
              )}
              <div className="mt-2 border-t border-slate-200 pt-3">
                <SideInfoRow label="Thanh toán" value={formatPaymentMethod(result.paymentMethod)} />
              </div>
            </aside>
          </div>
        </section>
      )}
    </div>
  );
}

// ─── SideInfoRow helper ───────────────────────────────────────────────────────

function SideInfoRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="grid gap-0.5">
      <span className="text-xs font-bold uppercase tracking-[0.14em] text-slate-400">{label}</span>
      <span className="text-sm font-bold text-slate-800">{value}</span>
    </div>
  );
}
