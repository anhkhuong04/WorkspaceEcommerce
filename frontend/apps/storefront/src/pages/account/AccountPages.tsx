import { formatOrderStatus, formatPaymentMethod, formatPaymentStatus } from "../../i18n-formatters";
import storefrontI18n from "../../i18n";
import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import type {
  CustomerOrderDto,
  CustomerOrderListItemDto,
  CustomerOrderStatusHistoryDto,
  CustomerWarrantyListItemDto,
  LoyaltyAccountDto,
  LoyaltyTierDto,
  LoyaltyTierType,
  LoyaltyTransactionDto,
  LoyaltyTransactionType,
  OrderStatus,
  PaymentStatus,
  RedeemLoyaltyPointsResponse,
  ShipmentTrackingDto,
  TwoFactorSetupStartResponse
} from "@workspace-ecommerce/api-types";
import { downloadBlob, formatDate, formatMoney } from "@workspace-ecommerce/shared-utils";
import type { ReactNode } from "react";
import { useEffect, useState } from "react";
import { useForm } from "react-hook-form";
import { useTranslation } from "react-i18next";
import { Link, NavLink, useNavigate, useParams } from "react-router-dom";
import { z } from "zod";
import { ShipmentTrackingPanel } from "../../components/shipment/ShipmentTrackingPanel";
import { OrderShippingDetails } from "../../components/shipment/OrderShippingDetails";
import { ReceiptPreviewModal } from "../../components/receipt/ReceiptPreviewModal";
import { useCustomerAuth } from "../../features/customer-auth/useCustomerAuth";
import { getApiErrorMessage } from "../../services/api/errors";
import { storefrontApi } from "../../services/api/storefrontApi";

const profileSchema = () => z.object({
  fullName: z.string().trim().min(2, storefrontI18n.t("uiCopy.full_name_is_required")).max(250, storefrontI18n.t("uiCopy.full_name_is_too_long")),
  phoneNumber: z.string().trim().min(8, storefrontI18n.t("uiCopy.phone_number_is_invalid")).max(50, storefrontI18n.t("uiCopy.phone_number_is_too_long"))
});

type ProfileFormValues = z.infer<ReturnType<typeof profileSchema>>;

const statusStyles: Record<OrderStatus, string> = {
  0: "bg-yellow-100 text-yellow-800",
  1: "bg-blue-100 text-blue-800",
  2: "bg-indigo-100 text-indigo-800",
  3: "bg-violet-100 text-violet-800",
  4: "bg-emerald-100 text-emerald-800",
  5: "bg-red-100 text-red-800",
  6: "bg-slate-100 text-slate-600",
  7: "bg-orange-100 text-orange-800"
};

const paymentStatusStyles: Record<PaymentStatus, string> = {
  0: "bg-slate-100 text-slate-700",
  1: "bg-blue-100 text-blue-800",
  2: "bg-emerald-100 text-emerald-800",
  3: "bg-red-100 text-red-800",
  4: "bg-slate-100 text-slate-700",
  5: "bg-amber-100 text-amber-800",
  6: "bg-emerald-100 text-emerald-800",
  7: "bg-amber-100 text-amber-800"
};

const loyaltyVoucherAmountPerPoint = 1000;
const loyaltyTransactionsPageSize = 10;

const tierStyles: Record<LoyaltyTierType, string> = {
  0: "bg-slate-100 text-slate-700",
  1: "bg-zinc-100 text-zinc-700",
  2: "bg-amber-100 text-amber-800",
  3: "bg-cyan-100 text-cyan-800"
};

export function AccountOverviewPage() {
  useTranslation();
  const { customer } = useCustomerAuth();
  const ordersQuery = useQuery({
    queryKey: ["customer", "orders", "recent", customer?.id],
    queryFn: () => storefrontApi.getCustomerOrders({ pageNumber: 1, pageSize: 3 })
  });

  const recentOrders = ordersQuery.data?.items ?? [];

  return (
    <AccountShell>
      <div className="grid gap-6">
        <section className="grid gap-4 md:grid-cols-3">
          <MetricCard label={storefrontI18n.t("uiCopy.saved_contact")} value={customer?.phoneNumber || storefrontI18n.t("uiCopy.not_set")} />
          <MetricCard label={storefrontI18n.t("uiCopy.orders")} value={ordersQuery.data ? String(ordersQuery.data.totalCount) : "..."} />
          <MetricCard label={storefrontI18n.t("uiCopy.member_since")} value={formatOptionalDate(customer?.createdAt)} />
        </section>

        <section className="grid gap-5 lg:grid-cols-[minmax(0,1fr)_360px]">
          <div className="ui-card border border-slate-100 p-6">
            <div className="flex flex-wrap items-start justify-between gap-3">
              <div>
                <p className="ui-caption uppercase tracking-[0.18em] text-[var(--brand)]">{storefrontI18n.t("uiCopy.profile")}</p>
                <h2 className="ui-h2 mt-2 text-slate-950">{customer?.fullName || storefrontI18n.t("uiCopy.customer_account")}</h2>
              </div>
              <Link to="/account/profile" className="ui-control rounded-[var(--radius-control)] border border-slate-200 px-4 py-2 text-slate-700 transition hover:border-slate-950 hover:text-slate-950">{storefrontI18n.t("uiCopy.edit_profile")}</Link>
            </div>

            <div className="mt-5 grid gap-4 sm:grid-cols-2">
              <InfoBlock label={storefrontI18n.t("uiCopy.email")} value={customer?.email || "-"} />
              <InfoBlock label={storefrontI18n.t("uiCopy.phone")} value={customer?.phoneNumber || "-"} />
              <InfoBlock label={storefrontI18n.t("uiCopy.created")} value={formatOptionalDate(customer?.createdAt)} />
              <InfoBlock label={storefrontI18n.t("uiCopy.last_updated")} value={formatOptionalDate(customer?.updatedAt)} />
            </div>
          </div>

          <aside className="ui-card border border-slate-100 p-6">
            <p className="ui-caption uppercase tracking-[0.18em] text-[var(--brand)]">{storefrontI18n.t("uiCopy.need_help")}</p>
            <div className="mt-4 grid gap-3">
              <Link to="/products" className="ui-control flex h-11 items-center justify-center rounded-[var(--radius-control)] bg-slate-950 text-white transition hover:bg-slate-800">{storefrontI18n.t("uiCopy.continue_shopping")}</Link>
              <Link to="/orders/lookup" className="ui-control flex h-11 items-center justify-center rounded-[var(--radius-control)] border border-slate-200 text-slate-700 transition hover:border-slate-950 hover:text-slate-950">{storefrontI18n.t("uiCopy.public_order_lookup")}</Link>
            </div>
          </aside>
        </section>

        <section className="ui-card border border-slate-100 p-6">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <div>
              <p className="ui-caption uppercase tracking-[0.18em] text-[var(--brand)]">{storefrontI18n.t("uiCopy.recent_orders")}</p>
              <h2 className="ui-h2 mt-2 text-slate-950">{storefrontI18n.t("uiCopy.order_history")}</h2>
            </div>
            <Link to="/account/orders" className="ui-control text-slate-700 underline underline-offset-4 transition hover:text-slate-950">{storefrontI18n.t("uiCopy.view_all")}</Link>
          </div>

          <OrderQueryState isLoading={ordersQuery.isLoading} error={ordersQuery.error} />
          {!ordersQuery.isLoading && !ordersQuery.error && (
            recentOrders.length > 0 ? <OrderList orders={recentOrders} /> : <EmptyOrdersState />
          )}
        </section>
      </div>
    </AccountShell>
  );
}

