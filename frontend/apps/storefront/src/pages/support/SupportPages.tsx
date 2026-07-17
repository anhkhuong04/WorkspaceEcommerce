import { Link, Navigate, useParams } from "react-router-dom";

type SupportSection = {
  heading: string;
  body?: string;
  items?: string[];
};

type SupportPageContent = {
  slug: string;
  title: string;
  eyebrow: string;
  summary: string;
  sections: SupportSection[];
  contactPrompt?: string;
};

const supportPages: SupportPageContent[] = [
  {
    slug: "contact",
    eyebrow: "Support",
    title: "Contact Us",
    summary:
      "Reach our team for order support, product consultation, warranty questions, and partnership requests.",
    sections: [
      {
        heading: "Customer care",
        body:
          "For order updates, delivery questions, warranty guidance, or product recommendations, contact WorkspaceEcom through the official support channels below.",
        items: [
          "Email: support@workspaceecom.com",
          "Hotline: 1900 636 660",
          "Average response time: within 1 business day"
        ]
      },
      {
        heading: "Before you contact us",
        body:
          "Please include your order code, phone number, product name, and photos or videos when the request is related to delivery damage, return review, or warranty inspection."
      },
      {
        heading: "Business and bulk orders",
        body:
          "For office projects, bulk purchases, and installation planning, our team can prepare volume pricing, delivery coordination, and after-sales support."
      }
    ]
  },
  {
    slug: "terms-of-service",
    eyebrow: "Policy",
    title: "Terms of Service",
    summary:
      "These terms explain the basic rules for using WorkspaceEcom, placing orders, managing accounts, and completing transactions.",
    sections: [
      {
        heading: "Website usage",
        items: [
          "Customers must provide accurate account, contact, and delivery information.",
          "The website is intended for browsing, shopping, and account management related to WorkspaceEcom products.",
          "Customers are responsible for protecting their account credentials and reporting suspected unauthorized access."
        ]
      },
      {
        heading: "Order acceptance and pricing",
        items: [
          "Orders may require phone or email confirmation before fulfillment.",
          "WorkspaceEcom may refuse or cancel an order when product information, stock, pricing, delivery details, or payment status cannot be verified.",
          "If a pricing or product-display error occurs, our team will contact the customer to confirm the next step."
        ]
      },
      {
        heading: "Transactions and communication",
        body:
          "Customers can request support for changing or canceling an order before it is handed over to the carrier. Once the order is in transit, shipping and return policies apply."
      },
      {
        heading: "Intellectual property",
        body:
          "Product images, copy, UI elements, and other site content are provided for shopping and support purposes. They may not be reused for commercial purposes without written permission."
      }
    ]
  },
  {
    slug: "privacy-policy",
    eyebrow: "Privacy",
    title: "Privacy Policy",
    summary:
      "This policy describes what information WorkspaceEcom collects, why it is collected, and how it is protected while operating the store.",
    sections: [
      {
        heading: "Information we collect",
        items: [
          "Order information such as full name, phone number, email, delivery address, and order history.",
          "Payment-related status from supported payment providers. Sensitive card information is not stored by WorkspaceEcom.",
          "Account information, customer support messages, and website usage data used to improve the shopping experience."
        ]
      },
      {
        heading: "How we use information",
        items: [
          "Process orders, coordinate delivery, provide tracking updates, and support after-sales requests.",
          "Improve product discovery, customer support, promotions, and website reliability.",
          "Meet legal, tax, fraud-prevention, and security requirements."
        ]
      },
      {
        heading: "Sharing and protection",
        body:
          "Customer data is shared only with service providers needed to run the order flow, such as payment, delivery, analytics, and support systems. WorkspaceEcom does not sell customer personal data."
      },
      {
        heading: "Customer choices",
        body:
          "Customers may request account updates, delivery-information corrections, or marketing opt-out through the support team."
      }
    ]
  },
  {
    slug: "shipping-policy",
    eyebrow: "Delivery",
    title: "Shipping Policy",
    summary:
      "WorkspaceEcom prepares, packs, and ships orders through supported delivery partners across Vietnam.",
    sections: [
      {
        heading: "Delivery scope",
        body:
          "Standard delivery is available within Vietnam. For remote areas, oversized products, or special handling, support may contact the customer before dispatch."
      },
      {
        heading: "Estimated delivery time",
        items: [
          "Major cities: usually 1-2 business days after order confirmation.",
          "Other provinces: usually 3-5 business days after order confirmation.",
          "Large, custom, or bulk orders may require a separate delivery schedule."
        ]
      },
      {
        heading: "Shipping fee",
        body:
          "Shipping cost is calculated during checkout based on delivery address, cart weight, and service availability. Any special surcharge is confirmed before fulfillment."
      },
      {
        heading: "Receiving an order",
        items: [
          "Customers should inspect packaging, product model, color, and quantity before signing for receipt.",
          "If the package appears damaged or incomplete, contact support within 24 hours with photos or videos.",
          "After failed delivery attempts caused by unavailable or incorrect contact information, re-delivery fees may apply."
        ]
      }
    ]
  },
  {
    slug: "returns-policy",
    eyebrow: "After Sales",
    title: "Return Policy",
    summary:
      "Eligible products can be reviewed for return or exchange when they are defective, incorrect, or still meet the return conditions.",
    sections: [
      {
        heading: "Return conditions",
        items: [
          "Return requests should be submitted within 7 days from the delivery date.",
          "Products must be unused, complete, undamaged, and returned with original packaging, accessories, labels, and proof of purchase.",
          "A return may be approved for manufacturing defects, shipping damage, incorrect items, or eligible change-of-mind cases."
        ]
      },
      {
        heading: "Non-returnable cases",
        items: [
          "Products damaged by misuse, incorrect installation, unauthorized repair, or normal wear.",
          "Products missing invoices, accessories, packaging, serial numbers, or warranty labels.",
          "Custom items, clearance items, final-sale campaigns, and requests submitted after the return window."
        ]
      },
      {
        heading: "Return process",
        items: [
          "Contact support with order code, phone number, reason, and product condition evidence.",
          "The support team reviews the request and provides return instructions when eligible.",
          "After inspection, WorkspaceEcom may issue a replacement, exchange, store credit, or refund through the original payment method."
        ]
      },
      {
        heading: "Return costs",
        body:
          "WorkspaceEcom covers return shipping when the issue is caused by product defect, wrong item, or delivery damage. Customer-requested returns may include shipping or handling costs."
      }
    ]
  },
  {
    slug: "warranty-policy",
    eyebrow: "Warranty",
    title: "Warranty Policy",
    summary:
      "Warranty coverage protects eligible products against verified manufacturing defects during the applicable warranty period.",
    sections: [
      {
        heading: "Warranty scope",
        items: [
          "Warranty applies to products purchased from WorkspaceEcom or authorized sales channels with valid proof of purchase.",
          "Coverage is limited to manufacturing defects, material defects, and failures under normal use.",
          "Warranty periods vary by product category and are shown on product details, invoice, or warranty record."
        ]
      },
      {
        heading: "Warranty conditions",
        items: [
          "Customers should report defects promptly with clear images or videos.",
          "Products must be used according to the product manual, safety instructions, and intended purpose.",
          "The product serial number, invoice, warranty stamp, or equivalent proof must be readable and valid."
        ]
      },
      {
        heading: "Exclusions",
        items: [
          "Normal wear, cosmetic scratches, fading, fabric pilling, and aging from regular use.",
          "Damage caused by misuse, liquid exposure, electrical issues, accidents, unauthorized repair, or third-party modification.",
          "Damage during moving, self-transport, or installation performed outside authorized support instructions."
        ]
      },
      {
        heading: "Resolution",
        body:
          "After inspection, WorkspaceEcom may repair the product, replace the faulty component, provide an equivalent replacement, or recommend paid service when the issue is outside warranty."
      }
    ]
  }
];

