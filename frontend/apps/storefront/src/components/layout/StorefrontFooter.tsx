import type { FormEvent, ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router-dom";

function ServiceIcon({
  type,
}: {
  type: "shipping" | "installation" | "restoration" | "manual";
}) {
  const paths: Record<typeof type, ReactNode> = {
    shipping: (
      <path d="M3 7.5 12 3l9 4.5-9 4.5-9-4.5Zm2 3.2V16l7 4 7-4v-5.3M12 12v8" />
    ),
    installation: (
      <path d="m14.5 5.5 4-4 4 4-4 4m-13 9-4 4 4 4 4-4m-2-11 9 9m-4-15 6 6M3 3l5 1 1 4-2 2-4-4V3Z" />
    ),
    restoration: (
      <path d="M7 8V5a5 5 0 0 1 10 0v3m-12 0h14v13H5V8Zm7 4v5m-2-3h4" />
    ),
    manual: (
      <path d="M5 3h11a3 3 0 0 1 3 3v15H7a2 2 0 0 1-2-2V3Zm2 14h12M12 7v6m-3-3h6" />
    ),
  };

  return (
    <svg
      className="h-6 w-6"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.8"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      {paths[type]}
    </svg>
  );
}

function StorefrontFaq() {
  const { t } = useTranslation();
  const services = [
    {
      title: t("footer.services.shippingTitle"),
      description: t("footer.services.shippingDescription"),
      icon: "shipping" as const,
    },
    {
      title: t("footer.services.installationTitle"),
      description: t("footer.services.installationDescription"),
      icon: "installation" as const,
    },
    {
      title: t("footer.services.restorationTitle"),
      description: t("footer.services.restorationDescription"),
      icon: "restoration" as const,
    },
    {
      title: t("footer.services.manualTitle"),
      description: t("footer.services.manualDescription"),
      icon: "manual" as const,
    },
  ];
  const faqs = [
    {
      question: t("footer.faq.paymentQuestion"),
      answer: <><p>{t("footer.faq.paymentIntro")}</p><ul className="mt-3 list-disc space-y-2 pl-5"><li>{t("footer.faq.paymentBank")}</li><li>{t("footer.faq.paymentCard")}</li><li>{t("footer.faq.paymentQr")}</li><li>{t("footer.faq.paymentCod")}</li></ul><p className="mt-4">{t("footer.faq.paymentOffer")}</p></>,
    },
    {
      question: t("footer.faq.deliveryQuestion"),
      answer: <><p>{t("footer.faq.deliveryCities")}</p><p className="mt-4">{t("footer.faq.deliveryOther")}</p></>,
    },
    { question: t("footer.faq.inspectionQuestion"), answer: <p>{t("footer.faq.inspectionAnswer")}</p> },
    { question: t("footer.faq.returnQuestion"), answer: <p>{t("footer.faq.returnAnswer")}</p> },
    { question: t("footer.faq.bulkQuestion"), answer: <p>{t("footer.faq.bulkAnswer")}</p> },
    { question: t("footer.faq.otherQuestion"), answer: <p>{t("footer.faq.otherAnswer")}</p> },
  ];

  return (
    <section
      className="bg-white px-5 py-14 sm:px-8 lg:px-10 lg:py-20"
      aria-labelledby="faq-title"
    >
      <div className="mx-auto grid w-full max-w-[1440px] gap-10 lg:grid-cols-[1fr_1.08fr] lg:gap-12">
        <div>
          <h2 id="faq-title" className="ui-h1 tracking-tight text-slate-950">
            {t("footer.haveQuestion")}
          </h2>
          <p className="ui-body mt-5 max-w-lg text-slate-600">
            {t("footer.faqDescription")}
          </p>
          <p className="ui-caption mt-8 text-slate-500">
            {t("footer.responseTime")}
          </p>

          <div className="mt-8 grid gap-3 sm:grid-cols-2">
            {services.map((service) => (
              <article
                key={service.title}
                className="relative min-h-28 rounded-xl bg-[#f3f3f3] p-5 text-slate-800"
              >
                <ServiceIcon type={service.icon} />
                <span
                  className="absolute right-4 top-3 text-lg font-light"
                  aria-hidden="true"
                >
                  +
                </span>
                <h3 className="mt-3 text-sm font-semibold text-slate-950">
                  {service.title}
                </h3>
                <p className="ui-caption mt-1 text-slate-500">
                  {service.description}
                </p>
              </article>
            ))}
          </div>
        </div>

        <div className="rounded-2xl bg-[#f3f3f3] px-5 py-3 sm:px-8">
          {faqs.map((faq) => (
            <details
              key={faq.question}
              className="group border-b border-slate-300/80 last:border-b-0"
            >
              <summary className="flex cursor-pointer list-none items-center justify-between gap-5 py-5 text-sm font-semibold text-slate-950 [&::-webkit-details-marker]:hidden">
                {faq.question}
                <span
                  className="grid h-5 w-5 shrink-0 place-items-center rounded-full bg-slate-300 text-slate-700 transition-transform group-open:rotate-180 group-open:bg-slate-800 group-open:text-white"
                  aria-hidden="true"
                >
                  <svg className="h-3 w-3" viewBox="0 0 12 12" fill="none">
                    <path
                      d="m3 4.5 3 3 3-3"
                      stroke="currentColor"
                      strokeWidth="1.5"
                      strokeLinecap="round"
                      strokeLinejoin="round"
                    />
                  </svg>
                </span>
              </summary>
              <div className="ui-body max-w-xl pb-6 pr-8 text-slate-700">
                {faq.answer}
              </div>
            </details>
          ))}
        </div>
      </div>
    </section>
  );
}

export function StorefrontFooter() {
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
        { label: t("footer.recruitment"), to: "#" },
        { label: t("header.showroom"), to: "#" },
        { label: t("header.news"), to: "/news" },
      ],
    },
  ];
  function submitNewsletter(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
  }

  return (
    <>
      <StorefrontFaq />
      <footer className="bg-[#181818] px-5 py-12 text-white sm:px-8 lg:px-10 lg:py-14">
        <div className="mx-auto w-full max-w-[1440px]">
          <div className="grid gap-10 border-b border-white/10 pb-12 sm:grid-cols-2 lg:grid-cols-[1.7fr_repeat(4,1fr)]">
            <div>
              <h2 className="text-xl font-semibold">{t("footer.subscribe")}</h2>
              <p className="ui-body mt-4 max-w-xs text-white/65">
                {t("footer.subscribeDescription")}
              </p>
              <form
                onSubmit={submitNewsletter}
                className="mt-5 flex max-w-sm rounded-lg border border-white/20 p-1"
              >
                <label htmlFor="footer-email" className="sr-only">
                  {t("footer.emailAddress")}
                </label>
                <input
                  id="footer-email"
                  type="email"
                  required
                  placeholder={t("footer.email")}
                  className="min-w-0 flex-1 bg-transparent px-3 py-2 text-sm text-white outline-none placeholder:text-white/45"
                />
                <button
                  type="submit"
                  className="grid h-8 w-8 place-items-center rounded-full bg-white/10 transition hover:bg-white/20"
                  aria-label={t("footer.subscribeAction")}
                >
                  <svg
                    className="h-3 w-3"
                    viewBox="0 0 12 12"
                    fill="none"
                    aria-hidden="true"
                  >
                    <path
                      d="m4.5 3 3 3-3 3"
                      stroke="currentColor"
                      strokeWidth="1.5"
                      strokeLinecap="round"
                      strokeLinejoin="round"
                    />
                  </svg>
                </button>
              </form>
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
                        className="text-xs font-medium text-white/55 transition hover:text-white"
                      >
                        {label.label}
                      </Link>
                    </li>
                  ))}
                </ul>
              </nav>
            ))}
          </div>
          <div className="mt-10 flex flex-col gap-3 border-t border-white/10 pt-6 text-[11px] text-white/45 sm:flex-row sm:items-center sm:justify-between">
            <p>&copy; 2026 WorkspaceEcom. {t("footer.rights")}</p>
          </div>
        </div>
      </footer>
    </>
  );
}
