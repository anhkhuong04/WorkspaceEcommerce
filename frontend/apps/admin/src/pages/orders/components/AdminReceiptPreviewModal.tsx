import type { AdminOrderDto } from "@workspace-ecommerce/api-types";
import { formatDate, formatMoney, formatOrderStatus, formatPaymentMethod, formatPaymentStatus } from "@workspace-ecommerce/shared-utils";
import type { ReactNode } from "react";
import { Button, Modal } from "../../../components/ui/AdminUi";
import { formatLocalizedText } from "../../../utils/localizedText";

interface AdminReceiptPreviewModalProps {
  open: boolean;
  order: AdminOrderDto;
  isDownloading: boolean;
  onClose: () => void;
  onDownload: () => void;
}

export function AdminReceiptPreviewModal({ open, order, isDownloading, onClose, onDownload }: AdminReceiptPreviewModalProps) {
  return (
    <Modal
      open={open}
      title={`Receipt preview · ${order.orderCode}`}
      widthClass="max-w-6xl"
      onClose={onClose}
      footer={
        <>
          <Button type="button" onClick={onClose}>Close</Button>
          <Button type="button" variant="primary" disabled={isDownloading} onClick={onDownload}>
            {isDownloading ? "Generating PDF..." : "Download PDF"}
          </Button>
        </>
      }
    >
      <div className="bg-slate-100 p-2 sm:p-5">
        <article className="mx-auto max-w-[920px] bg-white px-5 py-8 text-slate-800 shadow-sm ring-1 ring-slate-200 sm:px-10 lg:px-14">
          <header className="flex flex-col justify-between gap-8 border-b border-slate-200 pb-8 sm:flex-row sm:items-start">
            <div>
              <img src="/demo/logo.svg" alt="Workspace Ecommerce" className="h-auto w-[190px]" />
              <p className="mt-2 text-sm font-medium text-slate-500">workspaceecom.com</p>
            </div>
            <div className="sm:text-right">
              <p className="text-3xl font-black tracking-tight text-slate-950">ORDER RECEIPT</p>
              <p className="mt-2 font-mono text-sm font-bold text-slate-600">#{order.orderCode}</p>
              <p className="mt-1 text-sm text-slate-500">Date: {formatDate(order.createdAt)}</p>
            </div>
          </header>

          <div className="grid gap-8 border-b border-slate-200 py-8 md:grid-cols-2 md:gap-16">
            <Section title="From / Seller">
              <p className="font-bold text-slate-950">Workspace Ecommerce</p>
              <p>support@workspaceecom.com</p><p>1900 636 660</p><p>workspaceecom.com</p>
            </Section>
            <Section title="Bill to / Customer">
              <p className="font-bold text-slate-950">{formatLocalizedText(order.customerName)}</p>
              <p>{order.customerPhone}</p>
              {order.customerEmail ? <p>{order.customerEmail}</p> : null}
              <p>{order.shippingAddress}</p>
            </Section>
          </div>

          <section className="border-b border-slate-200 py-8">
            <h3 className="text-sm font-black uppercase tracking-[0.12em] text-slate-950">Order info</h3>
            <div className="mt-5 grid gap-x-8 gap-y-4 text-sm sm:grid-cols-2 lg:grid-cols-3">
              <Meta label="Order" value={`#${order.orderCode}`} />
              <Meta label="Payment" value={formatPaymentMethod(order.paymentMethod)} />
              <Meta label="Payment status" value={<span className="rounded-md bg-blue-50 px-2 py-1 font-bold text-blue-700">{formatPaymentStatus(order.paymentStatus)}</span>} />
              <Meta label="Order status" value={formatOrderStatus(order.status)} />
              <Meta label="Tracking" value={order.trackingCode ?? "Not available"} />
              <Meta label="Paid at" value={order.paidAt ? formatDate(order.paidAt) : "Not available"} />
            </div>
          </section>

          <div className="mt-8 overflow-x-auto rounded-lg border border-slate-200">
            <table className="w-full min-w-[660px] text-sm">
              <thead className="bg-slate-50 text-left text-xs font-black uppercase tracking-wide text-slate-700">
                <tr><th className="px-5 py-4">Product</th><th className="px-5 py-4 text-center">Qty</th><th className="px-5 py-4 text-right">Unit price</th><th className="px-5 py-4 text-right">Amount</th></tr>
              </thead>
              <tbody className="divide-y divide-slate-200">
                {order.items.map((item) => {
                  const productName = formatLocalizedText(item.productNameSnapshot);
                  return (
                    <tr key={item.id}>
                      <td className="px-5 py-5">
                        <div className="flex items-center gap-4">
                          {item.productImageUrlSnapshot ? (
                            <img src={item.productImageUrlSnapshot} alt="" className="h-14 w-14 shrink-0 rounded-lg object-cover ring-1 ring-slate-200" />
                          ) : (
                            <div className="grid h-14 w-14 shrink-0 place-items-center rounded-lg bg-gradient-to-br from-slate-50 to-slate-200 text-lg font-black text-slate-500 ring-1 ring-slate-200" aria-hidden="true">{productName.trim().charAt(0).toUpperCase() || "P"}</div>
                          )}
                          <div><p className="font-bold text-slate-950">{productName}</p><p className="mt-1 font-mono text-xs text-slate-500">{item.skuSnapshot}</p>{item.requiresInstallation ? <p className="mt-1 text-xs font-semibold text-amber-700">Installation required</p> : null}</div>
                        </div>
                      </td>
                      <td className="px-5 py-5 text-center font-semibold">{item.quantity}</td>
                      <td className="px-5 py-5 text-right">{formatMoney(item.unitPrice)}</td>
                      <td className="px-5 py-5 text-right font-bold text-slate-950">{formatMoney(item.lineTotal)}</td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>

          <div className="ml-auto mt-7 grid max-w-[390px] gap-3 text-sm">
            <Total label="Subtotal" value={formatMoney(order.subtotal)} />
            {order.discountAmount > 0 ? <Total label={`Discount${order.couponCodeSnapshot ? ` (${order.couponCodeSnapshot})` : ""}`} value={`-${formatMoney(order.discountAmount)}`} accent /> : null}
            <Total label="Shipping" value={formatMoney(order.shippingFee)} />
            <div className="mt-1 flex items-center justify-between border-t border-slate-300 pt-4 text-xl font-black text-slate-950"><span>Total</span><span className="text-blue-600">{formatMoney(order.totalAmount)}</span></div>
          </div>

          <div className="mt-10 grid gap-8 border-y border-slate-200 py-8 md:grid-cols-2 md:gap-16">
            <Section title="Payment"><p>{formatPaymentMethod(order.paymentMethod)}</p><p>{formatPaymentStatus(order.paymentStatus)}</p>{order.paidAt ? <p>Paid on {formatDate(order.paidAt)}</p> : null}</Section>
            <Section title="Notes"><p>{order.note ?? "Thank you for your purchase."}</p><p className="font-semibold text-rose-700">Order confirmation only; this document is not a VAT invoice.</p></Section>
          </div>

          <footer className="flex flex-wrap justify-center gap-x-5 gap-y-2 pt-8 text-center text-xs font-medium text-slate-500"><span>support@workspaceecom.com</span><span>|</span><span>1900 636 660</span><span>|</span><span>workspaceecom.com</span></footer>
        </article>
      </div>
    </Modal>
  );
}

function Section({ title, children }: { title: string; children: ReactNode }) {
  return <section><h3 className="mb-4 text-sm font-black uppercase tracking-[0.12em] text-slate-950">{title}</h3><div className="grid gap-2 text-sm text-slate-600">{children}</div></section>;
}

function Meta({ label, value }: { label: string; value: ReactNode }) {
  return <div className="flex min-w-0 items-center gap-2"><span className="font-semibold text-slate-500">{label}:</span><span className="min-w-0 font-bold text-slate-800">{value}</span></div>;
}

function Total({ label, value, accent = false }: { label: string; value: string; accent?: boolean }) {
  return <div className={`flex items-center justify-between gap-6 ${accent ? "text-blue-600" : "text-slate-700"}`}><span>{label}</span><span className="font-semibold">{value}</span></div>;
}
