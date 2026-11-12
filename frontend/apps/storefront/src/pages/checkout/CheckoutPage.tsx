import { bankTransferConfigured } from "../../services/storefrontInfo";
import storefrontI18n from "../../i18n";
import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import type {
  CartItemDto,
  CheckoutCouponValidationResponse,
  CheckoutRequest,
} from "@workspace-ecommerce/api-types";
import { ApiClientError } from "@workspace-ecommerce/api-client";
import { useTranslation } from "react-i18next";
import type {
  InputHTMLAttributes,
  ReactNode,
  SelectHTMLAttributes,
  TextareaHTMLAttributes,
} from "react";
import { useEffect, useState } from "react";
import { useForm, useWatch } from "react-hook-form";
import { Link, useNavigate } from "react-router-dom";
import { z } from "zod";
import { VIETNAM_ADMINISTRATIVE_UNITS } from "../../constants/vietnamAdministrativeUnits";
import { useStorefrontCart } from "../../features/cart/StorefrontCartContext";
import { useCustomerAuth } from "../../features/customer-auth/useCustomerAuth";
import { buildManualTransferContent } from "../../features/checkout/manualTransfer";
import { getApiErrorMessage } from "../../services/api/errors";
import { storefrontApi } from "../../services/api/storefrontApi";
import { useShippingSelection } from "../../features/checkout/useShippingSelection";
import { ShippingSelection } from "../../features/checkout/ShippingSelection";
import { isShippingQuoteCurrent } from "../../features/checkout/shippingQuoteState";

const checkoutSchema = () => z.object({
  customerName: z.string().min(2, storefrontI18n.t("uiCopy.please_enter_your_full_name")),
  customerPhone: z.string().min(8, storefrontI18n.t("uiCopy.invalid_phone_number")),
  customerEmail: z
    .string()
    .email(storefrontI18n.t("uiCopy.invalid_email"))
    .optional()
    .or(z.literal("")),
  addressLine: z.string().min(5, storefrontI18n.t("uiCopy.please_enter_your_shipping_address")),
  ward: z.string().min(1, storefrontI18n.t("uiCopy.please_select_a_ward_commune")),
  city: z.string().min(1, storefrontI18n.t("uiCopy.please_select_a_province_city")),
  note: z.string().optional(),
  paymentMethod: z.enum(["0", "1", "2"]),
});

type CheckoutFormValues = z.infer<ReturnType<typeof checkoutSchema>>;
type CheckoutTextField = "customerName" | "customerPhone" | "customerEmail";
type CouponValidationMode = "apply" | "revalidate";

interface FieldProps {
  label: string;
  error?: string;
  required?: boolean;
}

interface InputProps
  extends FieldProps,
    InputHTMLAttributes<HTMLInputElement> {}

interface SelectProps
  extends FieldProps,
    SelectHTMLAttributes<HTMLSelectElement> {
  children: ReactNode;
}

interface TextAreaProps
  extends FieldProps,
    TextareaHTMLAttributes<HTMLTextAreaElement> {}

