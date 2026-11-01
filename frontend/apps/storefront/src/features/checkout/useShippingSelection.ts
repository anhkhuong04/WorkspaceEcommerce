import { useCallback, useState, useSyncExternalStore } from "react";
import { useQuery } from "@tanstack/react-query";
import {
  ShippingMethod,
  type ShippingQuoteInput,
} from "@workspace-ecommerce/api-types";
import { storefrontApi } from "../../services/api/storefrontApi";
import {
  isShippingQuoteCurrent,
  shippingContextKey,
} from "./shippingQuoteState";

export function useShippingSelection(
  input: Omit<ShippingQuoteInput, "method"> | null,
  cartSignature: string,
  subject: string,
) {
  const capabilities = useQuery({
    queryKey: ["shipping-capabilities"],
    queryFn: storefrontApi.getShippingCapabilities,
    retry: false,
    staleTime: 60000,
  });
  const context = shippingContextKey(input, cartSignature, subject);
  const [selection, setSelection] = useState<{
    context: string;
    method: ShippingMethod | null;
    confirmation: string | null;
    epoch: string;
  }>({ context, method: null, confirmation: null, epoch: "" });
  // Reset during the prop-change render, before a stale selection can be submitted.
  if (selection.context !== context)
    setSelection({ context, method: null, confirmation: null, epoch: "" });
  const method = selection.context === context ? selection.method : null;
  const enabled = capabilities.data?.enabled === true;
  const options = useQuery({
    queryKey: ["shipping-options", context],
    queryFn: () =>
      storefrontApi.getShippingOptions({
        ...input!,
        method: ShippingMethod.WorkspaceEC,
      }),
    enabled: enabled && !!input,
    retry: false,
    refetchOnWindowFocus: false,
  });
  const quote = useQuery({
    queryKey: ["shipping-offer", context, method, selection.epoch],
    queryFn: () =>
      storefrontApi.issueShippingQuote({ ...input!, method: method! }),
    enabled: enabled && !!input && method !== null,
    retry: false,
    staleTime: Infinity,
    refetchOnWindowFocus: false,
    refetchOnReconnect: false,
  });
  const expiry = Date.parse(quote.data?.expiresAtUtc ?? "");
  const expired = useSyncExternalStore(
    useCallback(
      (onChange) => {
        if (!Number.isFinite(expiry)) return () => {};
        const timer = window.setTimeout(
          onChange,
          Math.max(0, Math.min(expiry - Date.now() + 5, 3600000)),
        );
        return () => window.clearTimeout(timer);
      },
      [expiry],
    ),
    () => !Number.isFinite(expiry) || expiry <= Date.now(),
    () => true,
  );
  const valid =
    enabled &&
    method !== null &&
    !expired &&
    isShippingQuoteCurrent(quote.data, 0);
  const confirmed = valid && selection.confirmation === quote.data?.id;
  return {
    capabilities,
    options,
    quote,
    method,
    enabled,
    confirmed,
    choose: (next: ShippingMethod) =>
      setSelection({
        context,
        method: next,
        confirmation: null,
        epoch: crypto.randomUUID(),
      }),
    confirm: (checked: boolean) =>
      setSelection((previous) => ({
        ...previous,
        confirmation: checked && valid ? quote.data!.id : null,
      })),
    refresh: () =>
      setSelection((previous) => ({
        ...previous,
        confirmation: null,
        epoch: crypto.randomUUID(),
      })),
    valid,
  };
}
