import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import type { AdminOrderListRequest, OrderStatus } from "@workspace-ecommerce/api-types";
import { formatDate, formatMoney, formatOrderStatus, formatPaymentMethod } from "@workspace-ecommerce/shared-utils";
import type { ReactNode } from "react";
import { useEffect, useMemo, useState } from "react";
import { Controller, useForm } from "react-hook-form";
import { useSearchParams } from "react-router-dom";
import { z } from "zod";
import { AdminPageHeader } from "../../components/ui/AdminPageHeader";
import { Button, Drawer, EmptyState, Field, Notice, Pill, SelectInput, TextArea, TextInput } from "../../components/ui/AdminUi";
import { adminApi } from "../../services/api/adminApi";
import { getApiErrorMessage } from "../../services/api/errors";

const orderStatuses: OrderStatus[] = [0, 1, 2, 3, 4, 5, 6];
const nextStatusesByStatus: Record<OrderStatus, OrderStatus[]> = { 0: [1, 6], 1: [2], 2: [3], 3: [4, 5], 4: [], 5: [3, 6], 6: [] };
const statusSchema = z.object({
  status: z.union([z.literal(0), z.literal(1), z.literal(2), z.literal(3), z.literal(4), z.literal(5), z.literal(6)]),
  note: z.string().trim().max(1000, "Note is too long.").optional()
});

type StatusFormValues = z.infer<typeof statusSchema>;

const statusTones: Record<OrderStatus, "green" | "red" | "blue" | "orange" | "slate" | "teal"> = { 0: "orange", 1: "teal", 2: "blue", 3: "blue", 4: "green", 5: "orange", 6: "red" };

function orderStatusPill(status: OrderStatus) {
  return <Pill tone={statusTones[status]}>{formatOrderStatus(status)}</Pill>;
}

function toStatusRequest(values: StatusFormValues) {
  return { status: values.status, note: values.note?.trim() ? values.note.trim() : null };
}

function parseOrderStatus(value: string | null): OrderStatus | undefined {
  if (value === null) return undefined;
  const status = Number(value);
  return orderStatuses.includes(status as OrderStatus) ? status as OrderStatus : undefined;
}

function parsePageNumber(value: string | null): number {
  const pageNumber = Number(value);
  return Number.isInteger(pageNumber) && pageNumber > 0 ? pageNumber : 1;
}

