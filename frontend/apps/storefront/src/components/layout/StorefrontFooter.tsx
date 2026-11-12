import { useTranslation } from "react-i18next";
import { Link } from "react-router-dom";

function StorefrontFaq() {
  const { t } = useTranslation();
  return <section className="storefront-container py-12" aria-labelledby="storefront-faq-title">
    <h2 id="storefront-faq-title" className="ui-h2">{t("footer.faqTitle")}</h2>
    <div className="mt-6 rounded-2xl bg-slate-50 px-5 sm:px-8">
      {["shipping-policy", "returns-policy", "warranty-policy"].map(slug => <details key={slug} className="border-b border-slate-200 last:border-0">
        <summary className="cursor-pointer py-5 font-semibold">{t(`supportPages.${slug}.title`)}</summary>
        <p className="whitespace-pre-line pb-5 text-sm leading-7 text-slate-700">{t(`supportPages.${slug}.body`)}</p>
      </details>)}
    </div>
  </section>;
}

export function StorefrontFooter({ compact = false }: { compact?: boolean }) {
  const { t } = useTranslation();
  const footerColumns = [
    {
      title: t("footer.support"),
      links: [
        { label: t("footer.contact"), to: "/support/contact" },
        { label: t("footer.terms"), to: "/support/terms-of-service" },
        { label: t("footer.privacy"), to: "/support/privacy-policy" },
        { label: t("footer.shipping"), to: "/support/shipping-policy" },
        { label: t("footer.returns"), to: "/support/returns-policy" },
        { label: t("header.warrantyPolicy"), to: "/warranty-policy" },
        { label: t("footer.checkWarranty"), to: "/warranty" },
      ],
    },
    {
      title: t("footer.explore"),
      links: [
        { label: t("header.about"), to: "/about-us" },
        { label: t("header.news"), to: "/news" },
      ],
    },
  ];

  return (
    <>
      {!compact && <StorefrontFaq />}
      <footer className="bg-[#181818] px-5 py-12 text-white sm:px-8 lg:px-10 lg:py-14">
        <div className="mx-auto w-full max-w-[1440px]">
          <div className="grid gap-10 border-b border-white/10 pb-12 sm:grid-cols-2 lg:grid-cols-[1.7fr_repeat(4,1fr)]">
            <div>
              <h2 className="text-xl font-semibold">WorkspaceEcom</h2>
              <p className="ui-body mt-4 max-w-xs text-white/80">{t("footer.contactIntro")}</p>
              <Link to="/support/contact" className="mt-5 inline-flex min-h-11 items-center underline underline-offset-4">{t("footer.contact")}</Link>
            </div>

            {footerColumns.map((column) => (
              <nav key={column.title} aria-label={column.title}>
                <h3 className="text-xs font-semibold text-white">
                  {column.title}
                </h3>
                <ul className="mt-5 space-y-3">
                  {column.links.map((label) => (
                    <li key={label.label}>
                      <Link
                        to={label.to}
                        className="text-xs font-medium text-white/75 transition hover:text-white"
                      >
                        {label.label}
                      </Link>
                    </li>
                  ))}
                </ul>
              </nav>
            ))}
          </div>
          <div className="mt-10 flex flex-col gap-3 border-t border-white/10 pt-6 text-[11px] text-white/75 sm:flex-row sm:items-center sm:justify-between">
            <p>&copy; 2026 WorkspaceEcom. {t("footer.rights")}</p>
          </div>
        </div>
      </footer>
    </>
  );
}
