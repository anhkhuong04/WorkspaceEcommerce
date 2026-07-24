import { useQueryClient } from "@tanstack/react-query";
import type { ReactNode } from "react";
import { NavLink, Outlet, useNavigate } from "react-router-dom";
import { useAdminAuth } from "../../features/auth/useAdminAuth";
import { Button } from "../ui/AdminUi";
import { cx } from "../ui/cx";

type AdminNavIconName = "dashboard" | "categories" | "products" | "coupons" | "orders" | "banners" | "blogs" | "reviews";

const menuItems: { to: string; label: string; icon: AdminNavIconName }[] = [
  { to: "/", label: "Dashboard", icon: "dashboard" },
  { to: "/categories", label: "Categories", icon: "categories" },
  { to: "/products", label: "Products", icon: "products" },
  { to: "/coupons", label: "Coupons", icon: "coupons" },
  { to: "/orders", label: "Orders", icon: "orders" },
  { to: "/banners", label: "Banners", icon: "banners" },
  { to: "/blogs", label: "News/Blogs", icon: "blogs" },
  { to: "/reviews", label: "Reviews", icon: "reviews" }
];

export function AdminLayout() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { session, signOut } = useAdminAuth();

  function handleLogout() {
    signOut();
    queryClient.clear();
    navigate("/login", { replace: true });
  }

  return (
    <div className="min-h-screen bg-[var(--surface-soft)] text-[var(--ink)] lg:grid lg:grid-cols-[264px_1fr]">
      <aside className="border-b border-[var(--border)] bg-[var(--surface)] px-4 py-4 lg:min-h-screen lg:border-b-0 lg:border-r">
        <div className="flex h-12 items-center gap-3 px-2">
          <img src="/demo/logo.svg" alt="WorkspaceEcom" className="h-auto w-[164px]" />
          <span className="rounded-[var(--radius-control)] bg-[var(--brand-soft)] px-2 py-1 text-xs font-bold uppercase tracking-wide text-[var(--muted-strong)]">
            Admin
          </span>
        </div>
        <nav className="mt-5 flex gap-2 overflow-x-auto lg:flex-col lg:overflow-visible" aria-label="Admin navigation">
          {menuItems.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              end={item.to === "/"}
              className={({ isActive }) => cx(
                "flex min-w-fit items-center gap-3 rounded-[var(--radius-panel)] px-3 py-2.5 text-sm font-bold transition focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--brand)] focus-visible:ring-offset-2",
                isActive
                  ? "bg-[var(--brand-soft)] text-[var(--brand)] shadow-sm ring-1 ring-[var(--border)]"
                  : "text-[var(--muted-strong)] hover:bg-[var(--brand-soft)] hover:text-[var(--brand)]"
              )}
            >
              {({ isActive }) => (
                <>
                  <span className={cx(
                    "grid h-8 w-8 place-items-center rounded-[var(--radius-card)] ring-1 transition",
                    isActive ? "bg-[var(--brand)] text-[var(--brand-contrast)] ring-[var(--brand)]" : "bg-[var(--surface)] text-[var(--muted)] ring-[var(--border)]"
                  )}>
                    <AdminNavIcon name={item.icon} />
                  </span>
                  {item.label}
                </>
              )}
            </NavLink>
          ))}
        </nav>
      </aside>

      <div className="min-w-0">
        <header className="flex flex-col gap-3 border-b border-[var(--border)] bg-[var(--surface)]/90 px-6 py-4 backdrop-blur lg:h-16 lg:flex-row lg:items-center lg:justify-between lg:py-0">
          <div>
            <p className="font-black text-[var(--ink)]">Operations console</p>
            <p className="text-xs font-semibold text-[var(--muted)]">WorkspaceEcom admin portal for MVP operations</p>
          </div>
          <div className="flex items-center gap-3">
            <span className="text-sm font-semibold text-[var(--muted)]">{session?.email}</span>
            <Button onClick={handleLogout}>Logout</Button>
          </div>
        </header>
        <main className="p-4 lg:p-6">
          <Outlet />
        </main>
      </div>
    </div>
  );
}

function AdminNavIcon({ name }: { name: AdminNavIconName }) {
  const paths: Record<AdminNavIconName, ReactNode> = {
    dashboard: <><rect x="3" y="3" width="7" height="7" rx="1" /><rect x="14" y="3" width="7" height="7" rx="1" /><rect x="3" y="14" width="7" height="7" rx="1" /><rect x="14" y="14" width="7" height="7" rx="1" /></>,
    categories: <><path d="M4 7h6l2 2h8v10H4z" /><path d="M4 7V5h6l2 2" /></>,
    products: <><path d="m12 3 8 4.5v9L12 21l-8-4.5v-9z" /><path d="m4 7.5 8 4.5 8-4.5M12 12v9" /></>,
    coupons: <><path d="M4 7a3 3 0 0 1 3-3h3l10 10-6 6L4 10z" /><path d="M8 8h.01" /><path d="M13 9l2 2M9 13l2 2" /></>,
    orders: <><path d="M6 3h12v18H6z" /><path d="M9 8h6M9 12h6M9 16h4" /></>,
    banners: <><path d="M4 5h16v14H4z" /><path d="m4 15 4-4 3 3 3-3 6 6" /><circle cx="15.5" cy="8.5" r="1.5" /></>,
    blogs: <><path d="M19 20H5a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2h10l5 5v11a2 2 0 0 1-2 2z" /><path d="M14 3v6h6M16 13H8M16 17H8M10 9H8" /></>,
    reviews: <><path d="M11.48 3.499a.562.562 0 0 1 1.04 0l2.125 5.111a.563.563 0 0 0 .475.345l5.518.442c.499.04.701.663.321.988l-4.204 3.602a.563.563 0 0 0-.182.557l1.285 5.385a.562.562 0 0 1-.84.61l-4.725-2.885a.562.562 0 0 0-.586 0L6.982 20.54a.562.562 0 0 1-.84-.61l1.285-5.386a.562.562 0 0 0-.182-.557l-4.204-3.602a.562.562 0 0 1 .321-.988l5.518-.442a.563.563 0 0 0 .475-.345L11.48 3.5z" /></>
  };

  return (
    <svg viewBox="0 0 24 24" className="h-4 w-4" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      {paths[name]}
    </svg>
  );
}
