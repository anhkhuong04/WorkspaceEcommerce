import { useEffect } from "react";
import { Link } from "react-router-dom";
import { useStorefrontCart } from "../../features/cart/StorefrontCartContext";

export function CartPage() {
  const { cartItemCount, openCartDrawer } = useStorefrontCart();

  useEffect(() => {
    openCartDrawer();
  }, [openCartDrawer]);

  return (
    <div className="grid min-h-[420px] place-items-center">
      <section className="ui-card max-w-xl border border-slate-100 p-8 text-center">
        <p className="ui-caption uppercase tracking-[0.2em] text-[var(--brand)]">Cart</p>
        <h1 className="ui-h1 mt-3 text-slate-950">Your cart opens in the drawer.</h1>
        <p className="ui-body mt-3 text-slate-500">
          We use one consistent shopping cart experience across the storefront. The cart drawer is open now and contains {cartItemCount} item{cartItemCount === 1 ? "" : "s"}.
        </p>
        <div className="mt-6 flex flex-wrap justify-center gap-3">
          <button
            type="button"
            onClick={() => openCartDrawer()}
            className="ui-control rounded-[var(--radius-control)] bg-slate-950 px-5 py-3 text-white transition hover:bg-slate-800"
          >
            Open cart
          </button>
          <Link
            to="/products"
            className="ui-control rounded-[var(--radius-control)] border border-slate-200 px-5 py-3 text-slate-700 transition hover:border-slate-300"
          >
            Continue shopping
          </Link>
        </div>
      </section>
    </div>
  );
}
