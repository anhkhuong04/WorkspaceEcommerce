import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate, Trend } from 'k6/metrics';
import {
  JSON_HEADERS,
  buildUrl,
  getPositiveInteger,
  isSuccessfulEnvelope,
  requireEnvironment,
  requireExplicitFlag,
  requireIsolatedStaging,
} from './lib/runtime.js';

requireExplicitFlag('K6_ALLOW_ADMIN_READS');
requireIsolatedStaging();

const accessToken = requireEnvironment('K6_ADMIN_ACCESS_TOKEN');
const virtualUsers = getPositiveInteger('K6_ADMIN_VUS', 1, 50);
const duration = (__ENV.K6_ADMIN_DURATION || '1m').trim();

const adminReadDuration = new Trend('admin_read_request_duration', true);
const adminReadSuccess = new Rate('admin_read_success');

export const options = {
  scenarios: {
    adminReads: {
      executor: 'constant-vus',
      vus: virtualUsers,
      duration,
    },
  },
  thresholds: {
    checks: ['rate>0.99'],
    http_req_failed: ['rate<0.01'],
    admin_read_request_duration: ['p(95)<1000', 'p(99)<2000'],
    admin_read_success: ['rate>0.99'],
  },
};

export default function () {
  requestAdminEnvelope('admin-dashboard', '/api/admin/dashboard');
  requestAdminEnvelope('admin-products', '/api/admin/products?pageNumber=1&pageSize=20');
  requestAdminEnvelope('admin-orders', '/api/admin/orders?pageNumber=1&pageSize=20');
  requestAdminEnvelope('admin-coupons', '/api/admin/coupons?pageNumber=1&pageSize=20');
  requestAdminEnvelope('admin-blog-posts', '/api/admin/blog-posts?pageNumber=1&pageSize=20');
  requestAdminEnvelope('admin-blog-comments', '/api/admin/blog-comments?pageNumber=1&pageSize=20');
  requestAdminEnvelope('admin-reviews', '/api/admin/reviews?pageNumber=1&pageSize=20');
  sleep(0.5 + Math.random());
}

function requestAdminEnvelope(endpoint, path) {
  const response = http.get(buildUrl(path), withEndpoint(endpoint));
  const succeeded = isSuccessfulEnvelope(response);
  const tags = { endpoint };

  adminReadDuration.add(response.timings.duration, tags);
  adminReadSuccess.add(succeeded, tags);
  check(response, {
    [`${endpoint}: HTTP 200`]: (result) => result.status === 200,
    [`${endpoint}: successful API envelope`]: () => succeeded,
  });
}

function withEndpoint(endpoint) {
  return {
    ...JSON_HEADERS,
    headers: {
      ...JSON_HEADERS.headers,
      Authorization: `Bearer ${accessToken}`,
    },
    tags: { endpoint },
  };
}
