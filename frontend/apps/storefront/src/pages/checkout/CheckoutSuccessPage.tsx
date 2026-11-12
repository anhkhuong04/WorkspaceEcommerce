import { formatOrderStatus, formatPaymentMethod, formatPaymentStatus } from "../../i18n-formatters";
import storefrontI18n from "../../i18n";
import { useQuery } from "@tanstack/react-query";
import type { OrderDto } from "@workspace-ecommerce/api-types";
import { formatDate, formatMoney } from "@workspace-ecommerce/shared-utils";
import type { ReactNode } from "react";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { storefrontInfo, bankTransferConfigured } from "../../services/storefrontInfo";
import { OrderShippingDetails } from "../../components/shipment/OrderShippingDetails";
import { Link, useLocation, useSearchParams } from "react-router-dom";
import { getManualTransferContent } from "../../features/checkout/manualTransfer";
import { getApiErrorMessage } from "../../services/api/errors";
import { storefrontApi } from "../../services/api/storefrontApi";

export function CheckoutSuccessPage() {
  useTranslation();
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
    return <StateMessage>{storefrontI18n.t("uiCopy.loading_order")}</StateMessage>;
  }

  if (!order && lookupQuery.error) {
    return (
      <StateMessage tone="error">
        {getApiErrorMessage(lookupQuery.error)} - <Link to="/orders/lookup" className="underline">{storefrontI18n.t("uiCopy.open_order_lookup")}</Link>
      </StateMessage>
    );
  }

  if (!order) {
    return (
      <StateMessage tone="error">{storefrontI18n.t("uiCopy.missing_order_details")}<Link to="/orders/lookup" className="underline">{storefrontI18n.t("uiCopy.open_order_lookup")}</Link>
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
          <h1 className="ui-h1 tracking-tight">{storefrontI18n.t("uiCopy.order_placed_successfully")}</h1>
          <p className="ui-body mt-3 text-white/80">{storefrontI18n.t("uiCopy.your_order_has_been_recorded_we_will_contact_you_soon_to_confirm_it")}</p>
        </div>
        <div className="ui-control mt-2 rounded-full bg-white/20 px-6 py-2 tracking-widest">{order.orderCode}</div>
      </section>

      {order.paymentDueAt && (order.paymentStatus === 0 || order.paymentStatus === 1) && <p role="status" className="rounded-xl bg-amber-50 p-4 text-amber-900">{storefrontI18n.t("paymentResult.payBefore", { time: formatDate(order.paymentDueAt) })}</p>}
      <OrderShippingDetails shipping={order.shipping} />
      <div className="grid gap-6 lg:grid-cols-[1fr_360px]">
        <section className="ui-card border border-slate-100 p-6">
          <h2 className="ui-h2 text-slate-950">{storefrontI18n.t("uiCopy.ordered_items")}</h2>

          <div className="mt-4 grid gap-3">
            {order.items.map((item) => (
              <div key={item.id} className="grid gap-1 rounded-[var(--radius-card)] border border-slate-100 bg-slate-50 p-4 sm:grid-cols-[1fr_auto]">
                <div>
                  <p className="ui-control text-slate-950">{item.productNameSnapshot}</p>
                  <p className="mt-0.5 font-mono text-xs text-slate-600">{item.skuSnapshot}</p>
                  {item.requiresInstallation ? <span className="ui-caption mt-1 inline-block rounded-full bg-amber-100 px-2 py-0.5 text-amber-800">{storefrontI18n.t("uiCopy.installation_required")}</span> : null}
                </div>
                <div className="flex flex-col items-end gap-1">
                  <p className="ui-body text-slate-600">{formatMoney(item.unitPrice)} x {item.quantity}</p>
                  <p className="ui-control text-slate-950">{formatMoney(item.lineTotal)}</p>
                </div>
              </div>
            ))}
          </div>

          <div className="mt-5 grid gap-2 border-t border-slate-200 pt-4">
            <div className="flex justify-between text-sm font-bold text-slate-600"><span>{storefrontI18n.t("uiCopy.subtotal")}</span><span>{formatMoney(order.subtotal)}</span></div>
            {order.shippingFee > 0 ? <div className="flex justify-between text-sm font-bold text-slate-600"><span>{storefrontI18n.t("uiCopy.shipping_fee")}</span><span>{formatMoney(order.shippingFee)}</span></div> : null}
            {order.discountAmount > 0 ? <div className="flex justify-between text-sm font-bold text-emerald-600"><span>{formatDiscountLabel(order)}</span><span>-{formatMoney(order.discountAmount)}</span></div> : null}
            <div className="flex justify-between text-xl font-black text-slate-950"><span>{storefrontI18n.t("uiCopy.total")}</span><span className="text-[var(--brand)]">{formatMoney(order.totalAmount)}</span></div>
          </div>
        </section>

        <aside className="grid h-fit gap-4">
          <section className="ui-card border border-slate-100 p-6">
            <h2 className="ui-caption uppercase tracking-[0.18em] text-[var(--brand)]">{storefrontI18n.t("uiCopy.order_information")}</h2>
            <div className="mt-4 grid gap-3 text-sm">
              <InfoRow label={storefrontI18n.t("uiCopy.order_code")} value={order.orderCode} mono />
              <InfoRow label={storefrontI18n.t("uiCopy.status")} value={formatOrderStatus(order.status)} />
              <InfoRow label={storefrontI18n.t("uiCopy.created")} value={formatDate(order.createdAt)} />
              <InfoRow label={storefrontI18n.t("uiCopy.payment")} value={formatPaymentMethod(order.paymentMethod)} />
              <InfoRow label={storefrontI18n.t("uiCopy.payment_status")} value={formatPaymentStatus(order.paymentStatus)} />
              {order.couponCodeSnapshot ? <InfoRow label={storefrontI18n.t("uiCopy.coupon")} value={formatCouponSnapshot(order)} mono /> : null}
              <InfoRow label={storefrontI18n.t("uiCopy.recipient")} value={order.customerName} />
              <InfoRow label={storefrontI18n.t("uiCopy.phone")} value={order.customerPhone} />
              {order.customerEmail ? <InfoRow label={storefrontI18n.t("uiCopy.email")} value={order.customerEmail} /> : null}
              <InfoRow label={storefrontI18n.t("uiCopy.address")} value={order.shippingAddress} />
              {order.note ? <InfoRow label={storefrontI18n.t("uiCopy.note")} value={order.note} /> : null}
            </div>
          </section>

          {order.paymentMethod === 1 && (order.paymentStatus === 0 || order.paymentStatus === 1) ? <ManualTransferPanel order={order} /> : null}

          {order.customerId ? (
            <Link to={`/account/orders/${order.id}`} className="ui-control flex h-12 items-center justify-center rounded-[var(--radius-control)] bg-slate-950 text-white transition hover:bg-slate-800">{storefrontI18n.t("uiCopy.view_in_account")}</Link>
          ) : null}
          <Link to={`/orders/lookup?orderCode=${encodeURIComponent(order.orderCode)}&phone=${encodeURIComponent(order.customerPhone)}`} className="ui-control flex h-12 items-center justify-center rounded-[var(--radius-control)] border border-[var(--brand)] text-[var(--brand)] transition hover:bg-[var(--brand-soft)]">{storefrontI18n.t("uiCopy.track_order")}</Link>
          <Link to="/products" className="ui-control flex h-12 items-center justify-center rounded-[var(--radius-control)] border border-slate-200 text-slate-700 transition hover:border-slate-950 hover:text-slate-950">{storefrontI18n.t("uiCopy.continue_shopping")}</Link>
        </aside>
      </div>
    </div>
  );
}

