import type { OrderStatus, PaymentMethod, PaymentStatus } from "@workspace-ecommerce/api-types";
import { formatMoney } from "@workspace-ecommerce/shared-utils";
import { useEffect, useRef, type ReactNode } from "react";
import { useTranslation } from "react-i18next";

export interface ReceiptPreviewOrder {
  orderCode: string;
  customerName: string;
  customerPhone: string;
  customerEmail: string | null;
  shippingAddress: string;
  note: string | null;
  couponCodeSnapshot: string | null;
  subtotal: number;
  shippingFee: number;
  discountAmount: number;
  totalAmount: number;
  status: OrderStatus;
  paymentMethod: PaymentMethod;
  paymentStatus: PaymentStatus;
  paidAt: string | null;
  createdAt: string;
  trackingCode?: string | null;
  items: Array<{
    id: string;
    productNameSnapshot: string;
    productImageUrlSnapshot: string | null;
    skuSnapshot: string;
    unitPrice: number;
    quantity: number;
    lineTotal: number;
    requiresInstallation: boolean;
  }>;
}

interface ReceiptPreviewModalProps {
  open: boolean;
  order: ReceiptPreviewOrder;
  isDownloading: boolean;
  downloadError?: string | null;
  onClose: () => void;
  onDownload: () => void;
}

