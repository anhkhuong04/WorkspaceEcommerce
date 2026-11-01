// Synthetic MiniLogistics protocol emulator. Only the isolated E2E runner starts
// this container; it has no outbound requests and never uses real credentials.
import http from "node:http";
import { randomUUID } from "node:crypto";
const key = process.env.SYNTHETIC_PROVIDER_KEY;
if (!key) throw new Error("Synthetic provider key is required.");
const shipments = new Map();
const counts = { quote: 0, create: 0, tracking: 0, cancel: 0 };
let outage = false;
http.createServer(async (request, response) => {
  const reply = (status, body) => { response.writeHead(status, { "content-type": "application/json" }); response.end(JSON.stringify(body)); };
  if (request.headers.authorization !== `Bearer ${key}`) return reply(401, { error: "Synthetic authentication required" });
  const url = new URL(request.url, "http://synthetic");
  let text = "";
  for await (const chunk of request) { text += chunk; if (text.length > 65536) return reply(413, {}); }
  let body;
  try { body = text ? JSON.parse(text) : {}; } catch { return reply(400, {}); }
  if (url.pathname === "/__test/state") return reply(200, { counts, shipments: [...shipments.values()] });
  if (url.pathname === "/__test/outage") { outage = body.enabled === true; return reply(200, {}); }
  if (url.pathname === "/__test/status") {
    const shipment = [...shipments.values()].find(x => x.trackingCode === body.trackingCode);
    if (!shipment || !["Draft", "Delivered", "Cancelled"].includes(body.status)) return reply(400, {});
    shipment.status = body.status; return reply(200, {});
  }
  if (url.pathname.endsWith("/shipping/quote")) {
    counts.quote++; if (outage) return reply(503, { error: "Synthetic outage" });
    return reply(200, { routeType: "Synthetic", totalFeeAmount: 33000, baseFeeAmount: 33000, extraWeightFeeAmount: 0, insuranceFeeAmount: 0, currency: "VND" });
  }
  if (url.pathname === "/api/v1/partner/shipments" && request.method === "POST") {
    counts.create++;
    const idempotency = request.headers["idempotency-key"];
    if (!idempotency || !body.externalOrderId || idempotency !== body.externalOrderId) return reply(400, {});
    if (!shipments.has(idempotency)) shipments.set(idempotency, { shipmentId: randomUUID(), externalOrderId: body.externalOrderId, trackingCode: `SYN-${randomUUID().slice(0, 16)}`, status: "Draft", shippingFeeAmount: 43000, currency: "VND" });
    return reply(200, shipments.get(idempotency));
  }
  const trackingCode = url.pathname.split("/")[5];
  const shipment = [...shipments.values()].find(x => x.trackingCode === trackingCode);
  if (shipment) {
    if (url.pathname.endsWith("/cancel")) { counts.cancel++; shipment.status = "Cancelled"; }
    else counts.tracking++;
    return reply(200, { ...shipment, timeline: [{ status: shipment.status, note: "Synthetic provider event", changedAtUtc: new Date().toISOString() }] });
  }
  reply(404, {});
}).listen(8081, "0.0.0.0");