function ManualTransferPanel({ order }: { order: OrderDto }) {
  const { t } = useTranslation();
  const transferContent = getManualTransferContent(order);

  return (
    <section className="ui-card border border-slate-100 p-6">
      <h2 className="ui-h2">{t("bank.title")}</h2>
      {bankTransferConfigured ? <div className="mt-4 grid gap-3">
        <InfoRow label={t("bank.bankName")} value={storefrontInfo.bankName} />
        <InfoRow label={t("bank.accountName")} value={storefrontInfo.bankAccountName} />
        <CopyInfoRow label={t("bank.accountNumber")} value={storefrontInfo.bankAccountNumber} />
        <CopyInfoRow label={t("bank.reference")} value={transferContent} />
        <InfoRow label={t("bank.amount")} value={formatTransferMoney(order.totalAmount)} />
      </div> : <p className="mt-4 text-sm leading-6 text-slate-700">{t("bank.missing")} <Link to="/support/contact" className="underline">{t("footer.contact")}</Link></p>}
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
      <span className="ui-caption uppercase tracking-[0.14em] text-slate-600">{label}</span>
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
      <span className="ui-caption uppercase tracking-[0.14em] text-slate-600">{label}</span>
      <span className={`font-bold text-slate-800 ${mono ? "font-mono" : ""}`}>{value}</span>
    </div>
  );
}

function StateMessage({ children, tone = "info" }: { children: ReactNode; tone?: "info" | "error" }) {
  return (
    <div className={`rounded-[var(--radius-card)] px-5 py-4 text-sm font-semibold ${tone === "error" ? "bg-red-50 text-red-700" : "bg-slate-50 text-slate-600"}`}>
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

function formatDiscountLabel(order: OrderDto): string {
  return order.couponCodeSnapshot ? `Discount (${order.couponCodeSnapshot})` : "Discount";
}

function formatCouponSnapshot(order: OrderDto): string {
  return order.couponNameSnapshot ? `${order.couponCodeSnapshot} - ${order.couponNameSnapshot}` : order.couponCodeSnapshot ?? "";
}

function formatTransferMoney(value: number): string {
  return new Intl.NumberFormat("vi-VN", {
    style: "currency",
    currency: "VND",
    maximumFractionDigits: 0
  }).format(value);
}
