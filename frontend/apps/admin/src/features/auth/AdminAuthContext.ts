import { createContext } from "react";
import type { AdminLoginResponse } from "@workspace-ecommerce/api-types";
import type { AdminSession } from "../../services/api/adminApi";

export interface AdminAuthContextValue {
  session: AdminSession | null;
  isAuthenticated: boolean;
  signIn: (response: AdminLoginResponse) => void;
  signOut: () => void;
}

export const AdminAuthContext = createContext<AdminAuthContextValue | null>(null);
