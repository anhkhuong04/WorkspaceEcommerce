import { useQuery } from "@tanstack/react-query";
import type { OrderDto } from "@workspace-ecommerce/api-types";
import { formatDate, formatMoney, formatOrderStatus, formatPaymentMethod } from "@workspace-ecommerce/shared-utils";
import type { ReactNode } from "react";
import { useState } from "react";
import { Link, useLocation, useSearchParams } from "react-router-dom";
import { getManualTransferContent } from "../../features/checkout/manualTransfer";
import { getApiErrorMessage } from "../../services/api/errors";
import { storefrontApi } from "../../services/api/storefrontApi";

export function CheckoutSuccessPage() {
  const location = useLocation();
  const [searchParams] = useSearchParams();
  const stateOrder = getStateOrder(location.state);
  const orderCode = searchParams.get("orderCode") ?? stateOrder?.orderCode ?? "";
  const phone = searchParams.get("phone") ?? stateOrder?.customerPhone ?? "";

  const lookupQuery = useQuery({
    queryKey: ["checkout", "success", "lookup", orderCode, phone],
    queryFn: () => storefrontApi.lookupOrder({ orderCode, phone }).then((response) => response.order),
    enabled: !stateOrder && orderCode.length > 0 && phone.length > 0,
    retry: false
  });

  const order = stateOrder ?? lookupQuery.data;

  if (!order && lookupQuery.isLoading) {
    return <StateMessage>Loading order...</StateMessage>;
  }

  if (!order && lookupQuery.error) {
    return (
      <StateMessage tone="error">
        {getApiErrorMessage(lookupQuery.error)} - <Link to="/orders/lookup" className="underline">Open order lookup</Link>
      </StateMessage>
    );
  }

  if (!order) {
    return (
      <StateMessage tone="error">
        Missing order details. <Link to="/orders/lookup" className="underline">Open order lookup</Link>
      </StateMessage>
    );
  }

  return (
    <div className="grid gap-6">
      <section className="flex flex-col items-center gap-4 rounded-[var(--radius-card)] bg-gradient-to-br from-[#111111] to-[#2b2b2b] px-8 py-12 text-center text-white shadow-[var(--shadow-card)]">
        <div className="flex h-16 w-16 items-center justify-center rounded-full bg-white/20">
          <svg className="h-9 w-9" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M4.5 12.75l6 6 9-13.5" />
          </svg>
        </div>
        <div>
          <h1 className="ui-h1 tracking-tight">Order placed successfully.</h1>
          <p className="ui-body mt-3 text-white/80">Your order has been recorded. We will contact you soon to confirm it.</p>
        </div>
        <div className="ui-control mt-2 rounded-full bg-white/20 px-6 py-2 tracking-widest">{order.orderCode}</div>
      </section>

      <div className="grid gap-6 lg:grid-cols-[1fr_360px]">
        <section className="ui-card border border-slate-100 p-6">
          <h2 className="ui-h2 text-slate-950">Ordered items</h2>

          <div className="mt-4 grid gap-3">
            {order.items.map((item) => (
              <div key={item.id} className="grid gap-1 rounded-[var(--radius-card)] border border-slate-100 bg-slate-50 p-4 sm:grid-cols-[1fr_auto]">
                <div>
                  <p className="ui-control text-slate-950">{item.productNameSnapshot}</p>
                  <p className="mt-0.5 font-mono text-xs text-slate-500">{item.skuSnapshot}</p>
                  {item.requiresInstallation ? <span className="ui-caption mt-1 inline-block rounded-full bg-amber-100 px-2 py-0.5 text-amber-800">Installation required</span> : null}
                </div>
                <div className="flex flex-col items-end gap-1">
                  <p className="ui-body text-slate-500">{formatMoney(item.unitPrice)} x {item.quantity}</p>
                  <p className="ui-control text-slate-950">{formatMoney(item.lineTotal)}</p>
                </div>
              </div>
            ))}
          </div>

          <div className="mt-5 grid gap-2 border-t border-slate-200 pt-4">
            <div className="flex justify-between text-sm font-bold text-slate-500"><span>Subtotal</span><span>{formatMoney(order.subtotal)}</span></div>
            {order.shippingFee > 0 ? <div className="flex justify-between text-sm font-bold text-slate-500"><span>Shipping fee</span><span>{formatMoney(order.shippingFee)}</span></div> : null}
            {order.discountAmount > 0 ? <div className="flex justify-between text-sm font-bold text-emerald-600"><span>Discount</span><span>-{formatMoney(order.discountAmount)}</span></div> : null}
            <div className="flex justify-between text-xl font-black text-slate-950"><span>Total</span><span className="text-[var(--brand)]">{formatMoney(order.totalAmount)}</span></div>
          </div>
        </section>

        <aside className="grid h-fit gap-4">
          <section className="ui-card border border-slate-100 p-6">
            <h2 className="ui-caption uppercase tracking-[0.18em] text-[var(--brand)]">Order information</h2>
            <div className="mt-4 grid gap-3 text-sm">
              <InfoRow label="Order code" value={order.orderCode} mono />
              <InfoRow label="Status" value={formatOrderStatus(order.status)} />
              <InfoRow label="Created" value={formatDate(order.createdAt)} />
              <InfoRow label="Payment" value={formatPaymentMethod(order.paymentMethod)} />
              <InfoRow label="Recipient" value={order.customerName} />
              <InfoRow label="Phone" value={order.customerPhone} />
              {order.customerEmail ? <InfoRow label="Email" value={order.customerEmail} /> : null}
              <InfoRow label="Address" value={order.shippingAddress} />
              {order.note ? <InfoRow label="Note" value={order.note} /> : null}
            </div>
          </section>

          {order.paymentMethod === 1 ? <ManualTransferPanel order={order} /> : null}

          {order.customerId ? (
            <Link to={`/account/orders/${order.id}`} className="ui-control flex h-12 items-center justify-center rounded-[var(--radius-control)] bg-slate-950 text-white transition hover:bg-slate-800">
              View in account
            </Link>
          ) : null}
          <Link to={`/orders/lookup?orderCode=${encodeURIComponent(order.orderCode)}&phone=${encodeURIComponent(order.customerPhone)}`} className="ui-control flex h-12 items-center justify-center rounded-[var(--radius-control)] border border-[var(--brand)] text-[var(--brand)] transition hover:bg-[var(--brand-soft)]">
            Track order
          </Link>
          <Link to="/products" className="ui-control flex h-12 items-center justify-center rounded-[var(--radius-control)] border border-slate-200 text-slate-700 transition hover:border-slate-950 hover:text-slate-950">
            Continue shopping
          </Link>
        </aside>
      </div>
    </div>
  );
}

