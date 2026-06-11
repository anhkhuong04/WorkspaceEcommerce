import { useState } from "react";
import { Link } from "react-router-dom";

type AuthMode = "login" | "register";

function MailIcon() {
  return (
    <svg className="h-5 w-5" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.7" aria-hidden="true">
      <rect x="3" y="5" width="18" height="14" rx="2" />
      <path d="m4 7 8 6 8-6" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

function UserIcon() {
  return (
    <svg className="h-5 w-5" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.7" aria-hidden="true">
      <circle cx="12" cy="8" r="4" />
      <path d="M4.5 20c1.4-4 4-6 7.5-6s6.1 2 7.5 6" strokeLinecap="round" />
    </svg>
  );
}

function LockIcon() {
  return (
    <svg className="h-5 w-5" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.7" aria-hidden="true">
      <rect x="5" y="10" width="14" height="11" rx="2" />
      <path d="M8 10V7a4 4 0 0 1 8 0v3" strokeLinecap="round" />
    </svg>
  );
}

function EyeIcon({ hidden }: { hidden: boolean }) {
  return (
    <svg className="h-5 w-5" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.7" aria-hidden="true">
      <path d="M2.5 12s3.5-6 9.5-6 9.5 6 9.5 6-3.5 6-9.5 6-9.5-6-9.5-6Z" />
      <circle cx="12" cy="12" r="2.5" />
      {hidden ? <path d="m4 4 16 16" strokeLinecap="round" /> : null}
    </svg>
  );
}

function GoogleIcon() {
  return (
    <svg className="h-5 w-5" viewBox="0 0 24 24" aria-hidden="true">
      <path fill="#4285F4" d="M21.6 12.2c0-.7-.1-1.4-.2-2H12v3.9h5.4a4.6 4.6 0 0 1-2 3v2.6h3.2c1.9-1.8 3-4.4 3-7.5Z" />
      <path fill="#34A853" d="M12 22c2.7 0 5-.9 6.6-2.3l-3.2-2.6c-.9.6-2 1-3.4 1a5.8 5.8 0 0 1-5.5-4H3.2v2.6A10 10 0 0 0 12 22Z" />
      <path fill="#FBBC05" d="M6.5 14.1a6 6 0 0 1 0-4.2V7.3H3.2a10 10 0 0 0 0 9.4l3.3-2.6Z" />
      <path fill="#EA4335" d="M12 5.9c1.5 0 2.9.5 3.9 1.5l2.8-2.8A9.5 9.5 0 0 0 3.2 7.3l3.3 2.6a5.8 5.8 0 0 1 5.5-4Z" />
    </svg>
  );
}

interface TextFieldProps {
  label: string;
  type: "text" | "email" | "password";
  placeholder: string;
  autoComplete: string;
  icon: "user" | "mail" | "lock";
  showPassword?: boolean;
  onTogglePassword?: () => void;
}

function TextField({ label, type, placeholder, autoComplete, icon, showPassword, onTogglePassword }: TextFieldProps) {
  const resolvedType = type === "password" && showPassword ? "text" : type;

  return (
    <label className="block">
      <span className="mb-1.5 block text-sm font-semibold">{label}</span>
      <span className="flex h-12 items-center gap-3 rounded-md border border-slate-300 px-4 text-slate-400 transition focus-within:border-slate-950 focus-within:ring-1 focus-within:ring-slate-950">
        {icon === "user" ? <UserIcon /> : icon === "mail" ? <MailIcon /> : <LockIcon />}
        <input type={resolvedType} autoComplete={autoComplete} placeholder={placeholder} className="min-w-0 flex-1 bg-transparent text-sm text-slate-950 outline-none placeholder:text-slate-400" />
        {type === "password" && onTogglePassword ? (
          <button type="button" onClick={onTogglePassword} className="grid h-8 w-8 place-items-center rounded-full transition hover:bg-slate-100" aria-label={showPassword ? "Hide password" : "Show password"}>
            <EyeIcon hidden={Boolean(showPassword)} />
          </button>
        ) : null}
      </span>
    </label>
  );
}

export function LoginPage() {
  const [mode, setMode] = useState<AuthMode>("login");
  const [showPassword, setShowPassword] = useState(false);
  const [showConfirmPassword, setShowConfirmPassword] = useState(false);
  const isRegister = mode === "register";

  function switchMode(nextMode: AuthMode) {
    setMode(nextMode);
    setShowPassword(false);
    setShowConfirmPassword(false);
  }

  return (
    <section className="min-h-screen bg-[#fafafa] text-slate-950 lg:grid lg:h-screen lg:min-h-[680px] lg:grid-cols-[45%_55%] lg:overflow-hidden">
      <div className="hidden h-full overflow-hidden border-r border-slate-200 lg:block">
        <img src="/demo/login-page.png" alt="WorkspaceEcom office" className="h-full w-full object-fill" />
      </div>

      <div className="flex min-h-screen items-center justify-center px-5 py-6 sm:px-10 lg:min-h-0 lg:px-[4vw] lg:py-5">
        <div className="w-full max-w-[660px]">
          <div className="rounded-2xl border border-slate-200 bg-white px-6 py-6 shadow-[0_14px_45px_rgba(0,0,0,0.07)] sm:px-9 lg:px-10 lg:py-7">
            <div className="mb-6 grid grid-cols-2 border-b border-slate-200" role="tablist" aria-label="Account access">
              <button type="button" role="tab" aria-selected={!isRegister} onClick={() => switchMode("login")} className={`border-b-2 pb-3 text-sm font-semibold transition ${!isRegister ? "border-slate-950 text-slate-950" : "border-transparent text-slate-400 hover:text-slate-700"}`}>
                Sign in
              </button>
              <button type="button" role="tab" aria-selected={isRegister} onClick={() => switchMode("register")} className={`border-b-2 pb-3 text-sm font-semibold transition ${isRegister ? "border-slate-950 text-slate-950" : "border-transparent text-slate-400 hover:text-slate-700"}`}>
                Register
              </button>
            </div>

            <div>
              <h1 className="text-2xl font-bold tracking-[-0.03em] sm:text-3xl">{isRegister ? "Create your account" : "Sign in"}</h1>
              <p className="mt-1.5 text-sm text-slate-500">{isRegister ? "Join WorkspaceEcom and start building your ideal workspace." : "Welcome back to WorkspaceEcom."}</p>
            </div>

            <form className="mt-5" onSubmit={(event) => event.preventDefault()}>
              <div className="grid gap-3.5">
                {isRegister ? <TextField label="Full name" type="text" placeholder="Enter your full name" autoComplete="name" icon="user" /> : null}
                <TextField label="Email" type="email" placeholder="Enter your email" autoComplete="email" icon="mail" />
                <TextField label="Password" type="password" placeholder="Enter your password" autoComplete={isRegister ? "new-password" : "current-password"} icon="lock" showPassword={showPassword} onTogglePassword={() => setShowPassword((visible) => !visible)} />
                {isRegister ? <TextField label="Confirm password" type="password" placeholder="Confirm your password" autoComplete="new-password" icon="lock" showPassword={showConfirmPassword} onTogglePassword={() => setShowConfirmPassword((visible) => !visible)} /> : null}
              </div>

              {isRegister ? (
                <label className="mt-4 flex items-start gap-2.5 text-xs leading-5 text-slate-500">
                  <input type="checkbox" required className="mt-0.5 h-4 w-4 shrink-0 accent-slate-950" />
                  <span>I agree to the Terms of Service and Privacy Policy.</span>
                </label>
              ) : (
                <div className="mt-4 flex items-center justify-between gap-4 text-xs">
                  <label className="flex items-center gap-2 text-slate-600"><input type="checkbox" className="h-4 w-4 accent-slate-950" />Remember me</label>
                  <button type="button" className="font-medium underline underline-offset-4">Forgot password?</button>
                </div>
              )}

              <button type="submit" className="mt-5 h-12 w-full rounded-md bg-[#111111] text-sm font-semibold text-white transition hover:bg-slate-800 focus:outline-none focus:ring-2 focus:ring-slate-950 focus:ring-offset-2">
                {isRegister ? "Create account" : "Sign in"}
              </button>

              <div className="my-5 grid grid-cols-[1fr_auto_1fr] items-center gap-4 text-xs font-medium uppercase text-slate-400">
                <span className="h-px bg-slate-200" />Or<span className="h-px bg-slate-200" />
              </div>

              <button type="button" className="flex h-12 w-full items-center justify-center gap-3 rounded-md border border-slate-300 bg-white text-sm font-semibold transition hover:bg-slate-50">
                <GoogleIcon />
                {isRegister ? "Register with Google" : "Continue with Google"}
              </button>
            </form>

            <p className="mt-5 text-center text-xs text-slate-500">
              {isRegister ? "Already have an account?" : "Do not have an account?"}{" "}
              <button type="button" onClick={() => switchMode(isRegister ? "login" : "register")} className="font-semibold text-slate-950 underline underline-offset-4">
                {isRegister ? "Sign in" : "Create an account"}
              </button>
            </p>

            <p className="mt-5 text-center text-[11px] text-slate-400">&copy; 2026 WorkspaceEcom. All rights reserved.</p>
          </div>

          <Link to="/" className="mx-auto mt-4 block w-fit text-xs font-medium text-slate-500 transition hover:text-slate-950">Back to storefront</Link>
        </div>
      </div>
    </section>
  );
}
