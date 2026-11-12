import { useTranslation } from "react-i18next";
import { Link, useParams } from "react-router-dom";
import { storefrontInfo } from "../../services/storefrontInfo";
import { StorefrontErrorPage } from "../StorefrontErrorPage";

const slugs = ["contact", "terms-of-service", "privacy-policy", "shipping-policy", "returns-policy", "warranty-policy", "about-us"];
const pagePath = (slug: string) => ["warranty-policy", "about-us"].includes(slug) ? `/${slug}` : `/support/${slug}`;
type PolicyContent = { title: string; body: string };

export function SupportPage({ slug: fixedSlug }: { slug?: string }) {
  const { slug: routeSlug } = useParams();
  const { t } = useTranslation();
  const slug = fixedSlug ?? routeSlug ?? "";
  if (!slugs.includes(slug)) return <StorefrontErrorPage notFound />;
  const page = t(`supportPages.${slug}`, { returnObjects: true }) as PolicyContent;
  return <div className="grid gap-8">
    <header className="rounded-3xl bg-slate-950 p-8 text-white sm:p-10"><h1 className="ui-h1">{page.title}</h1></header>
    <div className="grid gap-8 lg:grid-cols-[260px_1fr]">
      <nav aria-label={t("footer.support")} className="grid h-fit gap-1 rounded-2xl border border-slate-200 p-3">
        {slugs.map(item => <Link key={item} to={pagePath(item)} aria-current={slug === item ? "page" : undefined}
          className={`flex min-h-11 items-center rounded-xl px-4 py-3 text-sm font-semibold ${slug === item ? "bg-slate-950 text-white" : "hover:bg-slate-100"}`}>{t(`supportPages.${item}.title`)}</Link>)}
      </nav>
      <article className="grid content-start gap-8 rounded-2xl border border-slate-200 p-6 sm:p-10">
        <p className="whitespace-pre-line leading-7 text-slate-700">{page.body}</p>
        {slug === "contact" && <div className="grid gap-3">
          {storefrontInfo.supportEmail && <a className="min-h-11 break-all underline" href={`mailto:${storefrontInfo.supportEmail}`}>{storefrontInfo.supportEmail}</a>}
          {storefrontInfo.supportPhone && <a className="min-h-11 underline" href={`tel:${storefrontInfo.supportPhone}`}>{storefrontInfo.supportPhone}</a>}
          {!storefrontInfo.supportEmail && !storefrontInfo.supportPhone && <p className="text-slate-700">{t("supportPages.contactNotPublished")}</p>}
        </div>}
        <Link to="/orders/lookup" className="inline-flex min-h-11 w-fit items-center rounded-full bg-slate-950 px-5 text-white">{t("paymentResult.lookup")}</Link>
      </article>
    </div>
  </div>;
}
