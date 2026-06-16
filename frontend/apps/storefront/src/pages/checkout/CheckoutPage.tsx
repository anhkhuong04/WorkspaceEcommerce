import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery } from "@tanstack/react-query";
import type { CartItemDto } from "@workspace-ecommerce/api-types";
import type { InputHTMLAttributes, ReactNode, SelectHTMLAttributes, TextareaHTMLAttributes } from "react";
import { useEffect } from "react";
import { useForm } from "react-hook-form";
import { Link, useNavigate } from "react-router-dom";
import { z } from "zod";
import { getApiErrorMessage } from "../../services/api/errors";
import { storefrontApi } from "../../services/api/storefrontApi";
import { getCartSessionId, resetCartSessionId } from "../../services/cartSession";

const checkoutSchema = z.object({
  customerName: z.string().min(2, "Vui lòng nhập họ tên."),
  customerPhone: z.string().min(8, "Số điện thoại không hợp lệ."),
  customerEmail: z.string().email("Email không hợp lệ.").optional().or(z.literal("")),
  addressLine: z.string().min(5, "Vui lòng nhập địa chỉ giao hàng."),
  ward: z.string().min(2, "Vui lòng nhập phường/xã."),
  city: z.string().min(1, "Vui lòng chọn tỉnh/thành."),
  newsletter: z.boolean(),
  note: z.string().optional(),
  wantsVat: z.boolean(),
  companyName: z.string().optional(),
  taxCode: z.string().optional(),
  companyAddress: z.string().optional(),
  paymentMethod: z.enum(["0", "1"])
});

type CheckoutFormValues = z.infer<typeof checkoutSchema>;

interface FieldProps {
  label: string;
  error?: string;
  required?: boolean;
}

interface InputProps extends FieldProps, InputHTMLAttributes<HTMLInputElement> {}

interface SelectProps extends FieldProps, SelectHTMLAttributes<HTMLSelectElement> {
  children: ReactNode;
}

