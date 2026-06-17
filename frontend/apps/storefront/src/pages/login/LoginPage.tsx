import { zodResolver } from "@hookform/resolvers/zod";
import { useEffect, useState } from "react";
import { useForm } from "react-hook-form";
import { Link, Navigate, useLocation, useNavigate } from "react-router-dom";
import { z } from "zod";
import { useCustomerAuth } from "../../features/customer-auth/useCustomerAuth";
import { getApiErrorMessage } from "../../services/api/errors";
import { storefrontApi } from "../../services/api/storefrontApi";

type AuthMode = "login" | "register";

const authSchema = z
  .object({
    fullName: z.string().optional(),
    phoneNumber: z.string().optional(),
    email: z.string().email("Enter a valid email.").max(250, "Email is too long."),
    password: z.string().min(8, "Password must be at least 8 characters.").max(100, "Password is too long."),
    confirmPassword: z.string().optional()
  })
  .superRefine((value, context) => {
    if (value.fullName !== undefined && value.fullName.trim().length === 0) {
      context.addIssue({ code: "custom", path: ["fullName"], message: "Full name is required." });
    }

    if (value.phoneNumber !== undefined && value.phoneNumber.trim().length < 8) {
      context.addIssue({ code: "custom", path: ["phoneNumber"], message: "Phone number is invalid." });
    }

    if (value.confirmPassword !== undefined && value.password !== value.confirmPassword) {
      context.addIssue({ code: "custom", path: ["confirmPassword"], message: "Passwords do not match." });
    }
  });

type AuthFormValues = z.infer<typeof authSchema>;

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

