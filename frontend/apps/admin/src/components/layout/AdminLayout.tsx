import { useQueryClient } from "@tanstack/react-query";
import * as Dialog from "@radix-ui/react-dialog";
import {
  Boxes,
  ChevronRight,
  FileText,
  FolderTree,
  Image,
  LayoutDashboard,
  LogOut,
  Menu,
  ShieldCheck,
  ShoppingBag,
  Star,
  TicketPercent,
  Truck,
  X,
  type LucideIcon,
} from "lucide-react";
import { useEffect, useState } from "react";
import {
  NavLink,
  Outlet,
  useLocation,
  useNavigate,
  useNavigation,
} from "react-router-dom";
import { useAdminAuth } from "../../features/auth/useAdminAuth";
import { IconButton } from "../ui/AdminUi";
import { AdminBrand } from "../ui/AdminBrand";
import { cx } from "../ui/cx";
import { AdminSearch } from "./AdminSearch";

const groups: {
  label: string;
  items: { to: string; label: string; icon: LucideIcon }[];
}[] = [
  {
    label: "Overview",
    items: [{ to: "/", label: "Dashboard", icon: LayoutDashboard }],
  },
  {
    label: "Commerce",
    items: [
      { to: "/orders", label: "Orders", icon: ShoppingBag },
      { to: "/shipping", label: "Shipping rates", icon: Truck },
      { to: "/products", label: "Products", icon: Boxes },
      { to: "/categories", label: "Categories", icon: FolderTree },
      { to: "/coupons", label: "Coupons", icon: TicketPercent },
    ],
  },
  {
    label: "Content",
    items: [
      { to: "/banners", label: "Banners", icon: Image },
      { to: "/blogs", label: "Articles", icon: FileText },
      { to: "/reviews", label: "Reviews", icon: Star },
    ],
  },
  {
    label: "Operations",
    items: [{ to: "/operations", label: "Operations", icon: ShieldCheck }, { to: "/warranties", label: "Warranties", icon: ShieldCheck }],
  },
];
export function AdminLayout() {
  const [mobileOpen, setMobileOpen] = useState(false);
  useEffect(() => {
    const desktop = window.matchMedia("(min-width: 1024px)");
    const closeOnDesktop = () => {
      if (desktop.matches) setMobileOpen(false);
    };
    desktop.addEventListener("change", closeOnDesktop);
    return () => desktop.removeEventListener("change", closeOnDesktop);
  }, []);
  const location = useLocation();
  const navigation = useNavigation();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { session, signOut } = useAdminAuth();
  const group =
    groups.find((g) => g.items.some((item) => item.to === location.pathname)) ??
    groups[0];
  const current = group.items.find((item) => item.to === location.pathname);
  function handleLogout() {
    signOut();
    queryClient.clear();
    navigate("/login", { replace: true });
  }
  const nav = (
    <nav
      className="flex-1 overflow-y-auto px-3 py-4"
      aria-label="Admin navigation"
    >
      {groups.map((g) => (
        <div key={g.label} className="mb-5 last:mb-0">
          <p className="mb-2 px-3 text-xs font-medium text-[var(--muted)]">
            {g.label}
          </p>
          <div className="grid gap-1">
            {g.items.map(({ icon: Icon, ...item }) => (
              <NavLink
                key={item.to}
                to={item.to}
                end={item.to === "/"}
                onClick={() => setMobileOpen(false)}
                className={({ isActive }) =>
                  cx(
                    "flex min-h-11 items-center gap-3 rounded-lg px-3 text-sm font-medium transition-colors",
                    isActive
                      ? "bg-[var(--brand-soft)] text-[var(--brand)]"
                      : "text-[var(--muted-strong)] hover:bg-slate-50",
                  )
                }
              >
                <Icon size={19} strokeWidth={1.8} aria-hidden="true" />
                {item.label}
              </NavLink>
            ))}
          </div>
        </div>
      ))}
    </nav>
  );
  const footer = (
    <div className="border-t border-[var(--border)] p-4">
      <div className="mb-3 flex min-w-0 items-center gap-3">
        <span
          className="grid h-9 w-9 shrink-0 place-items-center rounded-full bg-slate-100 font-medium text-slate-600"
          aria-hidden="true"
        >
          A
        </span>
        <div className="min-w-0">
          <p className="text-sm font-medium">Administrator</p>
          <p
            className="truncate text-xs text-[var(--muted)]"
            title={session?.email}
          >
            {session?.email}
          </p>
        </div>
      </div>
      <button
        type="button"
        onClick={handleLogout}
        className="flex min-h-10 w-full items-center gap-3 rounded-lg px-3 text-sm text-[var(--muted-strong)] hover:bg-slate-50"
      >
        <LogOut size={17} aria-hidden="true" />
        Sign out
      </button>
    </div>
  );
  return (
    <div className="min-h-screen text-[var(--ink)] lg:grid lg:grid-cols-[232px_minmax(0,1fr)]">
      <a
        href="#admin-main"
        className="sr-only fixed left-4 top-4 z-[100] rounded-lg bg-white p-3 text-[var(--brand)] focus:not-sr-only"
      >
        Skip to content
      </a>
      <aside className="sticky top-0 hidden h-dvh flex-col border-r border-[var(--border)] bg-white lg:flex">
        <div className="border-b border-[var(--border)] px-6 py-4">
          <AdminBrand />
        </div>
        {nav}
        {footer}
      </aside>
      <div className="min-w-0">
        <header className="sticky top-0 z-30 flex h-[69px] items-center justify-between gap-3 border-b border-[var(--border)] bg-white/95 px-4 backdrop-blur-sm lg:px-7">
          <div className="flex min-w-0 items-center gap-3">
            <Dialog.Root open={mobileOpen} onOpenChange={setMobileOpen}>
              <Dialog.Trigger asChild>
                <IconButton label="Open navigation" className="lg:hidden">
                  <Menu size={22} aria-hidden="true" />
                </IconButton>
              </Dialog.Trigger>
              <Dialog.Portal>
                <Dialog.Overlay className="admin-overlay lg:hidden" />
                <Dialog.Content
                  aria-describedby={undefined}
                  className="fixed inset-y-0 left-0 z-[61] flex w-[min(300px,calc(100vw-32px))] flex-col border-r border-[var(--border)] bg-white shadow-xl lg:hidden"
                >
                  <div className="flex items-center justify-between border-b border-[var(--border)] px-5 py-4">
                    <Dialog.Title asChild>
                      <div>
                        <AdminBrand />
                      </div>
                    </Dialog.Title>
                    <Dialog.Close asChild>
                      <IconButton label="Close navigation">
                        <X size={20} aria-hidden="true" />
                      </IconButton>
                    </Dialog.Close>
                  </div>
                  {nav}
                  {footer}
                </Dialog.Content>
              </Dialog.Portal>
            </Dialog.Root>
            <p className="flex min-w-0 items-center gap-2 text-sm">
              <span className="hidden text-[var(--muted)] sm:inline">
                {group.label}
              </span>
              <ChevronRight
                size={14}
                className="hidden text-slate-500 sm:inline"
                aria-hidden="true"
              />
              <span className="truncate font-medium">
                {current?.label ?? "Admin"}
              </span>
            </p>
          </div>
          <AdminSearch />
          {navigation.state !== "idle" ? (
            <p role="status" className="shrink-0 text-xs text-[var(--muted)]">
              Loading page...
            </p>
          ) : null}
        </header>
        <main
          id="admin-main"
          tabIndex={-1}
          className="min-w-0 p-4 outline-none sm:p-6 lg:p-7"
        >
          <Outlet />
        </main>
      </div>
    </div>
  );
}
