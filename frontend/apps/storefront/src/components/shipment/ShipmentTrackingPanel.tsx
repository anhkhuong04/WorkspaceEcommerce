import type { ShipmentTrackingDto } from "@workspace-ecommerce/api-types";
import { formatDate, formatMoney } from "@workspace-ecommerce/shared-utils";
import { useTranslation } from "react-i18next";

export function ShipmentTrackingPanel({ tracking }: { tracking: ShipmentTrackingDto }) {
  const { t } = useTranslation();
  if (!tracking.trackingCode) {
    return (
      <div className="rounded-[var(--radius-card)] border border-dashed border-slate-200 bg-slate-50 p-5">
        <p className="text-sm font-bold text-slate-800">{t("shipment.preparing")}</p>
        <p className="ui-body mt-1 text-slate-500">{t("shipment.preparingHint")}</p>
        {tracking.lastCommandError ? <p className="mt-2 text-sm font-semibold text-amber-700">{t("shipment.retryQueued")}</p> : null}
      </div>
    );
  }

  return (
    <div className="grid gap-5">
      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
        <ShipmentField label={t("shipment.trackingCode")} value={tracking.trackingCode} mono />
        <ShipmentField label={t("shipment.carrierStatus")} value={formatProviderStatus(tracking.providerStatus, t("shipment.pending"))} />
        <ShipmentField label={t("shipment.carrier")} value={tracking.provider ?? "MiniLogistics"} />
        <ShipmentField label={t("shipment.lastUpdate")} value={tracking.lastEventAtUtc ? formatDate(tracking.lastEventAtUtc) : t("shipment.pending")} />
        {tracking.shippingFeeAmount !== null ? (
          <ShipmentField label={t("shipment.shippingFee")} value={formatMoney(tracking.shippingFeeAmount)} />
        ) : null}
      </div>

      {tracking.timeline.length > 0 ? (
        <ol className="grid gap-4 border-t border-slate-100 pt-5">
          {tracking.timeline.map((entry) => (
            <li key={entry.id} className="grid grid-cols-[14px_minmax(0,1fr)] gap-3">
              <span className="mt-1.5 h-3.5 w-3.5 rounded-full bg-emerald-600 ring-4 ring-emerald-50" aria-hidden="true" />
              <span className="min-w-0">
                <span className="block text-sm font-bold text-slate-950">{formatProviderStatus(entry.providerStatus, t("shipment.pending"))}</span>
                <span className="mt-0.5 block text-xs font-medium text-slate-500">{formatDate(entry.changedAtUtc)}</span>
                {entry.note ? <span className="mt-1 block text-sm text-slate-600">{entry.note}</span> : null}
              </span>
            </li>
          ))}
        </ol>
      ) : (
        <p className="border-t border-slate-100 pt-4 text-sm font-semibold text-slate-500">{t("shipment.waiting")}</p>
      )}
    </div>
  );
}

function ShipmentField({ label, mono = false, value }: { label: string; mono?: boolean; value: string }) {
  return (
    <div className="min-w-0">
      <p className="ui-caption uppercase tracking-[0.14em] text-slate-400">{label}</p>
      <p className={`mt-1 break-words text-sm font-bold text-slate-800 ${mono ? "font-mono" : ""}`}>{value}</p>
    </div>
  );
}

function formatProviderStatus(status: string | null, pending: string): string {
  if (!status) return pending;
  return status.replace(/([a-z])([A-Z])/g, "$1 $2");
}
