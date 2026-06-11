import type { FormEvent, ReactNode } from "react";
import { Link } from "react-router-dom";

const faqs = [
  {
    question: "What are the payment methods?",
    answer: (
      <>
        <p>WorkspaceEcom accepts payments via:</p>
        <ul className="mt-3 list-disc space-y-2 pl-5">
          <li>Online banking transfer</li>
          <li>Visa and Mastercard credit cards</li>
          <li>Banking QR Code</li>
          <li>Cash on Delivery (COD)</li>
        </ul>
        <p className="mt-4">Customers who pay in advance by bank transfer may receive special offers.</p>
      </>
    )
  },
  {
    question: "How long does delivery take?",
    answer: (
      <>
        <p>Orders in major cities are usually delivered within 1-2 business days.</p>
        <p className="mt-4">For other areas, shipping can take 3-7 business days depending on the location.</p>
      </>
    )
  },
  {
    question: "Product inspection and warranty",
    answer: <p>You may inspect the package on delivery. Warranty coverage depends on the product and is shown on its detail page.</p>
  },
  {
    question: "Product returns process",
    answer: <p>Eligible products can be returned within 7 days. Items must remain complete, undamaged, and in their original packaging.</p>
  },
  {
    question: "Bulk orders",
    answer: <p>Contact our support team for volume pricing, office packages, installation planning, and dedicated business support.</p>
  },
  {
    question: "Have any other questions?",
    answer: <p>Email us at support@workspaceecom.com or call 1900 636 660. Our team will help you find the right answer.</p>
  }
];

const footerColumns = [
  { title: "Marketing Partnership", links: ["Connecting Creators", "Press and Media", "Events & Campaigns"] },
  { title: "Business Partnership", links: ["Featured Projects", "Office Solutions Package", "B2B Partnership", "Open Box Products"] },
  { title: "Support", links: ["Contact Us", "Terms of Service", "Privacy Policy", "Shipping Policy", "Return Policy", "Warranty Policy"] },
  { title: "Explore", links: ["About Us", "Recruitment", "Showroom", "News"] }
];

function ServiceIcon({ type }: { type: "shipping" | "installation" | "restoration" | "manual" }) {
  const paths: Record<typeof type, ReactNode> = {
    shipping: <path d="M3 7.5 12 3l9 4.5-9 4.5-9-4.5Zm2 3.2V16l7 4 7-4v-5.3M12 12v8" />,
    installation: <path d="m14.5 5.5 4-4 4 4-4 4m-13 9-4 4 4 4 4-4m-2-11 9 9m-4-15 6 6M3 3l5 1 1 4-2 2-4-4V3Z" />,
    restoration: <path d="M7 8V5a5 5 0 0 1 10 0v3m-12 0h14v13H5V8Zm7 4v5m-2-3h4" />,
    manual: <path d="M5 3h11a3 3 0 0 1 3 3v15H7a2 2 0 0 1-2-2V3Zm2 14h12M12 7v6m-3-3h6" />
  };

  return <svg className="h-6 w-6" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">{paths[type]}</svg>;
}

