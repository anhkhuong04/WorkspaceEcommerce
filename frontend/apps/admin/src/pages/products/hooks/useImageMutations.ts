import { useMutation, useQueryClient } from "@tanstack/react-query";
import type { AdminProductDto, AdminProductImageDto } from "@workspace-ecommerce/api-types";
import type { Dispatch, SetStateAction } from "react";
import type { UseFormReturn } from "react-hook-form";
import { adminApi } from "../../../services/api/adminApi";
import { getApiErrorMessage } from "../../../services/api/errors";
import type { ImageFormValues, NoticeState } from "../productTypes";
import { imageDefaultValues, toImageRequest } from "../productTypes";

interface UseImageMutationsArgs {
  imageProduct: AdminProductDto | null;
  editingImage: AdminProductImageDto | null;
  imageForm: UseFormReturn<ImageFormValues>;
  setImageProduct: Dispatch<SetStateAction<AdminProductDto | null>>;
  setEditingImage: Dispatch<SetStateAction<AdminProductImageDto | null>>;
  setIsImageModalOpen: Dispatch<SetStateAction<boolean>>;
  setNotice: Dispatch<SetStateAction<NoticeState>>;
}

export function useImageMutations({
  imageProduct,
  editingImage,
  imageForm,
  setImageProduct,
  setEditingImage,
  setIsImageModalOpen,
  setNotice
}: UseImageMutationsArgs) {
  const queryClient = useQueryClient();

  async function refreshProducts() {
    await queryClient.invalidateQueries({ queryKey: ["admin-products"] });
  }

  const imageSaveMutation = useMutation({
    mutationFn: (values: ImageFormValues) => {
      const request = toImageRequest(values);
      if (editingImage) return adminApi.updateProductImage(editingImage.id, request);
      if (!imageProduct) throw new Error("Product is required for a new image.");
      return adminApi.createProductImage(imageProduct.id, request);
    },
    onSuccess: async () => {
      await refreshProducts();
      setIsImageModalOpen(false);
      setImageProduct(null);
      setEditingImage(null);
      imageForm.reset(imageDefaultValues);
      setNotice({ type: "success", message: "Product image saved." });
    },
    onError: (error) => setNotice({ type: "error", message: getApiErrorMessage(error) })
  });

  const imageDeleteMutation = useMutation({
    mutationFn: (image: AdminProductImageDto) => adminApi.deleteProductImage(image.id),
    onSuccess: async () => {
      await refreshProducts();
      setNotice({ type: "success", message: "Product image deleted." });
    },
    onError: (error) => setNotice({ type: "error", message: getApiErrorMessage(error) })
  });

  return { imageSaveMutation, imageDeleteMutation };
}
