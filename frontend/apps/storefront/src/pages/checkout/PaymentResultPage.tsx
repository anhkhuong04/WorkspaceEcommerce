import { useQuery } from "@tanstack/react-query";
import type { PaymentStatus } from "@workspace-ecommerce/api-types";
import { formatDate, formatMoney } from "@workspace-ecommerce/shared-utils";
import { Link, useLocation, useSearchParams } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { getApiErrorMessage } from "../../services/api/errors";
import { storefrontApi } from "../../services/api/storefrontApi";
import { OrderShippingDetails } from "../../components/shipment/OrderShippingDetails";
import { useCustomerAuth } from "../../features/customer-auth/useCustomerAuth";

const paymentKeys: Record<PaymentStatus, string> = { 0: "unpaid", 1: "pending", 2: "paid", 3: "failed", 4: "cancelled", 5: "refundPending", 6: "refunded", 7: "partiallyRefunded" };

export function PaymentResultPage() {
  const { t } = useTranslation();
  const { customer, isReady } = useCustomerAuth();
  const [searchParams] = useSearchParams();
  const location = useLocation();
  const orderCode = searchParams.get("orderCode") ?? "";
  const resultAccessToken = new URLSearchParams(location.hash.replace(/^#/, "")).get("resultToken");
  const resultQuery = useQuery({
    queryKey: ["payment-result", orderCode, customer?.id, resultAccessToken],
    queryFn: () => storefrontApi.getPaymentResult(orderCode, resultAccessToken),
    enabled: Boolean(orderCode) && (Boolean(resultAccessToken) || isReady),
    retry: false,
    refetchInterval: (query) => query.state.data?.paymentStatus === 1 && query.state.dataUpdateCount < 20 ? 3000 : false,
    refetchIntervalInBackground: false,
  });
  if (!orderCode) return <div role="alert">{t("paymentResult.missingCode")} <Link to="/orders/lookup" className="underline">{t("paymentResult.lookup")}</Link></div>;
  if (resultQuery.isPending) return <p role="status">{t("paymentResult.checking")}</p>;
  const result = resultQuery.data;
  const key = result ? paymentKeys[result.paymentStatus] : "pending";
  const tone = result?.paymentStatus === 2 ? "bg-emerald-800" : result?.paymentStatus === 3 ? "bg-rose-800" : "bg-slate-800";
  return <div className="grid gap-6">
    {resultQuery.error && <div role="alert" className="rounded-xl bg-red-50 p-5 text-red-700">{getApiErrorMessage(resultQuery.error)}</div>}
    {result && <>
      <section className={`rounded-[var(--radius-card)] px-6 py-10 text-center text-white ${tone}`} aria-live="polite">
        <h1 className="ui-h1">{t(`paymentResult.${key}.title`)}</h1>
        <p className="ui-body mx-auto mt-3 max-w-2xl">{t(`paymentResult.${key}.description`)}</p>
        <p className="mt-5 break-all font-mono font-semibold">{result.orderCode}</p>
      </section>
      <section className="ui-card grid gap-4 border border-slate-200 p-6 sm:grid-cols-2">
        <div><p className="text-sm text-slate-600">{t("paymentResult.method")}</p><p>{t(`paymentResult.methods.${result.paymentMethod}`)}</p></div>
        <div><p className="text-sm text-slate-600">{t("paymentResult.paidAt")}</p><p>{result.paidAt ? formatDate(result.paidAt) : "—"}</p></div>
        {result.totalAmount != null && <div><p className="text-sm text-slate-600">{t("paymentResult.amount")}</p><p className="font-semibold">{formatMoney(result.totalAmount)}</p></div>}
      </section>
      <OrderShippingDetails shipping={result.shipping} />
    </>}
    <div className="flex flex-wrap gap-3">
      <button type="button" disabled={resultQuery.isFetching} onClick={() => void resultQuery.refetch()} className="min-h-11 rounded-full border border-slate-300 px-5 disabled:opacity-50">{t("paymentResult.refresh")}</button>
      <Link to={`/orders/lookup?orderCode=${encodeURIComponent(orderCode)}`} className="inline-flex min-h-11 items-center rounded-full border border-slate-300 px-5">{t("paymentResult.lookup")}</Link>
      <Link to="/products" className="inline-flex min-h-11 items-center rounded-full bg-slate-950 px-5 text-white">{t("paymentResult.shop")}</Link>
    </div>
  </div>;
}
