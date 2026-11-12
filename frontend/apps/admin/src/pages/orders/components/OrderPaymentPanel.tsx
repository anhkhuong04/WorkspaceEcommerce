import { useState } from "react";
import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Button, ConfirmDialog, Notice } from "../../../components/ui/AdminUi";
import { getApiErrorMessage } from "../../../services/api/errors";
import {
  orderPaymentApi,
  type PaymentConfirmationAction,
} from "../../../services/api/orderPaymentApi";

const schema = z.object({
  reference: z.string().trim().min(1).max(120),
  reason: z.string().trim().min(1).max(500),
});
const actions = {
  "confirm-transfer": "Confirm bank transfer",
  "collect-cod": "Confirm full COD collection",
  "remit-cod": "Confirm COD remittance",
  "confirm-full-refund": "Record completed full refund",
};
export function OrderPaymentPanel({ orderId }: { orderId: string }) {
  const cache = useQueryClient();
  const query = useQuery({
    queryKey: ["admin-order-payment", orderId],
    queryFn: () => orderPaymentApi.summary(orderId),
  });
  const [action, setAction] = useState<PaymentConfirmationAction | null>(null);
  const [confirming, setConfirming] = useState(false);
  const form = useForm<z.infer<typeof schema>>({
    resolver: zodResolver(schema),
    defaultValues: { reference: "", reason: "" },
  });
  const mutation = useMutation({
    mutationFn: () => {
      if (!query.data || !action)
        throw new Error("Select a payment confirmation");
      return orderPaymentApi.confirm(orderId, action, {
        ...form.getValues(),
        amount: query.data.totalAmount,
        currency: query.data.currency,
      });
    },
    onSuccess: async (value) => {
      setConfirming(false);
      setAction(null);
      form.reset();
      cache.setQueryData(["admin-order-payment", orderId], value);
      await Promise.all(
        [
          ["admin-order-payment", orderId],
          ["admin-order", orderId],
          ["admin-order-shipment", orderId],
          ["admin-orders"],
          ["admin-dashboard"],
        ].map((queryKey) => cache.invalidateQueries({ queryKey })),
      );
    },
  });
  if (query.isLoading)
    return <p role="status">Loading payment confirmations...</p>;
  if (query.isError)
    return (
      <Notice type="error" title={getApiErrorMessage(query.error)}>
        <Button onClick={() => void query.refetch()}>
          Retry payment summary
        </Button>
      </Notice>
    );
  const data = query.data;
  if (!data) return null;
  return (
    <section className="grid gap-3 rounded-xl border border-slate-200 p-4">
      <h3>Payment confirmations</h3>
      <p>
        Exact frozen amount: {data.totalAmount.toLocaleString("vi-VN")}{" "}
        {data.currency}. Collection and remittance are separate events.
      </p>
      {data.importSource && <p>Imported from {data.importSource}: manual delivery and audited payment confirmations.</p>}
      {data.shippingMethod === null && !data.importSource && (
        <p>Legacy order: existing payment workflow applies.</p>
      )}
      {mutation.isSuccess && (
        <p role="status">Payment confirmation recorded.</p>
      )}
      {mutation.isError && (
        <Notice type="error" title={getApiErrorMessage(mutation.error)} />
      )}
      <div className="flex flex-wrap gap-2">
        {(["confirm-transfer", "collect-cod", "remit-cod", "confirm-full-refund"] as const)
          .filter((a) =>
            a === "confirm-transfer"
              ? data.canConfirmTransfer
              : a === "collect-cod"
                ? data.canCollectCod
                : a === "remit-cod" ? data.canRemitCod : data.canConfirmFullRefund,
          )
          .map((a) => (
            <Button
              key={a}
              disabled={mutation.isPending}
              onClick={() => {
                setAction(a);
                mutation.reset();
              }}
            >
              {actions[a]}
            </Button>
          ))}
      </div>
      {action && (
        <form
          className="grid gap-3"
          onSubmit={form.handleSubmit(() => setConfirming(true))}
        >
          <label>
            Transaction / receipt reference
            <input
              className="admin-input w-full"
              aria-label="Transaction / receipt reference"
              {...form.register("reference")}
              aria-invalid={!!form.formState.errors.reference}
            />
            {form.formState.errors.reference && (
              <span role="alert">
                {form.formState.errors.reference.message}
              </span>
            )}
          </label>
          <label>
            Confirmation reason
            <input
              className="admin-input w-full"
              aria-label="Confirmation reason"
              {...form.register("reason")}
              aria-invalid={!!form.formState.errors.reason}
            />
            {form.formState.errors.reason && (
              <span role="alert">{form.formState.errors.reason.message}</span>
            )}
          </label>
          <Button type="submit" disabled={mutation.isPending}>
            Review confirmation
          </Button>
          <Button disabled={mutation.isPending} onClick={() => setAction(null)}>
            Cancel confirmation
          </Button>
        </form>
      )}
      {data.receipts.map((r) => (
        <details key={r.id}>
          <summary>
            {["Bank transfer", "COD collected", "COD remitted", "Full refund"][r.kind]} ·{" "}
            {r.amount.toLocaleString("vi-VN")} {r.currency}
          </summary>
          <p>
            {r.reference} · {r.reason}
          </p>
          <p>
            {r.actor} · {new Date(r.occurredAtUtc).toLocaleString()}
          </p>
        </details>
      ))}
      <ConfirmDialog
        open={confirming}
        title={action ? actions[action] : "Payment confirmation"}
        message={<>
          {action === "confirm-full-refund" ? "Record only a full refund already sent and verified against bank/provider evidence. This does not initiate a bank transfer." : null}
          {`Record ${data.totalAmount.toLocaleString("vi-VN")} ${data.currency} for this order? Confirm only a verified movement of money. Partial or excess amounts require exception handling. This audited fact cannot be edited here.`}
          {mutation.isError && <p role="alert">{getApiErrorMessage(mutation.error)}</p>}
        </>}
        confirmLabel="Record payment fact"
        busy={mutation.isPending}
        onCancel={() => setConfirming(false)}
        onConfirm={() => mutation.mutate()}
      />
    </section>
  );
}
