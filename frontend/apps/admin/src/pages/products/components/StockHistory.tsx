import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { ApiClient } from "@workspace-ecommerce/api-client";
import type { PagedResult } from "@workspace-ecommerce/api-types";
import { formatDate } from "@workspace-ecommerce/shared-utils";
import { getAdminToken } from "../../../services/api/adminApi";
import { getApiErrorMessage } from "../../../services/api/errors";
import { Button, Notice } from "../../../components/ui/AdminUi";
import { Pagination } from "../../../components/ui/Pagination";

const client = new ApiClient({
  baseUrl: import.meta.env.VITE_API_BASE_URL ?? "",
  getAccessToken: getAdminToken,
});
interface StockAdjustment {
  id: string;
  beforeQuantity: number;
  afterQuantity: number;
  reason: string;
  actor: string;
  occurredAt: string;
}
export function StockHistory({ variantId }: { variantId: string }) {
  const [page, setPage] = useState(1);
  const query = useQuery({
    queryKey: ["admin-stock-history", variantId, page],
    queryFn: () =>
      client.get<PagedResult<StockAdjustment>>(
        `/api/admin/product-variants/${variantId}/stock-history?pageNumber=${page}`,
      ),
  });
  return (
    <section
      className="mt-6 border-t pt-4"
      aria-label="Manual stock adjustment history"
    >
      <h3 className="font-medium">Manual stock adjustments</h3>
      <p className="mt-1 text-xs text-[var(--muted)]">
        Manual count changes only. Sales, returns and warranty replacement
        retain their own transaction history.
      </p>
      {query.isPending && <p role="status">Loading adjustments...</p>}
      {query.isError && (
        <Notice type="error" title={getApiErrorMessage(query.error)}>
          <Button onClick={() => void query.refetch()}>
            Retry stock history
          </Button>
        </Notice>
      )}
      {query.data?.items.length === 0 && (
        <p className="py-4 text-sm text-[var(--muted)]">
          No manual adjustments recorded.
        </p>
      )}
      <ol className="mt-3 grid gap-3">
        {query.data?.items.map((item) => (
          <li key={item.id} className="rounded-lg border p-3 text-sm">
            <p className="font-medium">
              {item.beforeQuantity} to {item.afterQuantity} units
            </p>
            <p className="mt-1 break-words">{item.reason}</p>
            <p className="mt-1 break-all text-xs text-[var(--muted)]">
              {formatDate(item.occurredAt)} · {item.actor}
            </p>
          </li>
        ))}
      </ol>
      {query.data && (
        <Pagination
          data={query.data}
          label="manual adjustments"
          onPageChange={setPage}
          busy={query.isFetching}
        />
      )}
    </section>
  );
}
