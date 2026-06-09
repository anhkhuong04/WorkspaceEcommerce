import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import type { AdminCategoryDto, AdminCategoryUpsertRequest } from "@workspace-ecommerce/api-types";
import { Alert, Button, Card, Empty, Form, Input, InputNumber, Modal, Select, Space, Switch, Table, Tag, message } from "antd";
import { useMemo, useState } from "react";
import { Controller, useForm } from "react-hook-form";
import { z } from "zod";
import { AdminPageHeader } from "../../components/ui/AdminPageHeader";
import { adminApi } from "../../services/api/adminApi";
import { getApiErrorMessage } from "../../services/api/errors";

const categorySchema = z.object({
  parentId: z.string().nullable(),
  name: z.string().trim().min(1, "Name is required.").max(200, "Name is too long."),
  slug: z.string().trim().min(1, "Slug is required.").max(200, "Slug is too long.").regex(/^[a-z0-9]+(?:-[a-z0-9]+)*$/, "Slug must use lowercase letters, numbers, and hyphens."),
  sortOrder: z.number().int("Sort order must be an integer.").min(0, "Sort order cannot be negative."),
  isActive: z.boolean()
});

type CategoryFormValues = z.infer<typeof categorySchema>;

type CategoryOption = {
  id: string;
  label: string;
  level: number;
};

const defaultValues: CategoryFormValues = {
  parentId: null,
  name: "",
  slug: "",
  sortOrder: 0,
  isActive: true
};

function flattenCategories(categories: AdminCategoryDto[], level = 0): CategoryOption[] {
  return categories.flatMap((category) => [
    { id: category.id, label: category.name, level },
    ...flattenCategories(category.children, level + 1)
  ]);
}

function collectDescendantIds(category: AdminCategoryDto): string[] {
  return category.children.flatMap((child) => [child.id, ...collectDescendantIds(child)]);
}

function findCategory(categories: AdminCategoryDto[], id: string): AdminCategoryDto | null {
  for (const category of categories) {
    if (category.id === id) {
      return category;
    }

    const child = findCategory(category.children, id);
    if (child) {
      return child;
    }
  }

  return null;
}

function toFormValues(category: AdminCategoryDto): CategoryFormValues {
  return {
    parentId: category.parentId,
    name: category.name,
    slug: category.slug,
    sortOrder: category.sortOrder,
    isActive: category.isActive
  };
}

function toRequest(values: CategoryFormValues): AdminCategoryUpsertRequest {
  return {
    parentId: values.parentId,
    name: values.name.trim(),
    slug: values.slug.trim(),
    sortOrder: values.sortOrder,
    isActive: values.isActive
  };
}

