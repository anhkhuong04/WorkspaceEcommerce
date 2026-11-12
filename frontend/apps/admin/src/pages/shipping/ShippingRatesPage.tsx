import { useCallback, useEffect, useState } from "react";
import { useBeforeUnload, useBlocker } from "react-router-dom";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import type {
  SaveShippingRatePlanRequest,
  ShippingRatePlan,
  ShippingRatePreview,
} from "@workspace-ecommerce/api-types";
import { AdminPageHeader } from "../../components/ui/AdminPageHeader";
import { Button, ConfirmDialog, Notice } from "../../components/ui/AdminUi";
import {
  shippingKeys,
  useShippingCatalog,
  useShippingRates,
} from "../../hooks/queries/useShippingRates";
import { shippingApi } from "../../services/api/shippingApi";
import { getApiErrorMessage } from "../../services/api/errors";
import { RatePlanEditor } from "./RatePlanEditor";
import { RatePlanHistory, RatePlanPreview } from "./RatePlanTools";

const statuses = ["Draft", "Published", "Retired"];
export function ShippingRatesPage() {
  const cache = useQueryClient();
  const [page, setPage] = useState(1);
  const [weightPage, setWeightPage] = useState(1);
  const [id, setId] = useState<string | null>(null);
  const [creating, setCreating] = useState(false);
  const [reason, setReason] = useState("");
  const [preview, setPreview] = useState<ShippingRatePreview | null>(null);
  const [action, setAction] = useState<"publish" | "retire" | null>(null);
  const [saved, setSaved] = useState(false);
  const [dirty, setDirty] = useState(false);
  const [pendingSelection, setPendingSelection] = useState<{ id: string | null } | null>(null);
  const [editorVersion, setEditorVersion] = useState(0);
  const catalog = useShippingCatalog();
  const plans = useShippingRates(page);
  const active = useQuery({
    queryKey: [...shippingKeys, "active"],
    queryFn: () => shippingApi.plans(1, 1),
  });
  const detail = useQuery({
    queryKey: [...shippingKeys, "plan", id],
    queryFn: () => shippingApi.plan(id!),
    enabled: !!id,
  });
  const weights = useQuery({
    queryKey: [...shippingKeys, "weights", weightPage],
    queryFn: () => shippingApi.weights(weightPage),
  });
  const plan = detail.data;
  async function refresh(value: ShippingRatePlan) {
    setId(value.id);
    setCreating(false);
    setPreview(null);
    setReason("");
    setSaved(true);
    setDirty(false);
    setAction(null);
    cache.setQueryData([...shippingKeys, "plan", value.id], value);
    await cache.invalidateQueries({ queryKey: shippingKeys });
  }
  const save = useMutation({
    mutationFn: (body: SaveShippingRatePlanRequest) =>
      shippingApi.save(creating ? null : id, body),
    onSuccess: refresh,
  });
  const lifecycle = useMutation({
    mutationFn: async (operation: "publish" | "retire" | "clone") => {
      if (!plan) throw new Error("Select a plan");
      const body = { expectedRevision: plan.revision, reason: reason.trim() };
      if (operation === "clone")
        return shippingApi.clone(plan.id, {
          ...body,
          name: `${plan.name.slice(0, 108)} (copy)`,
        });
      if (operation === "retire") return shippingApi.retire(plan.id, body);
      const published = active.data?.items[0];
      return shippingApi.publish(plan.id, {
        ...body,
        expectedActivePlanId: published?.id ?? null,
        expectedActiveRevision: published?.revision ?? null,
      });
    },
    onSuccess: refresh,
  });
  function applySelection(value: string | null) {
    setEditorVersion(version => version + 1);
    setId(value);
    setCreating(value === null);
    setPreview(null);
    setSaved(false);
    setDirty(false);
    save.reset();
    lifecycle.reset();
  }
  const busy = save.isPending || lifecycle.isPending;
  const blocker = useBlocker(dirty || busy);
  useEffect(() => { if (!dirty && !busy && blocker.state === "blocked") blocker.proceed(); }, [dirty, busy, blocker]);
  useBeforeUnload(useCallback(event => {
    if (dirty || busy) { event.preventDefault(); event.returnValue = ""; }
  }, [dirty, busy]));
  function select(value: string | null) {
    if (busy) return;
    if (dirty) setPendingSelection({ id: value }); else applySelection(value);
  }
  const error = save.error ?? lifecycle.error;
  return (
    <div className="admin-page-grid">
      <AdminPageHeader
        title="Shipping rates"
        description="WorkspaceEC zones, versioned rates and readiness. Prices use whole VND; weights use grams."
        actions={<Button disabled={busy} onClick={() => select(null)}>New rate plan</Button>}
      />
      {saved && <Notice type="success" title="Shipping configuration saved" />}
      {error && (
        <Notice type="error" title={getApiErrorMessage(error)}>
          <p>
            Reloading replaces the unsaved draft with the latest revision. Review and reapply your changes before retrying.
          </p>
          <Button
            onClick={() => {
              void cache.invalidateQueries({ queryKey: shippingKeys });
              save.reset();
              lifecycle.reset();
            }}
          >
            Reload latest revision
          </Button>
        </Notice>
      )}
      {[catalog, plans, detail, active, weights].some((q) => q.isError) && (
        <Notice type="error" title="Shipping data could not be loaded">
          <Button
            onClick={() =>
              void cache.invalidateQueries({ queryKey: shippingKeys })
            }
          >
            Try again
          </Button>
        </Notice>
      )}
      <section className="admin-panel grid gap-3">
        <h2>Rate plans</h2>
        {plans.isLoading && <p role="status">Loading rate plans...</p>}
        {plans.data?.totalCount === 0 && (
          <p>
            No rate plans yet. Enter approved commercial prices before
            publishing.
          </p>
        )}
        {plans.data?.items.map((p) => (
          <Button
            key={p.id}
            disabled={busy}
            onClick={() => select(p.id)}
            aria-pressed={p.id === id}
          >
            {p.name} · {statuses[p.status]} · revision {p.revision}
          </Button>
        ))}
        <div className="flex gap-2">
          <Button
            disabled={page === 1 || plans.isFetching}
            onClick={() => setPage(page - 1)}
          >
            Previous plans
          </Button>
          <span>Page {page}</span>
          <Button
            disabled={!plans.data?.hasNextPage || plans.isFetching}
            onClick={() => setPage(page + 1)}
          >
            Next plans
          </Button>
        </div>
      </section>
      {detail.isFetching && id && (
        <p role="status">Loading selected revision...</p>
      )}
      {catalog.data && (creating || plan) && (
        <section className="admin-panel grid gap-4">
          <h2>
            {creating
              ? "New draft"
              : `${plan!.name} · ${statuses[plan!.status]} · revision ${plan!.revision}`}
          </h2>
          {plan && !creating && (
            <>
              <p>Catalog: {plan.catalogVersion}</p>
              <p role="status">
                Coverage: {plan.coverage.isComplete ? "complete" : "incomplete"}
                . Missing:{" "}
                {plan.coverage.missingProvinceCodes.join(", ") || "none"}.
                Unknown:{" "}
                {plan.coverage.unknownProvinceCodes.join(", ") || "none"}.
              </p>
              {plan.coverage.errors.map((e) => (
                <p role="alert" key={e}>
                  {e}
                </p>
              ))}
            </>
          )}
          {creating || plan?.status === 0 ? (
            <RatePlanEditor
              key={`${creating ? "new" : `${plan!.id}:${plan!.revision}`}:${editorVersion}`}
              plan={creating ? null : plan!}
              catalog={catalog.data}
              busy={busy}
              onSave={(body) => save.mutate(body)}
              onDirtyChange={setDirty}
            />
          ) : (
            <div className="grid gap-3">
              {plan?.zones.map((z) => (
                <p key={z.code}>
                  {z.name} ({z.code}): {z.baseFee.toLocaleString()} VND /{" "}
                  {z.baseWeightGrams} g + {z.stepFee.toLocaleString()} VND /{" "}
                  {z.stepWeightGrams} g. Maximum:{" "}
                  {z.maximumWeightGrams ?? "unlimited"}. Provinces:{" "}
                  {z.provinceCodes.join(", ")}.
                </p>
              ))}
            </div>
          )}
          {!creating && plan && (
            <>
              <label>
                Lifecycle reason
                <input
                  className="admin-input w-full"
                  maxLength={500}
                  value={reason}
                  onChange={(e) => setReason(e.target.value)}
                />
              </label>
              <div className="flex flex-wrap gap-2">
                <Button
                  disabled={busy || !reason.trim()}
                  onClick={() => lifecycle.mutate("clone")}
                >
                  Clone into new draft
                </Button>
                {plan.status === 0 && (
                  <Button
                    disabled={
                      busy ||
                      !reason.trim() ||
                      !plan.coverage.isComplete ||
                      preview?.revision !== plan.revision ||
                      dirty ||
                      !active.isSuccess
                    }
                    onClick={() => setAction("publish")}
                  >
                    Publish plan
                  </Button>
                )}
                {plan.status === 1 && (
                  <Button
                    disabled={busy || !reason.trim()}
                    onClick={() => setAction("retire")}
                  >
                    Retire plan
                  </Button>
                )}
              </div>
              <p>
                Publish requires complete coverage and a successful preview of
                this saved revision.
              </p>
            </>
          )}
        </section>
      )}
      {plan && !creating && catalog.data && (
        <>
          <RatePlanPreview
            key={`${plan.id}:${plan.revision}`}
            plan={plan}
            catalog={catalog.data}
            onPreview={setPreview}
            onInvalidate={() => setPreview(null)}
            busy={busy}
          />
          <RatePlanHistory key={plan.id} id={plan.id} />
        </>
      )}
      <section className="admin-panel grid gap-3">
        <h2>Weight readiness</h2>
        <p>
          {weights.data?.totalCount ?? "…"} variants with missing or invalid
          actual weight (including inactive variants).
        </p>
        {weights.data?.items.map((w) => (
          <p key={w.productVariantId}>
            {w.sku} · {w.name} · {w.weightKg ?? "unknown"} kg ·{" "}
            {w.isActive ? "active" : "inactive"}
          </p>
        ))}
        <div className="flex gap-2">
          <Button
            disabled={weightPage === 1 || weights.isFetching}
            onClick={() => setWeightPage(weightPage - 1)}
          >
            Previous weights
          </Button>
          <Button
            disabled={!weights.data?.hasNextPage || weights.isFetching}
            onClick={() => setWeightPage(weightPage + 1)}
          >
            Next weights
          </Button>
        </div>
      </section>
      <ConfirmDialog
        open={pendingSelection !== null || blocker.state === "blocked"}
        title={busy ? "Saving shipping changes" : "Discard unsaved changes?"}
        message={busy ? "Wait for the current operation to finish before leaving this page." : "Your draft edits have not been saved. Discard them and continue?"}
        confirmLabel="Discard and continue" busy={busy}
        onCancel={() => { setPendingSelection(null); if (blocker.state === "blocked") blocker.reset(); }}
        onConfirm={() => {
          if (pendingSelection) { applySelection(pendingSelection.id); setPendingSelection(null); }
          if (blocker.state === "blocked") blocker.proceed();
        }} />
      <ConfirmDialog
        open={action !== null}
        title={
          action === "publish"
            ? "Publish these shipping rates?"
            : "Retire these shipping rates?"
        }
        message={<>
          {action === "publish"
            ? `This replaces ${active.data?.items[0]?.name ?? "no active plan"}. Valid existing quotes retain their accepted price. Review the saved preview and coverage first.`
            : "WorkspaceEC cannot issue new quotes without an active plan. Existing valid quotes and accepted orders keep their price."
          }
          {lifecycle.isError && <p role="alert">{getApiErrorMessage(lifecycle.error)}</p>}
        </>}
        confirmLabel={
          action === "publish" ? "Confirm publish" : "Confirm retire"
        }
        busy={lifecycle.isPending}
        onCancel={() => setAction(null)}
        onConfirm={() => action && lifecycle.mutate(action)}
      />
    </div>
  );
}