export function CheckoutPage() {
  useTranslation();
  const { t } = useTranslation("shipping");
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { cartQueryKey, cartSessionId, openCartDrawer, resetCartSession } =
    useStorefrontCart();
  const { customer } = useCustomerAuth();
  const [couponCodeInput, setCouponCodeInput] = useState("");
  const [appliedCoupon, setAppliedCoupon] =
    useState<CheckoutCouponValidationResponse | null>(null);
  const [couponNotice, setCouponNotice] = useState<{
    type: "success" | "error" | "info";
    message: string;
  } | null>(null);
  const [lastValidatedCartSignature, setLastValidatedCartSignature] = useState<
    string | null
  >(null);
  const [attempt, setAttempt] = useState<CheckoutRequest | null>(null);

  const cartQuery = useQuery({
    queryKey: cartQueryKey,
    queryFn: () => storefrontApi.getCart(cartSessionId),
  });
  const cart = cartQuery.data;
  const hasCartItems = Boolean(cart && cart.items.length > 0);
  const cartSignature = cart
    ? cart.items
        .map((item) => `${item.id}:${item.quantity}:${item.lineTotal}`)
        .join("|")
    : "";

  const {
    register,
    handleSubmit,
    getValues,
    setValue,
    control,
    formState: { errors },
  } = useForm<CheckoutFormValues>({
    resolver: zodResolver(checkoutSchema()),
    defaultValues: {
      customerName: "",
      customerPhone: "",
      customerEmail: "",
      addressLine: "",
      ward: "",
      city: "",
      note: "",
      paymentMethod: "0",
    },
  });

  const [addressLine, wardCode, selectedProvinceCode, selectedPaymentMethod] =
    useWatch({
      control,
      name: ["addressLine", "ward", "city", "paymentMethod"],
    });
  const selectedProvince = findProvinceByCode(selectedProvinceCode);
  const wardOptions = selectedProvince?.wards ?? [];
  const selectedWard = wardOptions.find((item) => item.code === wardCode);
  const isManualTransfer = selectedPaymentMethod === "1";

  const streetName = addressLine ?? "";
  const wardName = selectedWard?.name ?? "";
  const provinceName = selectedProvince?.name ?? "";

  const isAddressComplete = Boolean(
    streetName.length >= 5 && wardName && provinceName,
  );
  const shipping = useShippingSelection(
    hasCartItems && isAddressComplete
      ? {
          sessionId: cartSessionId,
          shippingStreet: streetName,
          provinceCode: selectedProvinceCode,
          wardCode,
          paymentMethod: Number(selectedPaymentMethod),
          couponCode: appliedCoupon?.couponCode ?? null,
        }
      : null,
    cartSignature,
    customer?.id ?? "guest",
  );
  const legacyShipping = shipping.capabilities.isSuccess && !shipping.enabled;

  const shippingQuoteQuery = useQuery({
    queryKey: [
      "shipping-quote",
      cartSessionId,
      streetName,
      wardName,
      provinceName,
      cartSignature,
    ],
    queryFn: () =>
      storefrontApi.getShippingQuote({
        sessionId: cartSessionId,
        street: streetName,
        ward: wardName,
        province: provinceName,
      }),
    enabled: hasCartItems && isAddressComplete && legacyShipping,
    retry: false,
  });

  const shippingQuote = shippingQuoteQuery.data;
  const shippingFee = shipping.enabled
    ? shipping.valid
      ? shipping.quote.data!.customerCharge
      : null
    : shippingQuoteQuery.isError
      ? null
      : (shippingQuote?.totalFeeAmount ?? null);
  const subtotal = cart?.totalAmount ?? 0;
  const discount = appliedCoupon?.discountAmount ?? 0;
  const displayTotalAmount =
    shippingFee === null ? null : subtotal + shippingFee - discount;

  useEffect(() => {
    setValue("ward", "", {
      shouldDirty: Boolean(selectedProvinceCode),
      shouldValidate: Boolean(selectedProvinceCode),
    });
  }, [selectedProvinceCode, setValue]);

  useEffect(() => {
    if (!customer) {
      return;
    }

    const setTextIfEmpty = (
      name: CheckoutTextField,
      value: string | null | undefined,
    ) => {
      if (!value || (getValues(name) ?? "").trim().length > 0) {
        return;
      }

      setValue(name, value, { shouldDirty: false, shouldValidate: false });
    };

    setTextIfEmpty("customerName", customer.fullName);
    setTextIfEmpty("customerPhone", customer.phoneNumber);
    setTextIfEmpty("customerEmail", customer.email);
  }, [customer, getValues, setValue]);

  const updateQuantityMutation = useMutation({
    mutationFn: ({ itemId, quantity }: { itemId: string; quantity: number }) =>
      storefrontApi.updateCartItem(itemId, {
        sessionId: cartSessionId,
        quantity,
      }),
    onSuccess: (nextCart) => {
      queryClient.setQueryData(cartQueryKey, nextCart);
    },
  });

  const validateCouponMutation = useMutation({
    mutationFn: ({ code }: { code: string; mode: CouponValidationMode }) =>
      storefrontApi.validateCheckoutCoupon({
        sessionId: cartSessionId,
        couponCode: code,
      }),
    onSuccess: (response, variables) => {
      setAppliedCoupon(response);
      setCouponCodeInput(response.couponCode);
      setLastValidatedCartSignature(cartSignature);
      setCouponNotice({
        type: "success",
        message:
          variables.mode === "revalidate"
            ? `Coupon ${response.couponCode} has been updated according to the new cart.`
            : response.message,
      });
    },
    onError: (error, variables) => {
      setAppliedCoupon(null);
      setLastValidatedCartSignature(null);
      setCouponNotice({
        type: "error",
        message:
          variables.mode === "revalidate"
            ? `The discount code has been removed because the cart is no longer eligible. ${getApiErrorMessage(error)}`
            : getApiErrorMessage(error),
      });
    },
  });

  const checkoutMutation = useMutation({
    mutationFn: async (values: CheckoutFormValues) => {
      if (attempt) return storefrontApi.checkout(attempt);
      if (
        shipping.enabled &&
        (!shipping.confirmed ||
          !isShippingQuoteCurrent(shipping.quote.data, Date.now()))
      )
        throw new Error(t("unknown"));
      if (!shipping.enabled && (!legacyShipping || !shippingQuote))
        throw new Error(t("unknown"));
      const request: CheckoutRequest = {
        sessionId: cartSessionId,
        customerName: values.customerName,
        customerPhone: values.customerPhone,
        customerEmail: values.customerEmail || null,
        shippingAddress: formatShippingAddress(values),
        shippingStreet: values.addressLine,
        shippingWard:
          findProvinceByCode(values.city)?.wards.find(
            (item) => item.code === values.ward,
          )?.name ?? "",
        shippingProvince: findProvinceByCode(values.city)?.name ?? "",
        note: formatOrderNote(values),
        couponCode: appliedCoupon?.couponCode ?? null,
        paymentMethod: Number(values.paymentMethod) as 0 | 1 | 2,
        ...(shipping.enabled
          ? {
              shippingQuoteId: shipping.quote.data!.id,
              shippingMethod: shipping.method!,
              shippingProvinceCode: values.city,
              shippingWardCode: values.ward,
            }
          : {}),
      };
      if (shipping.enabled) setAttempt(request);
      return storefrontApi.checkout(request);
    },
    onError: (error) => {
      if (
        error instanceof ApiClientError &&
        error.statusCode >= 400 &&
        error.statusCode < 500
      ) {
        setAttempt(null);
        if (shipping.enabled) shipping.refresh();
      }
    },
    onSuccess: (response) => {
      resetCartSession();
      if (response.paymentRequired && response.paymentUrl) {
        window.location.assign(response.paymentUrl);
        return;
      }

      const query = new URLSearchParams({
        orderCode: response.order.orderCode,
        phone: response.order.customerPhone,
      });
      void navigate(`/checkout/success?${query.toString()}`, {
        state: { order: response.order },
      });
    },
  });

  if (appliedCoupon && cart && !hasCartItems) {
    setAppliedCoupon(null);
    setLastValidatedCartSignature(null);
    setCouponNotice({
      type: "info",
      message: storefrontI18n.t("uiCopy.the_discount_code_has_been_removed_because_the_cart_is_empty"),
    });
  }

  useEffect(() => {
    if (!appliedCoupon) {
      return;
    }

    if (
      lastValidatedCartSignature !== null &&
      cartSignature !== lastValidatedCartSignature &&
      !validateCouponMutation.isPending
    ) {
      validateCouponMutation.mutate({
        code: appliedCoupon.couponCode,
        mode: "revalidate",
      });
    }
  }, [
    appliedCoupon,
    cartSignature,
    hasCartItems,
    lastValidatedCartSignature,
    validateCouponMutation,
  ]);

  function handleCouponCodeChange(value: string) {
    setCouponCodeInput(value);

    if (
      appliedCoupon &&
      value.trim().toUpperCase() !== appliedCoupon.couponCode
    ) {
      setAppliedCoupon(null);
      setLastValidatedCartSignature(null);
      setCouponNotice({
        type: "info",
        message: storefrontI18n.t("uiCopy.discount_code_removed_click_apply_to_check_the_new_code"),
      });
    }
  }

  function handleApplyCoupon() {
    const code = couponCodeInput.trim();
    if (!code) {
      setCouponNotice({
        type: "error",
        message: storefrontI18n.t("uiCopy.please_enter_a_discount_code"),
      });
      return;
    }

    validateCouponMutation.mutate({ code, mode: "apply" });
  }

  function handleRemoveCoupon() {
    setAppliedCoupon(null);
    setCouponCodeInput("");
    setLastValidatedCartSignature(null);
    setCouponNotice({
      type: "info",
      message: storefrontI18n.t("uiCopy.discount_code_has_been_removed_from_the_order"),
    });
  }

  return (
    <section className="-mt-8 ml-[calc(50%-50vw)] min-h-[calc(100vh-5rem)] w-screen bg-[#f6f6f6] px-5 py-10 sm:px-8 lg:py-14">
      <div className="mx-auto grid max-w-[1440px] gap-8">
        {shipping.capabilities.isError && <div role="alert" className="rounded-xl bg-red-50 p-5 text-red-800">
          {t("capabilitiesError")}
          <button type="button" onClick={() => void shipping.capabilities.refetch()} className="ml-3 min-h-11 underline">{t("retry")}</button>
        </div>}
        <div className="max-w-3xl">
          <h1 className="text-4xl font-black tracking-tight text-slate-900 sm:text-5xl">{storefrontI18n.t("uiCopy.checkout")}</h1>
          <p className="mt-6 max-w-2xl text-base leading-7 text-slate-800">{storefrontI18n.t("uiCopy.enter_your_shipping_information_and_select_a_payment_method")}</p>
        </div>

        {customer ? (
          <div className="rounded-lg border border-emerald-100 bg-emerald-50 px-5 py-4 text-sm font-semibold text-emerald-800">{storefrontI18n.t("uiCopy.signed_in_as")}<span className="font-black">{customer.email}</span>.
            Contact fields have been prefilled for you.
          </div>
        ) : null}

        {cartQuery.isLoading ? (
          <StateMessage>{storefrontI18n.t("uiCopy.loading_cart")}</StateMessage>
        ) : null}

        {cartQuery.isError ? (
          <StateMessage tone="error">
            {getApiErrorMessage(cartQuery.error)} -{" "}
            <button
              type="button"
              onClick={() => openCartDrawer()}
              className="underline"
            >{storefrontI18n.t("uiCopy.open_cart")}</button>
          </StateMessage>
        ) : null}

        {attempt && checkoutMutation.isError && (
          <div role="alert">
            <p>{t("uncertain")}</p>
            <button
              type="button"
              className="underline"
              disabled={checkoutMutation.isPending}
              onClick={() => checkoutMutation.mutate(getValues())}
            >
              {t("retryOrder")}
            </button>
          </div>
        )}

        {!attempt &&
        !cartQuery.isLoading &&
        !cartQuery.isError &&
        cart &&
        cart.items.length === 0 ? (
          <EmptyCheckoutState onOpenCart={() => openCartDrawer()} />
        ) : null}

        {hasCartItems && cart ? (
          <form
            onSubmit={handleSubmit((values) => checkoutMutation.mutate(values))}
            className="grid gap-8 lg:grid-cols-[minmax(0,1fr)_440px] lg:items-start"
          >
            <fieldset
              disabled={checkoutMutation.isPending || !!attempt}
              className="grid min-w-0 gap-5"
            >
              <section>
                <h2 className="mb-4 text-lg font-black text-slate-950">{storefrontI18n.t("uiCopy.shipping_information")}</h2>
                <div className="rounded-lg bg-white p-5 shadow-sm sm:p-7">
                  <div className="grid gap-4 sm:grid-cols-2">
                    <FormInput
                      placeholder={storefrontI18n.t("uiCopy.full_name")}
                      autoComplete="name"
                      {...register("customerName")}
                      error={errors.customerName?.message}
                      label={storefrontI18n.t("uiCopy.full_name")}
                    />
                    <FormInput
                      placeholder={storefrontI18n.t("uiCopy.phone_number")}
                      type="tel"
                      autoComplete="tel"
                      {...register("customerPhone")}
                      error={errors.customerPhone?.message}
                      label={storefrontI18n.t("uiCopy.phone_number")}
                    />
                    <FormInput
                      placeholder={storefrontI18n.t("uiCopy.email")}
                      type="email"
                      autoComplete="email"
                      {...register("customerEmail")}
                      error={errors.customerEmail?.message}
                      label={storefrontI18n.t("uiCopy.email")}
                    />
                    <div className="sm:col-span-2">
                      <FormInput
                        placeholder={storefrontI18n.t("uiCopy.address")}
                        autoComplete="street-address"
                        {...register("addressLine")}
                        error={errors.addressLine?.message}
                        label={storefrontI18n.t("uiCopy.address")}
                      />
                    </div>
                    <FormSelect
                      {...register("city")}
                      error={errors.city?.message}
                      label={storefrontI18n.t("uiCopy.province_city")}
                    >
                      <option value="">{storefrontI18n.t("uiCopy.select_province_city")}</option>
                      {VIETNAM_ADMINISTRATIVE_UNITS.map((province) => (
                        <option key={province.code} value={province.code}>
                          {province.name}
                        </option>
                      ))}
                    </FormSelect>
                    <FormSelect
                      {...register("ward")}
                      disabled={!selectedProvince}
                      error={errors.ward?.message}
                      label={storefrontI18n.t("uiCopy.ward_commune")}
                    >
                      <option value="">
                        {selectedProvince
                          ? storefrontI18n.t("uiCopy.select_ward_commune")
                          : storefrontI18n.t("uiCopy.select_province_city_first")}
                      </option>
                      {wardOptions.map((ward) => (
                        <option key={ward.code} value={ward.code}>
                          {ward.name}
                        </option>
                      ))}
                    </FormSelect>
                  </div>
                </div>
              </section>

              <section>
                <h2 className="mb-4 text-lg font-black text-slate-950">{storefrontI18n.t("uiCopy.order_notes")}</h2>
                <FormTextArea
                  rows={4}
                  placeholder={storefrontI18n.t("uiCopy.e_g_deliver_during_business_hours_call_30_minutes_before_arrival")}
                  {...register("note")}
                  error={errors.note?.message}
                  label={storefrontI18n.t("uiCopy.order_notes")}
                />
              </section>

              <section className="rounded-lg bg-white p-5 shadow-sm sm:p-6">
                <h2 className="mb-4 text-lg font-black text-slate-950">{storefrontI18n.t("uiCopy.payment_method")}</h2>
                <div className="grid gap-3 sm:grid-cols-3">
                  <PaymentOption
                    value="0"
                    label={storefrontI18n.t("uiCopy.cod")}
                    description={storefrontI18n.t("uiCopy.pay_on_delivery")}
                    register={register("paymentMethod")}
                  />
                  <PaymentOption
                    value="1"
                    disabled={!bankTransferConfigured}
                    label={storefrontI18n.t("uiCopy.bank_transfer")}
                    description={bankTransferConfigured ? storefrontI18n.t("bank.title") : storefrontI18n.t("bank.missing")}
                    register={register("paymentMethod")}
                  />
                  <PaymentOption
                    value="2"
                    label="VNPay"
                    description={storefrontI18n.t("uiCopy.pay_online_via_sandbox_gateway")}
                    register={register("paymentMethod")}
                  />
                </div>
                {isManualTransfer ? <ManualTransferNotice /> : null}
              </section>
              <ShippingSelection
                state={shipping}
                addressComplete={isAddressComplete}
                disabled={checkoutMutation.isPending || !!attempt}
              />
            </fieldset>

            <OrderSummary
              appliedCoupon={appliedCoupon}
              cartItems={cart.items}
              couponCodeInput={couponCodeInput}
              couponNotice={couponNotice}
              isCouponPending={validateCouponMutation.isPending}
              isPending={
                checkoutMutation.isPending ||
                !!attempt ||
                updateQuantityMutation.isPending ||
                validateCouponMutation.isPending
              }
              mutationError={
                checkoutMutation.error ?? updateQuantityMutation.error
              }
              onApplyCoupon={handleApplyCoupon}
              onCouponCodeChange={handleCouponCodeChange}
              onRemoveCoupon={handleRemoveCoupon}
              onUpdateQuantity={(itemId, quantity) =>
                updateQuantityMutation.mutate({ itemId, quantity })
              }
              paymentMethod={selectedPaymentMethod}
              subtotal={cart.totalAmount}
              totalAmount={displayTotalAmount}
              shippingFee={shippingFee}
              isShippingQuoteLoading={
                shippingQuoteQuery.isLoading ||
                shipping.quote.isFetching ||
                shipping.capabilities.isLoading
              }
              isAddressComplete={isAddressComplete}
              shippingUnavailable={
                shipping.enabled
                  ? !shipping.confirmed
                  : !legacyShipping ||
                    !shippingQuote ||
                    shippingQuoteQuery.isError
              }
            />
          </form>
        ) : null}
      </div>
    </section>
  );
}

