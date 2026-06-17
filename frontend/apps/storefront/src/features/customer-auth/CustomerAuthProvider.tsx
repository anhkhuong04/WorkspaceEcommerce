import type { CustomerProfileDto } from "@workspace-ecommerce/api-types";
import type { ReactNode } from "react";
import { useCallback, useEffect, useMemo, useState } from "react";
import {
  clearCustomerSession,
  getCustomerSession,
  saveCustomerSession,
  setCustomerUnauthorizedHandler,
  storefrontApi,
  updateCustomerSessionProfile
} from "../../services/api/storefrontApi";
import type { CustomerSession } from "../../services/api/storefrontApi";
import { CustomerAuthContext } from "./CustomerAuthContext";
import type { CustomerAuthContextValue } from "./CustomerAuthContext";

function profileFromSession(session: CustomerSession): CustomerProfileDto {
  return {
    id: session.customerId,
    fullName: session.fullName,
    phoneNumber: session.phoneNumber,
    email: session.email,
    createdAt: "",
    updatedAt: ""
  };
}

export function CustomerAuthProvider({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<CustomerSession | null>(() => getCustomerSession());
  const [customer, setCustomer] = useState<CustomerProfileDto | null>(() => {
    const initialSession = getCustomerSession();
    return initialSession ? profileFromSession(initialSession) : null;
  });

  const clearSession = useCallback(() => {
    clearCustomerSession();
    setSession(null);
    setCustomer(null);
  }, []);

  const updateCustomer = useCallback((profile: CustomerProfileDto) => {
    const nextSession = updateCustomerSessionProfile(profile);
    if (nextSession) {
      setSession(nextSession);
    }
    setCustomer(profile);
  }, []);

  const refreshCustomer = useCallback(async () => {
    if (!getCustomerSession()) {
      clearSession();
      return null;
    }

    const profile = await storefrontApi.getCustomerMe();
    updateCustomer(profile);
    return profile;
  }, [clearSession, updateCustomer]);

  useEffect(() => {
    setCustomerUnauthorizedHandler(clearSession);
    return () => setCustomerUnauthorizedHandler(null);
  }, [clearSession]);

  useEffect(() => {
    if (!session) {
      return;
    }

    const expiresAt = new Date(session.expiresAt).getTime();
    const delay = Number.isNaN(expiresAt) ? 0 : Math.max(expiresAt - Date.now(), 0);
    const timeoutId = window.setTimeout(clearSession, Math.min(delay, 2_147_483_647));

    return () => window.clearTimeout(timeoutId);
  }, [clearSession, session]);

  useEffect(() => {
    if (!session) {
      return;
    }

    void refreshCustomer().catch(() => undefined);
  }, [refreshCustomer, session?.accessToken]);

  const value = useMemo<CustomerAuthContextValue>(
    () => ({
      session,
      customer,
      isAuthenticated: session !== null,
      signIn: (response) => {
        const nextSession = saveCustomerSession(response);
        setSession(nextSession);
        setCustomer(profileFromSession(nextSession));
      },
      updateCustomer,
      refreshCustomer,
      signOut: clearSession
    }),
    [clearSession, customer, refreshCustomer, session, updateCustomer]
  );

  return <CustomerAuthContext.Provider value={value}>{children}</CustomerAuthContext.Provider>;
}
