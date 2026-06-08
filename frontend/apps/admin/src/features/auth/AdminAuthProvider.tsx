import { useCallback, useEffect, useMemo, useState } from "react";
import type { ReactNode } from "react";
import {
  clearAdminSession,
  getAdminSession,
  saveAdminSession,
  setAdminUnauthorizedHandler
} from "../../services/api/adminApi";
import type { AdminSession } from "../../services/api/adminApi";
import { AdminAuthContext } from "./AdminAuthContext";
import type { AdminAuthContextValue } from "./AdminAuthContext";

export function AdminAuthProvider({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<AdminSession | null>(() => getAdminSession());

  const clearSession = useCallback(() => {
    clearAdminSession();
    setSession(null);
  }, []);

  useEffect(() => {
    setAdminUnauthorizedHandler(clearSession);
    return () => setAdminUnauthorizedHandler(null);
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

  const value = useMemo<AdminAuthContextValue>(
    () => ({
      session,
      isAuthenticated: session !== null,
      signIn: (response) => setSession(saveAdminSession(response)),
      signOut: clearSession
    }),
    [clearSession, session]
  );

  return <AdminAuthContext.Provider value={value}>{children}</AdminAuthContext.Provider>;
}
