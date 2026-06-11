import { NavLink, Outlet, useLocation } from "react-router-dom";
import { StorefrontCartProvider } from "../../features/cart/StorefrontCartProvider";
import { useStorefrontCart } from "../../features/cart/StorefrontCartContext";
import { StorefrontFooter } from "./StorefrontFooter";

const navItems = [
  { to: "/products", label: "Products", hasDropdown: true },
  { label: "Warranty", hasDropdown: true },
  { label: "News" },
  { label: "Showroom" }
];

export function StorefrontLayout() {
  return (
    <StorefrontCartProvider>
      <StorefrontLayoutContent />
    </StorefrontCartProvider>
  );
}

function StorefrontLayoutContent() {
  const location = useLocation();
  const { cartItemCount, openCartDrawer } = useStorefrontCart();
  const isHome = location.pathname === "/";
  const hideHeader = location.pathname === "/login";

  const headerClass = isHome
    ? "absolute top-0 left-0 right-0 z-50 bg-transparent text-white"
    : "sticky top-0 z-20 border-b border-slate-100 bg-white/95 shadow-[0_8px_30px_rgba(15,23,42,0.04)] backdrop-blur-xl text-slate-950";

  const textColorClass = isHome ? "text-white" : "text-slate-950";
  const iconColorClass = isHome ? "text-white" : "text-slate-900";

  return (
    <div className="flex min-h-screen flex-col bg-[var(--surface-soft)] text-[var(--ink)]">
      {hideHeader ? null : (
        <header className={headerClass}>
          <div className="mx-auto flex min-h-20 max-w-[1600px] items-center justify-between gap-6 px-5 py-4 sm:px-8 lg:px-10">
            <div className="flex min-w-0 flex-1 items-center gap-5 lg:gap-12">
              <NavLink to="/" className="flex shrink-0 items-center" aria-label="WorkspaceEcom home">
                <img
                  src="/demo/logo.svg"
                  alt="WorkspaceEcom"
                  className={`h-auto w-[150px] sm:w-[180px] ${isHome ? "brightness-0 invert" : ""}`}
                />
              </NavLink>

              <nav className={`ui-control flex min-w-0 items-center gap-3 overflow-x-auto whitespace-nowrap scrollbar-hidden sm:gap-5 lg:gap-10 ${textColorClass}`}>
                {navItems.map((item) =>
                  item.to ? (
                    <NavLink
                      key={`${item.label}-${item.to}`}
                      to={item.to}
                      className={({ isActive }) =>
                        `inline-flex items-center gap-2 rounded-full px-1 py-2 transition ${isHome ? "hover:text-white/70" : "hover:text-[var(--brand)]"} ${
                          isActive && !isHome ? "text-[var(--brand)]" : ""
                        }`
                      }
                    >
                      {item.label}
                      {item.hasDropdown ? <span className={`mt-0.5 text-[10px] leading-none ${isHome ? "text-white" : "text-slate-900"}`} aria-hidden="true">v</span> : null}
                    </NavLink>
                  ) : (
                    <button
                      key={item.label}
                      type="button"
                      className="inline-flex cursor-default items-center gap-2 rounded-full px-1 py-2"
                      aria-disabled="true"
                    >
                      {item.label}
                      {item.hasDropdown ? <span className={`mt-0.5 text-[10px] leading-none ${isHome ? "text-white" : "text-slate-900"}`} aria-hidden="true">v</span> : null}
                    </button>
                  )
                )}
              </nav>
            </div>

            <div className={`flex shrink-0 items-center gap-3 sm:gap-5 lg:gap-8 ${iconColorClass}`}>
              <NavLink
                to="/products"
                className={`grid h-10 w-10 place-items-center rounded-full transition ${isHome ? "hover:bg-white/20" : "hover:bg-slate-100"}`}
                aria-label="Search"
              >
                <svg className="h-5 w-5" viewBox="0 0 24 24" fill="none" aria-hidden="true">
                  <circle cx="11" cy="11" r="6.5" stroke="currentColor" strokeWidth="2" />
                  <path d="m16 16 4 4" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
                </svg>
              </NavLink>

              <NavLink
                to="/login"
                className={`grid h-10 w-10 place-items-center rounded-full transition ${isHome ? "hover:bg-white/20" : "hover:bg-slate-100"}`}
                aria-label="Login"
              >
                <svg className="h-5 w-5" viewBox="0 0 24 24" fill="none" aria-hidden="true">
                  <circle cx="12" cy="8" r="4" stroke="currentColor" strokeWidth="2" />
                  <path d="M4.5 20c1.4-4 4-6 7.5-6s6.1 2 7.5 6" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
                </svg>
              </NavLink>

              <button
                type="button"
                onClick={() => openCartDrawer()}
                className={`relative grid h-10 w-10 place-items-center rounded-full transition ${isHome ? "hover:bg-white/20" : "hover:bg-slate-100"}`}
                aria-label="Cart"
              >
                <svg className="h-5 w-5" viewBox="0 0 24 24" fill="none" aria-hidden="true">
                  <path d="M5 6h15l-1.7 8.5a2 2 0 0 1-2 1.5H9a2 2 0 0 1-2-1.6L5 3H2" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
                  <circle cx="9" cy="20" r="1.5" fill="currentColor" />
                  <circle cx="17" cy="20" r="1.5" fill="currentColor" />
                </svg>
                {cartItemCount > 0 ? (
                  <span className="ui-caption absolute -right-1 -top-1 grid h-5 min-w-5 place-items-center rounded-full bg-[#171717] px-1.5 leading-none text-white ring-2 ring-white">
                    {cartItemCount > 99 ? "99+" : cartItemCount}
                  </span>
                ) : null}
              </button>
            </div>
          </div>
        </header>
      )}
      <main className={hideHeader ? "w-full flex-1" : isHome ? "w-full flex-1 pb-8" : "mx-auto w-full max-w-[1440px] flex-1 px-5 py-8 sm:px-8"}>
        <Outlet />
      </main>
      {hideHeader ? null : <StorefrontFooter />}
    </div>
  );
}
