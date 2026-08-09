import { createStorefrontApi, ApiClient } from "@workspace-ecommerce/api-client";
import type { CustomerAuthResponse, CustomerProfileDto } from "@workspace-ecommerce/api-types";

const baseUrl = import.meta.env.VITE_API_BASE_URL ?? "";
const customerSessionKey = "workspace-ecommerce-customer-session";

export interface CustomerSession {
  accessToken: string;
  tokenType: string;
  expiresAt: string;
  customerId: string;
  email: string;
  fullName: string;
  phoneNumber: string;
}

let unauthorizedHandler: (() => void) | null = null;

function isSessionExpired(session: CustomerSession): boolean {
  return Number.isNaN(Date.parse(session.expiresAt)) || new Date(session.expiresAt).getTime() <= Date.now();
}

function parseCustomerSession(value: string | null): CustomerSession | null {
  if (!value) {
    return null;
  }

  try {
    const parsed = JSON.parse(value) as Partial<CustomerSession>;
    if (!parsed.accessToken || !parsed.expiresAt || !parsed.customerId || !parsed.email) {
      return null;
    }

    return {
      accessToken: parsed.accessToken,
      tokenType: parsed.tokenType ?? "Bearer",
      expiresAt: parsed.expiresAt,
      customerId: parsed.customerId,
      email: parsed.email,
      fullName: parsed.fullName ?? "",
      phoneNumber: parsed.phoneNumber ?? ""
    };
  } catch {
    return null;
  }
}

export function getCustomerSession(): CustomerSession | null {
  const session = parseCustomerSession(sessionStorage.getItem(customerSessionKey));
  if (!session || isSessionExpired(session)) {
    clearCustomerSession();
    return null;
  }

  return session;
}

export function saveCustomerSession(response: CustomerAuthResponse): CustomerSession {
  if (!response.accessToken || !response.expiresAt) {
    throw new Error("A completed customer authentication response is required to create a session.");
  }

  const session: CustomerSession = {
    accessToken: response.accessToken,
    tokenType: response.tokenType ?? "Bearer",
    expiresAt: response.expiresAt,
    customerId: response.customerId,
    email: response.email,
    fullName: response.fullName,
    phoneNumber: response.phoneNumber
  };

  // Only the short-lived access token is visible to JavaScript and it is
  // scoped to this browser tab. The long-lived refresh credential is an
  // HttpOnly, SameSite cookie and is never serialised here.
  sessionStorage.setItem(customerSessionKey, JSON.stringify(session));
  return session;
}

export function updateCustomerSessionProfile(profile: CustomerProfileDto): CustomerSession | null {
  const current = getCustomerSession();
  if (!current) {
    return null;
  }

  const nextSession: CustomerSession = {
    ...current,
    customerId: profile.id,
    email: profile.email,
    fullName: profile.fullName,
    phoneNumber: profile.phoneNumber ?? ""
  };

  sessionStorage.setItem(customerSessionKey, JSON.stringify(nextSession));
  return nextSession;
}

export function clearCustomerSession(): void {
  sessionStorage.removeItem(customerSessionKey);
}

export function getCustomerToken(): string | null {
  return getCustomerSession()?.accessToken ?? null;
}

export function setCustomerUnauthorizedHandler(handler: (() => void) | null): void {
  unauthorizedHandler = handler;
}

export const storefrontApi = createStorefrontApi(
  new ApiClient({
    baseUrl,
    getAccessToken: getCustomerToken,
    onUnauthorized: () => unauthorizedHandler?.()
  })
);
