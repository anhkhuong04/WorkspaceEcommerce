interface PageHeaderProps {
  eyebrow: string;
  title: string;
  description: string;
}

export function PageHeader({ eyebrow, title, description }: PageHeaderProps) {
  return (
    <section className="rounded-[2rem] border border-slate-200 bg-white p-8 shadow-sm">
      <p className="text-sm font-bold uppercase tracking-[0.2em] text-[var(--brand)]">{eyebrow}</p>
      <div className="mt-3 grid gap-4 lg:grid-cols-[1fr_420px] lg:items-end">
        <h1 className="max-w-4xl text-5xl font-black tracking-tight text-slate-950">{title}</h1>
        <p className="text-base leading-7 text-slate-600">{description}</p>
      </div>
    </section>
  );
}
