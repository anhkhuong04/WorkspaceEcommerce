import type { CustomerAuthResponse, CustomerProfileDto } from "@workspace-ecommerce/api-types";
import { afterEach, describe, expect, it } from "vitest";
import {
  clearCustomerSession,
  getCustomerSession,
  getCustomerToken,
  saveCustomerSession,
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
});

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
});