const supportNav = supportPages.map((page) => ({
  slug: page.slug,
  title: page.title
}));

function findSupportPage(slug?: string) {
  return supportPages.find((page) => page.slug === slug);
}

export function SupportPage() {
  const { slug } = useParams();
  const page = findSupportPage(slug);

  if (!page) {
    return <Navigate to="/support/contact" replace />;
  }

  return (
    <div className="grid w-full gap-8 py-4">
      <header className="rounded-3xl bg-slate-950 px-6 py-10 text-white sm:px-10 sm:py-12">
        <p className="ui-caption uppercase tracking-[0.2em] text-white/60">{page.eyebrow}</p>
        <h1 className="mt-4 max-w-3xl text-4xl font-black tracking-tight sm:text-5xl">{page.title}</h1>
        <p className="ui-body mt-5 max-w-2xl text-white/70">{page.summary}</p>
      </header>

      <div className="grid gap-8 lg:grid-cols-[280px_1fr]">
        <aside className="lg:sticky lg:top-28 lg:self-start">
          <nav className="rounded-2xl border border-slate-100 bg-white p-3 shadow-sm" aria-label="Support pages">
            {supportNav.map((item) => (
              <Link
                key={item.slug}
                to={`/support/${item.slug}`}
                className={`block rounded-xl px-4 py-3 text-sm font-bold transition ${
                  item.slug === page.slug
                    ? "bg-slate-950 text-white"
                    : "text-slate-600 hover:bg-slate-50 hover:text-slate-950"
                }`}
              >
                {item.title}
              </Link>
            ))}
          </nav>
        </aside>

        <article className="overflow-hidden rounded-3xl border border-slate-100 bg-white shadow-sm">
          <div className="grid gap-8 px-6 py-8 sm:px-10 sm:py-10">
          {page.sections.map((section) => (
            <section key={section.heading} className="border-b border-slate-100 pb-8 last:border-b-0 last:pb-0">
              <h2 className="text-xl font-black tracking-tight text-slate-950">{section.heading}</h2>
              {section.body ? <p className="ui-body mt-4 max-w-3xl text-slate-600">{section.body}</p> : null}
              {section.items ? (
                <ul className="mt-4 grid gap-3 text-sm font-semibold leading-6 text-slate-600">
                  {section.items.map((item) => (
                    <li key={item} className="flex gap-3">
                      <span className="mt-2 h-1.5 w-1.5 shrink-0 rounded-full bg-[var(--brand)]" aria-hidden="true" />
                      <span>{item}</span>
                    </li>
                  ))}
                </ul>
              ) : null}
            </section>
          ))}

          <div className="rounded-2xl bg-slate-50 p-5 sm:flex sm:items-center sm:justify-between sm:gap-6">
            <div>
              <h2 className="text-base font-black text-slate-950">Need help with an order?</h2>
              <p className="ui-body mt-2 text-slate-600">
                Use order lookup for live order status, or contact support with your order code.
              </p>
            </div>
            <Link
              to="/orders/lookup"
              className="mt-4 inline-flex rounded-full bg-slate-950 px-5 py-3 text-sm font-black text-white transition hover:bg-slate-800 sm:mt-0"
            >
              Check order
            </Link>
          </div>
          </div>
        </article>
      </div>
    </div>
  );
}
