import { useState } from "react";
import { useMutation, useQuery } from "@tanstack/react-query";
import type { ShippingCatalogSummary, ShippingRatePlan, ShippingRatePreview } from "@workspace-ecommerce/api-types";
import { Button, Notice } from "../../components/ui/AdminUi";
import { shippingApi } from "../../services/api/shippingApi";
import { getApiErrorMessage } from "../../services/api/errors";
import { shippingKeys } from "../../hooks/queries/useShippingRates";

export function RatePlanPreview({ plan, catalog, onPreview }: { plan: ShippingRatePlan; catalog: ShippingCatalogSummary; onPreview: (value: ShippingRatePreview) => void }) {
  const [province, setProvince] = useState("");
  const [ward, setWard] = useState("");
  const [page, setPage] = useState(1);
  const [weight, setWeight] = useState("1");
  const wards = useQuery({ queryKey: [...shippingKeys, "wards", province, page], queryFn: () => shippingApi.wards(province, page), enabled: !!province });
  const preview = useMutation({ mutationFn: () => shippingApi.preview(plan.id, { provinceCode: province, wardCode: ward, expectedRevision: plan.revision, items: [{ weightKg: Number(weight), quantity: 1 }] }), onSuccess: onPreview });
  return <section className="admin-panel grid gap-3"><h2>Test persisted revision {plan.revision}</h2><p>Simulation only; no customer quote is issued. Draft prices are not active.</p>
    <label>Destination province<select className="admin-input w-full" value={province} onChange={e => { setProvince(e.target.value); setWard(""); setPage(1); preview.reset(); }}><option value="">Choose province</option>{catalog.provinces.map(p => <option value={p.code} key={p.code}>{p.name}</option>)}</select></label>
    <label>Destination ward<select className="admin-input w-full" value={ward} onChange={e => { setWard(e.target.value); preview.reset(); }} disabled={wards.isFetching}><option value="">Choose ward</option>{wards.data?.items.map(w => <option key={w.code} value={w.code}>{w.name} ({w.code})</option>)}</select></label>
    {wards.isError && <Notice type="error" title={getApiErrorMessage(wards.error)} />}
    {province && <div className="flex gap-2"><Button disabled={page === 1 || wards.isFetching} onClick={() => { setPage(page - 1); setWard(""); }}>Previous wards</Button><span>Page {page}</span><Button disabled={!wards.data?.hasNextPage || wards.isFetching} onClick={() => { setPage(page + 1); setWard(""); }}>Next wards</Button></div>}
    <label>Parcel weight (kg)<input className="admin-input w-full" type="number" step="0.001" min="0.001" value={weight} onChange={e => { setWeight(e.target.value); preview.reset(); }} /></label>
    <Button disabled={!ward || Number(weight) <= 0 || preview.isPending} onClick={() => preview.mutate()}>Preview price</Button>
    {preview.isError && <Notice type="error" title={getApiErrorMessage(preview.error)} />}
    {preview.data && <p role="status">Preview: {preview.data.calculation.totalFee.toLocaleString("vi-VN")} VND · {preview.data.calculation.weightGrams} g · zone {preview.data.zoneCode}</p>}
  </section>;
}
export function RatePlanHistory({ id }: { id: string }) {
  const [page, setPage] = useState(1);
  const query = useQuery({ queryKey: [...shippingKeys, "history", id, page], queryFn: () => shippingApi.history(id, page) });
  return <section className="admin-panel grid gap-3"><h2>Change history</h2>{query.isLoading && <p role="status">Loading history...</p>}{query.isError && <Notice type="error" title={getApiErrorMessage(query.error)} />}{query.data?.items.map(a => <details key={a.id}><summary>Revision {a.revision} · {a.action} · {new Date(a.occurredAtUtc).toLocaleString()}</summary><p>{a.actor} · {a.reason}</p><pre className="overflow-auto whitespace-pre-wrap text-xs">{a.changesJson}</pre></details>)}{query.data?.totalCount === 0 && <p>No changes recorded.</p>}<div className="flex gap-2"><Button disabled={page === 1 || query.isFetching} onClick={() => setPage(page - 1)}>Previous history</Button><Button disabled={!query.data?.hasNextPage || query.isFetching} onClick={() => setPage(page + 1)}>Next history</Button></div></section>;
}