function FormInput({ label, error, className = "", ...props }: InputProps) {
  return (
    <label className="block">
      <span className="mb-2 block text-sm font-semibold text-slate-700">{label}</span>
      <input
        aria-invalid={Boolean(error)}
        aria-describedby={error ? `checkout-${props.name}-error` : undefined}
        aria-label={label}
        className={`h-14 w-full rounded-lg border border-slate-200 bg-white px-5 text-base font-medium text-slate-900 outline-none transition placeholder:text-slate-600 focus:border-slate-500 ${className}`}
        {...props}
      />
      <FieldError id={`checkout-${props.name}-error`} message={error} />
    </label>
  );
}

function FormSelect({
  label,
  error,
  children,
  className = "",
  ...props
}: SelectProps) {
  return (
    <label className="block">
      <span className="mb-2 block text-sm font-semibold text-slate-700">{label}</span>
      <select
        aria-invalid={Boolean(error)}
        aria-describedby={error ? `checkout-${props.name}-error` : undefined}
        aria-label={label}
        className={`h-14 w-full rounded-lg border border-slate-200 bg-white px-5 text-base font-medium text-slate-600 outline-none transition focus:border-slate-500 ${className}`}
        {...props}
      >
        {children}
      </select>
      <FieldError id={`checkout-${props.name}-error`} message={error} />
    </label>
  );
}

