import http from 'k6/http';
import ws from 'k6/ws';
import { check, fail, sleep } from 'k6';
import { Counter, Rate, Trend } from 'k6/metrics';
import {
  BASE_URL,
  JSON_HEADERS,
  buildUrl,
  getPositiveInteger,
  parseJson,
  requireEnvironment,
  requireExplicitFlag,
  requireIsolatedStaging,
} from './lib/runtime.js';

requireExplicitFlag('K6_ALLOW_SIGNALR_CONNECTIVITY');
requireIsolatedStaging();

const accessToken = requireEnvironment('K6_SIGNALR_ACCESS_TOKEN');
const virtualUsers = getPositiveInteger('K6_SIGNALR_VUS', 1, 25);
const duration = (__ENV.K6_SIGNALR_DURATION || '1m').trim();
const recordSeparator = String.fromCharCode(30);

const signalRNegotiationSuccess = new Rate('signalr_negotiate_success');
const signalRWebSocketSuccess = new Rate('signalr_websocket_success');
const signalRHandshakeSuccess = new Rate('signalr_handshake_success');
const signalRConnectionDuration = new Trend('signalr_connection_duration', true);
const signalRConnectionErrors = new Counter('signalr_connection_errors');

export const options = {
  scenarios: {
    signalRConnectivity: {
      executor: 'constant-vus',
      vus: virtualUsers,
      duration,
    },
  },
  thresholds: {
    checks: ['rate>0.99'],
    http_req_failed: ['rate<0.01'],
    signalr_negotiate_success: ['rate>0.99'],
    signalr_websocket_success: ['rate>0.99'],
    signalr_handshake_success: ['rate>0.99'],
    signalr_connection_duration: ['p(95)<3000', 'p(99)<5000'],
  },
};

export default function () {
  const negotiation = http.post(
    buildUrl('/hubs/notifications/negotiate?negotiateVersion=1'),
    null,
    withAuthorization('signalr-negotiate'));
  const negotiationBody = parseJson(negotiation);
  const connectionToken = negotiationBody && typeof negotiationBody.connectionToken === 'string'
    ? negotiationBody.connectionToken
    : '';
  const supportsWebSockets = negotiationBody && Array.isArray(negotiationBody.availableTransports) &&
    negotiationBody.availableTransports.some((transport) => transport && transport.transport === 'WebSockets');
  const negotiationSucceeded = negotiation.status === 200 && connectionToken.length > 0 && supportsWebSockets;

  signalRNegotiationSuccess.add(negotiationSucceeded, { endpoint: 'signalr-negotiate' });
  signalRConnectionDuration.add(negotiation.timings.duration, { endpoint: 'signalr-negotiate' });
  check(negotiation, {
    'signalr-negotiate: HTTP 200': (result) => result.status === 200,
    'signalr-negotiate: returns a WebSocket connection token': () => negotiationSucceeded,
  });

  if (!negotiationSucceeded) {
    fail('SignalR negotiation did not return a WebSocket connection token. Use a short-lived synthetic customer token with the Customer role.');
  }

  let handshakeSucceeded = false;
  const websocketUrl = `${BASE_URL.replace(/^http/, 'ws')}/hubs/notifications?id=${encodeURIComponent(connectionToken)}`;
  const response = ws.connect(websocketUrl, withAuthorization('signalr-websocket'), (socket) => {
    socket.on('open', () => {
      socket.send(`{"protocol":"json","version":1}${recordSeparator}`);
    });

    socket.on('message', (message) => {
      if (String(message).includes(`{}${recordSeparator}`)) {
        handshakeSucceeded = true;
        socket.close();
      }
    });

    socket.on('error', () => {
      signalRConnectionErrors.add(1, { endpoint: 'signalr-websocket' });
    });

    socket.setTimeout(() => socket.close(), 10_000);
  });
  const websocketSucceeded = response && response.status === 101;
  const tags = { endpoint: 'signalr-websocket' };

  signalRWebSocketSuccess.add(websocketSucceeded, tags);
  signalRHandshakeSuccess.add(handshakeSucceeded, tags);
  signalRConnectionDuration.add(response.timings.duration, tags);
  check(response, {
    'signalr-websocket: HTTP 101': (result) => result.status === 101,
    'signalr-websocket: JSON protocol handshake succeeds': () => handshakeSucceeded,
  });
  sleep(0.5 + Math.random() * 0.5);
}

function withAuthorization(endpoint) {
  return {
    ...JSON_HEADERS,
    headers: {
      ...JSON_HEADERS.headers,
      Authorization: `Bearer ${accessToken}`,
    },
    tags: { endpoint },
  };
}
