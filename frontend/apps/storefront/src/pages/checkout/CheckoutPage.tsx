import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery } from "@tanstack/react-query";
import { formatMoney } from "@workspace-ecommerce/shared-utils";
import type { InputHTMLAttributes } from "react";
import { useEffect } from "react";
import { useForm } from "react-hook-form";
import { Link, useNavigate } from "react-router-dom";
import { z } from "zod";
import { PageHeader } from "../../components/ui/PageHeader";
import { getApiErrorMessage } from "../../services/api/errors";
import { storefrontApi } from "../../services/api/storefrontApi";
import { getCartSessionId, resetCartSessionId } from "../../services/cartSession";

const checkoutSchema = z.object({
  customerName: z.string().min(2, "Full name must contain at least 2 characters."),
  customerPhone: z.string().min(8, "Phone number is invalid."),
  customerEmail: z.string().email("Email is invalid.").optional().or(z.literal("")),
  shippingAddress: z.string().min(5, "Shipping address must contain at least 5 characters."),
  note: z.string().optional(),
  paymentMethod: z.enum(["0", "1"])
});

type CheckoutFormValues = z.infer<typeof checkoutSchema>;

function FieldError({ message }: { message?: string }) {
  if (!message) return null;
  return <p className="ui-caption mt-1 text-red-600">{message}</p>;
}

interface InputProps extends InputHTMLAttributes<HTMLInputElement> {
  label: string;
  error?: string;
  required?: boolean;
}

function FormInput({ label, error, required, ...props }: InputProps) {
  return (
    <label className="grid gap-1">
      <span className="ui-control text-slate-700">
        {label}
        {required && <span className="ml-0.5 text-red-500">*</span>}
      </span>
      <input
        className={`ui-control h-11 rounded-[var(--radius-control)] border px-4 outline-none transition focus:ring-2 focus:ring-[var(--brand)]/30 ${
          error ? "border-red-400 bg-red-50 focus:border-red-500" : "border-slate-200 bg-white focus:border-[var(--brand)]"
        }`}
        {...props}
      />
      <FieldError message={error} />
    </label>
  );
}