function FormTextArea({
  label,
  error,
  className = "",
  ...props
}: TextAreaProps) {
  return (
    <label className="block rounded-lg bg-white shadow-sm">
      <span className="mb-2 block text-sm font-semibold text-slate-700">{label}</span>
      <textarea
        aria-invalid={Boolean(error)}
        aria-describedby={error ? `checkout-${props.name}-error` : undefined}
        aria-label={label}
        className={`w-full resize-none rounded-lg border border-white bg-white px-5 py-4 text-base font-medium text-slate-900 outline-none transition placeholder:text-slate-600 focus:border-slate-300 ${className}`}
        {...props}
      />
      <FieldError id={`checkout-${props.name}-error`} message={error} />
    </label>
  );
}

function FieldError({ message, id }: { message?: string; id?: string }) {
  if (!message) {
    return null;
  }

  return <p id={id} role="alert" className="mt-1 text-sm font-semibold text-red-700">{message}</p>;
}

function PaymentOption({
  description,
  disabled = false,
  label,
  register,
  value,
}: {
  description: string;
  disabled?: boolean;
  label: string;
  register: ReturnType<typeof useForm<CheckoutFormValues>>["register"] extends (
    name: "paymentMethod",
  ) => infer R
    ? R
    : never;
  value: "0" | "1" | "2";
}) {
  return (
    <label className="flex cursor-pointer items-start gap-3 rounded-lg border border-slate-200 p-4 transition has-[:disabled]:cursor-not-allowed has-[:disabled]:bg-slate-100 has-[:checked]:border-slate-950 has-[:checked]:bg-slate-50">
      <input
        type="radio"
        disabled={disabled}
        value={value}
        className="mt-1 accent-slate-950"
        {...register}
      />
      <span>
        <span className="block font-bold text-slate-950">{label}</span>
        <span className="mt-1 block text-sm text-slate-600">{description}</span>
      </span>
    </label>
  );
}

