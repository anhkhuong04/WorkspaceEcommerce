import { useMutation, useQueryClient } from "@tanstack/react-query";
import type { AdminProductDto } from "@workspace-ecommerce/api-types";
import type { Dispatch, SetStateAction } from "react";
import type { UseFormReturn } from "react-hook-form";
import { adminApi } from "../../../services/api/adminApi";
import { getApiErrorMessage } from "../../../services/api/errors";
import type { NoticeState, ProductFormValues } from "../productTypes";
import { productDefaultValues, toProductFormValues, toProductRequest } from "../productTypes";

interface UseProductMutationsArgs {
  editingProduct: AdminProductDto | null;
  productForm: UseFormReturn<ProductFormValues>;
  setEditingProduct: Dispatch<SetStateAction<AdminProductDto | null>>;
  setIsProductModalOpen: Dispatch<SetStateAction<boolean>>;
  setExpandedProductIds: Dispatch<SetStateAction<Set<string>>>;
  setNotice: Dispatch<SetStateAction<NoticeState>>;
}

export function useProductMutations({
  editingProduct,
  productForm,
  setEditingProduct,
  setIsProductModalOpen,
  setExpandedProductIds,
  setNotice
}: UseProductMutationsArgs) {
  const queryClient = useQueryClient();

  async function refreshProducts() {
    await queryClient.invalidateQueries({ queryKey: ["admin-products"] });
  }

  const productSaveMutation = useMutation({
    mutationFn: (values: ProductFormValues) => {
      const request = toProductRequest(values);
      return editingProduct ? adminApi.updateProduct(editingProduct.id, request) : adminApi.createProduct(request);
    },
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ["admin-products"] }),
        queryClient.invalidateQueries({ queryKey: ["admin-dashboard"] })
      ]);
      setIsProductModalOpen(false);
      setEditingProduct(null);
      productForm.reset(productDefaultValues);
      setNotice({ type: "success", message: "Product saved." });
    },
    onError: (error) => setNotice({ type: "error", message: getApiErrorMessage(error) })
  });

  const productToggleMutation = useMutation({
    mutationFn: (product: AdminProductDto) => adminApi.updateProduct(product.id, { ...toProductRequest(toProductFormValues(product)), isActive: !product.isActive }),
    onSuccess: async () => {
      await refreshProducts();
      setNotice({ type: "success", message: "Product status updated." });
    },
    onError: (error) => setNotice({ type: "error", message: getApiErrorMessage(error) })
  });

  const productDeleteMutation = useMutation({
    mutationFn: (product: AdminProductDto) => adminApi.deleteProduct(product.id),
    onSuccess: async (_, product) => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ["admin-products"] }),
        queryClient.invalidateQueries({ queryKey: ["admin-dashboard"] })
      ]);
      setExpandedProductIds((current) => {
        const next = new Set(current);
        next.delete(product.id);
        return next;
      });
      setNotice({ type: "success", message: "Product deleted." });
    },
    onError: (error) => setNotice({ type: "error", message: getApiErrorMessage(error) })
  });

  return {
    productSaveMutation,
    productDeleteMutation,
    productToggleMutation,
    refreshProducts
  };
}