export function AccountProfilePage() {
  useTranslation();
  const { customer, updateCustomer, refreshCustomer } = useCustomerAuth();
  const queryClient = useQueryClient();
  const [successMessage, setSuccessMessage] = useState<string | null>(null);
  const [twoFactorSetup, setTwoFactorSetup] = useState<TwoFactorSetupStartResponse | null>(null);
  const [twoFactorCode, setTwoFactorCode] = useState("");
  const [recoveryCodes, setRecoveryCodes] = useState<string[] | null>(null);
  const [disableCode, setDisableCode] = useState("");
  const {
    register,
    handleSubmit,
    reset,
    formState: { errors }
  } = useForm<ProfileFormValues>({
    resolver: zodResolver(profileSchema()),
    defaultValues: {
      fullName: customer?.fullName ?? "",
      phoneNumber: customer?.phoneNumber ?? ""
    }
  });

  useEffect(() => {
    reset({
      fullName: customer?.fullName ?? "",
      phoneNumber: customer?.phoneNumber ?? ""
    });
  }, [customer, reset]);

  const profileMutation = useMutation({
    mutationFn: (values: ProfileFormValues) =>
      storefrontApi.updateCustomerMe({
        fullName: values.fullName.trim(),
        phoneNumber: values.phoneNumber.trim()
      }),
    onSuccess: (profile) => {
      updateCustomer(profile);
      setSuccessMessage(storefrontI18n.t("uiCopy.profile_updated"));
      void queryClient.invalidateQueries({ queryKey: ["customer"] });
    },
    onError: () => setSuccessMessage(null)
  });

  const startTwoFactorMutation = useMutation({
    mutationFn: () => storefrontApi.startTwoFactorSetup(),
    onSuccess: (setup) => {
      setTwoFactorSetup(setup);
      setRecoveryCodes(null);
      setTwoFactorCode("");
    }
  });

  const confirmTwoFactorMutation = useMutation({
    mutationFn: () => storefrontApi.confirmTwoFactorSetup({ code: twoFactorCode.trim() }),
    onSuccess: async (result) => {
      setRecoveryCodes(result.recoveryCodes);
      setTwoFactorSetup(null);
      setTwoFactorCode("");
      await refreshCustomer();
    }
  });

  const disableTwoFactorMutation = useMutation({
    mutationFn: () => storefrontApi.disableTwoFactor({ code: disableCode.trim(), recoveryCode: null }),
    onSuccess: async () => {
      setDisableCode("");
      setRecoveryCodes(null);
      await refreshCustomer();
    }
  });

  return (
    <AccountShell>
      <div className="grid gap-6">
      <section className="ui-card border border-slate-100 p-6">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div>
            <p className="ui-caption uppercase tracking-[0.18em] text-[var(--brand)]">{storefrontI18n.t("uiCopy.profile")}</p>
            <h2 className="ui-h2 mt-2 text-slate-950">{storefrontI18n.t("uiCopy.contact_details")}</h2>
          </div>
          <span className="ui-caption rounded-full bg-slate-100 px-3 py-1 text-slate-600">{storefrontI18n.t("uiCopy.email_cannot_be_changed")}</span>
        </div>

        <form className="mt-6 grid gap-5" onSubmit={handleSubmit((values) => profileMutation.mutate(values))}>
          <div className="grid gap-4 sm:grid-cols-2">
            <FormField label={storefrontI18n.t("uiCopy.full_name")} error={errors.fullName?.message}>
              <input
                autoComplete="name"
                className={fieldClassName(Boolean(errors.fullName))}
                {...register("fullName")}
              />
            </FormField>

            <FormField label={storefrontI18n.t("uiCopy.phone_number")} error={errors.phoneNumber?.message}>
              <input
                type="tel"
                autoComplete="tel"
                className={fieldClassName(Boolean(errors.phoneNumber))}
                {...register("phoneNumber")}
              />
            </FormField>

            <FormField label={storefrontI18n.t("uiCopy.email")}>
              <input
                type="email"
                value={customer?.email ?? ""}
                disabled
                className={`${fieldClassName(false)} cursor-not-allowed bg-slate-50 text-slate-600`}
              />
            </FormField>
          </div>

          {profileMutation.error ? <div className="ui-control rounded-[var(--radius-card)] bg-red-50 px-4 py-3 text-red-700">{getApiErrorMessage(profileMutation.error)}</div> : null}
          {successMessage ? <div className="ui-control rounded-[var(--radius-card)] bg-emerald-50 px-4 py-3 text-emerald-700">{successMessage}</div> : null}

          <div className="flex flex-wrap gap-3">
            <button type="submit" disabled={profileMutation.isPending} className="ui-control h-11 rounded-[var(--radius-control)] bg-slate-950 px-6 text-white transition hover:bg-slate-800 disabled:cursor-not-allowed disabled:opacity-60">
              {profileMutation.isPending ? "Saving..." : storefrontI18n.t("uiCopy.save_changes")}
            </button>
            <Link to="/account" className="ui-control flex h-11 items-center rounded-[var(--radius-control)] border border-slate-200 px-6 text-slate-700 transition hover:border-slate-950 hover:text-slate-950">{storefrontI18n.t("uiCopy.back_to_overview")}</Link>
          </div>
        </form>
      </section>
      <section className="ui-card border border-slate-100 p-6">
        <p className="ui-caption uppercase tracking-[0.18em] text-[var(--brand)]">{storefrontI18n.t("uiCopy.security")}</p>
        <h2 className="ui-h2 mt-2 text-slate-950">{storefrontI18n.t("uiCopy.two_factor_authentication")}</h2>
        <p className="ui-body mt-2 text-slate-600">{storefrontI18n.t("uiCopy.use_an_authenticator_app_to_require_a_second_proof_when_you_sign_in")}</p>

        {customer?.twoFactorEnabled ? (
          <div className="mt-5 grid gap-4">
            <div className="rounded-[var(--radius-card)] bg-emerald-50 p-4 text-sm font-semibold text-emerald-800">{storefrontI18n.t("uiCopy.two_factor_authentication_is_enabled")}</div>
            <label className="block max-w-sm">
              <span className="mb-1.5 block text-sm font-semibold">{storefrontI18n.t("uiCopy.current_authenticator_code")}</span>
              <input value={disableCode} onChange={(event) => setDisableCode(event.target.value)} inputMode="numeric" autoComplete="one-time-code" maxLength={6} className={fieldClassName(false)} />
            </label>
            {disableTwoFactorMutation.error ? <div className="ui-control rounded-[var(--radius-card)] bg-red-50 px-4 py-3 text-red-700">{getApiErrorMessage(disableTwoFactorMutation.error)}</div> : null}
            <button type="button" disabled={disableTwoFactorMutation.isPending || disableCode.trim().length !== 6} onClick={() => disableTwoFactorMutation.mutate()} className="ui-control h-11 w-fit rounded-[var(--radius-control)] border border-red-200 px-5 text-red-700 transition hover:bg-red-50 disabled:cursor-not-allowed disabled:opacity-60">
              {disableTwoFactorMutation.isPending ? "Disabling..." : storefrontI18n.t("uiCopy.disable_two_factor_authentication")}
            </button>
          </div>
        ) : twoFactorSetup ? (
          <div className="mt-5 grid gap-4">
            <div className="rounded-[var(--radius-card)] border border-slate-200 bg-slate-50 p-4">
              <p className="text-sm font-semibold text-slate-950">{storefrontI18n.t("uiCopy.add_this_key_to_your_authenticator_app")}</p>
              <p className="mt-2 break-all font-mono text-sm text-slate-700">{twoFactorSetup.manualEntryKey}</p>
              <a href={twoFactorSetup.provisioningUri} className="mt-3 inline-block text-sm font-semibold text-slate-950 underline underline-offset-4">{storefrontI18n.t("uiCopy.open_authenticator_app")}</a>
            </div>
            <label className="block max-w-sm">
              <span className="mb-1.5 block text-sm font-semibold">{storefrontI18n.t("uiCopy.verification_code")}</span>
              <input value={twoFactorCode} onChange={(event) => setTwoFactorCode(event.target.value)} inputMode="numeric" autoComplete="one-time-code" maxLength={6} className={fieldClassName(false)} />
            </label>
            {confirmTwoFactorMutation.error ? <div className="ui-control rounded-[var(--radius-card)] bg-red-50 px-4 py-3 text-red-700">{getApiErrorMessage(confirmTwoFactorMutation.error)}</div> : null}
            <div className="flex flex-wrap gap-3">
              <button type="button" disabled={confirmTwoFactorMutation.isPending || twoFactorCode.trim().length !== 6} onClick={() => confirmTwoFactorMutation.mutate()} className="ui-control h-11 rounded-[var(--radius-control)] bg-slate-950 px-5 text-white transition hover:bg-slate-800 disabled:cursor-not-allowed disabled:opacity-60">
                {confirmTwoFactorMutation.isPending ? "Confirming..." : storefrontI18n.t("uiCopy.confirm_setup")}
              </button>
              <button type="button" onClick={() => { setTwoFactorSetup(null); setTwoFactorCode(""); }} className="ui-control h-11 rounded-[var(--radius-control)] border border-slate-200 px-5 text-slate-700 transition hover:border-slate-950">{storefrontI18n.t("uiCopy.cancel")}</button>
            </div>
          </div>
        ) : (
          <div className="mt-5">
            {startTwoFactorMutation.error ? <div className="mb-4 ui-control rounded-[var(--radius-card)] bg-red-50 px-4 py-3 text-red-700">{getApiErrorMessage(startTwoFactorMutation.error)}</div> : null}
            <button type="button" disabled={startTwoFactorMutation.isPending} onClick={() => startTwoFactorMutation.mutate()} className="ui-control h-11 rounded-[var(--radius-control)] bg-slate-950 px-5 text-white transition hover:bg-slate-800 disabled:cursor-not-allowed disabled:opacity-60">
              {startTwoFactorMutation.isPending ? "Preparing..." : storefrontI18n.t("uiCopy.set_up_two_factor_authentication")}
            </button>
          </div>
        )}

        {recoveryCodes ? (
          <div className="mt-5 rounded-[var(--radius-card)] border border-amber-200 bg-amber-50 p-4 text-amber-950">
            <p className="font-semibold">{storefrontI18n.t("uiCopy.save_these_recovery_codes_now_they_will_not_be_shown_again")}</p>
            <div className="mt-3 grid gap-2 font-mono text-sm sm:grid-cols-2">{recoveryCodes.map((code) => <span key={code}>{code}</span>)}</div>
          </div>
        ) : null}
      </section>
      </div>
    </AccountShell>
  );
}

