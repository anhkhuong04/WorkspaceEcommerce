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
    avatarUrl: null,
    isEmailVerified: false,
    rewardPoints: 0,
    twoFactorEnabled: false,
    createdAt: "",
    updatedAt: ""
  };
}

export function CustomerAuthProvider({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<CustomerSession | null>(() => getCustomerSession());
  const [isReady, setIsReady] = useState(false);
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

  const renewSession = useCallback(async () => {
    const response = await storefrontApi.refreshCustomerSession();
    const nextSession = saveCustomerSession(response);
    setSession(nextSession);
    setCustomer(profileFromSession(nextSession));
    return nextSession;
  }, []);

  useEffect(() => {
    setCustomerUnauthorizedHandler(clearSession);
    return () => setCustomerUnauthorizedHandler(null);
  }, [clearSession]);

  useEffect(() => {
    let active = true;

    async function restoreSession() {
      try {
        let current = getCustomerSession();
        if (!current) {
          current = await renewSession();
        }

        if (!active) {
          return;
        }

        setSession(current);
        const profile = await storefrontApi.getCustomerMe();
        if (active) {
          updateCustomer(profile);
        }
      } catch {
        if (active) {
          clearSession();
        }
      } finally {
        if (active) {
          setIsReady(true);
        }
      }
    }

    void restoreSession();
    return () => {
      active = false;
    };
  }, [clearSession, renewSession, updateCustomer]);

  useEffect(() => {
    if (!session) {
      return;
    }

    const expiresAt = new Date(session.expiresAt).getTime();
    // Renew one minute before expiry. If renewal fails, the normal unauthorized
    // handler clears the short-lived in-tab access credential.
    const delay = Number.isNaN(expiresAt) ? 0 : Math.max(expiresAt - Date.now() - 60_000, 0);
    const timeoutId = window.setTimeout(() => {
      void renewSession().catch(() => clearSession());
    }, Math.min(delay, 2_147_483_647));

    return () => window.clearTimeout(timeoutId);
  }, [clearSession, renewSession, session]);

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
      isReady,
      signIn: (response) => {
        const nextSession = saveCustomerSession(response);
        setSession(nextSession);
        setCustomer(profileFromSession(nextSession));
      },
      updateCustomer,
      refreshCustomer,
      signOut: () => {
        // Server revocation is best-effort because local removal is still the
        // safe behaviour if the browser is offline.
        void storefrontApi.logoutCustomer().catch(() => undefined);
        clearSession();
      }
    }),
    [clearSession, customer, isReady, refreshCustomer, session, updateCustomer]
  );

  return <CustomerAuthContext.Provider value={value}>{children}</CustomerAuthContext.Provider>;
}
