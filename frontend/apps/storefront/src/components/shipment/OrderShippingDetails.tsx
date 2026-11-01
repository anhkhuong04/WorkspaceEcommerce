import type { OrderShippingDetailsDto } from "@workspace-ecommerce/api-types";
import { useTranslation } from "react-i18next";
import { formatMoney } from "@workspace-ecommerce/shared-utils";
export function OrderShippingDetails({ shipping }: { shipping?: OrderShippingDetailsDto | null }) {
  const { t } = useTranslation("shipping");
  if (!shipping) return null;
  return <section className="grid gap-2 rounded-lg border border-slate-200 p-4 text-sm">
    <h3 className="font-bold">{t("method")}: {shipping.method === 0 ? "WorkspaceEC" : "MiniLogistics"}</h3>
    <p>{t("fee")}: {formatMoney(shipping.customerCharge)}</p>
    <p>{t("weight")}: {shipping.weightGrams} g</p>
    {shipping.trackingCode && <p>{t("tracking")}: <span className="font-mono">{shipping.trackingCode}</span></p>}
    {shipping.status && <p>{t("status")}: {t(`statuses.${shipping.status}`, { defaultValue: shipping.status })}</p>}
    {shipping.zoneCode && <p>{t("zone")}: {shipping.zoneCode}</p>}
    {shipping.rateRevision !== null && <p>{t("revision")}: {shipping.rateRevision}</p>}
  </section>;
}