export function AccountOrdersPage() {
  useTranslation();
  const { customer } = useCustomerAuth();
  const [page, setPage] = useState(1);
  const ordersQuery = useQuery({
    queryKey: ["customer", "orders", "all", customer?.id, page],
    queryFn: () => storefrontApi.getCustomerOrders({ pageNumber: page, pageSize: 20 })
  });

  return (
    <AccountShell>
      <section className="ui-card border border-slate-100 p-6">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div>
            <p className="ui-caption uppercase tracking-[0.18em] text-[var(--brand)]">{storefrontI18n.t("uiCopy.orders")}</p>
            <h2 className="ui-h2 mt-2 text-slate-950">{storefrontI18n.t("uiCopy.your_order_history")}</h2>
          </div>
          {ordersQuery.data ? <span className="ui-caption rounded-full bg-slate-100 px-3 py-1 text-slate-600">{ordersQuery.data.totalCount} total</span> : null}
        </div>

        <OrderQueryState isLoading={ordersQuery.isLoading} error={ordersQuery.error} />
        <AccountPagination page={page} total={ordersQuery.data?.totalCount ?? 0} onChange={setPage} />
        {!ordersQuery.isLoading && !ordersQuery.error && (
          ordersQuery.data && ordersQuery.data.items.length > 0 ? <OrderList orders={ordersQuery.data.items} /> : <EmptyOrdersState />
        )}
      </section>
    </AccountShell>
  );
}

export function AccountLoyaltyPage() {
  useTranslation();
  const { customer } = useCustomerAuth();
  const queryClient = useQueryClient();
  const [transactionPage, setTransactionPage] = useState(1);
  const [pointsInput, setPointsInput] = useState("");
  const [voucher, setVoucher] = useState<RedeemLoyaltyPointsResponse | null>(null);

  const accountQuery = useQuery({
    queryKey: ["customer", "loyalty", "account", customer?.id],
    queryFn: () => storefrontApi.getMyLoyalty()
  });
  const tiersQuery = useQuery({
    queryKey: ["loyalty", "tiers"],
    queryFn: () => storefrontApi.getLoyaltyTiers()
  });
  const transactionsQuery = useQuery({
    queryKey: ["customer", "loyalty", "transactions", customer?.id, transactionPage],
    queryFn: () => storefrontApi.getLoyaltyTransactions({ page: transactionPage, pageSize: loyaltyTransactionsPageSize })
  });

  const account = accountQuery.data ?? null;
  const tiers = tiersQuery.data ?? [];
  const redeemPoints = parsePositiveInt(pointsInput);
  const previewDiscount = redeemPoints * loyaltyVoucherAmountPerPoint;
  const canRedeem = Boolean(account && redeemPoints > 0 && redeemPoints <= account.currentPoints);

  const redeemMutation = useMutation({
    mutationFn: (points: number) => storefrontApi.redeemLoyaltyPoints({ points }),
    onSuccess: (response) => {
      setVoucher(response);
      setPointsInput("");
      setTransactionPage(1);
      void queryClient.invalidateQueries({ queryKey: ["customer", "loyalty"] });
    },
    onError: () => setVoucher(null)
  });

  return (
    <AccountShell>
      <div className="grid gap-6">
        <section className="ui-card border border-slate-100 p-6">
          <div className="flex flex-wrap items-start justify-between gap-3">
            <div>
              <p className="ui-caption uppercase tracking-[0.18em] text-[var(--brand)]">{storefrontI18n.t("uiCopy.loyalty")}</p>
              <h2 className="ui-h2 mt-2 text-slate-950">{storefrontI18n.t("uiCopy.membership_wallet")}</h2>
            </div>
            {account ? <TierBadge tier={account.currentTier} /> : null}
          </div>

          {accountQuery.isLoading ? <StateMessage>{storefrontI18n.t("uiCopy.loading_loyalty_account")}</StateMessage> : null}
          {accountQuery.error ? <StateMessage tone="error">{getApiErrorMessage(accountQuery.error)}</StateMessage> : null}
          {account ? <LoyaltyAccountSummary account={account} tiers={tiers} /> : null}
        </section>

        <section className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_360px]">
          <section className="ui-card border border-slate-100 p-6">
            <div className="flex flex-wrap items-start justify-between gap-3">
              <div>
                <p className="ui-caption uppercase tracking-[0.18em] text-[var(--brand)]">{storefrontI18n.t("uiCopy.tiers")}</p>
                <h2 className="ui-h2 mt-2 text-slate-950">{storefrontI18n.t("uiCopy.benefits")}</h2>
              </div>
              {tiers.length ? <span className="ui-caption rounded-full bg-slate-100 px-3 py-1 text-slate-600">{tiers.length} levels</span> : null}
            </div>

            {tiersQuery.isLoading ? <StateMessage>{storefrontI18n.t("uiCopy.loading_tiers")}</StateMessage> : null}
            {tiersQuery.error ? <StateMessage tone="error">{getApiErrorMessage(tiersQuery.error)}</StateMessage> : null}
            {!tiersQuery.isLoading && !tiersQuery.error ? <TierBenefitsTable tiers={tiers} currentTier={account?.currentTier ?? 0} /> : null}
          </section>

          <section className="ui-card border border-slate-100 p-6">
            <p className="ui-caption uppercase tracking-[0.18em] text-[var(--brand)]">{storefrontI18n.t("uiCopy.redeem")}</p>
            <h2 className="ui-h2 mt-2 text-slate-950">{storefrontI18n.t("uiCopy.create_voucher")}</h2>

            <div className="mt-5 grid gap-4">
              <FormField label={storefrontI18n.t("uiCopy.points_to_redeem")}>
                <input
                  inputMode="numeric"
                  min={1}
                  max={account?.currentPoints ?? undefined}
                  value={pointsInput}
                  onChange={(event) => setPointsInput(event.target.value.replace(/\D/g, ""))}
                  className={fieldClassName(false)}
                  placeholder="100"
                />
              </FormField>

              <div className="rounded-[var(--radius-card)] border border-slate-100 bg-slate-50 p-4">
                <InfoBlock label={storefrontI18n.t("uiCopy.voucher_value")} value={previewDiscount > 0 ? formatVoucherAmount(previewDiscount) : "-"} />
                <div className="mt-3">
                  <InfoBlock label={storefrontI18n.t("uiCopy.remaining_points")} value={account ? String(Math.max(0, account.currentPoints - redeemPoints)) : "-"} />
                </div>
              </div>

              {redeemPoints > 0 && account && redeemPoints > account.currentPoints ? (
                <div className="ui-control rounded-[var(--radius-card)] bg-amber-50 px-4 py-3 text-amber-800">{storefrontI18n.t("uiCopy.you_do_not_have_enough_points_for_this_voucher")}</div>
              ) : null}
              {redeemMutation.error ? <div className="ui-control rounded-[var(--radius-card)] bg-red-50 px-4 py-3 text-red-700">{getApiErrorMessage(redeemMutation.error)}</div> : null}
              {voucher ? <RedeemedVoucher voucher={voucher} /> : null}

              <button
                type="button"
                disabled={!canRedeem || redeemMutation.isPending}
                onClick={() => redeemMutation.mutate(redeemPoints)}
                className="ui-control h-11 rounded-[var(--radius-control)] bg-slate-950 px-5 text-white transition hover:bg-slate-800 disabled:cursor-not-allowed disabled:opacity-60"
              >
                {redeemMutation.isPending ? "Creating..." : storefrontI18n.t("uiCopy.redeem_points")}
              </button>
            </div>
          </section>
        </section>

        <section className="ui-card border border-slate-100 p-6">
          <div className="flex flex-wrap items-start justify-between gap-3">
            <div>
              <p className="ui-caption uppercase tracking-[0.18em] text-[var(--brand)]">{storefrontI18n.t("uiCopy.history")}</p>
              <h2 className="ui-h2 mt-2 text-slate-950">{storefrontI18n.t("uiCopy.point_transactions")}</h2>
            </div>
            {transactionsQuery.data ? <span className="ui-caption rounded-full bg-slate-100 px-3 py-1 text-slate-600">{transactionsQuery.data.totalCount} total</span> : null}
          </div>

          {transactionsQuery.isLoading ? <StateMessage>{storefrontI18n.t("uiCopy.loading_transactions")}</StateMessage> : null}
          {transactionsQuery.error ? <StateMessage tone="error">{getApiErrorMessage(transactionsQuery.error)}</StateMessage> : null}
          {!transactionsQuery.isLoading && !transactionsQuery.error ? (
            <LoyaltyTransactionList transactions={transactionsQuery.data?.items ?? []} />
          ) : null}
          {transactionsQuery.data ? (
            <div className="mt-5 flex flex-wrap items-center justify-between gap-3 border-t border-slate-100 pt-4">
              <span className="ui-caption text-slate-600">Page {transactionsQuery.data.pageNumber} of {transactionsQuery.data.totalPages || 1}</span>
              <div className="flex gap-2">
                <button
                  type="button"
                  disabled={!transactionsQuery.data.hasPreviousPage}
                  onClick={() => setTransactionPage((page) => Math.max(1, page - 1))}
                  className="ui-control h-10 rounded-[var(--radius-control)] border border-slate-200 px-4 text-slate-700 transition hover:border-slate-950 hover:text-slate-950 disabled:cursor-not-allowed disabled:opacity-50"
                >{storefrontI18n.t("uiCopy.previous")}</button>
                <button
                  type="button"
                  disabled={!transactionsQuery.data.hasNextPage}
                  onClick={() => setTransactionPage((page) => page + 1)}
                  className="ui-control h-10 rounded-[var(--radius-control)] border border-slate-200 px-4 text-slate-700 transition hover:border-slate-950 hover:text-slate-950 disabled:cursor-not-allowed disabled:opacity-50"
                >{storefrontI18n.t("uiCopy.next")}</button>
              </div>
            </div>
          ) : null}
        </section>
      </div>
    </AccountShell>
  );
}

