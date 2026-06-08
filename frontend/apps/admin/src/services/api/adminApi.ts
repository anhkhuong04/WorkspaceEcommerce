import { ApiClient, createAdminApi } from "@workspace-ecommerce/api-client";

const baseUrl = import.meta.env.VITE_API_BASE_URL ?? "";
const tokenKey = "workspace-ecommerce-admin-token";

export function getAdminToken(): string | null {
  return localStorage.getItem(tokenKey);
}

export function setAdminToken(token: string): void {
  localStorage.setItem(tokenKey, token);
}

export function clearAdminToken(): void {
  localStorage.removeItem(tokenKey);
}

export const adminApi = createAdminApi(
  new ApiClient({
    baseUrl,
    getAccessToken: getAdminToken
  })
);
