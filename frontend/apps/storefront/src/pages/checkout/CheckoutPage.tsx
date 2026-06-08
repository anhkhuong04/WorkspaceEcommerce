import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery } from "@tanstack/react-query";
import { formatMoney } from "@workspace-ecommerce/shared-utils";
import { useEffect } from "react";
import { useForm } from "react-hook-form";
import { Link, useNavigate } from "react-router-dom";
import { z } from "zod";
import { getApiErrorMessage } from "../../services/api/errors";
import { storefrontApi } from "../../services/api/storefrontApi";
import { getCartSessionId, resetCartSessionId } from "../../services/cartSession";

// ─── Schema ───────────────────────────────────────────────────────────────────

const checkoutSchema = z.object({
  customerName: z.string().min(2, "Họ tên phải có ít nhất 2 ký tự"),
  customerPhone: z.string().min(8, "Số điện thoại không hợp lệ"),
  customerEmail: z.string().email("Email không hợp lệ").optional().or(z.literal("")),
  shippingAddress: z.string().min(5, "Địa chỉ phải có ít nhất 5 ký tự"),
  note: z.string().optional(),
  paymentMethod: z.enum(["0", "1"])
});

type CheckoutFormValues = z.infer<typeof checkoutSchema>;

// ─── Helpers ──────────────────────────────────────────────────────────────────

function FieldError({ message }: { message?: string }) {
  if (!message) return null;
  return <p className="mt-1 text-xs font-semibold text-red-600">{message}</p>;
}

interface InputProps extends React.InputHTMLAttributes<HTMLInputElement> {
  label: string;
  error?: string;
  required?: boolean;
}

function FormInput({ label, error, required, ...props }: InputProps) {
  return (
    <label className="grid gap-1">
      <span className="text-sm font-bold text-slate-700">
        {label}
        {required && <span className="ml-0.5 text-red-500">*</span>}
      </span>
      <input
        className={`h-11 rounded-2xl border px-4 font-medium outline-none transition focus:ring-2 focus:ring-[var(--brand)]/30 ${
          error
            ? "border-red-400 bg-red-50 focus:border-red-500"
            : "border-slate-200 bg-white focus:border-[var(--brand)]"
        }`}
        {...props}
      />
      <FieldError message={error} />
    </label>
  );
}

