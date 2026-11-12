import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Controller, useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import type {
  AdminOrderDto,
  OrderReturnCaseDto,
  OrderReturnSummaryDto,
  CreateOrderReturnRequest,
  ReceiveOrderReturnRequest,
  RefundOrderReturnRequest,
} from "@workspace-ecommerce/api-types";
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
import { adminApi } from "../../../services/api/adminApi";
import { getApiErrorMessage } from "../../../services/api/errors";

type Action =
  | { kind: "create" | "reject-request" }
  | { kind: "receive" | "refund" | "reject"; id: string };
type Operation =
  | { kind: "create"; body: CreateOrderReturnRequest }
  | { kind: "receive"; id: string; body: ReceiveOrderReturnRequest }
  | { kind: "refund"; id: string; body: RefundOrderReturnRequest }
  | {
      kind: "reject";
      id: string;
      body: { expectedRevision: number; reason: string };
    }
  | { kind: "reject-request"; body: { reason: string } };
const labels = [
  "Awaiting receipt",
  "Rejected",
  "Received · refund pending",
  "Refund recorded",
  "Closed · no refund due",
];

export function OrderReturnsPanel({ order }: { order: AdminOrderDto }) {
  const cache = useQueryClient();
  const [page, setPage] = useState(1);
  const [action, setAction] = useState<Action | null>(null);
  const [prepared, setPrepared] = useState<Operation | null>(null);
  const query = useQuery({
    queryKey: ["admin-order-returns", order.id, page],
    queryFn: () => adminApi.getOrderReturns(order.id, page),
  });
  const mutation = useMutation({
    mutationFn: (operation: Operation) => {
      switch (operation.kind) {
        case "create":
          return adminApi.createOrderReturn(order.id, operation.body);
        case "receive":
          return adminApi.receiveOrderReturn(
            order.id,
            operation.id,
            operation.body,
          );
        case "refund":
          return adminApi.refundOrderReturn(
            order.id,
            operation.id,
            operation.body,
          );
        case "reject":
          return adminApi.rejectOrderReturn(
            order.id,
            operation.id,
            operation.body,
          );
        case "reject-request":
          return adminApi.rejectOrderReturnRequest(
            order.id,
            operation.body.reason,
          );
      }
    },
    onSuccess: async (value) => {
      setAction(null);
      setPrepared(null);
      setPage(1);
      cache.setQueryData(["admin-order-returns", order.id, 1], value);
      await Promise.all(
        [
          ["admin-order-returns", order.id],
          ["admin-order", order.id],
          ["admin-order-payment", order.id],
          ["admin-orders"],
          ["admin-products"],
          ["admin-dashboard"],
          ["admin", "warranties"],
          ["admin", "warranty-units"],
        ].map((queryKey) => cache.invalidateQueries({ queryKey })),
      );
    },
  });
  const data = query.data;
  const selected =
    action && "id" in action
      ? data?.returns.items.find((item) => item.id === action.id)
      : undefined;
  function open(value: Action) {
    mutation.reset();
    setPrepared(null);
    setAction(value);
  }
  if (query.isLoading) return <p role="status">Loading returns...</p>;
  if (query.isError || !data)
    return (
      <Notice type="error" title="Returns could not be loaded">
        <Button onClick={() => void query.refetch()}>Try again</Button>
      </Notice>
    );
  return (
    <section className="grid gap-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h3 className="text-lg font-semibold">Returns and refunds</h3>
          <p className="mt-1 text-sm text-[var(--muted)]">
            Approve quantities, inspect actual goods, then record a verified
            refund.
          </p>
        </div>
        {data.canCreate && (
          <Button variant="primary" onClick={() => open({ kind: "create" })}>
            Approve return items
          </Button>
        )}
      </div>
      {data.requestedAt && (
        <Notice
          title={
            data.reviewedAt
              ? `Customer request: ${data.decision}`
              : "Customer return request awaits review"
          }
        >
          <p>{order.returnReason}</p>
          {data.decisionReason && <p>{data.decisionReason}</p>}
          {!data.reviewedAt && (
            <Button
              className="mt-2"
              variant="danger"
              onClick={() => open({ kind: "reject-request" })}
            >
              Reject customer request
            </Button>
          )}
        </Notice>
      )}
      {data.eligibilityMessage && (
        <p className="text-sm text-[var(--muted)]">{data.eligibilityMessage}</p>
      )}
      {mutation.isSuccess && (
        <Notice type="success" title="Return operation recorded" />
      )}
      {data.returns.items.length === 0 && (
        <p className="rounded-lg bg-slate-50 p-5 text-sm text-[var(--muted)]">
          No return cases recorded.
        </p>
      )}
      {data.returns.items.map((value) => (
        <article key={value.id} className="grid gap-3 rounded-xl border p-4">
          <div className="flex flex-wrap justify-between gap-2">
            <p className="font-medium">
              Return {value.id.slice(0, 8).toUpperCase()}
            </p>
            <Pill
              tone={
                value.status === 3
                  ? "green"
                  : value.status === 1
                    ? "red"
                    : "orange"
              }
            >
              {labels[value.status]}
            </Pill>
          </div>
          <p className="text-xs text-[var(--muted)]">
            {formatDate(value.createdAt)} · revision {value.revision}
          </p>
          <p className="break-words text-sm">{value.reason}</p>
          <ul className="grid gap-2 text-sm">
            {value.items.map((item) => (
              <li key={item.orderItemId} className="rounded-lg bg-slate-50 p-3">
                <p className="font-medium">{item.productName}</p>
                <p className="text-xs text-[var(--muted)]">
                  {item.sku} · approved {item.quantity} · received{" "}
                  {item.receivedQuantity} · restocked {item.restockQuantity}
                </p>
                {item.inspection && (
                  <p className="mt-1 break-words">{item.inspection}</p>
                )}
              </li>
            ))}
          </ul>
          {value.receivedAt && (
            <p className="text-sm">
              Goods {formatMoney(value.merchandiseRefund)} + delivery{" "}
              {formatMoney(value.shippingRefund)} ={" "}
              <strong>{formatMoney(value.refundAmount)}</strong>
            </p>
          )}
          {value.refundReference && (
            <p className="break-words text-sm">
              Refund reference: {value.refundReference} ·{" "}
              {value.refundedAt && formatDate(value.refundedAt)}
            </p>
          )}
          {value.rejectionReason && (
            <p className="text-sm text-red-700">{value.rejectionReason}</p>
          )}
          <div className="flex flex-wrap gap-2">
            {value.status === 0 && (
              <>
                <Button onClick={() => open({ kind: "receive", id: value.id })}>
                  Receive and inspect
                </Button>
                <Button
                  variant="danger"
                  onClick={() => open({ kind: "reject", id: value.id })}
                >
                  Reject return
                </Button>
              </>
            )}
            {value.status === 2 && (
              <Button
                variant="primary"
                onClick={() => open({ kind: "refund", id: value.id })}
              >
                Record verified refund
              </Button>
            )}
          </div>
          <details>
            <summary className="cursor-pointer text-sm">Return audit</summary>
            <ol className="mt-2 grid gap-2 text-xs text-[var(--muted)]">
              {value.audit.map((entry, index) => (
                <li key={index} className="break-words">
                  {entry.action} · {entry.actor} ·{" "}
                  {formatDate(entry.occurredAt)}
                  <p>{entry.reason}</p>
                </li>
              ))}
            </ol>
          </details>
        </article>
      ))}
      <Pagination
        data={data.returns}
        label="returns"
        busy={query.isFetching}
        onPageChange={setPage}
      />
      <Modal
        open={!!action}
        title={
          action?.kind === "create"
            ? "Approve return items"
            : action?.kind === "receive"
              ? "Receive and inspect goods"
              : action?.kind === "refund"
                ? "Record a completed refund"
                : "Reject return request"
        }
        busy={mutation.isPending}
        onClose={() => {
          if (!prepared) setAction(null);
        }}
        widthClass="max-w-2xl"
        error={
          mutation.isError ? getApiErrorMessage(mutation.error) : undefined
        }
      >
        {mutation.isError && (
          <Button
            className="mb-4"
            onClick={() => {
              setAction(null);
              setPrepared(null);
              mutation.reset();
              void query.refetch();
            }}
          >
            Reload latest return
          </Button>
        )}
        {action?.kind === "create" && (
          <CreateReturnForm
            summary={data}
            busy={mutation.isPending}
            onReview={(body) => setPrepared({ kind: "create", body })}
          />
        )}
        {action?.kind === "receive" && selected && (
          <ReceiveReturnForm
            value={selected}
            summary={data}
            busy={mutation.isPending}
            onReview={(body) =>
              setPrepared({ kind: "receive", id: selected.id, body })
            }
          />
        )}
        {action?.kind === "refund" && selected && (
          <RefundReturnForm
            value={selected}
            busy={mutation.isPending}
            onReview={(body) =>
              setPrepared({ kind: "refund", id: selected.id, body })
            }
          />
        )}
        {(action?.kind === "reject-request" ||
          (action?.kind === "reject" && selected)) && (
          <ReasonForm
            busy={mutation.isPending}
            onReview={(reason) =>
              setPrepared(
                action.kind === "reject-request"
                  ? { kind: "reject-request", body: { reason } }
                  : {
                      kind: "reject",
                      id: selected!.id,
                      body: { reason, expectedRevision: selected!.revision },
                    },
              )
            }
          />
        )}
      </Modal>
      <ConfirmDialog
        open={!!prepared}
        title="Confirm return operation"
        busy={mutation.isPending}
        confirmVariant={
          prepared?.kind === "reject" || prepared?.kind === "reject-request"
            ? "danger"
            : "primary"
        }
        message={
          <>
            <p>
              Order {order.orderCode}.{" "}
              {prepared?.kind === "refund"
                ? `Record an already sent and verified refund of ${formatMoney(prepared.body.amount)}. This does not initiate a bank transfer.`
                : prepared?.kind === "receive"
                  ? "Record actual inspected goods. Sellable quantities update stock and returned units lose warranty entitlement."
                  : prepared?.kind === "create"
                    ? "Approve these return quantities. Stock and refunds change only after goods are received and inspected."
                    : "Reject this request and retain the decision reason."}
            </p>
            {mutation.isError && (
              <p role="alert" className="mt-2 text-red-700">
                {getApiErrorMessage(mutation.error)}
              </p>
            )}
          </>
        }
        confirmLabel="Record operation"
        onCancel={() => setPrepared(null)}
        onConfirm={() => prepared && mutation.mutate(prepared)}
      />
    </section>
  );
}

