import { zodResolver } from "@hookform/resolvers/zod";
import { useMemo, useState } from "react";
import { useForm } from "react-hook-form";
import { useSearchParams } from "react-router-dom";
import { AdminPageHeader } from "../../components/ui/AdminPageHeader";
import { Button, ConfirmDialog, Notice } from "../../components/ui/AdminUi";
import { useAdminCategories } from "../../hooks/queries/useAdminCategories";
import { useAdminProducts } from "../../hooks/queries/useAdminProducts";
import { getApiErrorMessage } from "../../services/api/errors";
import { ImageModal } from "./components/ImageModal";
import { ProductModal } from "./components/ProductModal";
import { ProductTable } from "./components/ProductTable";
import { SpecificationModal } from "./components/SpecificationModal";
import { VariantModal } from "./components/VariantModal";
import { useImageMutations } from "./hooks/useImageMutations";
import { useProductMutations } from "./hooks/useProductMutations";
import { useProductPageState } from "./hooks/useProductPageState";
import { useSpecificationMutations } from "./hooks/useSpecificationMutations";
import { useVariantMutations } from "./hooks/useVariantMutations";
import type { NoticeState } from "./productTypes";
import {
  flattenCategories,
  imageDefaultValues,
  imageSchema,
  productDefaultValues,
  productSchema,
  specificationDefaultValues,
  specificationSchema,
  variantDefaultValues,
  variantSchema
} from "./productTypes";

const pageSize = 20;

