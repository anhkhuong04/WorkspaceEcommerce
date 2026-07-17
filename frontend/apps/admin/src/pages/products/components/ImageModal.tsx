import type { AdminProductDto, AdminProductImageDto } from "@workspace-ecommerce/api-types";
import { Controller } from "react-hook-form";
import type { UseFormReturn } from "react-hook-form";
import { Button, Field, Modal, TextInput } from "../../../components/ui/AdminUi";
import type { ImageFormValues } from "../productTypes";

interface ImageModalProps {
  open: boolean;
  imageProduct: AdminProductDto | null;
  editingImage: AdminProductImageDto | null;
  form: UseFormReturn<ImageFormValues>;
  isPending: boolean;
  onClose: () => void;
  onSubmit: (values: ImageFormValues) => void;
}

export function ImageModal({ open, imageProduct, editingImage, form, isPending, onClose, onSubmit }: ImageModalProps) {
  return (
    <Modal
      title={editingImage ? "Edit product image" : `New image${imageProduct ? ` for ${imageProduct.name}` : ""}`}
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
        <Controller control={form.control} name="imageUrl" render={({ field, fieldState }) => <Field label="Image URL" error={fieldState.error?.message}><TextInput {...field} placeholder="https://example.test/product.jpg" /></Field>} />
        <Controller control={form.control} name="altText" render={({ field, fieldState }) => <Field label="Alt text" error={fieldState.error?.message}><TextInput {...field} placeholder="Standing desk front view" /></Field>} />
        <Controller control={form.control} name="sortOrder" render={({ field, fieldState }) => <Field label="Sort order" error={fieldState.error?.message}><TextInput type="number" value={field.value} onChange={(event) => field.onChange(Number(event.target.value))} /></Field>} />
      </form>
    </Modal>
  );
}