export function AccountOrderDetailPage() {
  useTranslation();
  const { customer } = useCustomerAuth();
  const { id } = useParams();
  const queryClient = useQueryClient();
  const [actionError, setActionError] = useState<string | null>(null);
  const [receiptError, setReceiptError] = useState<string | null>(null);
  const [isReceiptPreviewOpen, setIsReceiptPreviewOpen] = useState(false);

  const orderQuery = useQuery({
    queryKey: ["customer", "order", id, customer?.id],
    queryFn: () => storefrontApi.getCustomerOrder(id ?? ""),
    enabled: Boolean(id)
  });
  const trackingQuery = useQuery({
    queryKey: ["customer", "order", id, "tracking", customer?.id],
    queryFn: () => storefrontApi.getCustomerOrderTracking(id ?? ""),
    enabled: Boolean(id)
  });

  const cancelMutation = useMutation({
    mutationFn: (reason: string) => storefrontApi.cancelCustomerOrder(id ?? "", reason),
    onSuccess: (updated) => {
      setActionError(null);
      queryClient.setQueryData(["customer", "order", id, customer?.id], updated);
      void queryClient.invalidateQueries({ queryKey: ["customer", "orders"] });
    },
    onError: (err) => setActionError(getApiErrorMessage(err))
  });

  const returnMutation = useMutation({
    mutationFn: (reason: string) => storefrontApi.requestOrderReturn(id ?? "", reason),
    onSuccess: (updated) => {
      setActionError(null);
      queryClient.setQueryData(["customer", "order", id, customer?.id], updated);
      void queryClient.invalidateQueries({ queryKey: ["customer", "orders"] });
    },
    onError: (err) => setActionError(getApiErrorMessage(err))
  });

  const receiptMutation = useMutation({
    mutationFn: () => storefrontApi.getCustomerOrderReceipt(id ?? ""),
    onSuccess: (blob) => {
      setReceiptError(null);
      const orderCode = orderQuery.data?.orderCode ?? "order";
      downloadBlob(blob, `order-receipt-${orderCode}.pdf`);
    },
    onError: (error) => setReceiptError(getApiErrorMessage(error))
  });

  return (
    <AccountShell>
      {!id ? <StateMessage tone="error">{storefrontI18n.t("uiCopy.missing_order_id")}</StateMessage> : null}
      {orderQuery.isLoading ? <StateMessage>{storefrontI18n.t("uiCopy.loading_order")}</StateMessage> : null}
      {orderQuery.error ? <StateMessage tone="error">{getApiErrorMessage(orderQuery.error)}</StateMessage> : null}
      {orderQuery.data ? (
        <>
          <OrderDetail
            order={orderQuery.data}
            tracking={trackingQuery.data ?? null}
            onCancel={cancelMutation.mutate}
            isCancelling={cancelMutation.isPending}
            onReturn={returnMutation.mutate}
            isReturning={returnMutation.isPending}
            actionError={actionError}
            onViewReceipt={() => setIsReceiptPreviewOpen(true)}
          />
          <ReceiptPreviewModal
            open={isReceiptPreviewOpen}
            order={orderQuery.data}
            isDownloading={receiptMutation.isPending}
            downloadError={receiptError}
            onClose={() => setIsReceiptPreviewOpen(false)}
            onDownload={() => receiptMutation.mutate()}
          />
        </>
      ) : null}
    </AccountShell>
  );
}

export function AccountWarrantiesPage() {
  useTranslation();
  const { customer } = useCustomerAuth();
  const [page, setPage] = useState(1);
  const warrantiesQuery = useQuery({
    queryKey: ["customer", "warranties", customer?.id, page],
    queryFn: () => storefrontApi.getCustomerWarranties({ pageNumber: page, pageSize: 20 })
  });

  return (
    <AccountShell>
      <section className="ui-card border border-slate-100 p-6">
        <div className="flex flex-wrap items-start justify-between gap-4">
          <div>
            <p className="ui-caption uppercase tracking-[0.18em] text-[var(--brand)]">{storefrontI18n.t("uiCopy.product_protection")}</p>
            <h1 className="ui-h2 mt-2 text-slate-950">{storefrontI18n.t("uiCopy.my_warranties")}</h1>
            <p className="ui-body mt-2 max-w-2xl text-slate-600">{storefrontI18n.t("uiCopy.view_products_assigned_to_your_account_and_the_coverage_dates_for_activated_warranties")}</p>
          </div>
          <Link to="/warranty" className="ui-control rounded-[var(--radius-control)] bg-slate-950 px-4 py-2 text-white transition hover:bg-[var(--brand)]">{storefrontI18n.t("uiCopy.check_or_activate")}</Link>
        </div>

        <AccountPagination page={page} total={warrantiesQuery.data?.totalCount ?? 0} onChange={setPage} />
        {warrantiesQuery.isLoading ? <StateMessage>{storefrontI18n.t("uiCopy.loading_warranties")}</StateMessage> : null}
        {warrantiesQuery.error ? <StateMessage tone="error">{getApiErrorMessage(warrantiesQuery.error)}</StateMessage> : null}
        {warrantiesQuery.data?.items.length === 0 ? <StateMessage>{storefrontI18n.t("uiCopy.you_do_not_have_any_assigned_warranty_products_yet")}</StateMessage> : null}
        {warrantiesQuery.data?.items.length ? (
          <div className="mt-6 grid gap-3">
            {warrantiesQuery.data.items.map((warranty) => <WarrantyListItem key={warranty.id} warranty={warranty} />)}
          </div>
        ) : null}
      </section>
    </AccountShell>
  );
}

