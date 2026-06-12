import { ApiClient, createAdminApi } from "@workspace-ecommerce/api-client";
import type { AdminDashboardDto, AdminLoginResponse } from "@workspace-ecommerce/api-types";

const baseUrl = import.meta.env.VITE_API_BASE_URL ?? "";
const sessionKey = "workspace-ecommerce-admin-session";

export interface AdminSession {
  accessToken: string;
  tokenType: string;
  expiresAt: string;
  email: string;
}

let unauthorizedHandler: (() => void) | null = null;

function isSessionExpired(session: AdminSession): boolean {
  return Number.isNaN(Date.parse(session.expiresAt)) || new Date(session.expiresAt).getTime() <= Date.now();
}

function parseSession(value: string | null): AdminSession | null {
  if (!value) {
    return null;
  }

  try {
    const parsed = JSON.parse(value) as Partial<AdminSession>;
    if (!parsed.accessToken || !parsed.expiresAt || !parsed.email) {
      return null;
    }

    return {
      accessToken: parsed.accessToken,
      tokenType: parsed.tokenType ?? "Bearer",
      expiresAt: parsed.expiresAt,
      email: parsed.email
    };
  } catch {
    return null;
  }
}

export function getAdminSession(): AdminSession | null {
  const session = parseSession(localStorage.getItem(sessionKey));
  if (!session || isSessionExpired(session)) {
    clearAdminSession();
    return null;
  }

  return session;
}

export function saveAdminSession(response: AdminLoginResponse): AdminSession {
  const session: AdminSession = {
    accessToken: response.accessToken,
    tokenType: response.tokenType,
    expiresAt: response.expiresAt,
    email: response.email
  };

  localStorage.setItem(sessionKey, JSON.stringify(session));
  return session;
}

export function clearAdminSession(): void {
  localStorage.removeItem(sessionKey);
}

export function getAdminToken(): string | null {
  return getAdminSession()?.accessToken ?? null;
}

export function setAdminUnauthorizedHandler(handler: (() => void) | null): void {
  unauthorizedHandler = handler;
}

const api = createAdminApi(
  new ApiClient({
    baseUrl,
    getAccessToken: getAdminToken,
    onUnauthorized: () => unauthorizedHandler?.()
  })
);

function assertDashboardContract(value: AdminDashboardDto): AdminDashboardDto {
  if (
    typeof value.lowStockThreshold !== "number" ||
    !Array.isArray(value.lowStockVariants) ||
    !Array.isArray(value.orderStatusSummary) ||
    !Array.isArray(value.recentOrders)
  ) {
    throw new Error("Dashboard API response is outdated. Rebuild and restart the backend API.");
  }

  return value;
}

export const adminApi = {
  ...api,
  getDashboard: async () => assertDashboardContract(await api.getDashboard())
};
