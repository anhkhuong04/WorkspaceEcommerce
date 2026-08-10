import { fail } from 'k6';

const rawBaseUrl = (__ENV.BASE_URL || 'http://localhost:5080').trim();

export const BASE_URL = validateBaseUrl(rawBaseUrl);

export const JSON_HEADERS = {
  headers: {
    Accept: 'application/json',
    'Content-Type': 'application/json',
    'User-Agent': 'workspace-ecommerce-prh-016-k6',
  },
};

export function buildUrl(path) {
  if (!path || path.charAt(0) !== '/') {
    fail(`Expected an absolute API path, received '${path}'.`);
  }

  return `${BASE_URL}${path}`;
}

export function parseJson(response) {
  try {
    return response.json();
  } catch (_) {
    return null;
  }
}

export function isSuccessfulEnvelope(response) {
  if (response.status < 200 || response.status > 299) {
    return false;
  }

  const body = parseJson(response);
  return body !== null && body.success === true;
}

export function getPositiveInteger(name, fallback, maximum) {
  const rawValue = __ENV[name];
  if (rawValue === undefined || rawValue === '') {
    return fallback;
  }

  const parsedValue = Number(rawValue);
  if (!Number.isInteger(parsedValue) || parsedValue < 1 || parsedValue > maximum) {
    fail(`${name} must be an integer between 1 and ${maximum}.`);
  }

  return parsedValue;
}

export function requireEnvironment(name) {
  const value = (__ENV[name] || '').trim();
  if (value.length === 0) {
    fail(`${name} must be supplied through the test runner environment.`);
  }

  return value;
}

export function requireExplicitFlag(name) {
  if ((__ENV[name] || '').toLowerCase() !== 'true') {
    fail(`${name}=true is required before this scenario can run.`);
  }
}

export function requireIsolatedStaging() {
  if ((__ENV.K6_TEST_ENVIRONMENT || '').toLowerCase() !== 'isolated-staging') {
    fail('K6_TEST_ENVIRONMENT=isolated-staging is required for this scenario.');
  }
}

export function requireSafeHttpReadUrl(name) {
  const value = requireEnvironment(name);
  const isPlainHttpUrl = /^https?:\/\/(?:\[[0-9a-f:.]+\]|[a-z0-9.-]+)(?::\d{1,5})?(?:\/[^\s?#]*)?$/i.test(value);

  if (!isPlainHttpUrl || value.includes('@')) {
    fail(`${name} must be a plain http(s) URL without credentials, query string, or fragment.`);
  }

  return value;
}

function validateBaseUrl(value) {
  const normalized = value.replace(/\/+$/, '');
  const match = /^https?:\/\/(\[[0-9a-f:.]+\]|[^\/:?#]+)(?::\d+)?(?:\/|$)/i.exec(normalized);

  if (match === null || /[?@#]/.test(normalized)) {
    fail('BASE_URL must be a plain http(s) origin without credentials, query parameters, or fragments.');
  }

  const host = match[1].replace(/^\[|\]$/g, '').toLowerCase();
  const localHosts = ['localhost', '127.0.0.1', '::1'];
  const isLocalTarget = localHosts.indexOf(host) !== -1;

  if (!isLocalTarget && (__ENV.K6_ALLOW_NONLOCAL_TARGET || '').toLowerCase() !== 'true') {
    fail('Refusing a non-local target. Set K6_ALLOW_NONLOCAL_TARGET=true only for an approved isolated environment.');
  }

  return normalized;
}