function WarrantyListItem({ warranty }: { warranty: CustomerWarrantyListItemDto }) {
  const status = warranty.status === 1 ? "Active" : warranty.status === 0 ? storefrontI18n.t("uiCopy.awaiting_activation") : warranty.status === 2 ? "Voided" : "Replaced";
  return (
    <article className="flex flex-col gap-3 rounded-[var(--radius-card)] border border-slate-200 p-4 sm:flex-row sm:items-center sm:justify-between">
      <div>
        <h2 className="font-black text-slate-950">{warranty.productName}</h2>
        <p className="mt-1 text-sm font-semibold text-slate-600">{warranty.maskedIdentifier}</p>
      </div>
      <div className="text-left sm:text-right">
        <span className={`inline-flex rounded-full px-3 py-1 text-xs font-black ${warranty.status === 1 ? "bg-emerald-100 text-emerald-800" : "bg-amber-100 text-amber-800"}`}>{status}</span>
        <p className="mt-2 text-xs font-semibold text-slate-600">{warranty.latestCoverageEndsAt ? `Coverage ends ${formatDate(warranty.latestCoverageEndsAt)}` : warranty.activatedAt ? `Activated ${formatDate(warranty.activatedAt)}` : storefrontI18n.t("uiCopy.activation_required")}</p>
      </div>
    </article>
  );
}

function AccountShell({ children }: { children: ReactNode }) {
  const { signOut } = useCustomerAuth();
  const navigate = useNavigate();

  function handleSignOut() {
    signOut();
    void navigate("/", { replace: true });
  }

  async function handleSignOutEverywhere() {
    try {
      await storefrontApi.logoutAllCustomerSessions();
    } finally {
      signOut();
      await navigate("/", { replace: true });
    }
  }

  return (
    <div className="grid gap-6">
      <div className="grid gap-6 lg:grid-cols-[250px_minmax(0,1fr)]">
        <aside className="lg:self-stretch">
          <div className="ui-card border border-slate-100 p-4 lg:sticky lg:top-28 lg:max-h-[calc(100vh-8rem)] lg:overflow-y-auto lg:overscroll-contain">
            <nav className="grid gap-1">
              <AccountNavLink to="/account" end>{storefrontI18n.t("uiCopy.overview")}</AccountNavLink>
              <AccountNavLink to="/account/profile">{storefrontI18n.t("uiCopy.profile")}</AccountNavLink>
              <AccountNavLink to="/account/orders">{storefrontI18n.t("uiCopy.orders")}</AccountNavLink>
              <AccountNavLink to="/account/loyalty">{storefrontI18n.t("uiCopy.loyalty")}</AccountNavLink>
              <AccountNavLink to="/account/warranties">{storefrontI18n.t("uiCopy.warranties")}</AccountNavLink>
            </nav>
            <button type="button" onClick={handleSignOut} className="ui-control mt-4 flex h-11 w-full items-center justify-center rounded-[var(--radius-control)] border border-slate-200 text-slate-700 transition hover:border-red-200 hover:bg-red-50 hover:text-red-700">{storefrontI18n.t("uiCopy.sign_out")}</button>
            <button type="button" onClick={() => void handleSignOutEverywhere()} className="ui-control mt-2 flex h-11 w-full items-center justify-center rounded-[var(--radius-control)] border border-red-200 text-red-700 transition hover:bg-red-50">{storefrontI18n.t("uiCopy.sign_out_everywhere")}</button>
          </div>
        </aside>

        <div className="min-w-0">{children}</div>
      </div>
    </div>
  );
}

function AccountNavLink({ children, end = false, to }: { children: ReactNode; end?: boolean; to: string }) {
  return (
    <NavLink
      to={to}
      end={end}
      className={({ isActive }) =>
        `ui-control rounded-[var(--radius-control)] px-4 py-3 transition ${
          isActive ? "bg-slate-950 text-white" : "text-slate-600 hover:bg-slate-100 hover:text-slate-950"
        }`
      }
    >
      {children}
    </NavLink>
  );
}

function LoyaltyAccountSummary({ account, tiers }: { account: LoyaltyAccountDto; tiers: LoyaltyTierDto[] }) {
  const currentTier = tiers.find((tier) => tier.type === account.currentTier);
  const nextTier = account.nextTier === null ? null : tiers.find((tier) => tier.type === account.nextTier) ?? null;
  const currentTierFloor = currentTier?.minTotalPointsEarned ?? 0;
  const nextTierFloor = nextTier?.minTotalPointsEarned ?? null;
  const progress =
    nextTierFloor === null
      ? 100
      : clampPercent(((account.totalPointsEarned - currentTierFloor) / Math.max(1, nextTierFloor - currentTierFloor)) * 100);

  return (
    <div className="mt-6 grid gap-6">
      {(account.pointsDebt ?? 0) > 0 && <p role="status" className="rounded-xl bg-amber-50 p-4 text-sm text-amber-900">{storefrontI18n.t("loyaltyDebt", { points: account.pointsDebt })}</p>}
      <section className="grid gap-4 md:grid-cols-4">
        <MetricCard label={storefrontI18n.t("uiCopy.current_points")} value={account.currentPoints.toLocaleString("en-US")} />
        <MetricCard label={storefrontI18n.t("uiCopy.total_earned")} value={account.totalPointsEarned.toLocaleString("en-US")} />

      </section>

      <section className="rounded-[var(--radius-card)] border border-slate-100 bg-slate-50 p-5">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div>
            <p className="ui-caption uppercase tracking-[0.18em] text-slate-600">{storefrontI18n.t("uiCopy.tier_progress")}</p>
            <p className="mt-1 text-lg font-black text-slate-950">
              {nextTier ? `${account.pointsToNextTier?.toLocaleString("en-US") ?? 0} points to ${formatTierType(nextTier.type)}` : storefrontI18n.t("uiCopy.top_tier_reached")}
            </p>
          </div>
          <div className="flex items-center gap-2">
            <TierBadge tier={account.currentTier} />
            {nextTier ? <span className="text-slate-600">to</span> : null}
            {nextTier ? <TierBadge tier={nextTier.type} /> : null}
          </div>
        </div>
        <div className="mt-4 h-3 overflow-hidden rounded-full bg-white">
          <div className="h-full rounded-full bg-slate-950 transition-all" style={{ width: `${progress}%` }} />
        </div>
        <div className="mt-2 flex justify-between text-xs font-bold text-slate-600">
          <span>{currentTierFloor.toLocaleString("en-US")} pts</span>
          <span>{nextTierFloor === null ? `${account.totalPointsEarned.toLocaleString("en-US")} pts` : `${nextTierFloor.toLocaleString("en-US")} pts`}</span>
        </div>
      </section>
    </div>
  );
}

function TierBenefitsTable({ currentTier, tiers }: { currentTier: LoyaltyTierType; tiers: LoyaltyTierDto[] }) {
  if (tiers.length === 0) {
    return <StateMessage>{storefrontI18n.t("uiCopy.no_tiers_configured")}</StateMessage>;
  }

  return (
    <div className="mt-5 overflow-x-auto">
      <table className="w-full text-sm">
        <thead>
          <tr className="border-b border-slate-100 text-left text-xs font-bold uppercase tracking-[0.14em] text-slate-600">
            <th className="pb-3 pr-4">{storefrontI18n.t("uiCopy.tier")}</th>
            <th className="pb-3 pr-4 text-right">{storefrontI18n.t("uiCopy.minimum_points")}</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-slate-100">
          {tiers
            .slice()
            .sort((left, right) => left.minTotalPointsEarned - right.minTotalPointsEarned)
            .map((tier) => (
              <tr key={tier.id} className={tier.type === currentTier ? "bg-slate-50" : undefined}>
                <td className="py-3 pr-4">
                  <TierBadge tier={tier.type} />
                </td>
                <td className="py-3 pr-4 text-right font-bold text-slate-700">{tier.minTotalPointsEarned.toLocaleString("en-US")}</td>


              </tr>
            ))}
        </tbody>
      </table>
    </div>
  );
}

function LoyaltyTransactionList({ transactions }: { transactions: LoyaltyTransactionDto[] }) {
  if (transactions.length === 0) {
    return (
      <div className="mt-5 rounded-[var(--radius-card)] border border-dashed border-slate-200 bg-slate-50 p-6 text-center">
        <h3 className="ui-h3 text-slate-950">{storefrontI18n.t("uiCopy.no_loyalty_transactions_yet")}</h3>
        <p className="ui-body mx-auto mt-2 max-w-xl text-slate-600">{storefrontI18n.t("uiCopy.completed_account_orders_and_redeemed_vouchers_will_appear_here")}</p>
      </div>
    );
  }

  return (
    <div className="mt-5 grid gap-3">
      {transactions.map((transaction) => (
        <div key={transaction.id} className="grid gap-3 rounded-[var(--radius-card)] border border-slate-100 bg-slate-50 p-4 sm:grid-cols-[1fr_auto] sm:items-center">
          <div className="min-w-0">
            <div className="flex flex-wrap items-center gap-2">
              <span className={`inline-flex rounded-full px-3 py-1 text-xs font-black ${transaction.points >= 0 ? "bg-emerald-100 text-emerald-800" : "bg-rose-100 text-rose-800"}`}>
                {formatLoyaltyTransactionType(transaction.type)}
              </span>
              {transaction.orderId ? <span className="ui-caption font-mono text-slate-600">Order {formatShortId(transaction.orderId)}</span> : null}
              {transaction.voucherId ? <span className="ui-caption font-mono text-slate-600">Voucher {formatShortId(transaction.voucherId)}</span> : null}
            </div>
            <p className="mt-2 text-sm font-bold text-slate-950">{transaction.description}</p>
            <p className="ui-caption mt-1 text-slate-600">{formatDate(transaction.createdAt)}</p>
          </div>
          <div className="text-left sm:text-right">
            <p className={`text-lg font-black ${transaction.points >= 0 ? "text-emerald-700" : "text-rose-700"}`}>{formatPoints(transaction.points)}</p>
            <p className="ui-caption text-slate-600">Balance {transaction.balanceAfter.toLocaleString("en-US")}</p>
          </div>
        </div>
      ))}
    </div>
  );
}