function ManualTransferNotice() {
  return (
    <div className="mt-4 rounded-lg border border-slate-200 bg-slate-50 p-4 text-sm text-slate-700">
      <p className="font-bold text-slate-950">{storefrontI18n.t("uiCopy.manual_bank_transfer")}</p>
      <p className="mt-1 leading-6">
        After placing the order, the system will generate an order code and
        transfer details for copying. Our staff will confirm your order once the
        payment is received.
      </p>
      <p className="mt-3 font-mono text-xs font-bold uppercase tracking-[0.14em] text-slate-600">
        Sample reference: {buildManualTransferContent("ORDER-CODE")}
      </p>
    </div>
  );
}

function OrderSummary({
  appliedCoupon,
  cartItems,
  couponCodeInput,
  couponNotice,
  isCouponPending,
  isPending,
  mutationError,
  onApplyCoupon,
  onCouponCodeChange,
  onRemoveCoupon,
  onUpdateQuantity,
  paymentMethod,
  subtotal,
  totalAmount,
  shippingFee,
  isShippingQuoteLoading,
  isAddressComplete,
  shippingUnavailable,
}: {
  appliedCoupon: CheckoutCouponValidationResponse | null;
  cartItems: CartItemDto[];
  couponCodeInput: string;
  couponNotice: { type: "success" | "error" | "info"; message: string } | null;
  isCouponPending: boolean;
  isPending: boolean;
  mutationError: unknown;
  onApplyCoupon: () => void;
  onCouponCodeChange: (value: string) => void;
  onRemoveCoupon: () => void;
  onUpdateQuantity: (itemId: string, quantity: number) => void;
  paymentMethod: "0" | "1" | "2";
  subtotal: number;
  totalAmount: number | null;
  shippingFee: number | null;
  isShippingQuoteLoading: boolean;
  isAddressComplete: boolean;
  shippingUnavailable: boolean;
}) {
  const { t } = useTranslation("shipping");
  const submitLabel =
    paymentMethod === "1"
      ? storefrontI18n.t("uiCopy.place_order_and_get_transfer_info")
      : paymentMethod === "2"
        ? storefrontI18n.t("uiCopy.continue_to_vnpay")
        : storefrontI18n.t("uiCopy.place_order_cod");

  return (
    <aside className="lg:sticky lg:top-28">
      <h2 className="mb-4 text-lg font-black text-slate-950">{storefrontI18n.t("uiCopy.order_summary")}</h2>
      <div className="rounded-lg bg-white p-5 shadow-sm sm:p-7">
        <div className="grid gap-5">
          {cartItems.map((item) => (
            <CheckoutCartItem
              key={item.id}
              isPending={isPending}
              item={item}
              onUpdateQuantity={onUpdateQuantity}
            />
          ))}
        </div>

        <div className="mt-5 border-t border-slate-200 pt-5">
          <div className="flex min-h-12 items-center rounded-full border border-slate-200 bg-white pl-5 pr-1.5 text-slate-700 focus-within:border-slate-500">
            <input
              value={couponCodeInput}
              aria-label={t("couponCode")}
              onChange={(event) =>
                onCouponCodeChange(event.currentTarget.value)
              }
              onKeyDown={(event) => {
                if (event.key === "Enter") {
                  event.preventDefault();
                  onApplyCoupon();
                }
              }}
              placeholder={storefrontI18n.t("uiCopy.enter_discount_code")}
              disabled={isPending}
              className="min-w-0 flex-1 bg-transparent text-sm font-bold uppercase outline-none placeholder:normal-case placeholder:font-medium placeholder:text-slate-600 disabled:cursor-not-allowed"
            />
            {appliedCoupon ? (
              <button
                type="button"
                disabled={isPending}
                onClick={onRemoveCoupon}
                className="mr-1 rounded-full px-3 py-2 text-xs font-black text-slate-600 transition hover:bg-slate-100 hover:text-slate-950 disabled:cursor-not-allowed disabled:opacity-60"
              >{storefrontI18n.t("uiCopy.remove")}</button>
            ) : null}
            <button
              type="button"
              className="grid h-9 w-9 place-items-center rounded-full bg-slate-950 text-white transition hover:bg-slate-700 disabled:cursor-not-allowed disabled:bg-slate-200"
              aria-label={storefrontI18n.t("uiCopy.apply_discount_code")}
              disabled={isPending || couponCodeInput.trim().length === 0}
              onClick={onApplyCoupon}
            >
              <svg
                className="h-5 w-5"
                viewBox="0 0 24 24"
                fill="none"
                aria-hidden="true"
              >
                <path
                  d="M5 12h14m-6-6 6 6-6 6"
                  stroke="currentColor"
                  strokeWidth="2.5"
                  strokeLinecap="round"
                  strokeLinejoin="round"
                />
              </svg>
            </button>
          </div>
          {couponNotice ? (
            <CouponMessage type={couponNotice.type}>
              {couponNotice.message}
            </CouponMessage>
          ) : null}
          {appliedCoupon ? (
            <div className="mt-3 rounded-lg border border-emerald-100 bg-emerald-50 px-4 py-3 text-sm font-semibold text-emerald-800">
              <div className="flex items-center justify-between gap-3">
                <span className="font-black">{appliedCoupon.couponCode}</span>
                <span>-{formatCartMoney(appliedCoupon.discountAmount)}</span>
              </div>
              <p className="mt-1 text-xs text-emerald-700">
                Eligible subtotal:{" "}
                {formatCartMoney(appliedCoupon.eligibleSubtotal)}
              </p>
            </div>
          ) : null}
          {isCouponPending ? (
            <CouponMessage type="info">{storefrontI18n.t("uiCopy.checking_discount_code")}</CouponMessage>
          ) : null}
        </div>

        <div className="mt-5 grid gap-4 text-sm text-slate-600">
          <SummaryLine
            label={storefrontI18n.t("uiCopy.subtotal")}
            value={formatCartMoney(subtotal, false)}
            strong
          />
          <SummaryLine
            label={storefrontI18n.t("uiCopy.shipping_fee")}
            value={
              isShippingQuoteLoading
                ? "Calculating..."
                : shippingFee !== null
                  ? formatCartMoney(shippingFee, false)
                  : isAddressComplete
                    ? t("unknown")
                    : t("address")
            }
          />
          {appliedCoupon ? (
            <SummaryLine
              label={`Discount (${appliedCoupon.couponCode})`}
              value={`-${formatCartMoney(appliedCoupon.discountAmount, false)}`}
              tone="success"
            />
          ) : null}
          <div className="border-t border-slate-200 pt-5">
            <SummaryLine
              label={storefrontI18n.t("uiCopy.total_vnd")}
              value={
                totalAmount === null
                  ? t("unknown")
                  : formatCartMoney(totalAmount, false)
              }
              strong
            />
          </div>
        </div>

        {mutationError ? (
          <div className="mt-5 rounded-lg bg-red-50 px-4 py-3 text-sm font-semibold text-red-700">
            {getApiErrorMessage(mutationError)}
          </div>
        ) : null}

        <button
          type="submit"
          disabled={isPending || isShippingQuoteLoading || shippingUnavailable}
          className="mt-8 min-h-14 w-full rounded-full bg-slate-950 px-6 py-3 text-base font-black text-white transition hover:bg-slate-950 disabled:cursor-not-allowed disabled:opacity-60"
        >
          {isPending ? "Processing..." : submitLabel}
        </button>
      </div>
    </aside>
  );
}

