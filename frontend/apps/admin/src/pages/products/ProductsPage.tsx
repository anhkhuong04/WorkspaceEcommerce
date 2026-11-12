import { Pagination } from "../../components/ui/Pagination";
import { Plus } from "lucide-react";
import { zodResolver } from "@hookform/resolvers/zod";
import { useMemo, useState } from "react";
import { useForm } from "react-hook-form";
import { useSearchParams } from "react-router-dom";
import type { AdminProductListRequest } from "@workspace-ecommerce/api-types";
import { AdminPageHeader } from "../../components/ui/AdminPageHeader";
import { Button, ConfirmDialog, Notice } from "../../components/ui/AdminUi";
import { useAdminCategories } from "../../hooks/queries/useAdminCategories";
import { useAdminProducts } from "../../hooks/queries/useAdminProducts";
import { getApiErrorMessage } from "../../services/api/errors";
import { ImageModal } from "./components/ImageModal";
import { ProductModal } from "./components/ProductModal";
import { ProductTable } from "./components/ProductTable";
import { ProductFilters } from "./components/ProductFilters";
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
  variantSchema,
} from "./productTypes";

const pageSize = 20;

export function ProductsPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const requestedPage = Number(searchParams.get("page") ?? 1);
  const pageNumber = searchParams.has("productId") ? 1 : Number.isInteger(requestedPage) && requestedPage > 0 ? requestedPage : 1;
  const request = useMemo<AdminProductListRequest>(() => {
    const number = (key: string) => { const value = searchParams.get(key); return value && /^\d+$/.test(value) ? Number(value) : undefined; };
    const sort = searchParams.get("sortBy");
    const stock = searchParams.get("stock");
    return { pageNumber, pageSize, productId: searchParams.get("productId") || undefined,
      search: searchParams.get("search") || undefined, categoryId: searchParams.get("categoryId") || undefined,
      isActive: ["true", "false"].includes(searchParams.get("isActive") ?? "") ? searchParams.get("isActive") === "true" : undefined,
      isFeatured: ["true", "false"].includes(searchParams.get("isFeatured") ?? "") ? searchParams.get("isFeatured") === "true" : undefined,
      stock: stock === "in" || stock === "out" || stock === "low" ? stock : undefined,
      minPrice: number("minPrice"), maxPrice: number("maxPrice"),
      sortBy: sort === "created-desc" || sort === "price-asc" || sort === "price-desc" || sort === "stock-asc" ? sort : "slug" };
  }, [searchParams, pageNumber]);
  function setPageNumber(page: number) {
    const params = new URLSearchParams(searchParams);
    params.delete("productId");
    params.delete("variantId");
    if (page <= 1) params.delete("page");
    else params.set("page", String(page));
    setSearchParams(params);
  }
  const [notice, setNotice] = useState<NoticeState>(null);
  const productsQuery = useAdminProducts(request);
  const categoriesQuery = useAdminCategories();
  const productForm = useForm({
    resolver: zodResolver(productSchema),
    defaultValues: productDefaultValues,
  });
  const variantForm = useForm({
    resolver: zodResolver(variantSchema),
    defaultValues: variantDefaultValues,
  });
  const imageForm = useForm({
    resolver: zodResolver(imageSchema),
    defaultValues: imageDefaultValues,
  });
  const specificationForm = useForm({
    resolver: zodResolver(specificationSchema),
    defaultValues: specificationDefaultValues,
  });
  const products = productsQuery.data?.items ?? [];
  const categoryOptions = useMemo(
    () => flattenCategories(categoriesQuery.data ?? []),
    [categoriesQuery.data],
  );
  const state = useProductPageState({
    products,
    categoryOptions,
    productForm,
    variantForm,
    imageForm,
    specificationForm,
    searchParams,
    setSearchParams,
  });
  const { productSaveMutation, productDeleteMutation, productToggleMutation } =
    useProductMutations({
      editingProduct: state.editingProduct,
      productForm,
      setEditingProduct: state.setEditingProduct,
      setIsProductModalOpen: state.setIsProductModalOpen,
      setExpandedProductIds: state.setExpandedProductIds,
      setNotice,
    });
  const { variantSaveMutation, variantToggleMutation } = useVariantMutations({
    variantProduct: state.variantProduct,
    editingVariant: state.editingVariant,
    variantForm,
    setVariantProduct: state.setVariantProduct,
    setEditingVariant: state.setEditingVariant,
    setIsVariantModalOpen: state.setIsVariantModalOpen,
    setNotice,
  });
  const { imageSaveMutation, imageDeleteMutation } = useImageMutations({
    imageProduct: state.imageProduct,
    editingImage: state.editingImage,
    imageForm,
    setImageProduct: state.setImageProduct,
    setEditingImage: state.setEditingImage,
    setIsImageModalOpen: state.setIsImageModalOpen,
    setNotice,
  });
  const { specificationSaveMutation, specificationDeleteMutation } =
    useSpecificationMutations({
      specificationProduct: state.specificationProduct,
      editingSpecification: state.editingSpecification,
      specificationForm,
      setSpecificationProduct: state.setSpecificationProduct,
      setEditingSpecification: state.setEditingSpecification,
      setIsSpecificationModalOpen: state.setIsSpecificationModalOpen,
      setNotice,
    });
  const deleteBusy =
    productDeleteMutation.isPending ||
    imageDeleteMutation.isPending ||
    specificationDeleteMutation.isPending;

  return (
    <div className="admin-page-grid">
      <AdminPageHeader
        title="Products"
        description="Manage your catalog, pricing and inventory."
        actions={
          <Button
            type="button"
            variant="primary"
            disabled={categoryOptions.length === 0}
            onClick={() => {
              productSaveMutation.reset();
              state.openCreateProductModal();
            }}
          >
            <Plus size={17} aria-hidden="true" />
            New product
          </Button>
        }
      />
      {notice ? <Notice type={notice.type} title={notice.message} /> : null}
      {productsQuery.isError ? (
        <Notice type="error" title="Products could not be loaded">
          {getApiErrorMessage(productsQuery.error)}
          <div className="mt-3">
            <Button
              disabled={productsQuery.isFetching}
              onClick={() => void productsQuery.refetch()}
            >
              Try again
            </Button>
          </div>
        </Notice>
      ) : null}
      {categoriesQuery.isError ? (
        <Notice type="warning" title="Categories could not be loaded">
          Product forms need categories before creating or editing products.
        </Notice>
      ) : null}

      <ProductFilters request={request} categories={categoryOptions} busy={productsQuery.isFetching}
        onApply={values => {
          const params = new URLSearchParams();
          for (const [key, value] of Object.entries(values)) if (value !== undefined && value !== "") params.set(key, String(value));
          setSearchParams(params);
        }} onReset={() => setSearchParams(new URLSearchParams())} />
      {request.productId && <Notice title="Viewing a selected product"><Button onClick={() => setSearchParams({})}>Show all products</Button></Notice>}
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
        onToggleProductStatus={(product) =>
          productToggleMutation.mutate(product)
        }
        onToggleVariantStatus={(variant) =>
          variantToggleMutation.mutate(variant)
        }
        onEditProduct={(...args) => {
          productSaveMutation.reset();
          state.openEditProductModal(...args);
        }}
        onCreateVariant={(product) => {
          variantSaveMutation.reset();
          state.openCreateVariantModal(product);
        }}
        onEditVariant={(...args) => {
          variantSaveMutation.reset();
          state.openEditVariantModal(...args);
        }}
        onCreateImage={(product) => {
          imageSaveMutation.reset();
          state.openCreateImageModal(product);
        }}
        onEditImage={(...args) => {
          imageSaveMutation.reset();
          state.openEditImageModal(...args);
        }}
        onCreateSpecification={(product) => {
          specificationSaveMutation.reset();
          state.openCreateSpecificationModal(product);
        }}
        onEditSpecification={(...args) => {
          specificationSaveMutation.reset();
          state.openEditSpecificationModal(...args);
        }}
        onDeleteProduct={(product) => {
          productDeleteMutation.reset();
          state.setDeleteTarget({ type: "product", item: product });
        }}
        onDeleteImage={(image) => {
          imageDeleteMutation.reset();
          state.setDeleteTarget({ type: "image", item: image });
        }}
        onDeleteSpecification={(specification) => {
          specificationDeleteMutation.reset();
          state.setDeleteTarget({ type: "specification", item: specification });
        }}
      />

      {productsQuery.data ? (
        <Pagination
          data={productsQuery.data}
          label="products"
          busy={productsQuery.isFetching}
          onPageChange={setPageNumber}
        />
      ) : null}

      <ProductModal
        open={state.isProductModalOpen}
        editingProduct={state.editingProduct}
        categoryOptions={categoryOptions}
        form={productForm}
        isPending={productSaveMutation.isPending}
        error={
          productSaveMutation.isError
            ? getApiErrorMessage(productSaveMutation.error)
            : undefined
        }
        onClose={() => state.setIsProductModalOpen(false)}
        onSubmit={(values) => productSaveMutation.mutate(values)}
      />
      <VariantModal
        open={state.isVariantModalOpen}
        variantProduct={state.variantProduct}
        editingVariant={state.editingVariant}
        form={variantForm}
        isPending={variantSaveMutation.isPending}
        error={
          variantSaveMutation.isError
            ? getApiErrorMessage(variantSaveMutation.error)
            : undefined
        }
        onClose={() => state.setIsVariantModalOpen(false)}
        onSubmit={(values) => variantSaveMutation.mutate(values)}
      />
      <ImageModal
        open={state.isImageModalOpen}
        imageProduct={state.imageProduct}
        editingImage={state.editingImage}
        form={imageForm}
        isPending={imageSaveMutation.isPending}
        error={
          imageSaveMutation.isError
            ? getApiErrorMessage(imageSaveMutation.error)
            : undefined
        }
        onClose={() => state.setIsImageModalOpen(false)}
        onSubmit={(values) => imageSaveMutation.mutate(values)}
      />
      <SpecificationModal
        open={state.isSpecificationModalOpen}
        specificationProduct={state.specificationProduct}
        editingSpecification={state.editingSpecification}
        form={specificationForm}
        isPending={specificationSaveMutation.isPending}
        error={
          specificationSaveMutation.isError
            ? getApiErrorMessage(specificationSaveMutation.error)
            : undefined
        }
        onClose={() => state.setIsSpecificationModalOpen(false)}
        onSubmit={(values) => specificationSaveMutation.mutate(values)}
      />

      <ConfirmDialog
        open={state.deleteTarget !== null}
        title={
          state.deleteTarget?.type === "product"
            ? "Delete product"
            : state.deleteTarget?.type === "image"
              ? "Delete product image"
              : "Delete product specification"
        }
        message={
          state.deleteTarget?.type === "product"
            ? "This permanently removes the product, variants, images, and specifications. Products with order history cannot be deleted; deactivate them instead."
            : state.deleteTarget?.type === "image"
              ? "This image will be removed from the product gallery."
              : "This specification will be removed from the product detail data."
        }
        confirmLabel="Delete"
        busy={deleteBusy}
        error={
          state.deleteTarget?.type === "product" &&
          productDeleteMutation.isError
            ? getApiErrorMessage(productDeleteMutation.error)
            : state.deleteTarget?.type === "image" &&
                imageDeleteMutation.isError
              ? getApiErrorMessage(imageDeleteMutation.error)
              : state.deleteTarget?.type === "specification" &&
                  specificationDeleteMutation.isError
                ? getApiErrorMessage(specificationDeleteMutation.error)
                : undefined
        }
        onCancel={() => state.setDeleteTarget(null)}
        onConfirm={() =>
          state.confirmDeleteTarget({
            onProduct: (product) =>
              productDeleteMutation.mutate(product, {
                onSuccess: () => state.setDeleteTarget(null),
              }),
            onImage: (image) =>
              imageDeleteMutation.mutate(image, {
                onSuccess: () => state.setDeleteTarget(null),
              }),
            onSpecification: (specification) =>
              specificationDeleteMutation.mutate(specification, {
                onSuccess: () => state.setDeleteTarget(null),
              }),
          })
        }
      />
    </div>
  );
}