function ManualTransferPanel({ order }: { order: OrderDto }) {
  const transferContent = getManualTransferContent(order);

  return (
    <section className="ui-card border border-slate-100 p-6">
      <h2 className="ui-caption uppercase tracking-[0.18em] text-[var(--brand)]">Manual transfer</h2>
      <p className="mt-3 text-sm leading-6 text-slate-600">
        Use the generated content below as your transfer reference. Staff will confirm the order after payment is received.
      </p>
      <div className="mt-4 grid gap-3">
        <CopyInfoRow label="Order code" value={order.orderCode} />
        <CopyInfoRow label="Transfer content" value={transferContent} />
        <InfoRow label="Amount" value={formatTransferMoney(order.totalAmount)} />
      </div>
    </section>
  );
}

function CopyInfoRow({ label, value }: { label: string; value: string }) {
  const [copied, setCopied] = useState(false);

  async function copyValue() {
    if (!navigator.clipboard) {
      return;
    }

    await navigator.clipboard.writeText(value);
    setCopied(true);
    window.setTimeout(() => setCopied(false), 1800);
  }

  return (
    <div className="grid gap-1">
      <span className="ui-caption uppercase tracking-[0.14em] text-slate-400">{label}</span>
      <div className="grid grid-cols-[minmax(0,1fr)_auto] gap-2">
        <span className="min-w-0 rounded-[var(--radius-control)] bg-slate-50 px-3 py-2 font-mono text-sm font-bold text-slate-800">{value}</span>
        <button type="button" onClick={() => void copyValue()} className="ui-control h-10 rounded-[var(--radius-control)] border border-slate-200 px-3 text-slate-700 transition hover:border-slate-950 hover:text-slate-950">
          {copied ? "Copied" : "Copy"}
        </button>
      </div>
    </div>
  );
}

function InfoRow({ label, value, mono = false }: { label: string; value: string; mono?: boolean }) {
  return (
    <div className="flex flex-col gap-0.5">
      <span className="ui-caption uppercase tracking-[0.14em] text-slate-400">{label}</span>
      <span className={`font-bold text-slate-800 ${mono ? "font-mono" : ""}`}>{value}</span>
    </div>
  );
}

function StateMessage({ children, tone = "info" }: { children: ReactNode; tone?: "info" | "error" }) {
  return (
    <div className={`rounded-[var(--radius-card)] px-5 py-4 text-sm font-semibold ${tone === "error" ? "bg-red-50 text-red-700" : "bg-slate-50 text-slate-500"}`}>
      {children}
    </div>
  );
}

function getStateOrder(state: unknown): OrderDto | undefined {
  if (state && typeof state === "object" && "order" in state) {
    return (state as { order?: OrderDto }).order;
  }

  return undefined;
}

function formatTransferMoney(value: number): string {
  return new Intl.NumberFormat("vi-VN", {
    style: "currency",
    currency: "VND",
    maximumFractionDigits: 0
  }).format(value);
}
