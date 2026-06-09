import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import type {
  AdminCategoryDto,
  AdminProductDto,
  AdminProductUpsertRequest,
  AdminProductVariantDto,
  AdminProductVariantUpsertRequest
} from "@workspace-ecommerce/api-types";
import { formatMoney } from "@workspace-ecommerce/shared-utils";
import { Alert, Button, Card, Empty, Form, Input, InputNumber, Modal, Select, Space, Switch, Table, Tag, Typography, message } from "antd";
import { useMemo, useState } from "react";
import { Controller, useForm } from "react-hook-form";
import { z } from "zod";
import { AdminPageHeader } from "../../components/ui/AdminPageHeader";
import { adminApi } from "../../services/api/adminApi";
import { getApiErrorMessage } from "../../services/api/errors";

const productSchema = z.object({
  categoryId: z.string().min(1, "Category is required."),
  name: z.string().trim().min(1, "Name is required.").max(250, "Name is too long."),
  slug: z.string().trim().min(1, "Slug is required.").max(250, "Slug is too long.").regex(/^[a-z0-9]+(?:-[a-z0-9]+)*$/, "Slug must use lowercase letters, numbers, and hyphens."),
  description: z.string().trim().optional(),
  isFeatured: z.boolean(),
  isActive: z.boolean()
});

const variantSchema = z.object({
  sku: z.string().trim().min(1, "SKU is required.").max(100, "SKU is too long.").regex(/^[A-Za-z0-9][A-Za-z0-9._-]*$/, "SKU must use letters, numbers, dots, underscores, or hyphens."),
  name: z.string().trim().min(1, "Variant name is required.").max(250, "Variant name is too long."),
  color: z.string().trim().max(100, "Color is too long.").optional(),
  size: z.string().trim().max(100, "Size is too long.").optional(),
  price: z.number().min(0, "Price cannot be negative."),
  compareAtPrice: z.number().min(0, "Compare-at price cannot be negative.").nullable(),
  stockQuantity: z.number().int("Stock must be an integer.").min(0, "Stock cannot be negative."),
  requiresInstallation: z.boolean(),
  isActive: z.boolean()
}).refine((values) => values.compareAtPrice === null || values.compareAtPrice >= values.price, {
  path: ["compareAtPrice"],
  message: "Compare-at price cannot be lower than price."
});

type ProductFormValues = z.infer<typeof productSchema>;
type VariantFormValues = z.infer<typeof variantSchema>;

type CategoryOption = {
  id: string;
  label: string;
  level: number;
};

const productDefaultValues: ProductFormValues = {
  categoryId: "",
  name: "",
  slug: "",
  description: "",
  isFeatured: false,
  isActive: true
};

const variantDefaultValues: VariantFormValues = {
  sku: "",
  name: "",
  color: "",
  size: "",
  price: 0,
  compareAtPrice: null,
  stockQuantity: 0,
  requiresInstallation: false,
  isActive: true
};

function flattenCategories(categories: AdminCategoryDto[], level = 0): CategoryOption[] {
  return categories.flatMap((category) => [
    { id: category.id, label: category.name, level },
    ...flattenCategories(category.children, level + 1)
  ]);
}

function toProductFormValues(product: AdminProductDto): ProductFormValues {
  return {
    categoryId: product.categoryId,
    name: product.name,
    slug: product.slug,
    description: product.description ?? "",
    isFeatured: product.isFeatured,
    isActive: product.isActive
  };
}

function toProductRequest(values: ProductFormValues): AdminProductUpsertRequest {
  return {
    categoryId: values.categoryId,
    name: values.name.trim(),
    slug: values.slug.trim(),
    description: values.description?.trim() ? values.description.trim() : null,
    isFeatured: values.isFeatured,
    isActive: values.isActive
  };
}

function toVariantFormValues(variant: AdminProductVariantDto): VariantFormValues {
  return {
    sku: variant.sku,
    name: variant.name,
    color: variant.color ?? "",
    size: variant.size ?? "",
    price: variant.price,
    compareAtPrice: variant.compareAtPrice,
    stockQuantity: variant.stockQuantity,
    requiresInstallation: variant.requiresInstallation,
    isActive: variant.isActive
  };
}