interface TextAreaProps extends FieldProps, TextareaHTMLAttributes<HTMLTextAreaElement> {}

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
    watch,
    formState: { errors }
  } = useForm<CheckoutFormValues>({
    resolver: zodResolver(checkoutSchema),
    defaultValues: {
      customerName: "",
      customerPhone: "",
      customerEmail: "",
      addressLine: "",
      ward: "",
      city: "",
      newsletter: true,
      note: "",
      wantsVat: false,
      companyName: "",
      taxCode: "",
      companyAddress: "",
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
        shippingAddress: formatShippingAddress(values),
        note: formatOrderNote(values),
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
  const wantsVat = watch("wantsVat");

  return (
    <section className="-mt-8 ml-[calc(50%-50vw)] min-h-[calc(100vh-5rem)] w-screen bg-[#f6f6f6] px-5 py-10 sm:px-8 lg:py-14">
      <div className="mx-auto grid max-w-[1440px] gap-8">
        <div className="max-w-3xl">
          <h1 className="text-4xl font-black tracking-tight text-slate-900 sm:text-5xl">Thanh toán</h1>
          <p className="mt-6 max-w-2xl text-base leading-7 text-slate-800">
            Vui lòng điền đầy đủ thông tin để chúng tôi giao đơn hàng tới đúng địa chỉ. Thông tin thanh toán và đơn hàng sẽ được xử lý bảo mật.
          </p>
        </div>

        {cartQuery.isLoading && <StateMessage>Đang tải giỏ hàng...</StateMessage>}

        {cartQuery.isError && (
          <StateMessage tone="error">
            {getApiErrorMessage(cartQuery.error)} - <Link to="/cart" className="underline">Quay lại giỏ hàng</Link>
          </StateMessage>
        )}

        {cart && cart.items.length > 0 && (
          <form onSubmit={handleSubmit((values) => checkoutMutation.mutate(values))} className="grid gap-8 lg:grid-cols-[minmax(0,1fr)_440px] lg:items-start">
            <div className="grid gap-5">
              <section>
                <h2 className="mb-4 text-lg font-black text-slate-950">Thông tin giao hàng</h2>
                <div className="rounded-lg bg-white p-5 shadow-sm sm:p-7">
                  <div className="grid gap-4 sm:grid-cols-2">
                    <FormInput placeholder="Họ tên *" autoComplete="name" {...register("customerName")} error={errors.customerName?.message} label="Họ tên" />
                    <FormInput placeholder="Số điện thoại *" type="tel" autoComplete="tel" {...register("customerPhone")} error={errors.customerPhone?.message} label="Số điện thoại" />
                    <FormInput placeholder="Email *" type="email" autoComplete="email" {...register("customerEmail")} error={errors.customerEmail?.message} label="Email" />
                    <label className="flex min-h-14 items-center gap-3 rounded-lg px-1 text-sm leading-6 text-slate-500">
                      <input type="checkbox" className="h-5 w-5 rounded-full accent-slate-700" {...register("newsletter")} />
                      <span>Đăng ký nhận ưu đãi từ HyperWork. Có thể huỷ đăng ký bất cứ lúc nào.</span>
                    </label>
                    <div className="sm:col-span-2">
                      <FormInput placeholder="Địa chỉ *" autoComplete="street-address" {...register("addressLine")} error={errors.addressLine?.message} label="Địa chỉ" />
                    </div>
                    <FormInput placeholder="Phường/Xã *" {...register("ward")} error={errors.ward?.message} label="Phường/Xã" />
                    <FormSelect {...register("city")} error={errors.city?.message} label="Tỉnh/Thành">
                      <option value="">Tỉnh/Thành *</option>
                      <option value="TP. Hồ Chí Minh">TP. Hồ Chí Minh</option>
                      <option value="Hà Nội">Hà Nội</option>
                      <option value="Đà Nẵng">Đà Nẵng</option>
                      <option value="Bình Dương">Bình Dương</option>
                      <option value="Đồng Nai">Đồng Nai</option>
                      <option value="Khác">Khác</option>
                    </FormSelect>
                  </div>
                </div>
              </section>

              <section>
                <h2 className="mb-4 text-lg font-black text-slate-950">Ghi chú đơn hàng</h2>
                <FormTextArea
                  rows={4}
                  placeholder="VD: giao giờ hành chính, gọi trước 30 phút..."
                  {...register("note")}
                  error={errors.note?.message}
                  label="Ghi chú đơn hàng"
                />
              </section>

              <section className="rounded-lg bg-white p-5 shadow-sm sm:p-6">
                <label className="flex cursor-pointer items-start justify-between gap-4">
                  <span>
                    <span className="block text-lg font-black text-slate-950">Xuất hoá đơn VAT</span>
                    <span className="mt-1 block text-sm text-slate-500">Tuỳ chọn - Nhấn để điền thông tin</span>
                  </span>
                  <span className="grid h-9 w-9 shrink-0 place-items-center rounded-full bg-slate-100 text-slate-600">
                    <input type="checkbox" className="peer sr-only" {...register("wantsVat")} />
                    <svg className="h-4 w-4 transition peer-checked:rotate-180" viewBox="0 0 20 20" fill="none" aria-hidden="true">
                      <path d="m6 8 4 4 4-4" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
                    </svg>
                  </span>
                </label>
                {wantsVat && (
                  <div className="mt-5 grid gap-4 sm:grid-cols-2">
                    <FormInput placeholder="Tên công ty" {...register("companyName")} error={errors.companyName?.message} label="Tên công ty" />
                    <FormInput placeholder="Mã số thuế" {...register("taxCode")} error={errors.taxCode?.message} label="Mã số thuế" />
                    <div className="sm:col-span-2">
                      <FormInput placeholder="Địa chỉ công ty" {...register("companyAddress")} error={errors.companyAddress?.message} label="Địa chỉ công ty" />
                    </div>
                  </div>
                )}
              </section>

              <section className="rounded-lg bg-white p-5 shadow-sm sm:p-6">
                <h2 className="mb-4 text-lg font-black text-slate-950">Phương thức thanh toán</h2>
                <div className="grid gap-3 sm:grid-cols-2">
                  <PaymentOption value="0" label="COD" description="Thanh toán khi nhận hàng" register={register("paymentMethod")} />
                  <PaymentOption value="1" label="Chuyển khoản" description="Nhân viên xác nhận sau khi đặt hàng" register={register("paymentMethod")} />
                </div>
              </section>
            </div>

            <OrderSummary
              cartItems={cart.items}
              isPending={checkoutMutation.isPending}
              mutationError={checkoutMutation.error}
              subtotal={cart.totalAmount}
              totalAmount={cart.totalAmount}
            />
          </form>
        )}
      </div>
    </section>
  );
}

function FormInput({ label, error, className = "", ...props }: InputProps) {
  return (
    <label className="block">
      <span className="sr-only">{label}</span>
      <input
        className={`h-14 w-full rounded-lg border border-slate-200 bg-white px-5 text-base font-medium text-slate-900 outline-none transition placeholder:text-slate-400 focus:border-slate-500 ${className}`}
        {...props}
      />
      <FieldError message={error} />
    </label>
  );
}

function FormSelect({ label, error, children, className = "", ...props }: SelectProps) {
  return (
    <label className="block">
      <span className="sr-only">{label}</span>
      <select
        className={`h-14 w-full rounded-lg border border-slate-200 bg-white px-5 text-base font-medium text-slate-500 outline-none transition focus:border-slate-500 ${className}`}
        {...props}
      >
        {children}
      </select>
      <FieldError message={error} />
    </label>
  );
}

function FormTextArea({ label, error, className = "", ...props }: TextAreaProps) {
  return (
    <label className="block rounded-lg bg-white shadow-sm">
      <span className="sr-only">{label}</span>
      <textarea
        className={`w-full resize-none rounded-lg border border-white bg-white px-5 py-4 text-base font-medium text-slate-900 outline-none transition placeholder:text-slate-400 focus:border-slate-300 ${className}`}
        {...props}
      />
      <FieldError message={error} />
    </label>
  );
}

function FieldError({ message }: { message?: string }) {
  if (!message) {
    return null;
  }

  return <p className="mt-1 text-sm font-semibold text-red-600">{message}</p>;
}

function PaymentOption({
  description,
  label,
  register,
  value
}: {
  description: string;
  label: string;
  register: ReturnType<typeof useForm<CheckoutFormValues>>["register"] extends (name: "paymentMethod") => infer R ? R : never;
  value: "0" | "1";
}) {
  return (
    <label className="flex cursor-pointer items-start gap-3 rounded-lg border border-slate-200 p-4 transition has-[:checked]:border-slate-950 has-[:checked]:bg-slate-50">
      <input type="radio" value={value} className="mt-1 accent-slate-950" {...register} />
      <span>
        <span className="block font-bold text-slate-950">{label}</span>
        <span className="mt-1 block text-sm text-slate-500">{description}</span>
      </span>
    </label>
  );
}

function OrderSummary({
  cartItems,
  isPending,
  mutationError,
  subtotal,
  totalAmount
}: {
  cartItems: CartItemDto[];
  isPending: boolean;
  mutationError: unknown;
  subtotal: number;
  totalAmount: number;
}) {
  return (
    <aside className="lg:sticky lg:top-28">
      <h2 className="mb-4 text-lg font-black text-slate-950">Tóm tắt đơn hàng</h2>
      <div className="rounded-lg bg-white p-5 shadow-sm sm:p-7">
        <div className="grid gap-5">
          {cartItems.map((item) => (
            <CheckoutCartItem key={item.id} item={item} />
          ))}
        </div>

        <div className="mt-5 border-t border-slate-200 pt-5">
          <div className="flex h-12 items-center rounded-full border border-slate-200 bg-white pl-5 pr-1.5 text-slate-400">
            <span className="min-w-0 flex-1 truncate text-sm font-medium">Nhập mã giảm giá khác</span>
            <button type="button" className="grid h-9 w-9 place-items-center rounded-full bg-slate-200 text-white" aria-label="Áp dụng mã giảm giá" disabled>
              <svg className="h-5 w-5" viewBox="0 0 24 24" fill="none" aria-hidden="true">
                <path d="M5 12h14m-6-6 6 6-6 6" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" />
              </svg>
            </button>
          </div>
        </div>

        <div className="mt-5 grid gap-4 text-sm text-slate-500">
          <SummaryLine label="Tạm tính" value={formatCartMoney(subtotal, false)} strong />
          <SummaryLine label="Phí vận chuyển" value="Nhập địa chỉ để tính" />
          <div className="border-t border-slate-200 pt-5">
            <SummaryLine label="Tổng cộng (VND)" value={formatCartMoney(totalAmount, false)} strong />
          </div>
        </div>

        {mutationError ? <div className="mt-5 rounded-lg bg-red-50 px-4 py-3 text-sm font-semibold text-red-700">{getApiErrorMessage(mutationError)}</div> : null}

        <button
          type="submit"
          disabled={isPending}
          className="mt-8 h-14 w-full rounded-full bg-[#9d9d9d] px-6 text-base font-black text-white transition hover:bg-slate-950 disabled:cursor-not-allowed disabled:opacity-60"
        >
          {isPending ? "Đang xử lý..." : "Chọn phương thức thanh toán"}
        </button>
      </div>
    </aside>
  );
}

function CheckoutCartItem({ item }: { item: CartItemDto }) {
  return (
    <article className="grid grid-cols-[104px_minmax(0,1fr)_56px] gap-4">
      <div className="grid aspect-square place-items-center overflow-hidden rounded-md bg-[#f1f1f1]">
        {item.imageUrl ? (
          <img src={item.imageUrl} alt={item.productName} className="h-full w-full object-contain p-3" />
        ) : (
          <svg className="h-10 w-10 text-slate-400" viewBox="0 0 24 24" fill="none" aria-hidden="true">
            <path d="M4 8h16v11H4z" stroke="currentColor" strokeWidth="1.7" />
            <path d="M8 8a4 4 0 0 1 8 0" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" />
          </svg>
        )}
      </div>
      <div className="min-w-0 py-1">
        <p className="truncate text-sm text-slate-500">{item.productName}</p>
        <h3 className="mt-1 line-clamp-2 text-sm font-black leading-5 text-slate-800">{item.variantName}</h3>
        <p className="mt-2 text-sm font-semibold text-slate-600">{formatCartMoney(item.unitPriceSnapshot, false)}</p>
        {formatVariantOptions(item) ? <p className="mt-2 truncate text-sm text-slate-500">{formatVariantOptions(item)}</p> : null}
      </div>
      <div className="grid justify-items-end gap-2 py-5">
        <span className="grid h-9 w-10 place-items-center rounded-lg border border-slate-200 text-sm text-slate-500">{item.quantity}</span>
      </div>
    </article>
  );
}

function SummaryLine({ label, strong = false, value }: { label: string; strong?: boolean; value: string }) {
  return (
    <div className="flex items-center justify-between gap-4">
      <span className="text-slate-500">{label}</span>
      <span className={strong ? "font-black text-slate-800" : "font-medium text-slate-500"}>{value}</span>
    </div>
  );
}

function StateMessage({ children, tone = "info" }: { children: ReactNode; tone?: "info" | "error" }) {
  return (
    <div className={`rounded-lg px-5 py-4 text-sm font-semibold ${tone === "error" ? "bg-red-50 text-red-700" : "bg-white text-slate-500"}`}>
      {children}
    </div>
  );
}

function formatShippingAddress(values: CheckoutFormValues) {
  return [values.addressLine, values.ward, values.city].filter(Boolean).join(", ");
}

function formatOrderNote(values: CheckoutFormValues) {
  const notes = [values.note?.trim()].filter(Boolean);

  if (values.wantsVat) {
    notes.push(
      [
        "Yêu cầu xuất hoá đơn VAT",
        values.companyName ? `Công ty: ${values.companyName}` : null,
        values.taxCode ? `MST: ${values.taxCode}` : null,
        values.companyAddress ? `Địa chỉ công ty: ${values.companyAddress}` : null
      ].filter(Boolean).join(" | ")
    );
  }

  return notes.length > 0 ? notes.join("\n") : null;
}

function formatVariantOptions(item: CartItemDto) {
  return [item.variantColor, item.variantSize].filter(Boolean).join(" / ");
}

function formatCartMoney(value: number, includeCurrency = true) {
  const formatted = new Intl.NumberFormat("vi-VN", {
    maximumFractionDigits: 0
  }).format(value);

  return includeCurrency ? `${formatted} VND` : formatted;
}
