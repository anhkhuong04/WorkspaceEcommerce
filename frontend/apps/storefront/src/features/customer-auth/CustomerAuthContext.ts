import { createContext } from "react";
import type { CustomerAuthResponse, CustomerProfileDto } from "@workspace-ecommerce/api-types";
import type { CustomerSession } from "../../services/api/storefrontApi";

export interface CustomerAuthContextValue {
  session: CustomerSession | null;
  customer: CustomerProfileDto | null;
  isAuthenticated: boolean;
  signIn: (response: CustomerAuthResponse) => void;
  updateCustomer: (profile: CustomerProfileDto) => void;
  refreshCustomer: () => Promise<CustomerProfileDto | null>;
  signOut: () => void;
}

export const CustomerAuthContext = createContext<CustomerAuthContextValue | null>(null);
