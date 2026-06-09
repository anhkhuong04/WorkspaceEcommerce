import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import type { AdminBannerDto, AdminBannerUpsertRequest } from "@workspace-ecommerce/api-types";
import { Alert, Button, Card, Empty, Form, Image, Input, InputNumber, Modal, Space, Switch, Table, Tag, message } from "antd";
import { useState } from "react";
import { Controller, useForm } from "react-hook-form";
import { z } from "zod";
import { AdminPageHeader } from "../../components/ui/AdminPageHeader";
import { adminApi } from "../../services/api/adminApi";
import { getApiErrorMessage } from "../../services/api/errors";

const bannerSchema = z.object({
  title: z.string().trim().min(1, "Title is required.").max(250, "Title is too long."),
  imageUrl: z.string().trim().min(1, "Image URL is required.").max(1000, "Image URL is too long."),
  linkUrl: z.string().trim().max(1000, "Link URL is too long.").optional(),
  sortOrder: z.number().int("Sort order must be an integer.").min(0, "Sort order cannot be negative."),
  isActive: z.boolean()
});

type BannerFormValues = z.infer<typeof bannerSchema>;

const defaultValues: BannerFormValues = {
  title: "",
  imageUrl: "",
  linkUrl: "",
  sortOrder: 0,
  isActive: true
};

function toFormValues(banner: AdminBannerDto): BannerFormValues {
  return {
    title: banner.title,
    imageUrl: banner.imageUrl,
    linkUrl: banner.linkUrl ?? "",
    sortOrder: banner.sortOrder,
    isActive: banner.isActive
  };
}

function toRequest(values: BannerFormValues): AdminBannerUpsertRequest {
  return {
    title: values.title.trim(),
    imageUrl: values.imageUrl.trim(),
    linkUrl: values.linkUrl?.trim() ? values.linkUrl.trim() : null,
    sortOrder: values.sortOrder,
    isActive: values.isActive
  };
}

export function BannersPage() {
  const queryClient = useQueryClient();
  const bannersQuery = useQuery({ queryKey: ["admin-banners"], queryFn: adminApi.getBanners });
  const [messageApi, contextHolder] = message.useMessage();
  const [editingBanner, setEditingBanner] = useState<AdminBannerDto | null>(null);
  const [isModalOpen, setIsModalOpen] = useState(false);

  const form = useForm<BannerFormValues>({
    resolver: zodResolver(bannerSchema),
    defaultValues
  });

  const saveMutation = useMutation({
    mutationFn: (values: BannerFormValues) => {
      const request = toRequest(values);
      return editingBanner ? adminApi.updateBanner(editingBanner.id, request) : adminApi.createBanner(request);
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["admin-banners"] });
      setIsModalOpen(false);
      setEditingBanner(null);
      form.reset(defaultValues);
      messageApi.success("Banner saved.");
    },
    onError: (error) => messageApi.error(getApiErrorMessage(error))
  });

  const toggleMutation = useMutation({
    mutationFn: (banner: AdminBannerDto) => adminApi.updateBanner(banner.id, { ...toRequest(toFormValues(banner)), isActive: !banner.isActive }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["admin-banners"] });
      messageApi.success("Banner status updated.");
    },
    onError: (error) => messageApi.error(getApiErrorMessage(error))
  });

  function openCreateModal() {
    setEditingBanner(null);
    form.reset(defaultValues);
    setIsModalOpen(true);
  }

  function openEditModal(banner: AdminBannerDto) {
    setEditingBanner(banner);
    form.reset(toFormValues(banner));
    setIsModalOpen(true);
  }

  return (
    <div className="admin-page-grid">
      {contextHolder}
      <AdminPageHeader
        title="Banners"
        description="Create, update, activate, deactivate, and sort homepage banners."
        actions={<Button type="primary" onClick={openCreateModal}>New banner</Button>}
      />

      {bannersQuery.isError ? (
        <Alert type="error" showIcon message="Banners could not be loaded" description={getApiErrorMessage(bannersQuery.error)} />
      ) : null}

      <Card>
        <Table
          rowKey="id"
          loading={bannersQuery.isLoading}
          dataSource={bannersQuery.data ?? []}
          pagination={false}
          locale={{ emptyText: <Empty description="No banners yet" /> }}
          columns={[
            {
              title: "Preview",
              dataIndex: "imageUrl",
              width: 116,
              render: (value: string, record) => <Image width={84} height={44} src={value} alt={record.title} className="admin-table-image" />
            },
            { title: "Title", dataIndex: "title" },
            { title: "Image", dataIndex: "imageUrl", ellipsis: true },
            { title: "Link", dataIndex: "linkUrl", ellipsis: true, render: (value: string | null) => value || "-" },
            { title: "Sort", dataIndex: "sortOrder", width: 90 },
            {
              title: "Status",
              dataIndex: "isActive",
              width: 140,
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
        title={editingBanner ? "Edit banner" : "New banner"}
        open={isModalOpen}
        onCancel={() => setIsModalOpen(false)}
        onOk={form.handleSubmit((values) => saveMutation.mutate(values))}
        confirmLoading={saveMutation.isPending}
        okText="Save"
      >
        <Form layout="vertical" className="admin-form">
          <Controller
            control={form.control}
            name="title"
            render={({ field, fieldState }) => (
              <Form.Item label="Title" validateStatus={fieldState.error ? "error" : undefined} help={fieldState.error?.message}>
                <Input {...field} placeholder="Ergonomic workspace sale" />
              </Form.Item>
            )}
          />
          <Controller
            control={form.control}
            name="imageUrl"
            render={({ field, fieldState }) => (
              <Form.Item label="Image URL" validateStatus={fieldState.error ? "error" : undefined} help={fieldState.error?.message}>
                <Input {...field} placeholder="/demo/banners/workspace-hero.webp" />
              </Form.Item>
            )}
          />
          <Controller
            control={form.control}
            name="linkUrl"
            render={({ field, fieldState }) => (
              <Form.Item label="Link URL" validateStatus={fieldState.error ? "error" : undefined} help={fieldState.error?.message}>
                <Input {...field} placeholder="/products" />
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