export function CheckoutPage() {
  const navigate = useNavigate();
  const sessionId = getCartSessionId();

  const cartQuery = useQuery({
    queryKey: ["storefront", "cart", sessionId],
    queryFn: () => storefrontApi.getCart(sessionId)
  });

  const {
    register,
    handleSubmit,
    formState: { errors }
  } = useForm<CheckoutFormValues>({
    resolver: zodResolver(checkoutSchema),
    defaultValues: {
      customerName: "",
      customerPhone: "",
      customerEmail: "",
      shippingAddress: "",
      note: "",
      paymentMethod: "0"
    }
  });

  const checkoutMutation = useMutation({
    mutationFn: (values: CheckoutFormValues) =>
      storefrontApi.checkout({
        sessionId,
        customerName: values.customerName,
        customerPhone: values.customerPhone,
        customerEmail: values.customerEmail || null,
        shippingAddress: values.shippingAddress,
        note: values.note || null,
        paymentMethod: Number(values.paymentMethod) as 0 | 1
      }),
    onSuccess: (response) => {
      resetCartSessionId();
      void navigate("/checkout/success", { state: { order: response.order } });
    }
  });

  useEffect(() => {
    if (cartQuery.data && cartQuery.data.items.length === 0) {
      void navigate("/cart");
    }
  }, [cartQuery.data, navigate]);

  const cart = cartQuery.data;
  const isCartLoading = cartQuery.isLoading;
  const isEmptyCart = cart && cart.items.length === 0;

  function onSubmit(values: CheckoutFormValues) {
    checkoutMutation.mutate(values);
  }

  return (
    <div className="grid gap-6">
      <PageHeader
        eyebrow="Checkout"
        title="Place your order"
        description="Enter recipient details and choose a payment method."
      />

      {isCartLoading && <div className="ui-card p-10 text-center text-slate-500">Loading cart...</div>}

      {cartQuery.isError && (
        <div className="rounded-[var(--radius-card)] bg-red-50 p-6 font-semibold text-red-700">
          {getApiErrorMessage(cartQuery.error)} - <Link to="/cart" className="underline">Back to cart</Link>
        </div>
      )}

      {isEmptyCart && <div className="ui-card p-8 text-center text-slate-500">Your cart is empty. Redirecting...</div>}

      {cart && cart.items.length > 0 && (
        <form onSubmit={handleSubmit(onSubmit)} className="grid gap-6 lg:grid-cols-[1fr_380px] lg:items-start">
          <section className="ui-card grid gap-5 border border-slate-100 p-8">
            <h2 className="ui-h2 text-slate-950">Recipient details</h2>

            <div className="grid gap-4 sm:grid-cols-2">
              <FormInput label="Full name" required placeholder="Alex Nguyen" autoComplete="name" {...register("customerName")} error={errors.customerName?.message} />
              <FormInput label="Phone number" required type="tel" placeholder="0900 000 000" autoComplete="tel" {...register("customerPhone")} error={errors.customerPhone?.message} />
            </div>

            <FormInput label="Email" type="email" placeholder="email@example.com (optional)" autoComplete="email" {...register("customerEmail")} error={errors.customerEmail?.message} />
            <FormInput label="Shipping address" required placeholder="Street, district, city" autoComplete="street-address" {...register("shippingAddress")} error={errors.shippingAddress?.message} />

            <label className="grid gap-1">
              <span className="ui-control text-slate-700">Note</span>
              <textarea
                rows={3}
                placeholder="Optional order note"
                className="ui-control resize-none rounded-[var(--radius-control)] border border-slate-200 bg-white px-4 py-3 outline-none transition focus:border-[var(--brand)] focus:ring-2 focus:ring-[var(--brand)]/30"
                {...register("note")}
              />
            </label>

            <fieldset className="grid gap-2">
              <legend className="ui-control text-slate-700">Payment method <span className="ml-0.5 text-red-500">*</span></legend>
              <div className="grid gap-2 sm:grid-cols-2">
                {([
                  { value: "0", label: "COD - Pay on delivery" },
                  { value: "1", label: "Manual bank transfer" }
                ] as const).map(({ value, label }) => (
                  <label key={value} className="flex cursor-pointer items-center gap-3 rounded-[var(--radius-card)] border border-slate-200 bg-slate-50 p-4 transition has-[:checked]:border-[var(--brand)] has-[:checked]:bg-[var(--brand-soft)]">
                    <input type="radio" value={value} className="accent-[var(--brand)]" {...register("paymentMethod")} />
                    <span className="ui-control text-slate-800">{label}</span>
                  </label>
                ))}
              </div>
            </fieldset>
          </section>

          <aside className="grid gap-4">
            <section className="ui-card border border-slate-100 p-6">
              <h2 className="ui-caption uppercase tracking-[0.18em] text-[var(--brand)]">Your order</h2>

              <div className="mt-4 grid gap-3">
                {cart.items.map((item) => (
                  <div key={item.id} className="flex items-center justify-between gap-3 border-b border-slate-100 pb-3 last:border-0 last:pb-0">
                    <div className="min-w-0">
                      <p className="ui-caption truncate uppercase tracking-[0.12em] text-slate-400">Variant ID</p>
                      <p className="truncate font-mono text-xs font-bold text-slate-800">{item.productVariantId.slice(0, 8)}...</p>
                      <p className="ui-caption mt-0.5 text-slate-500">{formatMoney(item.unitPriceSnapshot)} x {item.quantity}</p>
                    </div>
                    <p className="ui-control shrink-0 text-slate-950">{formatMoney(item.lineTotal)}</p>
                  </div>
                ))}
              </div>

              <div className="mt-5 grid gap-2 border-t border-slate-200 pt-4">
                <div className="flex justify-between text-sm font-bold text-slate-500">
                  <span>Quantity</span>
                  <span>{cart.totalQuantity} item{cart.totalQuantity === 1 ? "" : "s"}</span>
                </div>
                <div className="flex justify-between text-lg font-black text-slate-950">
                  <span>Total</span>
                  <span className="text-[var(--brand)]">{formatMoney(cart.totalAmount)}</span>
                </div>
              </div>
            </section>

            {checkoutMutation.isError && <div className="rounded-[var(--radius-card)] bg-red-50 px-5 py-4 text-sm font-semibold text-red-700">{getApiErrorMessage(checkoutMutation.error)}</div>}

            <button type="submit" disabled={checkoutMutation.isPending} className="ui-control h-14 rounded-[var(--radius-control)] bg-[var(--brand)] px-8 text-white shadow-[var(--shadow-card)] transition hover:brightness-110 disabled:cursor-not-allowed disabled:opacity-60">
              {checkoutMutation.isPending ? "Processing..." : "Place order"}
            </button>

            <Link to="/cart" className="ui-control text-center text-slate-500 underline transition hover:text-slate-800">Back to cart</Link>
          </aside>
        </form>
      )}
    </div>
  );
}
