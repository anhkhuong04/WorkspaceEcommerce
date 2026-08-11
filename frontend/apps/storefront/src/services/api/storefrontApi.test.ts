import type { CustomerAuthResponse, CustomerProfileDto } from "@workspace-ecommerce/api-types";
import { afterEach, describe, expect, it, vi } from "vitest";
import {
  clearCustomerSession,
  getCustomerSession,
  getCustomerToken,
  saveCustomerSession,
  setCustomerUnauthorizedHandler,
  storefrontApi,
  updateCustomerSessionProfile
} from "./storefrontApi";

const customerSessionKey = "workspace-ecommerce-customer-session";

function completedAuthentication(overrides: Partial<CustomerAuthResponse> = {}): CustomerAuthResponse {
  return {
    accessToken: "short-lived-access-token",
    tokenType: "Bearer",
    expiresAt: new Date(Date.now() + 5 * 60_000).toISOString(),
    customerId: "customer-123",
    email: "customer@example.com",
    fullName: "Original Customer",
    phoneNumber: "0900000000",
    requiresTwoFactor: false,
    twoFactorChallengeToken: null,
    ...overrides
  };
}

afterEach(() => {
  clearCustomerSession();
  setCustomerUnauthorizedHandler(null);
  vi.unstubAllGlobals();
});

function successfulEnvelope<T>(data: T): Response {
  return new Response(JSON.stringify({ success: true, data, errors: [], traceId: "trace-test" }), {
    status: 200,
    headers: { "Content-Type": "application/json" }
  });
}

function failedEnvelope(status: number, errors: string[]): Response {
  return new Response(JSON.stringify({ success: false, data: null, errors, traceId: "trace-test" }), {
    status,
    headers: { "Content-Type": "application/json" }
  });
}

function getRequestPath(path: string): string {
  return new URL(path, "https://storefront.example.test").pathname;
}

describe("customer session storage", () => {
  it("persists only the completed, short-lived customer session", () => {
    const session = saveCustomerSession(completedAuthentication());

    expect(session).toEqual({
      accessToken: "short-lived-access-token",
      tokenType: "Bearer",
      expiresAt: expect.any(String),
      customerId: "customer-123",
      email: "customer@example.com",
      fullName: "Original Customer",
      phoneNumber: "0900000000"
    });
    expect(JSON.parse(sessionStorage.getItem(customerSessionKey) ?? "{}")).toEqual(session);
    expect(getCustomerToken()).toBe("short-lived-access-token");
  });

  it("removes malformed and expired credentials before they can be reused", () => {
    sessionStorage.setItem(customerSessionKey, "not-json");
    expect(getCustomerSession()).toBeNull();
    expect(sessionStorage.getItem(customerSessionKey)).toBeNull();

    sessionStorage.setItem(
      customerSessionKey,
      JSON.stringify({
        ...completedAuthentication(),
        expiresAt: new Date(Date.now() - 1_000).toISOString()
      })
    );

    expect(getCustomerSession()).toBeNull();
    expect(getCustomerToken()).toBeNull();
    expect(sessionStorage.getItem(customerSessionKey)).toBeNull();
  });

  it("rejects incomplete authentication responses without creating a session", () => {
    expect(() => saveCustomerSession(completedAuthentication({ accessToken: null }))).toThrow(
      "A completed customer authentication response is required"
    );
    expect(getCustomerSession()).toBeNull();
  });

  it("updates the profile without replacing the active access credential", () => {
    const original = saveCustomerSession(completedAuthentication());
    const profile: CustomerProfileDto = {
      id: "customer-456",
      fullName: "Updated Customer",
      phoneNumber: null,
      email: "updated@example.com",
      avatarUrl: null,
      isEmailVerified: true,
      rewardPoints: 20,
      twoFactorEnabled: true,
      createdAt: "2026-01-01T00:00:00.000Z",
      updatedAt: "2026-01-02T00:00:00.000Z"
    };

    expect(updateCustomerSessionProfile(profile)).toEqual({
      ...original,
      customerId: "customer-456",
      email: "updated@example.com",
      fullName: "Updated Customer",
      phoneNumber: ""
    });
    expect(getCustomerToken()).toBe("short-lived-access-token");
  });

  it("sends a 2FA challenge through the credentialed API client with only the short-lived bearer", async () => {
    saveCustomerSession(completedAuthentication());
    const fetchMock = vi.fn().mockResolvedValue(successfulEnvelope(completedAuthentication({
      fullName: "Two-factor customer"
    })));
    vi.stubGlobal("fetch", fetchMock);

    await storefrontApi.verifyTwoFactorLogin({ challengeToken: "challenge-token", code: "123456" });

    expect(fetchMock).toHaveBeenCalledOnce();
    const [path, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(getRequestPath(path)).toBe("/api/customer/auth/2fa/verify");
    expect(init).toMatchObject({ method: "POST", credentials: "include" });
    expect(new Headers(init.headers).get("Authorization")).toBe("Bearer short-lived-access-token");
    expect(new Headers(init.headers).get("Content-Type")).toBe("application/json");
    expect(init.body).toBe(JSON.stringify({ challengeToken: "challenge-token", code: "123456" }));
  });

  it("routes email verification and password recovery through distinct POST contracts", async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(successfulEnvelope(null))
      .mockResolvedValueOnce(successfulEnvelope(null))
      .mockResolvedValueOnce(successfulEnvelope(null));
    vi.stubGlobal("fetch", fetchMock);

    await storefrontApi.requestEmailVerification({ email: "customer@example.com" });
    await storefrontApi.forgotCustomerPassword({ email: "customer@example.com" });
    await storefrontApi.resetCustomerPassword({ token: "single-use-token", newPassword: "New-password-123!" });

    expect(fetchMock.mock.calls.map(([path]) => getRequestPath(path as string))).toEqual([
      "/api/customer/auth/email-verification/request",
      "/api/customer/auth/password/forgot",
      "/api/customer/auth/password/reset"
    ]);
    expect(fetchMock.mock.calls.map(([, init]) => (init as RequestInit).method)).toEqual(["POST", "POST", "POST"]);
  });

  it("surfaces a coupon validation conflict without treating it as an authentication failure", async () => {
    const fetchMock = vi.fn().mockResolvedValue(failedEnvelope(409, ["Coupon is expired."]));
    vi.stubGlobal("fetch", fetchMock);
    const unauthorizedHandler = vi.fn();
    setCustomerUnauthorizedHandler(unauthorizedHandler);

    await expect(storefrontApi.validateCheckoutCoupon({ sessionId: "cart-session", couponCode: "EXPIRED" }))
      .rejects
      .toMatchObject({
        name: "ApiClientError",
        statusCode: 409,
        errors: ["Coupon is expired."]
      });

    expect(unauthorizedHandler).not.toHaveBeenCalled();
  });

  it("submits blog comments through the moderation acknowledgement endpoint", async () => {
    const fetchMock = vi.fn().mockResolvedValue(successfulEnvelope({ message: "Comment submitted for moderation." }));
    vi.stubGlobal("fetch", fetchMock);

    const acknowledgement = await storefrontApi.submitBlogComment("release-update", {
      authorName: "Synthetic commenter",
      authorEmail: "synthetic-commenter@example.test",
      content: "Please review this update."
    });

    expect(acknowledgement.message).toContain("moderation");
    const [path, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(getRequestPath(path)).toBe("/api/blog-posts/release-update/comments");
    expect(init.body).toBe(JSON.stringify({
      authorName: "Synthetic commenter",
      authorEmail: "synthetic-commenter@example.test",
      content: "Please review this update."
    }));
  });
});