export function ReceiptPreviewModal({
  open,
  order,
  isDownloading,
  downloadError,
  onClose,
  onDownload
}: ReceiptPreviewModalProps) {
  const { t, i18n } = useTranslation();
  const dialogRef = useRef<HTMLElement>(null);
  const closeButtonRef = useRef<HTMLButtonElement>(null);
  const previousFocusRef = useRef<HTMLElement | null>(null);

  useEffect(() => {
    if (!open) return;

    previousFocusRef.current = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    closeButtonRef.current?.focus();

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === "Escape") {
        event.preventDefault();
        onClose();
        return;
      }

      if (event.key !== "Tab" || !dialogRef.current) return;

      const focusableElements = Array.from(dialogRef.current.querySelectorAll<HTMLElement>(
        'button:not([disabled]), [href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'
      ));
      if (focusableElements.length === 0) {
        event.preventDefault();
        dialogRef.current.focus();
        return;
      }

      const firstElement = focusableElements[0];
      const lastElement = focusableElements[focusableElements.length - 1];
      if (event.shiftKey && document.activeElement === firstElement) {
        event.preventDefault();
        lastElement.focus();
      } else if (!event.shiftKey && document.activeElement === lastElement) {
        event.preventDefault();
        firstElement.focus();
      }
    }

    window.addEventListener("keydown", handleKeyDown);
    return () => {
      document.body.style.overflow = previousOverflow;
      window.removeEventListener("keydown", handleKeyDown);
      previousFocusRef.current?.focus();
    };
  }, [open, onClose]);

  if (!open) return null;

  const language = i18n.resolvedLanguage?.startsWith("vi") ? "vi-VN" : "en-US";
  const statusTone = order.paymentStatus === 2
    ? "bg-blue-50 text-blue-700"
    : order.paymentStatus === 3 || order.paymentStatus === 4
      ? "bg-red-50 text-red-700"
      : "bg-amber-50 text-amber-700";

  return (
    <div
      className="fixed inset-0 z-[100] grid place-items-center bg-slate-950/55 p-2 backdrop-blur-sm sm:p-5"
      role="presentation"
      onMouseDown={(event) => {
        if (event.target === event.currentTarget) onClose();
      }}
    >
      <section
        ref={dialogRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby="receipt-preview-title"
        tabIndex={-1}
        className="flex max-h-[96vh] w-full max-w-6xl flex-col overflow-hidden rounded-2xl bg-slate-100 shadow-2xl"
      >
        <div className="flex flex-wrap items-center justify-between gap-3 border-b border-slate-200 bg-white px-4 py-3 sm:px-6">
          <div>
            <h2 id="receipt-preview-title" className="text-base font-black text-slate-950 sm:text-lg">{t("receipt.preview")}</h2>
            <p className="text-xs font-medium text-slate-500">{t("receipt.previewHint")}</p>
          </div>
          <div className="flex items-center gap-2">
            <button
              type="button"
              disabled={isDownloading}
              onClick={onDownload}
              className="ui-control h-10 rounded-[var(--radius-control)] bg-[var(--brand)] px-4 font-bold text-white transition hover:brightness-110 disabled:cursor-not-allowed disabled:opacity-60"
            >
              {isDownloading ? t("receipt.downloading") : t("receipt.downloadPdf")}
            </button>
            <button
              ref={closeButtonRef}
              type="button"
              onClick={onClose}
              aria-label={t("receipt.closePreview")}
              className="grid h-10 w-10 place-items-center rounded-full border border-slate-200 bg-white text-xl text-slate-600 transition hover:border-slate-950 hover:text-slate-950"
            >
              ×
            </button>
          </div>
        </div>

        {downloadError ? <div className="border-b border-red-100 bg-red-50 px-6 py-3 text-sm font-semibold text-red-700">{downloadError}</div> : null}

        <div className="overflow-y-auto p-2 sm:p-6">
          <article className="mx-auto w-full max-w-[920px] bg-white px-5 py-7 text-slate-800 shadow-sm ring-1 ring-slate-200 sm:px-10 sm:py-10 lg:px-14">
            <header className="flex flex-col justify-between gap-8 border-b border-slate-200 pb-8 sm:flex-row sm:items-start">
              <div>
                <img src="/demo/logo.svg" alt="Workspace Ecommerce" className="h-auto w-[190px]" />
                <p className="mt-2 text-sm font-medium text-slate-500">workspaceecom.com</p>
              </div>
              <div className="sm:text-right">
                <p className="text-2xl font-black tracking-tight text-slate-950 sm:text-3xl">{t("receipt.documentTitle")}</p>
                <p className="mt-2 font-mono text-sm font-bold text-slate-600">#{order.orderCode}</p>
                <p className="mt-1 text-sm text-slate-500">{t("receipt.date")}: {formatReceiptDate(order.createdAt, language)}</p>
              </div>
            </header>

            <div className="grid gap-8 border-b border-slate-200 py-8 md:grid-cols-2 md:gap-16">
              <ReceiptSection title={t("receipt.seller")}>
                <p className="font-bold text-slate-950">Workspace Ecommerce</p>
                <p>support@workspaceecom.com</p>
                <p>1900 636 660</p>
                <p>workspaceecom.com</p>
              </ReceiptSection>
              <ReceiptSection title={t("receipt.customer")}>
                <p className="font-bold text-slate-950">{order.customerName}</p>
                <p>{order.customerPhone}</p>
                {order.customerEmail ? <p>{order.customerEmail}</p> : null}
                <p>{order.shippingAddress}</p>
              </ReceiptSection>
            </div>

            <section className="border-b border-slate-200 py-8">
              <h3 className="text-sm font-black uppercase tracking-[0.12em] text-slate-950">{t("receipt.orderInfo")}</h3>
              <div className="mt-5 grid gap-x-8 gap-y-4 text-sm sm:grid-cols-2 lg:grid-cols-3">
                <ReceiptMeta label={t("receipt.order")} value={`#${order.orderCode}`} />
                <ReceiptMeta label={t("receipt.payment")} value={paymentMethodText(order.paymentMethod, t)} />
                <ReceiptMeta label={t("receipt.status")} value={<span className={`rounded-md px-2 py-1 font-bold ${statusTone}`}>{paymentStatusText(order.paymentStatus, t)}</span>} />
                <ReceiptMeta label={t("receipt.orderStatus")} value={orderStatusText(order.status, t)} />
                <ReceiptMeta label={t("receipt.tracking")} value={order.trackingCode ?? t("receipt.notAvailable")} />
                <ReceiptMeta label={t("receipt.paidAt")} value={order.paidAt ? formatReceiptDate(order.paidAt, language) : t("receipt.notAvailable")} />
              </div>
            </section>

            <div className="mt-8 overflow-x-auto rounded-lg border border-slate-200">
              <table className="w-full min-w-[660px] text-sm">
                <thead className="bg-slate-50 text-left text-xs font-black uppercase tracking-wide text-slate-700">
                  <tr>
                    <th className="px-5 py-4">{t("receipt.product")}</th>
                    <th className="px-5 py-4 text-center">{t("receipt.qty")}</th>
                    <th className="px-5 py-4 text-right">{t("receipt.unitPrice")}</th>
                    <th className="px-5 py-4 text-right">{t("receipt.amount")}</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-200">
                  {order.items.map((item) => (
                    <tr key={item.id}>
                      <td className="px-5 py-5">
                        <div className="flex items-center gap-4">
                          {item.productImageUrlSnapshot ? (
                            <img
                              src={item.productImageUrlSnapshot}
                              alt=""
                              className="h-14 w-14 shrink-0 rounded-lg object-cover ring-1 ring-slate-200"
                            />
                          ) : (
                            <div className="grid h-14 w-14 shrink-0 place-items-center rounded-lg bg-gradient-to-br from-slate-50 to-slate-200 text-lg font-black text-slate-500 ring-1 ring-slate-200" aria-hidden="true">
                              {item.productNameSnapshot.trim().charAt(0).toUpperCase() || "P"}
                            </div>
                          )}
                          <div>
                            <p className="font-bold text-slate-950">{item.productNameSnapshot}</p>
                            <p className="mt-1 font-mono text-xs text-slate-500">{item.skuSnapshot}</p>
                            {item.requiresInstallation ? <p className="mt-1 text-xs font-semibold text-amber-700">{t("receipt.installationRequired")}</p> : null}
                          </div>
                        </div>
                      </td>
                      <td className="px-5 py-5 text-center font-semibold">{item.quantity}</td>
                      <td className="px-5 py-5 text-right">{formatMoney(item.unitPrice)}</td>
                      <td className="px-5 py-5 text-right font-bold text-slate-950">{formatMoney(item.lineTotal)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            <div className="ml-auto mt-7 grid max-w-[390px] gap-3 text-sm">
              <ReceiptTotal label={t("receipt.subtotal")} value={formatMoney(order.subtotal)} />
              {order.discountAmount > 0 ? <ReceiptTotal label={`${t("receipt.discount")}${order.couponCodeSnapshot ? ` (${order.couponCodeSnapshot})` : ""}`} value={`-${formatMoney(order.discountAmount)}`} accent /> : null}
              <ReceiptTotal label={t("receipt.shipping")} value={formatMoney(order.shippingFee)} />
              <div className="mt-1 flex items-center justify-between border-t border-slate-300 pt-4 text-xl font-black text-slate-950">
                <span>{t("receipt.total")}</span>
                <span className="text-blue-600">{formatMoney(order.totalAmount)}</span>
              </div>
            </div>

            <div className="mt-10 grid gap-8 border-y border-slate-200 py-8 md:grid-cols-2 md:gap-16">
              <ReceiptSection title={t("receipt.payment")}>
                <p>{paymentMethodText(order.paymentMethod, t)}</p>
                <p>{paymentStatusText(order.paymentStatus, t)}</p>
                {order.paidAt ? <p>{t("receipt.paidOn")} {formatReceiptDate(order.paidAt, language)}</p> : null}
              </ReceiptSection>
              <ReceiptSection title={t("receipt.notes")}>
                <p>{order.note || t("receipt.thankYou")}</p>
                <p className="font-semibold text-rose-700">{t("receipt.notTaxInvoice")}</p>
              </ReceiptSection>
            </div>

            <footer className="flex flex-wrap justify-center gap-x-5 gap-y-2 pt-8 text-center text-xs font-medium text-slate-500">
              <span>support@workspaceecom.com</span><span aria-hidden="true">|</span><span>1900 636 660</span><span aria-hidden="true">|</span><span>workspaceecom.com</span>
            </footer>
          </article>
        </div>
      </section>
    </div>
  );
}

function ReceiptSection({ title, children }: { title: string; children: ReactNode }) {
  return <section><h3 className="mb-4 text-sm font-black uppercase tracking-[0.12em] text-slate-950">{title}</h3><div className="grid gap-2 text-sm text-slate-600">{children}</div></section>;
}

function ReceiptMeta({ label, value }: { label: string; value: ReactNode }) {
  return <div className="flex min-w-0 items-center gap-2"><span className="font-semibold text-slate-500">{label}:</span><span className="min-w-0 font-bold text-slate-800">{value}</span></div>;
}

function ReceiptTotal({ label, value, accent = false }: { label: string; value: string; accent?: boolean }) {
  return <div className={`flex items-center justify-between gap-6 ${accent ? "text-blue-600" : "text-slate-700"}`}><span>{label}</span><span className="font-semibold">{value}</span></div>;
}

function formatReceiptDate(value: string, locale: string): string {
  return new Intl.DateTimeFormat(locale, { dateStyle: "medium", timeStyle: "short" }).format(new Date(value));
}

type Translate = (key: string) => string;

function orderStatusText(status: OrderStatus, t: Translate): string {
  return t(`receipt.orderStatuses.${status}`);
}

function paymentMethodText(method: PaymentMethod, t: Translate): string {
  return t(`receipt.paymentMethods.${method}`);
}

function paymentStatusText(status: PaymentStatus, t: Translate): string {
  return t(`receipt.paymentStatuses.${status}`);
}
