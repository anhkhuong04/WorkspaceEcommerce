import { useTranslation } from "react-i18next";
import storefrontI18n from "../../i18n";
import { zodResolver } from "@hookform/resolvers/zod";
import { GoogleLogin } from "@react-oauth/google";
import { useState, type FormEvent } from "react";
import { useForm } from "react-hook-form";
import { Link, Navigate, useLocation, useNavigate } from "react-router-dom";
import { z } from "zod";
import type { CustomerAuthResponse } from "@workspace-ecommerce/api-types";
import { useCustomerAuth } from "../../features/customer-auth/useCustomerAuth";
import { getApiErrorMessage } from "../../services/api/errors";
import { storefrontApi } from "../../services/api/storefrontApi";

type AuthMode = "login" | "register";

const authSchema = () => z
  .object({
    fullName: z.string().optional(),
    phoneNumber: z.string().optional(),
    email: z.string().email(storefrontI18n.t("uiCopy.enter_a_valid_email")).max(250, storefrontI18n.t("uiCopy.email_is_too_long")),
    password: z.string().min(8, storefrontI18n.t("uiCopy.password_must_be_at_least_8_characters")).max(100, storefrontI18n.t("uiCopy.password_is_too_long")),
    confirmPassword: z.string().optional()
  })
  .superRefine((value, context) => {
    if (value.fullName !== undefined && value.fullName.trim().length === 0) {
      context.addIssue({ code: "custom", path: ["fullName"], message: storefrontI18n.t("uiCopy.full_name_is_required") });
    }

    if (value.phoneNumber !== undefined && value.phoneNumber.trim().length < 8) {
      context.addIssue({ code: "custom", path: ["phoneNumber"], message: storefrontI18n.t("uiCopy.phone_number_is_invalid") });
    }

    if (value.confirmPassword !== undefined && value.password !== value.confirmPassword) {
      context.addIssue({ code: "custom", path: ["confirmPassword"], message: storefrontI18n.t("uiCopy.passwords_do_not_match") });
    }
  });

