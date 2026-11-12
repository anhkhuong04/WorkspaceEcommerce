import { useId } from "react";
import type {
  AdminProductDto,
  AdminProductVariantDto,
} from "@workspace-ecommerce/api-types";
import { Controller } from "react-hook-form";
import type { UseFormReturn } from "react-hook-form";
import {
  Button,
  Field,
  Modal,
  TextInput,
  Toggle,
} from "../../../components/ui/AdminUi";
import { formatLocalizedText } from "../../../utils/localizedText";
import { StockHistory } from "./StockHistory";
import type { VariantFormValues } from "../productTypes";

interface VariantModalProps {
  open: boolean;
  variantProduct: AdminProductDto | null;
  editingVariant: AdminProductVariantDto | null;
  form: UseFormReturn<VariantFormValues>;
  isPending: boolean;
  error?: string;
  onClose: () => void;
  onSubmit: (values: VariantFormValues) => void;
}

export function VariantModal({
  open,
  variantProduct,
  editingVariant,
  form,
  isPending,
  error,
  onClose,
  onSubmit,
}: VariantModalProps) {
  const formId = useId();
  return (
    <Modal
      title={
        editingVariant
          ? "Edit variant"
          : `New SKU${variantProduct ? ` for ${formatLocalizedText(variantProduct.name)}` : ""}`
      }
      open={open}
      busy={isPending}
      error={error}
      onClose={onClose}
      widthClass="max-w-3xl"
      footer={
        <>
          <Button type="button" disabled={isPending} onClick={onClose}>
            Cancel
          </Button>
          <Button
            type="submit"
            form={formId}
            variant="primary"
            disabled={isPending}
          >
            {isPending ? "Saving..." : "Save"}
          </Button>
        </>
      }
    >
      <form
        id={formId}
        className="grid gap-4"
        noValidate
        onSubmit={form.handleSubmit(values => {
          if (editingVariant && editingVariant.stockQuantity !== values.stockQuantity && !values.stockChangeReason.trim()) {
            form.setError("stockChangeReason", { message: "Explain this stock adjustment." }, { shouldFocus: true }); return;
          }
          onSubmit(values);
        })}
      >
        <Controller
          control={form.control}
          name="sku"
          render={({ field, fieldState }) => (
            <Field label="SKU" error={fieldState.error?.message}>
              <TextInput {...field} placeholder="DESK-PRO-BLK" />
            </Field>
          )}
        />
        <Controller
          control={form.control}
          name="name"
          render={({ field, fieldState }) => (
            <Field label="Variant name" error={fieldState.error?.message}>
              <TextInput {...field} placeholder="Black frame / 140cm" />
            </Field>
          )}
        />
        {editingVariant && <Controller control={form.control} name="stockChangeReason" render={({ field, fieldState }) => <Field label="Stock adjustment reason (required when stock changes)" error={fieldState.error?.message}><TextInput {...field} placeholder="Count correction, inspected replenishment..." /></Field>} />}
        <div className="grid gap-4 md:grid-cols-3">
          <Controller
            control={form.control}
            name="color"
            render={({ field, fieldState }) => (
              <Field label="Color" error={fieldState.error?.message}>
                <TextInput {...field} placeholder="Black" />
              </Field>
            )}
          />
          <Controller
            control={form.control}
            name="size"
            render={({ field, fieldState }) => (
              <Field label="Size" error={fieldState.error?.message}>
                <TextInput {...field} placeholder="140cm" />
              </Field>
            )}
          />
          <Controller
            control={form.control}
            name="stockQuantity"
            render={({ field, fieldState }) => (
              <Field label="Stock" error={fieldState.error?.message}>
                <TextInput
                  type="number"
                  min={0}
                  value={field.value}
                  onChange={(event) =>
                    field.onChange(Number(event.target.value))
                  }
                />
              </Field>
            )}
          />
        </div>
        <div className="grid gap-4 md:grid-cols-2">
          <Controller
            control={form.control}
            name="price"
            render={({ field, fieldState }) => (
              <Field label="Price (VND)" error={fieldState.error?.message}>
                <TextInput
                  type="number"
                  min={0}
                  step={1000}
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
            name="compareAtPrice"
            render={({ field, fieldState }) => (
              <Field label="Compare at (VND)" error={fieldState.error?.message}>
                <TextInput
                  type="number"
                  min={0}
                  step={1000}
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
        </div>
        <div className="grid gap-4 sm:grid-cols-2">
          {(["weightKg", "lengthCm", "widthCm", "heightCm"] as const).map(name => (
            <Controller key={name} control={form.control} name={name} render={({ field, fieldState }) => (
              <Field label={{ weightKg: "Actual weight (kg)", lengthCm: "Length (cm)", widthCm: "Width (cm)", heightCm: "Height (cm)" }[name]} error={fieldState.error?.message}>
                <TextInput type="number" min="0.001" step="0.001" value={field.value ?? ""} onChange={event => field.onChange(event.target.value === "" ? null : Number(event.target.value))} />
              </Field>
            )} />
          ))}
        </div>
        <p className="text-xs text-[var(--muted)]">Actual weight is required for WorkspaceEC shipping quotes. Leave unknown measurements blank and verify before offering delivery.</p>
        <div className="grid gap-4 sm:grid-cols-2">
          <Controller
            control={form.control}
            name="requiresInstallation"
            render={({ field }) => (
              <Field label="Requires installation">
                <Toggle checked={field.value} onChange={field.onChange} />
              </Field>
            )}
          />
          <Controller
            control={form.control}
            name="isActive"
            render={({ field }) => (
              <Field label="Active">
                <Toggle checked={field.value} onChange={field.onChange} />
              </Field>
            )}
          />
        </div>
      </form>
      {open && editingVariant && <StockHistory key={editingVariant.id} variantId={editingVariant.id} />}
    </Modal>
  );
}
