import { act } from "react";
import { createRoot } from "react-dom/client";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { afterEach, expect, it, vi } from "vitest";
import { CustomerAuthProvider } from "./CustomerAuthProvider";
import { useCustomerAuth } from "./useCustomerAuth";
import type { CustomerAuthContextValue } from "./CustomerAuthContext";
import { clearCustomerSession, saveCustomerSession, storefrontApi } from "../../services/api/storefrontApi";
import type { CustomerAuthResponse, CustomerProfileDto } from "@workspace-ecommerce/api-types";

const response = (id: string): CustomerAuthResponse => ({ customerId: id, accessToken: `token-${id}`, tokenType: "Bearer",
  expiresAt: new Date(Date.now() + 300_000).toISOString(), fullName: id, email: `${id}@example.test`, phoneNumber: "0900000000", requiresTwoFactor: false, twoFactorChallengeToken: null });
const profile = (id: string): CustomerProfileDto => ({ id, fullName: id, email: `${id}@example.test`, phoneNumber: "0900000000", avatarUrl: null,
  isEmailVerified: true, rewardPoints: 0, twoFactorEnabled: false, createdAt: "", updatedAt: "" });

afterEach(() => { clearCustomerSession(); vi.restoreAllMocks(); });

it("removes private cache and cancels old in-flight queries when signing out and changing account", async () => {
  Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });
  saveCustomerSession(response("A"));
  vi.spyOn(storefrontApi, "getCustomerMe").mockResolvedValue(profile("A"));
  vi.spyOn(storefrontApi, "logoutCustomer").mockResolvedValue(undefined);
  const cache = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  cache.setQueryData(["customer", "orders", "all", "A"], ["private-order-A"]);
  cache.setQueryData(["payment-result", "A"], { private: "A" });
  cache.setQueryData(["storefront", "categories"], ["public"]);
  let finish: (value: string) => void = () => undefined;
  const inFlight = cache.fetchQuery({ queryKey: ["customer", "order", "pending", "A"], queryFn: () => new Promise<string>(resolve => { finish = resolve; }) }).catch(() => undefined);
  let auth: CustomerAuthContextValue | null = null;
  function Probe() { auth = useCustomerAuth(); return null; }
  const node = document.createElement("div"); document.body.append(node);
  const root = createRoot(node);
  try {
    await act(async () => { root.render(<QueryClientProvider client={cache}><CustomerAuthProvider><Probe /></CustomerAuthProvider></QueryClientProvider>); });
    await act(async () => { auth!.signOut(); auth!.signIn(response("B")); });
    finish("old private response"); await inFlight;
    expect(auth!.session?.customerId).toBe("B");
    expect(cache.getQueryData(["customer", "orders", "all", "A"])).toBeUndefined();
    expect(cache.getQueryData(["customer", "order", "pending", "A"])).toBeUndefined();
    expect(cache.getQueryData(["payment-result", "A"])).toBeUndefined();
    expect(cache.getQueryData(["storefront", "categories"])).toEqual(["public"]);
    // A late profile cannot overwrite B's identity.
    await act(async () => { auth!.updateCustomer(profile("A")); });
    expect(auth!.session?.customerId).toBe("B");
  } finally { await act(async () => root.unmount()); node.remove(); cache.clear(); }
});
