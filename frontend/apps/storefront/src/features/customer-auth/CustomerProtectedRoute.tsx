import type { ReactNode } from "react";
import { Navigate, useLocation } from "react-router-dom";
import { useCustomerAuth } from "./useCustomerAuth";

export function CustomerProtectedRoute({ children }: { children: ReactNode }) {
  const location = useLocation();
  const { isAuthenticated } = useCustomerAuth();

  if (!isAuthenticated) {
    return <Navigate to="/login" replace state={{ from: `${location.pathname}${location.search}` }} />;
  }

  return <>{children}</>;
}