export function CategoriesPage() {
  const queryClient = useQueryClient();
  const categoriesQuery = useQuery({ queryKey: ["admin-categories"], queryFn: adminApi.getCategories });
  const [messageApi, contextHolder] = message.useMessage();
  const [editingCategory, setEditingCategory] = useState<AdminCategoryDto | null>(null);
  const [isModalOpen, setIsModalOpen] = useState(false);

  const form = useForm<CategoryFormValues>({ resolver: zodResolver(categorySchema), defaultValues });
  const categories = useMemo(() => categoriesQuery.data ?? [], [categoriesQuery.data]);
  const parentOptions = useMemo(() => flattenCategories(categories), [categories]);
  const blockedParentIds = useMemo(() => {
    if (!editingCategory) {
      return new Set<string>();
    }

    return new Set([editingCategory.id, ...collectDescendantIds(editingCategory)]);
  }, [editingCategory]);

  const saveMutation = useMutation({
    mutationFn: (values: CategoryFormValues) => {
      const request = toRequest(values);
      return editingCategory ? adminApi.updateCategory(editingCategory.id, request) : adminApi.createCategory(request);
    },
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ["admin-categories"] }),
        queryClient.invalidateQueries({ queryKey: ["admin-products"] })
      ]);
      setIsModalOpen(false);
      setEditingCategory(null);
      form.reset(defaultValues);
      messageApi.success("Category saved.");
    },
    onError: (error) => messageApi.error(getApiErrorMessage(error))
  });

  const toggleMutation = useMutation({
    mutationFn: (category: AdminCategoryDto) => adminApi.updateCategory(category.id, { ...toRequest(toFormValues(category)), isActive: !category.isActive }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["admin-categories"] });
      messageApi.success("Category status updated.");
    },
    onError: (error) => messageApi.error(getApiErrorMessage(error))
  });

  function openCreateModal() {
    setEditingCategory(null);
    form.reset(defaultValues);
    setIsModalOpen(true);
  }

  function openEditModal(category: AdminCategoryDto) {
    const latestCategory = findCategory(categories, category.id) ?? category;
    setEditingCategory(latestCategory);
    form.reset(toFormValues(latestCategory));
    setIsModalOpen(true);
  }

  return (
    <div className="admin-page-grid">
      {contextHolder}
      <AdminPageHeader
        title="Categories"
        description="Manage category visibility, sort order, and parent-child placement."
        actions={<Button type="primary" onClick={openCreateModal}>New category</Button>}
      />

      {categoriesQuery.isError ? (
        <Alert type="error" showIcon message="Categories could not be loaded" description={getApiErrorMessage(categoriesQuery.error)} />
      ) : null}

      <Card>
        <Table
          rowKey="id"
          loading={categoriesQuery.isLoading}
          dataSource={categories}
          pagination={false}
          locale={{ emptyText: <Empty description="No categories yet" /> }}
          columns={[
            { title: "Name", dataIndex: "name" },
            { title: "Slug", dataIndex: "slug" },
            { title: "Sort", dataIndex: "sortOrder", width: 90 },
            {
              title: "Status",
              dataIndex: "isActive",
              width: 150,
              render: (value: boolean, record) => (
                <Space>
                  <Switch checked={value} loading={toggleMutation.isPending} onChange={() => toggleMutation.mutate(record)} />
                  <Tag color={value ? "green" : "default"}>{value ? "Active" : "Inactive"}</Tag>
                </Space>
              )
            },
            {
              title: "Actions",
              key: "actions",
              width: 110,
              render: (_, record) => <Button onClick={() => openEditModal(record)}>Edit</Button>
            }
          ]}
        />
      </Card>

      <Modal
        title={editingCategory ? "Edit category" : "New category"}
        open={isModalOpen}
        onCancel={() => setIsModalOpen(false)}
        onOk={form.handleSubmit((values) => saveMutation.mutate(values))}
        confirmLoading={saveMutation.isPending}
        okText="Save"
      >
        <Form layout="vertical" className="admin-form">
          <Controller
            control={form.control}
            name="parentId"
            render={({ field }) => (
              <Form.Item label="Parent category">
                <Select
                  allowClear
                  placeholder="Root category"
                  value={field.value}
                  onChange={(value) => field.onChange(value ?? null)}
                  options={parentOptions.map((option) => ({
                    value: option.id,
                    label: `${"  ".repeat(option.level)}${option.label}`,
                    disabled: blockedParentIds.has(option.id)
                  }))}
                />
              </Form.Item>
            )}
          />
          <Controller
            control={form.control}
            name="name"
            render={({ field, fieldState }) => (
              <Form.Item label="Name" validateStatus={fieldState.error ? "error" : undefined} help={fieldState.error?.message}>
                <Input {...field} placeholder="Monitor arms" />
              </Form.Item>
            )}
          />
          <Controller
            control={form.control}
            name="slug"
            render={({ field, fieldState }) => (
              <Form.Item label="Slug" validateStatus={fieldState.error ? "error" : undefined} help={fieldState.error?.message}>
                <Input {...field} placeholder="monitor-arms" />
              </Form.Item>
            )}
          />
          <Space size={16} align="start">
            <Controller
              control={form.control}
              name="sortOrder"
              render={({ field, fieldState }) => (
                <Form.Item label="Sort order" validateStatus={fieldState.error ? "error" : undefined} help={fieldState.error?.message}>
                  <InputNumber min={0} value={field.value} onChange={(value) => field.onChange(value ?? 0)} />
                </Form.Item>
              )}
            />
            <Controller
              control={form.control}
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