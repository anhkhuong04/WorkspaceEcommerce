import { NavLink, Outlet, useLocation } from "react-router-dom";

const navItems = [
  { to: "/products", label: "Products", hasDropdown: true },
  { label: "Warranty", hasDropdown: true },
  { label: "News" },
  { label: "Showroom" }
];

export function StorefrontLayout() {
  const location = useLocation();
  const hideHeader = location.pathname === "/login" || location.pathname === "/cart";

  return (
    <div className="min-h-screen bg-[var(--surface-soft)] text-[var(--ink)]">
      {hideHeader ? null : <header className="sticky top-0 z-20 border-b border-slate-100 bg-white/95 shadow-[0_8px_30px_rgba(15,23,42,0.04)] backdrop-blur-xl">
        <div className="mx-auto flex min-h-20 max-w-[1600px] items-center justify-between gap-6 px-5 py-4 sm:px-8 lg:px-10">
          <div className="flex min-w-0 flex-1 items-center gap-5 lg:gap-12">
            <NavLink to="/" className="flex shrink-0 items-center gap-2 text-sm font-black tracking-tight text-slate-950 lg:text-base">
              <img
                src="/demo/logo.svg"
                alt="WorkspaceEcom Logo"
                className="h-6 w-auto object-contain"
              />
              <span>
                Workspace<span className="text-[var(--brand)]">Ecom</span>
              </span>
            </NavLink>

            <nav className="flex min-w-0 items-center gap-3 overflow-x-auto whitespace-nowrap text-xs font-black text-slate-950 scrollbar-hidden sm:gap-5 lg:gap-10 lg:text-sm">
              {navItems.map((item) =>
                item.to ? (
                  <NavLink
                    key={`${item.label}-${item.to}`}
                    to={item.to}
                    className={({ isActive }) =>
                      `inline-flex items-center gap-2 rounded-full px-1 py-2 transition hover:text-[var(--brand)] ${
                        isActive ? "text-[var(--brand)]" : "text-slate-950"
                      }`
                    }
                  >
                    {item.label}
                    {item.hasDropdown ? <span className="mt-0.5 text-[10px] leading-none text-slate-900" aria-hidden="true">v</span> : null}
                  </NavLink>
                ) : (
                  <button
                    key={item.label}
                    type="button"
                    className="inline-flex cursor-default items-center gap-2 rounded-full px-1 py-2 text-slate-950"
                    aria-disabled="true"
                  >
                    {item.label}
                    {item.hasDropdown ? <span className="mt-0.5 text-[10px] leading-none text-slate-900" aria-hidden="true">v</span> : null}
                  </button>
                )
              )}
            </nav>
          </div>

          <div className="flex shrink-0 items-center gap-3 sm:gap-5 lg:gap-8">
            <NavLink
              to="/products"
              className="grid h-10 w-10 place-items-center rounded-full transition hover:bg-slate-100"
              aria-label="Search"
            >
              <svg className="h-5 w-5 text-slate-900" viewBox="0 0 24 24" fill="none" aria-hidden="true">
                <circle cx="11" cy="11" r="6.5" stroke="currentColor" strokeWidth="2" />
                <path d="m16 16 4 4" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
              </svg>
            </NavLink>

            <NavLink
              to="/login"
              className="grid h-10 w-10 place-items-center rounded-full transition hover:bg-slate-100"
              aria-label="Login"
            >
              <svg className="h-5 w-5 text-slate-900" viewBox="0 0 24 24" fill="none" aria-hidden="true">
                <circle cx="12" cy="8" r="4" stroke="currentColor" strokeWidth="2" />
                <path d="M4.5 20c1.4-4 4-6 7.5-6s6.1 2 7.5 6" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
              </svg>
            </NavLink>

            <NavLink
              to="/cart"
              className="grid h-10 w-10 place-items-center rounded-full transition hover:bg-slate-100"
              aria-label="Cart"
            >
              <svg className="h-5 w-5 text-slate-900" viewBox="0 0 24 24" fill="none" aria-hidden="true">
                <path d="M5 6h15l-1.7 8.5a2 2 0 0 1-2 1.5H9a2 2 0 0 1-2-1.6L5 3H2" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
                <circle cx="9" cy="20" r="1.5" fill="currentColor" />
                <circle cx="17" cy="20" r="1.5" fill="currentColor" />
              </svg>
            </NavLink>
          </div>
        </div>
      </header>}
      <main className="mx-auto max-w-[1440px] px-8 py-8">
        <Outlet />
      </main>
    </div>
  );
}
