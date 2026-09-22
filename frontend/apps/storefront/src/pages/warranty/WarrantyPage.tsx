import { useMutation } from "@tanstack/react-query";
import type { PublicWarrantyLookupResponse, WarrantyEntitlementStatus } from "@workspace-ecommerce/api-types";
import { formatDate } from "@workspace-ecommerce/shared-utils";
import { useState, type FormEvent } from "react";
import { useTranslation } from "react-i18next";
import { Link, useNavigate } from "react-router-dom";
import { useCustomerAuth } from "../../features/customer-auth/useCustomerAuth";
import { getApiErrorMessage } from "../../services/api/errors";
import { storefrontApi } from "../../services/api/storefrontApi";

const statusLabelKey: Record<WarrantyEntitlementStatus, string> = {
  0: "warranty.status.awaiting",
  1: "warranty.status.active",
  2: "warranty.status.voided",
  3: "warranty.status.replaced"
};

export function WarrantyPage() {
  const { t } = useTranslation();
  const [identifier, setIdentifier] = useState("");
  const [result, setResult] = useState<PublicWarrantyLookupResponse | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [showLoginPrompt, setShowLoginPrompt] = useState(false);
  const { isAuthenticated } = useCustomerAuth();
  const navigate = useNavigate();

  const lookupMutation = useMutation({
    mutationFn: storefrontApi.lookupWarranty,
    onSuccess: (response) => {
      setResult(response);
      setMessage(null);
    },
    onError: (error) => {
      setResult(null);
      setMessage(getApiErrorMessage(error));
    }
  });
  const activationMutation = useMutation({
    mutationFn: storefrontApi.activateWarranty,
    onSuccess: (warranty) => {
      setMessage(t("warranty.activatedSuccess", { identifier: warranty.maskedIdentifier }));
      void lookupMutation.mutateAsync({ identifier });
    },
    onError: (error) => setMessage(getApiErrorMessage(error))
  });

  function submitLookup(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const value = identifier.trim();
    if (value.length < 3) {
      setMessage(t("warranty.invalidIdentifier"));
      return;
    }

    void lookupMutation.mutate({ identifier: value });
  }

  function activate() {
    if (!isAuthenticated) {
      setShowLoginPrompt(true);
      return;
    }

    void activationMutation.mutate({ identifier: identifier.trim() });
  }

  return (
    <section className="mx-auto max-w-[1200px] py-4 sm:py-10">
      <div className="rounded-[32px] border border-slate-200 bg-[#f5f8fc] px-5 py-10 text-center shadow-[0_20px_55px_rgba(15,23,42,0.07)] sm:px-10 sm:py-14">
        <span className="inline-flex items-center gap-2 rounded-full bg-blue-100 px-4 py-2 text-xs font-black uppercase tracking-[0.18em] text-blue-700">
          <span aria-hidden="true">⌾</span> {t("warranty.eyebrow")}
        </span>
        <h1 className="ui-h1 mt-5 text-slate-950">{t("warranty.title")}</h1>
        <p className="ui-body mx-auto mt-4 max-w-2xl text-slate-600">
          {t("warranty.description")}
        </p>

        <form onSubmit={submitLookup} className="mx-auto mt-8 flex max-w-4xl flex-col gap-3 rounded-[22px] border border-slate-200 bg-white p-3 shadow-sm sm:flex-row sm:p-4">
          <label className="sr-only" htmlFor="warranty-identifier">{t("warranty.identifier")}</label>
          <input
            id="warranty-identifier"
            value={identifier}
            onChange={(event) => setIdentifier(event.target.value)}
            autoComplete="off"
            maxLength={64}
            placeholder={t("warranty.placeholder")}
            className="min-h-14 min-w-0 flex-1 rounded-xl px-4 text-base text-slate-950 outline-none ring-offset-2 placeholder:text-slate-400 focus:ring-2 focus:ring-blue-600"
          />
          <button type="submit" disabled={lookupMutation.isPending} className="min-h-14 rounded-xl bg-[var(--brand)] px-7 font-bold text-white transition hover:bg-slate-950 disabled:cursor-not-allowed disabled:opacity-60">
            {lookupMutation.isPending ? t("warranty.checking") : t("warranty.check")}
          </button>
        </form>

        <p className="mt-3 text-xs font-medium text-slate-500">{t("warranty.privacyHint")}</p>

        {message ? <div className="mx-auto mt-6 max-w-4xl rounded-xl border border-blue-200 bg-blue-50 px-4 py-3 text-sm font-semibold text-blue-900" role="status">{message}</div> : null}

        {result ? <WarrantyResult result={result} activating={activationMutation.isPending} onActivate={activate} /> : null}
      </div>

      <div className="mt-8 grid gap-5 md:grid-cols-3">
        <Step number="1" title={t("warranty.step1Title")} description={t("warranty.step1Description")} />
        <Step number="2" title={t("warranty.step2Title")} description={t("warranty.step2Description")} />
        <Step number="3" title={t("warranty.step3Title")} description={t("warranty.step3Description")} />
      </div>

      <p className="mt-7 text-center text-sm text-slate-600">{t("warranty.policyPrompt")} <Link to="/warranty-policy" className="font-bold text-slate-950 underline underline-offset-4">{t("warranty.readPolicy")}</Link>.</p>

      {showLoginPrompt ? (
        <div className="fixed inset-0 z-50 grid place-items-center bg-slate-950/45 p-4 backdrop-blur-sm" role="presentation">
          <div className="w-full max-w-md rounded-3xl bg-white p-6 text-left shadow-2xl" role="dialog" aria-modal="true" aria-labelledby="warranty-login-title">
            <h2 id="warranty-login-title" className="text-xl font-black text-slate-950">{t("warranty.signInRequired")}</h2>
            <p className="mt-3 text-sm leading-6 text-slate-600">{t("warranty.signInDescription")}</p>
            <div className="mt-6 flex justify-end gap-3">
              <button type="button" onClick={() => setShowLoginPrompt(false)} className="rounded-xl border border-slate-200 px-4 py-2 text-sm font-bold text-slate-700">{t("common.cancel")}</button>
              <button type="button" onClick={() => navigate("/login", { state: { from: "/warranty" } })} className="rounded-xl bg-slate-950 px-4 py-2 text-sm font-bold text-white">{t("warranty.signIn")}</button>
            </div>
          </div>
        </div>
      ) : null}
    </section>
  );
}

