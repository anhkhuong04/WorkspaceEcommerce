import type { AdminProductDto, AdminProductVariantDto } from "@workspace-ecommerce/api-types";
import { Controller } from "react-hook-form";
import type { UseFormReturn } from "react-hook-form";
import { Button, Field, Modal, TextInput, Toggle } from "../../../components/ui/AdminUi";
import type { VariantFormValues } from "../productTypes";

interface VariantModalProps {
  open: boolean;
  variantProduct: AdminProductDto | null;
  editingVariant: AdminProductVariantDto | null;
  form: UseFormReturn<VariantFormValues>;
  isPending: boolean;
  onClose: () => void;
  onSubmit: (values: VariantFormValues) => void;
}

export function VariantModal({ open, variantProduct, editingVariant, form, isPending, onClose, onSubmit }: VariantModalProps) {
  return (
    <Modal
      title={editingVariant ? "Edit variant" : `New SKU${variantProduct ? ` for ${variantProduct.name}` : ""}`}
      open={open}
      onClose={onClose}
      widthClass="max-w-3xl"
      footer={(
        <>
          <Button type="button" onClick={onClose}>Cancel</Button>
          <Button type="button" variant="primary" disabled={isPending} onClick={form.handleSubmit(onSubmit)}>{isPending ? "Saving..." : "Save"}</Button>
        </>
      )}
    >
      <form className="grid gap-4" noValidate>
        <Controller control={form.control} name="sku" render={({ field, fieldState }) => <Field label="SKU" error={fieldState.error?.message}><TextInput {...field} placeholder="DESK-PRO-BLK" /></Field>} />
        <Controller control={form.control} name="name" render={({ field, fieldState }) => <Field label="Variant name" error={fieldState.error?.message}><TextInput {...field} placeholder="Black frame / 140cm" /></Field>} />
        <div className="grid gap-4 md:grid-cols-3">
          <Controller control={form.control} name="color" render={({ field, fieldState }) => <Field label="Color" error={fieldState.error?.message}><TextInput {...field} placeholder="Black" /></Field>} />
          <Controller control={form.control} name="size" render={({ field, fieldState }) => <Field label="Size" error={fieldState.error?.message}><TextInput {...field} placeholder="140cm" /></Field>} />
          <Controller control={form.control} name="stockQuantity" render={({ field, fieldState }) => <Field label="Stock" error={fieldState.error?.message}><TextInput type="number" min={0} value={field.value} onChange={(event) => field.onChange(Number(event.target.value))} /></Field>} />
        </div>
        <div className="grid gap-4 md:grid-cols-2">
          <Controller control={form.control} name="price" render={({ field, fieldState }) => <Field label="Price" error={fieldState.error?.message}><TextInput type="number" min={0} value={field.value} onChange={(event) => field.onChange(Number(event.target.value))} /></Field>} />
          <Controller control={form.control} name="compareAtPrice" render={({ field, fieldState }) => <Field label="Compare at" error={fieldState.error?.message}><TextInput type="number" min={0} value={field.value ?? ""} onChange={(event) => field.onChange(event.target.value === "" ? null : Number(event.target.value))} /></Field>} />
        </div>
        <div className="grid gap-4 sm:grid-cols-2">
          <Controller control={form.control} name="requiresInstallation" render={({ field }) => <Field label="Requires installation"><Toggle checked={field.value} onChange={field.onChange} /></Field>} />
          <Controller control={form.control} name="isActive" render={({ field }) => <Field label="Active"><Toggle checked={field.value} onChange={field.onChange} /></Field>} />
        </div>
      </form>
    </Modal>
  );
}
