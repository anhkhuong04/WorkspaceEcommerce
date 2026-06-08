import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { ConfigProvider } from "antd";
import { RouterProvider } from "react-router-dom";
import { AdminAuthProvider } from "../features/auth/AdminAuthProvider";
import { router } from "./router";

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 20_000,
      retry: 1
    }
  }
});

export function App() {
  return (
    <ConfigProvider
      theme={{
        token: {
          colorPrimary: "#0f766e",
          borderRadius: 14,
          fontFamily: "Plus Jakarta Sans, Aptos, sans-serif"
        }
      }}
    >
      <QueryClientProvider client={queryClient}>
        <AdminAuthProvider>
          <RouterProvider router={router} />
        </AdminAuthProvider>
      </QueryClientProvider>
    </ConfigProvider>
  );
}