function WarrantyResult({ result, activating, onActivate }: { result: PublicWarrantyLookupResponse; activating: boolean; onActivate: () => void }) {
  const { t } = useTranslation();
  if (!result.found) {
    return <div className="mx-auto mt-6 max-w-4xl rounded-2xl border border-slate-200 bg-white p-5 text-left text-sm font-semibold text-slate-600" role="status">{t("warranty.notFound")}</div>;
  }

  const canActivate = result.status === 0;
  return (
    <section className="mx-auto mt-6 max-w-4xl rounded-2xl border border-slate-200 bg-white p-5 text-left shadow-sm" aria-live="polite">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <p className="text-xs font-black uppercase tracking-[0.16em] text-blue-700">{t("warranty.record")}</p>
          <h2 className="mt-1 text-xl font-black text-slate-950">{result.productName}</h2>
          <p className="mt-1 text-sm font-semibold text-slate-500">{result.maskedIdentifier}</p>
        </div>
        <span className={`rounded-full px-3 py-1.5 text-sm font-black ${result.status === 1 ? "bg-emerald-100 text-emerald-800" : "bg-amber-100 text-amber-800"}`}>{result.status === null ? t("warranty.status.unknown") : t(statusLabelKey[result.status])}</span>
      </div>
      {result.activatedAt ? <p className="mt-4 text-sm text-slate-600">{t("warranty.activatedOn", { date: formatDate(result.activatedAt) })}</p> : null}
      {result.coverages.length > 0 ? (
        <div className="mt-5 grid gap-3 sm:grid-cols-2">
          {result.coverages.map((coverage) => <div key={coverage.componentCode} className="rounded-xl bg-slate-50 p-4"><p className="font-bold text-slate-950">{coverage.displayName}</p><p className="mt-1 text-sm text-slate-600">{coverage.endsAt ? t("warranty.coverageEnds", { date: formatDate(coverage.endsAt) }) : t("warranty.durationMonths", { count: coverage.durationMonths })}</p></div>)}
        </div>
      ) : null}
      {canActivate ? <button type="button" disabled={activating} onClick={onActivate} className="mt-5 rounded-xl bg-[var(--brand)] px-5 py-3 text-sm font-bold text-white transition hover:bg-slate-950 disabled:opacity-60">{activating ? t("warranty.activating") : t("warranty.activate")}</button> : null}
    </section>
  );
}

function Step({ number, title, description }: { number: string; title: string; description: string }) {
  return <article className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm"><span className="grid h-10 w-10 place-items-center rounded-full bg-blue-100 font-black text-blue-700">{number}</span><h2 className="mt-5 text-lg font-black text-slate-950">{title}</h2><p className="mt-2 text-sm leading-6 text-slate-600">{description}</p></article>;
}