function RedeemedVoucher({ voucher }: { voucher: RedeemLoyaltyPointsResponse }) {
  return (
    <div className="rounded-[var(--radius-card)] border border-emerald-100 bg-emerald-50 p-4">
      <p className="ui-caption uppercase tracking-[0.14em] text-emerald-700">{storefrontI18n.t("uiCopy.voucher_created")}</p>
      <p className="mt-1 font-mono text-lg font-black text-emerald-950">{voucher.voucherCode}</p>
      <p className="ui-body mt-1 text-emerald-800">
        {formatVoucherAmount(voucher.discountAmount)} off, expires {formatDate(voucher.expiresAt)}.
      </p>
    </div>
  );
}

function TierBadge({ tier }: { tier: LoyaltyTierType }) {
  return (
    <span className={`inline-flex rounded-full px-3 py-1 text-xs font-black ${tierStyles[tier]}`}>
      {formatTierType(tier)}
    </span>
  );
}

function OrderDetail({
  order,
  tracking,
  onCancel,
  isCancelling,
  onReturn,
  isReturning,
  actionError,
  onViewReceipt
}: {
  order: CustomerOrderDto;
  tracking: ShipmentTrackingDto | null;
  onCancel: (reason: string) => void;
  isCancelling: boolean;
  onReturn: (reason: string) => void;
  isReturning: boolean;
  actionError: string | null;
  onViewReceipt: () => void;
}) {
  const { t } = useTranslation();

  return (
    <div className="grid gap-6">
      <section className="ui-card border border-slate-100 p-6">
        <div className="flex flex-wrap items-start justify-between gap-3 border-b border-slate-100 pb-5">
          <div>
            <p className="font-mono text-2xl font-black text-slate-950">{order.orderCode}</p>
            <p className="ui-body mt-1 text-slate-600">Created on {formatDate(order.createdAt)}</p>
          </div>
          <StatusBadge status={order.status} />
        </div>

        <div className="mt-5 grid gap-6 lg:grid-cols-[minmax(0,1fr)_340px]">
          <div>
            <h2 className="ui-h3 text-slate-950">{storefrontI18n.t("uiCopy.items")}</h2>
            <OrderItemsTable order={order} />
            <OrderTotals order={order} />
          </div>

          <aside className="grid h-fit gap-5">
            <section className="rounded-[var(--radius-card)] border border-slate-100 bg-slate-50 p-5">
              <h2 className="ui-caption uppercase tracking-[0.18em] text-[var(--brand)]">{storefrontI18n.t("uiCopy.recipient_details")}</h2>
              <div className="mt-4 grid gap-3">
                <InfoBlock label={storefrontI18n.t("uiCopy.full_name")} value={order.customerName} />
                <InfoBlock label={storefrontI18n.t("uiCopy.phone")} value={order.customerPhone} />
                {order.customerEmail ? <InfoBlock label={storefrontI18n.t("uiCopy.email")} value={order.customerEmail} /> : null}
                <InfoBlock label={storefrontI18n.t("uiCopy.shipping_address")} value={order.shippingAddress} />
                <OrderShippingDetails shipping={order.shipping} />
                {order.note ? <InfoBlock label={storefrontI18n.t("uiCopy.note")} value={order.note} /> : null}
                {order.couponCodeSnapshot ? <InfoBlock label={storefrontI18n.t("uiCopy.coupon")} value={formatCouponSnapshot(order)} /> : null}
                <InfoBlock label={storefrontI18n.t("uiCopy.payment")} value={formatPaymentMethod(order.paymentMethod)} />
                <InfoBlock label={storefrontI18n.t("uiCopy.payment_status")} value={formatPaymentStatus(order.paymentStatus)} />
              </div>
            </section>

            <section className="rounded-[var(--radius-card)] border border-slate-100 bg-white p-5">
              <h2 className="ui-caption uppercase tracking-[0.18em] text-[var(--brand)]">{storefrontI18n.t("uiCopy.status_timeline")}</h2>
              <StatusTimeline order={order} />
            </section>

            <div className="grid gap-2">
              <button
                type="button"
                onClick={onViewReceipt}
                className="ui-control flex h-11 items-center justify-center rounded-[var(--radius-control)] border border-[var(--brand)] bg-white px-4 font-bold text-[var(--brand)] transition hover:bg-[var(--brand)] hover:text-white"
              >
                {t("receipt.download")}
              </button>
              <p className="ui-caption text-slate-600">{t("receipt.notTaxInvoice")}</p>
            </div>

            <OrderActionPanel
              order={order}
              onCancel={onCancel}
              isCancelling={isCancelling}
              onReturn={onReturn}
              isReturning={isReturning}
              actionError={actionError}
            />

            <Link to="/account/orders" className="ui-control flex h-11 items-center justify-center rounded-[var(--radius-control)] border border-slate-200 text-slate-700 transition hover:border-slate-950 hover:text-slate-950">{storefrontI18n.t("uiCopy.back_to_orders")}</Link>
          </aside>
        </div>
      </section>
      {tracking ? (
        <section className="ui-card border border-slate-100 p-6">
          <p className="ui-caption uppercase tracking-[0.18em] text-[var(--brand)]">{storefrontI18n.t("uiCopy.shipment")}</p>
          <h2 className="ui-h3 mt-2 text-slate-950">{storefrontI18n.t("uiCopy.carrier_tracking")}</h2>
          <div className="mt-5"><ShipmentTrackingPanel tracking={tracking} /></div>
        </section>
      ) : null}
    </div>
  );
}

