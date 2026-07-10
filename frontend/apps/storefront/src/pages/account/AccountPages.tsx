import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import type {
  CustomerOrderDto,
  CustomerOrderListItemDto,
  CustomerOrderStatusHistoryDto,
  LoyaltyAccountDto,
  LoyaltyTierDto,
  LoyaltyTierType,
  LoyaltyTransactionDto,
  LoyaltyTransactionType,
  OrderStatus,
  PaymentStatus,
  RedeemLoyaltyPointsResponse
} from "@workspace-ecommerce/api-types";
import { formatDate, formatMoney, formatOrderStatus, formatPaymentMethod, formatPaymentStatus } from "@workspace-ecommerce/shared-utils";
import type { ReactNode } from "react";
import { useEffect, useState } from "react";
import { useForm } from "react-hook-form";
import { Link, NavLink, useNavigate, useParams } from "react-router-dom";
import { z } from "zod";
import { PageHeader } from "../../components/ui/PageHeader";
import { useCustomerAuth } from "../../features/customer-auth/useCustomerAuth";
import { getApiErrorMessage } from "../../services/api/errors";
import { storefrontApi } from "../../services/api/storefrontApi";

const profileSchema = z.object({
  fullName: z.string().trim().min(2, "Full name is required.").max(250, "Full name is too long."),
  phoneNumber: z.string().trim().min(8, "Phone number is invalid.").max(50, "Phone number is too long.")
});

