import { useMutation } from "@tanstack/react-query";
import type {
  AdminOrderImportCommitResultDto,
  AdminOrderImportPreviewDto,
} from "@workspace-ecommerce/api-types";
import type { ChangeEvent } from "react";
import { useState } from "react";
import {
  Button,
  EmptyState,
  Modal,
  Notice,
  Field,
  TextInput,
} from "../../../components/ui/AdminUi";
import { adminApi } from "../../../services/api/adminApi";
import { getApiErrorMessage } from "../../../services/api/errors";

interface OrderImportModalProps {
  open: boolean;
  onClose: () => void;
  onImported: (result: AdminOrderImportCommitResultDto) => void;
}

export function OrderImportModal({
  open,
  onClose,
  onImported,
}: OrderImportModalProps) {
  const [file, setFile] = useState<File | null>(null);
  const [source, setSource] = useState("manual");
  const [preview, setPreview] = useState<AdminOrderImportPreviewDto | null>(
    null,
  );
  const [notice, setNotice] = useState<{
    type: "success" | "error";
    message: string;
  } | null>(null);

  const previewMutation = useMutation({
    mutationFn: (selectedFile: File) =>
      adminApi.previewOrderImport(selectedFile, source),
    onSuccess: (result) => {
      setPreview(result);
      setNotice({
        type: result.errorRows > 0 ? "error" : "success",
        message:
          result.errorRows > 0
            ? `Preview found ${result.errorRows} row(s) with errors.`
            : `Preview passed for ${result.validRows} row(s).`,
      });
    },
    onError: (error) =>
      setNotice({ type: "error", message: getApiErrorMessage(error) }),
  });

  const commitMutation = useMutation({
    mutationFn: (selectedFile: File) =>
      adminApi.commitOrderImport(selectedFile, source),
    onSuccess: (result) => {
      setPreview(result.preview);
      if (result.createdOrders > 0 || (result.skippedOrders ?? 0) > 0) {
        onImported(result);
        setFile(null);
        setPreview(null);
        setNotice(null);
      } else {
        setNotice({
          type: "error",
          message:
            "Import was not committed because the file has validation errors.",
        });
      }
    },
    onError: (error) =>
      setNotice({ type: "error", message: getApiErrorMessage(error) }),
  });

  function handleFileChange(event: ChangeEvent<HTMLInputElement>) {
    const [selectedFile] = event.target.files ?? [];
    setFile(selectedFile ?? null);
    setPreview(null);
    setNotice(null);
  }

  function close() {
    if (!previewMutation.isPending && !commitMutation.isPending) {
      onClose();
    }
  }

  const canCommit = Boolean(
    file && /^[a-zA-Z0-9_.-]{1,80}$/.test(source) && preview && preview.errorRows === 0 && preview.validRows > 0,
  );

  return (
    <Modal
      title="Import orders"
      open={open}
      busy={previewMutation.isPending || commitMutation.isPending}
      onClose={close}
      widthClass="max-w-5xl"
      footer={
        <>
          <Button
            type="button"
            disabled={previewMutation.isPending || commitMutation.isPending}
            onClick={close}
          >
            Cancel
          </Button>
          <Button
            type="button"
            disabled={
              !file || previewMutation.isPending || commitMutation.isPending
            }
            onClick={() => file && previewMutation.mutate(file)}
          >
            {previewMutation.isPending ? "Previewing..." : "Preview"}
          </Button>
          <Button
            type="button"
            variant="primary"
            disabled={
              !canCommit ||
              previewMutation.isPending ||
              commitMutation.isPending
            }
            onClick={() => file && commitMutation.mutate(file)}
          >
            {commitMutation.isPending ? "Importing..." : "Import orders"}
          </Button>
        </>
      }
    >
      <div className="grid gap-4">
        <p className="text-sm text-[var(--muted-strong)]">Create new orders for manual delivery using COD or bank transfer. Confirm verified payment before completing delivery. This import reserves stock; carrier bookings and VNPay payments use storefront checkout.</p>
        <Field label="Import source" error={source && !/^[a-zA-Z0-9_.-]{1,80}$/.test(source) ? "Use letters, digits, dots, underscores or hyphens." : undefined}>
          <TextInput value={source} maxLength={80} onChange={event => { setSource(event.target.value); setPreview(null); }} />
        </Field>
        <p className="text-xs text-[var(--muted)]">Keep the same source and external order code when retrying. Identical orders are skipped; changed data is reported as a conflict. Use a different source for a separate sales channel.</p>
        <div>
          <label
            htmlFor="order-import-file"
            className="mb-1.5 block text-sm font-medium text-slate-700"
          >
            CSV or XLSX file
          </label>
          <input
            id="order-import-file"
            type="file"
            accept=".csv,.xlsx,text/csv,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            onChange={handleFileChange}
            className="w-full rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm outline-none transition file:mr-4 file:rounded-lg file:border-0 file:bg-slate-900 file:px-3 file:py-1.5 file:text-sm file:font-medium file:text-white focus:border-slate-400 focus:ring-4 focus:ring-slate-100"
          />
          <p className="mt-2 text-xs font-semibold text-slate-500">
            Required columns: externalOrderCode, customerName, customerPhone,
            shippingStreet, shippingWard, shippingProvince, sku, quantity.
          </p>
        </div>

        {notice ? <Notice type={notice.type} title={notice.message} /> : null}

        {preview ? (
          <ImportPreviewTable preview={preview} />
        ) : (
          <EmptyState>
            Select a CSV/XLSX file and run preview before importing.
          </EmptyState>
        )}
      </div>
    </Modal>
  );
}

function ImportPreviewTable({
  preview,
}: {
  preview: AdminOrderImportPreviewDto;
}) {
  return (
    <section className="rounded-2xl border border-slate-200">
      <div className="flex flex-wrap gap-3 border-b border-slate-100 p-4 text-sm font-medium text-slate-700">
        <span>Total rows: {preview.totalRows}</span>
        <span>Valid: {preview.validRows}</span>
        <span>Errors: {preview.errorRows}</span>
      </div>
      <div className="max-h-[420px] overflow-auto">
        <table className="w-full min-w-[820px] text-left text-sm">
          <thead className="sticky top-0 bg-white text-xs uppercase tracking-wide text-slate-500">
            <tr className="border-b border-slate-100">
              <th className="py-3 pl-4 pr-3">Row</th>
              <th className="py-3 pr-3">External order</th>
              <th className="py-3 pr-3">SKU</th>
              <th className="py-3 pr-3">Qty</th>
              <th className="py-3 pr-4">Status</th>
            </tr>
          </thead>
          <tbody>
            {preview.rows.map((row) => (
              <tr
                key={row.rowNumber}
                className="border-b border-slate-100 last:border-0"
              >
                <td className="py-3 pl-4 pr-3 font-medium text-slate-700">
                  {row.rowNumber}
                </td>
                <td className="py-3 pr-3 text-slate-700">
                  {row.externalOrderCode || "-"}
                </td>
                <td className="py-3 pr-3 font-semibold text-slate-900">
                  {row.sku || "-"}
                </td>
                <td className="py-3 pr-3 text-slate-700">
                  {row.quantity ?? "-"}
                </td>
                <td className="py-3 pr-4">
                  {row.isValid ? (
                    <span className="font-medium text-emerald-700">Valid</span>
                  ) : (
                    <span className="font-semibold text-red-700">
                      {row.errors.join("; ")}
                    </span>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}
