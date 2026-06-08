import { formatDate, formatMoney, formatOrderStatus, formatPaymentMethod } from "@workspace-ecommerce/shared-utils";
import { useEffect } from "react";
import { Link, useLocation, useNavigate } from "react-router-dom";
import type { OrderDto } from "@workspace-ecommerce/api-types";

// ─── CheckoutSuccessPage ──────────────────────────────────────────────────────

export function CheckoutSuccessPage() {
  const location = useLocation();
  const navigate = useNavigate();

  const order = location.state?.order as OrderDto | undefined;

  // If navigated directly without order state, bounce home
  useEffect(() => {
    if (!order) {
      void navigate("/", { replace: true });
    }
  }, [order, navigate]);

  if (!order) return null;

  return (
    <div className="grid gap-6">
      {/* ── Success banner ─────────────────────────────────────────── */}
      <section className="flex flex-col items-center gap-4 rounded-[2rem] bg-gradient-to-br from-[#0f9f7a] to-[#0d8569] px-8 py-12 text-center text-white shadow-md">
        <div className="flex h-16 w-16 items-center justify-center rounded-full bg-white/20">
          <svg className="h-9 w-9" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M4.5 12.75l6 6 9-13.5" />
          </svg>
        </div>
        <div>
          <h1 className="text-4xl font-black tracking-tight lg:text-5xl">
            Đặt hàng thành công!
          </h1>
          <p className="mt-3 text-base text-white/80">
            Đơn hàng của bạn đã được ghi nhận. Chúng tôi sẽ liên hệ xác nhận sớm nhất.
          </p>
        </div>
        <div className="mt-2 rounded-full bg-white/20 px-6 py-2 text-lg font-black tracking-widest">
          {order.orderCode}
        </div>
      </section>

      {/* ── Order details ──────────────────────────────────────────── */}
      <div className="grid gap-6 lg:grid-cols-[1fr_360px]">
        {/* Left: order items */}
        <section className="rounded-[2rem] border border-slate-200 bg-white p-6 shadow-sm">
          <h2 className="text-lg font-black text-slate-950">Sản phẩm đã đặt</h2>

          <div className="mt-4 grid gap-3">
            {order.items.map((item) => (
              <div
                key={item.id}
                className="grid gap-1 rounded-2xl border border-slate-100 bg-slate-50 p-4 sm:grid-cols-[1fr_auto]"
              >
                <div>
                  <p className="font-bold text-slate-950">{item.productNameSnapshot}</p>
                  <p className="mt-0.5 font-mono text-xs text-slate-500">{item.skuSnapshot}</p>
                  {item.requiresInstallation && (
                    <span className="mt-1 inline-block rounded-full bg-amber-100 px-2 py-0.5 text-xs font-bold text-amber-800">
                      Yêu cầu lắp đặt
                    </span>
                  )}
                </div>
                <div className="flex flex-col items-end gap-1">
                  <p className="text-sm font-bold text-slate-500">
                    {formatMoney(item.unitPrice)} × {item.quantity}
                  </p>
                  <p className="text-base font-black text-slate-950">
                    {formatMoney(item.lineTotal)}
                  </p>
                </div>
              </div>
            ))}
          </div>

          {/* Totals */}
          <div className="mt-5 grid gap-2 border-t border-slate-200 pt-4">
            <div className="flex justify-between text-sm font-bold text-slate-500">
              <span>Tạm tính</span>
              <span>{formatMoney(order.subtotal)}</span>
            </div>
            {order.shippingFee > 0 && (
              <div className="flex justify-between text-sm font-bold text-slate-500">
                <span>Phí vận chuyển</span>
                <span>{formatMoney(order.shippingFee)}</span>
              </div>
            )}
            {order.discountAmount > 0 && (
              <div className="flex justify-between text-sm font-bold text-emerald-600">
                <span>Giảm giá</span>
                <span>−{formatMoney(order.discountAmount)}</span>
              </div>
            )}
            <div className="flex justify-between text-xl font-black text-slate-950">
              <span>Tổng cộng</span>
              <span className="text-[var(--brand)]">{formatMoney(order.totalAmount)}</span>
            </div>
          </div>
        </section>

        {/* Right: order info + CTAs */}
        <aside className="grid h-fit gap-4">
          <section className="rounded-[2rem] border border-slate-200 bg-white p-6 shadow-sm">
            <h2 className="text-sm font-bold uppercase tracking-[0.18em] text-[var(--brand)]">
              Thông tin đơn hàng
            </h2>
            <div className="mt-4 grid gap-3 text-sm">
              <InfoRow label="Mã đơn" value={order.orderCode} mono />
              <InfoRow label="Trạng thái" value={formatOrderStatus(order.status)} />
              <InfoRow label="Ngày đặt" value={formatDate(order.createdAt)} />
              <InfoRow label="Thanh toán" value={formatPaymentMethod(order.paymentMethod)} />
              <InfoRow label="Người nhận" value={order.customerName} />
              <InfoRow label="Điện thoại" value={order.customerPhone} />
              {order.customerEmail && (
                <InfoRow label="Email" value={order.customerEmail} />
              )}
              <InfoRow label="Địa chỉ" value={order.shippingAddress} />
              {order.note && (
                <InfoRow label="Ghi chú" value={order.note} />
              )}
            </div>
          </section>

          <Link
            to={`/orders/lookup?orderCode=${encodeURIComponent(order.orderCode)}&phone=${encodeURIComponent(order.customerPhone)}`}
            className="flex h-12 items-center justify-center rounded-full border border-[var(--brand)] text-sm font-black text-[var(--brand)] transition hover:bg-[var(--brand-soft)]"
          >
            Tra cứu đơn hàng
          </Link>

          <Link
            to="/products"
            className="flex h-12 items-center justify-center rounded-full bg-slate-950 text-sm font-black text-white transition hover:bg-slate-800"
          >
            Tiếp tục mua sắm
          </Link>
        </aside>
      </div>
    </div>
  );
}

// ─── InfoRow helper ───────────────────────────────────────────────────────────

function InfoRow({ label, value, mono = false }: { label: string; value: string; mono?: boolean }) {
  return (
    <div className="flex flex-col gap-0.5">
      <span className="text-xs font-bold uppercase tracking-[0.14em] text-slate-400">{label}</span>
      <span className={`font-bold text-slate-800 ${mono ? "font-mono" : ""}`}>{value}</span>
    </div>
  );
}
