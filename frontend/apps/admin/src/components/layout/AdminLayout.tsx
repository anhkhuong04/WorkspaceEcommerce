import { useQueryClient } from "@tanstack/react-query";
import type { ReactNode } from "react";
import { NavLink, Outlet, useNavigate } from "react-router-dom";
import { useAdminAuth } from "../../features/auth/useAdminAuth";
import { Button } from "../ui/AdminUi";
import { cx } from "../ui/cx";

type AdminNavIconName = "dashboard" | "categories" | "products" | "orders" | "banners";

const menuItems: { to: string; label: string; icon: AdminNavIconName }[] = [
  { to: "/", label: "Dashboard", icon: "dashboard" },
  { to: "/categories", label: "Categories", icon: "categories" },
  { to: "/products", label: "Products", icon: "products" },
  { to: "/orders", label: "Orders", icon: "orders" },
  { to: "/banners", label: "Banners", icon: "banners" }
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
    <div className="min-h-screen bg-[#f4f7f6] text-slate-900 lg:grid lg:grid-cols-[264px_1fr]">
      <aside className="border-b border-slate-200 bg-white px-4 py-4 lg:min-h-screen lg:border-b-0 lg:border-r">
        <div className="flex h-12 items-center gap-3 px-2 text-lg font-black text-slate-950">
          <span className="grid h-9 w-9 place-items-center rounded-2xl bg-teal-700 text-white">W</span>
          Workspace Admin
        </div>
        <nav className="mt-5 flex gap-2 overflow-x-auto lg:flex-col lg:overflow-visible" aria-label="Admin navigation">
          {menuItems.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              end={item.to === "/"}
              className={({ isActive }) => cx(
                "flex min-w-fit items-center gap-3 rounded-2xl px-3 py-2.5 text-sm font-bold transition focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-teal-600 focus-visible:ring-offset-2",
                isActive
                  ? "bg-teal-50 text-teal-800 shadow-sm ring-1 ring-teal-100"
                  : "text-slate-600 hover:bg-slate-100 hover:text-slate-950"
              )}
            >
              {({ isActive }) => (
                <>
                  <span className={cx(
                    "grid h-8 w-8 place-items-center rounded-xl ring-1 transition",
                    isActive ? "bg-teal-700 text-white ring-teal-700" : "bg-white text-slate-500 ring-slate-200"
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
        <header className="flex flex-col gap-3 border-b border-slate-200 bg-white/90 px-6 py-4 backdrop-blur lg:h-16 lg:flex-row lg:items-center lg:justify-between lg:py-0">
          <div>
            <p className="font-black text-slate-900">Operations console</p>
            <p className="text-xs font-semibold text-slate-500">Tailwind admin portal for MVP operations</p>
          </div>
          <div className="flex items-center gap-3">
            <span className="text-sm font-semibold text-slate-500">{session?.email}</span>
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
    orders: <><path d="M6 3h12v18H6z" /><path d="M9 8h6M9 12h6M9 16h4" /></>,
    banners: <><path d="M4 5h16v14H4z" /><path d="m4 15 4-4 3 3 3-3 6 6" /><circle cx="15.5" cy="8.5" r="1.5" /></>
  };

  return (
    <svg viewBox="0 0 24 24" className="h-4 w-4" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      {paths[name]}
    </svg>
  );
}
