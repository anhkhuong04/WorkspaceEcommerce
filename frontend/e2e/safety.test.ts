import assert from "node:assert/strict";
import { afterEach, describe, it } from "node:test";

import { assertLoopbackHttpUrl, getLoopbackHttpUrl } from "./safety.ts";

const environmentVariable = "E2E_SAFETY_TEST_URL";
const originalEnvironmentValue = process.env[environmentVariable];

afterEach(() => {
  if (originalEnvironmentValue === undefined) {
    delete process.env[environmentVariable];
  } else {
    process.env[environmentVariable] = originalEnvironmentValue;
  }
});

describe("assertLoopbackHttpUrl", () => {
  it("accepts and normalizes local URLs", () => {
    const cases = [
      ["http://127.0.0.1:4173/", "http://127.0.0.1:4173"],
      ["http://localhost:4173/catalog/", "http://localhost:4173/catalog"],
      ["http://[::1]:4173/products/atlas-standing-desk", "http://[::1]:4173/products/atlas-standing-desk"],
      ["http://LOCALHOST:4173/search/?q=desk#results", "http://localhost:4173/search?q=desk#results"]
    ];

    for (const [value, expected] of cases) {
      assert.equal(assertLoopbackHttpUrl("E2E_STOREFRONT_URL", value), expected);
    }
  });

  it("rejects unsafe or invalid E2E targets", () => {
    const cases = [
      "https://127.0.0.1:4173",
      "http://example.test:4173",
      "http://127.0.0.2:4173",
      "http://user:password@127.0.0.1:4173",
      "http://localhost@127.0.0.1:4173",
      "http://127.0.0.1:4173 ",
      "not a URL"
    ];

    for (const value of cases) {
      assert.throws(() => assertLoopbackHttpUrl("E2E_STOREFRONT_URL", value), /E2E_STOREFRONT_URL/);
    }
  });

  it("validates an environment override instead of trusting it", () => {
    process.env[environmentVariable] = "https://storefront.example.com";

    assert.throws(
      () => getLoopbackHttpUrl(environmentVariable, "http://127.0.0.1:4173"),
      /http protocol/
    );
  });

  it("uses and validates the local fallback when no override is present", () => {
    delete process.env[environmentVariable];

    assert.equal(getLoopbackHttpUrl(environmentVariable, "http://localhost:4173/"), "http://localhost:4173");
  });
});
