import { zodResolver } from "@hookform/resolvers/zod";
import { Controller, useForm } from "react-hook-form";
import { Navigate, useLocation, useNavigate } from "react-router-dom";
import { z } from "zod";
import { Button, Field, Notice, TextInput } from "../../components/ui/AdminUi";
import { useAdminAuth } from "../../features/auth/useAdminAuth";
import { adminApi } from "../../services/api/adminApi";
import { getApiErrorMessage } from "../../services/api/errors";

const loginSchema = z.object({
  email: z.email("Enter a valid admin email.").max(256, "Email must be 256 characters or less."),
  password: z.string().min(1, "Password is required.").max(200, "Password must be 200 characters or less.")
});

type LoginFormValues = z.infer<typeof loginSchema>;

interface LoginLocationState {
  from?: string;
}

export function LoginPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const { isAuthenticated, signIn } = useAdminAuth();
  const redirectTo = (location.state as LoginLocationState | null)?.from ?? "/";

  const {
    control,
    handleSubmit,
    formState: { errors, isSubmitting },
    setError,
    clearErrors
  } = useForm<LoginFormValues>({
    resolver: zodResolver(loginSchema),
    defaultValues: {
      email: "admin@example.com",
      password: ""
    }
  });

  if (isAuthenticated) {
    return <Navigate to={redirectTo} replace />;
  }

  async function handleLogin(values: LoginFormValues) {
    clearErrors("root");

    try {
      const response = await adminApi.login(values);
      signIn(response);
      navigate(redirectTo, { replace: true });
    } catch (error) {
      setError("root", { message: getApiErrorMessage(error) });
    }
  }

  return (
    <main className="grid min-h-screen place-items-center bg-[radial-gradient(circle_at_top_left,#dff8ef,transparent_34%),linear-gradient(135deg,#f7fbf9,#dceee8)] p-4">
      <section className="w-full max-w-md rounded-[2rem] border border-white/70 bg-white p-8 shadow-2xl shadow-slate-900/10">
        <p className="text-xs font-black uppercase tracking-[0.18em] text-teal-700">Workspace Admin</p>
        <h1 className="mt-3 text-3xl font-black text-slate-950">Sign in</h1>
        <p className="mt-2 text-sm leading-6 text-slate-500">Use your admin credentials to access operations screens.</p>

        {errors.root?.message ? (
          <div className="mt-5">
            <Notice type="error" title={errors.root.message} />
          </div>
        ) : null}

        <form className="mt-6 grid gap-4" onSubmit={handleSubmit(handleLogin)} noValidate>
          <Controller
            name="email"
            control={control}
            render={({ field }) => (
              <Field label="Email" error={errors.email?.message}>
                <TextInput {...field} autoComplete="email" inputMode="email" disabled={isSubmitting} />
              </Field>
            )}
          />

          <Controller
            name="password"
            control={control}
            render={({ field }) => (
              <Field label="Password" error={errors.password?.message}>
                <TextInput {...field} type="password" autoComplete="current-password" disabled={isSubmitting} />
              </Field>
            )}
          />

          <Button type="submit" variant="primary" fullWidth disabled={isSubmitting}>
            {isSubmitting ? "Signing in..." : "Login"}
          </Button>
        </form>
      </section>
    </main>
  );
}
