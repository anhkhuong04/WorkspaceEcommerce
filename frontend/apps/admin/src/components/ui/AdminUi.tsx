import type { ButtonHTMLAttributes, InputHTMLAttributes, ReactNode, SelectHTMLAttributes, TextareaHTMLAttributes } from "react";
import { cx } from "./cx";

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: "primary" | "secondary" | "ghost" | "danger";
  fullWidth?: boolean;
}

export function Button({ className, variant = "secondary", fullWidth = false, disabled, ...props }: ButtonProps) {
  const variants = {
    primary: "border-teal-700 bg-teal-700 text-white hover:bg-teal-800",
    secondary: "border-slate-200 bg-white text-slate-800 hover:border-teal-300 hover:text-teal-800",
    ghost: "border-transparent bg-transparent text-slate-600 hover:bg-slate-100 hover:text-slate-900",
    danger: "border-red-200 bg-red-50 text-red-700 hover:bg-red-100"
  };

  return (
    <button
      {...props}
      disabled={disabled}
      className={cx(
        "inline-flex items-center justify-center rounded-xl border px-4 py-2 text-sm font-bold shadow-sm transition disabled:cursor-not-allowed disabled:opacity-60",
        variants[variant],
        fullWidth && "w-full",
        className
      )}
    />
  );
}

interface NoticeProps {
  type?: "error" | "warning" | "success" | "info";
  title: string;
  children?: ReactNode;
}

export function Notice({ type = "info", title, children }: NoticeProps) {
  const styles = {
    error: "border-red-200 bg-red-50 text-red-900",
    warning: "border-amber-200 bg-amber-50 text-amber-900",
    success: "border-emerald-200 bg-emerald-50 text-emerald-900",
    info: "border-sky-200 bg-sky-50 text-sky-900"
  };

  return (
    <div className={cx("rounded-2xl border p-4", styles[type])}>
      <p className="font-bold">{title}</p>
      {children ? <div className="mt-1 text-sm opacity-85">{children}</div> : null}
    </div>
  );
}

interface ModalProps {
  title: string;
  open: boolean;
  children: ReactNode;
  footer?: ReactNode;
  widthClass?: string;
  onClose: () => void;
}

export function Modal({ title, open, children, footer, widthClass = "max-w-xl", onClose }: ModalProps) {
  if (!open) {
    return null;
  }

  return (
    <div className="fixed inset-0 z-50 grid place-items-center bg-slate-950/45 p-4 backdrop-blur-sm" role="dialog" aria-modal="true">
      <div className={cx("max-h-[90vh] w-full overflow-hidden rounded-3xl bg-white shadow-2xl", widthClass)}>
        <div className="flex items-center justify-between border-b border-slate-100 px-6 py-4">
          <h2 className="text-xl font-black text-slate-950">{title}</h2>
          <button className="rounded-full px-3 py-1 text-2xl leading-none text-slate-500 hover:bg-slate-100" onClick={onClose} aria-label="Close">
            x
          </button>
        </div>
        <div className="max-h-[calc(90vh-140px)] overflow-y-auto px-6 py-5">{children}</div>
        {footer ? <div className="flex justify-end gap-3 border-t border-slate-100 px-6 py-4">{footer}</div> : null}
      </div>
    </div>
  );
}

interface FieldProps {
  label: string;
  error?: string;
  children: ReactNode;
}

export function Field({ label, error, children }: FieldProps) {
  return (
    <label className="block">
      <span className="mb-1.5 block text-sm font-bold text-slate-700">{label}</span>
      {children}
      {error ? <span className="mt-1 block text-sm font-semibold text-red-600">{error}</span> : null}
    </label>
  );
}

export function TextInput(props: InputHTMLAttributes<HTMLInputElement>) {
  return <input {...props} className={cx("w-full rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm outline-none transition focus:border-teal-500 focus:ring-4 focus:ring-teal-100 disabled:bg-slate-100", props.className)} />;
}

export function TextArea(props: TextareaHTMLAttributes<HTMLTextAreaElement>) {
  return <textarea {...props} className={cx("w-full rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm outline-none transition focus:border-teal-500 focus:ring-4 focus:ring-teal-100 disabled:bg-slate-100", props.className)} />;
}

export function SelectInput(props: SelectHTMLAttributes<HTMLSelectElement>) {
  return <select {...props} className={cx("w-full rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm outline-none transition focus:border-teal-500 focus:ring-4 focus:ring-teal-100 disabled:bg-slate-100", props.className)} />;
}

interface ToggleProps {
  checked: boolean;
  disabled?: boolean;
  onChange: (checked: boolean) => void;
}

export function Toggle({ checked, disabled, onChange }: ToggleProps) {
  return (
    <button
      type="button"
      disabled={disabled}
      onClick={() => onChange(!checked)}
      className={cx(
        "relative h-6 w-11 rounded-full transition disabled:opacity-60",
        checked ? "bg-teal-700" : "bg-slate-300"
      )}
      aria-pressed={checked}
    >
      <span className={cx("absolute top-1 h-4 w-4 rounded-full bg-white shadow transition", checked ? "left-6" : "left-1")} />
    </button>
  );
}

interface PillProps {
  children: ReactNode;
  tone?: "green" | "red" | "blue" | "orange" | "slate" | "teal";
}

export function Pill({ children, tone = "slate" }: PillProps) {
  const tones = {
    green: "bg-emerald-50 text-emerald-700 ring-emerald-200",
    red: "bg-red-50 text-red-700 ring-red-200",
    blue: "bg-blue-50 text-blue-700 ring-blue-200",
    orange: "bg-orange-50 text-orange-700 ring-orange-200",
    slate: "bg-slate-100 text-slate-700 ring-slate-200",
    teal: "bg-teal-50 text-teal-700 ring-teal-200"
  };

  return <span className={cx("inline-flex rounded-full px-2.5 py-1 text-xs font-black ring-1", tones[tone])}>{children}</span>;
}

export function EmptyState({ children }: { children: ReactNode }) {
  return <div className="rounded-2xl border border-dashed border-slate-200 bg-slate-50 p-8 text-center text-sm font-semibold text-slate-500">{children}</div>;
}



