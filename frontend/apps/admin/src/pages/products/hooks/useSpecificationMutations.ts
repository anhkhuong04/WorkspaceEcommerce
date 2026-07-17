import { useMutation, useQueryClient } from "@tanstack/react-query";
import type { AdminProductDto, AdminProductSpecificationDto } from "@workspace-ecommerce/api-types";
import type { Dispatch, SetStateAction } from "react";
import type { UseFormReturn } from "react-hook-form";
import { adminApi } from "../../../services/api/adminApi";
import { getApiErrorMessage } from "../../../services/api/errors";
import type { NoticeState, SpecificationFormValues } from "../productTypes";
import { specificationDefaultValues, toSpecificationRequest } from "../productTypes";

interface UseSpecificationMutationsArgs {
  specificationProduct: AdminProductDto | null;
  editingSpecification: AdminProductSpecificationDto | null;
  specificationForm: UseFormReturn<SpecificationFormValues>;
  setSpecificationProduct: Dispatch<SetStateAction<AdminProductDto | null>>;
  setEditingSpecification: Dispatch<SetStateAction<AdminProductSpecificationDto | null>>;
  setIsSpecificationModalOpen: Dispatch<SetStateAction<boolean>>;
  setNotice: Dispatch<SetStateAction<NoticeState>>;
}

export function useSpecificationMutations({
  specificationProduct,
  editingSpecification,
  specificationForm,
  setSpecificationProduct,
  setEditingSpecification,
  setIsSpecificationModalOpen,
  setNotice
}: UseSpecificationMutationsArgs) {
  const queryClient = useQueryClient();

  async function refreshProducts() {
    await queryClient.invalidateQueries({ queryKey: ["admin-products"] });
  }

  const specificationSaveMutation = useMutation({
    mutationFn: (values: SpecificationFormValues) => {
      const request = toSpecificationRequest(values);
      if (editingSpecification) return adminApi.updateProductSpecification(editingSpecification.id, request);
      if (!specificationProduct) throw new Error("Product is required for a new specification.");
      return adminApi.createProductSpecification(specificationProduct.id, request);
    },
    onSuccess: async () => {
      await refreshProducts();
      setIsSpecificationModalOpen(false);
      setSpecificationProduct(null);
      setEditingSpecification(null);
      specificationForm.reset(specificationDefaultValues);
      setNotice({ type: "success", message: "Product specification saved." });
    },
    onError: (error) => setNotice({ type: "error", message: getApiErrorMessage(error) })
  });

  const specificationDeleteMutation = useMutation({
    mutationFn: (specification: AdminProductSpecificationDto) => adminApi.deleteProductSpecification(specification.id),
    onSuccess: async () => {
      await refreshProducts();
      setNotice({ type: "success", message: "Product specification deleted." });
    },
    onError: (error) => setNotice({ type: "error", message: getApiErrorMessage(error) })
  });

  return { specificationSaveMutation, specificationDeleteMutation };
}
