import crypto from 'k6/crypto';
import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate, Trend } from 'k6/metrics';
import {
  JSON_HEADERS,
  buildUrl,
  getPositiveInteger,
  parseJson,
  requireEnvironment,
  requireExplicitFlag,
  requireIsolatedStaging,
} from './lib/runtime.js';

requireExplicitFlag('K6_ALLOW_SIGNED_WEBHOOK_TEST');
requireIsolatedStaging();

const webhookSecret = requireEnvironment('K6_MINILOGISTICS_WEBHOOK_SECRET');
const virtualUsers = getPositiveInteger('K6_WEBHOOK_VUS', 1, 5);
const iterations = getPositiveInteger('K6_WEBHOOK_ITERATIONS', 1, 100);
const testPayload = JSON.stringify({ event: 'webhook.test' });

const signedWebhookDuration = new Trend('signed_webhook_request_duration', true);
const signedWebhookSuccess = new Rate('signed_webhook_success');

export const options = {
  scenarios: {
    signedWebhook: {
      executor: 'per-vu-iterations',
      vus: virtualUsers,
      iterations,
      maxDuration: '10m',
    },
  },
  thresholds: {
    checks: ['rate>0.99'],
    http_req_failed: ['rate<0.01'],
    signed_webhook_request_duration: ['p(95)<1000', 'p(99)<2000'],
    signed_webhook_success: ['rate>0.99'],
  },
};

export default function () {
  const timestamp = new Date().toISOString();
  const signature = crypto.hmac('sha256', webhookSecret, `${timestamp}.${testPayload}`, 'hex');
  const response = http.post(
    buildUrl('/api/webhooks/minilogistics'),
    testPayload,
    {
      ...JSON_HEADERS,
      headers: {
        ...JSON_HEADERS.headers,
        'X-MiniLogistics-Event': 'webhook.test',
        'X-MiniLogistics-Signature': `sha256=${signature}`,
        'X-MiniLogistics-Timestamp': timestamp,
      },
      tags: { endpoint: 'minilogistics-webhook-test' },
    });
  const responseBody = parseJson(response);
  const succeeded = response.status === 200 && responseBody &&
    responseBody.message === 'Test event received successfully.';
  const tags = { endpoint: 'minilogistics-webhook-test' };

  signedWebhookDuration.add(response.timings.duration, tags);
  signedWebhookSuccess.add(succeeded, tags);
  check(response, {
    'signed-webhook: HTTP 200': (result) => result.status === 200,
    'signed-webhook: test callback is acknowledged': () => succeeded,
  });
  sleep(0.1);
}
