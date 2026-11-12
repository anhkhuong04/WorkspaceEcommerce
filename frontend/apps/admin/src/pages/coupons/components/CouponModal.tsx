import { ProductChecklist } from "../../../components/ui/ProductChecklist";
import { useId } from "react";
import type {
  AdminCouponDto,
  CouponDiscountType,
} from "@workspace-ecommerce/api-types";
import type { UseFormReturn } from "react-hook-form";
import { Controller } from "react-hook-form";
import {
  Button,
  Field,
  Modal,
  Notice,
  Pill,
  SelectInput,
  TextArea,
  TextInput,
  Toggle,
} from "../../../components/ui/AdminUi";
import {
  couponDefaultValues,
  discountTypes,
  formatDiscountType,
  type CouponFormValues,
  type CouponTargetScope,
} from "../couponTypes";

type CouponModalProps = {
  editingCoupon: AdminCouponDto | null;
  open: boolean;
  form: UseFormReturn<CouponFormValues>;
  watchedDiscountType: CouponDiscountType;
  watchedTargetScope: CouponTargetScope;
  targetSearch: string;
  savePending: boolean;
  error?: string;
  onTargetSearchChange: (value: string) => void;
  onClose: () => void;
  onSubmit: (values: CouponFormValues) => void;
};

export function CouponModal({
  editingCoupon,
  open,
  form,
  watchedDiscountType,
  watchedTargetScope,
  targetSearch,
  savePending,
  error,
  onTargetSearchChange,
  onClose,
  onSubmit,
}: CouponModalProps) {
  const formId = useId();
  return (
    <Modal
      title={editingCoupon ? `Edit ${editingCoupon.code}` : "New coupon"}
      open={open}
      busy={savePending}
      error={error}
      onClose={onClose}
      widthClass="max-w-5xl"
      footer={
        <>
          <Button type="button" disabled={savePending} onClick={onClose}>
            Cancel
          </Button>
          <Button
            type="submit"
            form={formId}
            variant="primary"
            disabled={savePending}
          >
            {savePending ? "Saving..." : "Save"}
          </Button>
        </>
      }
    >
      <form
        id={formId}
        className="grid gap-5"
        noValidate
        onSubmit={form.handleSubmit(onSubmit)}
      >
        {editingCoupon && editingCoupon.redemptionCount > 0 ? (
          <Notice type="warning" title="This coupon has redemption history">
            Rule changes affect future checkouts only. Existing orders keep
            their coupon code, name, and discount snapshot.
          </Notice>
        ) : null}

        <div className="grid gap-4 md:grid-cols-2">
          <Controller
            control={form.control}
            name="code"
            render={({ field, fieldState }) => (
              <Field label="Code" error={fieldState.error?.message}>
                <TextInput {...field} placeholder="SUMMER10" />
              </Field>
            )}
          />
          <Controller
            control={form.control}
            name="name"
            render={({ field, fieldState }) => (
              <Field label="Name" error={fieldState.error?.message}>
                <TextInput {...field} placeholder="Summer campaign" />
              </Field>
            )}
          />
        </div>

        <Controller
          control={form.control}
          name="description"
          render={({ field, fieldState }) => (
            <Field label="Description" error={fieldState.error?.message}>
              <TextArea
                {...field}
                rows={3}
                placeholder="Internal campaign note"
              />
            </Field>
          )}
        />

        <div className="grid gap-4 lg:grid-cols-3">
          <Controller
            control={form.control}
            name="discountType"
            render={({ field, fieldState }) => (
              <Field label="Discount type" error={fieldState.error?.message}>
                <SelectInput
                  value={field.value}
                  onChange={(event) =>
                    field.onChange(
                      Number(event.target.value) as CouponDiscountType,
                    )
                  }
                >
                  {discountTypes.map((type) => (
                    <option key={type} value={type}>
                      {formatDiscountType(type)}
                    </option>
                  ))}
                </SelectInput>
              </Field>
            )}
          />
          <Controller
            control={form.control}
            name="discountValue"
            render={({ field, fieldState }) => (
              <Field
                label={
                  watchedDiscountType === 0
                    ? "Discount percent"
                    : "Discount amount"
                }
                error={fieldState.error?.message}
              >
                <TextInput
                  type="number"
                  min={0}
                  step="0.01"
                  value={field.value}
                  onChange={(event) =>
                    field.onChange(Number(event.target.value))
                  }
                />
              </Field>
            )}
          />
          <Controller
            control={form.control}
            name="maxDiscountAmount"
            render={({ field, fieldState }) => (
              <Field label="Max discount" error={fieldState.error?.message}>
                <TextInput
                  type="number"
                  min={0}
                  step="0.01"
                  value={field.value ?? ""}
                  disabled={watchedDiscountType !== 0}
                  onChange={(event) =>
                    field.onChange(
                      event.target.value === ""
                        ? null
                        : Number(event.target.value),
                    )
                  }
                />
              </Field>
            )}
          />
        </div>

        <div className="grid gap-4 lg:grid-cols-4">
          <Controller
            control={form.control}
            name="minimumSubtotal"
            render={({ field, fieldState }) => (
              <Field label="Minimum subtotal" error={fieldState.error?.message}>
                <TextInput
                  type="number"
                  min={0}
                  step="0.01"
                  value={field.value ?? ""}
                  onChange={(event) =>
                    field.onChange(
                      event.target.value === ""
                        ? null
                        : Number(event.target.value),
                    )
                  }
                />
              </Field>
            )}
          />
          <Controller
            control={form.control}
            name="usageLimit"
            render={({ field, fieldState }) => (
              <Field label="Usage limit" error={fieldState.error?.message}>
                <TextInput
                  type="number"
                  min={1}
                  step={1}
                  value={field.value ?? ""}
                  onChange={(event) =>
                    field.onChange(
                      event.target.value === ""
                        ? null
                        : Number(event.target.value),
                    )
                  }
                />
              </Field>
            )}
          />
          <Controller
            control={form.control}
            name="startsAt"
            render={({ field, fieldState }) => (
              <Field label="Starts at" error={fieldState.error?.message}>
                <TextInput
                  type="datetime-local"
                  value={field.value}
                  onChange={field.onChange}
                />
              </Field>
            )}
          />
          <Controller
            control={form.control}
            name="endsAt"
            render={({ field, fieldState }) => (
              <Field label="Ends at" error={fieldState.error?.message}>
                <TextInput
                  type="datetime-local"
                  value={field.value}
                  onChange={field.onChange}
                />
              </Field>
            )}
          />
        </div>

        <div className="grid gap-4 md:grid-cols-[220px_1fr]">
          <Controller
            control={form.control}
            name="isActive"
            render={({ field }) => (
              <Field label="Active">
                <div className="flex items-center gap-3">
                  <Toggle checked={field.value} onChange={field.onChange} />
                  <Pill tone={field.value ? "green" : "slate"}>
                    {field.value ? "Active" : "Inactive"}
                  </Pill>
                </div>
              </Field>
            )}
          />
          <Controller
            control={form.control}
            name="targetScope"
            render={({ field, fieldState }) => (
              <Field label="Target products" error={fieldState.error?.message}>
                <SelectInput
                  value={field.value}
                  onChange={(event) => {
                    const nextScope = event.target.value as CouponTargetScope;
                    field.onChange(nextScope);
                    if (nextScope === "all") {
                      form.setValue(
                        "productTargetIds",
                        couponDefaultValues.productTargetIds,
                        { shouldDirty: true, shouldValidate: true },
                      );
                    }
                  }}
                >
                  <option value="all">All products</option>
                  <option value="products">Specific products</option>
                </SelectInput>
              </Field>
            )}
          />
        </div>

        {watchedTargetScope === "products" ? (
          <Controller
            control={form.control}
            name="productTargetIds"
            render={({ field, fieldState }) => (
              <ProductChecklist
                label="Coupon products"
                selectedIds={field.value}
                onChange={field.onChange}
                error={fieldState.error?.message}
                search={targetSearch}
                onSearchChange={onTargetSearchChange}
              />
            )}
          />
        ) : null}
      </form>
    </Modal>
  );
}
