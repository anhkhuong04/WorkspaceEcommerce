const cartSessionStorageKey = "workspace-ecommerce-cart-session";
const defaultDemoSessionId = "demo-checkout-session";

function createSessionId(): string {
  if (typeof crypto !== "undefined" && "randomUUID" in crypto) {
    return crypto.randomUUID();
  }

  return `cart-${Date.now()}-${Math.random().toString(16).slice(2)}`;
}

export function getCartSessionId(): string {
  const storedSessionId = localStorage.getItem(cartSessionStorageKey);
  if (storedSessionId) {
    return storedSessionId;
  }

  const sessionId = import.meta.env.VITE_CART_SESSION_ID || defaultDemoSessionId;
  localStorage.setItem(cartSessionStorageKey, sessionId);
  return sessionId;
}

export function resetCartSessionId(): string {
  const sessionId = createSessionId();
  localStorage.setItem(cartSessionStorageKey, sessionId);
  return sessionId;
}
