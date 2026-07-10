import { useQuery } from "@tanstack/react-query";
import type { PaymentResultDto, PaymentStatus } from "@workspace-ecommerce/api-types";
import { formatDate, formatPaymentMethod, formatPaymentStatus } from "@workspace-ecommerce/shared-utils";
import type { ReactNode } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { getApiErrorMessage } from "../../services/api/errors";
import { storefrontApi } from "../../services/api/storefrontApi";

type PaymentResultStatus = "success" | "failed" | "cancelled";

export function PaymentResultPage() {
  const [searchParams] = useSearchParams();
  const orderCode = searchParams.get("orderCode") ?? "";
  const statusParam = normalizeStatus(searchParams.get("status"));

  const resultQuery = useQuery({
    queryKey: ["payment-result", orderCode],
    queryFn: () => storefrontApi.getPaymentResult(orderCode),
    enabled: orderCode.length > 0,
    retry: false
  });

  if (!orderCode) {
    return (
      <StateMessage tone="error">
        Missing order code. <Link to="/orders/lookup" className="underline">Open order lookup</Link>
      </StateMessage>
    );
  }

  if (resultQuery.isLoading) {
    return <StateMessage>Checking payment result...</StateMessage>;
  }

  if (resultQuery.error) {
    return (
      <StateMessage tone="error">
        {getApiErrorMessage(resultQuery.error)} - <Link to="/orders/lookup" className="underline">Open order lookup</Link>
      </StateMessage>
    );
  }

  const result = resultQuery.data ?? null;
  const effectiveStatus = result ? statusFromPaymentStatus(result.paymentStatus) : statusParam;

  return (
    <div className="grid gap-6">
      <section className={`rounded-[var(--radius-card)] px-8 py-12 text-center shadow-[var(--shadow-card)] ${heroClassName(effectiveStatus)}`}>
        <div className="mx-auto grid h-16 w-16 place-items-center rounded-full bg-white/20">
          <PaymentResultIcon status={effectiveStatus} />
        </div>
        <h1 className="ui-h1 mt-5 text-white">{headline(effectiveStatus)}</h1>
        <p className="ui-body mx-auto mt-3 max-w-2xl text-white/80">{description(effectiveStatus)}</p>
        <div className="ui-control mx-auto mt-5 inline-flex rounded-full bg-white/20 px-6 py-2 font-mono tracking-widest text-white">
          {orderCode}
        </div>
      </section>

      {result ? <PaymentResultSummary result={result} /> : null}

      <section className="flex flex-wrap gap-3">
        <Link to={`/orders/lookup?orderCode=${encodeURIComponent(orderCode)}`} className="ui-control flex h-12 items-center rounded-[var(--radius-control)] border border-[var(--brand)] px-5 text-[var(--brand)] transition hover:bg-[var(--brand-soft)]">
          Open order lookup
        </Link>
        <Link to="/account/orders" className="ui-control flex h-12 items-center rounded-[var(--radius-control)] bg-slate-950 px-5 text-white transition hover:bg-slate-800">
          View account orders
        </Link>
        <Link to="/products" className="ui-control flex h-12 items-center rounded-[var(--radius-control)] border border-slate-200 px-5 text-slate-700 transition hover:border-slate-950 hover:text-slate-950">
          Continue shopping
        </Link>
      </section>
    </div>
  );
}

function PaymentResultSummary({ result }: { result: PaymentResultDto }) {
  return (
    <section className="ui-card border border-slate-100 p-6">
      <div className="flex flex-wrap items-start justify-between gap-3 border-b border-slate-100 pb-5">
        <div>
          <p className="ui-caption uppercase tracking-[0.18em] text-[var(--brand)]">Payment</p>
          <h2 className="ui-h2 mt-2 text-slate-950">{formatPaymentStatus(result.paymentStatus)}</h2>
        </div>
        <PaymentStatusBadge status={result.paymentStatus} />
      </div>

      <div className="mt-5 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        <InfoBlock label="Order code" value={result.orderCode} mono />
        <InfoBlock label="Method" value={formatPaymentMethod(result.paymentMethod)} />
        <InfoBlock label="Paid at" value={result.paidAt ? formatDate(result.paidAt) : "-"} />
        <InfoBlock label="Gateway code" value={result.gatewayResponseCode ?? "-"} />
        <InfoBlock label="Transaction" value={result.transaction?.txnRef ?? "-"} mono />
        <InfoBlock label="Shipment" value={result.shipmentCreated ? result.trackingCode ?? "Created" : "Pending"} />
      </div>

      {result.message ? <p className="ui-body mt-5 rounded-[var(--radius-card)] bg-slate-50 px-4 py-3 text-slate-600">{result.message}</p> : null}
    </section>
  );
}

function PaymentStatusBadge({ status }: { status: PaymentStatus }) {
  const className = status === 2
    ? "bg-emerald-100 text-emerald-800"
    : status === 1
      ? "bg-blue-100 text-blue-800"
      : status === 4
        ? "bg-slate-100 text-slate-700"
        : "bg-red-100 text-red-800";

  return <span className={`inline-flex rounded-full px-3 py-1 text-xs font-black ${className}`}>{formatPaymentStatus(status)}</span>;
}

function InfoBlock({ label, mono = false, value }: { label: string; mono?: boolean; value: string }) {
  return (
    <div className="grid gap-0.5">
      <span className="ui-caption uppercase tracking-[0.14em] text-slate-400">{label}</span>
      <span className={`break-words text-sm font-bold text-slate-800 ${mono ? "font-mono" : ""}`}>{value}</span>
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

function PaymentResultIcon({ status }: { status: PaymentResultStatus }) {
  if (status === "success") {
    return (
      <svg className="h-9 w-9 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5} aria-hidden="true">
        <path strokeLinecap="round" strokeLinejoin="round" d="M4.5 12.75l6 6 9-13.5" />
      </svg>
    );
  }

  return (
    <svg className="h-9 w-9 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5} aria-hidden="true">
      <path strokeLinecap="round" strokeLinejoin="round" d="M6 6l12 12M18 6 6 18" />
    </svg>
  );
}

function normalizeStatus(value: string | null): PaymentResultStatus {
  return value === "success" || value === "cancelled" ? value : "failed";
}

function statusFromPaymentStatus(status: PaymentStatus): PaymentResultStatus {
  if (status === 2) {
    return "success";
  }

  return status === 4 ? "cancelled" : "failed";
}

function heroClassName(status: PaymentResultStatus): string {
  return status === "success"
    ? "bg-gradient-to-br from-emerald-700 to-slate-950"
    : status === "cancelled"
      ? "bg-gradient-to-br from-slate-700 to-slate-950"
      : "bg-gradient-to-br from-rose-700 to-slate-950";
}

function headline(status: PaymentResultStatus): string {
  return status === "success"
    ? "Payment completed."
    : status === "cancelled"
      ? "Payment cancelled."
      : "Payment failed.";
}

function description(status: PaymentResultStatus): string {
  return status === "success"
    ? "Your VNPay payment has been confirmed. Shipment will be prepared automatically when available."
    : status === "cancelled"
      ? "The VNPay payment was cancelled. Your order remains available for review."
      : "The VNPay payment did not complete successfully. You can review the order status below.";
}