type ProfileFormValues = z.infer<typeof profileSchema>;

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
  4: "bg-slate-100 text-slate-700"
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
  const { customer } = useCustomerAuth();
  const ordersQuery = useQuery({
    queryKey: ["customer", "orders", "recent"],
    queryFn: () => storefrontApi.getCustomerOrders({ pageNumber: 1, pageSize: 3 })
  });

  const recentOrders = ordersQuery.data?.items ?? [];

  return (
    <AccountShell>
      <div className="grid gap-6">
        <section className="grid gap-4 md:grid-cols-3">
          <MetricCard label="Saved contact" value={customer?.phoneNumber || "Not set"} />
          <MetricCard label="Orders" value={ordersQuery.data ? String(ordersQuery.data.totalCount) : "..."} />
          <MetricCard label="Member since" value={formatOptionalDate(customer?.createdAt)} />
        </section>

        <section className="grid gap-5 lg:grid-cols-[minmax(0,1fr)_360px]">
          <div className="ui-card border border-slate-100 p-6">
            <div className="flex flex-wrap items-start justify-between gap-3">
              <div>
                <p className="ui-caption uppercase tracking-[0.18em] text-[var(--brand)]">Profile</p>
                <h2 className="ui-h2 mt-2 text-slate-950">{customer?.fullName || "Customer account"}</h2>
              </div>
              <Link to="/account/profile" className="ui-control rounded-[var(--radius-control)] border border-slate-200 px-4 py-2 text-slate-700 transition hover:border-slate-950 hover:text-slate-950">
                Edit profile
              </Link>
            </div>

            <div className="mt-5 grid gap-4 sm:grid-cols-2">
              <InfoBlock label="Email" value={customer?.email || "-"} />
              <InfoBlock label="Phone" value={customer?.phoneNumber || "-"} />
              <InfoBlock label="Created" value={formatOptionalDate(customer?.createdAt)} />
              <InfoBlock label="Last updated" value={formatOptionalDate(customer?.updatedAt)} />
            </div>
          </div>

          <aside className="ui-card border border-slate-100 p-6">
            <p className="ui-caption uppercase tracking-[0.18em] text-[var(--brand)]">Need help</p>
            <div className="mt-4 grid gap-3">
              <Link to="/products" className="ui-control flex h-11 items-center justify-center rounded-[var(--radius-control)] bg-slate-950 text-white transition hover:bg-slate-800">
                Continue shopping
              </Link>
              <Link to="/orders/lookup" className="ui-control flex h-11 items-center justify-center rounded-[var(--radius-control)] border border-slate-200 text-slate-700 transition hover:border-slate-950 hover:text-slate-950">
                Public order lookup
              </Link>
            </div>
          </aside>
        </section>

        <section className="ui-card border border-slate-100 p-6">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <div>
              <p className="ui-caption uppercase tracking-[0.18em] text-[var(--brand)]">Recent orders</p>
              <h2 className="ui-h2 mt-2 text-slate-950">Order history</h2>
            </div>
            <Link to="/account/orders" className="ui-control text-slate-700 underline underline-offset-4 transition hover:text-slate-950">
              View all
            </Link>
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
  const { customer, updateCustomer } = useCustomerAuth();
  const queryClient = useQueryClient();
  const [successMessage, setSuccessMessage] = useState<string | null>(null);
  const {
    register,
    handleSubmit,
    reset,
    formState: { errors }
  } = useForm<ProfileFormValues>({
    resolver: zodResolver(profileSchema),
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
      setSuccessMessage("Profile updated.");
      void queryClient.invalidateQueries({ queryKey: ["customer"] });
    },
    onError: () => setSuccessMessage(null)
  });

  return (
    <AccountShell>
      <section className="ui-card border border-slate-100 p-6">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div>
            <p className="ui-caption uppercase tracking-[0.18em] text-[var(--brand)]">Profile</p>
            <h2 className="ui-h2 mt-2 text-slate-950">Contact details</h2>
          </div>
          <span className="ui-caption rounded-full bg-slate-100 px-3 py-1 text-slate-600">Email cannot be changed</span>
        </div>

        <form className="mt-6 grid gap-5" onSubmit={handleSubmit((values) => profileMutation.mutate(values))}>
          <div className="grid gap-4 sm:grid-cols-2">
            <FormField label="Full name" error={errors.fullName?.message}>
              <input
                autoComplete="name"
                className={fieldClassName(Boolean(errors.fullName))}
                {...register("fullName")}
              />
            </FormField>

            <FormField label="Phone number" error={errors.phoneNumber?.message}>
              <input
                type="tel"
                autoComplete="tel"
                className={fieldClassName(Boolean(errors.phoneNumber))}
                {...register("phoneNumber")}
              />
            </FormField>

            <FormField label="Email">
              <input
                type="email"
                value={customer?.email ?? ""}
                disabled
                className={`${fieldClassName(false)} cursor-not-allowed bg-slate-50 text-slate-500`}
              />
            </FormField>
          </div>

          {profileMutation.error ? <div className="ui-control rounded-[var(--radius-card)] bg-red-50 px-4 py-3 text-red-700">{getApiErrorMessage(profileMutation.error)}</div> : null}
          {successMessage ? <div className="ui-control rounded-[var(--radius-card)] bg-emerald-50 px-4 py-3 text-emerald-700">{successMessage}</div> : null}

          <div className="flex flex-wrap gap-3">
            <button type="submit" disabled={profileMutation.isPending} className="ui-control h-11 rounded-[var(--radius-control)] bg-slate-950 px-6 text-white transition hover:bg-slate-800 disabled:cursor-not-allowed disabled:opacity-60">
              {profileMutation.isPending ? "Saving..." : "Save changes"}
            </button>
            <Link to="/account" className="ui-control flex h-11 items-center rounded-[var(--radius-control)] border border-slate-200 px-6 text-slate-700 transition hover:border-slate-950 hover:text-slate-950">
              Back to overview
            </Link>
          </div>
        </form>
      </section>
    </AccountShell>
  );
}

export function AccountOrdersPage() {
  const ordersQuery = useQuery({
    queryKey: ["customer", "orders", "all"],
    queryFn: () => storefrontApi.getCustomerOrders({ pageNumber: 1, pageSize: 20 })
  });

  return (
    <AccountShell>
      <section className="ui-card border border-slate-100 p-6">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div>
            <p className="ui-caption uppercase tracking-[0.18em] text-[var(--brand)]">Orders</p>
            <h2 className="ui-h2 mt-2 text-slate-950">Your order history</h2>
          </div>
          {ordersQuery.data ? <span className="ui-caption rounded-full bg-slate-100 px-3 py-1 text-slate-600">{ordersQuery.data.totalCount} total</span> : null}
        </div>

        <OrderQueryState isLoading={ordersQuery.isLoading} error={ordersQuery.error} />
        {!ordersQuery.isLoading && !ordersQuery.error && (
          ordersQuery.data && ordersQuery.data.items.length > 0 ? <OrderList orders={ordersQuery.data.items} /> : <EmptyOrdersState />
        )}
      </section>
    </AccountShell>
  );
}

