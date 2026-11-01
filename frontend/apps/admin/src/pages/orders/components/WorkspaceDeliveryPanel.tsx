import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { zodResolver } from "@hookform/resolvers/zod";
import { useForm } from "react-hook-form";
import { z } from "zod";
import {
  WorkspaceShipmentAction as Action,
  type WorkspaceShipmentRequest,
} from "@workspace-ecommerce/api-types";
import { Button, ConfirmDialog, Notice } from "../../../components/ui/AdminUi";
import { getApiErrorMessage } from "../../../services/api/errors";
import { adminApi } from "../../../services/api/adminApi";
import { workspaceShipmentApi } from "../../../services/api/workspaceShipmentApi";

const labels = [
  "Assign delivery",
  "Dispatch delivery",
  "Record delivery",
  "Record failed delivery",
  "Retry delivery",
  "Cancel order and delivery",
];
const schema = z.object({
  reason: z.string().trim().min(1).max(500),
  assignedTo: z.string().trim().max(120),
});
export function WorkspaceDeliveryPanel({ orderId }: { orderId: string }) {
  const cache = useQueryClient();
  const [action, setAction] = useState<Action | null>(null);
  const [command, setCommand] = useState<WorkspaceShipmentRequest | null>(null);
  const [page, setPage] = useState(1);
  const query = useQuery({
    queryKey: ["workspace-shipment", orderId],
    queryFn: () => workspaceShipmentApi.get(orderId),
  });
  const history = useQuery({
    queryKey: ["workspace-shipment-history", orderId, page],
    queryFn: () => workspaceShipmentApi.history(orderId, page),
  });
  const form = useForm<z.infer<typeof schema>>({
    resolver: zodResolver(schema),
    defaultValues: { reason: "", assignedTo: "" },
  });
  const mutation = useMutation({
    mutationFn: async () => {
      if (!command) throw new Error("Review an operation first.");
      if (command.action === Action.Cancel)
        await adminApi.updateOrderStatus(orderId, {
          status: 6,
          cancellationReason: command.reason,
          expectedShipmentRevision: command.expectedRevision,
        });
      else await workspaceShipmentApi.act(orderId, command);
    },
    onSuccess: async () => {
      setAction(null);
      setCommand(null);
      form.reset();
      await Promise.all(
        [
          ["workspace-shipment", orderId],
          ["workspace-shipment-history", orderId],
          ["admin-order", orderId],
          ["admin-order-shipment", orderId],
          ["admin-order-payment", orderId],
          ["admin-orders"],
          ["admin-dashboard"],
        ].map((queryKey) => cache.invalidateQueries({ queryKey })),
      );
    },
  });
  const data = query.data;
  if (query.isLoading)
    return <p role="status">Loading WorkspaceEC delivery...</p>;
  if (query.isError || !data)
    return (
      <Notice type="error" title="Local delivery could not be loaded">
        <Button onClick={() => void query.refetch()}>Reload delivery</Button>
      </Notice>
    );
  const allowed = [
    data.canAssign,
    data.canDispatch,
    data.canDeliver,
    data.canFailDelivery,
    data.canRetryDelivery,
    data.canCancel,
  ];
  return (
    <section className="grid gap-3 rounded-xl border border-slate-200 p-4">
      <h3>WorkspaceEC delivery</h3>
      <p>
        {data.trackingCode} · {data.status} · revision {data.revision}
      </p>
      <p>Assigned reference: {data.assignedTo ?? "unassigned"}</p>
      <p>
        Dispatch requires order confirmation and paid prepaid orders. COD
        delivery waits for confirmed full collection before completion.
      </p>
      {mutation.isSuccess && <p role="status">Delivery operation recorded.</p>}
      {mutation.isError && (
        <Notice type="error" title={getApiErrorMessage(mutation.error)}>
          <Button
            onClick={() => {
              setCommand(null);
              mutation.reset();
              void query.refetch();
            }}
          >
            Reload latest delivery
          </Button>
        </Notice>
      )}
      <div className="flex flex-wrap gap-2">
        {labels.map((label, index) =>
          allowed[index] ? (
            <Button
              key={label}
              disabled={mutation.isPending}
              onClick={() => {
                setAction(index);
                mutation.reset();
                form.reset({ reason: "", assignedTo: data.assignedTo ?? "" });
              }}
            >
              {label}
            </Button>
          ) : null,
        )}
      </div>
      {action !== null && (
        <form
          className="grid gap-3"
          onSubmit={form.handleSubmit((values) => {
            if (action === Action.Assign && !values.assignedTo) {
              form.setError("assignedTo", {
                message: "An operational assignee reference is required.",
              });
              return;
            }
            setCommand({
              requestId: crypto.randomUUID(),
              expectedRevision: data.revision,
              action,
              reason: values.reason,
              assignedTo: action === Action.Assign ? values.assignedTo : null,
            });
          })}
        >
          {action === Action.Assign && (
            <label>
              Assignee reference
              <input
                className="admin-input w-full"
                aria-label="Assignee reference"
                {...form.register("assignedTo")}
              />
              {form.formState.errors.assignedTo && (
                <span role="alert">
                  {form.formState.errors.assignedTo.message}
                </span>
              )}
            </label>
          )}
          <label>
            Operation reason
            <input
              className="admin-input w-full"
              aria-label="Operation reason"
              {...form.register("reason")}
            />
            {form.formState.errors.reason && (
              <span role="alert">{form.formState.errors.reason.message}</span>
            )}
          </label>
          <Button type="submit" disabled={mutation.isPending}>
            Review delivery operation
          </Button>
          <Button onClick={() => setAction(null)} disabled={mutation.isPending}>
            Close operation
          </Button>
        </form>
      )}
      <h4>Private delivery audit</h4>
      {history.isError && (
        <Notice type="error" title="Delivery audit unavailable">
          <Button onClick={() => void history.refetch()}>Reload audit</Button>
        </Notice>
      )}
      {history.data?.items.map((entry) => (
        <details key={entry.id}>
          <summary>
            Revision {entry.revision} · {entry.status}
          </summary>
          <p>
            {entry.reason} · {entry.actor} ·{" "}
            {new Date(entry.occurredAtUtc).toLocaleString()}
          </p>
        </details>
      ))}
      <div className="flex gap-2">
        <Button
          disabled={page === 1 || history.isFetching}
          onClick={() => setPage(page - 1)}
        >
          Previous delivery audit
        </Button>
        <Button
          disabled={!history.data?.hasNextPage || history.isFetching}
          onClick={() => setPage(page + 1)}
        >
          Next delivery audit
        </Button>
      </div>
      <ConfirmDialog
        open={!!command}
        title={command ? labels[command.action] : "Delivery operation"}
        message={
          <>
            <p>
              Apply to saved revision {command?.expectedRevision}?{" "}
              {command?.action === Action.Cancel
                ? "The existing order cancellation workflow handles stock and payment."
                : "This operation is audited. Recording delivery does not confirm COD collection."}
            </p>
            {mutation.isError && (
              <p role="alert">{getApiErrorMessage(mutation.error)}</p>
            )}
          </>
        }
        busy={mutation.isPending}
        confirmLabel="Confirm delivery operation"
        onCancel={() => setCommand(null)}
        onConfirm={() => mutation.mutate()}
      />
    </section>
  );
}
