import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate, Trend } from 'k6/metrics';
import {
  JSON_HEADERS,
  getPositiveInteger,
  requireExplicitFlag,
  requireIsolatedStaging,
  requireSafeHttpReadUrl,
} from './lib/runtime.js';

requireExplicitFlag('K6_ALLOW_MEDIA_READS');
requireIsolatedStaging();

const mediaReadUrl = requireSafeHttpReadUrl('K6_MEDIA_READ_URL');
const expectedContentType = (__ENV.K6_MEDIA_EXPECTED_CONTENT_TYPE || 'image/webp').trim().toLowerCase();
const virtualUsers = getPositiveInteger('K6_MEDIA_VUS', 1, 100);
const duration = (__ENV.K6_MEDIA_DURATION || '1m').trim();

const mediaReadDuration = new Trend('media_read_request_duration', true);
const mediaReadSuccess = new Rate('media_read_success');

export const options = {
  scenarios: {
    mediaReads: {
      executor: 'constant-vus',
      vus: virtualUsers,
      duration,
    },
  },
  thresholds: {
    checks: ['rate>0.99'],
    http_req_failed: ['rate<0.01'],
    media_read_request_duration: ['p(95)<1000', 'p(99)<2000'],
    media_read_success: ['rate>0.99'],
  },
};

export default function () {
  const response = http.get(mediaReadUrl, {
    ...JSON_HEADERS,
    responseType: 'none',
    tags: { endpoint: 'media-read' },
  });
  const contentType = (response.headers['Content-Type'] || response.headers['content-type'] || '').toLowerCase();
  const succeeded = response.status === 200 && contentType.startsWith(expectedContentType);
  const tags = { endpoint: 'media-read' };

  mediaReadDuration.add(response.timings.duration, tags);
  mediaReadSuccess.add(succeeded, tags);
  check(response, {
    'media-read: HTTP 200': (result) => result.status === 200,
    'media-read: expected content type': () => contentType.startsWith(expectedContentType),
  });
  sleep(0.25 + Math.random() * 0.25);
}