export function AccountLoyaltyPage() {
  const queryClient = useQueryClient();
  const [transactionPage, setTransactionPage] = useState(1);
  const [pointsInput, setPointsInput] = useState("");
  const [voucher, setVoucher] = useState<RedeemLoyaltyPointsResponse | null>(null);

  const accountQuery = useQuery({
    queryKey: ["customer", "loyalty", "account"],
    queryFn: () => storefrontApi.getMyLoyalty()
  });
  const tiersQuery = useQuery({
    queryKey: ["loyalty", "tiers"],
    queryFn: () => storefrontApi.getLoyaltyTiers()
  });
  const transactionsQuery = useQuery({
    queryKey: ["customer", "loyalty", "transactions", transactionPage],
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
              <p className="ui-caption uppercase tracking-[0.18em] text-[var(--brand)]">Loyalty</p>
              <h2 className="ui-h2 mt-2 text-slate-950">Membership wallet</h2>
            </div>
            {account ? <TierBadge tier={account.currentTier} /> : null}
          </div>

          {accountQuery.isLoading ? <StateMessage>Loading loyalty account...</StateMessage> : null}
          {accountQuery.error ? <StateMessage tone="error">{getApiErrorMessage(accountQuery.error)}</StateMessage> : null}
          {account ? <LoyaltyAccountSummary account={account} tiers={tiers} /> : null}
        </section>

        <section className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_360px]">
          <section className="ui-card border border-slate-100 p-6">
            <div className="flex flex-wrap items-start justify-between gap-3">
              <div>
                <p className="ui-caption uppercase tracking-[0.18em] text-[var(--brand)]">Tiers</p>
                <h2 className="ui-h2 mt-2 text-slate-950">Benefits</h2>
              </div>
              {tiers.length ? <span className="ui-caption rounded-full bg-slate-100 px-3 py-1 text-slate-600">{tiers.length} levels</span> : null}
            </div>

            {tiersQuery.isLoading ? <StateMessage>Loading tiers...</StateMessage> : null}
            {tiersQuery.error ? <StateMessage tone="error">{getApiErrorMessage(tiersQuery.error)}</StateMessage> : null}
            {!tiersQuery.isLoading && !tiersQuery.error ? <TierBenefitsTable tiers={tiers} currentTier={account?.currentTier ?? 0} /> : null}
          </section>

          <section className="ui-card border border-slate-100 p-6">
            <p className="ui-caption uppercase tracking-[0.18em] text-[var(--brand)]">Redeem</p>
            <h2 className="ui-h2 mt-2 text-slate-950">Create voucher</h2>

            <div className="mt-5 grid gap-4">
              <FormField label="Points to redeem">
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
                <InfoBlock label="Voucher value" value={previewDiscount > 0 ? formatVoucherAmount(previewDiscount) : "-"} />
                <div className="mt-3">
                  <InfoBlock label="Remaining points" value={account ? String(Math.max(0, account.currentPoints - redeemPoints)) : "-"} />
                </div>
              </div>

              {redeemPoints > 0 && account && redeemPoints > account.currentPoints ? (
                <div className="ui-control rounded-[var(--radius-card)] bg-amber-50 px-4 py-3 text-amber-800">You do not have enough points for this voucher.</div>
              ) : null}
              {redeemMutation.error ? <div className="ui-control rounded-[var(--radius-card)] bg-red-50 px-4 py-3 text-red-700">{getApiErrorMessage(redeemMutation.error)}</div> : null}
              {voucher ? <RedeemedVoucher voucher={voucher} /> : null}

              <button
                type="button"
                disabled={!canRedeem || redeemMutation.isPending}
                onClick={() => redeemMutation.mutate(redeemPoints)}
                className="ui-control h-11 rounded-[var(--radius-control)] bg-slate-950 px-5 text-white transition hover:bg-slate-800 disabled:cursor-not-allowed disabled:opacity-60"
              >
                {redeemMutation.isPending ? "Creating..." : "Redeem points"}
              </button>
            </div>
          </section>
        </section>

        <section className="ui-card border border-slate-100 p-6">
          <div className="flex flex-wrap items-start justify-between gap-3">
            <div>
              <p className="ui-caption uppercase tracking-[0.18em] text-[var(--brand)]">History</p>
              <h2 className="ui-h2 mt-2 text-slate-950">Point transactions</h2>
            </div>
            {transactionsQuery.data ? <span className="ui-caption rounded-full bg-slate-100 px-3 py-1 text-slate-600">{transactionsQuery.data.totalCount} total</span> : null}
          </div>

          {transactionsQuery.isLoading ? <StateMessage>Loading transactions...</StateMessage> : null}
          {transactionsQuery.error ? <StateMessage tone="error">{getApiErrorMessage(transactionsQuery.error)}</StateMessage> : null}
          {!transactionsQuery.isLoading && !transactionsQuery.error ? (
            <LoyaltyTransactionList transactions={transactionsQuery.data?.items ?? []} />
          ) : null}
          {transactionsQuery.data ? (
            <div className="mt-5 flex flex-wrap items-center justify-between gap-3 border-t border-slate-100 pt-4">
              <span className="ui-caption text-slate-500">Page {transactionsQuery.data.pageNumber} of {transactionsQuery.data.totalPages || 1}</span>
              <div className="flex gap-2">
                <button
                  type="button"
                  disabled={!transactionsQuery.data.hasPreviousPage}
                  onClick={() => setTransactionPage((page) => Math.max(1, page - 1))}
                  className="ui-control h-10 rounded-[var(--radius-control)] border border-slate-200 px-4 text-slate-700 transition hover:border-slate-950 hover:text-slate-950 disabled:cursor-not-allowed disabled:opacity-50"
                >
                  Previous
                </button>
                <button
                  type="button"
                  disabled={!transactionsQuery.data.hasNextPage}
                  onClick={() => setTransactionPage((page) => page + 1)}
                  className="ui-control h-10 rounded-[var(--radius-control)] border border-slate-200 px-4 text-slate-700 transition hover:border-slate-950 hover:text-slate-950 disabled:cursor-not-allowed disabled:opacity-50"
                >
                  Next
                </button>
              </div>
            </div>
          ) : null}
        </section>
      </div>
    </AccountShell>
  );
}

