import http from 'k6/http';
import { check, fail, sleep } from 'k6';
import { Counter, Rate, Trend } from 'k6/metrics';
import {
  JSON_HEADERS,
  buildUrl,
  getPositiveInteger,
  isSuccessfulEnvelope,
} from './lib/runtime.js';

const virtualUsers = getPositiveInteger('K6_RESILIENCE_VUS', 10, 10000);
const duration = (__ENV.K6_RESILIENCE_DURATION || '15m').trim();
const expectRecovery = (__ENV.K6_EXPECT_RECOVERY || '').toLowerCase() === 'true';

const availability = new Rate('resilience_availability');
const serverErrorRate = new Rate('resilience_server_error');
const outageRequests = new Counter('resilience_outage_requests');
const recoveryEvents = new Counter('resilience_recovery_events');
const recoveryProbeDuration = new Trend('resilience_probe_duration', true);

let sawOutage = false;
let consecutiveHealthyProbes = 0;

export const options = buildOptions();

export function setup() {
  const response = http.get(buildUrl('/health/ready'), withEndpoint('readiness-baseline'));
  if (response.status !== 200) {
    fail(`The target is not ready before failure injection (HTTP ${response.status}).`);
  }

  return {};
}

export default function () {
  // Do not inject faults from this script. An approved operator controls the fault in the
  // target environment while this probe produces an availability/recovery timeline.
  const isCatalogProbe = __ITER % 2 === 0;
  const endpoint = isCatalogProbe ? 'catalog-recovery-probe' : 'readiness-recovery-probe';
  const response = isCatalogProbe
    ? http.get(buildUrl('/api/products?pageNumber=1&pageSize=5'), withEndpoint(endpoint))
    : http.get(buildUrl('/health/ready'), withEndpoint(endpoint));
  const succeeded = isCatalogProbe ? isSuccessfulEnvelope(response) : response.status === 200;
  const tags = { endpoint };

  availability.add(succeeded, tags);
  serverErrorRate.add(response.status >= 500, tags);
  recoveryProbeDuration.add(response.timings.duration, tags);

  check(response, {
    [`${endpoint}: available`]: () => succeeded,
  });

  if (succeeded) {
    consecutiveHealthyProbes += 1;
    if (sawOutage && consecutiveHealthyProbes >= 3) {
      recoveryEvents.add(1, tags);
      sawOutage = false;
    }
  } else {
    sawOutage = true;
    consecutiveHealthyProbes = 0;
    outageRequests.add(1, tags);
  }

  sleep(0.5);
}

function buildOptions() {
  const thresholds = {
    resilience_availability: ['rate>0.90'],
    resilience_server_error: ['rate<0.10'],
    resilience_probe_duration: ['p(95)<2000', 'p(99)<5000'],
  };

  if (expectRecovery) {
    thresholds.resilience_recovery_events = ['count>=1'];
  }

  return {
    scenarios: {
      recoveryProbes: {
        executor: 'constant-vus',
        vus: virtualUsers,
        duration,
      },
    },
    thresholds,
  };
}

function withEndpoint(endpoint) {
  return {
    ...JSON_HEADERS,
    tags: { endpoint },
  };
}
