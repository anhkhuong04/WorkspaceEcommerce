import type { ButtonHTMLAttributes, InputHTMLAttributes, ReactNode, SelectHTMLAttributes, TextareaHTMLAttributes } from "react";
import { useEffect, useId, useRef } from "react";
import { cx } from "./cx";

const focusableSelector = [
  "a[href]",
  "button:not([disabled])",
  "textarea:not([disabled])",
  "input:not([disabled])",
  "select:not([disabled])",
  "[tabindex]:not([tabindex='-1'])"
].join(",");

function getFocusableElements(container: HTMLElement) {
  return Array.from(container.querySelectorAll<HTMLElement>(focusableSelector)).filter((element) => !element.hasAttribute("aria-hidden"));
}

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: "primary" | "secondary" | "ghost" | "danger";
  fullWidth?: boolean;
}

export function Button({ className, variant = "secondary", fullWidth = false, disabled, ...props }: ButtonProps) {
  const variants = {
    primary: "border-slate-900 bg-slate-900 text-white hover:bg-black hover:border-black",
    secondary: "border-slate-200 bg-white text-slate-800 hover:border-slate-400 hover:text-slate-900",
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
  const titleId = useId();
  const dialogRef = useRef<HTMLDivElement>(null);
  const previouslyFocusedElementRef = useRef<HTMLElement | null>(null);

  useEffect(() => {
    if (!open) {
      return;
    }

    previouslyFocusedElementRef.current = document.activeElement instanceof HTMLElement ? document.activeElement : null;

    window.setTimeout(() => {
      const dialog = dialogRef.current;
      if (!dialog) {
        return;
      }

      const [firstFocusableElement] = getFocusableElements(dialog);
      (firstFocusableElement ?? dialog).focus();
    }, 0);

    return () => {
      previouslyFocusedElementRef.current?.focus();
    };
  }, [open]);

  useEffect(() => {
    if (!open) {
      return;
    }

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === "Escape") {
        event.preventDefault();
        onClose();
        return;
      }

      if (event.key !== "Tab") {
        return;
      }

      const dialog = dialogRef.current;
      if (!dialog) {
        return;
      }

      const focusableElements = getFocusableElements(dialog);
      if (focusableElements.length === 0) {
        event.preventDefault();
        dialog.focus();
        return;
      }

      const firstElement = focusableElements[0];
      const lastElement = focusableElements[focusableElements.length - 1];

      if (event.shiftKey && document.activeElement === firstElement) {
        event.preventDefault();
        lastElement.focus();
      } else if (!event.shiftKey && document.activeElement === lastElement) {
        event.preventDefault();
        firstElement.focus();
      }
    }

    document.addEventListener("keydown", handleKeyDown);
    return () => document.removeEventListener("keydown", handleKeyDown);
  }, [onClose, open]);

  if (!open) {
    return null;
  }

  return (
    <div className="fixed inset-0 z-50 grid place-items-center bg-slate-950/45 p-4 backdrop-blur-sm" role="presentation">
      <div
        ref={dialogRef}
        className={cx("max-h-[90vh] w-full overflow-hidden rounded-3xl bg-white shadow-2xl outline-none", widthClass)}
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        tabIndex={-1}
      >
        <div className="flex items-center justify-between border-b border-slate-100 px-6 py-4">
          <h2 id={titleId} className="text-xl font-black text-slate-950">{title}</h2>
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

interface ConfirmDialogProps {
  open: boolean;
  title: string;
  message: ReactNode;
  confirmLabel?: string;
  cancelLabel?: string;
  busy?: boolean;
  onCancel: () => void;
  onConfirm: () => void;
}

export function ConfirmDialog({
  open,
  title,
  message,
  confirmLabel = "Confirm",
  cancelLabel = "Cancel",
  busy = false,
  onCancel,
  onConfirm
}: ConfirmDialogProps) {
  return (
    <Modal
      title={title}
      open={open}
      onClose={onCancel}
      widthClass="max-w-md"
      footer={(
        <>
          <Button type="button" disabled={busy} onClick={onCancel}>{cancelLabel}</Button>
          <Button type="button" variant="danger" disabled={busy} onClick={onConfirm}>{busy ? "Working..." : confirmLabel}</Button>
        </>
      )}
    >
      <div className="text-sm font-semibold leading-6 text-slate-600">{message}</div>
    </Modal>
  );
}

interface DrawerProps {
  title: string;
  open: boolean;
  children: ReactNode;
  widthClass?: string;
  onClose: () => void;
}

export function Drawer({ title, open, children, widthClass = "max-w-3xl", onClose }: DrawerProps) {
  const titleId = useId();
  const drawerRef = useRef<HTMLElement>(null);
  const previouslyFocusedElementRef = useRef<HTMLElement | null>(null);

  useEffect(() => {
    if (!open) {
      return;
    }

    previouslyFocusedElementRef.current = document.activeElement instanceof HTMLElement ? document.activeElement : null;

    window.setTimeout(() => {
      const drawer = drawerRef.current;
      if (!drawer) {
        return;
      }

      const [firstFocusableElement] = getFocusableElements(drawer);
      (firstFocusableElement ?? drawer).focus();
    }, 0);

    return () => {
      previouslyFocusedElementRef.current?.focus();
    };
  }, [open]);

  useEffect(() => {
    if (!open) {
      return;
    }

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === "Escape") {
        event.preventDefault();
        onClose();
        return;
      }

      if (event.key !== "Tab") {
        return;
      }

      const drawer = drawerRef.current;
      if (!drawer) {
        return;
      }

      const focusableElements = getFocusableElements(drawer);
      if (focusableElements.length === 0) {
        event.preventDefault();
        drawer.focus();
        return;
      }

      const firstElement = focusableElements[0];
      const lastElement = focusableElements[focusableElements.length - 1];

      if (event.shiftKey && document.activeElement === firstElement) {
        event.preventDefault();
        lastElement.focus();
      } else if (!event.shiftKey && document.activeElement === lastElement) {
        event.preventDefault();
        firstElement.focus();
      }
    }

    document.addEventListener("keydown", handleKeyDown);
    return () => document.removeEventListener("keydown", handleKeyDown);
  }, [onClose, open]);

  if (!open) {
    return null;
  }

  return (
    <div className="fixed inset-0 z-50 flex justify-end bg-slate-950/45 backdrop-blur-sm" role="presentation">
      <aside
        ref={drawerRef}
        className={cx("h-full w-full overflow-y-auto bg-white p-6 shadow-2xl outline-none", widthClass)}
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        tabIndex={-1}
      >
        <div className="mb-5 flex items-center justify-between gap-4">
          <h2 id={titleId} className="text-2xl font-black text-slate-950">{title}</h2>
          <Button type="button" onClick={onClose}>Close</Button>
        </div>
        {children}
      </aside>
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
      {error ? <span className="mt-1 block text-sm font-semibold text-red-600" role="alert">{error}</span> : null}
    </label>
  );
}