function CheckoutCartItem({
  isPending,
  item,
  onUpdateQuantity,
}: {
  isPending: boolean;
  item: CartItemDto;
  onUpdateQuantity: (itemId: string, quantity: number) => void;
}) {
  const updateQuantity = (quantity: number) => {
    if (
      !Number.isFinite(quantity) ||
      quantity < 1 ||
      quantity === item.quantity
    ) {
      return;
    }

    onUpdateQuantity(item.id, quantity);
  };

  return (
    <article className="grid grid-cols-[104px_minmax(0,1fr)] gap-4">
      <div className="grid aspect-square place-items-center overflow-hidden rounded-md bg-[#f1f1f1]">
        {item.imageUrl ? (
          <img
            src={item.imageUrl}
            alt={item.productName}
            className="h-full w-full object-contain p-3"
          />
        ) : (
          <svg
            className="h-10 w-10 text-slate-600"
            viewBox="0 0 24 24"
            fill="none"
            aria-hidden="true"
          >
            <path d="M4 8h16v11H4z" stroke="currentColor" strokeWidth="1.7" />
            <path
              d="M8 8a4 4 0 0 1 8 0"
              stroke="currentColor"
              strokeWidth="1.7"
              strokeLinecap="round"
            />
          </svg>
        )}
      </div>
      <div className="min-w-0 py-1">
        <p className="truncate text-sm text-slate-600">{item.productName}</p>
        <h3 className="mt-1 line-clamp-2 text-sm font-black leading-5 text-slate-800">
          {item.variantName}
        </h3>
        <p className="mt-2 text-sm font-semibold text-slate-600">
          {formatCartMoney(item.unitPriceSnapshot, false)}
        </p>
        {formatVariantOptions(item) ? (
          <p className="mt-2 truncate text-sm text-slate-600">
            {formatVariantOptions(item)}
          </p>
        ) : null}
        <div className="mt-3 flex flex-wrap items-center justify-between gap-3">
          <div className="inline-flex h-10 items-center overflow-hidden rounded-lg border border-slate-200 bg-white">
            <button
              type="button"
              disabled={isPending || item.quantity <= 1}
              onClick={() => updateQuantity(item.quantity - 1)}
              className="grid h-10 w-9 place-items-center text-slate-600 transition hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-40"
              aria-label={storefrontI18n.t("uiCopy.decrease_quantity")}
            >
              -
            </button>
            <input
              type="number"
              min={1}
              inputMode="numeric"
              value={item.quantity}
              disabled={isPending}
              onChange={(event) => updateQuantity(Number(event.target.value))}
              className="h-10 w-12 border-x border-slate-200 text-center text-sm font-bold text-slate-700 outline-none disabled:bg-slate-50"
              aria-label={storefrontI18n.t("uiCopy.quantity")}
            />
            <button
              type="button"
              disabled={isPending}
              onClick={() => updateQuantity(item.quantity + 1)}
              className="grid h-10 w-9 place-items-center text-slate-600 transition hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-40"
              aria-label={storefrontI18n.t("uiCopy.increase_quantity")}
            >
              +
            </button>
          </div>
          <p className="text-sm font-black text-slate-800">
            {formatCartMoney(item.lineTotal, false)}
          </p>
        </div>
      </div>
    </article>
  );
}

