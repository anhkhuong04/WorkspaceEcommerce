import { useId } from "react";
import { zodResolver } from "@hookform/resolvers/zod";
import { Pencil, Plus, Trash2 } from "lucide-react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import type {
  AdminBannerDto,
  AdminBannerUpsertRequest,
} from "@workspace-ecommerce/api-types";
import { useState } from "react";
import { Controller, useForm } from "react-hook-form";
import { z } from "zod";
import { AdminPageHeader } from "../../components/ui/AdminPageHeader";
import { ImagePickerField } from "../../components/media/ImagePickerField";
import {
  Button,
  ConfirmDialog,
  EmptyState,
  Field,
  IconButton,
  Modal,
  Notice,
  Pill,
  TableViewport,
  TextInput,
  Toggle,
} from "../../components/ui/AdminUi";
import { useAdminBanners } from "../../hooks/queries/useAdminBanners";
import { adminApi } from "../../services/api/adminApi";
import { getApiErrorMessage } from "../../services/api/errors";

const bannerSchema = z.object({
  title: z.string().trim().max(250, "Title is too long."),
  imageUrl: z
    .string()
    .trim()
    .min(1, "Image URL is required.")
    .max(1000, "Image URL is too long."),
  linkUrl: z.string().trim().max(1000, "Link URL is too long.").optional(),
  sortOrder: z
    .number()
    .int("Sort order must be an integer.")
    .min(0, "Sort order cannot be negative."),
  isActive: z.boolean(),
});

type BannerFormValues = z.infer<typeof bannerSchema>;

const defaultValues: BannerFormValues = {
  title: "",
  imageUrl: "",
  linkUrl: "",
  sortOrder: 0,
  isActive: true,
};

function toFormValues(banner: AdminBannerDto): BannerFormValues {
  return {
    title: banner.title,
    imageUrl: banner.imageUrl,
    linkUrl: banner.linkUrl ?? "",
    sortOrder: banner.sortOrder,
    isActive: banner.isActive,
  };
}

function toRequest(values: BannerFormValues): AdminBannerUpsertRequest {
  return {
    title: values.title.trim(),
    imageUrl: values.imageUrl.trim(),
    linkUrl: values.linkUrl?.trim() ? values.linkUrl.trim() : null,
    sortOrder: values.sortOrder,
    isActive: values.isActive,
  };
}

