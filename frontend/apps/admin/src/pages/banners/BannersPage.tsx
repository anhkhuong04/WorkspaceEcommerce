import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import type { AdminBannerDto, AdminBannerUpsertRequest } from "@workspace-ecommerce/api-types";
import { useState } from "react";
import { Controller, useForm } from "react-hook-form";
import { z } from "zod";
import { AdminPageHeader } from "../../components/ui/AdminPageHeader";
import { Button, ConfirmDialog, EmptyState, Field, Modal, Notice, Pill, TextInput, Toggle } from "../../components/ui/AdminUi";
import { useAdminBanners } from "../../hooks/queries/useAdminBanners";
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
  const bannersQuery = useAdminBanners();
  const [editingBanner, setEditingBanner] = useState<AdminBannerDto | null>(null);
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [deleteTarget, setDeleteTarget] = useState<AdminBannerDto | null>(null);
  const [notice, setNotice] = useState<{ type: "success" | "error"; message: string } | null>(null);

  const form = useForm<BannerFormValues>({ resolver: zodResolver(bannerSchema), defaultValues });

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
      setNotice({ type: "success", message: "Banner saved." });
    },
    onError: (error) => setNotice({ type: "error", message: getApiErrorMessage(error) })
  });

  const toggleMutation = useMutation({
    mutationFn: (banner: AdminBannerDto) => adminApi.updateBanner(banner.id, { ...toRequest(toFormValues(banner)), isActive: !banner.isActive }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["admin-banners"] });
      setNotice({ type: "success", message: "Banner status updated." });
    },
    onError: (error) => setNotice({ type: "error", message: getApiErrorMessage(error) })
  });

  const deleteMutation = useMutation({
    mutationFn: (banner: AdminBannerDto) => adminApi.deleteBanner(banner.id),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["admin-banners"] });
      setDeleteTarget(null);
      setNotice({ type: "success", message: "Banner deleted." });
    },
    onError: (error) => setNotice({ type: "error", message: getApiErrorMessage(error) })
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
      <AdminPageHeader
        title="Banners"
        description="Create, update, activate, deactivate, and sort homepage banners."
        actions={<Button type="button" variant="primary" onClick={openCreateModal}>New banner</Button>}
      />

      {notice ? <Notice type={notice.type} title={notice.message} /> : null}
      {bannersQuery.isError ? <Notice type="error" title="Banners could not be loaded">{getApiErrorMessage(bannersQuery.error)}</Notice> : null}

      <section className="rounded-3xl border border-slate-200 bg-white p-5 shadow-sm">
        {bannersQuery.isLoading ? (
          <div className="grid gap-3">{[0, 1, 2].map((item) => <div key={item} className="h-14 animate-pulse rounded-2xl bg-slate-100" />)}</div>
        ) : bannersQuery.data?.length ? (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[900px] text-left text-sm">
              <thead className="text-xs uppercase tracking-wide text-slate-500">
                <tr className="border-b border-slate-100">
                  <th className="py-3 pr-4">Preview</th>
                  <th className="py-3 pr-4">Title</th>
                  <th className="py-3 pr-4">Image</th>
                  <th className="py-3 pr-4">Link</th>
                  <th className="py-3 pr-4">Sort</th>
                  <th className="py-3 pr-4">Status</th>
                  <th className="py-3 pr-4">Actions</th>
                </tr>
              </thead>
              <tbody>
                {bannersQuery.data.map((banner) => (
                  <tr key={banner.id} className="border-b border-slate-100 last:border-0">
                    <td className="py-3 pr-4"><img className="h-12 w-24 rounded-xl object-cover" src={banner.imageUrl} alt={banner.title} /></td>
                    <td className="py-3 pr-4 font-bold text-slate-900">{banner.title}</td>
                    <td className="max-w-[260px] truncate py-3 pr-4 text-slate-600">{banner.imageUrl}</td>
                    <td className="max-w-[220px] truncate py-3 pr-4 text-slate-600">{banner.linkUrl || "-"}</td>
                    <td className="py-3 pr-4 text-slate-600">{banner.sortOrder}</td>
                    <td className="py-3 pr-4">
                      <div className="flex items-center gap-3">
                        <Toggle checked={banner.isActive} disabled={toggleMutation.isPending} onChange={() => toggleMutation.mutate(banner)} />
                        <Pill tone={banner.isActive ? "green" : "slate"}>{banner.isActive ? "Active" : "Inactive"}</Pill>
                      </div>
                    </td>
                    <td className="py-3 pr-4"><div className="flex gap-2"><Button type="button" onClick={() => openEditModal(banner)}>Edit</Button><Button type="button" variant="danger" disabled={deleteMutation.isPending} onClick={() => setDeleteTarget(banner)}>Delete</Button></div></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : <EmptyState>No banners yet</EmptyState>}
      </section>

      <Modal
        title={editingBanner ? "Edit banner" : "New banner"}
        open={isModalOpen}
        onClose={() => setIsModalOpen(false)}
        footer={(
          <>
            <Button type="button" onClick={() => setIsModalOpen(false)}>Cancel</Button>
            <Button type="button" variant="primary" disabled={saveMutation.isPending} onClick={form.handleSubmit((values) => saveMutation.mutate(values))}>
              {saveMutation.isPending ? "Saving..." : "Save"}
            </Button>
          </>
        )}
      >
        <form className="grid gap-4" noValidate>
          <Controller control={form.control} name="title" render={({ field, fieldState }) => <Field label="Title" error={fieldState.error?.message}><TextInput {...field} placeholder="Ergonomic workspace sale" /></Field>} />
          <Controller control={form.control} name="imageUrl" render={({ field, fieldState }) => <Field label="Image URL" error={fieldState.error?.message}><TextInput {...field} placeholder="/demo/banners/workspace-hero.webp" /></Field>} />
          <Controller control={form.control} name="linkUrl" render={({ field, fieldState }) => <Field label="Link URL" error={fieldState.error?.message}><TextInput {...field} placeholder="/products" /></Field>} />
          <div className="grid gap-4 sm:grid-cols-2">
            <Controller control={form.control} name="sortOrder" render={({ field, fieldState }) => <Field label="Sort order" error={fieldState.error?.message}><TextInput type="number" min={0} value={field.value} onChange={(event) => field.onChange(Number(event.target.value))} /></Field>} />
            <Controller control={form.control} name="isActive" render={({ field }) => <Field label="Active"><Toggle checked={field.value} onChange={field.onChange} /></Field>} />
          </div>
        </form>
      </Modal>

      <ConfirmDialog
        open={deleteTarget !== null}
        title="Delete banner"
        message="This permanently removes the banner from Admin and the storefront homepage."
        confirmLabel="Delete"
        busy={deleteMutation.isPending}
        onCancel={() => setDeleteTarget(null)}
        onConfirm={() => deleteTarget && deleteMutation.mutate(deleteTarget)}
      />
    </div>
  );
}
