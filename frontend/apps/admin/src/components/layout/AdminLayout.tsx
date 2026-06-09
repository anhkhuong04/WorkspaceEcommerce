import { useQueryClient } from "@tanstack/react-query";
import { NavLink, Outlet, useNavigate } from "react-router-dom";
import { useAdminAuth } from "../../features/auth/useAdminAuth";
import { Button } from "../ui/AdminUi";
import { cx } from "../ui/cx";

const menuItems = [
  { to: "/", label: "Dashboard", icon: "▦" },
  { to: "/categories", label: "Categories", icon: "#" },
  { to: "/products", label: "Products", icon: "□" },
  { to: "/orders", label: "Orders", icon: "≡" },
  { to: "/banners", label: "Banners", icon: "◇" }
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
        <nav className="mt-5 flex gap-2 overflow-x-auto lg:flex-col lg:overflow-visible">
          {menuItems.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              end={item.to === "/"}
              className={({ isActive }) => cx(
                "flex min-w-fit items-center gap-3 rounded-2xl px-3 py-2.5 text-sm font-bold transition",
                isActive ? "bg-teal-50 text-teal-800 ring-1 ring-teal-100" : "text-slate-600 hover:bg-slate-100 hover:text-slate-950"
              )}
            >
              <span className="grid h-7 w-7 place-items-center rounded-xl bg-white text-base shadow-sm ring-1 ring-slate-100">{item.icon}</span>
              {item.label}
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