function OrderActionPanel({
  order,
  onCancel,
  isCancelling,
  onReturn,
  isReturning,
  actionError
}: {
  order: CustomerOrderDto;
  onCancel: (reason: string) => void;
  isCancelling: boolean;
  onReturn: (reason: string) => void;
  isReturning: boolean;
  actionError: string | null;
}) {
  const { t } = useTranslation();
  const [showCancelDialog, setShowCancelDialog] = useState(false);
  const [showReturnDialog, setShowReturnDialog] = useState(false);
  const [reason, setReason] = useState("");

  const canCancel = order.status === 0 || order.status === 1;
  const canReturn = order.status === 4 && !order.returnRequestedAt && order.canRequestReturn === true;

  if (order.returnRequestedAt) return <p role="status" className="rounded-xl bg-amber-50 p-4 text-amber-900">{order.returnReviewedAt ? t("returnRequest.decision", { decision: t(order.returnDecision === "Rejected" ? "returnRequest.rejected" : "returnRequest.approved"), reason: order.returnDecisionReason ?? "" }) : t("returnRequest.pending")}</p>;

  if (!canCancel && !canReturn) return null;

  function handleCancel() {
    if (!reason.trim()) return;
    onCancel(reason.trim());
    setShowCancelDialog(false);
    setReason("");
  }

  function handleReturn() {
    if (!reason.trim()) return;
    onReturn(reason.trim());
    setShowReturnDialog(false);
    setReason("");
  }

  return (
    <div className="grid gap-3">
      {actionError ? (
        <div className="rounded-[var(--radius-card)] bg-red-50 px-4 py-3 text-sm font-semibold text-red-700">
          {actionError}
        </div>
      ) : null}

      {canCancel && !showCancelDialog ? (
        <button
          type="button"
          id="btn-cancel-order"
          onClick={() => { setShowCancelDialog(true); setReason(""); }}
          className="ui-control flex h-11 items-center justify-center rounded-[var(--radius-control)] border border-red-200 text-red-700 transition hover:bg-red-50"
        >{storefrontI18n.t("uiCopy.cancel_order")}</button>
      ) : null}

      {canCancel && showCancelDialog ? (
        <div className="rounded-[var(--radius-card)] border border-red-100 bg-red-50 p-4">
          <p className="text-sm font-bold text-red-800">{storefrontI18n.t("uiCopy.cancel_this_order")}</p>
          <p className="mt-1 text-xs font-medium text-red-600">{storefrontI18n.t("uiCopy.this_action_cannot_be_undone_stock_will_be_restored_automatically")}</p>
          <textarea
            id="cancel-reason"
            rows={2}
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            maxLength={500}
            placeholder={storefrontI18n.t("uiCopy.reason_for_cancellation_required")}
            required
            className="mt-3 w-full rounded-[var(--radius-control)] border border-red-200 bg-white px-3 py-2 text-sm font-medium text-slate-900 placeholder:text-slate-600 outline-none focus:border-red-400 focus:ring-2 focus:ring-red-200"
          />
          <div className="mt-3 flex gap-2">
            <button
              type="button"
              id="btn-cancel-order-confirm"
              disabled={isCancelling || !reason.trim()}
              onClick={handleCancel}
              className="ui-control flex h-10 flex-1 items-center justify-center rounded-[var(--radius-control)] bg-red-600 text-sm text-white transition hover:bg-red-700 disabled:cursor-not-allowed disabled:opacity-60"
            >
              {isCancelling ? "Cancelling..." : storefrontI18n.t("uiCopy.yes_cancel_order")}
            </button>
            <button
              type="button"
              disabled={isCancelling}
              onClick={() => setShowCancelDialog(false)}
              className="ui-control flex h-10 flex-1 items-center justify-center rounded-[var(--radius-control)] border border-slate-200 text-sm text-slate-700 transition hover:border-slate-950 disabled:cursor-not-allowed disabled:opacity-60"
            >{storefrontI18n.t("uiCopy.keep_order")}</button>
          </div>
        </div>
      ) : null}

      {canReturn && !showReturnDialog ? (
        <button
          type="button"
          id="btn-return-order"
          onClick={() => { setShowReturnDialog(true); setReason(""); }}
          className="ui-control flex h-11 items-center justify-center rounded-[var(--radius-control)] border border-amber-200 text-amber-700 transition hover:bg-amber-50"
        >{storefrontI18n.t("uiCopy.request_return")}</button>
      ) : null}

      {canReturn && showReturnDialog ? (
        <div className="rounded-[var(--radius-card)] border border-amber-100 bg-amber-50 p-4">
          <p className="text-sm font-bold text-amber-800">{storefrontI18n.t("uiCopy.request_a_return")}</p>
          <p className="mt-1 text-xs font-medium text-amber-600">
            {t("returnRequest.required")}
          </p>
          <textarea
            id="return-reason"
            aria-label={t("returnRequest.reason")}
            maxLength={500}
            required
            rows={2}
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            placeholder={t("returnRequest.reason")}
            className="mt-3 w-full rounded-[var(--radius-control)] border border-amber-200 bg-white px-3 py-2 text-sm font-medium text-slate-900 placeholder:text-slate-600 outline-none focus:border-amber-400 focus:ring-2 focus:ring-amber-200"
          />
          <div className="mt-3 flex gap-2">
            <button
              type="button"
              id="btn-return-order-confirm"
              disabled={isReturning || !reason.trim()}
              onClick={handleReturn}
              className="ui-control flex h-10 flex-1 items-center justify-center rounded-[var(--radius-control)] bg-amber-800 text-sm text-white transition hover:bg-amber-600 disabled:cursor-not-allowed disabled:opacity-60"
            >
              {isReturning ? "Submitting..." : storefrontI18n.t("uiCopy.yes_request_return")}
            </button>
            <button
              type="button"
              disabled={isReturning}
              onClick={() => setShowReturnDialog(false)}
              className="ui-control flex h-10 flex-1 items-center justify-center rounded-[var(--radius-control)] border border-slate-200 text-sm text-slate-700 transition hover:border-slate-950 disabled:cursor-not-allowed disabled:opacity-60"
            >{storefrontI18n.t("uiCopy.go_back")}</button>
          </div>
        </div>
      ) : null}
    </div>
  );
}

