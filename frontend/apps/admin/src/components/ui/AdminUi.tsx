import * as AlertDialog from "@radix-ui/react-alert-dialog";
import * as Dialog from "@radix-ui/react-dialog";
import * as DropdownMenu from "@radix-ui/react-dropdown-menu";
import * as Switch from "@radix-ui/react-switch";
import * as Tabs from "@radix-ui/react-tabs";
import {
  AlertCircle,
  AlertTriangle,
  ArrowLeftRight,
  CheckCircle2,
  Ellipsis,
  Inbox,
  Info,
  X,
  type LucideIcon,
} from "lucide-react";
import {
  cloneElement,
  isValidElement,
  useEffect,
  useId,
  useRef,
  useState,
  type ButtonHTMLAttributes,
  type InputHTMLAttributes,
  type ReactNode,
  type Ref,
  type SelectHTMLAttributes,
  type TextareaHTMLAttributes,
} from "react";
import { cx } from "./cx";

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: "primary" | "secondary" | "ghost" | "danger";
  fullWidth?: boolean;
  ref?: Ref<HTMLButtonElement>;
}
export function Button({
  className,
  variant = "secondary",
  fullWidth,
  type = "button",
  ...props
}: ButtonProps) {
  return (
    <button
      {...props}
      type={type}
      className={cx(
        "admin-button",
        `admin-button-${variant}`,
        fullWidth && "w-full",
        className,
      )}
    />
  );
}
export function IconButton({
  label,
  children,
  className,
  ...props
}: ButtonProps & { label: string }) {
  return (
    <Button
      {...props}
      variant={props.variant ?? "ghost"}
      aria-label={label}
      title={props.title ?? label}
      className={cx("admin-icon-button", className)}
    >
      {children}
    </Button>
  );
}
interface NoticeProps {
  type?: "error" | "warning" | "success" | "info";
  title: string;
  children?: ReactNode;
}
export function Notice({ type = "info", title, children }: NoticeProps) {
  const Icon = {
    error: AlertCircle,
    warning: AlertTriangle,
    success: CheckCircle2,
    info: Info,
  }[type];
  return (
    <div
      className={cx("admin-notice", `admin-notice-${type}`)}
      role={type === "error" ? "alert" : "status"}
    >
      <Icon size={18} className="mt-0.5 shrink-0" aria-hidden="true" />
      <div className="min-w-0">
        <p className="font-medium">{title}</p>
        {children ? <div className="mt-1 text-sm">{children}</div> : null}
      </div>
    </div>
  );
}

