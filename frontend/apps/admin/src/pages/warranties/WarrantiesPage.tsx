import { ApiClientError } from "@workspace-ecommerce/api-client";
import { ShieldOff } from "lucide-react";
import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import type {
  AdminWarrantyImportResultDto,
  AdminWarrantyUnitDto,
  CreateWarrantyPlanRequest,
  WarrantyPlanCoverageInput,
} from "@workspace-ecommerce/api-types";
import { formatDate } from "@workspace-ecommerce/shared-utils";
import { useState, type ChangeEvent, type FormEvent } from "react";
import { AdminPageHeader } from "../../components/ui/AdminPageHeader";
import { Pagination } from "../../components/ui/Pagination";
import { VariantLookup, WarrantyPlanLookup, WarrantyOrderItemLookup } from "./WarrantyLookups";
import {
  AdminTabs,
  Button,
  ConfirmDialog,
  EmptyState,
  Field,
  Modal,
  Notice,
  Pill,
  SelectInput,
  TableViewport,
  TextArea,
  TextInput,
} from "../../components/ui/AdminUi";
import { getApiErrorMessage } from "../../services/api/errors";
import { adminApi } from "../../services/api/adminApi";

const defaultCoverage = [{ key: "frame", componentCode: "FRAME", displayName: "Frame", durationMonths: 60, sortOrder: 0 }, { key: "motor", componentCode: "MOTOR", displayName: "Motor", durationMonths: 36, sortOrder: 1 }];

