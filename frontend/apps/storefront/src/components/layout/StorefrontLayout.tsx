import { useEffect, useRef, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import type { StorefrontCategoryDto } from "@workspace-ecommerce/api-types";
import { useTranslation } from "react-i18next";
import { NavLink, Outlet, useLocation } from "react-router-dom";
import { StorefrontCartProvider } from "../../features/cart/StorefrontCartProvider";
import { useStorefrontCart } from "../../features/cart/StorefrontCartContext";
import { useCustomerAuth } from "../../features/customer-auth/useCustomerAuth";
import { storefrontApi } from "../../services/api/storefrontApi";
import { useModalDialog } from "../../hooks/useModalDialog";
import { StorefrontFooter } from "./StorefrontFooter";
import { StorefrontSearchOverlay } from "./StorefrontSearchOverlay";
import { LanguageSwitcher } from "./LanguageSwitcher";
import { CustomerOrderNotifications } from "../notifications/CustomerOrderNotifications";

const navItems: ReadonlyArray<{
  to?: string;
  labelKey: "header.products" | "header.warranty" | "header.news" | "header.showroom" | "header.about";
  menu?: "products" | "warranty";
}> = [
  { to: "/products", labelKey: "header.products", menu: "products" },
  { to: "/warranty", labelKey: "header.warranty", menu: "warranty" },
  { to: "/news", labelKey: "header.news" },
  { labelKey: "header.showroom" },
  { to: "/about-us", labelKey: "header.about" }
];

export function StorefrontLayout() {
  return <StorefrontCartProvider><StorefrontLayoutContent /></StorefrontCartProvider>;
}

function StorefrontLayoutContent() {
  const { t } = useTranslation();
  const location = useLocation();
  const { cartItemCount, openCartDrawer } = useStorefrontCart();
  const { isAuthenticated } = useCustomerAuth();
  const categoriesQuery = useQuery({ queryKey: ["storefront", "categories"], queryFn: storefrontApi.getCategories });
  const isHome = location.pathname === "/";
  const hideHeader = location.pathname === "/login";
  const headerRef = useRef<HTMLElement>(null);
  const [expandedMenu, setExpandedMenu] = useState<"products" | "warranty" | null>(null);
  const [isSearchOpen, setIsSearchOpen] = useState(false);
  const [isMobileMenuOpen, setIsMobileMenuOpen] = useState(false);
  const mobileMenu = useModalDialog(isMobileMenuOpen, () => setIsMobileMenuOpen(false));

  useEffect(() => { window.scrollTo({ top: 0, left: 0, behavior: "auto" }); }, [location.pathname, location.search]);
  useEffect(() => {
    if (!expandedMenu) return;
    function onPointerDown(event: PointerEvent) {
      if (!headerRef.current?.contains(event.target as Node)) setExpandedMenu(null);
    }
    function onKeyDown(event: KeyboardEvent) {
      if (event.key === "Escape") {
        headerRef.current?.querySelector<HTMLButtonElement>(`[aria-controls="header-${expandedMenu}"]`)?.focus();
        setExpandedMenu(null);
      }
    }
    document.addEventListener("pointerdown", onPointerDown);
    document.addEventListener("keydown", onKeyDown);
    return () => { document.removeEventListener("pointerdown", onPointerDown); document.removeEventListener("keydown", onKeyDown); };
  }, [expandedMenu]);

  return (
    <div className="storefront-shell flex min-h-screen flex-col bg-[var(--surface)] text-[var(--ink)]">
      <a href="#storefront-content" className="sr-only z-[100] rounded-full bg-white px-5 py-3 text-sm font-semibold focus:not-sr-only focus:fixed focus:left-4 focus:top-4">{t("header.skipToContent")}</a>
      {!hideHeader && <header ref={headerRef} className="sticky top-0 z-50 border-b border-[var(--border-soft)] bg-white/95 backdrop-blur-md">
        <div className="storefront-container flex min-h-18 items-center justify-between gap-2 py-3 lg:grid lg:min-h-20 lg:grid-cols-[minmax(0,1fr)_auto_minmax(0,1fr)]">
          <NavLink to="/" aria-label={t("header.home")} className="flex shrink-0 items-center gap-2 lg:col-start-1">
            <span className="block h-10 w-11 overflow-hidden"><img src="/demo/logo.svg" alt="" width={236} height={40} className="h-10 w-[236px] max-w-none" /></span>
            <span className="hidden text-lg font-semibold tracking-tight sm:block">WorkspaceEcom</span>
          </NavLink>
          <nav aria-label={t("header.navigation")} className="hidden items-center gap-5 text-sm font-medium lg:col-start-2 lg:flex xl:gap-8"
            onClickCapture={(event) => { if (event.target instanceof Element && event.target.closest("a")) setExpandedMenu(null); }}>
            {navItems.map((item) => item.to ? (
              <div key={item.labelKey} className="relative inline-flex items-center">
                <NavLink to={item.to} className={({ isActive }) => `inline-flex min-h-11 items-center whitespace-nowrap ${isActive ? "underline decoration-1 underline-offset-8" : "hover:text-slate-600"}`}>{t(item.labelKey)}</NavLink>
                {item.menu && <button type="button" aria-label={t(item.menu === "products" ? "header.browseCategories" : "header.warrantyLinks")}
                  aria-expanded={expandedMenu === item.menu} aria-controls={`header-${item.menu}`}
                  onClick={() => setExpandedMenu((open) => open === item.menu ? null : item.menu!)}
                  className="ml-1 grid h-11 w-6 place-items-center rounded-full text-slate-600">
                  <span aria-hidden="true" className={`transition-transform ${expandedMenu === item.menu ? "rotate-180" : ""}`}>⌄</span>
                </button>}
                {expandedMenu === item.menu && item.menu === "products" && <ProductMegaMenu categories={categoriesQuery.data ?? []} isLoading={categoriesQuery.isLoading} />}
                {expandedMenu === item.menu && item.menu === "warranty" && <WarrantyMenu />}
              </div>
            ) : <span key={item.labelKey} className="cursor-default text-slate-500">{t(item.labelKey)}</span>)}
          </nav>
          <div className="flex shrink-0 items-center sm:gap-1 lg:col-start-3 lg:justify-self-end">
            <button type="button" aria-label={t("header.openNavigation")} aria-haspopup="dialog" onClick={() => setIsMobileMenuOpen(true)} className="grid h-11 w-11 place-items-center rounded-full hover:bg-slate-100 lg:hidden">
              <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.7" aria-hidden="true"><path d="M4 6h16M4 12h16M4 18h16" /></svg>
            </button>
            <div className="hidden sm:block"><LanguageSwitcher isHeaderSolid /></div>
            <button type="button" aria-label={t("header.search")} aria-haspopup="dialog" onClick={() => { setExpandedMenu(null); setIsSearchOpen(true); }} className="grid h-11 w-11 place-items-center rounded-full hover:bg-slate-100">
              <svg width="20" height="20" viewBox="0 0 24 24" fill="none" aria-hidden="true"><circle cx="11" cy="11" r="6.5" stroke="currentColor" strokeWidth="1.7" /><path d="m16 16 4 4" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" /></svg>
            </button>
            <NavLink to={isAuthenticated ? "/account" : "/login"} aria-label={isAuthenticated ? t("header.account") : t("header.login")} className="grid h-11 w-11 place-items-center rounded-full hover:bg-slate-100">
              <svg width="20" height="20" viewBox="0 0 24 24" fill="none" aria-hidden="true"><circle cx="12" cy="8" r="3.5" stroke="currentColor" strokeWidth="1.7" /><path d="M4.5 20c1.4-4 4-6 7.5-6s6.1 2 7.5 6" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" /></svg>
            </NavLink>
            <button type="button" aria-label={t("header.cart")} aria-haspopup="dialog" onClick={() => { setExpandedMenu(null); openCartDrawer(); }} className="relative grid h-11 w-11 place-items-center rounded-full hover:bg-slate-100">
              <svg width="20" height="20" viewBox="0 0 24 24" fill="none" aria-hidden="true"><path d="M5 6h15l-1.7 8.5a2 2 0 0 1-2 1.5H9a2 2 0 0 1-2-1.6L5 3H2" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round" /><circle cx="9" cy="20" r="1.5" fill="currentColor" /><circle cx="17" cy="20" r="1.5" fill="currentColor" /></svg>
              {cartItemCount > 0 && <span className="absolute right-0 top-0 grid h-5 min-w-5 place-items-center rounded-full bg-[var(--ink)] px-1 text-[11px] font-medium text-white ring-2 ring-white">{cartItemCount > 99 ? "99+" : cartItemCount}</span>}
            </button>
          </div>
        </div>
      </header>}
      <main id="storefront-content" tabIndex={-1} className={hideHeader ? "w-full flex-1" : isHome ? "w-full flex-1 pb-16" : "storefront-container flex-1 py-8 sm:py-12"}>
        <Outlet />
      </main>
      {!hideHeader && <StorefrontFooter />}
      <StorefrontSearchOverlay isOpen={isSearchOpen} onClose={() => setIsSearchOpen(false)} />
      <CustomerOrderNotifications />
      {isMobileMenuOpen && <dialog {...mobileMenu} className="storefront-modal storefront-drawer flex flex-col" aria-label={t("header.navigation")}>
        <div className="flex min-h-20 items-center justify-between border-b border-slate-100 px-6">
          <h2 className="text-xl font-semibold">{t("header.navigation")}</h2>
          <button type="button" onClick={() => setIsMobileMenuOpen(false)} aria-label={t("header.closeNavigation")} className="grid h-11 w-11 place-items-center rounded-full hover:bg-slate-100">✕</button>
        </div>
        <nav className="min-h-0 flex-1 overflow-y-auto px-6 py-6" aria-label={t("header.navigation")}
          onClickCapture={(event) => { if (event.target instanceof Element && event.target.closest("a")) setIsMobileMenuOpen(false); }}>
          <div className="grid gap-1 border-b border-slate-100 pb-6">
            {navItems.map((item) => item.to ? <NavLink key={item.labelKey} to={item.to} className="flex min-h-12 items-center justify-between text-lg font-medium">{t(item.labelKey)}<span aria-hidden="true">↗</span></NavLink> : <span key={item.labelKey} className="py-3 text-slate-500">{t(item.labelKey)}</span>)}
            <NavLink to="/warranty-policy" className="flex min-h-12 items-center text-sm text-slate-600">{t("header.warrantyPolicy")}</NavLink>
          </div>
          <div className="grid gap-8 pt-6">
            {categoriesQuery.isLoading ? <p className="text-sm text-slate-600">{t("header.loadingCategories")}</p> : categoriesQuery.data?.map((category) => <CategoryColumn key={category.id} category={category} />)}
          </div>
        </nav>
        <div className="flex items-center justify-between border-t border-slate-100 px-6 py-4"><span className="text-sm font-medium">{t("language.select")}</span><LanguageSwitcher isHeaderSolid /></div>
      </dialog>}
    </div>
  );
}
function WarrantyMenu() {
  const { t } = useTranslation();
  return (
    <div className="absolute left-1/2 top-full z-50 w-80 -translate-x-1/2 pt-3">
      <div className="overflow-hidden rounded-2xl border border-slate-100 bg-white p-2 text-slate-950 shadow-[0_18px_45px_rgba(15,23,42,0.16)]" id="header-warranty">
        <NavLink to="/warranty" className="block rounded-xl px-4 py-3 text-base font-black transition hover:bg-slate-50 hover:text-[var(--brand)]">
          {t("header.activateWarranty")}
        </NavLink>
        <NavLink to="/warranty-policy" className="block rounded-xl px-4 py-3 text-base font-black transition hover:bg-slate-50 hover:text-[var(--brand)]">
          {t("header.warrantyPolicy")}
        </NavLink>
      </div>
    </div>
  );
}

function ProductMegaMenu({
  categories,
  isLoading
}: {
  categories: StorefrontCategoryDto[];
  isLoading: boolean;
}) {
  const { t } = useTranslation();
  return (
    <div className="absolute left-1/2 top-full z-50 w-[min(760px,calc(100vw-2rem))] -translate-x-1/2 pt-3">
      <div id="header-products" className="max-h-[min(560px,calc(100vh-7rem))] overflow-y-auto rounded-2xl border border-slate-100 bg-white p-5 text-slate-950 shadow-[0_18px_45px_rgba(15,23,42,0.16)]">
        <div className="grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
          {isLoading ? (
            <div className="text-sm font-semibold text-slate-500">{t("header.loadingCategories")}</div>
          ) : categories.length === 0 ? (
            <div className="text-sm font-semibold text-slate-500">{t("header.noCategories")}</div>
          ) : (
            categories.map((category) => <CategoryColumn key={category.id} category={category} />)
          )}
        </div>
      </div>
    </div>
  );
}

function CategoryColumn({ category }: { category: StorefrontCategoryDto }) {
  const { t } = useTranslation();
  return (
    <div className="min-w-0">
      <NavLink
        to={`/products?categorySlug=${encodeURIComponent(category.slug)}`}
        className="block text-[22px] font-black leading-tight text-slate-900 transition hover:text-[var(--brand)]"
      >
        {category.name}
      </NavLink>
      <div className="mt-7 grid gap-4">
        {category.children.length > 0 ? (
          category.children.map((child) => <CategoryMenuItem key={child.id} category={child} level={0} />)
        ) : (
          <NavLink
            to={`/products?categorySlug=${encodeURIComponent(category.slug)}`}
            className="block text-base font-bold text-slate-500 transition hover:text-slate-950"
          >
            {t("header.viewProducts")}
          </NavLink>
        )}
      </div>
    </div>
  );
}

function CategoryMenuItem({
  category,
  level
}: {
  category: StorefrontCategoryDto;
  level: number;
}) {
  return (
    <div className={level > 0 ? "pl-4" : ""}>
      <NavLink
        to={`/products?categorySlug=${encodeURIComponent(category.slug)}`}
        className="block text-base font-bold text-slate-500 transition hover:text-slate-950"
      >
        {category.name}
      </NavLink>
      {category.children.length > 0 ? (
        <div className="mt-3 grid gap-3">
          {category.children.map((child) => (
            <CategoryMenuItem key={child.id} category={child} level={level + 1} />
          ))}
        </div>
      ) : null}
    </div>
  );
}
