import { useQuery } from "@tanstack/react-query";
import { Search } from "lucide-react";
import { useEffect, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { adminApi } from "../../services/api/adminApi";
import { getApiErrorMessage } from "../../services/api/errors";
import { Button, Field, Modal, Notice, TextInput } from "../ui/AdminUi";

export function AdminSearch() {
  const [open, setOpen] = useState(false);
  const [search, setSearch] = useState("");
  const [term, setTerm] = useState("");
  const results = useRef<HTMLDivElement>(null);
  useEffect(() => {
    const timer = window.setTimeout(() => setTerm(search.trim()), 250);
    return () => window.clearTimeout(timer);
  }, [search]);
  useEffect(() => {
    const shortcut = (event: KeyboardEvent) => {
      if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "k") {
        event.preventDefault();
        setOpen((value) => !value);
      }
    };
    window.addEventListener("keydown", shortcut);
    return () => window.removeEventListener("keydown", shortcut);
  }, []);
  const query = useQuery({
    queryKey: ["admin-search", term],
    queryFn: () => adminApi.search(term),
    enabled: open && term.length >= 2,
    staleTime: 10000,
  });
  const pending = search.trim() !== term || query.isFetching;
  const items = query.data?.items ?? [];
  const kinds = [...new Set(items.map((item) => item.kind))];
  function close() {
    setOpen(false);
    setSearch("");
    setTerm("");
  }
  return (
    <>
      <button
        type="button"
        aria-label="Open admin search"
        onClick={() => setOpen(true)}
        className="flex min-h-10 min-w-0 items-center gap-2 rounded-lg border border-[var(--border)] bg-slate-50 px-3 text-left text-sm text-[var(--muted)] sm:w-72 xl:w-96"
      >
        <Search size={17} className="shrink-0" aria-hidden="true" />
        <span className="truncate sm:hidden">Search</span>
        <span className="hidden min-w-0 flex-1 truncate sm:block">
          Search orders, products, SKU...
        </span>
        <kbd className="hidden shrink-0 rounded border bg-white px-1.5 text-xs xl:block">
          Ctrl K
        </kbd>
      </button>
      <Modal
        open={open}
        title="Search your store"
        onClose={close}
        widthClass="max-w-2xl"
      >
        <Field label="Search admin">
          <TextInput
            type="search"
            autoComplete="off"
            autoFocus
            maxLength={150}
            value={search}
            placeholder="Order code, customer, product name, SKU, coupon..."
            onChange={(event) => setSearch(event.target.value)}
            aria-describedby="admin-search-help"
            onKeyDown={(event) => {
              if (event.key === "ArrowDown") {
                event.preventDefault();
                results.current
                  ?.querySelector<HTMLAnchorElement>("a[data-search-result]")
                  ?.focus();
              }
            }}
          />
        </Field>
        <p id="admin-search-help" className="mt-2 text-xs text-[var(--muted)]">
          Enter at least 2 characters. Up to 5 matches per section; use Tab or
          Arrow Down to reach results.
        </p>
        <div className="mt-4" role="status" aria-live="polite">
          {term.length >= 2 &&
            (pending
              ? "Searching..."
              : !query.isError
                ? `${items.length} results`
                : "")}
        </div>
        {query.isError && (
          <Notice type="error" title="Search could not be loaded">
            {getApiErrorMessage(query.error)}
            <Button className="mt-2" onClick={() => void query.refetch()}>
              Try again
            </Button>
          </Notice>
        )}
        <div ref={results} className="mt-2 grid gap-4">
          {!pending &&
            term.length >= 2 &&
            query.isSuccess &&
            items.length === 0 && (
              <p className="py-6 text-center text-sm text-[var(--muted)]">
                No matches. Try another name, code or SKU.
              </p>
            )}
          {!pending &&
            kinds.map((kind) => (
              <section key={kind} aria-label={kind}>
                <h3 className="mb-1 text-xs font-semibold uppercase tracking-wide text-[var(--muted)]">
                  {kind}
                </h3>
                <ul className="divide-y divide-slate-100">
                  {items
                    .filter((item) => item.kind === kind)
                    .map((item) => (
                      <li key={`${item.kind}:${item.id}`}>
                        <Link
                          data-search-result
                          to={item.url}
                          onClick={close}
                          className="block rounded-lg px-3 py-3 hover:bg-[var(--brand-soft)]"
                        >
                          <span className="block break-words text-sm font-medium">
                            {item.title}
                          </span>
                          {item.detail && (
                            <span className="mt-1 block break-words text-xs text-[var(--muted)]">
                              {item.detail}
                            </span>
                          )}
                        </Link>
                      </li>
                    ))}
                </ul>
              </section>
            ))}
        </div>
      </Modal>
    </>
  );
}