function toVariantRequest(values: VariantFormValues): AdminProductVariantUpsertRequest {
  return {
    sku: values.sku.trim(),
    name: values.name.trim(),
    color: values.color?.trim() ? values.color.trim() : null,
    size: values.size?.trim() ? values.size.trim() : null,
    price: values.price,
    compareAtPrice: values.compareAtPrice,
    stockQuantity: values.stockQuantity,
    requiresInstallation: values.requiresInstallation,
    isActive: values.isActive
  };
}

export function ProductsPage() {
  const queryClient = useQueryClient();
  const productsQuery = useQuery({ queryKey: ["admin-products"], queryFn: adminApi.getProducts });
  const categoriesQuery = useQuery({ queryKey: ["admin-categories"], queryFn: adminApi.getCategories });
  const [messageApi, contextHolder] = message.useMessage();
  const [editingProduct, setEditingProduct] = useState<AdminProductDto | null>(null);
  const [variantProduct, setVariantProduct] = useState<AdminProductDto | null>(null);
  const [editingVariant, setEditingVariant] = useState<AdminProductVariantDto | null>(null);
  const [isProductModalOpen, setIsProductModalOpen] = useState(false);
  const [isVariantModalOpen, setIsVariantModalOpen] = useState(false);

  const productForm = useForm<ProductFormValues>({ resolver: zodResolver(productSchema), defaultValues: productDefaultValues });
  const variantForm = useForm<VariantFormValues>({ resolver: zodResolver(variantSchema), defaultValues: variantDefaultValues });
  const categoryOptions = useMemo(() => flattenCategories(categoriesQuery.data ?? []), [categoriesQuery.data]);

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
      messageApi.success("Product saved.");
    },
    onError: (error) => messageApi.error(getApiErrorMessage(error))
  });

  const productToggleMutation = useMutation({
    mutationFn: (product: AdminProductDto) => adminApi.updateProduct(product.id, { ...toProductRequest(toProductFormValues(product)), isActive: !product.isActive }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["admin-products"] });
      messageApi.success("Product status updated.");
    },
    onError: (error) => messageApi.error(getApiErrorMessage(error))
  });

  const variantSaveMutation = useMutation({
    mutationFn: (values: VariantFormValues) => {
      const request = toVariantRequest(values);
      if (editingVariant) {
        return adminApi.updateProductVariant(editingVariant.id, request);
      }

      if (!variantProduct) {
        throw new Error("Product is required for a new variant.");
      }

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
      messageApi.success("Variant saved.");
    },
    onError: (error) => messageApi.error(getApiErrorMessage(error))
  });

  const variantToggleMutation = useMutation({
    mutationFn: (variant: AdminProductVariantDto) => adminApi.updateProductVariant(variant.id, { ...toVariantRequest(toVariantFormValues(variant)), isActive: !variant.isActive }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["admin-products"] });
      messageApi.success("Variant status updated.");
    },
    onError: (error) => messageApi.error(getApiErrorMessage(error))
  });

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

  return (
    <div className="admin-page-grid">
      {contextHolder}
      <AdminPageHeader
        title="Products"
        description="Manage products, visibility, featured state, variants, pricing, stock, and installation flags."
        actions={<Button type="primary" disabled={categoryOptions.length === 0} onClick={openCreateProductModal}>New product</Button>}
      />

      {productsQuery.isError ? (
        <Alert type="error" showIcon message="Products could not be loaded" description={getApiErrorMessage(productsQuery.error)} />
      ) : null}
      {categoriesQuery.isError ? (
        <Alert type="warning" showIcon message="Categories could not be loaded" description="Product forms need categories before creating or editing products." />
      ) : null}

      <Card>
        <Table
          rowKey="id"
          loading={productsQuery.isLoading}
          dataSource={productsQuery.data ?? []}
          pagination={{ pageSize: 8 }}
          locale={{ emptyText: <Empty description="No products yet" /> }}
          expandable={{
            expandedRowRender: (product) => (
              <Table
                rowKey="id"
                size="small"
                dataSource={product.variants}
                pagination={false}
                locale={{ emptyText: <Empty description="No variants for this product" /> }}
                columns={[
                  { title: "SKU", dataIndex: "sku", width: 150 },
                  { title: "Variant", dataIndex: "name" },
                  { title: "Color", dataIndex: "color", render: (value: string | null) => value || "-" },
                  { title: "Size", dataIndex: "size", render: (value: string | null) => value || "-" },
                  { title: "Price", dataIndex: "price", render: (value: number) => formatMoney(value) },
                  { title: "Compare at", dataIndex: "compareAtPrice", render: (value: number | null) => value === null ? "-" : formatMoney(value) },
                  { title: "Stock", dataIndex: "stockQuantity", render: (value: number) => <Tag color={value <= 5 ? "red" : value <= 10 ? "orange" : "green"}>{value}</Tag> },
                  { title: "Install", dataIndex: "requiresInstallation", render: (value: boolean) => value ? <Tag color="blue">Required</Tag> : <Tag>None</Tag> },
                  {
                    title: "Active",
                    dataIndex: "isActive",
                    render: (value: boolean, variant) => <Switch checked={value} loading={variantToggleMutation.isPending} onChange={() => variantToggleMutation.mutate(variant)} />
                  },
                  { title: "Actions", key: "actions", render: (_, variant) => <Button onClick={() => openEditVariantModal(product, variant)}>Edit</Button> }
                ]}
              />
            )
          }}
          columns={[
            {
              title: "Product",
              dataIndex: "name",
              render: (value: string, record) => (
                <Space direction="vertical" size={0}>
                  <Typography.Text strong>{value}</Typography.Text>
                  <Typography.Text type="secondary">{record.slug}</Typography.Text>
                </Space>
              )
            },
            { title: "Category", dataIndex: "categoryName", render: (value: string | null) => value ?? "-" },
            { title: "Variants", dataIndex: "variants", width: 100, render: (variants: AdminProductVariantDto[]) => variants.length },
            { title: "Featured", dataIndex: "isFeatured", width: 120, render: (value: boolean) => value ? <Tag color="blue">Featured</Tag> : <Tag>Standard</Tag> },
            {
              title: "Status",
              dataIndex: "isActive",
              width: 150,
              render: (value: boolean, record) => (
                <Space>
                  <Switch checked={value} loading={productToggleMutation.isPending} onChange={() => productToggleMutation.mutate(record)} />
                  <Tag color={value ? "green" : "default"}>{value ? "Active" : "Inactive"}</Tag>
                </Space>
              )
            },
            {
              title: "Actions",
              key: "actions",
              width: 210,
              render: (_, record) => (
                <Space>
                  <Button onClick={() => openEditProductModal(record)}>Edit</Button>
                  <Button onClick={() => openCreateVariantModal(record)}>Add SKU</Button>
                </Space>
              )
            }
          ]}
        />
      </Card>

      <Modal
        title={editingProduct ? "Edit product" : "New product"}
        open={isProductModalOpen}
        onCancel={() => setIsProductModalOpen(false)}
        onOk={productForm.handleSubmit((values) => productSaveMutation.mutate(values))}
        confirmLoading={productSaveMutation.isPending}
        okText="Save"
        width={680}
      >
        <Form layout="vertical" className="admin-form">
          <Controller
            control={productForm.control}
            name="categoryId"
            render={({ field, fieldState }) => (
              <Form.Item label="Category" validateStatus={fieldState.error ? "error" : undefined} help={fieldState.error?.message}>
                <Select
                  value={field.value || undefined}
                  onChange={field.onChange}
                  placeholder="Select category"
                  options={categoryOptions.map((option) => ({ value: option.id, label: `${"  ".repeat(option.level)}${option.label}` }))}
                />
              </Form.Item>
            )}
          />
          <Controller
            control={productForm.control}
            name="name"
            render={({ field, fieldState }) => (
              <Form.Item label="Name" validateStatus={fieldState.error ? "error" : undefined} help={fieldState.error?.message}>
                <Input {...field} placeholder="Height adjustable desk" />
              </Form.Item>
            )}
          />
          <Controller
            control={productForm.control}
            name="slug"
            render={({ field, fieldState }) => (
              <Form.Item label="Slug" validateStatus={fieldState.error ? "error" : undefined} help={fieldState.error?.message}>
                <Input {...field} placeholder="height-adjustable-desk" />
              </Form.Item>
            )}
          />
          <Controller
            control={productForm.control}
            name="description"
            render={({ field, fieldState }) => (
              <Form.Item label="Description" validateStatus={fieldState.error ? "error" : undefined} help={fieldState.error?.message}>
                <Input.TextArea {...field} rows={4} />
              </Form.Item>
            )}
          />
          <Space size={24}>
            <Controller
              control={productForm.control}
              name="isFeatured"
              render={({ field }) => (
                <Form.Item label="Featured">
                  <Switch checked={field.value} onChange={field.onChange} />
                </Form.Item>
              )}
            />
            <Controller
              control={productForm.control}
              name="isActive"
              render={({ field }) => (
                <Form.Item label="Active">
                  <Switch checked={field.value} onChange={field.onChange} />
                </Form.Item>
              )}
            />
          </Space>
        </Form>
      </Modal>

      <Modal
        title={editingVariant ? "Edit variant" : `New SKU${variantProduct ? ` for ${variantProduct.name}` : ""}`}
        open={isVariantModalOpen}
        onCancel={() => setIsVariantModalOpen(false)}
        onOk={variantForm.handleSubmit((values) => variantSaveMutation.mutate(values))}
        confirmLoading={variantSaveMutation.isPending}
        okText="Save"
        width={720}
      >
        <Form layout="vertical" className="admin-form">
          <Controller
            control={variantForm.control}
            name="sku"
            render={({ field, fieldState }) => (
              <Form.Item label="SKU" validateStatus={fieldState.error ? "error" : undefined} help={fieldState.error?.message}>
                <Input {...field} placeholder="DESK-PRO-BLK" />
              </Form.Item>
            )}
          />
          <Controller
            control={variantForm.control}
            name="name"
            render={({ field, fieldState }) => (
              <Form.Item label="Variant name" validateStatus={fieldState.error ? "error" : undefined} help={fieldState.error?.message}>
                <Input {...field} placeholder="Black frame / 140cm" />
              </Form.Item>
            )}
          />
          <Space size={16} align="start" wrap>
            <Controller
              control={variantForm.control}
              name="color"
              render={({ field, fieldState }) => (
                <Form.Item label="Color" validateStatus={fieldState.error ? "error" : undefined} help={fieldState.error?.message}>
                  <Input {...field} placeholder="Black" />
                </Form.Item>
              )}
            />
            <Controller
              control={variantForm.control}
              name="size"
              render={({ field, fieldState }) => (
                <Form.Item label="Size" validateStatus={fieldState.error ? "error" : undefined} help={fieldState.error?.message}>
                  <Input {...field} placeholder="140cm" />
                </Form.Item>
              )}
            />
            <Controller
              control={variantForm.control}
              name="price"
              render={({ field, fieldState }) => (
                <Form.Item label="Price" validateStatus={fieldState.error ? "error" : undefined} help={fieldState.error?.message}>
                  <InputNumber min={0} value={field.value} onChange={(value) => field.onChange(value ?? 0)} />
                </Form.Item>
              )}
            />
            <Controller
              control={variantForm.control}
              name="compareAtPrice"
              render={({ field, fieldState }) => (
                <Form.Item label="Compare at" validateStatus={fieldState.error ? "error" : undefined} help={fieldState.error?.message}>
                  <InputNumber min={0} value={field.value} onChange={(value) => field.onChange(value)} />
                </Form.Item>
              )}
            />
            <Controller
              control={variantForm.control}
              name="stockQuantity"
              render={({ field, fieldState }) => (
                <Form.Item label="Stock" validateStatus={fieldState.error ? "error" : undefined} help={fieldState.error?.message}>
                  <InputNumber min={0} value={field.value} onChange={(value) => field.onChange(value ?? 0)} />
                </Form.Item>
              )}
            />
          </Space>
          <Space size={24}>
            <Controller
              control={variantForm.control}
              name="requiresInstallation"
              render={({ field }) => (
                <Form.Item label="Requires installation">
                  <Switch checked={field.value} onChange={field.onChange} />
                </Form.Item>
              )}
            />
            <Controller
              control={variantForm.control}
              name="isActive"
              render={({ field }) => (
                <Form.Item label="Active">
                  <Switch checked={field.value} onChange={field.onChange} />
                </Form.Item>
              )}
            />
          </Space>
        </Form>
      </Modal>
    </div>
  );
}

