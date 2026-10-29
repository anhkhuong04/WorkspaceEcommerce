import { zodResolver } from "@hookform/resolvers/zod";
import { useFieldArray, useForm, useWatch } from "react-hook-form";
import { z } from "zod";
import type { SaveShippingRatePlanRequest, ShippingCatalogSummary, ShippingRatePlan } from "@workspace-ecommerce/api-types";
import { Button } from "../../components/ui/AdminUi";

const whole = z.number().int().nonnegative().max(Number.MAX_SAFE_INTEGER);
const zone = z.object({ code: z.string().trim().min(1).max(50), name: z.string().trim().min(1).max(120), baseWeightGrams: whole.positive(), baseFee: whole, stepWeightGrams: whole.positive(), stepFee: whole, maximumWeightGrams: whole.positive().nullable(), provinceCodes: z.array(z.string()).min(1) }).refine(v => v.maximumWeightGrams === null || v.maximumWeightGrams >= v.baseWeightGrams, { message: "Maximum must cover the base weight", path: ["maximumWeightGrams"] });
const schema = z.object({ name: z.string().trim().min(1).max(120), originProvinceCode: z.string().length(2), reason: z.string().trim().min(1).max(500), zones: z.array(zone).max(100) }).superRefine((value, ctx) => {
  const codes = value.zones.flatMap(v => v.provinceCodes);
  if (new Set(codes).size !== codes.length) ctx.addIssue({ code: "custom", path: ["zones"], message: "A province can belong to only one zone" });
  if (new Set(value.zones.map(v => v.code)).size !== value.zones.length) ctx.addIssue({ code: "custom", path: ["zones"], message: "Zone codes must be unique" });
});
type Values = z.infer<typeof schema>;
export function RatePlanEditor({ plan, catalog, busy, onSave }: { plan: ShippingRatePlan | null; catalog: ShippingCatalogSummary; busy: boolean; onSave: (body: SaveShippingRatePlanRequest) => void }) {
  const { register, control, handleSubmit, formState: { errors, isDirty } } = useForm<Values>({ resolver: zodResolver(schema), defaultValues: { name: plan?.name ?? "", originProvinceCode: plan?.originProvinceCode ?? "", zones: plan?.zones ?? [], reason: "" } });
  const { fields, append, remove } = useFieldArray({ control, name: "zones" });
  const zones = useWatch({ control, name: "zones" });
  const input = "admin-input w-full";
  return <form className="grid gap-4" onSubmit={handleSubmit(values => onSave({ ...values, catalogVersion: catalog.version, expectedRevision: plan?.revision ?? 0 }))}>
    <p>Draft edits affect no active price. Save changes before previewing the persisted revision.</p>
    {isDirty && <p role="status">Unsaved changes</p>}
    <label>Name<input className={input} aria-label="Name" {...register("name")} aria-invalid={!!errors.name} />{errors.name && <span role="alert">{errors.name.message}</span>}</label>
    <label>Origin province<select className={input} aria-label="Origin province" {...register("originProvinceCode")} aria-invalid={!!errors.originProvinceCode}><option value="">Choose origin</option>{catalog.provinces.map(p => <option key={p.code} value={p.code}>{p.name} ({p.code})</option>)}</select>{errors.originProvinceCode && <span role="alert">{errors.originProvinceCode.message}</span>}</label>
    {fields.map((field, index) => <fieldset key={field.id} className="rounded-xl border border-slate-200 p-4 grid gap-3">
      <legend>Zone {index + 1}</legend>
      <div className="grid gap-3 sm:grid-cols-2">
        <label>Zone code<input className={input} {...register(`zones.${index}.code`)} />{errors.zones?.[index]?.code?.message}</label>
        <label>Zone name<input className={input} {...register(`zones.${index}.name`)} />{errors.zones?.[index]?.name?.message}</label>
        {(["baseWeightGrams", "baseFee", "stepWeightGrams", "stepFee", "maximumWeightGrams"] as const).map(key => <label key={key}>{({ baseWeightGrams: "Base weight (g)", baseFee: "Base fee (VND)", stepWeightGrams: "Weight step (g)", stepFee: "Step fee (VND)", maximumWeightGrams: "Maximum weight (g, blank = unlimited)" })[key]}<input className={input} type="number" step="1" min={key.includes("Fee") ? 0 : 1} {...register(`zones.${index}.${key}`, { setValueAs: (value: string) => key === "maximumWeightGrams" && (value === "" || value == null) ? null : Number(value) })} aria-invalid={!!errors.zones?.[index]?.[key]} />{errors.zones?.[index]?.[key] && <span role="alert">{errors.zones[index][key]?.message}</span>}</label>)}
      </div>
      <fieldset><legend>Destination provinces (one zone per province)</legend><div className="grid gap-2 sm:grid-cols-2">{catalog.provinces.map(p => <label key={p.code} className="flex items-center gap-2 py-1"><input type="checkbox" value={p.code} disabled={zones.some((v, i) => i !== index && v.provinceCodes.includes(p.code))} {...register(`zones.${index}.provinceCodes`)} />{p.name} ({p.code})</label>)}</div>{errors.zones?.[index]?.provinceCodes && <span role="alert">{errors.zones[index].provinceCodes?.message}</span>}</fieldset>
      <Button onClick={() => remove(index)}>Remove zone {index + 1}</Button>
    </fieldset>)}
    {errors.zones?.root?.message && <p role="alert">{errors.zones.root.message}</p>}
    {errors.zones?.message && <p role="alert">{errors.zones.message}</p>}
    <Button disabled={fields.length >= 100} onClick={() => append({ code: "", name: "", baseWeightGrams: 1000, baseFee: 0, stepWeightGrams: 1000, stepFee: 0, maximumWeightGrams: null, provinceCodes: [] })}>Add zone</Button>
    <label>Reason<input className={input} aria-label="Reason" {...register("reason")} aria-invalid={!!errors.reason} />{errors.reason && <span role="alert">{errors.reason.message}</span>}</label>
    <Button type="submit" variant="primary" disabled={busy}>{busy ? "Saving..." : "Save draft"}</Button>
  </form>;
}