type AuthFormValues = z.infer<ReturnType<typeof authSchema>>;

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
      <span className={`flex h-12 items-center gap-3 rounded-md border px-4 text-slate-600 transition focus-within:ring-1 ${error ? "border-red-400 bg-red-50 focus-within:ring-red-300" : "border-slate-300 focus-within:border-slate-950 focus-within:ring-slate-950"}`}>
        {icon === "user" ? <UserIcon /> : icon === "mail" ? <MailIcon /> : icon === "phone" ? <PhoneIcon /> : <LockIcon />}
        <input
          aria-invalid={Boolean(error)}
          aria-describedby={error ? `auth-${registration.name}-error` : undefined}
          type={resolvedType}
          autoComplete={autoComplete}
          placeholder={placeholder}
          className="min-w-0 flex-1 bg-transparent text-sm text-slate-950 outline-none placeholder:text-slate-600"
          {...registration}
        />
        {type === "password" && onTogglePassword ? (
          <button type="button" onClick={onTogglePassword} className="grid h-8 w-8 place-items-center rounded-full transition hover:bg-slate-100" aria-label={showPassword ? storefrontI18n.t("uiCopy.hide_password") : storefrontI18n.t("uiCopy.show_password")}>
            <EyeIcon hidden={Boolean(showPassword)} />
          </button>
        ) : null}
      </span>
      {error ? <span id={`auth-${registration.name}-error`} role="alert" className="mt-1.5 block text-xs font-medium text-red-700">{error}</span> : null}
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
  useTranslation();
  const [mode, setMode] = useState<AuthMode>("login");
  const [showPassword, setShowPassword] = useState(false);
  const [showConfirmPassword, setShowConfirmPassword] = useState(false);
  const [apiError, setApiError] = useState<string | null>(null);
  const [twoFactorChallengeToken, setTwoFactorChallengeToken] = useState<string | null>(null);
  const [twoFactorCode, setTwoFactorCode] = useState("");
  const [useRecoveryCode, setUseRecoveryCode] = useState(false);
  const [isCompletingTwoFactor, setIsCompletingTwoFactor] = useState(false);
  const { isAuthenticated, signIn } = useCustomerAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const redirectPath = getRedirectPath(location.state);
  const isRegister = mode === "register";
  const googleLoginEnabled = Boolean(import.meta.env.VITE_GOOGLE_CLIENT_ID);

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting }
  } = useForm<AuthFormValues>({
    resolver: zodResolver(authSchema()),
    defaultValues: {
      fullName: undefined,
      phoneNumber: undefined,
      email: "",
      password: "",
      confirmPassword: undefined
    }
  });

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

      completePrimaryAuthentication(response);
    } catch (error) {
      setApiError(getApiErrorMessage(error));
    }
  }

  async function handleGoogleSuccess(credential: string) {
    setApiError(null);
    try {
      const response = await storefrontApi.loginWithGoogle({
        idToken: credential
      });
      completePrimaryAuthentication(response);
    } catch (error) {
      setApiError(getApiErrorMessage(error));
    }
  }

  function switchMode(nextMode: AuthMode) {
    if (nextMode === mode) {
      return;
    }

    reset({
      fullName: nextMode === "register" ? "" : undefined,
      phoneNumber: nextMode === "register" ? "" : undefined,
      email: "",
      password: "",
      confirmPassword: nextMode === "register" ? "" : undefined
    });
    setApiError(null);
    setShowPassword(false);
    setShowConfirmPassword(false);
    setMode(nextMode);
  }

  function completePrimaryAuthentication(response: CustomerAuthResponse) {
    if (response.requiresTwoFactor && response.twoFactorChallengeToken) {
      setTwoFactorChallengeToken(response.twoFactorChallengeToken);
      setTwoFactorCode("");
      setUseRecoveryCode(false);
      return;
    }

    if (!response.accessToken || !response.expiresAt) {
      setApiError(storefrontI18n.t("uiCopy.authentication_could_not_be_completed_please_try_again"));
      return;
    }

    signIn(response);
    navigate(redirectPath, { replace: true });
  }

  async function submitTwoFactor(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!twoFactorChallengeToken || !twoFactorCode.trim()) {
      return;
    }

    setApiError(null);
    setIsCompletingTwoFactor(true);
    try {
      const response = useRecoveryCode
        ? await storefrontApi.verifyTwoFactorRecovery({
            challengeToken: twoFactorChallengeToken,
            recoveryCode: twoFactorCode.trim()
          })
        : await storefrontApi.verifyTwoFactorLogin({
            challengeToken: twoFactorChallengeToken,
            code: twoFactorCode.trim()
          });
      completePrimaryAuthentication(response);
    } catch (error) {
      setApiError(getApiErrorMessage(error));
    } finally {
      setIsCompletingTwoFactor(false);
    }
  }

  if (isAuthenticated) {
    return <Navigate to={redirectPath} replace />;
  }

  return (
    <section
      className="relative min-h-screen bg-[#fafafa] bg-cover bg-center bg-no-repeat text-slate-950"
      style={{ backgroundImage: "url('/demo/bg-login.png')" }}
    >
      <div className="absolute inset-0 bg-black/20"></div>
      <div className="relative z-10 flex min-h-screen items-center justify-center lg:justify-end px-5 py-6 sm:px-10 lg:px-[10vw] xl:px-[15vw] lg:py-8">
        <div className="w-full max-w-[500px]">
          <div className="rounded-2xl border border-white/30 bg-white/95 px-6 py-6 shadow-2xl backdrop-blur-sm sm:px-9 lg:px-10 lg:py-7">
            <div>
              <h1 className="text-2xl font-bold sm:text-3xl">{isRegister ? storefrontI18n.t("uiCopy.create_your_account") : storefrontI18n.t("uiCopy.sign_in")}</h1>
              <p className="mt-1.5 text-sm text-slate-600">{isRegister ? storefrontI18n.t("uiCopy.save_your_details_and_track_every_order_from_one_place") : storefrontI18n.t("uiCopy.view_your_orders_and_continue_checkout_faster")}</p>
            </div>

            {twoFactorChallengeToken ? (
              <form className="mt-5" onSubmit={submitTwoFactor}>
                <p className="text-sm text-slate-600">{storefrontI18n.t("uiCopy.enter_the_code_from_your_authenticator_app_or_use_one_of_your_saved_recovery_codes")}</p>
                <label className="mt-4 block">
                  <span className="mb-1.5 block text-sm font-semibold">{useRecoveryCode ? storefrontI18n.t("uiCopy.recovery_code") : storefrontI18n.t("uiCopy.authentication_code")}</span>
                  <input
                    value={twoFactorCode}
                    onChange={(event) => setTwoFactorCode(event.target.value)}
                    inputMode={useRecoveryCode ? "text" : "numeric"}
                    autoComplete="one-time-code"
                    maxLength={useRecoveryCode ? 128 : 6}
                    className="h-12 w-full rounded-md border border-slate-300 px-4 text-sm text-slate-950 outline-none transition focus:border-slate-950 focus:ring-1 focus:ring-slate-950"
                  />
                </label>
                {apiError ? <div className="mt-4 rounded-md bg-red-50 px-4 py-3 text-sm font-medium text-red-700">{apiError}</div> : null}
                <button type="submit" disabled={isCompletingTwoFactor} className="mt-5 h-12 w-full rounded-md bg-[#111111] text-sm font-semibold text-white transition hover:bg-slate-800 focus:outline-none focus:ring-2 focus:ring-slate-950 focus:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-60">
                  {isCompletingTwoFactor ? "Verifying..." : storefrontI18n.t("uiCopy.complete_sign_in")}
                </button>
                <button type="button" onClick={() => { setUseRecoveryCode((value) => !value); setTwoFactorCode(""); setApiError(null); }} className="mt-4 w-full text-sm font-semibold text-slate-700 underline underline-offset-4">
                  {useRecoveryCode ? storefrontI18n.t("uiCopy.use_authenticator_code") : storefrontI18n.t("uiCopy.use_a_recovery_code")}
                </button>
              </form>
            ) : (
              <>
                <form className="mt-5" onSubmit={handleSubmit(onSubmit)}>
                  <div className="grid gap-3.5">
                    {isRegister ? (
                      <>
                        <TextField label={storefrontI18n.t("uiCopy.full_name")} type="text" placeholder={storefrontI18n.t("uiCopy.enter_your_full_name")} autoComplete="name" icon="user" error={errors.fullName?.message} registration={register("fullName")} />
                        <TextField label={storefrontI18n.t("uiCopy.phone_number")} type="tel" placeholder={storefrontI18n.t("uiCopy.enter_your_phone_number")} autoComplete="tel" icon="phone" error={errors.phoneNumber?.message} registration={register("phoneNumber")} />
                      </>
                    ) : null}
                    <TextField label={storefrontI18n.t("uiCopy.email")} type="email" placeholder={storefrontI18n.t("uiCopy.enter_your_email")} autoComplete="email" icon="mail" error={errors.email?.message} registration={register("email")} />
                    <div>
                      <TextField label={storefrontI18n.t("uiCopy.password")} type="password" placeholder={storefrontI18n.t("uiCopy.enter_your_password")} autoComplete={isRegister ? "new-password" : "current-password"} icon="lock" error={errors.password?.message} showPassword={showPassword} onTogglePassword={() => setShowPassword((visible) => !visible)} registration={register("password")} />
                      {!isRegister ? (
                        <Link to="/reset-password" className="mt-2 block text-left text-xs font-semibold text-slate-600 underline underline-offset-4 hover:text-slate-900">{storefrontI18n.t("uiCopy.forgot_your_password")}</Link>
                      ) : null}
                    </div>
                    {isRegister ? (
                      <TextField label={storefrontI18n.t("uiCopy.confirm_password")} type="password" placeholder={storefrontI18n.t("uiCopy.confirm_your_password")} autoComplete="new-password" icon="lock" error={errors.confirmPassword?.message} showPassword={showConfirmPassword} onTogglePassword={() => setShowConfirmPassword((visible) => !visible)} registration={register("confirmPassword")} />
                    ) : null}
                  </div>

                  {apiError ? <div className="mt-4 rounded-md bg-red-50 px-4 py-3 text-sm font-medium text-red-700">{apiError}</div> : null}

                  <button type="submit" disabled={isSubmitting} className="mt-5 h-12 w-full rounded-md bg-[#111111] text-sm font-semibold text-white transition hover:bg-slate-800 focus:outline-none focus:ring-2 focus:ring-slate-950 focus:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-60">
                    {isSubmitting ? storefrontI18n.t("uiCopy.please_wait") : isRegister ? storefrontI18n.t("uiCopy.create_account") : storefrontI18n.t("uiCopy.sign_in")}
                  </button>
                </form>

                {googleLoginEnabled ? (
                  <>
                    <div className="my-5 flex items-center gap-3">
                      <div className="h-px flex-1 bg-slate-200" />
                      <span className="text-xs font-medium text-slate-600">{storefrontI18n.t("uiCopy.or_continue_with")}</span>
                      <div className="h-px flex-1 bg-slate-200" />
                    </div>

                    <div className="flex justify-center">
                      <GoogleLogin
                        onSuccess={(response) => {
                          if (response.credential) void handleGoogleSuccess(response.credential);
                        }}
                        onError={() => setApiError(storefrontI18n.t("uiCopy.google_sign_in_failed_please_try_again"))}
                        text={isRegister ? "signup_with" : "signin_with"}
                        shape="rectangular"
                        theme="outline"
                        size="large"
                        width="400"
                      />
                    </div>
                  </>
                ) : null}

                <p className="mt-5 text-center text-xs text-slate-600">
                  {isRegister ? storefrontI18n.t("uiCopy.already_have_an_account") : storefrontI18n.t("uiCopy.do_not_have_an_account")}{" "}
                  <button type="button" onClick={() => switchMode(isRegister ? "login" : "register")} className="font-semibold text-slate-950 underline underline-offset-4">
                    {isRegister ? storefrontI18n.t("uiCopy.sign_in") : storefrontI18n.t("uiCopy.create_an_account")}
                  </button>
                </p>
              </>
            )}

            <p className="mt-5 text-center text-[11px] text-slate-600">&copy; 2026 WorkspaceEcom. All rights reserved.</p>
          </div>

          <Link to="/" className="mx-auto mt-6 block w-fit text-sm font-medium text-white/80 transition hover:text-white drop-shadow-md">{storefrontI18n.t("uiCopy.back_to_storefront")}</Link>
        </div>
      </div>
    </section>
  );
}
