import http from 'k6/http';
import { check, fail, sleep } from 'k6';
import { Counter, Rate, Trend } from 'k6/metrics';
import {
  BASE_URL,
  JSON_HEADERS,
  buildUrl,
  getPositiveInteger,
  isSuccessfulEnvelope,
  parseJson,
} from './lib/runtime.js';

const profile = (__ENV.K6_PROFILE || 'smoke').toLowerCase();
const peakVirtualUsers = getPositiveInteger('K6_PEAK_VUS', 100, 10000);
const soakVirtualUsers = getPositiveInteger('K6_SOAK_VUS', 20, 10000);

const applicationRequestDuration = new Trend('application_request_duration', true);
const applicationSuccess = new Rate('application_success');
const serverErrorRate = new Rate('application_server_error');
const rateLimitedRequests = new Counter('application_rate_limited_requests');

export const options = buildOptions(profile);

export function setup() {
  const response = http.get(
    buildUrl('/api/products?pageNumber=1&pageSize=20'),
    withEndpoint('catalog-list-setup'));

  if (!isSuccessfulEnvelope(response)) {
    fail(`Catalog setup failed with HTTP ${response.status}. Seed representative catalog data before running PRH-016.`);
  }

  const body = parseJson(response);
  const items = body && body.data && Array.isArray(body.data.items) ? body.data.items : [];
  const configuredSlug = (__ENV.K6_PRODUCT_SLUG || '').trim();
  const selectedProduct = configuredSlug.length > 0
    ? null
    : items.find((item) => item && typeof item.slug === 'string' && item.slug.length > 0);
  const productSlug = configuredSlug || (selectedProduct && selectedProduct.slug);

  if (!productSlug) {
    fail('No product slug is available. Set K6_PRODUCT_SLUG or seed at least one active product.');
  }

  const productName = selectedProduct && typeof selectedProduct.name === 'string'
    ? selectedProduct.name
    : productSlug;
  const firstSearchToken = productName.trim().split(/\s+/)[0];

  return {
    productSlug,
    searchTerm: firstSearchToken || productSlug,
  };
}

export default function (catalog) {
  requestApiEnvelope('catalog-list', '/api/products?pageNumber=1&pageSize=20');
  requestApiEnvelope('catalog-search', `/api/products?pageNumber=1&pageSize=20&search=${encodeURIComponent(catalog.searchTerm)}`);
  requestApiEnvelope('categories', '/api/categories');
  requestApiEnvelope('banners', '/api/banners');
  requestApiEnvelope('blog-list', '/api/blog-posts');
  requestApiEnvelope('product-detail', `/api/products/${encodeURIComponent(catalog.productSlug)}`);
  requestApiEnvelope('product-reviews', `/api/products/${encodeURIComponent(catalog.productSlug)}/reviews`);

  // Health traffic is deliberately sparse: deployment probes should not dominate the storefront model.
  if (Math.random() < 0.05) {
    requestHealth('readiness', '/health/ready');
  }

  sleep(0.2 + Math.random());
}

function buildOptions(selectedProfile) {
  const common = {
    thresholds: {
      checks: ['rate>0.99'],
      http_req_failed: ['rate<0.01'],
      http_req_duration: ['p(95)<500', 'p(99)<1000'],
      application_request_duration: ['p(95)<500', 'p(99)<1000'],
      application_success: ['rate>0.99'],
      application_server_error: ['rate<0.01'],
    },
  };

  switch (selectedProfile) {
    case 'smoke':
      return {
        ...common,
        scenarios: {
          storefront: { executor: 'constant-vus', vus: 1, duration: '30s' },
        },
      };
    case 'baseline':
      return {
        ...common,
        scenarios: {
          storefront: {
            executor: 'ramping-vus',
            startVUs: 0,
            stages: [
              { duration: '2m', target: 10 },
              { duration: '10m', target: 10 },
              { duration: '2m', target: 0 },
            ],
          },
        },
      };
    case 'peak':
      return {
        ...common,
        scenarios: {
          storefront: {
            executor: 'ramping-vus',
            startVUs: 0,
            stages: [
              { duration: '5m', target: peakVirtualUsers },
              { duration: '30m', target: peakVirtualUsers },
              { duration: '5m', target: 0 },
            ],
          },
        },
      };
    case 'soak':
      return {
        ...common,
        scenarios: {
          storefront: {
            executor: 'ramping-vus',
            startVUs: 0,
            stages: [
              { duration: '5m', target: soakVirtualUsers },
              { duration: '8h', target: soakVirtualUsers },
              { duration: '5m', target: 0 },
            ],
          },
        },
      };
    default:
      fail(`Unsupported K6_PROFILE '${selectedProfile}'. Use smoke, baseline, peak, or soak.`);
  }
}

function requestApiEnvelope(endpoint, path) {
  const response = http.get(buildUrl(path), withEndpoint(endpoint));
  const succeeded = isSuccessfulEnvelope(response);

  recordResponse(response, endpoint, succeeded);
  check(response, {
    [`${endpoint}: HTTP 200`]: (result) => result.status === 200,
    [`${endpoint}: successful API envelope`]: () => succeeded,
  });
}

function requestHealth(endpoint, path) {
  const response = http.get(buildUrl(path), withEndpoint(endpoint));
  const succeeded = response.status === 200;

  recordResponse(response, endpoint, succeeded);
  check(response, {
    [`${endpoint}: HTTP 200`]: (result) => result.status === 200,
  });
}

function recordResponse(response, endpoint, succeeded) {
  const tags = { endpoint };
  applicationRequestDuration.add(response.timings.duration, tags);
  applicationSuccess.add(succeeded, tags);
  serverErrorRate.add(response.status >= 500, tags);

  if (response.status === 429) {
    rateLimitedRequests.add(1, tags);
  }
}

function withEndpoint(endpoint) {
  return {
    ...JSON_HEADERS,
    tags: { endpoint },
  };
}
