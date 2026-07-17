import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import type { AdminBlogPostDto } from "@workspace-ecommerce/api-types";
import { useEffect, useState } from "react";
import { useForm, useWatch } from "react-hook-form";
import { AdminPageHeader } from "../../components/ui/AdminPageHeader";
import { Button, ConfirmDialog, Notice } from "../../components/ui/AdminUi";
import { useAdminBlogPostComments, useAdminBlogPosts } from "../../hooks/queries/useAdminBlogPosts";
import { useAdminProducts } from "../../hooks/queries/useAdminProducts";
import { adminApi } from "../../services/api/adminApi";
import { getApiErrorMessage } from "../../services/api/errors";
import { BlogPostModal } from "./components/BlogPostModal";
import { BlogPostsTable } from "./components/BlogPostsTable";
import { blogPostDefaultValues, blogPostSchema, slugify, type BlogPostFormValues } from "./blogTypes";

export function BlogsPage() {
  const queryClient = useQueryClient();
  const blogPostsQuery = useAdminBlogPosts();
  const productsQuery = useAdminProducts({ pageNumber: 1, pageSize: 100 }, "related-picker");
  const products = productsQuery.data?.items ?? [];

  const [editingPost, setEditingPost] = useState<AdminBlogPostDto | null>(null);
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [activeTab, setActiveTab] = useState<"details" | "comments">("details");
  const [deleteTarget, setDeleteTarget] = useState<AdminBlogPostDto | null>(null);
  const [notice, setNotice] = useState<{ type: "success" | "error"; message: string } | null>(null);

  const form = useForm<BlogPostFormValues>({
    resolver: zodResolver(blogPostSchema),
    defaultValues: blogPostDefaultValues
  });

  const watchTitle = useWatch({ control: form.control, name: "title" });

  useEffect(() => {
    if (!editingPost && watchTitle) {
      const currentSlug = form.getValues("slug");
      const generated = slugify(watchTitle);
      if (!currentSlug || currentSlug === slugify(watchTitle.slice(0, -1))) {
        form.setValue("slug", generated, { shouldValidate: true });
      }
    }
  }, [watchTitle, editingPost, form]);

  const commentsQuery = useAdminBlogPostComments(editingPost?.id, isModalOpen && activeTab === "comments");

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
      form.reset(blogPostDefaultValues);
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
    form.reset(blogPostDefaultValues);
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
        <BlogPostsTable
          posts={blogPostsQuery.data ?? []}
          isLoading={blogPostsQuery.isLoading}
          togglePublishPending={togglePublishMutation.isPending}
          deletePending={deleteMutation.isPending}
          onEdit={openEditModal}
          onDelete={setDeleteTarget}
          onTogglePublish={(post) => togglePublishMutation.mutate(post)}
        />
      </section>

      <BlogPostModal
        open={isModalOpen}
        editingPost={editingPost}
        activeTab={activeTab}
        form={form}
        products={products}
        productsLoading={productsQuery.isLoading}
        comments={commentsQuery.data}
        commentsLoading={commentsQuery.isLoading}
        savePending={saveMutation.isPending}
        deleteCommentPending={deleteCommentMutation.isPending}
        onClose={() => setIsModalOpen(false)}
        onSave={(values) => saveMutation.mutate(values)}
        onActiveTabChange={setActiveTab}
        onDeleteComment={(commentId) => deleteCommentMutation.mutate(commentId)}
      />

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
