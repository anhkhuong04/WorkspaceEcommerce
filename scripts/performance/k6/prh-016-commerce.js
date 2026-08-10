import http from 'k6/http';
import { check, fail, sleep } from 'k6';
import { Counter, Rate, Trend } from 'k6/metrics';
import {
  JSON_HEADERS,
  buildUrl,
  getPositiveInteger,
  isSuccessfulEnvelope,
  parseJson,
  requireEnvironment,
  requireExplicitFlag,
} from './lib/runtime.js';

requireExplicitFlag('K6_ALLOW_WRITE_TESTS');

const productVariantId = requireEnvironment('K6_TEST_VARIANT_ID');
const testCouponCode = (__ENV.K6_TEST_COUPON_CODE || '').trim();
const enableCheckout = (__ENV.K6_ENABLE_CHECKOUT || '').toLowerCase() === 'true';
const enableShippingQuote = (__ENV.K6_ENABLE_SHIPPING_QUOTE || '').toLowerCase() === 'true';
const commerceVirtualUsers = getPositiveInteger('K6_COMMERCE_VUS', 1, 10);
const commerceIterations = getPositiveInteger('K6_COMMERCE_ITERATIONS', 1, 1000);

if (!/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(productVariantId)) {
  fail('K6_TEST_VARIANT_ID must be a product-variant GUID.');
}

if (enableCheckout || enableShippingQuote) {
  requireExplicitFlag('K6_ALLOW_EXTERNAL_PROVIDER_CALLS');
  if ((__ENV.K6_TEST_ENVIRONMENT || '').toLowerCase() !== 'isolated-staging') {
    fail('Checkout and shipping-quote scenarios are restricted to K6_TEST_ENVIRONMENT=isolated-staging.');
  }
}

if (enableCheckout) {
  requireExplicitFlag('K6_ALLOW_CHECKOUT');
}

const commerceRequestDuration = new Trend('commerce_request_duration', true);
const checkoutDuration = new Trend('commerce_checkout_duration', true);
const commerceSuccess = new Rate('commerce_success');
const cartItemsAdded = new Counter('commerce_cart_items_added');
const cartItemsRemoved = new Counter('commerce_cart_items_removed');
const checkoutOrdersCreated = new Counter('commerce_checkout_orders_created');

export const options = {
  scenarios: {
    commerceFlow: {
      executor: 'per-vu-iterations',
      vus: commerceVirtualUsers,
      iterations: commerceIterations,
      maxDuration: '1h',
    },
  },
  thresholds: {
    checks: ['rate>0.99'],
    http_req_failed: ['rate<0.01'],
    commerce_request_duration: ['p(95)<1000', 'p(99)<2000'],
    commerce_success: ['rate>0.99'],
    ...(enableCheckout ? { commerce_checkout_duration: ['p(95)<2000'] } : {}),
  },
};

export default function () {
  const sessionId = `prh016-${__VU}-${__ITER}-${Date.now()}`;
  const addResponse = requestEnvelope(
    'cart-add-item',
    'POST',
    '/api/cart/items',
    {
      sessionId,
      productVariantId,
      quantity: 1,
    },
    200);

  if (!isSuccessfulEnvelope(addResponse)) {
    fail('Could not add the configured test variant to the isolated test cart. Check stock and data preparation.');
  }

  cartItemsAdded.add(1);
  const itemId = getCartItemId(addResponse, productVariantId);
  if (!itemId) {
    fail('The cart response does not contain the item just added.');
  }

  requestEnvelope('cart-read', 'GET', `/api/cart?sessionId=${encodeURIComponent(sessionId)}`, null, 200);

  if (testCouponCode.length > 0) {
    requestEnvelope(
      'coupon-validate',
      'POST',
      '/api/checkout/coupons/validate',
      { sessionId, couponCode: testCouponCode },
      200);
  }

  if (enableShippingQuote) {
    requestEnvelope(
      'shipping-quote',
      'POST',
      '/api/checkout/shipping-quote',
      buildShippingQuoteRequest(sessionId),
      200);
  }

  if (enableCheckout) {
    const checkoutResponse = requestEnvelope(
      'checkout',
      'POST',
      '/api/checkout',
      buildCheckoutRequest(sessionId),
      201);

    if (!isSuccessfulEnvelope(checkoutResponse)) {
      fail('Checkout did not succeed. Preserve the evidence and reconcile stock, coupon, payment, loyalty, outbox, and shipment state.');
    }

    checkoutOrdersCreated.add(1);
  } else {
    const removeResponse = requestEnvelope(
      'cart-remove-item',
      'DELETE',
      `/api/cart/items/${encodeURIComponent(itemId)}?sessionId=${encodeURIComponent(sessionId)}`,
      null,
      200);

    if (!isSuccessfulEnvelope(removeResponse)) {
      fail('Cart cleanup failed. Do not reuse this environment until the leftover test cart is reconciled.');
    }

    cartItemsRemoved.add(1);
  }

  sleep(0.25);
}

