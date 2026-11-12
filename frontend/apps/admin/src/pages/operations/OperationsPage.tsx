import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ApiClient } from "@workspace-ecommerce/api-client";
import { formatDate } from "@workspace-ecommerce/shared-utils";
import { Link } from "react-router-dom";
import { AdminPageHeader } from "../../components/ui/AdminPageHeader";
import {
  Button,
  ConfirmDialog,
  Field,
  Notice,
  SelectInput,
} from "../../components/ui/AdminUi";
import { getAdminToken } from "../../services/api/adminApi";
import { getApiErrorMessage } from "../../services/api/errors";

interface Queue {
  outbox: string;
  dueCount: number;
  leasedCount: number;
  retryCount: number;
  deadLetterCount: number;
  oldestActiveAt: string | null;
}
interface DeadLetter {
  outbox: string;
  id: string;
  orderId: string | null;
  commandType: string | null;
  attemptCount: number;
  errorCategory: string | null;
  error: string | null;
  deadLetteredAtUtc: string;
}
const client = new ApiClient({
  baseUrl: import.meta.env.VITE_API_BASE_URL ?? "",
  getAccessToken: getAdminToken,
});
export function OperationsPage() {
  const cache = useQueryClient();
  const [limit, setLimit] = useState(50);
  const [selected, setSelected] = useState<DeadLetter | null>(null);
  const summary = useQuery({
    queryKey: ["admin-operations", "summary"],
    queryFn: () =>
      client.get<{ observedAtUtc: string; queues: Queue[] }>(
        "/api/admin/operations/outbox",
      ),
  });
  const letters = useQuery({
    queryKey: ["admin-operations", "dead-letters", limit],
    queryFn: () =>
      client.get<DeadLetter[]>(
        `/api/admin/operations/outbox/dead-letters?limit=${limit}`,
      ),
  });
  const replay = useMutation({
    mutationFn: (value: DeadLetter) =>
      client.post(
        `/api/admin/operations/outbox/${value.outbox}/${value.id}/replay`,
        {},
      ),
    onSuccess: async () => {
      setSelected(null);
      await Promise.all(
        [["admin-operations"], ["admin-dashboard"], ["admin-orders"]].map(
          (queryKey) => cache.invalidateQueries({ queryKey }),
        ),
      );
    },
  });
  return (
    <div className="admin-page-grid">
      <AdminPageHeader
        title="Operations"
        description="Monitor durable email and delivery work. Investigate the cause before replaying a failed command."
        actions={
          <Button
            disabled={summary.isFetching || letters.isFetching}
            onClick={() =>
              void cache.invalidateQueries({ queryKey: ["admin-operations"] })
            }
          >
            Refresh queues
          </Button>
        }
      />
      {summary.isError || letters.isError ? (
        <Notice
          type="error"
          title={getApiErrorMessage(summary.error ?? letters.error)}
        >
          <Button
            onClick={() =>
              void cache.invalidateQueries({ queryKey: ["admin-operations"] })
            }
          >
            Try again
          </Button>
        </Notice>
      ) : null}
      {summary.isPending && <p role="status">Loading queues...</p>}
      {replay.isSuccess && (
        <Notice
          type="success"
          title="Replay queued for background processing"
        />
      )}
      <div className="grid gap-4 md:grid-cols-2">
        {summary.data?.queues.map((queue) => (
          <section key={queue.outbox} className="admin-panel">
            <h2 className="font-semibold">
              {queue.outbox === "customer-email"
                ? "Customer email"
                : "Shipment commands"}
            </h2>
            <dl className="mt-4 grid grid-cols-2 gap-3 text-sm">
              {[
                ["Due", queue.dueCount],
                ["Processing", queue.leasedCount],
                ["Retrying", queue.retryCount],
                ["Terminal failure records", queue.deadLetterCount],
              ].map(([label, count]) => (
                <div key={label}>
                  <dt className="text-[var(--muted)]">{label}</dt>
                  <dd className="text-2xl font-semibold tabular-nums">
                    {count}
                  </dd>
                </div>
              ))}
            </dl>
            <p className="mt-3 text-xs text-[var(--muted)]">
              Oldest active:{" "}
              {queue.oldestActiveAt ? formatDate(queue.oldestActiveAt) : "None"}
            </p>
          </section>
        ))}
      </div>
      <section className="admin-panel grid gap-4">
        <div className="flex flex-wrap items-end justify-between gap-3">
          <div>
            <h2 className="font-semibold">Failed background work</h2>
            <p className="mt-1 text-sm text-[var(--muted)]">
              Latest terminal failures only. Replays create a new durable
              command and retain the failed record.
            </p>
          </div>
          <Field label="Recent failure limit">
            <SelectInput
              value={limit}
              onChange={(event) => setLimit(Number(event.target.value))}
            >
              <option value={50}>Latest 50</option>
              <option value={100}>Latest 100</option>
            </SelectInput>
          </Field>
        </div>
        {letters.isPending && <p role="status">Loading failed work...</p>}
        {letters.data?.length === 0 && (
          <p className="py-6 text-center text-sm text-[var(--muted)]">
            No terminal failures.
          </p>
        )}
        {letters.data?.map((item) => (
          <article
            key={`${item.outbox}:${item.id}`}
            className="grid gap-2 rounded-lg border p-4 text-sm"
          >
            <p className="font-medium">
              {item.outbox} · {item.commandType || "Email"} ·{" "}
              {item.attemptCount} attempts
            </p>
            <p className="text-xs text-[var(--muted)]">
              {formatDate(item.deadLetteredAtUtc)}
            </p>
            <p className="break-words">
              {item.errorCategory}: {item.error || "No error details available"}
            </p>
            <div className="flex flex-wrap gap-2">
              {item.orderId && (
                <Link
                  className="admin-button"
                  to={`/orders?orderId=${item.orderId}`}
                >
                  View order
                </Link>
              )}
              <Button
                disabled={replay.isPending}
                onClick={() => {
                  replay.reset();
                  setSelected(item);
                }}
              >
                Review replay
              </Button>
            </div>
          </article>
        ))}
      </section>
      <ConfirmDialog
        open={!!selected}
        busy={replay.isPending}
        confirmVariant="primary"
        title="Replay failed background work"
        confirmLabel="Queue replay"
        message={
          <>
            <p>
              {selected?.outbox === "customer-email"
                ? "Queue another delivery attempt for this customer email after resolving its failure."
                : "Queue the shipment command again after checking the provider and order state."}
            </p>
            {replay.isError && (
              <p role="alert">{getApiErrorMessage(replay.error)}</p>
            )}
          </>
        }
        onCancel={() => setSelected(null)}
        onConfirm={() => selected && replay.mutate(selected)}
      />
    </div>
  );
}
