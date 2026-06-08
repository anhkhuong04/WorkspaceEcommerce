import { Button, Card, Form, Input, Typography } from "antd";
import { useNavigate } from "react-router-dom";
import { adminApi, setAdminToken } from "../../services/api/adminApi";

interface LoginFormValues {
  email: string;
  password: string;
}

export function LoginPage() {
  const navigate = useNavigate();

  async function handleFinish(values: LoginFormValues) {
    const response = await adminApi.login(values);
    setAdminToken(response.accessToken);
    navigate("/");
  }

  return (
    <main className="login-page">
      <Card className="login-card">
        <Typography.Text className="admin-eyebrow">Workspace Admin</Typography.Text>
        <Typography.Title level={2}>Sign in</Typography.Title>
        <Form layout="vertical" onFinish={handleFinish} initialValues={{ email: "admin@example.com", password: "" }}>
          <Form.Item label="Email" name="email" rules={[{ required: true, message: "Email is required." }]}>
            <Input size="large" />
          </Form.Item>
          <Form.Item label="Password" name="password" rules={[{ required: true, message: "Password is required." }]}>
            <Input.Password size="large" />
          </Form.Item>
          <Button type="primary" htmlType="submit" size="large" block>Login</Button>
        </Form>
      </Card>
    </main>
  );
}
