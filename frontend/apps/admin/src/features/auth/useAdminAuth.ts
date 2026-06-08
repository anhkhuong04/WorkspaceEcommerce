import { useContext } from "react";
import { AdminAuthContext } from "./AdminAuthContext";
import type { AdminAuthContextValue } from "./AdminAuthContext";

export function useAdminAuth(): AdminAuthContextValue {
  const context = useContext(AdminAuthContext);
  if (!context) {
    throw new Error("useAdminAuth must be used inside AdminAuthProvider.");
  }

  return context;
}
