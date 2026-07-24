import type { ReactNode } from "react";

interface AdminPageHeaderProps {
  title: string;
  description: string;
  actions?: ReactNode;
}

export function AdminPageHeader({ title, description, actions }: AdminPageHeaderProps) {
  return (
    <div className="flex flex-col gap-5 rounded-[var(--radius-shell)] border border-[var(--border)] bg-[var(--surface-card)] p-6 shadow-[var(--shadow-card)] lg:flex-row lg:items-end lg:justify-between">
      <div>
        <p className="text-xs font-black uppercase tracking-[0.18em] text-[var(--muted)]">WorkspaceEcom Admin</p>
        <h1 className="mt-1 text-3xl font-black text-[var(--ink)]">{title}</h1>
      </div>
      <div className="flex flex-col gap-4 lg:items-end">
        <p className="max-w-xl text-sm leading-6 text-[var(--muted)]">{description}</p>
        {actions}
      </div>
    </div>
  );
}
