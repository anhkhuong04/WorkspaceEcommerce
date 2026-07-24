import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import type { AdminCategoryDto, AdminCategoryUpsertRequest } from "@workspace-ecommerce/api-types";
import { useMemo, useState } from "react";
import { Controller, useForm } from "react-hook-form";
import { z } from "zod";
import { AdminPageHeader } from "../../components/ui/AdminPageHeader";
import { Button, ConfirmDialog, EmptyState, Field, Modal, Notice, Pill, SelectInput, TextInput, Toggle } from "../../components/ui/AdminUi";
import { useAdminCategories } from "../../hooks/queries/useAdminCategories";
import { adminApi } from "../../services/api/adminApi";
import { getApiErrorMessage } from "../../services/api/errors";
import { formatLocalizedText } from "../../utils/localizedText";

const categorySchema = z.object({
  parentId: z.string().nullable(),
  name: z.string().trim().min(1, "Name is required.").max(200, "Name is too long."),
  slug: z.string().trim().min(1, "Slug is required.").max(200, "Slug is too long.").regex(/^[a-z0-9]+(?:-[a-z0-9]+)*$/, "Slug must use lowercase letters, numbers, and hyphens."),
  sortOrder: z.number().int("Sort order must be an integer.").min(0, "Sort order cannot be negative."),
  isActive: z.boolean()
});

type CategoryFormValues = z.infer<typeof categorySchema>;

type CategoryOption = { id: string; label: string; level: number };
type CategoryRow = AdminCategoryDto & { level: number };

const defaultValues: CategoryFormValues = { parentId: null, name: "", slug: "", sortOrder: 0, isActive: true };

function flattenCategories(categories: AdminCategoryDto[], level = 0): CategoryOption[] {
  return categories.flatMap((category) => [{ id: category.id, label: formatLocalizedText(category.name), level }, ...flattenCategories(category.children, level + 1)]);
}

function flattenRows(categories: AdminCategoryDto[], level = 0): CategoryRow[] {
  return categories.flatMap((category) => [{ ...category, level }, ...flattenRows(category.children, level + 1)]);
}

function collectDescendantIds(category: AdminCategoryDto): string[] {
  return category.children.flatMap((child) => [child.id, ...collectDescendantIds(child)]);
}

function findCategory(categories: AdminCategoryDto[], id: string): AdminCategoryDto | null {
  for (const category of categories) {
    if (category.id === id) return category;
    const child = findCategory(category.children, id);
    if (child) return child;
  }
  return null;
}

function toFormValues(category: AdminCategoryDto): CategoryFormValues {
  return { parentId: category.parentId, name: formatLocalizedText(category.name, ""), slug: category.slug, sortOrder: category.sortOrder, isActive: category.isActive };
}

function toRequest(values: CategoryFormValues): AdminCategoryUpsertRequest {
  return { parentId: values.parentId, name: values.name.trim(), slug: values.slug.trim(), sortOrder: values.sortOrder, isActive: values.isActive };
}