export function AccountOrderDetailPage() {
  const { id } = useParams();
  const orderQuery = useQuery({
    queryKey: ["customer", "order", id],
    queryFn: () => storefrontApi.getCustomerOrder(id ?? ""),
    enabled: Boolean(id)
  });

  return (
    <AccountShell>
      {!id ? <StateMessage tone="error">Missing order id.</StateMessage> : null}
      {orderQuery.isLoading ? <StateMessage>Loading order...</StateMessage> : null}
      {orderQuery.error ? <StateMessage tone="error">{getApiErrorMessage(orderQuery.error)}</StateMessage> : null}
      {orderQuery.data ? <OrderDetail order={orderQuery.data} /> : null}
    </AccountShell>
  );
}

function AccountShell({ children }: { children: ReactNode }) {
  const { customer, signOut } = useCustomerAuth();
  const navigate = useNavigate();

  function handleSignOut() {
    signOut();
    void navigate("/", { replace: true });
  }

  return (
    <div className="grid gap-6">
      <PageHeader
        eyebrow="Account"
        title={`Hello${customer?.fullName ? `, ${customer.fullName}` : ""}`}
        description="Manage your profile, review your orders, and continue shopping with your saved account."
      />

      <div className="grid gap-6 lg:grid-cols-[250px_minmax(0,1fr)] lg:items-start">
        <aside className="ui-card border border-slate-100 p-4">
          <nav className="grid gap-1">
            <AccountNavLink to="/account" end>Overview</AccountNavLink>
            <AccountNavLink to="/account/profile">Profile</AccountNavLink>
            <AccountNavLink to="/account/orders">Orders</AccountNavLink>
            <AccountNavLink to="/account/loyalty">Loyalty</AccountNavLink>
          </nav>
          <button type="button" onClick={handleSignOut} className="ui-control mt-4 flex h-11 w-full items-center justify-center rounded-[var(--radius-control)] border border-slate-200 text-slate-700 transition hover:border-red-200 hover:bg-red-50 hover:text-red-700">
            Sign out
          </button>
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
      <section className="grid gap-4 md:grid-cols-4">
        <MetricCard label="Current points" value={account.currentPoints.toLocaleString("en-US")} />
        <MetricCard label="Total earned" value={account.totalPointsEarned.toLocaleString("en-US")} />
        <MetricCard label="Tier discount" value={`${account.discountPercent}%`} />
        <MetricCard label="Free shipping" value={account.freeShippingEnabled ? "Enabled" : "No"} />
      </section>

      <section className="rounded-[var(--radius-card)] border border-slate-100 bg-slate-50 p-5">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div>
            <p className="ui-caption uppercase tracking-[0.18em] text-slate-400">Tier progress</p>
            <p className="mt-1 text-lg font-black text-slate-950">
              {nextTier ? `${account.pointsToNextTier?.toLocaleString("en-US") ?? 0} points to ${formatTierType(nextTier.type)}` : "Top tier reached"}
            </p>
          </div>
          <div className="flex items-center gap-2">
            <TierBadge tier={account.currentTier} />
            {nextTier ? <span className="text-slate-400">to</span> : null}
            {nextTier ? <TierBadge tier={nextTier.type} /> : null}
          </div>
        </div>
        <div className="mt-4 h-3 overflow-hidden rounded-full bg-white">
          <div className="h-full rounded-full bg-slate-950 transition-all" style={{ width: `${progress}%` }} />
        </div>
        <div className="mt-2 flex justify-between text-xs font-bold text-slate-500">
          <span>{currentTierFloor.toLocaleString("en-US")} pts</span>
          <span>{nextTierFloor === null ? `${account.totalPointsEarned.toLocaleString("en-US")} pts` : `${nextTierFloor.toLocaleString("en-US")} pts`}</span>
        </div>
      </section>
    </div>
  );
}

