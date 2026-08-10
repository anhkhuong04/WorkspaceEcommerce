const loopbackHosts = new Set(["127.0.0.1", "localhost", "::1"]);

/**
 * Validates a browser-E2E endpoint before it is used by a local test runner.
 *
 * This deliberately permits only HTTP endpoints on explicit loopback hostnames.
 * It does not resolve arbitrary hostnames, so a typo or a production URL cannot
 * silently become an E2E target.
 */
export function assertLoopbackHttpUrl(variableName: string, value: string): string {
  if (typeof value !== "string" || value.length === 0 || value.trim() !== value) {
    throw new Error(`${variableName} must be a non-empty absolute HTTP URL without surrounding whitespace.`);
  }

  let url: URL;

  try {
    url = new URL(value);
  } catch {
    throw new Error(`${variableName} must be a valid absolute HTTP URL.`);
  }

  if (url.protocol !== "http:") {
    throw new Error(`${variableName} must use the http protocol for isolated local E2E tests.`);
  }

  if (url.username.length > 0 || url.password.length > 0) {
    throw new Error(`${variableName} must not include URL credentials.`);
  }

  const hostname = url.hostname.toLowerCase().replace(/^\[|\]$/g, "");
  if (!loopbackHosts.has(hostname)) {
    throw new Error(`${variableName} must target 127.0.0.1, localhost, or ::1.`);
  }

  return normalizeUrl(url);
}

/**
 * Reads an E2E endpoint from its environment variable or a local fallback,
 * then applies the same production-target guard in both cases.
 */
export function getLoopbackHttpUrl(variableName: string, fallback: string): string {
  return assertLoopbackHttpUrl(variableName, process.env[variableName] ?? fallback);
}

function normalizeUrl(url: URL): string {
  const path = url.pathname === "/" ? "" : url.pathname.replace(/\/+$/, "");

  return `${url.origin}${path}${url.search}${url.hash}`;
}
