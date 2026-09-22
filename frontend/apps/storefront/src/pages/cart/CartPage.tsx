import { useEffect } from "react";
import { Link } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { useStorefrontCart } from "../../features/cart/StorefrontCartContext";

export function CartPage() {
  const { t } = useTranslation();
  const { cartItemCount, openCartDrawer } = useStorefrontCart();

  useEffect(() => {
    openCartDrawer();
  }, [openCartDrawer]);

  return (
    <div className="grid min-h-[420px] place-items-center">
      <section className="ui-card max-w-xl border border-slate-100 p-8 text-center">
        <p className="ui-caption uppercase tracking-[0.2em] text-[var(--brand)]">{t("header.cart")}</p>
        <h1 className="ui-h1 mt-3 text-slate-950">{t("cart.drawerTitle")}</h1>
        <p className="ui-body mt-3 text-slate-500">
          {t("cart.drawerDescription", { count: cartItemCount })}
        </p>
        <div className="mt-6 flex flex-wrap justify-center gap-3">
          <button
            type="button"
            onClick={() => openCartDrawer()}
            className="ui-control rounded-[var(--radius-control)] bg-slate-950 px-5 py-3 text-white transition hover:bg-slate-800"
          >
            {t("cart.open")}
          </button>
          <Link
            to="/products"
            className="ui-control rounded-[var(--radius-control)] border border-slate-200 px-5 py-3 text-slate-700 transition hover:border-slate-300"
          >
            {t("cart.continueShopping")}
          </Link>
        </div>
      </section>
    </div>
  );
}