const reason = z.string().trim().min(1, "A reason is required.").max(500);
function CreateReturnForm({
  summary,
  busy,
  onReview,
}: {
  summary: OrderReturnSummaryDto;
  busy: boolean;
  onReview: (request: CreateOrderReturnRequest) => void;
}) {
  const [requestId] = useState(() => crypto.randomUUID());
  const [available] = useState(() =>
    summary.items.filter((item) => item.availableQuantity > 0),
  );
  const schema = z
    .object({
      reason,
      quantities: z
        .array(z.number().int().min(0))
        .refine(
          (values) => values.some((v) => v > 0),
          "Choose at least one quantity.",
        ),
    })
    .superRefine((values, context) => {
      values.quantities.forEach((q, index) => {
        if (q > available[index].availableQuantity)
          context.addIssue({
            code: "custom",
            path: ["quantities", index],
            message: "Quantity exceeds the available return quantity.",
          });
      });
    });
  const form = useForm({
    resolver: zodResolver(schema),
    defaultValues: { reason: "", quantities: available.map(() => 0) },
  });
  return (
    <form
      noValidate
      className="grid gap-4"
      onSubmit={form.handleSubmit((values) =>
        onReview({
          requestId,
          reason: values.reason,
          items: available
            .map((item, index) => ({
              orderItemId: item.orderItemId,
              quantity: values.quantities[index],
            }))
            .filter((item) => item.quantity > 0),
        }),
      )}
    >
      {available.map((item, index) => (
        <Controller
          key={item.orderItemId}
          control={form.control}
          name={`quantities.${index}`}
          render={({ field, fieldState }) => (
            <Field
              label={`${item.sku} · ${item.productName} (up to ${item.availableQuantity})`}
              error={fieldState.error?.message}
            >
              <TextInput
                type="number"
                min="0"
                max={item.availableQuantity}
                value={field.value}
                onChange={(event) => field.onChange(Number(event.target.value))}
              />
            </Field>
          )}
        />
      ))}
      {form.formState.errors.quantities?.message && (
        <p role="alert">{form.formState.errors.quantities.message}</p>
      )}
      <Controller
        control={form.control}
        name="reason"
        render={({ field, fieldState }) => (
          <Field
            label="Decision / customer message"
            error={fieldState.error?.message}
          >
            <TextArea {...field} />
          </Field>
        )}
      />
      <Button type="submit" variant="primary" disabled={busy}>
        Review return approval
      </Button>
    </form>
  );
}
function ReceiveReturnForm({
  value,
  summary,
  busy,
  onReview,
}: {
  value: OrderReturnCaseDto;
  summary: OrderReturnSummaryDto;
  busy: boolean;
  onReview: (request: ReceiveOrderReturnRequest) => void;
}) {
  const schema = z
    .object({
      reason,
      refundShipping: z.boolean(),
      items: z.array(
        z.object({
          orderItemId: z.string(),
          receivedQuantity: z.number().int().min(0),
          restockQuantity: z.number().int().min(0),
          inspection: reason,
          serializedUnitIds: z.array(z.string()),
        }),
      ),
    })
    .superRefine((values, ctx) => {
      values.items.forEach((item, index) => {
        if (item.receivedQuantity > value.items[index].quantity)
          ctx.addIssue({
            code: "custom",
            path: ["items", index, "receivedQuantity"],
            message: "Exceeds approved quantity.",
          });
        if (item.restockQuantity > item.receivedQuantity)
          ctx.addIssue({
            code: "custom",
            path: ["items", index, "restockQuantity"],
            message: "Restock cannot exceed receipt.",
          });
        const units =
          summary.items.find((i) => i.orderItemId === item.orderItemId)
            ?.units ?? [];
        if (
          units.length &&
          item.serializedUnitIds.length !== item.receivedQuantity
        )
          ctx.addIssue({
            code: "custom",
            path: ["items", index, "serializedUnitIds"],
            message: "Select every received physical unit.",
          });
      });
    });
  const form = useForm({
    resolver: zodResolver(schema),
    defaultValues: {
      reason: "",
      refundShipping: false,
      items: value.items.map((item) => ({
        orderItemId: item.orderItemId,
        receivedQuantity: item.quantity,
        restockQuantity: 0,
        inspection: "",
        serializedUnitIds: [] as string[],
      })),
    },
  });
  return (
    <form
      noValidate
      className="grid gap-4"
      onSubmit={form.handleSubmit((values) =>
        onReview({ ...values, expectedRevision: value.revision }),
      )}
    >
      <Notice title="Record physical receipt only">
        Restock only quantities inspected as sellable. Serialized units enter
        quarantine; their warranty is closed.
      </Notice>
      {value.items.map((item, index) => {
        const units =
          summary.items.find((i) => i.orderItemId === item.orderItemId)
            ?.units ?? [];
        return (
          <fieldset
            key={item.orderItemId}
            className="grid min-w-0 gap-3 rounded-lg border p-3"
          >
            <legend className="text-sm font-medium">
              {item.sku} · approved {item.quantity}
            </legend>
            {(["receivedQuantity", "restockQuantity"] as const).map((name) => (
              <Controller
                key={name}
                control={form.control}
                name={`items.${index}.${name}`}
                render={({ field, fieldState }) => (
                  <Field
                    label={
                      name === "receivedQuantity"
                        ? "Actually received"
                        : "Restock as sellable"
                    }
                    error={fieldState.error?.message}
                  >
                    <TextInput
                      type="number"
                      min="0"
                      max={item.quantity}
                      disabled={name === "restockQuantity" && units.length > 0}
                      value={field.value}
                      onChange={(event) =>
                        field.onChange(Number(event.target.value))
                      }
                    />
                  </Field>
                )}
              />
            ))}
            <Controller
              control={form.control}
              name={`items.${index}.inspection`}
              render={({ field, fieldState }) => (
                <Field
                  label="Inspection and disposition"
                  error={fieldState.error?.message}
                >
                  <TextArea
                    {...field}
                    placeholder="Condition, packaging and quarantine / restock reason"
                  />
                </Field>
              )}
            />
            {units.length > 0 && (
              <Controller
                control={form.control}
                name={`items.${index}.serializedUnitIds`}
                render={({ field, fieldState }) => (
                  <Field
                    label="Physical units received"
                    error={fieldState.error?.message}
                  >
                    <div>
                      {units.map((unit) => (
                        <label
                          key={unit.id}
                          className="flex min-h-11 items-center gap-2"
                        >
                          <input
                            type="checkbox"
                            checked={field.value.includes(unit.id)}
                            onChange={(event) =>
                              field.onChange(
                                event.target.checked
                                  ? [...field.value, unit.id]
                                  : field.value.filter((id) => id !== unit.id),
                              )
                            }
                          />
                          {unit.maskedIdentifier}
                        </label>
                      ))}
                    </div>
                  </Field>
                )}
              />
            )}
          </fieldset>
        );
      })}
      <label className="flex min-h-11 items-center gap-2 text-sm">
        <input type="checkbox" {...form.register("refundShipping")} />
        Refund delivery fee after the whole order is received
      </label>
      <Controller
        control={form.control}
        name="reason"
        render={({ field, fieldState }) => (
          <Field label="Receipt reason" error={fieldState.error?.message}>
            <TextArea {...field} />
          </Field>
        )}
      />
      <Button type="submit" variant="primary" disabled={busy}>
        Review received goods
      </Button>
    </form>
  );
}
function RefundReturnForm({
  value,
  busy,
  onReview,
}: {
  value: OrderReturnCaseDto;
  busy: boolean;
  onReview: (request: RefundOrderReturnRequest) => void;
}) {
  const schema = z.object({
    amount: z
      .number()
      .int()
      .refine(
        (amount) => amount === value.refundAmount,
        "Enter the exact approved refund amount.",
      ),
    reference: z.string().trim().min(1).max(120),
    reason,
  });
  const form = useForm({
    resolver: zodResolver(schema),
    defaultValues: { amount: value.refundAmount, reference: "", reason: "" },
  });
  return (
    <form
      noValidate
      className="grid gap-4"
      onSubmit={form.handleSubmit((values) =>
        onReview({ ...values, expectedRevision: value.revision }),
      )}
    >
      <Notice title={`Approved refund: ${formatMoney(value.refundAmount)}`}>
        Record only money already sent and verified against bank/provider
        evidence.
      </Notice>
      <Controller
        control={form.control}
        name="amount"
        render={({ field, fieldState }) => (
          <Field
            label="Verified refund amount (VND)"
            error={fieldState.error?.message}
          >
            <TextInput
              type="number"
              min="1"
              step="1"
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
            label="Refund transaction reference"
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
            label="Refund evidence / reason"
            error={fieldState.error?.message}
          >
            <TextArea {...field} />
          </Field>
        )}
      />
      <Button type="submit" variant="primary" disabled={busy}>
        Review refund record
      </Button>
    </form>
  );
}
function ReasonForm({
  busy,
  onReview,
}: {
  busy: boolean;
  onReview: (reason: string) => void;
}) {
  const form = useForm({
    resolver: zodResolver(z.object({ reason })),
    defaultValues: { reason: "" },
  });
  return (
    <form
      noValidate
      className="grid gap-4"
      onSubmit={form.handleSubmit((values) => onReview(values.reason))}
    >
      <Controller
        control={form.control}
        name="reason"
        render={({ field, fieldState }) => (
          <Field
            label="Decision / customer message"
            error={fieldState.error?.message}
          >
            <TextArea {...field} />
          </Field>
        )}
      />
      <Button type="submit" variant="danger" disabled={busy}>
        Review rejection
      </Button>
    </form>
  );
}
