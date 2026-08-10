import http from 'k6/http';
import { check, fail, sleep } from 'k6';
import { Rate, Trend } from 'k6/metrics';
import {
  JSON_HEADERS,
  buildUrl,
  getPositiveInteger,
  isSuccessfulEnvelope,
  parseJson,
  requireEnvironment,
  requireExplicitFlag,
} from './lib/runtime.js';

requireExplicitFlag('K6_ALLOW_AUTH_FLOW');

const suppliedAccessToken = (__ENV.K6_ACCESS_TOKEN || '').trim();
const suppliedEmail = (__ENV.K6_CUSTOMER_EMAIL || '').trim();
const suppliedPassword = (__ENV.K6_CUSTOMER_PASSWORD || '').trim();
const hasLoginCredentials = suppliedEmail.length > 0 && suppliedPassword.length > 0;
const testRefreshRotation = (__ENV.K6_TEST_REFRESH_ROTATION || '').toLowerCase() === 'true';
const authVirtualUsers = getPositiveInteger('K6_AUTH_VUS', 1, 50);
const refreshIterations = getPositiveInteger('K6_REFRESH_ITERATIONS', 1, 20);
const authDuration = (__ENV.K6_AUTH_DURATION || '1m').trim();

if (!suppliedAccessToken && !hasLoginCredentials) {
  fail('Supply K6_ACCESS_TOKEN, or both K6_CUSTOMER_EMAIL and K6_CUSTOMER_PASSWORD for authenticated reads.');
}

if (testRefreshRotation && !hasLoginCredentials) {
  fail('K6_CUSTOMER_EMAIL and K6_CUSTOMER_PASSWORD are required when K6_TEST_REFRESH_ROTATION=true.');
}

const authenticatedRequestDuration = new Trend('authenticated_request_duration', true);
const authenticatedSuccess = new Rate('authenticated_success');

// Each virtual user has its own module context, so this token is never shared between users.
let cachedAccessToken = suppliedAccessToken;

export const options = {
  scenarios: {
    authenticatedReads: {
      executor: 'constant-vus',
      exec: 'authenticatedReads',
      vus: authVirtualUsers,
      duration: authDuration,
    },
    ...(testRefreshRotation
      ? {
        refreshRotation: {
          executor: 'per-vu-iterations',
          exec: 'refreshRotation',
          vus: 1,
          iterations: refreshIterations,
          maxDuration: '5m',
        },
      }
      : {}),
  },
  thresholds: {
    checks: ['rate>0.99'],
    http_req_failed: ['rate<0.01'],
    authenticated_request_duration: ['p(95)<500', 'p(99)<1000'],
    authenticated_success: ['rate>0.99'],
  },
};

export function authenticatedReads() {
  const accessToken = getAccessToken();
  requestAuthenticatedEnvelope('customer-profile', '/api/customer/me', accessToken);
  requestAuthenticatedEnvelope('customer-orders', '/api/customer/orders?pageNumber=1&pageSize=20', accessToken);
  requestAuthenticatedEnvelope('loyalty-profile', '/api/loyalty/me', accessToken);
  requestAuthenticatedEnvelope('loyalty-transactions', '/api/loyalty/me/transactions?pageNumber=1&pageSize=20', accessToken);
  sleep(0.5 + Math.random());
}

export function refreshRotation() {
  // A fresh login gives this one-VU scenario its own refresh-token family. The default k6
  // cookie jar carries the HttpOnly refresh cookie to the refresh endpoint without logging it.
  const initialToken = login('auth-login-for-refresh');
  const response = http.post(buildUrl('/api/customer/auth/refresh'), null, withEndpoint('auth-refresh'));
  const succeeded = isSuccessfulEnvelope(response) && getAccessTokenFromResponse(response).length > 0;

  recordResponse(response, 'auth-refresh', succeeded);
  check(response, {
    'auth-refresh: HTTP 200': (result) => result.status === 200,
    'auth-refresh: returns a replacement access token': () => succeeded,
  });

  if (!succeeded) {
    fail('Refresh-token rotation did not return a usable access token.');
  }

  const refreshedToken = getAccessTokenFromResponse(response);
  if (refreshedToken === initialToken) {
    fail('Refresh-token rotation returned the original access token; verify the target configuration and test account.');
  }
}

function getAccessToken() {
  if (cachedAccessToken.length > 0) {
    return cachedAccessToken;
  }

  cachedAccessToken = login('auth-login');
  return cachedAccessToken;
}

function login(endpoint) {
  const email = requireEnvironment('K6_CUSTOMER_EMAIL');
  const password = requireEnvironment('K6_CUSTOMER_PASSWORD');
  const response = http.post(
    buildUrl('/api/customer/auth/login'),
    JSON.stringify({ email, password }),
    withEndpoint(endpoint));
  const accessToken = getAccessTokenFromResponse(response);
  const succeeded = isSuccessfulEnvelope(response) && accessToken.length > 0;

  recordResponse(response, endpoint, succeeded);
  check(response, {
    [`${endpoint}: HTTP 200`]: (result) => result.status === 200,
    [`${endpoint}: returns access token`]: () => succeeded,
  });

  if (!succeeded) {
    fail('Synthetic load-test account could not log in. Do not use a real customer account or a TOTP-enabled account.');
  }

  return accessToken;
}

function requestAuthenticatedEnvelope(endpoint, path, accessToken) {
  const response = http.get(buildUrl(path), withEndpoint(endpoint, {
    Authorization: `Bearer ${accessToken}`,
  }));
  const succeeded = isSuccessfulEnvelope(response);

  recordResponse(response, endpoint, succeeded);
  check(response, {
    [`${endpoint}: HTTP 200`]: (result) => result.status === 200,
    [`${endpoint}: successful API envelope`]: () => succeeded,
  });
}

function getAccessTokenFromResponse(response) {
  const body = parseJson(response);
  return body && body.data && typeof body.data.accessToken === 'string'
    ? body.data.accessToken
    : '';
}

function recordResponse(response, endpoint, succeeded) {
  const tags = { endpoint };
  authenticatedRequestDuration.add(response.timings.duration, tags);
  authenticatedSuccess.add(succeeded, tags);
}

function withEndpoint(endpoint, additionalHeaders) {
  return {
    ...JSON_HEADERS,
    headers: {
      ...JSON_HEADERS.headers,
      ...(additionalHeaders || {}),
    },
    tags: { endpoint },
  };
}
