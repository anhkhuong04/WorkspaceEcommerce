import { useEffect, useState, type FormEvent, type ReactNode } from "react";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
import { getApiErrorMessage } from "../../services/api/errors";
import { storefrontApi } from "../../services/api/storefrontApi";

function LifecycleShell({ children, title, subtitle }: { children: ReactNode; title: string; subtitle: string }) {
  return (
    <section className="mx-auto flex min-h-[60vh] max-w-lg items-center px-5 py-12">
      <div className="w-full rounded-2xl border border-slate-200 bg-white p-6 shadow-sm sm:p-8">
        <h1 className="text-2xl font-bold text-slate-950">{title}</h1>
        <p className="mt-2 text-sm text-slate-500">{subtitle}</p>
        {children}
      </div>
    </section>
  );
}

const inputClassName = "mt-1.5 h-11 w-full rounded-md border border-slate-300 px-3 text-sm outline-none focus:border-slate-950 focus:ring-1 focus:ring-slate-950";
const buttonClassName = "mt-5 h-11 w-full rounded-md bg-slate-950 text-sm font-semibold text-white transition hover:bg-slate-800 disabled:cursor-not-allowed disabled:opacity-60";

export function EmailVerificationPage() {
  const [searchParams] = useSearchParams();
  const token = searchParams.get("token");
  const [email, setEmail] = useState("");
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  useEffect(() => {
    if (!token) {
      return;
    }

    let active = true;
    void storefrontApi.confirmEmailVerification({ token })
      .then(() => {
        if (active) setMessage("Your email address has been verified. You can now sign in.");
      })
      .catch((requestError: unknown) => {
        if (active) setError(getApiErrorMessage(requestError));
      });
    return () => {
      active = false;
    };
  }, [token]);

  async function requestVerification(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setIsSubmitting(true);
    setError(null);
    try {
      await storefrontApi.requestEmailVerification({ email });
      // This deliberately stays neutral so UI copy cannot reveal whether an
      // arbitrary account email exists.
      setMessage("If the account is eligible, an email has been sent.");
    } catch (requestError) {
      setError(getApiErrorMessage(requestError));
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <LifecycleShell title="Verify your email" subtitle="Open the verification link from your email, or request a new link below.">
      {message ? <p className="mt-5 rounded-md bg-emerald-50 px-4 py-3 text-sm font-medium text-emerald-800">{message}</p> : null}
      {error ? <p className="mt-5 rounded-md bg-red-50 px-4 py-3 text-sm font-medium text-red-700">{error}</p> : null}
      <form className="mt-5" onSubmit={requestVerification}>
        <label className="block text-sm font-semibold text-slate-800">
          Email address
          <input value={email} onChange={(event) => setEmail(event.target.value)} type="email" autoComplete="email" required className={inputClassName} />
        </label>
        <button className={buttonClassName} disabled={isSubmitting} type="submit">{isSubmitting ? "Requesting…" : "Send verification link"}</button>
      </form>
      <Link className="mt-5 block text-center text-sm font-semibold text-slate-700 underline underline-offset-4" to="/login">Back to sign in</Link>
    </LifecycleShell>
  );
}

export function PasswordResetPage() {
  const [searchParams] = useSearchParams();
  const token = searchParams.get("token");
  const navigate = useNavigate();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function submitForgotPassword(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setIsSubmitting(true);
    setError(null);
    try {
      await storefrontApi.forgotCustomerPassword({ email });
      setMessage("If the account is eligible, an email has been sent.");
    } catch (requestError) {
      setError(getApiErrorMessage(requestError));
    } finally {
      setIsSubmitting(false);
    }
  }

  async function submitPasswordReset(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!token) return;
    if (password !== confirmPassword) {
      setError("Passwords do not match.");
      return;
    }

    setIsSubmitting(true);
    setError(null);
    try {
      await storefrontApi.resetCustomerPassword({ token, newPassword: password });
      setMessage("Your password has been reset. Please sign in again.");
      window.setTimeout(() => navigate("/login", { replace: true }), 900);
    } catch (requestError) {
      setError(getApiErrorMessage(requestError));
    } finally {
      setIsSubmitting(false);
    }
  }

  const isReset = Boolean(token);
  return (
    <LifecycleShell
      title={isReset ? "Choose a new password" : "Reset your password"}
      subtitle={isReset ? "This link can be used once. Resetting your password signs out every existing session." : "Enter your email and we will send a reset link if the account is eligible."}>
      {message ? <p className="mt-5 rounded-md bg-emerald-50 px-4 py-3 text-sm font-medium text-emerald-800">{message}</p> : null}
      {error ? <p className="mt-5 rounded-md bg-red-50 px-4 py-3 text-sm font-medium text-red-700">{error}</p> : null}
      {isReset ? (
        <form className="mt-5" onSubmit={submitPasswordReset}>
          <label className="block text-sm font-semibold text-slate-800">
            New password
            <input value={password} onChange={(event) => setPassword(event.target.value)} type="password" autoComplete="new-password" minLength={8} maxLength={128} required className={inputClassName} />
          </label>
          <label className="mt-4 block text-sm font-semibold text-slate-800">
            Confirm new password
            <input value={confirmPassword} onChange={(event) => setConfirmPassword(event.target.value)} type="password" autoComplete="new-password" minLength={8} maxLength={128} required className={inputClassName} />
          </label>
          <button className={buttonClassName} disabled={isSubmitting} type="submit">{isSubmitting ? "Resetting…" : "Reset password"}</button>
        </form>
      ) : (
        <form className="mt-5" onSubmit={submitForgotPassword}>
          <label className="block text-sm font-semibold text-slate-800">
            Email address
            <input value={email} onChange={(event) => setEmail(event.target.value)} type="email" autoComplete="email" required className={inputClassName} />
          </label>
          <button className={buttonClassName} disabled={isSubmitting} type="submit">{isSubmitting ? "Requesting…" : "Send reset link"}</button>
        </form>
      )}
      <Link className="mt-5 block text-center text-sm font-semibold text-slate-700 underline underline-offset-4" to="/login">Back to sign in</Link>
    </LifecycleShell>
  );
}