function TierBenefitsTable({ currentTier, tiers }: { currentTier: LoyaltyTierType; tiers: LoyaltyTierDto[] }) {
  if (tiers.length === 0) {
    return <StateMessage>No tiers configured.</StateMessage>;
  }

  return (
    <div className="mt-5 overflow-x-auto">
      <table className="w-full text-sm">
        <thead>
          <tr className="border-b border-slate-100 text-left text-xs font-bold uppercase tracking-[0.14em] text-slate-400">
            <th className="pb-3 pr-4">Tier</th>
            <th className="pb-3 pr-4 text-right">Minimum points</th>
            <th className="pb-3 pr-4 text-right">Discount</th>
            <th className="pb-3 text-right">Free shipping</th>
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
                <td className="py-3 pr-4 text-right font-bold text-slate-700">{tier.discountPercent}%</td>
                <td className="py-3 text-right font-bold text-slate-700">{tier.freeShippingEnabled ? "Yes" : "No"}</td>
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
        <h3 className="ui-h3 text-slate-950">No loyalty transactions yet</h3>
        <p className="ui-body mx-auto mt-2 max-w-xl text-slate-500">Completed account orders and redeemed vouchers will appear here.</p>
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
              {transaction.orderId ? <span className="ui-caption font-mono text-slate-400">Order {formatShortId(transaction.orderId)}</span> : null}
              {transaction.voucherId ? <span className="ui-caption font-mono text-slate-400">Voucher {formatShortId(transaction.voucherId)}</span> : null}
            </div>
            <p className="mt-2 text-sm font-bold text-slate-950">{transaction.description}</p>
            <p className="ui-caption mt-1 text-slate-400">{formatDate(transaction.createdAt)}</p>
          </div>
          <div className="text-left sm:text-right">
            <p className={`text-lg font-black ${transaction.points >= 0 ? "text-emerald-700" : "text-rose-700"}`}>{formatPoints(transaction.points)}</p>
            <p className="ui-caption text-slate-400">Balance {transaction.balanceAfter.toLocaleString("en-US")}</p>
          </div>
        </div>
      ))}
    </div>
  );
}