export function WarrantiesPage() {
  const queryClient = useQueryClient();
  const [tab, setTab] = useState("registrations");
  const [pages, setPages] = useState({ registrations: 1, units: 1, plans: 1 });
  const [search, setSearch] = useState("");
  const [replacementSearch, setReplacementSearch] = useState("");
  const [replacementPage, setReplacementPage] = useState(1);
  const [planCode, setPlanCode] = useState("");
  const [planName, setPlanName] = useState("");
  const [termsVersion, setTermsVersion] = useState("v1");
  const [activationWindowDays, setActivationWindowDays] = useState("60");
  const [coverages, setCoverages] = useState(defaultCoverage);
  const [variantId, setVariantId] = useState("");
  const [selectedPlanId, setSelectedPlanId] = useState("");
  const [unitFile, setUnitFile] = useState<File | null>(null);
  const [importNotice, setImportNotice] = useState<string | null>(null);
  const [importPreview, setImportPreview] =
    useState<AdminWarrantyImportResultDto | null>(null);
  const [assignUnit, setAssignUnit] = useState<AdminWarrantyUnitDto | null>(null);
  const [orderItemId, setOrderItemId] = useState("");
  const [searchDraft, setSearchDraft] = useState("");
  const [selectedRegistrationId, setSelectedRegistrationId] = useState<
    string | null
  >(null);
  const [notice, setNotice] = useState<{
    type: "success" | "error";
    message: string;
  } | null>(null);
  const [action, setAction] = useState<{
    kind: "retire" | "activate" | "void" | "replace";
    id: string;
    label: string;
  } | null>(null);
  const [reason, setReason] = useState("");
  const [replacementId, setReplacementId] = useState("");
  const [actionError, setActionError] = useState<string | null>(null);

  const plansQuery = useQuery({
    queryKey: ["admin", "warranty-plans", pages.plans, search],
    queryFn: () => adminApi.getWarrantyPlans({ pageNumber: pages.plans, pageSize: 20, search: search || undefined }),
    placeholderData: keepPreviousData,
  });
  const unitsQuery = useQuery({
    queryKey: ["admin", "warranty-units", pages.units, search],
    queryFn: () => adminApi.getWarrantyUnits({ pageNumber: pages.units, pageSize: 20, search: search || undefined }),
    placeholderData: keepPreviousData,
  });
  const warrantiesQuery = useQuery({
    queryKey: ["admin", "warranties", pages.registrations, search],
    queryFn: () => adminApi.getAdminWarranties({ pageNumber: pages.registrations, pageSize: 20, search: search || undefined }),
    placeholderData: keepPreviousData,
  });
  const selectedWarrantyQuery = useQuery({
    queryKey: ["admin", "warranty", selectedRegistrationId],
    queryFn: () => adminApi.getAdminWarranty(selectedRegistrationId ?? ""),
    enabled: Boolean(selectedRegistrationId),
  });
  const replacementUnits = useQuery({
    queryKey: ["admin", "replacement-units", action?.id, replacementSearch, replacementPage],
    enabled: action?.kind === "replace",
    queryFn: () => adminApi.getWarrantyReplacementUnits(action!.id, { search: replacementSearch || undefined, pageNumber: replacementPage, pageSize: 20 }),
  });
  const invalidateWarranty = async () => {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: ["admin", "warranty-plans"] }),
      queryClient.invalidateQueries({ queryKey: ["admin", "warranty-units"] }),
      queryClient.invalidateQueries({ queryKey: ["admin", "warranties"] }),
      queryClient.invalidateQueries({ queryKey: ["admin", "warranty"] }),
      queryClient.invalidateQueries({ queryKey: ["admin", "replacement-units"] }),
      queryClient.invalidateQueries({ queryKey: ["admin-products"] }),
      queryClient.invalidateQueries({ queryKey: ["admin-dashboard"] }),
    ]);
  };

  const createPlanMutation = useMutation({
    mutationFn: adminApi.createWarrantyPlan,
    onSuccess: async () => {
      setNotice({ type: "success", message: "Warranty plan created." });
      setPlanCode("");
      setPlanName("");
      await invalidateWarranty();
    },
    onError: (error) =>
      setNotice({ type: "error", message: getApiErrorMessage(error) }),
  });
  const assignPlanMutation = useMutation({
    mutationFn: ({
      productVariantId,
      warrantyPlanId,
    }: {
      productVariantId: string;
      warrantyPlanId: string;
    }) =>
      adminApi.assignWarrantyPlanToVariant(productVariantId, {
        warrantyPlanId,
        effectiveFrom: new Date().toISOString(),
      }),
    onSuccess: () =>
      setNotice({
        type: "success",
        message: "Warranty plan assigned to product variant.",
      }),
    onError: (error) =>
      setNotice({ type: "error", message: getApiErrorMessage(error) }),
  });
  const retirePlanMutation = useMutation({
    mutationFn: adminApi.retireWarrantyPlan,
    onSuccess: async () => {
      setNotice({
        type: "success",
        message:
          "Warranty plan retired. Existing registrations keep their snapshotted terms.",
      });
      await invalidateWarranty();
    },
    onError: (error) =>
      setNotice({ type: "error", message: getApiErrorMessage(error) }),
  });
  const previewImportMutation = useMutation({
    mutationFn: adminApi.previewWarrantyUnitImport,
    onSuccess: (result) => {
      setImportPreview(result);
      setImportNotice(
        result.isValid
          ? `${result.totalRows} rows are valid and ready to import.`
          : `${result.failedRows} rows need attention.`,
      );
    },
    onError: (error) => setImportNotice(getApiErrorMessage(error)),
  });
  const importMutation = useMutation({
    mutationFn: adminApi.importWarrantyUnits,
    onSuccess: async (result) => {
      setImportNotice(`Imported ${result.importedRows} units.`);
      setUnitFile(null);
      setImportPreview(null);
      await invalidateWarranty();
    },
    onError: (error) => setImportNotice(getApiErrorMessage(error)),
  });
  const assignUnitMutation = useMutation({
    mutationFn: ({
      unitId,
      orderItemId,
    }: {
      unitId: string;
      orderItemId: string;
    }) => adminApi.assignWarrantyUnit(unitId, { orderItemId }),
    onSuccess: async () => {
      setAssignUnit(null); setOrderItemId("");
      setNotice({
        type: "success",
        message: "Warranty unit assigned to the order item.",
      });
      await invalidateWarranty();
    },
    onError: (error) =>
      setNotice({ type: "error", message: getApiErrorMessage(error) }),
  });
  const activateMutation = useMutation({
    mutationFn: adminApi.activateAdminWarranty,
    onSuccess: invalidateWarranty,
    onError: (error) =>
      setNotice({ type: "error", message: getApiErrorMessage(error) }),
  });
  const voidMutation = useMutation({
    mutationFn: ({ id, reason }: { id: string; reason: string }) =>
      adminApi.voidAdminWarranty(id, { reason }),
    onSuccess: invalidateWarranty,
    onError: (error) =>
      setNotice({ type: "error", message: getApiErrorMessage(error) }),
  });
  const replaceMutation = useMutation({
    mutationFn: ({
      id,
      replacementSerializedProductUnitId,
      reason,
    }: {
      id: string;
      replacementSerializedProductUnitId: string;
      reason: string;
    }) =>
      adminApi.replaceAdminWarranty(id, {
        replacementSerializedProductUnitId,
        reason,
      }),
    onSuccess: async () => {
      setNotice({
        type: "success",
        message:
          "Warranty replacement completed and coverage dates were carried forward.",
      });
      await invalidateWarranty();
    },
    onError: (error) =>
      setNotice({ type: "error", message: getApiErrorMessage(error) }),
  });

  function parseCoverages(): WarrantyPlanCoverageInput[] {
    return coverages.map(({ componentCode, displayName, durationMonths }, index) => ({ componentCode: componentCode.trim(), displayName: displayName.trim(), durationMonths, sortOrder: index }));
  }

  function createPlan(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const request: CreateWarrantyPlanRequest = {
      code: planCode,
      name: planName,
      activationWindowDays: Number(activationWindowDays),
      termsVersion,
      effectiveFrom: new Date().toISOString(),
      coverages: parseCoverages(),
    };
    createPlanMutation.mutate(request);
  }

  function assignPlan(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!variantId || !selectedPlanId) {
      setNotice({
        type: "error",
        message: "Choose a SKU and an active warranty plan.",
      });
      return;
    }
    assignPlanMutation.mutate({
      productVariantId: variantId,
      warrantyPlanId: selectedPlanId,
    });
  }

  function chooseFile(event: ChangeEvent<HTMLInputElement>) {
    setUnitFile(event.target.files?.[0] ?? null);
    setImportNotice(null);
    setImportPreview(null);
  }

  function openAction(
    kind: "retire" | "activate" | "void" | "replace",
    id: string,
    label: string,
  ) {
    setReason("");
    setReplacementId("");
    setReplacementSearch("");
    setReplacementPage(1);
    setActionError(null);
    setAction({ kind, id, label });
  }
  const actionBusy =
    retirePlanMutation.isPending ||
    activateMutation.isPending ||
    voidMutation.isPending ||
    replaceMutation.isPending;
  function confirmAction() {
    if (!action) return;
    const callbacks = {
      onSuccess: () => setAction(null),
      onError: (error: unknown) => setActionError(getApiErrorMessage(error)),
    };
    if (action.kind === "retire") {
      retirePlanMutation.mutate(action.id, callbacks);
      return;
    }
    if (action.kind === "activate") {
      activateMutation.mutate(action.id, callbacks);
      return;
    }
    if (!reason.trim()) {
      setActionError("An operational reason is required.");
      return;
    }
    if (action.kind === "void")
      voidMutation.mutate({ id: action.id, reason: reason.trim() }, callbacks);
    else if (!replacementId.trim())
      setActionError("A replacement unit ID is required.");
    else
      replaceMutation.mutate(
        {
          id: action.id,
          replacementSerializedProductUnitId: replacementId.trim(),
          reason: reason.trim(),
        },
        callbacks,
      );
  }
  const queries = [plansQuery, unitsQuery, warrantiesQuery];
  const header = (
    <AdminPageHeader
      title="Warranties"
      description="Manage plans, serialized units and registrations."
    />
  );
  const unavailable = queries.every(
    (q) => q.error instanceof ApiClientError && q.error.statusCode === 404,
  );
  if (unavailable)
    return (
      <div className="admin-page-grid">
        {header}
        <section className="admin-panel flex flex-col items-center py-12 text-center">
          <ShieldOff
            size={36}
            className="mb-4 text-slate-400"
            aria-hidden="true"
          />
          <h2 className="text-lg font-semibold">
            Warranty management is unavailable
          </h2>
          <p className="mt-2 max-w-md text-sm text-[var(--muted)]">
            Warranty management is not enabled for this workspace.
          </p>
        </section>
      </div>
    );
  if (queries.some((q) => q.isPending))
    return (
      <div className="admin-page-grid">
        {header}
        <div
          className="h-52 animate-pulse rounded-xl bg-slate-200"
          role="status"
          aria-label="Loading warranty management"
        />
      </div>
    );
  if (queries.some((q) => q.isError))
    return (
      <div className="admin-page-grid">
        {header}
        <Notice type="error" title="Warranty data could not be loaded">
          {getApiErrorMessage(
            plansQuery.error ?? unitsQuery.error ?? warrantiesQuery.error,
          )}
          <div className="mt-3">
            <Button onClick={() => queries.forEach((q) => void q.refetch())}>
              Try again
            </Button>
          </div>
        </Notice>
      </div>
    );

  return (
    <div className="admin-page-grid">
      {header}
      {notice ? <Notice type={notice.type} title={notice.message} /> : null}
      <form className="admin-panel flex flex-wrap items-end gap-3" onSubmit={event => {
        event.preventDefault(); setSearch(searchDraft.trim()); setPages({ registrations: 1, units: 1, plans: 1 });
      }}><Field label="Search warranties, units or plans"><TextInput name="search" value={searchDraft} onChange={event => setSearchDraft(event.target.value)} maxLength={150} placeholder="Order code, customer, SKU, masked unit or plan" /></Field><Button type="submit">Search</Button><Button onClick={() => { setSearch(""); setSearchDraft(""); setPages({ registrations: 1, units: 1, plans: 1 }); }}>Reset</Button></form>
      <AdminTabs
        label="Warranty management"
        value={tab} onValueChange={setTab}
        tabs={[
          {
            value: "registrations",
            label: "Registrations",
            content: (
              <section className="admin-panel">
                <h2 className="mb-4 text-base font-semibold">
                  Warranty registrations
                </h2>
                {warrantiesQuery.data?.items.length ? (
                  <TableViewport label="Warranty registrations">
                    <table className="w-full min-w-[720px]">
                      <thead>
                        <tr>
                          <th>Product / unit</th>
                          <th>Order</th>
                          <th>Status</th>
                          <th>Activated</th>
                          <th>Actions</th>
                        </tr>
                      </thead>
                      <tbody>
                        {warrantiesQuery.data.items.map((w) => (
                          <tr
                            key={w.id}
                            className="border-b border-slate-100 last:border-0"
                          >
                            <td>
                              <p className="max-w-xs font-medium">
                                {w.productName}
                              </p>
                              <p className="mt-1 text-xs text-[var(--muted)]">
                                {w.maskedIdentifier}
                              </p>
                            </td>
                            <td>{w.orderCode}</td>
                            <td>
                              <Pill
                                tone={
                                  w.status === 1
                                    ? "green"
                                    : w.status === 0
                                      ? "orange"
                                      : "slate"
                                }
                              >
                                {entitlementStatus(w.status)}
                              </Pill>
                            </td>
                            <td className="text-xs text-[var(--muted)]">
                              {w.activatedAt ? formatDate(w.activatedAt) : "—"}
                            </td>
                            <td>
                              <div className="flex flex-wrap gap-2">
                                <Button
                                  variant="ghost"
                                  onClick={() =>
                                    setSelectedRegistrationId(w.id)
                                  }
                                >
                                  Audit
                                </Button>
                                {w.status === 0 ? (
                                  <Button
                                    disabled={activateMutation.isPending}
                                    onClick={() =>
                                      openAction(
                                        "activate",
                                        w.id,
                                        w.productName,
                                      )
                                    }
                                  >
                                    Activate
                                  </Button>
                                ) : null}
                                {w.status === 1 ? (
                                  <>
                                    <Button
                                      variant="danger"
                                      disabled={voidMutation.isPending}
                                      onClick={() =>
                                        openAction("void", w.id, w.productName)
                                      }
                                    >
                                      Void
                                    </Button>
                                    <Button
                                      disabled={replaceMutation.isPending}
                                      onClick={() =>
                                        openAction(
                                          "replace",
                                          w.id,
                                          w.productName,
                                        )
                                      }
                                    >
                                      Replace
                                    </Button>
                                  </>
                                ) : null}
                              </div>
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </TableViewport>
                ) : (
                  <EmptyState>No warranty registrations yet</EmptyState>
                )}
              </section>
            ),
          },
          {
            value: "units",
            label: "Serialized units",
            content: (
              <div className="grid min-w-0 gap-5">
                <section className="admin-panel">
                  <h2 className="text-base font-semibold">
                    Import serialized units
                  </h2>
                  <p className="mt-1 text-sm text-[var(--muted)]">
                    CSV columns: sku, identifier, identifier_type. Preview the
                    file before importing.
                  </p>
                  <div className="mt-4 grid items-end gap-3 sm:grid-cols-[1fr_auto_auto]">
                    <Field label="CSV file">
                      <input
                        type="file"
                        accept=".csv,text/csv"
                        onChange={chooseFile}
                        disabled={importMutation.isPending}
                        className="admin-input"
                      />
                    </Field>
                    <Button
                      disabled={
                        !unitFile ||
                        previewImportMutation.isPending ||
                        importMutation.isPending
                      }
                      onClick={() =>
                        unitFile && previewImportMutation.mutate(unitFile)
                      }
                    >
                      Preview
                    </Button>
                    <Button
                      variant="primary"
                      disabled={
                        !unitFile ||
                        !importPreview?.isValid ||
                        importMutation.isPending
                      }
                      onClick={() =>
                        unitFile && importMutation.mutate(unitFile)
                      }
                    >
                      {importMutation.isPending
                        ? "Importing..."
                        : "Import units"}
                    </Button>
                  </div>
                  {importNotice ? (
                    <p className="mt-3 text-sm" role="status">
                      {importNotice}
                    </p>
                  ) : null}
                  {importPreview && !importPreview.isValid ? (
                    <Notice
                      type="warning"
                      title="Fix these rows before importing"
                    >
                      <ul className="mt-2 list-disc pl-5">
                        {importPreview.rows
                          .filter((r) => !r.isValid)
                          .slice(0, 25)
                          .map((r) => (
                            <li key={r.rowNumber}>
                              Row {r.rowNumber} ({r.sku || "missing SKU"}):{" "}
                              {r.errors.join(", ")}
                            </li>
                          ))}
                      </ul>
                      {importPreview.failedRows > 25 ? (
                        <p>Showing the first 25 invalid rows.</p>
                      ) : null}
                    </Notice>
                  ) : null}
                </section>
                <section className="admin-panel">
                  <h2 className="mb-4 text-base font-semibold">
                    Serialized units
                  </h2>
                  {unitsQuery.data?.items.length ? (
                    <TableViewport label="Serialized units">
                      <table className="w-full min-w-[720px]">
                        <thead>
                          <tr>
                            <th>Unit / SKU</th>
                            <th>Status</th>
                            <th>Order</th>
                            <th>Assign to order item</th>
                          </tr>
                        </thead>
                        <tbody>
                          {unitsQuery.data.items.map((u) => (
                            <tr
                              key={u.id}
                              className="border-b border-slate-100"
                            >
                              <td>
                                <p className="font-medium">
                                  {u.maskedIdentifier}
                                </p>
                                <p className="mt-1 font-mono text-xs text-[var(--muted)]">
                                  {u.sku}
                                </p>
                              </td>
                              <td>{unitStatus(u.status)}</td>
                              <td>{u.orderCode ?? "—"}</td>
                              <td>
                                {u.status === 0 ? (
                                  <Button disabled={assignUnitMutation.isPending} onClick={() => { assignUnitMutation.reset(); setAssignUnit(u); setOrderItemId(""); }}>Choose order item</Button>
                                ) : (
                                  "—"
                                )}
                              </td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </TableViewport>
                  ) : (
                    <EmptyState>
                      No serialized units yet. Import a CSV to add units.
                    </EmptyState>
                  )}
                </section>
              </div>
            ),
          },
          {
            value: "plans",
            label: "Plans & assignments",
            content: (
              <div className="grid min-w-0 gap-5 xl:grid-cols-2">
                <form onSubmit={createPlan} className="admin-panel">
                  <h2 className="mb-4 text-base font-semibold">
                    Create warranty plan
                  </h2>
                  <div className="grid gap-4">
                    <Field label="Plan code">
                      <TextInput
                        value={planCode}
                        onChange={(e) => setPlanCode(e.target.value)}
                        required
                      />
                    </Field>
                    <Field label="Plan name">
                      <TextInput
                        value={planName}
                        onChange={(e) => setPlanName(e.target.value)}
                        required
                      />
                    </Field>
                    <div className="grid gap-4 sm:grid-cols-2">
                      <Field label="Terms version">
                        <TextInput
                          value={termsVersion}
                          onChange={(e) => setTermsVersion(e.target.value)}
                          required
                        />
                      </Field>
                      <Field label="Activation window (days)">
                        <TextInput
                          type="number"
                          min={1}
                          value={activationWindowDays}
                          onChange={(e) =>
                            setActivationWindowDays(e.target.value)
                          }
                          required
                        />
                      </Field>
                    </div>
                    <fieldset className="grid min-w-0 gap-3"><legend className="mb-2 text-sm font-medium">Coverage components</legend>
                      {coverages.map((coverage, index) => <div key={coverage.key} className="grid min-w-0 gap-2 rounded-lg border p-3 sm:grid-cols-3">
                        <Field label={`Component code ${index + 1}`}><TextInput required maxLength={50} value={coverage.componentCode} onChange={event => setCoverages(current => current.map(item => item.key === coverage.key ? { ...item, componentCode: event.target.value } : item))} /></Field>
                        <Field label={`Component name ${index + 1}`}><TextInput required maxLength={200} value={coverage.displayName} onChange={event => setCoverages(current => current.map(item => item.key === coverage.key ? { ...item, displayName: event.target.value } : item))} /></Field>
                        <Field label={`Coverage months ${index + 1}`}><TextInput required type="number" min={1} max={1200} value={coverage.durationMonths} onChange={event => setCoverages(current => current.map(item => item.key === coverage.key ? { ...item, durationMonths: Number(event.target.value) } : item))} /></Field>
                        <Button disabled={coverages.length <= 1} onClick={() => setCoverages(current => current.filter(item => item.key !== coverage.key))}>Remove component {index + 1}</Button>
                      </div>)}
                      <Button disabled={coverages.length >= 20} onClick={() => setCoverages(current => [...current, { key: crypto.randomUUID(), componentCode: "", displayName: "", durationMonths: 12, sortOrder: current.length }])}>Add coverage component</Button>
                    </fieldset>
                    <Button
                      type="submit"
                      variant="primary"
                      disabled={createPlanMutation.isPending}
                    >
                      {createPlanMutation.isPending
                        ? "Creating..."
                        : "Create plan"}
                    </Button>
                  </div>
                </form>
                <section className="admin-panel">
                  <h2 className="mb-4 text-base font-semibold">
                    Plan assignments
                  </h2>
                  <form onSubmit={assignPlan} className="grid gap-4">
                    <VariantLookup value={variantId} onChange={setVariantId} />
                    <WarrantyPlanLookup value={selectedPlanId} onChange={setSelectedPlanId} />
                    <Button
                      type="submit"
                      disabled={assignPlanMutation.isPending}
                    >
                      {assignPlanMutation.isPending
                        ? "Assigning..."
                        : "Assign plan"}
                    </Button>
                  </form>
                  <div className="mt-6 grid gap-3">
                    {plansQuery.data?.items.map((p) => (
                      <article
                        key={p.id}
                        className="rounded-lg border border-slate-200 p-3"
                      >
                        <div className="flex flex-wrap items-center justify-between gap-2">
                          <p className="font-medium">{p.code}</p>
                          <Pill tone={p.isActive ? "green" : "slate"}>
                            {p.isActive ? "Active" : "Retired"}
                          </Pill>
                        </div>
                        <p className="mt-2 text-xs text-[var(--muted)]">
                          {p.coverages
                            .map(
                              (c) =>
                                `${c.displayName} · ${c.durationMonths} months`,
                            )
                            .join(" / ")}
                        </p>
                        {p.isActive ? (
                          <div className="mt-3">
                            <Button
                              variant="danger"
                              disabled={retirePlanMutation.isPending}
                              onClick={() => openAction("retire", p.id, p.code)}
                            >
                              Retire plan
                            </Button>
                          </div>
                        ) : null}
                      </article>
                    ))}
                  </div>
                </section>
              </div>
            ),
          },
        ]}
      />
      {(tab === "registrations" ? warrantiesQuery.data : tab === "units" ? unitsQuery.data : plansQuery.data) && <Pagination
        data={(tab === "registrations" ? warrantiesQuery.data : tab === "units" ? unitsQuery.data : plansQuery.data)!}
        label={tab === "registrations" ? "warranty registrations" : tab === "units" ? "serialized units" : "warranty plans"}
        busy={plansQuery.isFetching || unitsQuery.isFetching || warrantiesQuery.isFetching}
        onPageChange={page => setPages(current => ({ ...current, [tab]: page }))} />}
      <Modal title="Assign serialized unit" open={!!assignUnit} busy={assignUnitMutation.isPending} onClose={() => setAssignUnit(null)} error={assignUnitMutation.isError ? getApiErrorMessage(assignUnitMutation.error) : undefined} footer={<><Button disabled={assignUnitMutation.isPending} onClick={() => setAssignUnit(null)}>Cancel</Button><Button variant="primary" disabled={!orderItemId || assignUnitMutation.isPending} onClick={() => assignUnit && assignUnitMutation.mutate({ unitId: assignUnit.id, orderItemId })}>Confirm assignment</Button></>}>
        {assignUnit && <><p className="mb-4 text-sm">{assignUnit.sku} - {assignUnit.maskedIdentifier}. Select a purchased item of the same SKU. Packing quantities are checked by the server.</p><WarrantyOrderItemLookup key={assignUnit.id} variantId={assignUnit.productVariantId} value={orderItemId} onChange={setOrderItemId} /></>}
      </Modal>
      <Modal
        title="Registration audit"
        open={Boolean(selectedRegistrationId)}
        onClose={() => setSelectedRegistrationId(null)}
        widthClass="max-w-2xl"
      >
        {selectedWarrantyQuery.isLoading ? (
          <p role="status">Loading audit events...</p>
        ) : null}
        {selectedWarrantyQuery.error ? (
          <Notice type="error" title="Audit could not be loaded">
            {getApiErrorMessage(selectedWarrantyQuery.error)}
            <div className="mt-3">
              <Button
                disabled={selectedWarrantyQuery.isFetching}
                onClick={() => void selectedWarrantyQuery.refetch()}
              >
                Try again
              </Button>
            </div>
          </Notice>
        ) : null}
        {selectedWarrantyQuery.data ? (
          <>
            <p className="mb-4 font-medium">
              {selectedWarrantyQuery.data.productName} ·{" "}
              {selectedWarrantyQuery.data.maskedIdentifier}
            </p>
            <ol className="grid gap-4">
              {selectedWarrantyQuery.data.auditEvents.map((e) => (
                <li key={e.id} className="border-l-2 border-slate-200 pl-4">
                  <p className="font-medium">{auditAction(e.action)}</p>
                  <p className="mt-1 text-xs text-[var(--muted)]">
                    {formatDate(e.occurredAt)} · {e.actorType}
                  </p>
                  {e.reason ? (
                    <p className="mt-2 text-sm text-[var(--muted-strong)]">
                      {e.reason}
                    </p>
                  ) : null}
                </li>
              ))}
            </ol>
          </>
        ) : null}
      </Modal>
      <ConfirmDialog
        open={action?.kind === "retire" || action?.kind === "activate"}
        title={
          action?.kind === "retire"
            ? "Retire warranty plan"
            : "Activate warranty"
        }
        message={
          <>
            {action?.kind === "retire"
              ? `Retire ${action.label}? Existing registrations keep their coverage terms.`
              : `Activate warranty coverage for ${action?.label}?`}
            {actionError ? <Notice type="error" title={actionError} /> : null}
          </>
        }
        busy={actionBusy}
        confirmVariant={action?.kind === "activate" ? "primary" : "danger"}
        confirmLabel={action?.kind === "retire" ? "Retire" : "Activate"}
        onCancel={() => setAction(null)}
        onConfirm={confirmAction}
      />
      <Modal
        open={action?.kind === "void" || action?.kind === "replace"}
        title={
          action?.kind === "void" ? "Void warranty" : "Replace warranty unit"
        }
        busy={actionBusy}
        onClose={() => setAction(null)}
        footer={
          <>
            <Button disabled={actionBusy} onClick={() => setAction(null)}>
              Cancel
            </Button>
            <Button
              variant={action?.kind === "void" ? "danger" : "primary"}
              disabled={actionBusy}
              onClick={confirmAction}
            >
              {actionBusy
                ? "Working..."
                : action?.kind === "void"
                  ? "Void warranty"
                  : "Confirm replacement"}
            </Button>
          </>
        }
      >
        <div className="grid gap-4">
          <p className="text-sm text-[var(--muted-strong)]">{action?.label}</p>
          {action?.kind === "replace" ? (
            <div className="grid gap-3">
              <Field label="Find replacement unit"><TextInput value={replacementSearch} onChange={event => { setReplacementSearch(event.target.value); setReplacementPage(1); setReplacementId(""); }} placeholder="SKU or masked identifier" /></Field>
              {replacementUnits.isLoading && <p role="status">Loading available units...</p>}
              {replacementUnits.isError && <Notice type="error" title="Available units could not be loaded"><Button onClick={() => void replacementUnits.refetch()}>Try again</Button></Notice>}
              <Field label="Replacement unit"><SelectInput value={replacementId} onChange={event => setReplacementId(event.target.value)} disabled={actionBusy || replacementUnits.isFetching}>
                <option value="">Choose an available unit</option>{replacementUnits.data?.items.map(unit => <option key={unit.id} value={unit.id}>{unit.sku} · {unit.maskedIdentifier}</option>)}
              </SelectInput></Field>
              {replacementUnits.data && <Pagination data={replacementUnits.data} label="available units" onPageChange={page => { setReplacementPage(page); setReplacementId(""); }} busy={replacementUnits.isFetching} />}
            </div>
          ) : null}
          {action?.kind === "replace" ? (
            <p className="text-xs text-[var(--muted)]">
              Select an available unit of the same SKU. One stock unit is consumed and the original coverage dates carry forward. The removed unit is kept out of sellable stock.
            </p>
          ) : null}
          <Field label="Operational reason">
            <TextArea
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              disabled={actionBusy}
              rows={3}
            />
          </Field>
          {actionError ? <Notice type="error" title={actionError} /> : null}
        </div>
      </Modal>
    </div>
  );
}
function unitStatus(status: number) {
  return (
    ["Available", "Assigned", "Activated", "Voided", "Replaced", "Returned"][
      status
    ] ?? "Unknown"
  );
}
function entitlementStatus(status: number) {
  return (
    ["Awaiting activation", "Active", "Voided", "Replaced"][status] ?? "Unknown"
  );
}
function auditAction(action: number) {
  return (
    [
      "Unit imported",
      "Unit assigned",
      "Activated",
      "Voided",
      "Returned",
      "Replaced",
    ][action] ?? "Updated"
  );
}