// These controlled shells do not own a Trigger. Remember the page's button so
// closing a dialog returns focus to the action which opened it.
function useDialogFocus() {
  const returnFocus = useRef<HTMLElement | null>(null);
  return {
    onOpenAutoFocus: () => {
      returnFocus.current =
        document.activeElement instanceof HTMLElement
          ? document.activeElement
          : null;
    },
    onCloseAutoFocus: (event: Event) => {
      if (returnFocus.current?.isConnected) {
        event.preventDefault();
        returnFocus.current.focus({ preventScroll: true });
      }
    },
  };
}
interface ModalProps {
  title: string;
  open: boolean;
  children: ReactNode;
  footer?: ReactNode;
  widthClass?: string;
  busy?: boolean;
  error?: string;
  onClose: () => void;
}
export function Modal({
  title,
  open,
  children,
  footer,
  widthClass = "max-w-xl",
  busy = false,
  error,
  onClose,
}: ModalProps) {
  const focus = useDialogFocus();
  return (
    <Dialog.Root
      open={open}
      onOpenChange={(next) => {
        if (!next && !busy) onClose();
      }}
    >
      <Dialog.Portal>
        <Dialog.Overlay className="admin-overlay" />
        <Dialog.Content
          {...focus}
          aria-describedby={undefined}
          aria-busy={busy}
          className={cx("admin-modal", widthClass)}
          onEscapeKeyDown={(e) => {
            if (busy) e.preventDefault();
          }}
          onPointerDownOutside={(e) => {
            if (busy) e.preventDefault();
          }}
        >
          <div className="admin-dialog-heading">
            <Dialog.Title className="text-lg font-semibold">
              {title}
            </Dialog.Title>
            <Dialog.Close asChild>
              <IconButton label="Close dialog" disabled={busy}>
                <X size={20} aria-hidden="true" />
              </IconButton>
            </Dialog.Close>
          </div>
          <div className="admin-dialog-body">
            {error ? (
              <div className="mb-4">
                <Notice type="error" title={error} />
              </div>
            ) : null}
            <fieldset disabled={busy} className="m-0 min-w-0 border-0 p-0">
              {children}
            </fieldset>
          </div>
          {footer ? <div className="admin-dialog-footer">{footer}</div> : null}
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog.Root>
  );
}
interface ConfirmDialogProps {
  open: boolean;
  title: string;
  message: ReactNode;
  confirmLabel?: string;
  cancelLabel?: string;
  busy?: boolean;
  error?: string;
  confirmVariant?: "primary" | "danger";
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
  error,
  confirmVariant = "danger",
  onCancel,
  onConfirm,
}: ConfirmDialogProps) {
  const focus = useDialogFocus();
  return (
    <AlertDialog.Root
      open={open}
      onOpenChange={(next) => {
        if (!next && !busy) onCancel();
      }}
    >
      <AlertDialog.Portal>
        <AlertDialog.Overlay className="admin-overlay" />
        <AlertDialog.Content
          {...focus}
          aria-busy={busy}
          onEscapeKeyDown={(e) => {
            if (busy) e.preventDefault();
          }}
          className="admin-modal max-w-md"
        >
          <div className="admin-dialog-heading">
            <AlertDialog.Title className="text-lg font-semibold">
              {title}
            </AlertDialog.Title>
          </div>
          <AlertDialog.Description asChild>
            <div className="admin-dialog-body text-sm leading-6 text-[var(--muted-strong)]">
              {message}
              {error ? (
                <div className="mt-4">
                  <Notice type="error" title={error} />
                </div>
              ) : null}
            </div>
          </AlertDialog.Description>
          <div className="admin-dialog-footer">
            <AlertDialog.Cancel asChild>
              <Button disabled={busy}>{cancelLabel}</Button>
            </AlertDialog.Cancel>
            <AlertDialog.Action asChild>
              <Button
                variant={confirmVariant}
                disabled={busy}
                onClick={(event) => {
                  event.preventDefault();
                  onConfirm();
                }}
              >
                {busy ? "Working..." : confirmLabel}
              </Button>
            </AlertDialog.Action>
          </div>
        </AlertDialog.Content>
      </AlertDialog.Portal>
    </AlertDialog.Root>
  );
}
interface DrawerProps {
  title: string;
  open: boolean;
  children: ReactNode;
  widthClass?: string;
  onClose: () => void;
}
export function Drawer({
  title,
  open,
  children,
  widthClass = "max-w-3xl",
  onClose,
}: DrawerProps) {
  const focus = useDialogFocus();
  return (
    <Dialog.Root
      open={open}
      onOpenChange={(next) => {
        if (!next) onClose();
      }}
    >
      <Dialog.Portal>
        <Dialog.Overlay className="admin-overlay" />
        <Dialog.Content
          {...focus}
          aria-describedby={undefined}
          className={cx("admin-drawer", widthClass)}
        >
          <div className="admin-dialog-heading">
            <Dialog.Title className="text-lg font-semibold">
              {title}
            </Dialog.Title>
            <Dialog.Close asChild>
              <IconButton label="Close drawer">
                <X size={20} aria-hidden="true" />
              </IconButton>
            </Dialog.Close>
          </div>
          <div className="admin-dialog-body">{children}</div>
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog.Root>
  );
}
interface FieldProps {
  label: string;
  error?: string;
  children: ReactNode;
}
export function Field({ label, error, children }: FieldProps) {
  const generatedId = useId();
  const child = isValidElement<Record<string, unknown>>(children)
    ? children
    : null;
  const id = (child?.props.id as string | undefined) ?? generatedId;
  const isGroup = child?.type === "div";
  return (
    <div className="min-w-0">
      {isGroup ? (
        <span
          id={`${id}-label`}
          className="mb-1.5 block text-sm font-medium text-[var(--muted-strong)]"
        >
          {label}
        </span>
      ) : (
        <label
          htmlFor={id}
          className="mb-1.5 block text-sm font-medium text-[var(--muted-strong)]"
        >
          {label}
        </label>
      )}
      {child
        ? cloneElement(child, {
            id,
            ...(isGroup
              ? { role: "group", "aria-labelledby": `${id}-label` }
              : {}),
            "aria-invalid": error ? true : child.props["aria-invalid"],
            "aria-describedby":
              [child.props["aria-describedby"], error ? `${id}-error` : null]
                .filter(Boolean)
                .join(" ") || undefined,
          })
        : children}
      {error ? (
        <p
          id={`${id}-error`}
          className="mt-1.5 text-sm text-red-700"
          role="alert"
        >
          {error}
        </p>
      ) : null}
    </div>
  );
}
export function TextInput(props: InputHTMLAttributes<HTMLInputElement>) {
  return <input {...props} className={cx("admin-input", props.className)} />;
}
export function TextArea(props: TextareaHTMLAttributes<HTMLTextAreaElement>) {
  return (
    <textarea
      {...props}
      className={cx("admin-input min-h-24", props.className)}
    />
  );
}
export function SelectInput(props: SelectHTMLAttributes<HTMLSelectElement>) {
  return <select {...props} className={cx("admin-input", props.className)} />;
}
interface ToggleProps {
  checked: boolean;
  disabled?: boolean;
  label?: string;
  id?: string;
  "aria-invalid"?: boolean;
  "aria-describedby"?: string;
  onChange: (checked: boolean) => void;
}
export function Toggle({ checked, onChange, label, ...props }: ToggleProps) {
  return (
    <Switch.Root
      {...props}
      checked={checked}
      onCheckedChange={onChange}
      aria-label={label ?? (props.id ? undefined : "Active")}
      className="admin-switch"
    >
      <Switch.Thumb className="admin-switch-thumb" />
    </Switch.Root>
  );
}
export function Pill({
  children,
  tone = "slate",
}: {
  children: ReactNode;
  tone?: "green" | "red" | "blue" | "orange" | "slate";
}) {
  return (
    <span className={cx("admin-pill", `admin-pill-${tone}`)}>
      <span
        className="h-1.5 w-1.5 shrink-0 rounded-full bg-current"
        aria-hidden="true"
      />
      {children}
    </span>
  );
}
export function EmptyState({ children }: { children: ReactNode }) {
  return (
    <div className="admin-empty">
      <Inbox size={28} className="mb-3 text-slate-500" aria-hidden="true" />
      <div className="max-w-md text-sm leading-6 text-[var(--muted-strong)]">
        {children}
      </div>
    </div>
  );
}
export function TableViewport({
  label,
  children,
  className,
}: {
  label: string;
  children: ReactNode;
  className?: string;
}) {
  const ref = useRef<HTMLDivElement>(null);
  const [overflow, setOverflow] = useState(false);
  useEffect(() => {
    const el = ref.current;
    if (!el) return;
    const observer = new ResizeObserver(() =>
      setOverflow(el.scrollWidth > el.clientWidth + 1),
    );
    observer.observe(el);
    if (el.firstElementChild) observer.observe(el.firstElementChild);
    return () => observer.disconnect();
  }, [children]);
  return (
    <div className="min-w-0">
      <div
        ref={ref}
        role="region"
        aria-label={label}
        tabIndex={0}
        className={cx("admin-table-scroll overflow-auto", className)}
      >
        {children}
      </div>
      {overflow ? (
        <p className="mt-2 flex items-center gap-1.5 text-xs text-[var(--muted)]">
          <ArrowLeftRight size={14} aria-hidden="true" />
          Scroll to view more columns
        </p>
      ) : null}
    </div>
  );
}
export function AdminTabs({
  tabs,
  label,
  defaultValue,
  value,
  onValueChange,
}: {
  tabs: { value: string; label: string; content: ReactNode }[];
  label: string;
  defaultValue?: string;
  value?: string;
  onValueChange?: (value: string) => void;
}) {
  return (
    <Tabs.Root
      defaultValue={defaultValue ?? tabs[0]?.value}
      value={value}
      onValueChange={onValueChange}
      className="min-w-0"
    >
      <Tabs.List aria-label={label} className="admin-tabs">
        {tabs.map((tab) => (
          <Tabs.Trigger key={tab.value} value={tab.value} className="admin-tab">
            {tab.label}
          </Tabs.Trigger>
        ))}
      </Tabs.List>
      {tabs.map((tab) => (
        <Tabs.Content
          key={tab.value}
          value={tab.value}
          className="min-w-0 pt-4 outline-none"
        >
          {tab.content}
        </Tabs.Content>
      ))}
    </Tabs.Root>
  );
}
export interface RowAction {
  label: string;
  icon?: LucideIcon;
  onSelect: () => void;
  disabled?: boolean;
  danger?: boolean;
}
export function RowActions({
  label,
  actions,
}: {
  label: string;
  actions: RowAction[];
}) {
  const triggerRef = useRef<HTMLButtonElement>(null);
  const pendingAction = useRef<(() => void) | null>(null);
  return (
    <DropdownMenu.Root>
      <DropdownMenu.Trigger asChild>
        <IconButton label={label} ref={triggerRef}>
          <Ellipsis size={18} aria-hidden="true" />
        </IconButton>
      </DropdownMenu.Trigger>
      <DropdownMenu.Portal>
        <DropdownMenu.Content
          align="end"
          sideOffset={6}
          collisionPadding={12}
          className="admin-menu"
          onCloseAutoFocus={(event) => {
            if (!pendingAction.current) return;
            event.preventDefault();
            triggerRef.current?.focus();
            const action = pendingAction.current;
            pendingAction.current = null;
            requestAnimationFrame(action);
          }}
        >
          {actions.map(({ icon: Icon, ...action }) => (
            <DropdownMenu.Item
              key={action.label}
              onSelect={() => {
                pendingAction.current = action.onSelect;
              }}
              disabled={action.disabled}
              className={cx("admin-menu-item", action.danger && "text-red-700")}
            >
              {Icon ? <Icon size={16} aria-hidden="true" /> : null}
              {action.label}
            </DropdownMenu.Item>
          ))}
        </DropdownMenu.Content>
      </DropdownMenu.Portal>
    </DropdownMenu.Root>
  );
}
