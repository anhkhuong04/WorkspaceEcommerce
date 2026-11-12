export function AdminBrand() {
  return (
    <div className="flex items-center gap-3">
      <span className="h-9 w-9 shrink-0 overflow-hidden" aria-hidden="true">
        <img src="/demo/logo.svg" alt="" className="h-9 w-[212px] max-w-none" />
      </span>
      <div>
        <p className="font-semibold tracking-tight">Workspace</p>
        <p className="text-xs text-[var(--muted)]">Admin console</p>
      </div>
    </div>
  );
}
