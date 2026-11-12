import { ChevronLeft, ChevronRight } from "lucide-react";
import { IconButton } from "./AdminUi";
interface PageData {
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}
export function Pagination({
  data,
  label = "results",
  busy = false,
  onPageChange,
}: {
  data: PageData;
  label?: string;
  busy?: boolean;
  onPageChange: (page: number) => void;
}) {
  const start = data.totalCount ? (data.pageNumber - 1) * data.pageSize + 1 : 0;
  const end = Math.min(data.pageNumber * data.pageSize, data.totalCount);
  return (
    <div className="flex flex-wrap items-center justify-between gap-3 py-2 text-sm text-[var(--muted)]">
      <p>
        {data.totalPages > 1 ? `${start}–${end} of ` : ""}
        {data.totalCount} {label}
      </p>
      {data.totalPages > 1 ? (
        <nav
          aria-label={`${label} pagination`}
          className="flex items-center gap-2"
        >
          <IconButton
            label="Previous page"
            disabled={!data.hasPreviousPage || busy}
            onClick={() => onPageChange(Math.max(1, data.pageNumber - 1))}
          >
            <ChevronLeft size={18} aria-hidden="true" />
          </IconButton>
          <span className="px-2 text-xs tabular-nums">
            {data.pageNumber} / {data.totalPages}
          </span>
          <IconButton
            label="Next page"
            disabled={!data.hasNextPage || busy}
            onClick={() => onPageChange(data.pageNumber + 1)}
          >
            <ChevronRight size={18} aria-hidden="true" />
          </IconButton>
        </nav>
      ) : null}
    </div>
  );
}