function OrderItemsTable({ order }: { order: CustomerOrderDto }) {
  return (
    <div className="mt-3 overflow-x-auto">
      <table className="w-full text-sm">
        <thead>
          <tr className="border-b border-slate-100 text-left text-xs font-bold uppercase tracking-[0.14em] text-slate-600">
            <th className="pb-2 pr-4">{storefrontI18n.t("uiCopy.product")}</th>
            <th className="pb-2 pr-4 text-center">{storefrontI18n.t("uiCopy.qty")}</th>
            <th className="pb-2 pr-4 text-right">{storefrontI18n.t("uiCopy.unit_price")}</th>
            <th className="pb-2 text-right">{storefrontI18n.t("uiCopy.line_total")}</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-slate-100">
          {order.items.map((item) => (
            <tr key={item.id}>
              <td className="py-3 pr-4">
                <p className="font-bold text-slate-950">{item.productNameSnapshot}</p>
                <p className="mt-0.5 font-mono text-xs text-slate-600">{item.skuSnapshot}</p>
                {item.requiresInstallation ? <span className="ui-caption mt-1 inline-block rounded-full bg-amber-100 px-2 py-0.5 text-amber-800">{storefrontI18n.t("uiCopy.installation_required")}</span> : null}
              </td>
              <td className="py-3 pr-4 text-center font-bold text-slate-700">{item.quantity}</td>
              <td className="py-3 pr-4 text-right font-bold text-slate-700">{formatMoney(item.unitPrice)}</td>
              <td className="py-3 text-right font-black text-slate-950">{formatMoney(item.lineTotal)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function OrderTotals({ order }: { order: CustomerOrderDto }) {
  return (
    <div className="mt-4 grid gap-2 border-t border-slate-200 pt-4">
      <TotalLine label={storefrontI18n.t("uiCopy.subtotal")} value={formatMoney(order.subtotal)} />
      {order.shippingFee > 0 ? <TotalLine label={storefrontI18n.t("uiCopy.shipping_fee")} value={formatMoney(order.shippingFee)} /> : null}
      {order.discountAmount > 0 ? <TotalLine label={formatDiscountLabel(order)} value={`-${formatMoney(order.discountAmount)}`} tone="success" /> : null}
      <div className="flex justify-between text-xl font-black text-slate-950">
        <span>{storefrontI18n.t("uiCopy.total")}</span>
        <span className="text-[var(--brand)]">{formatMoney(order.totalAmount)}</span>
      </div>
    </div>
  );
}

function TotalLine({ label, tone = "muted", value }: { label: string; tone?: "muted" | "success"; value: string }) {
  return (
    <div className={`flex justify-between text-sm font-bold ${tone === "success" ? "text-emerald-600" : "text-slate-600"}`}>
      <span>{label}</span>
      <span>{value}</span>
    </div>
  );
}

function formatDiscountLabel(order: CustomerOrderDto): string {
  return order.couponCodeSnapshot ? `Discount (${order.couponCodeSnapshot})` : "Discount";
}

function formatCouponSnapshot(order: CustomerOrderDto): string {
  return order.couponNameSnapshot ? `${order.couponCodeSnapshot} - ${order.couponNameSnapshot}` : order.couponCodeSnapshot ?? "";
}

function StatusTimeline({ order }: { order: CustomerOrderDto }) {
  const entries: CustomerOrderStatusHistoryDto[] =
    order.statusHistory.length > 0
      ? order.statusHistory
      : [
          {
            id: "created",
            fromStatus: null,
            toStatus: order.status,
            cancellationReason: null,
            customerMessage: storefrontI18n.t("uiCopy.order_created"),
            changedAt: order.createdAt
          }
        ];

  return (
    <ol className="mt-4 grid gap-4">
      {entries.map((entry) => (
        <li key={entry.id} className="grid grid-cols-[14px_minmax(0,1fr)] gap-3">
          <span className="mt-1.5 h-3.5 w-3.5 rounded-full bg-slate-950 ring-4 ring-slate-100" aria-hidden="true" />
          <span className="min-w-0">
            <span className="block text-sm font-bold text-slate-950">{formatTimelineTitle(entry)}</span>
            <span className="mt-0.5 block text-xs font-medium text-slate-600">{formatDate(entry.changedAt)}</span>
            {entry.cancellationReason ? <span className="mt-1 block text-sm font-semibold text-red-700">Cancellation reason: {entry.cancellationReason}</span> : null}
            {entry.customerMessage ? <span className="mt-1 block text-sm text-slate-600">{entry.customerMessage}</span> : null}
          </span>
        </li>
      ))}
    </ol>
  );
}

function formatTimelineTitle(entry: CustomerOrderStatusHistoryDto): string {
  if (entry.fromStatus === null) {
    return formatOrderStatus(entry.toStatus);
  }

  return `${formatOrderStatus(entry.fromStatus)} to ${formatOrderStatus(entry.toStatus)}`;
}

function OrderList({ orders }: { orders: CustomerOrderListItemDto[] }) {
  return (
    <div className="mt-5 grid gap-3">
      {orders.map((order) => (
        <Link key={order.id} to={`/account/orders/${order.id}`} className="grid gap-3 rounded-[var(--radius-card)] border border-slate-100 bg-slate-50 p-4 transition hover:border-slate-300 hover:bg-white sm:grid-cols-[1fr_auto] sm:items-center">
          <div className="min-w-0">
            <div className="flex flex-wrap items-center gap-2">
              <p className="font-mono text-base font-black text-slate-950">{order.orderCode}</p>
              <StatusBadge status={order.status} />
              <PaymentStatusBadge status={order.paymentStatus} />
            </div>
            <p className="ui-body mt-1 text-slate-600">
              {formatDate(order.createdAt)} - {order.itemCount} item{order.itemCount === 1 ? "" : "s"} - {formatPaymentMethod(order.paymentMethod)}
            </p>
            {order.trackingCode ? (
              <p className="mt-2 text-sm font-semibold text-slate-700">{storefrontI18n.t("uiCopy.tracking_code")}<span className="font-mono text-slate-950">{order.trackingCode}</span>
                <span className="text-slate-600"> · </span>
                Carrier: {order.shipmentProvider ?? "MiniLogistics"}
              </p>
            ) : (
              <p className="mt-2 text-sm font-medium text-slate-600">{storefrontI18n.t("uiCopy.shipment_is_being_prepared")}</p>
            )}
            {order.shipping && <p className="mt-1 text-sm text-slate-600">{order.shipping.method === 0 ? "WorkspaceEC" : "MiniLogistics"} · {formatMoney(order.shipping.customerCharge)}</p>}
          </div>
          <div className="text-left sm:text-right">
            <p className="ui-price text-slate-950">{formatMoney(order.totalAmount)}</p>
            <p className="ui-caption mt-0.5 text-slate-600">Updated {formatDate(order.updatedAt)}</p>
          </div>
        </Link>
      ))}
    </div>
  );
}

function StatusBadge({ status }: { status: OrderStatus }) {
  return (
    <span className={`inline-flex rounded-full px-3 py-1 text-xs font-black ${statusStyles[status] ?? "bg-slate-100 text-slate-600"}`}>
      {formatOrderStatus(status)}
    </span>
  );
}

function PaymentStatusBadge({ status }: { status: PaymentStatus }) {
  return (
    <span className={`inline-flex rounded-full px-3 py-1 text-xs font-black ${paymentStatusStyles[status] ?? "bg-slate-100 text-slate-600"}`}>
      {formatPaymentStatus(status)}
    </span>
  );
}

function MetricCard({ label, value }: { label: string; value: string }) {
  return (
    <div className="ui-card border border-slate-100 p-5">
      <p className="ui-caption uppercase tracking-[0.18em] text-slate-600">{label}</p>
      <p className="mt-2 truncate text-xl font-black text-slate-950">{value}</p>
    </div>
  );
}

function InfoBlock({ label, value }: { label: string; value: string }) {
  return (
    <div className="grid gap-0.5">
      <span className="ui-caption uppercase tracking-[0.14em] text-slate-600">{label}</span>
      <span className="break-words text-sm font-bold text-slate-800">{value}</span>
    </div>
  );
}

function FormField({ children, error, label }: { children: ReactNode; error?: string; label: string }) {
  return (
    <label className="grid gap-1.5">
      <span className="ui-control text-slate-700">{label}</span>
      {children}
      {error ? <span className="ui-caption text-red-600">{error}</span> : null}
    </label>
  );
}

function OrderQueryState({ error, isLoading }: { error: unknown; isLoading: boolean }) {
  if (isLoading) {
    return <StateMessage>{storefrontI18n.t("uiCopy.loading_orders")}</StateMessage>;
  }

  if (error) {
    return <StateMessage tone="error">{getApiErrorMessage(error)}</StateMessage>;
  }

  return null;
}

function EmptyOrdersState() {
  return (
    <div className="mt-5 rounded-[var(--radius-card)] border border-dashed border-slate-200 bg-slate-50 p-6 text-center">
      <h3 className="ui-h3 text-slate-950">{storefrontI18n.t("uiCopy.no_account_orders_yet")}</h3>
      <p className="ui-body mx-auto mt-2 max-w-xl text-slate-600">{storefrontI18n.t("uiCopy.orders_placed_while_signed_in_will_appear_here_guest_orders_can_still_be_checked_with_order_code_and_phone_number")}</p>
      <div className="mt-5 flex flex-wrap justify-center gap-3">
        <Link to="/products" className="ui-control flex h-11 items-center rounded-[var(--radius-control)] bg-slate-950 px-5 text-white transition hover:bg-slate-800">{storefrontI18n.t("uiCopy.shop_products")}</Link>
        <Link to="/orders/lookup" className="ui-control flex h-11 items-center rounded-[var(--radius-control)] border border-slate-200 px-5 text-slate-700 transition hover:border-slate-950 hover:text-slate-950">{storefrontI18n.t("uiCopy.lookup_guest_order")}</Link>
      </div>
    </div>
  );
}

function StateMessage({ children, tone = "info" }: { children: ReactNode; tone?: "info" | "error" }) {
  return (
    <div className={`mt-5 rounded-[var(--radius-card)] px-5 py-4 text-sm font-semibold ${tone === "error" ? "bg-red-50 text-red-700" : "bg-slate-50 text-slate-600"}`}>
      {children}
    </div>
  );
}

function fieldClassName(hasError: boolean): string {
  return `h-11 w-full rounded-[var(--radius-control)] border px-4 text-sm font-semibold text-slate-950 outline-none transition focus:ring-2 focus:ring-[var(--brand)]/20 ${
    hasError ? "border-red-400 bg-red-50" : "border-slate-200 bg-white focus:border-[var(--brand)]"
  }`;
}

function parsePositiveInt(value: string): number {
  const parsed = Number.parseInt(value, 10);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : 0;
}

function clampPercent(value: number): number {
  return Math.min(100, Math.max(0, value));
}

function formatTierType(tier: LoyaltyTierType): string {
  const labels: Record<LoyaltyTierType, string> = {
    0: "Bronze",
    1: "Silver",
    2: "Gold",
    3: "Platinum"
  };

  return labels[tier] ?? "Tier";
}

function formatLoyaltyTransactionType(type: LoyaltyTransactionType): string {
  const labels: Record<LoyaltyTransactionType, string> = {
    0: "Earn",
    1: "Redeem",
    2: "Adjust"
  };

  return labels[type] ?? "Transaction";
}

function formatPoints(points: number): string {
  return `${points > 0 ? "+" : ""}${points.toLocaleString("en-US")} pts`;
}

function formatShortId(value: string): string {
  return value.replaceAll("-", "").slice(0, 8).toUpperCase();
}

function formatVoucherAmount(value: number): string {
  return new Intl.NumberFormat("vi-VN", {
    style: "currency",
    currency: "VND",
    maximumFractionDigits: 0,
    minimumFractionDigits: 0
  }).format(value);
}

function formatOptionalDate(value: string | null | undefined): string {
  if (!value) {
    return storefrontI18n.t("uiCopy.not_synced_yet");
  }

  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? storefrontI18n.t("uiCopy.not_synced_yet") : formatDate(date);
}

function AccountPagination({ page, total, onChange }: { page: number; total: number; onChange: (page: number) => void }) {
  const { t } = useTranslation();
  const pages = Math.max(1, Math.ceil(total / 20));
  if (pages <= 1 && page === 1) return null;
  return <nav aria-label={t("pagination.label")} className="my-5 flex items-center justify-between gap-3">
    <button type="button" disabled={page <= 1} onClick={() => onChange(page - 1)} className="min-h-11 rounded-lg border px-4 disabled:opacity-50">{t("pagination.previous")}</button>
    <span aria-live="polite">{t("pagination.page", { page, pages })}</span>
    <button type="button" disabled={page >= pages} onClick={() => onChange(page + 1)} className="min-h-11 rounded-lg border px-4 disabled:opacity-50">{t("pagination.next")}</button>
  </nav>;
}