function EmptyCheckoutState({ onOpenCart }: { onOpenCart: () => void }) {
  return (
    <section className="rounded-lg border border-dashed border-slate-200 bg-white p-8 text-center shadow-sm">
      <h2 className="text-2xl font-black text-slate-950">{storefrontI18n.t("uiCopy.your_cart_is_empty")}</h2>
      <p className="mx-auto mt-3 max-w-xl text-sm leading-6 text-slate-600">
        Your cart session might have expired or has already been paid for. Check
        your active cart or go back to the product catalog to continue shopping.
      </p>
      <div className="mt-6 flex flex-wrap justify-center gap-3">
        <button
          type="button"
          onClick={onOpenCart}
          className="ui-control rounded-[var(--radius-control)] border border-slate-200 px-5 py-3 text-slate-700 transition hover:border-slate-950 hover:text-slate-950"
        >{storefrontI18n.t("uiCopy.open_cart")}</button>
        <Link
          to="/products"
          className="ui-control rounded-[var(--radius-control)] bg-slate-950 px-5 py-3 text-white transition hover:bg-slate-800"
        >{storefrontI18n.t("uiCopy.continue_shopping")}</Link>
      </div>
    </section>
  );
}

function CouponMessage({
  children,
  type,
}: {
  children: ReactNode;
  type: "success" | "error" | "info";
}) {
  const className =
    type === "error"
      ? "bg-red-50 text-red-700"
      : type === "success"
        ? "bg-emerald-50 text-emerald-700"
        : "bg-slate-50 text-slate-600";

  return (
    <div
      className={`mt-3 rounded-lg px-4 py-3 text-sm font-semibold ${className}`}
    >
      {children}
    </div>
  );
}