// ─── CheckoutPage ─────────────────────────────────────────────────────────────

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

  // Redirect to cart when cart becomes empty
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
      {/* ── Page header ───────────────────────────────────────────── */}
      <section className="rounded-[2rem] border border-slate-200 bg-white p-8 shadow-sm">
        <p className="text-sm font-bold uppercase tracking-[0.2em] text-[var(--brand)]">Checkout</p>
        <div className="mt-3 grid gap-4 lg:grid-cols-[1fr_420px] lg:items-end">
          <h1 className="max-w-4xl text-5xl font-black tracking-tight text-slate-950">
            Đặt hàng
          </h1>
          <p className="text-base leading-7 text-slate-600">
            Điền thông tin nhận hàng và chọn phương thức thanh toán.
          </p>
        </div>
      </section>

      {/* ── Loading cart ──────────────────────────────────────────── */}
      {isCartLoading && (
        <div className="rounded-[2rem] border border-slate-200 bg-white p-10 text-center text-slate-500 shadow-sm">
          Đang tải thông tin giỏ hàng...
        </div>
      )}

      {/* ── Cart error ────────────────────────────────────────────── */}
      {cartQuery.isError && (
        <div className="rounded-[2rem] bg-red-50 p-6 font-semibold text-red-700">
          {getApiErrorMessage(cartQuery.error)} —{" "}
          <Link to="/cart" className="underline">
            Quay lại giỏ hàng
          </Link>
        </div>
      )}

      {/* ── Empty cart redirect notice ────────────────────────────── */}
      {isEmptyCart && (
        <div className="rounded-[2rem] border border-slate-200 bg-white p-8 text-center text-slate-500 shadow-sm">
          Giỏ hàng trống. Đang chuyển hướng...
        </div>
      )}

      {/* ── Main form + cart summary ──────────────────────────────── */}
      {cart && cart.items.length > 0 && (
        <form
          onSubmit={handleSubmit(onSubmit)}
          className="grid gap-6 lg:grid-cols-[1fr_380px] lg:items-start"
        >
          {/* Left: recipient info */}
          <section className="grid gap-5 rounded-[2rem] border border-slate-200 bg-white p-8 shadow-sm">
            <h2 className="text-xl font-black text-slate-950">Thông tin người nhận</h2>

            <div className="grid gap-4 sm:grid-cols-2">
              <FormInput
                label="Họ tên"
                required
                placeholder="Nguyễn Văn A"
                autoComplete="name"
                {...register("customerName")}
                error={errors.customerName?.message}
              />
              <FormInput
                label="Số điện thoại"
                required
                type="tel"
                placeholder="0900 000 000"
                autoComplete="tel"
                {...register("customerPhone")}
                error={errors.customerPhone?.message}
              />
            </div>

            <FormInput
              label="Email"
              type="email"
              placeholder="email@example.com (tuỳ chọn)"
              autoComplete="email"
              {...register("customerEmail")}
              error={errors.customerEmail?.message}
            />

            <FormInput
              label="Địa chỉ giao hàng"
              required
              placeholder="Số nhà, đường, quận/huyện, tỉnh/thành phố"
              autoComplete="street-address"
              {...register("shippingAddress")}
              error={errors.shippingAddress?.message}
            />

            <label className="grid gap-1">
              <span className="text-sm font-bold text-slate-700">Ghi chú</span>
              <textarea
                rows={3}
                placeholder="Ghi chú thêm cho đơn hàng (tuỳ chọn)"
                className="rounded-2xl border border-slate-200 bg-white px-4 py-3 font-medium outline-none transition focus:border-[var(--brand)] focus:ring-2 focus:ring-[var(--brand)]/30 resize-none"
                {...register("note")}
              />
            </label>

            <fieldset className="grid gap-2">
              <legend className="text-sm font-bold text-slate-700">
                Phương thức thanh toán <span className="ml-0.5 text-red-500">*</span>
              </legend>
              <div className="grid gap-2 sm:grid-cols-2">
                {([
                  { value: "0", label: "COD — Thanh toán khi nhận hàng", icon: "💵" },
                  { value: "1", label: "Chuyển khoản thủ công", icon: "🏦" }
                ] as const).map(({ value, label, icon }) => (
                  <label
                    key={value}
                    className="flex cursor-pointer items-center gap-3 rounded-2xl border border-slate-200 bg-slate-50 p-4 transition has-[:checked]:border-[var(--brand)] has-[:checked]:bg-[var(--brand-soft)]"
                  >
                    <input
                      type="radio"
                      value={value}
                      className="accent-[var(--brand)]"
                      {...register("paymentMethod")}
                    />
                    <span className="text-sm font-bold text-slate-800">
                      {icon} {label}
                    </span>
                  </label>
                ))}
              </div>
            </fieldset>
          </section>

          {/* Right: cart summary + submit */}
          <aside className="grid gap-4">
            <section className="rounded-[2rem] border border-slate-200 bg-white p-6 shadow-sm">
              <h2 className="text-sm font-bold uppercase tracking-[0.18em] text-[var(--brand)]">
                Đơn hàng của bạn
              </h2>

              <div className="mt-4 grid gap-3">
                {cart.items.map((item) => (
                  <div
                    key={item.id}
                    className="flex items-center justify-between gap-3 border-b border-slate-100 pb-3 last:border-0 last:pb-0"
                  >
                    <div className="min-w-0">
                      <p className="truncate text-xs font-bold uppercase tracking-[0.12em] text-slate-400">
                        Variant ID
                      </p>
                      <p className="truncate font-mono text-xs font-bold text-slate-800">
                        {item.productVariantId.slice(0, 8)}…
                      </p>
                      <p className="mt-0.5 text-xs text-slate-500">
                        {formatMoney(item.unitPriceSnapshot)} × {item.quantity}
                      </p>
                    </div>
                    <p className="shrink-0 text-sm font-black text-slate-950">
                      {formatMoney(item.lineTotal)}
                    </p>
                  </div>
                ))}
              </div>

              <div className="mt-5 grid gap-2 border-t border-slate-200 pt-4">
                <div className="flex justify-between text-sm font-bold text-slate-500">
                  <span>Số lượng</span>
                  <span>{cart.totalQuantity} sản phẩm</span>
                </div>
                <div className="flex justify-between text-lg font-black text-slate-950">
                  <span>Tổng tiền</span>
                  <span className="text-[var(--brand)]">{formatMoney(cart.totalAmount)}</span>
                </div>
              </div>
            </section>

            {/* Mutation error */}
            {checkoutMutation.isError && (
              <div className="rounded-2xl bg-red-50 px-5 py-4 text-sm font-semibold text-red-700">
                {getApiErrorMessage(checkoutMutation.error)}
              </div>
            )}

            <button
              type="submit"
              disabled={checkoutMutation.isPending}
              className="h-14 rounded-full bg-[var(--brand)] px-8 text-base font-black text-white shadow transition hover:brightness-110 disabled:cursor-not-allowed disabled:opacity-60"
            >
              {checkoutMutation.isPending ? "Đang xử lý…" : "Xác nhận đặt hàng"}
            </button>

            <Link
              to="/cart"
              className="text-center text-sm font-bold text-slate-500 underline transition hover:text-slate-800"
            >
              ← Quay lại giỏ hàng
            </Link>
          </aside>
        </form>
      )}
    </div>
  );
}
