import { useTranslation } from "react-i18next";
import { Link } from "react-router-dom";

export function StorefrontErrorPage({ notFound = false }: { notFound?: boolean }) {
  const { t } = useTranslation();
  return <section className="mx-auto max-w-2xl px-6 py-20 text-center">
    <h1 className="text-3xl font-semibold">{t(notFound ? "routeError.notFound" : "routeError.title")}</h1>
    <p className="mt-4 text-slate-600">{t("routeError.description")}</p>
    <div className="mt-8 flex flex-wrap justify-center gap-4">
      <Link to="/" className="inline-flex min-h-11 items-center rounded-full bg-slate-950 px-5 text-white">{t("header.home")}</Link>
      <Link to="/products" className="inline-flex min-h-11 items-center rounded-full border border-slate-300 px-5">{t("paymentResult.shop")}</Link>
    </div>
  </section>;
}