function SummaryLine({
  label,
  strong = false,
  tone = "muted",
  value,
}: {
  label: string;
  strong?: boolean;
  tone?: "muted" | "success";
  value: string;
}) {
  return (
    <div className="flex items-center justify-between gap-4">
      <span
        className={tone === "success" ? "text-emerald-600" : "text-slate-600"}
      >
        {label}
      </span>
      <span
        className={
          tone === "success"
            ? "font-black text-emerald-600"
            : strong
              ? "font-black text-slate-800"
              : "font-medium text-slate-600"
        }
      >
        {value}
      </span>
    </div>
  );
}

function StateMessage({
  children,
  tone = "info",
}: {
  children: ReactNode;
  tone?: "info" | "error";
}) {
  return (
    <div
      className={`rounded-lg px-5 py-4 text-sm font-semibold ${tone === "error" ? "bg-red-50 text-red-700" : "bg-white text-slate-600"}`}
    >
      {children}
    </div>
  );
}

function formatShippingAddress(values: CheckoutFormValues) {
  const province = findProvinceByCode(values.city);
  const ward = province?.wards.find((item) => item.code === values.ward);

  return [values.addressLine, ward?.name, province?.name]
    .filter(Boolean)
    .join(", ");
}

function formatOrderNote(values: CheckoutFormValues) {
  const notes = [values.note?.trim()].filter(Boolean);

  return notes.length > 0 ? notes.join("\n") : null;
}

function formatVariantOptions(item: CartItemDto) {
  return [item.variantColor, item.variantSize].filter(Boolean).join(" / ");
}

function formatCartMoney(value: number, includeCurrency = true) {
  const formatted = new Intl.NumberFormat("vi-VN", {
    maximumFractionDigits: 0,
  }).format(value);

  return includeCurrency ? `${formatted} VND` : formatted;
}

function findProvinceByCode(code: string) {
  return VIETNAM_ADMINISTRATIVE_UNITS.find(
    (province) => province.code === code,
  );
}