function requestEnvelope(endpoint, method, path, body, expectedStatus) {
  const response = method === 'GET' || method === 'DELETE'
    ? http.request(method, buildUrl(path), null, withEndpoint(endpoint))
    : http.request(method, buildUrl(path), JSON.stringify(body), withEndpoint(endpoint));
  const succeeded = response.status === expectedStatus && isSuccessfulEnvelope(response);

  recordResponse(response, endpoint, succeeded);
  if (endpoint === 'checkout') {
    checkoutDuration.add(response.timings.duration, { endpoint });
  }

  check(response, {
    [`${endpoint}: HTTP ${expectedStatus}`]: (result) => result.status === expectedStatus,
    [`${endpoint}: successful API envelope`]: () => succeeded,
  });

  return response;
}

function buildShippingQuoteRequest(sessionId) {
  return {
    sessionId,
    street: requireEnvironment('K6_CHECKOUT_STREET'),
    ward: requireEnvironment('K6_CHECKOUT_WARD'),
    province: requireEnvironment('K6_CHECKOUT_PROVINCE'),
  };
}

function buildCheckoutRequest(sessionId) {
  const paymentMethod = Number(__ENV.K6_CHECKOUT_PAYMENT_METHOD || '1');
  if (!Number.isInteger(paymentMethod) || paymentMethod < 0 || paymentMethod > 2) {
    fail('K6_CHECKOUT_PAYMENT_METHOD must be 0 (COD), 1 (manual bank transfer), or 2 (VNPay).');
  }

  const street = requireEnvironment('K6_CHECKOUT_STREET');
  const ward = requireEnvironment('K6_CHECKOUT_WARD');
  const province = requireEnvironment('K6_CHECKOUT_PROVINCE');
  const email = (__ENV.K6_CHECKOUT_EMAIL || '').trim();

  return {
    sessionId,
    customerName: requireEnvironment('K6_CHECKOUT_CUSTOMER_NAME'),
    customerPhone: requireEnvironment('K6_CHECKOUT_PHONE'),
    customerEmail: email || null,
    shippingAddress: `${street}, ${ward}, ${province}`,
    shippingStreet: street,
    shippingWard: ward,
    shippingProvince: province,
    note: 'PRH-016 isolated load-test order',
    paymentMethod,
    couponCode: testCouponCode || null,
    clientIpAddress: '127.0.0.1',
  };
}

function getCartItemId(response, expectedVariantId) {
  const body = parseJson(response);
  const items = body && body.data && Array.isArray(body.data.items) ? body.data.items : [];
  const item = items.find((candidate) => candidate && candidate.productVariantId === expectedVariantId);

  return item && typeof item.id === 'string' ? item.id : '';
}

function recordResponse(response, endpoint, succeeded) {
  const tags = { endpoint };
  commerceRequestDuration.add(response.timings.duration, tags);
  commerceSuccess.add(succeeded, tags);
}

function withEndpoint(endpoint) {
  return {
    ...JSON_HEADERS,
    tags: { endpoint },
  };
}
