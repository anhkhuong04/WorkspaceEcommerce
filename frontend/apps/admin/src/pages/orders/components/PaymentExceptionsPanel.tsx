import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Controller, useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { formatDate, formatMoney } from "@workspace-ecommerce/shared-utils";
import {
  Button,
  ConfirmDialog,
  Field,
  Modal,
  Notice,
  Pill,
  TextArea,
  TextInput,
} from "../../../components/ui/AdminUi";
import { Pagination } from "../../../components/ui/Pagination";
import {
  paymentExceptionApi,
  type PaymentExceptionDto,
  type RecordPaymentExceptionRequest,
  type ResolvePaymentExceptionRequest,
} from "../../../services/api/paymentExceptionApi";
import { getApiErrorMessage } from "../../../services/api/errors";

type Operation =
  | { kind: "record"; body: RecordPaymentExceptionRequest }
  | { kind: "resolve"; id: string; body: ResolvePaymentExceptionRequest };
export function PaymentExceptionsPanel({ orderId }: { orderId: string }) {
  const cache = useQueryClient();
  const [page, setPage] = useState(1);
  const [editing, setEditing] = useState<"record" | PaymentExceptionDto | null>(
    null,
  );
  const [prepared, setPrepared] = useState<Operation | null>(null);
  const query = useQuery({
    queryKey: ["admin-payment-exceptions", orderId, page],
    queryFn: () => paymentExceptionApi.list(orderId, page),
  });
  const mutation = useMutation({
    mutationFn: (op: Operation) =>
      op.kind === "record"
        ? paymentExceptionApi.record(orderId, op.body)
        : paymentExceptionApi.resolve(orderId, op.id, op.body),
    onSuccess: async () => {
      setEditing(null);
      setPrepared(null);
      await Promise.all(
        [
          ["admin-payment-exceptions", orderId],
          ["admin-order-payment", orderId],
          ["admin-order", orderId],
          ["admin-orders"],
          ["admin-dashboard"],
        ].map((queryKey) => cache.invalidateQueries({ queryKey })),
      );
    },
  });
  return (
    <section className="grid gap-3 rounded-xl border p-4">
      <h3 className="font-medium">Payment differences</h3>
      <p className="text-sm text-[var(--muted)]">
        Record verified short / excess receipts before full payment
        confirmation. Settlement evidence is required. Resolving an open order
        also records the reconciled full collection; cancelled orders require
        the received funds to be returned.
      </p>
      {query.isPending && <p role="status">Loading payment differences...</p>}
      {query.isError && (
        <Notice type="error" title={getApiErrorMessage(query.error)}>
          <Button onClick={() => void query.refetch()}>
            Retry payment differences
          </Button>
        </Notice>
      )}
      {query.data?.canRecord && (
        <Button
          onClick={() => {
            mutation.reset();
            setEditing("record");
          }}
        >
          Record short / excess payment
        </Button>
      )}
      {query.data?.exceptions.items.map((item) => (
        <article
          key={item.id}
          className="grid min-w-0 gap-2 border-t pt-3 text-sm"
        >
          <div className="flex flex-wrap items-center justify-between gap-2">
            <p className="font-medium">
              Received {formatMoney(item.actualAmount)} · expected{" "}
              {formatMoney(item.expectedAmount)}
            </p>
            <Pill tone={item.settledAt ? "green" : "orange"}>
              {item.settledAt ? "Reconciled" : "Open difference"}
            </Pill>
          </div>
          <p className="break-all">
            {item.reference} · {formatDate(item.recordedAt)} · {item.actor}
          </p>
          <p className="break-words">{item.reason}</p>
          {item.settledAt ? (
            <p className="break-words">
              {item.settlement}: {formatMoney(item.settlementAmount ?? 0)} ·{" "}
              {item.settlementReference} · {formatDate(item.settledAt)} ·{" "}
              {item.settledBy}
              <br />
              {item.settlementReason}
            </p>
          ) : (
            <Button
              onClick={() => {
                mutation.reset();
                setEditing(item);
              }}
            >
              {item.requiredSettlement}: {formatMoney(item.requiredAmount)}
            </Button>
          )}
        </article>
      ))}
      {query.data && (
        <Pagination
          data={query.data.exceptions}
          label="payment differences"
          onPageChange={setPage}
          busy={query.isFetching}
        />
      )}
      <Modal
        title={
          editing === "record"
            ? "Record payment difference"
            : "Record verified settlement"
        }
        open={editing !== null}
        busy={mutation.isPending}
        onClose={() => setEditing(null)}
        error={
          mutation.isError ? getApiErrorMessage(mutation.error) : undefined
        }
      >
        {editing && (
          <ExceptionForm
            key={editing === "record" ? "record" : editing.id}
            value={editing === "record" ? null : editing}
            busy={mutation.isPending}
            onReview={setPrepared}
          />
        )}
      </Modal>
      <ConfirmDialog
        open={!!prepared}
        busy={mutation.isPending}
        title="Confirm verified payment evidence"
        confirmVariant="primary"
        confirmLabel="Record evidence"
        message={
          <>
            <p>
              {prepared?.kind === "record"
                ? "Record the actual receipt and keep the order unpaid until the difference is reconciled."
                : "Record this already completed settlement. For an open order, the reconciled full collection is confirmed in the same transaction. This does not send money."}
            </p>
            {mutation.isError && (
              <p role="alert">{getApiErrorMessage(mutation.error)}</p>
            )}
          </>
        }
        onCancel={() => setPrepared(null)}
        onConfirm={() => prepared && mutation.mutate(prepared)}
      />
    </section>
  );
}
function ExceptionForm({
  value,
  busy,
  onReview,
}: {
  value: PaymentExceptionDto | null;
  busy: boolean;
  onReview: (operation: Operation) => void;
}) {
  const [requestId] = useState(() => crypto.randomUUID());
  const schema = z
    .object({
      amount: z.number().int().min(1).max(99999999999999),
      reference: z.string().trim().min(1).max(120),
      reason: z.string().trim().min(1).max(500),
    })
    .superRefine((data, context) => {
      if (value && data.amount !== value.requiredAmount)
        context.addIssue({
          code: "custom",
          path: ["amount"],
          message: "Enter the exact required settlement amount.",
        });
    });
  const form = useForm({
    resolver: zodResolver(schema),
    defaultValues: {
      amount: value?.requiredAmount ?? 0,
      reference: "",
      reason: "",
    },
  });
  return (
    <form
      noValidate
      className="grid gap-4"
      onSubmit={form.handleSubmit((data) =>
        onReview(
          value
            ? { kind: "resolve", id: value.id, body: data }
            : {
                kind: "record",
                body: {
                  requestId,
                  actualAmount: data.amount,
                  reference: data.reference,
                  reason: data.reason,
                },
              },
        ),
      )}
    >
      {value && (
        <Notice
          title={`${value.requiredSettlement}: ${formatMoney(value.requiredAmount)}`}
        >
          Use the distinct incoming / outgoing settlement reference after
          checking bank or carrier evidence.
        </Notice>
      )}
      <Controller
        control={form.control}
        name="amount"
        render={({ field, fieldState }) => (
          <Field
            label={
              value
                ? "Verified settlement amount (VND)"
                : "Actually received (VND)"
            }
            error={fieldState.error?.message}
          >
            <TextInput
              type="number"
              min={1}
              step={1}
              value={field.value}
              onChange={(event) => field.onChange(Number(event.target.value))}
            />
          </Field>
        )}
      />
      <Controller
        control={form.control}
        name="reference"
        render={({ field, fieldState }) => (
          <Field
            label="Evidence transaction reference"
            error={fieldState.error?.message}
          >
            <TextInput {...field} />
          </Field>
        )}
      />
      <Controller
        control={form.control}
        name="reason"
        render={({ field, fieldState }) => (
          <Field
            label="Verified evidence / reason"
            error={fieldState.error?.message}
          >
            <TextArea {...field} />
          </Field>
        )}
      />
      <Button type="submit" variant="primary" disabled={busy}>
        Review evidence
      </Button>
    </form>
  );
}
