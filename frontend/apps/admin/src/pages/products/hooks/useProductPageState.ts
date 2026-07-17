import type { AdminProductDto, AdminProductImageDto, AdminProductSpecificationDto, AdminProductVariantDto } from "@workspace-ecommerce/api-types";
import { useEffect, useMemo, useRef, useState } from "react";
import type { Dispatch, SetStateAction } from "react";
import type { UseFormReturn } from "react-hook-form";
import type {
  CategoryOption,
  DeleteTarget,
  ImageFormValues,
  ProductFormValues,
  SpecificationFormValues,
  VariantFormValues
} from "../productTypes";
import {
  imageDefaultValues,
  productDefaultValues,
  specificationDefaultValues,
  toImageFormValues,
  toProductFormValues,
  toSpecificationFormValues,
  toVariantFormValues,
  variantDefaultValues
} from "../productTypes";

interface UseProductPageStateArgs {
  products: AdminProductDto[];
  categoryOptions: CategoryOption[];
  productForm: UseFormReturn<ProductFormValues>;
  variantForm: UseFormReturn<VariantFormValues>;
  imageForm: UseFormReturn<ImageFormValues>;
  specificationForm: UseFormReturn<SpecificationFormValues>;
  searchParams: URLSearchParams;
  setSearchParams: (nextParams: URLSearchParams, options?: { replace?: boolean }) => void;
}

interface DeleteHandlers {
  onProduct: (product: AdminProductDto) => void;
  onImage: (image: AdminProductImageDto) => void;
  onSpecification: (specification: AdminProductSpecificationDto) => void;
}

