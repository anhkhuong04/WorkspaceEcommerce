import { zodResolver } from "@hookform/resolvers/zod";
import { Alert, Button, Card, Form, Input, Typography } from "antd";
import { Controller, useForm } from "react-hook-form";
import { Navigate, useLocation, useNavigate } from "react-router-dom";
import { z } from "zod";
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
    <main className="login-page">
      <Card className="login-card">
        <Typography.Text className="admin-eyebrow">Workspace Admin</Typography.Text>
        <Typography.Title level={2}>Sign in</Typography.Title>
        <Typography.Paragraph type="secondary">
          Use your admin credentials to access operations screens.
        </Typography.Paragraph>

        {errors.root?.message && (
          <Alert className="login-alert" type="error" showIcon title={errors.root.message} />
        )}

        <Form layout="vertical" onFinish={handleSubmit(handleLogin)} noValidate>
          <Form.Item
            label="Email"
            validateStatus={errors.email ? "error" : undefined}
            help={errors.email?.message}
          >
            <Controller
              name="email"
              control={control}
              render={({ field }) => (
                <Input
                  {...field}
                  size="large"
                  autoComplete="email"
                  inputMode="email"
                  disabled={isSubmitting}
                />
              )}
            />
          </Form.Item>

          <Form.Item
            label="Password"
            validateStatus={errors.password ? "error" : undefined}
            help={errors.password?.message}
          >
            <Controller
              name="password"
              control={control}
              render={({ field }) => (
                <Input.Password
                  {...field}
                  size="large"
                  autoComplete="current-password"
                  disabled={isSubmitting}
                />
              )}
            />
          </Form.Item>

          <Button type="primary" htmlType="submit" size="large" block loading={isSubmitting}>
            Login
          </Button>
        </Form>
      </Card>
    </main>
  );
}