export function TextInput(props: InputHTMLAttributes<HTMLInputElement>) {
  return <input {...props} className={cx("w-full rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm outline-none transition focus:border-slate-400 focus:ring-4 focus:ring-slate-100 disabled:bg-slate-100", props.className)} />;
}

export function TextArea(props: TextareaHTMLAttributes<HTMLTextAreaElement>) {
  return <textarea {...props} className={cx("w-full rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm outline-none transition focus:border-slate-400 focus:ring-4 focus:ring-slate-100 disabled:bg-slate-100", props.className)} />;
}

export function SelectInput(props: SelectHTMLAttributes<HTMLSelectElement>) {
  return <select {...props} className={cx("w-full rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm outline-none transition focus:border-slate-400 focus:ring-4 focus:ring-slate-100 disabled:bg-slate-100", props.className)} />;
}

interface ToggleProps {
  checked: boolean;
  disabled?: boolean;
  label?: string;
  onChange: (checked: boolean) => void;
}

export function Toggle({ checked, disabled, label = "Toggle setting", onChange }: ToggleProps) {
  return (
    <button
      type="button"
      disabled={disabled}
      onClick={() => onChange(!checked)}
      className={cx(
        "relative h-6 w-11 rounded-full transition disabled:opacity-60",
        checked ? "bg-slate-900" : "bg-slate-300"
      )}
      aria-pressed={checked}
      aria-label={label}
    >
      <span className={cx("absolute top-1 h-4 w-4 rounded-full bg-white shadow transition", checked ? "left-6" : "left-1")} />
    </button>
  );
}

interface PillProps {
  children: ReactNode;
  tone?: "green" | "red" | "blue" | "orange" | "slate";
}

export function Pill({ children, tone = "slate" }: PillProps) {
  const tones = {
    green: "bg-emerald-50 text-emerald-700 ring-emerald-200",
    red: "bg-red-50 text-red-700 ring-red-200",
    blue: "bg-blue-50 text-blue-700 ring-blue-200",
    orange: "bg-orange-50 text-orange-700 ring-orange-200",
    slate: "bg-slate-100 text-slate-700 ring-slate-200"
  };

  return <span className={cx("inline-flex rounded-full px-2.5 py-1 text-xs font-black ring-1", tones[tone])}>{children}</span>;
}

export function EmptyState({ children }: { children: ReactNode }) {
  return <div className="rounded-2xl border border-dashed border-slate-200 bg-slate-50 p-8 text-center text-sm font-semibold text-slate-500">{children}</div>;
}