export function OrdersPage() {
  const queryClient = useQueryClient();
  const [searchParams, setSearchParams] = useSearchParams();
  const [notice, setNotice] = useState<{ type: "success" | "error"; message: string } | null>(null);
  const pageNumber = parsePageNumber(searchParams.get("page"));
  const statusFilter = parseOrderStatus(searchParams.get("status"));
  const searchFilter = searchParams.get("search")?.trim() || undefined;
  const request = useMemo<AdminOrderListRequest>(() => ({ pageNumber, pageSize: 8, status: statusFilter, search: searchFilter }), [pageNumber, searchFilter, statusFilter]);
  const [selectedOrderId, setSelectedOrderId] = useState<string | null>(null);
  const ordersQuery = useQuery({ queryKey: ["admin-orders", request], queryFn: () => adminApi.getOrders(request) });
  const orderQuery = useQuery({ queryKey: ["admin-order", selectedOrderId], queryFn: () => adminApi.getOrder(selectedOrderId ?? ""), enabled: selectedOrderId !== null });
  const order = orderQuery.data;
  const nextStatuses = useMemo(() => order ? nextStatusesByStatus[order.status] : [], [order]);

  const statusForm = useForm<StatusFormValues>({ resolver: zodResolver(statusSchema), defaultValues: { status: 1, note: "" } });

  useEffect(() => {
    if (nextStatuses.length > 0) {
      statusForm.reset({ status: nextStatuses[0], note: "" });
    }
  }, [nextStatuses, statusForm]);

  const updateStatusMutation = useMutation({
    mutationFn: (values: StatusFormValues) => {
      if (!selectedOrderId) throw new Error("Order is required.");
      return adminApi.updateOrderStatus(selectedOrderId, toStatusRequest(values));
    },
    onSuccess: async (updatedOrder) => {
      queryClient.setQueryData(["admin-order", selectedOrderId], updatedOrder);
      await Promise.all([queryClient.invalidateQueries({ queryKey: ["admin-orders"] }), queryClient.invalidateQueries({ queryKey: ["admin-dashboard"] })]);
      setNotice({ type: "success", message: "Order status updated." });
    },
    onError: (error) => setNotice({ type: "error", message: getApiErrorMessage(error) })
  });

  function updateFilters(values: Pick<AdminOrderListRequest, "status" | "search">) {
    const nextParams = new URLSearchParams(searchParams);
    if (values.status === undefined) nextParams.delete("status");
    else nextParams.set("status", String(values.status));
    if (values.search === undefined) nextParams.delete("search");
    else nextParams.set("search", values.search);
    nextParams.delete("page");
    setSearchParams(nextParams);
  }

  function updatePage(nextPageNumber: number) {
    const nextParams = new URLSearchParams(searchParams);
    if (nextPageNumber <= 1) nextParams.delete("page");
    else nextParams.set("page", String(nextPageNumber));
    setSearchParams(nextParams);
  }

  return (
    <div className="admin-page-grid">
      <AdminPageHeader title="Orders" description="Review orders, inspect order snapshots, render status history, and move orders through MVP transitions." />
      {notice ? <Notice type={notice.type} title={notice.message} /> : null}
      {ordersQuery.isError ? <Notice type="error" title="Orders could not be loaded">{getApiErrorMessage(ordersQuery.error)}</Notice> : null}

      <section className="rounded-3xl border border-slate-200 bg-white p-5 shadow-sm">
        <div className="mb-4 flex flex-col gap-3 lg:flex-row">
          <TextInput key={searchFilter ?? ""} defaultValue={searchFilter ?? ""} placeholder="Search code, customer, phone" onKeyDown={(event) => { if (event.key === "Enter") updateFilters({ status: statusFilter, search: event.currentTarget.value.trim() || undefined }); }} />
          <SelectInput value={statusFilter ?? ""} onChange={(event) => updateFilters({ status: event.target.value === "" ? undefined : Number(event.target.value) as OrderStatus, search: searchFilter })} className="lg:max-w-xs">
            <option value="">All statuses</option>
            {orderStatuses.map((status) => <option key={status} value={status}>{formatOrderStatus(status)}</option>)}
          </SelectInput>
        </div>

        {ordersQuery.isLoading ? (
          <div className="grid gap-3">{[0, 1, 2].map((item) => <div key={item} className="h-14 animate-pulse rounded-2xl bg-slate-100" />)}</div>
        ) : ordersQuery.data?.items.length ? (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[980px] text-left text-sm">
              <thead className="text-xs uppercase tracking-wide text-slate-500">
                <tr className="border-b border-slate-100"><th className="py-3 pr-4">Code</th><th className="py-3 pr-4">Customer</th><th className="py-3 pr-4">Phone</th><th className="py-3 pr-4">Items</th><th className="py-3 pr-4">Total</th><th className="py-3 pr-4">Status</th><th className="py-3 pr-4">Payment</th><th className="py-3 pr-4">Created</th><th className="py-3 pr-4">Actions</th></tr>
              </thead>
              <tbody>
                {ordersQuery.data.items.map((item) => (
                  <tr key={item.id} className="border-b border-slate-100 last:border-0">
                    <td className="py-3 pr-4 font-bold">{item.orderCode}</td>
                    <td className="py-3 pr-4">{item.customerName}</td>
                    <td className="py-3 pr-4">{item.customerPhone}</td>
                    <td className="py-3 pr-4">{item.itemCount}</td>
                    <td className="py-3 pr-4">{formatMoney(item.totalAmount)}</td>
                    <td className="py-3 pr-4">{orderStatusPill(item.status)}</td>
                    <td className="py-3 pr-4">{formatPaymentMethod(item.paymentMethod)}</td>
                    <td className="py-3 pr-4">{formatDate(item.createdAt)}</td>
                    <td className="py-3 pr-4"><Button type="button" onClick={() => setSelectedOrderId(item.id)}>View</Button></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : <EmptyState>No orders found</EmptyState>}

        {ordersQuery.data ? (
          <div className="mt-4 flex items-center justify-between text-sm font-semibold text-slate-500">
            <span>Page {ordersQuery.data.pageNumber} of {ordersQuery.data.totalPages || 1}</span>
            <div className="flex gap-2">
              <Button type="button" disabled={!ordersQuery.data.hasPreviousPage} onClick={() => updatePage(Math.max(1, pageNumber - 1))}>Previous</Button>
              <Button type="button" disabled={!ordersQuery.data.hasNextPage} onClick={() => updatePage(pageNumber + 1)}>Next</Button>
            </div>
          </div>
        ) : null}
      </section>

      <Drawer title={order ? `Order ${order.orderCode}` : "Order detail"} open={selectedOrderId !== null} onClose={() => setSelectedOrderId(null)}>
        {orderQuery.isError ? <Notice type="error" title="Order could not be loaded">{getApiErrorMessage(orderQuery.error)}</Notice> : null}
        {orderQuery.isLoading ? (
          <div className="grid gap-3">{[0, 1, 2].map((item) => <div key={item} className="h-20 animate-pulse rounded-2xl bg-slate-100" />)}</div>
        ) : order ? (
          <div className="grid gap-4">
            <section className="rounded-3xl border border-slate-200 p-5">
              <div className="grid gap-3 text-sm sm:grid-cols-2">
                <Info label="Status" value={orderStatusPill(order.status)} />
                <Info label="Payment" value={formatPaymentMethod(order.paymentMethod)} />
                <Info label="Customer" value={order.customerName} />
                <Info label="Phone" value={order.customerPhone} />
                <Info label="Email" value={order.customerEmail ?? "-"} />
                <Info label="Created" value={formatDate(order.createdAt)} />
                <Info wide label="Shipping address" value={order.shippingAddress} />
                <Info wide label="Customer note" value={order.note ?? "-"} />
                <Info label="Subtotal" value={formatMoney(order.subtotal)} />
                <Info label="Shipping fee" value={formatMoney(order.shippingFee)} />
                <Info label="Discount" value={formatMoney(order.discountAmount)} />
                <Info label="Total" value={formatMoney(order.totalAmount)} />
              </div>
            </section>

            <section className="rounded-3xl border border-slate-200 p-5">
              <h3 className="mb-3 text-lg font-black">Items</h3>
              <div className="overflow-x-auto">
                <table className="w-full min-w-[680px] text-left text-sm">
                  <thead className="text-xs uppercase tracking-wide text-slate-500"><tr><th className="py-2 pr-3">Product</th><th className="py-2 pr-3">SKU</th><th className="py-2 pr-3">Price</th><th className="py-2 pr-3">Qty</th><th className="py-2 pr-3">Line</th><th className="py-2 pr-3">Install</th></tr></thead>
                  <tbody>{order.items.map((item) => <tr key={item.id} className="border-t border-slate-100"><td className="py-2 pr-3">{item.productNameSnapshot}</td><td className="py-2 pr-3">{item.skuSnapshot}</td><td className="py-2 pr-3">{formatMoney(item.unitPrice)}</td><td className="py-2 pr-3">{item.quantity}</td><td className="py-2 pr-3">{formatMoney(item.lineTotal)}</td><td className="py-2 pr-3"><Pill tone={item.requiresInstallation ? "blue" : "slate"}>{item.requiresInstallation ? "Required" : "None"}</Pill></td></tr>)}</tbody>
                </table>
              </div>
            </section>

            <section className="rounded-3xl border border-slate-200 p-5">
              <h3 className="mb-3 text-lg font-black">Status transition</h3>
              {nextStatuses.length === 0 ? <EmptyState>This order is in a terminal status</EmptyState> : (
                <form className="grid gap-4">
                  <Controller control={statusForm.control} name="status" render={({ field, fieldState }) => <Field label="Next status" error={fieldState.error?.message}><SelectInput value={field.value} onChange={(event) => field.onChange(Number(event.target.value) as OrderStatus)}>{nextStatuses.map((status) => <option key={status} value={status}>{formatOrderStatus(status)}</option>)}</SelectInput></Field>} />
                  <Controller control={statusForm.control} name="note" render={({ field, fieldState }) => <Field label="Internal note" error={fieldState.error?.message}><TextArea {...field} rows={3} placeholder="Optional note for status history" /></Field>} />
                  <Button type="button" variant="primary" disabled={updateStatusMutation.isPending} onClick={statusForm.handleSubmit((values) => updateStatusMutation.mutate(values))}>{updateStatusMutation.isPending ? "Updating..." : "Update status"}</Button>
                </form>
              )}
            </section>

            <section className="rounded-3xl border border-slate-200 p-5">
              <h3 className="mb-3 text-lg font-black">Status history</h3>
              {order.statusHistory.length ? (
                <div className="grid gap-3">{order.statusHistory.map((history) => <div key={history.id} className="border-l-4 border-teal-600 pl-4"><p className="font-bold">{history.fromStatus === null ? "Created" : formatOrderStatus(history.fromStatus)} to {formatOrderStatus(history.toStatus)}</p><p className="text-sm text-slate-500">{formatDate(history.changedAt)} by {history.changedBy ?? "system"}</p>{history.note ? <p className="mt-1 text-sm text-slate-700">{history.note}</p> : null}</div>)}</div>
              ) : <EmptyState>No status history</EmptyState>}
            </section>
          </div>
        ) : null}
      </Drawer>
    </div>
  );
}

function Info({ label, value, wide = false }: { label: string; value: ReactNode; wide?: boolean }) {
  return <div className={wide ? "sm:col-span-2" : undefined}><p className="text-xs font-black uppercase tracking-wide text-slate-400">{label}</p><div className="mt-1 font-semibold text-slate-800">{value}</div></div>;
}
