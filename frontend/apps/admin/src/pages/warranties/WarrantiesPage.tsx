import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import type { AdminWarrantyImportResultDto, CreateWarrantyPlanRequest, WarrantyPlanCoverageInput } from "@workspace-ecommerce/api-types";
import { formatDate } from "@workspace-ecommerce/shared-utils";
import { useState, type ChangeEvent, type FormEvent } from "react";
import { AdminPageHeader } from "../../components/ui/AdminPageHeader";
import { Button, Field, Notice, TextArea, TextInput } from "../../components/ui/AdminUi";
import { getApiErrorMessage } from "../../services/api/errors";
import { adminApi } from "../../services/api/adminApi";

const defaultCoverage = "FRAME|Frame|60\nMOTOR|Motor|36";

export function WarrantiesPage() {
  const queryClient = useQueryClient();
  const [planCode, setPlanCode] = useState("");
  const [planName, setPlanName] = useState("");
  const [termsVersion, setTermsVersion] = useState("v1");
  const [activationWindowDays, setActivationWindowDays] = useState("60");
  const [coverageLines, setCoverageLines] = useState(defaultCoverage);
  const [variantId, setVariantId] = useState("");
  const [selectedPlanId, setSelectedPlanId] = useState("");
  const [unitFile, setUnitFile] = useState<File | null>(null);
  const [importNotice, setImportNotice] = useState<string | null>(null);
  const [importPreview, setImportPreview] = useState<AdminWarrantyImportResultDto | null>(null);
  const [orderItemIds, setOrderItemIds] = useState<Record<string, string>>({});
  const [selectedRegistrationId, setSelectedRegistrationId] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const plansQuery = useQuery({ queryKey: ["admin", "warranty-plans"], queryFn: () => adminApi.getWarrantyPlans({ pageSize: 100 }) });
  const unitsQuery = useQuery({ queryKey: ["admin", "warranty-units"], queryFn: () => adminApi.getWarrantyUnits({ pageSize: 20 }) });
  const warrantiesQuery = useQuery({ queryKey: ["admin", "warranties"], queryFn: () => adminApi.getAdminWarranties({ pageSize: 20 }) });
  const selectedWarrantyQuery = useQuery({
    queryKey: ["admin", "warranty", selectedRegistrationId],
    queryFn: () => adminApi.getAdminWarranty(selectedRegistrationId ?? ""),
    enabled: Boolean(selectedRegistrationId)
  });
  const invalidateWarranty = async () => {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: ["admin", "warranty-plans"] }),
      queryClient.invalidateQueries({ queryKey: ["admin", "warranty-units"] }),
      queryClient.invalidateQueries({ queryKey: ["admin", "warranties"] })
    ]);
  };

  const createPlanMutation = useMutation({
    mutationFn: adminApi.createWarrantyPlan,
    onSuccess: async () => {
      setNotice("Warranty plan created.");
      setPlanCode("");
      setPlanName("");
      await invalidateWarranty();
    },
    onError: (error) => setNotice(getApiErrorMessage(error))
  });
  const assignPlanMutation = useMutation({
    mutationFn: ({ productVariantId, warrantyPlanId }: { productVariantId: string; warrantyPlanId: string }) =>
      adminApi.assignWarrantyPlanToVariant(productVariantId, { warrantyPlanId, effectiveFrom: new Date().toISOString() }),
    onSuccess: () => setNotice("Warranty plan assigned to product variant."),
    onError: (error) => setNotice(getApiErrorMessage(error))
  });
  const retirePlanMutation = useMutation({
    mutationFn: adminApi.retireWarrantyPlan,
    onSuccess: async () => {
      setNotice("Warranty plan retired. Existing registrations keep their snapshotted terms.");
      await invalidateWarranty();
    },
    onError: (error) => setNotice(getApiErrorMessage(error))
  });
  const previewImportMutation = useMutation({
    mutationFn: adminApi.previewWarrantyUnitImport,
    onSuccess: (result) => {
      setImportPreview(result);
      setImportNotice(result.isValid ? `${result.totalRows} rows are valid and ready to import.` : `${result.failedRows} rows need attention.`);
    },
    onError: (error) => setImportNotice(getApiErrorMessage(error))
  });
  const importMutation = useMutation({
    mutationFn: adminApi.importWarrantyUnits,
    onSuccess: async (result) => {
      setImportNotice(`Imported ${result.importedRows} units.`);
      setUnitFile(null);
      setImportPreview(null);
      await invalidateWarranty();
    },
    onError: (error) => setImportNotice(getApiErrorMessage(error))
  });
  const assignUnitMutation = useMutation({
    mutationFn: ({ unitId, orderItemId }: { unitId: string; orderItemId: string }) => adminApi.assignWarrantyUnit(unitId, { orderItemId }),
    onSuccess: async () => {
      setNotice("Warranty unit assigned to the order item.");
      await invalidateWarranty();
    },
    onError: (error) => setNotice(getApiErrorMessage(error))
  });
  const activateMutation = useMutation({
    mutationFn: adminApi.activateAdminWarranty,
    onSuccess: invalidateWarranty,
    onError: (error) => setNotice(getApiErrorMessage(error))
  });
  const voidMutation = useMutation({
    mutationFn: ({ id, reason }: { id: string; reason: string }) => adminApi.voidAdminWarranty(id, { reason }),
    onSuccess: invalidateWarranty,
    onError: (error) => setNotice(getApiErrorMessage(error))
  });
  const replaceMutation = useMutation({
    mutationFn: ({ id, replacementSerializedProductUnitId, reason }: { id: string; replacementSerializedProductUnitId: string; reason: string }) =>
      adminApi.replaceAdminWarranty(id, { replacementSerializedProductUnitId, reason }),
    onSuccess: async () => {
      setNotice("Warranty replacement completed and coverage dates were carried forward.");
      await invalidateWarranty();
    },
    onError: (error) => setNotice(getApiErrorMessage(error))
  });

  function parseCoverages(): WarrantyPlanCoverageInput[] {
    return coverageLines.split(/\r?\n/).map((line, index) => {
      const [componentCode = "", displayName = "", durationMonths = ""] = line.split("|").map((value) => value.trim());
      return { componentCode, displayName, durationMonths: Number(durationMonths), sortOrder: index };
    }).filter((coverage) => coverage.componentCode || coverage.displayName || coverage.durationMonths);
  }

  function createPlan(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const request: CreateWarrantyPlanRequest = {
      code: planCode,
      name: planName,
      activationWindowDays: Number(activationWindowDays),
      termsVersion,
      effectiveFrom: new Date().toISOString(),
      coverages: parseCoverages()
    };
    createPlanMutation.mutate(request);
  }

  function assignPlan(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!variantId || !selectedPlanId) {
      setNotice("Enter a product variant ID and select a warranty plan.");
      return;
    }
    assignPlanMutation.mutate({ productVariantId: variantId, warrantyPlanId: selectedPlanId });
  }

  function chooseFile(event: ChangeEvent<HTMLInputElement>) {
    setUnitFile(event.target.files?.[0] ?? null);
    setImportNotice(null);
    setImportPreview(null);
  }

  function voidWarranty(id: string) {
    const reason = window.prompt("Reason for voiding this warranty:");
    if (reason?.trim() && window.confirm("Void this active warranty?")) {
      voidMutation.mutate({ id, reason: reason.trim() });
    }
  }

  function replaceWarranty(id: string) {
    const replacementSerializedProductUnitId = window.prompt("Replacement serialized unit ID (it must be pending and assigned to the same order):");
    if (!replacementSerializedProductUnitId?.trim()) {
      return;
    }

    const reason = window.prompt("Reason for replacement:");
    if (reason?.trim() && window.confirm("Replace this active warranty and carry its coverage dates forward?")) {
      replaceMutation.mutate({ id, replacementSerializedProductUnitId: replacementSerializedProductUnitId.trim(), reason: reason.trim() });
    }
  }

  return (
    <div className="admin-page-grid">
      <AdminPageHeader title="Warranties" description="Version warranty rules, provision masked serial/IMEI units, assign physical products to orders, and manage registrations." />
      {notice ? <Notice type="info" title={notice} /> : null}
      {plansQuery.error || unitsQuery.error || warrantiesQuery.error ? <Notice type="error" title="Warranty data could not be loaded">{getApiErrorMessage(plansQuery.error ?? unitsQuery.error ?? warrantiesQuery.error)}</Notice> : null}

      <section className="grid gap-5 xl:grid-cols-[minmax(0,1.1fr)_minmax(340px,0.9fr)]">
        <form onSubmit={createPlan} className="rounded-3xl border border-slate-200 bg-white p-5 shadow-sm">
          <h2 className="text-lg font-black text-slate-950">Create versioned plan</h2>
          <p className="mt-1 text-sm text-slate-600">Coverage rules are snapshotted when a customer activates. Create a new plan rather than rewriting a used rule.</p>
          <div className="mt-5 grid gap-4 sm:grid-cols-2">
            <Field label="Plan code"><TextInput value={planCode} onChange={(event) => setPlanCode(event.target.value)} placeholder="ERGONOMIC-V1" required /></Field>
            <Field label="Plan name"><TextInput value={planName} onChange={(event) => setPlanName(event.target.value)} placeholder="Ergonomic chair warranty" required /></Field>
            <Field label="Activation window (days)"><TextInput type="number" min="1" max="365" value={activationWindowDays} onChange={(event) => setActivationWindowDays(event.target.value)} required /></Field>
            <Field label="Terms version"><TextInput value={termsVersion} onChange={(event) => setTermsVersion(event.target.value)} required /></Field>
          </div>
          <div className="mt-4"><Field label="Coverage components (CODE | Display name | months)"><TextArea value={coverageLines} onChange={(event) => setCoverageLines(event.target.value)} rows={4} required /></Field></div>
          <Button type="submit" variant="primary" className="mt-5" disabled={createPlanMutation.isPending}>{createPlanMutation.isPending ? "Creating..." : "Create warranty plan"}</Button>
        </form>

        <div className="rounded-3xl border border-slate-200 bg-white p-5 shadow-sm">
          <h2 className="text-lg font-black text-slate-950">Plan assignments</h2>
          <p className="mt-1 text-sm text-slate-600">Assign an effective plan to each eligible product variant before units are assigned to orders.</p>
          <form onSubmit={assignPlan} className="mt-5 grid gap-4">
            <Field label="Product variant ID"><TextInput value={variantId} onChange={(event) => setVariantId(event.target.value)} placeholder="UUID from Products" required /></Field>
            <label className="block"><span className="mb-1.5 block text-sm font-bold text-slate-700">Warranty plan</span><select value={selectedPlanId} onChange={(event) => setSelectedPlanId(event.target.value)} className="w-full rounded-xl border border-slate-200 px-3 py-2 text-sm" required><option value="">Select a plan</option>{plansQuery.data?.items.map((plan) => <option key={plan.id} value={plan.id}>{plan.code} — {plan.name}</option>)}</select></label>
            <Button type="submit" disabled={assignPlanMutation.isPending}>{assignPlanMutation.isPending ? "Assigning..." : "Assign plan"}</Button>
          </form>
          <div className="mt-6 grid gap-3">{plansQuery.data?.items.map((plan) => <article key={plan.id} className="rounded-xl bg-slate-50 p-3"><div className="flex items-center justify-between gap-3"><strong className="text-sm text-slate-950">{plan.code}</strong><span className={plan.isActive ? "text-xs font-black text-emerald-700" : "text-xs font-black text-slate-500"}>{plan.isActive ? "Active" : "Retired"}</span></div><p className="mt-1 text-xs text-slate-600">{plan.coverages.map((coverage) => `${coverage.displayName} ${coverage.durationMonths}m`).join(" · ")}</p></article>)}</div>
        </div>
      </section>

      <section className="rounded-3xl border border-slate-200 bg-white p-5 shadow-sm">
        <h2 className="text-lg font-black text-slate-950">Plan lifecycle</h2>
        <p className="mt-1 text-sm text-slate-600">Retiring a plan prevents future use while preserving all terms already snapshotted on registrations.</p>
        <div className="mt-4 flex flex-wrap gap-3">
          {plansQuery.data?.items.filter((plan) => plan.isActive).map((plan) => <Button key={plan.id} type="button" variant="danger" disabled={retirePlanMutation.isPending} onClick={() => { if (window.confirm(`Retire ${plan.code}? Existing registrations will not change.`)) retirePlanMutation.mutate(plan.id); }}>Retire {plan.code}</Button>)}
        </div>
      </section>

      <section className="rounded-3xl border border-slate-200 bg-white p-5 shadow-sm">
        <h2 className="text-lg font-black text-slate-950">Import serialized units</h2>
        <p className="mt-1 text-sm text-slate-600">CSV columns: <code>sku,identifier,identifier_type</code>. Raw values are fingerprinted server-side and never displayed after import.</p>
        <div className="mt-4 flex flex-wrap items-center gap-3"><input type="file" accept=".csv,text/csv" onChange={chooseFile} className="text-sm" /><Button type="button" disabled={!unitFile || previewImportMutation.isPending} onClick={() => unitFile && previewImportMutation.mutate(unitFile)}>Preview</Button><Button type="button" variant="primary" disabled={!unitFile || !importPreview?.isValid || importMutation.isPending} onClick={() => unitFile && importMutation.mutate(unitFile)}>{importMutation.isPending ? "Importing..." : "Commit import"}</Button></div>
        {importNotice ? <p className="mt-3 text-sm font-semibold text-slate-700" role="status">{importNotice}</p> : null}
        {importPreview && !importPreview.isValid ? <div className="mt-4 rounded-xl border border-amber-200 bg-amber-50 p-3"><p className="text-sm font-bold text-amber-900">Fix the following rows before committing. No identifier values are shown here.</p><ul className="mt-2 list-disc space-y-1 pl-5 text-sm text-amber-900">{importPreview.rows.filter((row) => !row.isValid).slice(0, 25).map((row) => <li key={row.rowNumber}>Row {row.rowNumber} ({row.sku || "missing SKU"}): {row.errors.join(", ")}</li>)}</ul>{importPreview.failedRows > 25 ? <p className="mt-2 text-xs text-amber-800">Showing the first 25 invalid rows.</p> : null}</div> : null}
      </section>

      <section className="rounded-3xl border border-slate-200 bg-white p-5 shadow-sm">
        <h2 className="text-lg font-black text-slate-950">Serialized units</h2>
        <div className="mt-4 overflow-x-auto"><table className="min-w-full text-left text-sm"><thead className="border-b border-slate-200 text-xs uppercase tracking-wide text-slate-500"><tr><th className="px-2 py-3">Unit</th><th className="px-2 py-3">SKU</th><th className="px-2 py-3">Status</th><th className="px-2 py-3">Order</th><th className="px-2 py-3">Assign to order item</th></tr></thead><tbody>{unitsQuery.data?.items.map((unit) => <tr key={unit.id} className="border-b border-slate-100"><td className="px-2 py-3 font-bold text-slate-950">{unit.maskedIdentifier}</td><td className="px-2 py-3">{unit.sku}</td><td className="px-2 py-3">{unitStatus(unit.status)}</td><td className="px-2 py-3">{unit.orderCode ?? "—"}</td><td className="px-2 py-3">{unit.status === 0 ? <div className="flex min-w-[330px] gap-2"><TextInput value={orderItemIds[unit.id] ?? ""} onChange={(event) => setOrderItemIds((current) => ({ ...current, [unit.id]: event.target.value }))} placeholder="Order item UUID" /><Button type="button" disabled={assignUnitMutation.isPending} onClick={() => assignUnitMutation.mutate({ unitId: unit.id, orderItemId: orderItemIds[unit.id] ?? "" })}>Assign</Button></div> : "—"}</td></tr>)}</tbody></table></div>
      </section>

      <section className="rounded-3xl border border-slate-200 bg-white p-5 shadow-sm">
        <h2 className="text-lg font-black text-slate-950">Warranty registrations</h2>
        <div className="mt-4 overflow-x-auto"><table className="min-w-full text-left text-sm"><thead className="border-b border-slate-200 text-xs uppercase tracking-wide text-slate-500"><tr><th className="px-2 py-3">Product / unit</th><th className="px-2 py-3">Order</th><th className="px-2 py-3">Status</th><th className="px-2 py-3">Activated</th><th className="px-2 py-3">Actions</th></tr></thead><tbody>{warrantiesQuery.data?.items.map((warranty) => <tr key={warranty.id} className="border-b border-slate-100"><td className="px-2 py-3"><strong className="block text-slate-950">{warranty.productName}</strong><span className="text-xs text-slate-500">{warranty.maskedIdentifier}</span></td><td className="px-2 py-3">{warranty.orderCode}</td><td className="px-2 py-3">{entitlementStatus(warranty.status)}</td><td className="px-2 py-3">{warranty.activatedAt ? formatDate(warranty.activatedAt) : "—"}</td><td className="px-2 py-3">{warranty.status === 0 ? <Button type="button" disabled={activateMutation.isPending} onClick={() => activateMutation.mutate(warranty.id)}>Activate</Button> : null}{warranty.status === 1 ? <Button type="button" variant="danger" disabled={voidMutation.isPending} onClick={() => { const reason = window.prompt("Reason for voiding this warranty:"); if (reason?.trim()) voidMutation.mutate({ id: warranty.id, reason: reason.trim() }); }}>Void</Button> : null}</td></tr>)}</tbody></table></div>
      </section>

      <section className="rounded-3xl border border-slate-200 bg-white p-5 shadow-sm">
        <h2 className="text-lg font-black text-slate-950">Registration lifecycle and audit</h2>
        <p className="mt-1 text-sm text-slate-600">Manual actions require a confirmation and a recorded operational reason. Replacement carries approved coverage dates forward.</p>
        <div className="mt-4 grid gap-3">
          {warrantiesQuery.data?.items.map((warranty) => <article key={warranty.id} className="flex flex-wrap items-center justify-between gap-3 rounded-xl bg-slate-50 p-3"><div><p className="font-bold text-slate-950">{warranty.productName} <span className="font-medium text-slate-500">{warranty.maskedIdentifier}</span></p><p className="mt-1 text-xs text-slate-500">{warranty.orderCode} · {entitlementStatus(warranty.status)}</p></div><div className="flex flex-wrap gap-2"><Button type="button" onClick={() => setSelectedRegistrationId(warranty.id)}>View audit</Button>{warranty.status === 0 ? <Button type="button" disabled={activateMutation.isPending} onClick={() => { if (window.confirm("Activate this warranty manually?")) activateMutation.mutate(warranty.id); }}>Activate</Button> : null}{warranty.status === 1 ? <><Button type="button" variant="danger" disabled={voidMutation.isPending} onClick={() => voidWarranty(warranty.id)}>Void</Button><Button type="button" disabled={replaceMutation.isPending} onClick={() => replaceWarranty(warranty.id)}>Replace</Button></> : null}</div></article>)}
        </div>
        {selectedRegistrationId ? <aside className="mt-5 rounded-2xl border border-slate-200 bg-slate-50 p-4"><div className="flex items-center justify-between gap-3"><h3 className="font-black text-slate-950">Registration audit trail</h3><Button type="button" onClick={() => setSelectedRegistrationId(null)}>Close</Button></div>{selectedWarrantyQuery.isLoading ? <p className="mt-3 text-sm text-slate-600">Loading audit events...</p> : null}{selectedWarrantyQuery.error ? <p className="mt-3 text-sm font-semibold text-red-700">{getApiErrorMessage(selectedWarrantyQuery.error)}</p> : null}{selectedWarrantyQuery.data ? <><p className="mt-3 text-sm text-slate-600">{selectedWarrantyQuery.data.productName} · {selectedWarrantyQuery.data.maskedIdentifier}</p><ol className="mt-4 grid gap-3">{selectedWarrantyQuery.data.auditEvents.map((event) => <li key={event.id} className="rounded-xl bg-white p-3"><p className="text-sm font-bold text-slate-950">{auditAction(event.action)} <span className="font-medium text-slate-500">by {event.actorType}</span></p><p className="mt-1 text-xs text-slate-500">{formatDate(event.occurredAt)}{event.reason ? ` · ${event.reason}` : ""}</p></li>)}</ol></> : null}</aside> : null}
      </section>
    </div>
  );
}

function unitStatus(status: number) { return ["Available", "Assigned", "Activated", "Voided", "Replaced", "Returned"][status] ?? "Unknown"; }
function entitlementStatus(status: number) { return ["Awaiting activation", "Active", "Voided", "Replaced"][status] ?? "Unknown"; }
function auditAction(action: number) { return ["Unit imported", "Unit assigned", "Activated", "Voided", "Returned", "Replaced"][action] ?? "Updated"; }