function StorefrontFaq() {
  const services = [
    { title: "Safe shipping", description: "Absolute protection for the products you order", icon: "shipping" as const },
    { title: "Professional installation", description: "Installation exactly where you want it", icon: "installation" as const },
    { title: "Site restoration", description: "Clean packaging and the installation space before leaving", icon: "restoration" as const },
    { title: "User manual", description: "Clear guidance to help you master the product", icon: "manual" as const }
  ];

  return (
    <section className="bg-white px-5 py-14 sm:px-8 lg:px-10 lg:py-20" aria-labelledby="faq-title">
      <div className="mx-auto grid w-full max-w-[1440px] gap-10 lg:grid-cols-[1fr_1.08fr] lg:gap-12">
        <div>
          <h2 id="faq-title" className="ui-h1 tracking-tight text-slate-950">Have a question?</h2>
          <p className="ui-body mt-5 max-w-lg text-slate-600">Our FAQs will help you quickly find answers to common questions about our products and services.</p>
          <p className="ui-caption mt-8 text-slate-500">Average response time: 1 hour</p>

          <div className="mt-8 grid gap-3 sm:grid-cols-2">
            {services.map((service) => (
              <article key={service.title} className="relative min-h-28 rounded-xl bg-[#f3f3f3] p-5 text-slate-800">
                <ServiceIcon type={service.icon} />
                <span className="absolute right-4 top-3 text-lg font-light" aria-hidden="true">+</span>
                <h3 className="mt-3 text-sm font-semibold text-slate-950">{service.title}</h3>
                <p className="ui-caption mt-1 text-slate-500">{service.description}</p>
              </article>
            ))}
          </div>
        </div>

        <div className="rounded-2xl bg-[#f3f3f3] px-5 py-3 sm:px-8">
          {faqs.map((faq) => (
            <details key={faq.question} className="group border-b border-slate-300/80 last:border-b-0">
              <summary className="flex cursor-pointer list-none items-center justify-between gap-5 py-5 text-sm font-semibold text-slate-950 [&::-webkit-details-marker]:hidden">
                {faq.question}
                <span className="grid h-5 w-5 shrink-0 place-items-center rounded-full bg-slate-300 text-slate-700 transition-transform group-open:rotate-180 group-open:bg-slate-800 group-open:text-white" aria-hidden="true">
                  <svg className="h-3 w-3" viewBox="0 0 12 12" fill="none"><path d="m3 4.5 3 3 3-3" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" /></svg>
                </span>
              </summary>
              <div className="ui-body max-w-xl pb-6 pr-8 text-slate-700">{faq.answer}</div>
            </details>
          ))}
        </div>
      </div>
    </section>
  );
}

export function StorefrontFooter() {
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
              <h2 className="text-xl font-semibold">Subscribe to newsletter</h2>
              <p className="ui-body mt-4 max-w-xs text-white/65">Stay updated with news, workspace design trends, and our latest products.</p>
              <form onSubmit={submitNewsletter} className="mt-5 flex max-w-sm rounded-lg border border-white/20 p-1">
                <label htmlFor="footer-email" className="sr-only">Email address</label>
                <input id="footer-email" type="email" required placeholder="Email" className="min-w-0 flex-1 bg-transparent px-3 py-2 text-sm text-white outline-none placeholder:text-white/45" />
                <button type="submit" className="grid h-8 w-8 place-items-center rounded-full bg-white/10 transition hover:bg-white/20" aria-label="Subscribe">
                  <svg className="h-3 w-3" viewBox="0 0 12 12" fill="none" aria-hidden="true"><path d="m4.5 3 3 3-3 3" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" /></svg>
                </button>
              </form>
            </div>

            {footerColumns.map((column) => (
              <nav key={column.title} aria-label={column.title}>
                <h3 className="text-xs font-semibold text-white">{column.title}</h3>
                <ul className="mt-5 space-y-3">
                  {column.links.map((label) => (
                    <li key={label}><a href="#" className="text-xs font-medium text-white/55 transition hover:text-white">{label}</a></li>
                  ))}
                </ul>
              </nav>
            ))}
          </div>

          <div className="grid gap-10 pt-10 lg:grid-cols-[1.25fr_1fr]">
            <div>
              <div className="flex gap-4 text-sm font-semibold" aria-label="Social media">
                {['f', 'ig', 'in', 'yt', 'tt'].map((network) => <a key={network} href="#" className="grid h-7 min-w-7 place-items-center rounded-full border border-white/25 px-1 text-[10px] uppercase transition hover:border-white">{network}</a>)}
              </div>
              <h3 className="mt-8 text-sm font-semibold uppercase">Workspace Ecommerce Company</h3>
              <ul className="ui-caption mt-4 space-y-2 text-white/65">
                <li>Business registration certificate: 0109193046</li>
                <li>Headquarters: Bangkok, Thailand</li>
                <li>Customer support available Monday through Saturday</li>
              </ul>
            </div>
          </div>

          <div className="mt-10 flex flex-col gap-3 border-t border-white/10 pt-6 text-[11px] text-white/45 sm:flex-row sm:items-center sm:justify-between">
            <p>&copy; 2026 WorkspaceEcom. All rights reserved.</p>
            <Link to="/products" className="transition hover:text-white">Browse all products</Link>
          </div>
        </div>
      </footer>
    </>
  );
}