export function BannersPage() {
  const queryClient = useQueryClient();
  const formId = useId();
  const bannersQuery = useAdminBanners();
  const [editingBanner, setEditingBanner] = useState<AdminBannerDto | null>(
    null,
  );
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [isUploading, setIsUploading] = useState(false);
  const [deleteTarget, setDeleteTarget] = useState<AdminBannerDto | null>(null);
  const [notice, setNotice] = useState<{
    type: "success" | "error";
    message: string;
  } | null>(null);

  const form = useForm<BannerFormValues>({
    resolver: zodResolver(bannerSchema),
    defaultValues,
  });

  const saveMutation = useMutation({
    mutationFn: (values: BannerFormValues) => {
      const request = toRequest(values);
      return editingBanner
        ? adminApi.updateBanner(editingBanner.id, request)
        : adminApi.createBanner(request);
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["admin-banners"] });
      setIsModalOpen(false);
      setEditingBanner(null);
      form.reset(defaultValues);
      setNotice({ type: "success", message: "Banner saved." });
    },
    onError: (error) =>
      setNotice({ type: "error", message: getApiErrorMessage(error) }),
  });

  const toggleMutation = useMutation({
    mutationFn: (banner: AdminBannerDto) =>
      adminApi.updateBanner(banner.id, {
        ...toRequest(toFormValues(banner)),
        isActive: !banner.isActive,
      }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["admin-banners"] });
      setNotice({ type: "success", message: "Banner status updated." });
    },
    onError: (error) =>
      setNotice({ type: "error", message: getApiErrorMessage(error) }),
  });

  const deleteMutation = useMutation({
    mutationFn: (banner: AdminBannerDto) => adminApi.deleteBanner(banner.id),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["admin-banners"] });
      setDeleteTarget(null);
      setNotice({ type: "success", message: "Banner deleted." });
    },
    onError: (error) =>
      setNotice({ type: "error", message: getApiErrorMessage(error) }),
  });

  function openCreateModal() {
    saveMutation.reset();
    setEditingBanner(null);
    form.reset(defaultValues);
    setIsModalOpen(true);
  }

  function openEditModal(banner: AdminBannerDto) {
    saveMutation.reset();
    setEditingBanner(banner);
    form.reset(toFormValues(banner));
    setIsModalOpen(true);
  }

  return (
    <div className="admin-page-grid">
      <AdminPageHeader
        title="Banners"
        description="Manage homepage banners and their display order."
        actions={
          <Button type="button" variant="primary" onClick={openCreateModal}>
            <Plus size={17} aria-hidden="true" />
            New banner
          </Button>
        }
      />

      {notice ? <Notice type={notice.type} title={notice.message} /> : null}
      {bannersQuery.isError ? (
        <Notice type="error" title="Banners could not be loaded">
          {getApiErrorMessage(bannersQuery.error)}
          <div className="mt-3">
            <Button
              disabled={bannersQuery.isFetching}
              onClick={() => void bannersQuery.refetch()}
            >
              Try again
            </Button>
          </div>
        </Notice>
      ) : null}

      <section className="admin-panel">
        <p className="mb-4 text-sm leading-6 text-[var(--muted)]">
          The first active banner is the hero. The next two are feature cards;
          the rest form a highlights slideshow. Change display order to move a
          banner between sections.
        </p>
        {bannersQuery.isLoading ? (
          <div className="grid gap-3">
            {[0, 1, 2].map((item) => (
              <div
                key={item}
                className="h-14 animate-pulse rounded-2xl bg-slate-100"
              />
            ))}
          </div>
        ) : bannersQuery.data?.length ? (
          <TableViewport label="Banners">
            <table className="w-full min-w-[680px] text-left text-sm">
              <thead className="text-xs uppercase tracking-wide text-slate-500">
                <tr className="border-b border-slate-100">
                  <th>Banner</th>
                  <th>Destination</th>
                  <th>Display order / section</th>
                  <th>Visibility</th>
                  <th className="py-3 pr-4">Actions</th>
                </tr>
              </thead>
              <tbody>
                {bannersQuery.data.map((banner, index) => {
                  const label =
                    banner.title.trim() || `Image-only banner ${index + 1}`;
                  const activeIndex = bannersQuery.data
                    .filter((item) => item.isActive)
                    .findIndex((item) => item.id === banner.id);
                  return (
                    <tr
                      key={banner.id}
                      className="border-b border-slate-100 last:border-0"
                    >
                      <td>
                        <div className="flex items-center gap-3">
                          <img
                            className="h-14 w-24 shrink-0 rounded-lg object-cover"
                            src={banner.imageUrl}
                            alt=""
                            loading="lazy"
                            width={96}
                            height={56}
                          />
                          <span className="max-w-xs font-medium">{label}</span>
                        </div>
                      </td>
                      <td className="max-w-[220px] truncate py-3 pr-4 text-slate-600">
                        {banner.linkUrl || "-"}
                      </td>
                      <td className="py-3 pr-4 text-slate-600">
                        {banner.sortOrder}
                        <p className="mt-1 text-xs text-[var(--muted)]">
                          {!banner.isActive
                            ? "Hidden"
                            : activeIndex === 0
                              ? "Hero"
                              : activeIndex <= 2
                                ? "Feature card"
                                : "Highlights"}
                        </p>
                      </td>
                      <td className="py-3 pr-4">
                        <div className="flex items-center gap-3">
                          <Toggle
                            label={`Visibility of ${label}`}
                            checked={banner.isActive}
                            disabled={toggleMutation.isPending}
                            onChange={() => toggleMutation.mutate(banner)}
                          />
                          <Pill tone={banner.isActive ? "green" : "slate"}>
                            {banner.isActive ? "Active" : "Inactive"}
                          </Pill>
                        </div>
                      </td>
                      <td>
                        <div className="flex gap-1">
                          <IconButton
                            label={`Edit ${label}`}
                            onClick={() => openEditModal(banner)}
                          >
                            <Pencil size={17} aria-hidden="true" />
                          </IconButton>
                          <IconButton
                            label={`Delete ${label}`}
                            variant="danger"
                            disabled={deleteMutation.isPending}
                            onClick={() => {
                              deleteMutation.reset();
                              setDeleteTarget(banner);
                            }}
                          >
                            <Trash2 size={17} aria-hidden="true" />
                          </IconButton>
                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </TableViewport>
        ) : (
          <EmptyState>No banners yet</EmptyState>
        )}
      </section>

      <Modal
        title={editingBanner ? "Edit banner" : "New banner"}
        open={isModalOpen}
        busy={saveMutation.isPending || isUploading}
        error={
          saveMutation.isError
            ? getApiErrorMessage(saveMutation.error)
            : undefined
        }
        onClose={() => setIsModalOpen(false)}
        footer={
          <>
            <Button
              type="button"
              disabled={saveMutation.isPending || isUploading}
              onClick={() => setIsModalOpen(false)}
            >
              Cancel
            </Button>
            <Button
              type="submit"
              form={formId}
              variant="primary"
              disabled={saveMutation.isPending || isUploading}
            >
              {saveMutation.isPending ? "Saving..." : "Save"}
            </Button>
          </>
        }
      >
        <form
          id={formId}
          className="grid gap-4"
          noValidate
          onSubmit={form.handleSubmit((values) => saveMutation.mutate(values))}
        >
          <Controller
            control={form.control}
            name="title"
            render={({ field, fieldState }) => (
              <Field label="Title (optional)" error={fieldState.error?.message}>
                <TextInput {...field} placeholder="Ergonomic workspace sale" />
              </Field>
            )}
          />
          <p className="-mt-2 text-xs leading-5 text-[var(--muted)]">
            Leave the title empty for image-only artwork. A supplied title
            appears below the image, without a dark overlay.
          </p>
          <Controller
            control={form.control}
            name="imageUrl"
            render={({ field, fieldState }) => (
              <ImagePickerField
                label="Image"
                value={field.value}
                folder="banners"
                error={fieldState.error?.message}
                placeholder="/demo/banners/workspace-hero.webp"
                onChange={field.onChange}
                onUploadingChange={setIsUploading}
              />
            )}
          />
          <Controller
            control={form.control}
            name="linkUrl"
            render={({ field, fieldState }) => (
              <Field label="Link URL" error={fieldState.error?.message}>
                <TextInput {...field} placeholder="/products/product-slug" />
              </Field>
            )}
          />
          <div className="grid gap-4 sm:grid-cols-2">
            <p className="text-xs leading-5 text-[var(--muted)] sm:col-span-2">Enter a product page URL to open that product when the banner is clicked. Each banner has its own destination.</p>
            <Controller
              control={form.control}
              name="sortOrder"
              render={({ field, fieldState }) => (
                <Field label="Display order" error={fieldState.error?.message}>
                  <TextInput
                    type="number"
                    min={0}
                    value={field.value}
                    onChange={(event) =>
                      field.onChange(Number(event.target.value))
                    }
                  />
                </Field>
              )}
            />
            <Controller
              control={form.control}
              name="isActive"
              render={({ field }) => (
                <Field label="Active">
                  <Toggle checked={field.value} onChange={field.onChange} />
                </Field>
              )}
            />
          </div>
        </form>
      </Modal>

      <ConfirmDialog
        open={deleteTarget !== null}
        title="Delete banner"
        message="This permanently removes the banner from Admin and the storefront homepage."
        confirmLabel="Delete"
        busy={deleteMutation.isPending}
        error={
          deleteMutation.isError
            ? getApiErrorMessage(deleteMutation.error)
            : undefined
        }
        onCancel={() => setDeleteTarget(null)}
        onConfirm={() => deleteTarget && deleteMutation.mutate(deleteTarget)}
      />
    </div>
  );
}
