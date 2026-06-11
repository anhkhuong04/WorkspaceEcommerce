interface PageHeaderProps {
  eyebrow: string;
  title: string;
  description: string;
}

export function PageHeader({ eyebrow, title, description }: PageHeaderProps) {
  return (
    <section className="ui-card border border-slate-100 p-4 md:p-8">
      <p className="ui-caption uppercase tracking-[0.2em] text-[var(--brand)]">{eyebrow}</p>
      <div className="mt-3 grid gap-4 lg:grid-cols-[1fr_420px] lg:items-end">
        <h1 className="ui-h1 max-w-4xl tracking-tight text-slate-950">{title}</h1>
        <p className="ui-body text-slate-600">{description}</p>
      </div>
    </section>
  );
}
