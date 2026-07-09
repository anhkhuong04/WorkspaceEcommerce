import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import type { AdminBlogPostDto, BlogCommentDto } from "@workspace-ecommerce/api-types";
import { useState, useEffect } from "react";
import { Controller, useForm } from "react-hook-form";
import { z } from "zod";
import { AdminPageHeader } from "../../components/ui/AdminPageHeader";
import { Button, ConfirmDialog, EmptyState, Field, Modal, Notice, Pill, TextInput, Toggle } from "../../components/ui/AdminUi";
import { adminApi } from "../../services/api/adminApi";
import { getApiErrorMessage } from "../../services/api/errors";

const blogPostSchema = z.object({
  title: z.string().trim().min(1, "Title is required.").max(250, "Title is too long."),
  slug: z.string().trim().min(1, "Slug is required.").max(250, "Slug is too long."),
  summary: z.string().trim().min(1, "Summary is required.").max(1000, "Summary is too long."),
  content: z.string().min(1, "Content is required."),
  imageUrl: z.string().trim().max(1000, "Image URL is too long.").optional().or(z.literal("")),
  isPublished: z.boolean(),
  relatedProductIds: z.array(z.string())
});

type BlogPostFormValues = z.infer<typeof blogPostSchema>;

const defaultValues: BlogPostFormValues = {
  title: "",
  slug: "",
  summary: "",
  content: "",
  imageUrl: "",
  isPublished: false,
  relatedProductIds: []
};

function slugify(text: string): string {
  return text
    .toLowerCase()
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "") // remove accents
    .replace(/[^\w\s-]/g, "")
    .replace(/[\s_]+/g, "-")
    .replace(/^-+|-+$/g, "");
}

