import { useQuery } from "@tanstack/react-query";
import { useEffect, useId, useState } from "react";
import { adminApi } from "../../services/api/adminApi";
import { getApiErrorMessage } from "../../services/api/errors";
import { formatLocalizedText } from "../../utils/localizedText";
import { Button, EmptyState, Notice, TextInput } from "./AdminUi";
import { Pagination } from "./Pagination";

interface Props {
  selectedIds: string[];
  onChange: (ids: string[]) => void;
  label: string;
  error?: string;
  search?: string;
  onSearchChange?: (value: string) => void;
}
export function ProductChecklist({ selectedIds, onChange, label, error, search, onSearchChange }: Props) {
  const id = useId();
  const [localSearch, setLocalSearch] = useState("");
  const [debounced, setDebounced] = useState("");
  const [page, setPage] = useState(1);
  const value = search ?? localSearch;
  useEffect(() => {
    const timer = window.setTimeout(() => { setDebounced(value.trim()); setPage(1); }, 250);
    return () => window.clearTimeout(timer);
  }, [value]);
  const results = useQuery({
    queryKey: ["admin-products", "picker", debounced, page],
    queryFn: () => adminApi.getProducts({ search: debounced || undefined, pageNumber: page, pageSize: 20 }),
  });
  const ids = [...new Set(selectedIds)].sort().join(",");
  const selected = useQuery({
    queryKey: ["admin-products", "picker-selected", ids],
    queryFn: () => adminApi.getProducts({ ids, pageSize: 100 }),
    enabled: ids.length > 0,
  });
  const names = new Map(selected.data?.items.map(product => [product.id, formatLocalizedText(product.name)]));
  return <fieldset className="m-0 min-w-0 border-0 p-0" aria-describedby={error ? `${id}-error` : undefined}>
    <legend className="mb-3 text-sm font-medium">{label} <span className="font-normal text-[var(--muted)]">({selectedIds.length} selected)</span></legend>
    {selectedIds.length > 0 && <div className="mb-3 flex max-h-32 flex-wrap gap-2 overflow-y-auto" aria-label="Selected products">
      {selectedIds.map(productId => <Button key={productId} onClick={() => onChange(selectedIds.filter(item => item !== productId))} aria-label={`Remove ${names.get(productId) ?? "selected product"}`}>
        {names.get(productId) ?? (selected.isPending ? "Loading name..." : "Unavailable product")} <span aria-hidden="true">x</span>
      </Button>)}
    </div>}
    {selected.isError && ids && <Notice type="error" title="Selected product names could not be loaded"><Button onClick={() => void selected.refetch()}>Retry names</Button></Notice>}
    <label htmlFor={id} className="mb-1.5 block text-xs text-[var(--muted)]">Find products</label>
    <TextInput id={id} type="search" value={value} maxLength={150} placeholder="Product name, slug or SKU" onChange={event => onSearchChange ? onSearchChange(event.target.value) : setLocalSearch(event.target.value)} />
    {results.isError ? <div className="mt-3"><Notice type="error" title={getApiErrorMessage(results.error)}><Button onClick={() => void results.refetch()}>Try again</Button></Notice></div> : results.isPending ? <p className="py-5 text-sm text-[var(--muted)]" role="status">Loading products...</p> : <>
      {results.data.items.length ? <div className="admin-table-scroll mt-3 max-h-64 overflow-y-auto rounded-lg border border-slate-200" tabIndex={0} role="region" aria-label={`${label} choices`}>
        {results.data.items.map(product => <label key={product.id} className="flex min-h-12 cursor-pointer items-center gap-3 border-b border-slate-100 px-3 py-3 last:border-0 hover:bg-slate-50">
          <input type="checkbox" className="h-4 w-4 shrink-0 accent-[var(--brand)]" checked={selectedIds.includes(product.id)} disabled={!selectedIds.includes(product.id) && selectedIds.length >= 100} onChange={event => onChange(event.target.checked ? [...selectedIds, product.id] : selectedIds.filter(item => item !== product.id))} />
          <span className="min-w-0"><span className="block text-sm">{formatLocalizedText(product.name)}</span><span className="mt-1 block text-xs text-[var(--muted)]">{product.categoryName}{!product.isActive ? " - Inactive" : ""}</span></span>
        </label>)}
      </div> : <EmptyState>No products match this search</EmptyState>}
      <Pagination data={results.data} label="product choices" busy={results.isFetching} onPageChange={setPage} />
    </>}
    {error && <p id={`${id}-error`} className="mt-2 text-sm text-red-700" role="alert">{error}</p>}
  </fieldset>;
}