function RedeemedVoucher({ voucher }: { voucher: RedeemLoyaltyPointsResponse }) {
  return (
    <div className="rounded-[var(--radius-card)] border border-emerald-100 bg-emerald-50 p-4">
      <p className="ui-caption uppercase tracking-[0.14em] text-emerald-700">Voucher created</p>
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

function OrderDetail({ order }: { order: CustomerOrderDto }) {
  return (
    <div className="grid gap-6">
      <section className="ui-card border border-slate-100 p-6">
        <div className="flex flex-wrap items-start justify-between gap-3 border-b border-slate-100 pb-5">
          <div>
            <p className="font-mono text-2xl font-black text-slate-950">{order.orderCode}</p>
            <p className="ui-body mt-1 text-slate-500">Created on {formatDate(order.createdAt)}</p>
          </div>
          <StatusBadge status={order.status} />
        </div>

        <div className="mt-5 grid gap-6 lg:grid-cols-[minmax(0,1fr)_340px]">
          <div>
            <h2 className="ui-h3 text-slate-950">Items</h2>
            <OrderItemsTable order={order} />
            <OrderTotals order={order} />
          </div>

          <aside className="grid h-fit gap-5">
            <section className="rounded-[var(--radius-card)] border border-slate-100 bg-slate-50 p-5">
              <h2 className="ui-caption uppercase tracking-[0.18em] text-[var(--brand)]">Recipient details</h2>
              <div className="mt-4 grid gap-3">
                <InfoBlock label="Full name" value={order.customerName} />
                <InfoBlock label="Phone" value={order.customerPhone} />
                {order.customerEmail ? <InfoBlock label="Email" value={order.customerEmail} /> : null}
                <InfoBlock label="Shipping address" value={order.shippingAddress} />
                {order.note ? <InfoBlock label="Note" value={order.note} /> : null}
                {order.couponCodeSnapshot ? <InfoBlock label="Coupon" value={formatCouponSnapshot(order)} /> : null}
                <InfoBlock label="Payment" value={formatPaymentMethod(order.paymentMethod)} />
                <InfoBlock label="Payment status" value={formatPaymentStatus(order.paymentStatus)} />
              </div>
            </section>

            <section className="rounded-[var(--radius-card)] border border-slate-100 bg-white p-5">
              <h2 className="ui-caption uppercase tracking-[0.18em] text-[var(--brand)]">Status timeline</h2>
              <StatusTimeline order={order} />
            </section>

            <Link to="/account/orders" className="ui-control flex h-11 items-center justify-center rounded-[var(--radius-control)] border border-slate-200 text-slate-700 transition hover:border-slate-950 hover:text-slate-950">
              Back to orders
            </Link>
          </aside>
        </div>
      </section>
    </div>
  );
}

function OrderItemsTable({ order }: { order: CustomerOrderDto }) {
  return (
    <div className="mt-3 overflow-x-auto">
      <table className="w-full text-sm">
        <thead>
          <tr className="border-b border-slate-100 text-left text-xs font-bold uppercase tracking-[0.14em] text-slate-400">
            <th className="pb-2 pr-4">Product</th>
            <th className="pb-2 pr-4 text-center">Qty</th>
            <th className="pb-2 pr-4 text-right">Unit price</th>
            <th className="pb-2 text-right">Line total</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-slate-100">
          {order.items.map((item) => (
            <tr key={item.id}>
              <td className="py-3 pr-4">
                <p className="font-bold text-slate-950">{item.productNameSnapshot}</p>
                <p className="mt-0.5 font-mono text-xs text-slate-400">{item.skuSnapshot}</p>
                {item.requiresInstallation ? <span className="ui-caption mt-1 inline-block rounded-full bg-amber-100 px-2 py-0.5 text-amber-800">Installation required</span> : null}
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
      <TotalLine label="Subtotal" value={formatMoney(order.subtotal)} />
      {order.shippingFee > 0 ? <TotalLine label="Shipping fee" value={formatMoney(order.shippingFee)} /> : null}
      {order.discountAmount > 0 ? <TotalLine label={formatDiscountLabel(order)} value={`-${formatMoney(order.discountAmount)}`} tone="success" /> : null}
      <div className="flex justify-between text-xl font-black text-slate-950">
        <span>Total</span>
        <span className="text-[var(--brand)]">{formatMoney(order.totalAmount)}</span>
      </div>
    </div>
  );
}

function TotalLine({ label, tone = "muted", value }: { label: string; tone?: "muted" | "success"; value: string }) {
  return (
    <div className={`flex justify-between text-sm font-bold ${tone === "success" ? "text-emerald-600" : "text-slate-500"}`}>
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
            note: "Order created.",
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
            <span className="mt-0.5 block text-xs font-medium text-slate-500">{formatDate(entry.changedAt)}</span>
            {entry.note ? <span className="mt-1 block text-sm text-slate-600">{entry.note}</span> : null}
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
            <p className="ui-body mt-1 text-slate-500">
              {formatDate(order.createdAt)} - {order.itemCount} item{order.itemCount === 1 ? "" : "s"} - {formatPaymentMethod(order.paymentMethod)}
            </p>
          </div>
          <div className="text-left sm:text-right">
            <p className="ui-price text-slate-950">{formatMoney(order.totalAmount)}</p>
            <p className="ui-caption mt-0.5 text-slate-400">Updated {formatDate(order.updatedAt)}</p>
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
      <p className="ui-caption uppercase tracking-[0.18em] text-slate-400">{label}</p>
      <p className="mt-2 truncate text-xl font-black text-slate-950">{value}</p>
    </div>
  );
}

function InfoBlock({ label, value }: { label: string; value: string }) {
  return (
    <div className="grid gap-0.5">
      <span className="ui-caption uppercase tracking-[0.14em] text-slate-400">{label}</span>
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
    return <StateMessage>Loading orders...</StateMessage>;
  }

  if (error) {
    return <StateMessage tone="error">{getApiErrorMessage(error)}</StateMessage>;
  }

  return null;
}

function EmptyOrdersState() {
  return (
    <div className="mt-5 rounded-[var(--radius-card)] border border-dashed border-slate-200 bg-slate-50 p-6 text-center">
      <h3 className="ui-h3 text-slate-950">No account orders yet</h3>
      <p className="ui-body mx-auto mt-2 max-w-xl text-slate-500">Orders placed while signed in will appear here. Guest orders can still be checked with order code and phone number.</p>
      <div className="mt-5 flex flex-wrap justify-center gap-3">
        <Link to="/products" className="ui-control flex h-11 items-center rounded-[var(--radius-control)] bg-slate-950 px-5 text-white transition hover:bg-slate-800">
          Shop products
        </Link>
        <Link to="/orders/lookup" className="ui-control flex h-11 items-center rounded-[var(--radius-control)] border border-slate-200 px-5 text-slate-700 transition hover:border-slate-950 hover:text-slate-950">
          Lookup guest order
        </Link>
      </div>
    </div>
  );
}

function StateMessage({ children, tone = "info" }: { children: ReactNode; tone?: "info" | "error" }) {
  return (
    <div className={`mt-5 rounded-[var(--radius-card)] px-5 py-4 text-sm font-semibold ${tone === "error" ? "bg-red-50 text-red-700" : "bg-slate-50 text-slate-500"}`}>
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
    return "Not synced yet";
  }

  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? "Not synced yet" : formatDate(date);
}