export function ProductsPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const [pageNumber, setPageNumber] = useState(1);
  const [notice, setNotice] = useState<NoticeState>(null);
  const productsQuery = useAdminProducts({ pageNumber, pageSize });
  const categoriesQuery = useAdminCategories();
  const productForm = useForm({ resolver: zodResolver(productSchema), defaultValues: productDefaultValues });
  const variantForm = useForm({ resolver: zodResolver(variantSchema), defaultValues: variantDefaultValues });
  const imageForm = useForm({ resolver: zodResolver(imageSchema), defaultValues: imageDefaultValues });
  const specificationForm = useForm({ resolver: zodResolver(specificationSchema), defaultValues: specificationDefaultValues });
  const products = productsQuery.data?.items ?? [];
  const categoryOptions = useMemo(() => flattenCategories(categoriesQuery.data ?? []), [categoriesQuery.data]);
  const state = useProductPageState({ products, categoryOptions, productForm, variantForm, imageForm, specificationForm, searchParams, setSearchParams });
  const { productSaveMutation, productDeleteMutation, productToggleMutation } = useProductMutations({
    editingProduct: state.editingProduct,
    productForm,
    setEditingProduct: state.setEditingProduct,
    setIsProductModalOpen: state.setIsProductModalOpen,
    setExpandedProductIds: state.setExpandedProductIds,
    setNotice
  });
  const { variantSaveMutation, variantToggleMutation } = useVariantMutations({
    variantProduct: state.variantProduct,
    editingVariant: state.editingVariant,
    variantForm,
    setVariantProduct: state.setVariantProduct,
    setEditingVariant: state.setEditingVariant,
    setIsVariantModalOpen: state.setIsVariantModalOpen,
    setNotice
  });
  const { imageSaveMutation, imageDeleteMutation } = useImageMutations({
    imageProduct: state.imageProduct,
    editingImage: state.editingImage,
    imageForm,
    setImageProduct: state.setImageProduct,
    setEditingImage: state.setEditingImage,
    setIsImageModalOpen: state.setIsImageModalOpen,
    setNotice
  });
  const { specificationSaveMutation, specificationDeleteMutation } = useSpecificationMutations({
    specificationProduct: state.specificationProduct,
    editingSpecification: state.editingSpecification,
    specificationForm,
    setSpecificationProduct: state.setSpecificationProduct,
    setEditingSpecification: state.setEditingSpecification,
    setIsSpecificationModalOpen: state.setIsSpecificationModalOpen,
    setNotice
  });
  const deleteBusy = productDeleteMutation.isPending || imageDeleteMutation.isPending || specificationDeleteMutation.isPending;

  return (
    <div className="admin-page-grid">
      <AdminPageHeader title="Products" description="Manage products, visibility, featured state, variants, images, specifications, pricing, stock, and installation flags." actions={<Button type="button" variant="primary" disabled={categoryOptions.length === 0} onClick={state.openCreateProductModal}>New product</Button>} />
      {notice ? <Notice type={notice.type} title={notice.message} /> : null}
      {productsQuery.isError ? <Notice type="error" title="Products could not be loaded">{getApiErrorMessage(productsQuery.error)}</Notice> : null}
      {categoriesQuery.isError ? <Notice type="warning" title="Categories could not be loaded">Product forms need categories before creating or editing products.</Notice> : null}

      <ProductTable
        products={products}
        isLoading={productsQuery.isLoading}
        targetProduct={state.targetProduct}
        targetVariantId={state.targetVariantId}
        productTogglePending={productToggleMutation.isPending}
        variantTogglePending={variantToggleMutation.isPending}
        productDeletePending={productDeleteMutation.isPending}
        imageDeletePending={imageDeleteMutation.isPending}
        specificationDeletePending={specificationDeleteMutation.isPending}
        isProductExpanded={state.isProductExpanded}
        onToggleExpanded={state.toggleExpanded}
        onToggleProductStatus={(product) => productToggleMutation.mutate(product)}
        onToggleVariantStatus={(variant) => variantToggleMutation.mutate(variant)}
        onEditProduct={state.openEditProductModal}
        onCreateVariant={state.openCreateVariantModal}
        onEditVariant={state.openEditVariantModal}
        onCreateImage={state.openCreateImageModal}
        onEditImage={state.openEditImageModal}
        onCreateSpecification={state.openCreateSpecificationModal}
        onEditSpecification={state.openEditSpecificationModal}
        onDeleteProduct={(product) => state.setDeleteTarget({ type: "product", item: product })}
        onDeleteImage={(image) => state.setDeleteTarget({ type: "image", item: image })}
        onDeleteSpecification={(specification) => state.setDeleteTarget({ type: "specification", item: specification })}
      />

      {productsQuery.data && productsQuery.data.totalPages > 1 ? (
        <div className="flex items-center justify-end gap-3">
          <span className="text-sm font-semibold text-slate-500">Page {productsQuery.data.pageNumber} of {productsQuery.data.totalPages}</span>
          <Button type="button" disabled={!productsQuery.data.hasPreviousPage || productsQuery.isFetching} onClick={() => setPageNumber((current) => Math.max(1, current - 1))}>Previous</Button>
          <Button type="button" disabled={!productsQuery.data.hasNextPage || productsQuery.isFetching} onClick={() => setPageNumber((current) => current + 1)}>Next</Button>
        </div>
      ) : null}

      <ProductModal open={state.isProductModalOpen} editingProduct={state.editingProduct} categoryOptions={categoryOptions} form={productForm} isPending={productSaveMutation.isPending} onClose={() => state.setIsProductModalOpen(false)} onSubmit={(values) => productSaveMutation.mutate(values)} />
      <VariantModal open={state.isVariantModalOpen} variantProduct={state.variantProduct} editingVariant={state.editingVariant} form={variantForm} isPending={variantSaveMutation.isPending} onClose={() => state.setIsVariantModalOpen(false)} onSubmit={(values) => variantSaveMutation.mutate(values)} />
      <ImageModal open={state.isImageModalOpen} imageProduct={state.imageProduct} editingImage={state.editingImage} form={imageForm} isPending={imageSaveMutation.isPending} onClose={() => state.setIsImageModalOpen(false)} onSubmit={(values) => imageSaveMutation.mutate(values)} />
      <SpecificationModal open={state.isSpecificationModalOpen} specificationProduct={state.specificationProduct} editingSpecification={state.editingSpecification} form={specificationForm} isPending={specificationSaveMutation.isPending} onClose={() => state.setIsSpecificationModalOpen(false)} onSubmit={(values) => specificationSaveMutation.mutate(values)} />

      <ConfirmDialog
        open={state.deleteTarget !== null}
        title={state.deleteTarget?.type === "product" ? "Delete product" : state.deleteTarget?.type === "image" ? "Delete product image" : "Delete product specification"}
        message={state.deleteTarget?.type === "product" ? "This permanently removes the product, variants, images, and specifications. Products with order history cannot be deleted; deactivate them instead." : state.deleteTarget?.type === "image" ? "This image will be removed from the product gallery." : "This specification will be removed from the product detail data."}
        confirmLabel="Delete"
        busy={deleteBusy}
        onCancel={() => state.setDeleteTarget(null)}
        onConfirm={() => state.confirmDeleteTarget({
          onProduct: (product) => productDeleteMutation.mutate(product, { onSuccess: () => state.setDeleteTarget(null) }),
          onImage: (image) => imageDeleteMutation.mutate(image, { onSuccess: () => state.setDeleteTarget(null) }),
          onSpecification: (specification) => specificationDeleteMutation.mutate(specification, { onSuccess: () => state.setDeleteTarget(null) })
        })}
      />
    </div>
  );
}
