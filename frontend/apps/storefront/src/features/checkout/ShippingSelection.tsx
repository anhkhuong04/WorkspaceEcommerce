import { useTranslation } from "react-i18next";
import { ShippingMethod } from "@workspace-ecommerce/api-types";
import { getApiErrorMessage } from "../../services/api/errors";
import type { useShippingSelection } from "./useShippingSelection";

export function ShippingSelection({
  state,
  addressComplete,
  disabled,
}: {
  state: ReturnType<typeof useShippingSelection>;
  addressComplete: boolean;
  disabled: boolean;
}) {
  const { t } = useTranslation("shipping");
  if (state.capabilities.isLoading) return <p role="status">{t("loading")}</p>;
  if (state.capabilities.isError)
    return (
      <p role="alert">
        {t("error")}{" "}
        <button type="button" onClick={() => void state.capabilities.refetch()}>
          {t("retry")}
        </button>
      </p>
    );
  if (!state.enabled) return null;
  return (
    <fieldset className="rounded-lg bg-white p-5 shadow-sm" disabled={disabled}>
      <legend className="font-bold">{t("title")}</legend>
      <p>{addressComplete ? t("select") : t("address")}</p>
      {state.options.isFetching && <p role="status">{t("loading")}</p>}
      {state.options.isError && (
        <p role="alert">
          {t("error")}{" "}
          <button type="button" onClick={() => void state.options.refetch()}>
            {t("retry")}
          </button>
        </p>
      )}
      <div className="grid gap-3 sm:grid-cols-2">
        {state.options.data?.map((option) => (
          <label
            key={option.method}
            className="flex gap-3 rounded border border-slate-300 p-4 has-[:checked]:border-slate-950"
          >
            <input
              type="radio"
              name="shipping-method"
              checked={state.method === option.method}
              disabled={!option.available}
              onChange={() => state.choose(option.method)}
            />
            <span>
              {option.method === ShippingMethod.WorkspaceEC
                ? t("local")
                : t("external")}
              {!option.available && (
                <span className="block text-sm">{t("unavailable")}</span>
              )}
            </span>
          </label>
        ))}
      </div>
      {state.quote.isFetching && <p role="status">{t("loading")}</p>}
      {state.quote.isError && (
        <p role="alert">
          {t("quoteFailed")} {getApiErrorMessage(state.quote.error)}
        </p>
      )}
      {state.quote.data && state.method !== null && (
        <div className="mt-3 grid gap-2">
          <p>
            {state.quote.data.customerCharge.toLocaleString()}{" "}
            {state.quote.data.currency}
          </p>
          <p>
            {t("expires", {
              time: new Date(
                state.quote.data.expiresAtUtc,
              ).toLocaleTimeString(),
            })}
          </p>
          {!state.valid && <p role="alert">{t("expired")}</p>}
          <label className="flex items-center gap-2">
            <input
              type="checkbox"
              checked={state.confirmed}
              disabled={!state.valid}
              onChange={(event) => state.confirm(event.target.checked)}
            />
            {t("confirm")}
          </label>
        </div>
      )}
      {state.method !== null && (
        <button
          type="button"
          className="mt-3 underline"
          disabled={state.quote.isFetching}
          onClick={state.refresh}
        >
          {t("refresh")}
        </button>
      )}
    </fieldset>
  );
}
