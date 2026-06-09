import { NavLink, Outlet } from "react-router-dom";
import searchIcon from "../../assets/search.svg";
import cartIcon from "../../assets/shoping-cart.png";
import userIcon from "../../assets/user.png";

const navItems = [
  { to: "/products", label: "Sản phẩm", hasDropdown: true },
  { to: "/orders/lookup", label: "Bảo hành", hasDropdown: true },
  { to: "/", label: "Tin tức" },
  { to: "/", label: "Trang trưng bày" }
];

export function StorefrontLayout() {
  return (
    <div className="min-h-screen bg-[var(--surface-soft)] text-[var(--ink)]">
      <header className="sticky top-0 z-20 border-b border-slate-100 bg-white/95 shadow-[0_8px_30px_rgba(15,23,42,0.04)] backdrop-blur-xl">
        <div className="mx-auto flex min-h-20 max-w-[1600px] items-center justify-between gap-6 px-5 py-4 sm:px-8 lg:px-10">
          <nav className="flex min-w-0 flex-1 items-center gap-4 overflow-x-auto whitespace-nowrap text-[15px] font-black text-slate-950 scrollbar-hidden sm:gap-8 lg:gap-16 lg:text-lg">
            {navItems.map((item) => (
              <NavLink
                key={`${item.label}-${item.to}`}
                to={item.to}
                className={({ isActive }) =>
                  `inline-flex items-center gap-2 rounded-full px-1 py-2 transition hover:text-[var(--brand)] ${
                    isActive && item.to !== "/" ? "text-[var(--brand)]" : "text-slate-950"
                  }`
                }
              >
                {item.label}
                {item.hasDropdown ? (
                  <span className="mt-0.5 text-sm leading-none text-slate-900" aria-hidden="true">
                    ˅
                  </span>
                ) : null}
              </NavLink>
            ))}
          </nav>

          <div className="flex shrink-0 items-center gap-3 sm:gap-5 lg:gap-8">
            <button
              type="button"
              className="hidden items-center gap-2 rounded-full px-1 py-2 text-[15px] font-black text-slate-950 transition hover:text-[var(--brand)] sm:inline-flex lg:text-lg"
            >
              Tiếng Việt
              <span className="mt-0.5 text-sm leading-none" aria-hidden="true">
                ˅
              </span>
            </button>

            <NavLink
              to="/products"
              className="grid h-10 w-10 place-items-center rounded-full transition hover:bg-slate-100"
              aria-label="Tìm kiếm"
            >
              <img src={searchIcon} alt="" className="h-7 w-7 object-contain" />
            </NavLink>

            <NavLink
              to="/orders/lookup"
              className="grid h-10 w-10 place-items-center rounded-full transition hover:bg-slate-100"
              aria-label="Tài khoản"
            >
              <img src={userIcon} alt="" className="h-7 w-7 object-contain" />
            </NavLink>

            <NavLink
              to="/cart"
              className="grid h-10 w-10 place-items-center rounded-full transition hover:bg-slate-100"
              aria-label="Giỏ hàng"
            >
              <img src={cartIcon} alt="" className="h-7 w-7 object-contain" />
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