function PhoneIcon() {
  return (
    <svg className="h-5 w-5" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.7" aria-hidden="true">
      <path d="M7 4h3l1.5 4-2 1.2a11 11 0 0 0 5.3 5.3l1.2-2 4 1.5v3a2 2 0 0 1-2.2 2A16 16 0 0 1 5 6.2 2 2 0 0 1 7 4Z" strokeLinecap="round" strokeLinejoin="round" />
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

interface TextFieldProps {
  label: string;
  type: "text" | "email" | "password" | "tel";
  placeholder: string;
  autoComplete: string;
  icon: "user" | "mail" | "lock" | "phone";
  error?: string;
  showPassword?: boolean;
  onTogglePassword?: () => void;
  registration: ReturnType<typeof useForm<AuthFormValues>>["register"] extends (name: infer T) => infer R ? R : never;
}

function TextField({ label, type, placeholder, autoComplete, icon, error, showPassword, onTogglePassword, registration }: TextFieldProps) {
  const resolvedType = type === "password" && showPassword ? "text" : type;

  return (
    <label className="block">
      <span className="mb-1.5 block text-sm font-semibold">{label}</span>
      <span className={`flex h-12 items-center gap-3 rounded-md border px-4 text-slate-400 transition focus-within:ring-1 ${error ? "border-red-400 bg-red-50 focus-within:ring-red-300" : "border-slate-300 focus-within:border-slate-950 focus-within:ring-slate-950"}`}>
        {icon === "user" ? <UserIcon /> : icon === "mail" ? <MailIcon /> : icon === "phone" ? <PhoneIcon /> : <LockIcon />}
        <input
          type={resolvedType}
          autoComplete={autoComplete}
          placeholder={placeholder}
          className="min-w-0 flex-1 bg-transparent text-sm text-slate-950 outline-none placeholder:text-slate-400"
          {...registration}
        />
        {type === "password" && onTogglePassword ? (
          <button type="button" onClick={onTogglePassword} className="grid h-8 w-8 place-items-center rounded-full transition hover:bg-slate-100" aria-label={showPassword ? "Hide password" : "Show password"}>
            <EyeIcon hidden={Boolean(showPassword)} />
          </button>
        ) : null}
      </span>
      {error ? <span className="mt-1.5 block text-xs font-medium text-red-600">{error}</span> : null}
    </label>
  );
}

function getRedirectPath(state: unknown): string {
  if (state && typeof state === "object" && "from" in state && typeof state.from === "string") {
    return state.from;
  }

  return "/account";
}

export function LoginPage() {
  const [mode, setMode] = useState<AuthMode>("login");
  const [showPassword, setShowPassword] = useState(false);
  const [showConfirmPassword, setShowConfirmPassword] = useState(false);
  const [apiError, setApiError] = useState<string | null>(null);
  const { isAuthenticated, signIn } = useCustomerAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const redirectPath = getRedirectPath(location.state);
  const isRegister = mode === "register";

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting }
  } = useForm<AuthFormValues>({
    resolver: zodResolver(authSchema),
    defaultValues: {
      fullName: undefined,
      phoneNumber: undefined,
      email: "",
      password: "",
      confirmPassword: undefined
    }
  });

  useEffect(() => {
    reset({
      fullName: isRegister ? "" : undefined,
      phoneNumber: isRegister ? "" : undefined,
      email: "",
      password: "",
      confirmPassword: isRegister ? "" : undefined
    });
    setApiError(null);
    setShowPassword(false);
    setShowConfirmPassword(false);
  }, [isRegister, reset]);

  async function onSubmit(values: AuthFormValues) {
    setApiError(null);

    try {
      const response = isRegister
        ? await storefrontApi.registerCustomer({
            fullName: values.fullName?.trim() ?? "",
            phoneNumber: values.phoneNumber?.trim() ?? "",
            email: values.email.trim(),
            password: values.password
          })
        : await storefrontApi.loginCustomer({
            email: values.email.trim(),
            password: values.password
          });

      signIn(response);
      navigate(redirectPath, { replace: true });
    } catch (error) {
      setApiError(getApiErrorMessage(error));
    }
  }

  function switchMode(nextMode: AuthMode) {
    setMode(nextMode);
  }

  if (isAuthenticated) {
    return <Navigate to={redirectPath} replace />;
  }

  return (
    <section className="min-h-screen bg-[#fafafa] text-slate-950 lg:grid lg:h-screen lg:min-h-[720px] lg:grid-cols-[45%_55%] lg:overflow-hidden">
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
              <h1 className="text-2xl font-bold sm:text-3xl">{isRegister ? "Create your account" : "Sign in"}</h1>
              <p className="mt-1.5 text-sm text-slate-500">{isRegister ? "Save your details and track every order from one place." : "View your orders and continue checkout faster."}</p>
            </div>

            <form className="mt-5" onSubmit={handleSubmit(onSubmit)}>
              <div className="grid gap-3.5">
                {isRegister ? (
                  <>
                    <TextField label="Full name" type="text" placeholder="Enter your full name" autoComplete="name" icon="user" error={errors.fullName?.message} registration={register("fullName")} />
                    <TextField label="Phone number" type="tel" placeholder="Enter your phone number" autoComplete="tel" icon="phone" error={errors.phoneNumber?.message} registration={register("phoneNumber")} />
                  </>
                ) : null}
                <TextField label="Email" type="email" placeholder="Enter your email" autoComplete="email" icon="mail" error={errors.email?.message} registration={register("email")} />
                <TextField label="Password" type="password" placeholder="Enter your password" autoComplete={isRegister ? "new-password" : "current-password"} icon="lock" error={errors.password?.message} showPassword={showPassword} onTogglePassword={() => setShowPassword((visible) => !visible)} registration={register("password")} />
                {isRegister ? (
                  <TextField label="Confirm password" type="password" placeholder="Confirm your password" autoComplete="new-password" icon="lock" error={errors.confirmPassword?.message} showPassword={showConfirmPassword} onTogglePassword={() => setShowConfirmPassword((visible) => !visible)} registration={register("confirmPassword")} />
                ) : null}
              </div>

              {apiError ? <div className="mt-4 rounded-md bg-red-50 px-4 py-3 text-sm font-medium text-red-700">{apiError}</div> : null}

              <button type="submit" disabled={isSubmitting} className="mt-5 h-12 w-full rounded-md bg-[#111111] text-sm font-semibold text-white transition hover:bg-slate-800 focus:outline-none focus:ring-2 focus:ring-slate-950 focus:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-60">
                {isSubmitting ? "Please wait..." : isRegister ? "Create account" : "Sign in"}
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
