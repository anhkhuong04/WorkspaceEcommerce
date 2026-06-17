import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { RouterProvider } from "react-router-dom";
import { CustomerAuthProvider } from "../features/customer-auth/CustomerAuthProvider";
import { router } from "./router";

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      retry: 1
    }
  }
});

export function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <CustomerAuthProvider>
        <RouterProvider router={router} />
      </CustomerAuthProvider>
    </QueryClientProvider>
  );
}