export function CategoriesPage() {
  const queryClient = useQueryClient();
  const categoriesQuery = useAdminCategories();
  const [editingCategory, setEditingCategory] = useState<AdminCategoryDto | null>(null);
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [deleteTarget, setDeleteTarget] = useState<AdminCategoryDto | null>(null);
  const [notice, setNotice] = useState<{ type: "success" | "error"; message: string } | null>(null);

  const form = useForm<CategoryFormValues>({ resolver: zodResolver(categorySchema), defaultValues });
  const categories = useMemo(() => categoriesQuery.data ?? [], [categoriesQuery.data]);
  const rows = useMemo(() => flattenRows(categories), [categories]);
  const parentOptions = useMemo(() => flattenCategories(categories), [categories]);
  const blockedParentIds = useMemo(() => {
    if (!editingCategory) return new Set<string>();
    return new Set([editingCategory.id, ...collectDescendantIds(editingCategory)]);
  }, [editingCategory]);

  const saveMutation = useMutation({
    mutationFn: (values: CategoryFormValues) => {
      const request = toRequest(values);
      return editingCategory ? adminApi.updateCategory(editingCategory.id, request) : adminApi.createCategory(request);
    },
    onSuccess: async () => {
      await Promise.all([queryClient.invalidateQueries({ queryKey: ["admin-categories"] }), queryClient.invalidateQueries({ queryKey: ["admin-products"] })]);
      setIsModalOpen(false);
      setEditingCategory(null);
      form.reset(defaultValues);
      setNotice({ type: "success", message: "Category saved." });
    },
    onError: (error) => setNotice({ type: "error", message: getApiErrorMessage(error) })
  });

  const toggleMutation = useMutation({
    mutationFn: (category: AdminCategoryDto) => adminApi.updateCategory(category.id, { ...toRequest(toFormValues(category)), isActive: !category.isActive }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["admin-categories"] });
      setNotice({ type: "success", message: "Category status updated." });
    },
    onError: (error) => setNotice({ type: "error", message: getApiErrorMessage(error) })
  });

  const deleteMutation = useMutation({
    mutationFn: (category: AdminCategoryDto) => adminApi.deleteCategory(category.id),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ["admin-categories"] }),
        queryClient.invalidateQueries({ queryKey: ["admin-products"] })
      ]);
      setDeleteTarget(null);
      setNotice({ type: "success", message: "Category deleted." });
    },
    onError: (error) => setNotice({ type: "error", message: getApiErrorMessage(error) })
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
      <AdminPageHeader title="Categories" description="Manage category visibility, sort order, and parent-child placement." actions={<Button type="button" variant="primary" onClick={openCreateModal}>New category</Button>} />
      {notice ? <Notice type={notice.type} title={notice.message} /> : null}
      {categoriesQuery.isError ? <Notice type="error" title="Categories could not be loaded">{getApiErrorMessage(categoriesQuery.error)}</Notice> : null}

      <section className="rounded-3xl border border-slate-200 bg-white p-5 shadow-sm">
        {categoriesQuery.isLoading ? (
          <div className="grid gap-3">{[0, 1, 2].map((item) => <div key={item} className="h-14 animate-pulse rounded-2xl bg-slate-100" />)}</div>
        ) : rows.length ? (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[760px] text-left text-sm">
              <thead className="text-xs uppercase tracking-wide text-slate-500"><tr className="border-b border-slate-100"><th className="py-3 pr-4">Name</th><th className="py-3 pr-4">Slug</th><th className="py-3 pr-4">Sort</th><th className="py-3 pr-4">Status</th><th className="py-3 pr-4">Actions</th></tr></thead>
              <tbody>
                {rows.map((category) => (
                  <tr key={category.id} className="border-b border-slate-100 last:border-0">
                    <td className="py-3 pr-4 font-bold text-slate-900"><span style={{ paddingLeft: category.level * 24 }}>{category.level > 0 ? "↳ " : ""}{formatLocalizedText(category.name)}</span></td>
                    <td className="py-3 pr-4 text-slate-600">{category.slug}</td>
                    <td className="py-3 pr-4 text-slate-600">{category.sortOrder}</td>
                    <td className="py-3 pr-4"><div className="flex items-center gap-3"><Toggle checked={category.isActive} disabled={toggleMutation.isPending} onChange={() => toggleMutation.mutate(category)} /><Pill tone={category.isActive ? "green" : "slate"}>{category.isActive ? "Active" : "Inactive"}</Pill></div></td>
                    <td className="py-3 pr-4"><div className="flex gap-2"><Button type="button" onClick={() => openEditModal(category)}>Edit</Button><Button type="button" variant="danger" disabled={deleteMutation.isPending} onClick={() => setDeleteTarget(category)}>Delete</Button></div></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : <EmptyState>No categories yet</EmptyState>}
      </section>

      <Modal
        title={editingCategory ? "Edit category" : "New category"}
        open={isModalOpen}
        onClose={() => setIsModalOpen(false)}
        footer={<><Button type="button" onClick={() => setIsModalOpen(false)}>Cancel</Button><Button type="button" variant="primary" disabled={saveMutation.isPending} onClick={form.handleSubmit((values) => saveMutation.mutate(values))}>{saveMutation.isPending ? "Saving..." : "Save"}</Button></>}
      >
        <form className="grid gap-4" noValidate>
          <Controller control={form.control} name="parentId" render={({ field }) => <Field label="Parent category"><SelectInput value={field.value ?? ""} onChange={(event) => field.onChange(event.target.value || null)}><option value="">Root category</option>{parentOptions.map((option) => <option key={option.id} value={option.id} disabled={blockedParentIds.has(option.id)}>{`${"  ".repeat(option.level)}${option.label}`}</option>)}</SelectInput></Field>} />
          <Controller control={form.control} name="name" render={({ field, fieldState }) => <Field label="Name" error={fieldState.error?.message}><TextInput {...field} placeholder="Monitor arms" /></Field>} />
          <Controller control={form.control} name="slug" render={({ field, fieldState }) => <Field label="Slug" error={fieldState.error?.message}><TextInput {...field} placeholder="monitor-arms" /></Field>} />
          <div className="grid gap-4 sm:grid-cols-2">
            <Controller control={form.control} name="sortOrder" render={({ field, fieldState }) => <Field label="Sort order" error={fieldState.error?.message}><TextInput type="number" min={0} value={field.value} onChange={(event) => field.onChange(Number(event.target.value))} /></Field>} />
            <Controller control={form.control} name="isActive" render={({ field }) => <Field label="Active"><Toggle checked={field.value} onChange={field.onChange} /></Field>} />
          </div>
        </form>
      </Modal>

      <ConfirmDialog
        open={deleteTarget !== null}
        title="Delete category"
        message="This permanently removes the category. Categories with child categories or products cannot be deleted."
        confirmLabel="Delete"
        busy={deleteMutation.isPending}
        onCancel={() => setDeleteTarget(null)}
        onConfirm={() => deleteTarget && deleteMutation.mutate(deleteTarget)}
      />
    </div>
  );
}
