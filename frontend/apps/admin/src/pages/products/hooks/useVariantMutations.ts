import { useMutation, useQueryClient } from "@tanstack/react-query";
import type { AdminProductDto, AdminProductVariantDto } from "@workspace-ecommerce/api-types";
import type { Dispatch, SetStateAction } from "react";
import type { UseFormReturn } from "react-hook-form";
import { adminApi } from "../../../services/api/adminApi";
import { getApiErrorMessage } from "../../../services/api/errors";
import type { NoticeState, VariantFormValues } from "../productTypes";
import { toVariantFormValues, toVariantRequest, variantDefaultValues } from "../productTypes";

interface UseVariantMutationsArgs {
  variantProduct: AdminProductDto | null;
  editingVariant: AdminProductVariantDto | null;
  variantForm: UseFormReturn<VariantFormValues>;
  setVariantProduct: Dispatch<SetStateAction<AdminProductDto | null>>;
  setEditingVariant: Dispatch<SetStateAction<AdminProductVariantDto | null>>;
  setIsVariantModalOpen: Dispatch<SetStateAction<boolean>>;
  setNotice: Dispatch<SetStateAction<NoticeState>>;
}

export function useVariantMutations({
  variantProduct,
  editingVariant,
  variantForm,
  setVariantProduct,
  setEditingVariant,
  setIsVariantModalOpen,
  setNotice
}: UseVariantMutationsArgs) {
  const queryClient = useQueryClient();

  async function refreshProducts() {
    await queryClient.invalidateQueries({ queryKey: ["admin-products"] });
  }

  const variantSaveMutation = useMutation({
    mutationFn: (values: VariantFormValues) => {
      const request = toVariantRequest(values);
      if (editingVariant) return adminApi.updateProductVariant(editingVariant.id, request);
      if (!variantProduct) throw new Error("Product is required for a new variant.");
      return adminApi.createProductVariant(variantProduct.id, request);
    },
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ["admin-products"] }),
        queryClient.invalidateQueries({ queryKey: ["admin-dashboard"] })
      ]);
      setIsVariantModalOpen(false);
      setVariantProduct(null);
      setEditingVariant(null);
      variantForm.reset(variantDefaultValues);
      setNotice({ type: "success", message: "Variant saved." });
    },
    onError: (error) => setNotice({ type: "error", message: getApiErrorMessage(error) })
  });

  const variantToggleMutation = useMutation({
    mutationFn: (variant: AdminProductVariantDto) => adminApi.updateProductVariant(variant.id, { ...toVariantRequest(toVariantFormValues(variant)), isActive: !variant.isActive }),
    onSuccess: async () => {
      await refreshProducts();
      setNotice({ type: "success", message: "Variant status updated." });
    },
    onError: (error) => setNotice({ type: "error", message: getApiErrorMessage(error) })
  });

  return { variantSaveMutation, variantToggleMutation };
}