export function BlogsPage() {
  const queryClient = useQueryClient();
  const blogPostsQuery = useQuery({ queryKey: ["admin-blog-posts"], queryFn: adminApi.getBlogPosts });
  const productsQuery = useQuery({ queryKey: ["admin-products"], queryFn: adminApi.getProducts });

  const [editingPost, setEditingPost] = useState<AdminBlogPostDto | null>(null);
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [activeTab, setActiveTab] = useState<"details" | "comments">("details");
  const [deleteTarget, setDeleteTarget] = useState<AdminBlogPostDto | null>(null);
  const [notice, setNotice] = useState<{ type: "success" | "error"; message: string } | null>(null);

  const form = useForm<BlogPostFormValues>({
    resolver: zodResolver(blogPostSchema),
    defaultValues
  });

  const watchTitle = form.watch("title");

  // Auto-generate slug when creating and slug has not been manually touched
  useEffect(() => {
    if (!editingPost && watchTitle) {
      const currentSlug = form.getValues("slug");
      const generated = slugify(watchTitle);
      // Only auto-update if slug is empty or matches slugified title
      if (!currentSlug || currentSlug === slugify(watchTitle.slice(0, -1))) {
        form.setValue("slug", generated, { shouldValidate: true });
      }
    }
  }, [watchTitle, editingPost, form]);

  // Load comments for the editing post
  const commentsQuery = useQuery({
    queryKey: ["admin-blog-comments", editingPost?.id],
    queryFn: () => adminApi.getBlogPostComments(editingPost!.id),
    enabled: isModalOpen && activeTab === "comments" && !!editingPost
  });

  const saveMutation = useMutation({
    mutationFn: (values: BlogPostFormValues) => {
      const request = {
        ...values,
        imageUrl: values.imageUrl?.trim() || undefined
      };
      return editingPost
        ? adminApi.updateBlogPost(editingPost.id, request)
        : adminApi.createBlogPost(request);
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["admin-blog-posts"] });
      setIsModalOpen(false);
      setEditingPost(null);
      form.reset(defaultValues);
      setNotice({ type: "success", message: "Blog post saved successfully." });
    },
    onError: (error) => setNotice({ type: "error", message: getApiErrorMessage(error) })
  });

  const deleteMutation = useMutation({
    mutationFn: (post: AdminBlogPostDto) => adminApi.deleteBlogPost(post.id),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["admin-blog-posts"] });
      setDeleteTarget(null);
      setNotice({ type: "success", message: "Blog post deleted successfully." });
    },
    onError: (error) => setNotice({ type: "error", message: getApiErrorMessage(error) })
  });

  const deleteCommentMutation = useMutation({
    mutationFn: (commentId: string) => adminApi.deleteBlogComment(commentId),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["admin-blog-comments", editingPost?.id] });
      setNotice({ type: "success", message: "Comment deleted successfully." });
    },
    onError: (error) => setNotice({ type: "error", message: getApiErrorMessage(error) })
  });

  const togglePublishMutation = useMutation({
    mutationFn: (post: AdminBlogPostDto) => adminApi.toggleBlogPostPublish(post.id),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["admin-blog-posts"] });
      setNotice({ type: "success", message: "Publication status toggled." });
    },
    onError: (error) => setNotice({ type: "error", message: getApiErrorMessage(error) })
  });

  function openCreateModal() {
    setEditingPost(null);
    form.reset(defaultValues);
    setActiveTab("details");
    setIsModalOpen(true);
  }

  function openEditModal(post: AdminBlogPostDto) {
    setEditingPost(post);
    form.reset({
      title: post.title,
      slug: post.slug,
      summary: post.summary,
      content: post.content,
      imageUrl: post.imageUrl || "",
      isPublished: post.isPublished,
      relatedProductIds: post.relatedProductIds
    });
    setActiveTab("details");
    setIsModalOpen(true);
  }

  return (
    <div className="admin-page-grid">
      <AdminPageHeader
        title="News & Blog Posts"
        description="Manage company news, product articles, and moderate customer comments."
        actions={<Button type="button" variant="primary" onClick={openCreateModal}>New Article</Button>}
      />

      {notice ? <Notice type={notice.type} title={notice.message} /> : null}
      {blogPostsQuery.isError ? <Notice type="error" title="Blog posts could not be loaded">{getApiErrorMessage(blogPostsQuery.error)}</Notice> : null}

      <section className="rounded-3xl border border-slate-200 bg-white p-5 shadow-sm">
        {blogPostsQuery.isLoading ? (
          <div className="grid gap-3">{[0, 1, 2].map((item) => <div key={item} className="h-14 animate-pulse rounded-2xl bg-slate-100" />)}</div>
        ) : blogPostsQuery.data?.length ? (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[900px] text-left text-sm">
              <thead className="text-xs uppercase tracking-wide text-slate-500">
                <tr className="border-b border-slate-100">
                  <th className="py-3 pr-4">Cover Image</th>
                  <th className="py-3 pr-4">Title</th>
                  <th className="py-3 pr-4">Slug</th>
                  <th className="py-3 pr-4">Date</th>
                  <th className="py-3 pr-4">Status</th>
                  <th className="py-3 pr-4">Actions</th>
                </tr>
              </thead>
              <tbody>
                {blogPostsQuery.data.map((post) => (
                  <tr key={post.id} className="border-b border-slate-100 last:border-0 hover:bg-slate-50/50">
                    <td className="py-3 pr-4">
                      {post.imageUrl ? (
                        <img className="h-12 w-20 rounded-lg object-cover ring-1 ring-slate-100" src={post.imageUrl} alt={post.title} />
                      ) : (
                        <div className="grid h-12 w-20 place-items-center rounded-lg bg-slate-100 text-xs font-bold text-slate-400">No Image</div>
                      )}
                    </td>
                    <td className="py-3 pr-4 font-bold text-slate-900">{post.title}</td>
                    <td className="py-3 pr-4 font-mono text-xs text-slate-500">/news/{post.slug}</td>
                    <td className="py-3 pr-4 text-xs text-slate-500">
                      {post.publishedAt ? new Date(post.publishedAt).toLocaleDateString() : "Drafted " + new Date(post.createdAt).toLocaleDateString()}
                    </td>
                    <td className="py-3 pr-4">
                      <div className="flex items-center gap-3">
                        <Toggle checked={post.isPublished} disabled={togglePublishMutation.isPending} onChange={() => togglePublishMutation.mutate(post)} />
                        <Pill tone={post.isPublished ? "green" : "slate"}>{post.isPublished ? "Published" : "Draft"}</Pill>
                      </div>
                    </td>
                    <td className="py-3 pr-4">
                      <div className="flex gap-2">
                        <Button type="button" onClick={() => openEditModal(post)}>Edit</Button>
                        <Button type="button" variant="danger" disabled={deleteMutation.isPending} onClick={() => setDeleteTarget(post)}>Delete</Button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : <EmptyState>No blog posts created yet. Get started by clicking New Article.</EmptyState>}
      </section>

      <Modal
        title={editingPost ? `Edit: ${editingPost.title}` : "Create New Article"}
        open={isModalOpen}
        onClose={() => setIsModalOpen(false)}
        footer={(
          <>
            <Button type="button" onClick={() => setIsModalOpen(false)}>Cancel</Button>
            {activeTab === "details" && (
              <Button type="button" variant="primary" disabled={saveMutation.isPending} onClick={form.handleSubmit((values) => saveMutation.mutate(values))}>
                {saveMutation.isPending ? "Saving..." : "Save"}
              </Button>
            )}
          </>
        )}
      >
        {editingPost && (
          <div className="mb-4 flex border-b border-slate-200">
            <button
              type="button"
              onClick={() => setActiveTab("details")}
              className={`border-b-2 px-4 py-2 text-sm font-bold transition-all focus:outline-none ${
                activeTab === "details" ? "border-slate-900 text-slate-900" : "border-transparent text-slate-500 hover:text-slate-900"
              }`}
            >
              Article Details
            </button>
            <button
              type="button"
              onClick={() => setActiveTab("comments")}
              className={`border-b-2 px-4 py-2 text-sm font-bold transition-all focus:outline-none ${
                activeTab === "comments" ? "border-slate-900 text-slate-900" : "border-transparent text-slate-500 hover:text-slate-900"
              }`}
            >
              Comments Moderation
            </button>
          </div>
        )}

        {activeTab === "details" ? (
          <form className="grid gap-4" noValidate>
            <Controller
              control={form.control}
              name="title"
              render={({ field, fieldState }) => (
                <Field label="Title" error={fieldState.error?.message}>
                  <TextInput {...field} placeholder="Introduce our new product lineup" />
                </Field>
              )}
            />

            <div className="grid gap-4 sm:grid-cols-2">
              <Controller
                control={form.control}
                name="slug"
                render={({ field, fieldState }) => (
                  <Field label="URL Slug" error={fieldState.error?.message}>
                    <TextInput {...field} placeholder="introduce-new-products" />
                  </Field>
                )}
              />
              <Controller
                control={form.control}
                name="imageUrl"
                render={({ field, fieldState }) => (
                  <Field label="Cover Image URL" error={fieldState.error?.message}>
                    <TextInput {...field} placeholder="https://images.unsplash.com/... or /images/..." />
                  </Field>
                )}
              />
            </div>

            <Controller
              control={form.control}
              name="summary"
              render={({ field, fieldState }) => (
                <Field label="Summary (Short intro shown in listing)" error={fieldState.error?.message}>
                  <textarea
                    {...field}
                    className="w-full rounded-2xl border border-slate-200 px-4 py-3 text-sm focus:border-slate-400 focus:outline-none focus:ring-1 focus:ring-slate-100 min-h-[80px]"
                    placeholder="Brief description of the blog post contents..."
                  />
                </Field>
              )}
            />

            <Controller
              control={form.control}
              name="content"
              render={({ field, fieldState }) => (
                <Field label="Article Content (supports plaintext, linebreaks, HTML)" error={fieldState.error?.message}>
                  <textarea
                    {...field}
                    className="w-full rounded-2xl border border-slate-200 px-4 py-3 font-mono text-sm focus:border-slate-400 focus:outline-none focus:ring-1 focus:ring-slate-100 min-h-[220px]"
                    placeholder="Start writing the article body here..."
                  />
                </Field>
              )}
            />

            <div className="grid gap-4 sm:grid-cols-2">
              <Controller
                control={form.control}
                name="isPublished"
                render={({ field }) => (
                  <Field label="Publish Instantly">
                    <Toggle checked={field.value} onChange={field.onChange} />
                  </Field>
                )}
              />

              <Controller
                control={form.control}
                name="relatedProductIds"
                render={({ field, fieldState }) => (
                  <Field label="Related Products" error={fieldState.error?.message}>
                    <div className="max-h-[140px] overflow-y-auto rounded-2xl border border-slate-200 p-3">
                      {productsQuery.isLoading ? (
                        <div className="text-xs text-slate-400">Loading products...</div>
                      ) : productsQuery.data?.length ? (
                        <div className="grid gap-2">
                          {productsQuery.data.map((product) => {
                            const isChecked = field.value?.includes(product.id);
                            return (
                              <label key={product.id} className="flex items-center gap-2.5 text-xs font-semibold text-slate-700 cursor-pointer select-none">
                                <input
                                  type="checkbox"
                                  checked={isChecked}
                                  onChange={(e) => {
                                    const next = e.target.checked
                                      ? [...(field.value || []), product.id]
                                      : (field.value || []).filter((id) => id !== product.id);
                                    field.onChange(next);
                                  }}
                                  className="h-4.5 w-4.5 rounded border-slate-300 text-slate-900 focus:ring-slate-600 cursor-pointer"
                                />
                                <span>{product.name}</span>
                              </label>
                            );
                          })}
                        </div>
                      ) : (
                        <div className="text-xs text-slate-400">No active products available</div>
                      )}
                    </div>
                  </Field>
                )}
              />
            </div>
          </form>
        ) : (
          <div className="grid gap-4">
            <h3 className="text-sm font-bold text-slate-900 mb-1">Approved Article Comments</h3>
            {commentsQuery.isLoading ? (
              <div className="text-center py-8 text-slate-400 text-xs animate-pulse">Loading comments...</div>
            ) : commentsQuery.data?.length ? (
              <div className="grid gap-3 max-h-[360px] overflow-y-auto pr-1">
                {commentsQuery.data.map((comment) => (
                  <div key={comment.id} className="rounded-2xl border border-slate-100 bg-slate-50/50 p-4 flex justify-between gap-4">
                    <div className="min-w-0">
                      <div className="flex items-center gap-2 mb-1">
                        <span className="text-xs font-bold text-slate-800">{comment.authorName}</span>
                        <span className="text-[10px] text-slate-400">({comment.authorEmail})</span>
                        <span className="text-[10px] text-slate-400">• {new Date(comment.createdAt).toLocaleDateString()}</span>
                      </div>
                      <p className="text-xs font-medium text-slate-600 break-words whitespace-pre-wrap">{comment.content}</p>
                    </div>
                    <button
                      type="button"
                      disabled={deleteCommentMutation.isPending}
                      onClick={() => deleteCommentMutation.mutate(comment.id)}
                      className="text-xs font-bold text-red-600 hover:text-red-800 h-fit"
                    >
                      Delete
                    </button>
                  </div>
                ))}
              </div>
            ) : (
              <div className="text-center py-8 text-slate-400 text-xs">No comments left on this article yet.</div>
            )}
          </div>
        )}
      </Modal>

      <ConfirmDialog
        open={deleteTarget !== null}
        title="Delete Blog Post"
        message="This permanently deletes the article, all its related product associations, and all user comments."
        confirmLabel="Delete"
        busy={deleteMutation.isPending}
        onCancel={() => setDeleteTarget(null)}
        onConfirm={() => deleteTarget && deleteMutation.mutate(deleteTarget)}
      />
    </div>
  );
}
