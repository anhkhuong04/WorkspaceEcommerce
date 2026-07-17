import type { AdminProductDto } from "@workspace-ecommerce/api-types";
import { Controller } from "react-hook-form";
import type { UseFormReturn } from "react-hook-form";
import { Button, Field, Modal, SelectInput, TextArea, TextInput, Toggle } from "../../../components/ui/AdminUi";
import type { CategoryOption, ProductFormValues } from "../productTypes";

interface ProductModalProps {
  open: boolean;
  editingProduct: AdminProductDto | null;
  categoryOptions: CategoryOption[];
  form: UseFormReturn<ProductFormValues>;
  isPending: boolean;
  onClose: () => void;
  onSubmit: (values: ProductFormValues) => void;
}

export function ProductModal({ open, editingProduct, categoryOptions, form, isPending, onClose, onSubmit }: ProductModalProps) {
  return (
    <Modal
      title={editingProduct ? "Edit product" : "New product"}
      open={open}
      onClose={onClose}
      widthClass="max-w-2xl"
      footer={(
        <>
          <Button type="button" onClick={onClose}>Cancel</Button>
          <Button type="button" variant="primary" disabled={isPending} onClick={form.handleSubmit(onSubmit)}>{isPending ? "Saving..." : "Save"}</Button>
        </>
      )}
    >
      <form className="grid gap-4" noValidate>
        <Controller control={form.control} name="categoryId" render={({ field, fieldState }) => <Field label="Category" error={fieldState.error?.message}><SelectInput value={field.value} onChange={field.onChange}><option value="">Select category</option>{categoryOptions.map((option) => <option key={option.id} value={option.id}>{`${"  ".repeat(option.level)}${option.label}`}</option>)}</SelectInput></Field>} />
        <Controller control={form.control} name="name" render={({ field, fieldState }) => <Field label="Name" error={fieldState.error?.message}><TextInput {...field} placeholder="Height adjustable desk" /></Field>} />
        <Controller control={form.control} name="slug" render={({ field, fieldState }) => <Field label="Slug" error={fieldState.error?.message}><TextInput {...field} placeholder="height-adjustable-desk" /></Field>} />
        <Controller control={form.control} name="description" render={({ field, fieldState }) => <Field label="Description" error={fieldState.error?.message}><TextArea {...field} rows={4} /></Field>} />
        <div className="grid gap-4 sm:grid-cols-2">
          <Controller control={form.control} name="isFeatured" render={({ field }) => <Field label="Featured"><Toggle checked={field.value} onChange={field.onChange} /></Field>} />
          <Controller control={form.control} name="isActive" render={({ field }) => <Field label="Active"><Toggle checked={field.value} onChange={field.onChange} /></Field>} />
        </div>
      </form>
    </Modal>
  );
}
