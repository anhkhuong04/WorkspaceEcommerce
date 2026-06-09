import type { ReactNode } from "react";

interface AdminPageHeaderProps {
  title: string;
  description: string;
  actions?: ReactNode;
}

export function AdminPageHeader({ title, description, actions }: AdminPageHeaderProps) {
  return (
    <div className="flex flex-col gap-5 rounded-[1.75rem] border border-slate-200 bg-white p-6 shadow-sm lg:flex-row lg:items-end lg:justify-between">
      <div>
        <p className="text-xs font-black uppercase tracking-[0.18em] text-teal-700">Admin</p>
        <h1 className="mt-1 text-3xl font-black text-slate-950">{title}</h1>
      </div>
      <div className="flex flex-col gap-4 lg:items-end">
        <p className="max-w-xl text-sm leading-6 text-slate-500">{description}</p>
        {actions}
      </div>
    </div>
  );
}