export function useProductPageState({
  products,
  categoryOptions,
  productForm,
  variantForm,
  imageForm,
  specificationForm,
  searchParams,
  setSearchParams
}: UseProductPageStateArgs) {
  const [editingProduct, setEditingProduct] = useState<AdminProductDto | null>(null);
  const [variantProduct, setVariantProduct] = useState<AdminProductDto | null>(null);
  const [editingVariant, setEditingVariant] = useState<AdminProductVariantDto | null>(null);
  const [imageProduct, setImageProduct] = useState<AdminProductDto | null>(null);
  const [editingImage, setEditingImage] = useState<AdminProductImageDto | null>(null);
  const [specificationProduct, setSpecificationProduct] = useState<AdminProductDto | null>(null);
  const [editingSpecification, setEditingSpecification] = useState<AdminProductSpecificationDto | null>(null);
  const [isProductModalOpen, setIsProductModalOpen] = useState(false);
  const [isVariantModalOpen, setIsVariantModalOpen] = useState(false);
  const [isImageModalOpen, setIsImageModalOpen] = useState(false);
  const [isSpecificationModalOpen, setIsSpecificationModalOpen] = useState(false);
  const [deleteTarget, setDeleteTarget] = useState<DeleteTarget | null>(null);
  const [expandedProductIds, setExpandedProductIds] = useState<Set<string>>(new Set());
  const handledTargetRef = useRef<string | null>(null);
  const targetProductId = searchParams.get("productId");
  const targetVariantId = searchParams.get("variantId");
  const targetProduct = useMemo(() => {
    if (!targetProductId) return null;
    const product = products.find((item) => item.id === targetProductId) ?? null;
    if (targetVariantId && !product?.variants.some((variant) => variant.id === targetVariantId)) return null;
    return product;
  }, [products, targetProductId, targetVariantId]);

  useEffect(() => {
    if (!targetProduct) {
      handledTargetRef.current = null;
      return;
    }

    const targetKey = `${targetProduct.id}:${targetVariantId ?? ""}`;
    if (handledTargetRef.current === targetKey) return;

    handledTargetRef.current = targetKey;
    window.setTimeout(() => {
      document.getElementById(targetVariantId ? `variant-${targetVariantId}` : `product-${targetProduct.id}`)?.scrollIntoView({ behavior: "smooth", block: "center" });
    }, 0);
  }, [targetProduct, targetVariantId]);

  function openCreateProductModal() {
    setEditingProduct(null);
    productForm.reset({ ...productDefaultValues, categoryId: categoryOptions[0]?.id ?? "" });
    setIsProductModalOpen(true);
  }

  function openEditProductModal(product: AdminProductDto) {
    setEditingProduct(product);
    productForm.reset(toProductFormValues(product));
    setIsProductModalOpen(true);
  }

  function openCreateVariantModal(product: AdminProductDto) {
    setVariantProduct(product);
    setEditingVariant(null);
    variantForm.reset(variantDefaultValues);
    setIsVariantModalOpen(true);
  }

  function openEditVariantModal(product: AdminProductDto, variant: AdminProductVariantDto) {
    setVariantProduct(product);
    setEditingVariant(variant);
    variantForm.reset(toVariantFormValues(variant));
    setIsVariantModalOpen(true);
  }

  function openCreateImageModal(product: AdminProductDto) {
    setImageProduct(product);
    setEditingImage(null);
    imageForm.reset({ ...imageDefaultValues, sortOrder: product.images.length + 1 });
    setIsImageModalOpen(true);
  }

  function openEditImageModal(product: AdminProductDto, image: AdminProductImageDto) {
    setImageProduct(product);
    setEditingImage(image);
    imageForm.reset(toImageFormValues(image));
    setIsImageModalOpen(true);
  }

  function openCreateSpecificationModal(product: AdminProductDto) {
    setSpecificationProduct(product);
    setEditingSpecification(null);
    specificationForm.reset({ ...specificationDefaultValues, sortOrder: product.specifications.length + 1 });
    setIsSpecificationModalOpen(true);
  }

  function openEditSpecificationModal(product: AdminProductDto, specification: AdminProductSpecificationDto) {
    setSpecificationProduct(product);
    setEditingSpecification(specification);
    specificationForm.reset(toSpecificationFormValues(specification));
    setIsSpecificationModalOpen(true);
  }

  function toggleExpanded(productId: string) {
    if (targetProduct?.id === productId) {
      const nextParams = new URLSearchParams(searchParams);
      nextParams.delete("productId");
      nextParams.delete("variantId");
      setSearchParams(nextParams, { replace: true });
      return;
    }

    setExpandedProductIds((current) => {
      const next = new Set(current);
      if (next.has(productId)) next.delete(productId);
      else next.add(productId);
      return next;
    });
  }

  function isProductExpanded(productId: string) {
    return expandedProductIds.has(productId) || targetProduct?.id === productId;
  }

  function confirmDeleteTarget(handlers: DeleteHandlers) {
    if (!deleteTarget) {
      return;
    }

    if (deleteTarget.type === "product") {
      handlers.onProduct(deleteTarget.item);
      return;
    }

    if (deleteTarget.type === "image") {
      handlers.onImage(deleteTarget.item);
      return;
    }

    handlers.onSpecification(deleteTarget.item);
  }

  return {
    editingProduct,
    setEditingProduct,
    variantProduct,
    setVariantProduct,
    editingVariant,
    setEditingVariant,
    imageProduct,
    setImageProduct,
    editingImage,
    setEditingImage,
    specificationProduct,
    setSpecificationProduct,
    editingSpecification,
    setEditingSpecification,
    isProductModalOpen,
    setIsProductModalOpen,
    isVariantModalOpen,
    setIsVariantModalOpen,
    isImageModalOpen,
    setIsImageModalOpen,
    isSpecificationModalOpen,
    setIsSpecificationModalOpen,
    deleteTarget,
    setDeleteTarget,
    expandedProductIds,
    setExpandedProductIds: setExpandedProductIds as Dispatch<SetStateAction<Set<string>>>,
    targetProduct,
    targetVariantId,
    openCreateProductModal,
    openEditProductModal,
    openCreateVariantModal,
    openEditVariantModal,
    openCreateImageModal,
    openEditImageModal,
    openCreateSpecificationModal,
    openEditSpecificationModal,
    toggleExpanded,
    isProductExpanded,
    confirmDeleteTarget
  };
}
