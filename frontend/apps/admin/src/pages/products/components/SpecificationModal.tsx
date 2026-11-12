import { useId } from "react";
import type {
  AdminProductDto,
  AdminProductSpecificationDto,
} from "@workspace-ecommerce/api-types";
import { Controller } from "react-hook-form";
import type { UseFormReturn } from "react-hook-form";
import {
  Button,
  Field,
  Modal,
  TextInput,
} from "../../../components/ui/AdminUi";
import { formatLocalizedText } from "../../../utils/localizedText";
import type { SpecificationFormValues } from "../productTypes";

interface SpecificationModalProps {
  open: boolean;
  specificationProduct: AdminProductDto | null;
  editingSpecification: AdminProductSpecificationDto | null;
  form: UseFormReturn<SpecificationFormValues>;
  isPending: boolean;
  error?: string;
  onClose: () => void;
  onSubmit: (values: SpecificationFormValues) => void;
}

export function SpecificationModal({
  open,
  specificationProduct,
  editingSpecification,
  form,
  isPending,
  error,
  onClose,
  onSubmit,
}: SpecificationModalProps) {
  const formId = useId();
  return (
    <Modal
      title={
        editingSpecification
          ? "Edit specification"
          : `New specification${specificationProduct ? ` for ${formatLocalizedText(specificationProduct.name)}` : ""}`
      }
      open={open}
      busy={isPending}
      error={error}
      onClose={onClose}
      widthClass="max-w-2xl"
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
        onSubmit={form.handleSubmit(onSubmit)}
      >
        <Controller
          control={form.control}
          name="name"
          render={({ field, fieldState }) => (
            <Field label="Name" error={fieldState.error?.message}>
              <TextInput {...field} placeholder="Material" />
            </Field>
          )}
        />
        <Controller
          control={form.control}
          name="value"
          render={({ field, fieldState }) => (
            <Field label="Value" error={fieldState.error?.message}>
              <TextInput {...field} placeholder="Solid wood" />
            </Field>
          )}
        />
        <Controller
          control={form.control}
          name="sortOrder"
          render={({ field, fieldState }) => (
            <Field label="Sort order" error={fieldState.error?.message}>
              <TextInput
                type="number"
                value={field.value}
                onChange={(event) => field.onChange(Number(event.target.value))}
              />
            </Field>
          )}
        />
      </form>
    </Modal>
  );
}
