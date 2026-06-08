import { NavLink, Outlet } from "react-router-dom";

const navItems = [
  { to: "/", label: "Home" },
  { to: "/products", label: "Products" },
  { to: "/cart", label: "Cart" },
  { to: "/orders/lookup", label: "Order lookup" }
];

export function StorefrontLayout() {
  return (
    <div className="min-h-screen bg-[var(--surface-soft)] text-[var(--ink)]">
      <header className="sticky top-0 z-20 border-b border-slate-200/80 bg-white/90 backdrop-blur-xl">
        <div className="mx-auto flex h-18 max-w-[1440px] items-center justify-between px-8">
          <NavLink to="/" className="text-xl font-black tracking-tight text-slate-950">
            Workspace<span className="text-[var(--brand)]">Ecom</span>
          </NavLink>
          <nav className="flex items-center gap-2 rounded-full border border-slate-200 bg-slate-50 p-1">
            {navItems.map((item) => (
              <NavLink
                key={item.to}
                to={item.to}
                className={({ isActive }) =>
                  `rounded-full px-4 py-2 text-sm font-semibold transition ${
                    isActive ? "bg-white text-slate-950 shadow-sm" : "text-slate-500 hover:text-slate-950"
                  }`
                }
              >
                {item.label}
              </NavLink>
            ))}
          </nav>
        </div>
      </header>
      <main className="mx-auto max-w-[1440px] px-8 py-8">
        <Outlet />
      </main>
    </div>
  );
}
