import { NavLink, Outlet } from "react-router-dom";

const navItems = [
  { to: "/products", label: "Products", hasDropdown: true },
  { label: "Warranty", hasDropdown: true },
  { label: "News" },
  { label: "Showroom" }
];

export function StorefrontLayout() {
  return (
    <div className="min-h-screen bg-[var(--surface-soft)] text-[var(--ink)]">
      <header className="sticky top-0 z-20 border-b border-slate-100 bg-white/95 shadow-[0_8px_30px_rgba(15,23,42,0.04)] backdrop-blur-xl">
        <div className="mx-auto flex min-h-20 max-w-[1600px] items-center justify-between gap-6 px-5 py-4 sm:px-8 lg:px-10">
          <div className="flex min-w-0 flex-1 items-center gap-5 lg:gap-12">
            <NavLink to="/" className="shrink-0 text-xl font-black tracking-tight text-slate-950 lg:text-2xl">
              Workspace<span className="text-[var(--brand)]">Ecom</span>
            </NavLink>

            <nav className="flex min-w-0 items-center gap-3 overflow-x-auto whitespace-nowrap text-[15px] font-black text-slate-950 scrollbar-hidden sm:gap-5 lg:gap-10 lg:text-lg">
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
                    {item.hasDropdown ? <span className="mt-0.5 text-sm leading-none text-slate-900" aria-hidden="true">v</span> : null}
                  </NavLink>
                ) : (
                  <button
                    key={item.label}
                    type="button"
                    className="inline-flex cursor-default items-center gap-2 rounded-full px-1 py-2 text-slate-950"
                    aria-disabled="true"
                  >
                    {item.label}
                    {item.hasDropdown ? <span className="mt-0.5 text-sm leading-none text-slate-900" aria-hidden="true">v</span> : null}
                  </button>
                )
              )}
            </nav>
          </div>

          <div className="flex shrink-0 items-center gap-3 sm:gap-5 lg:gap-8">
            <button
              type="button"
              className="hidden cursor-default items-center gap-2 rounded-full px-1 py-2 text-[15px] font-black text-slate-950 sm:inline-flex lg:text-lg"
              aria-disabled="true"
            >
              English
              <span className="mt-0.5 text-sm leading-none" aria-hidden="true">v</span>
            </button>

            <NavLink
              to="/products"
              className="grid h-10 w-10 place-items-center rounded-full transition hover:bg-slate-100"
              aria-label="Search"
            >
              <span className="text-lg font-black text-slate-900" aria-hidden="true">S</span>
            </NavLink>

            <NavLink
              to="/login"
              className="grid h-10 w-10 place-items-center rounded-full transition hover:bg-slate-100"
              aria-label="Login"
            >
              <span className="text-lg font-black text-slate-900" aria-hidden="true">U</span>
            </NavLink>

            <NavLink
              to="/cart"
              className="grid h-10 w-10 place-items-center rounded-full transition hover:bg-slate-100"
              aria-label="Cart"
            >
              <span className="text-lg font-black text-slate-900" aria-hidden="true">C</span>
            </NavLink>
          </div>
        </div>
      </header>
      <main className="mx-auto max-w-[1440px] px-8 py-8">
        <Outlet />
      </main>
    </div>
  );
}
