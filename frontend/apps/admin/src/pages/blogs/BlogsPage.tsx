import { zodResolver } from "@hookform/resolvers/zod";
import { Plus } from "lucide-react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import type { AdminBlogPostDto, AdminBlogListRequest, BlogCommentModerationStatus } from "@workspace-ecommerce/api-types";
import { useEffect, useMemo, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { Pagination } from "../../components/ui/Pagination";
import { BlogModerationQueue } from "./components/BlogModerationQueue";
import { useForm, useWatch } from "react-hook-form";
import { AdminPageHeader } from "../../components/ui/AdminPageHeader";
import { Button, ConfirmDialog, Field, Notice, SelectInput, TextInput } from "../../components/ui/AdminUi";
import {
  useAdminBlogPostComments,
  useAdminBlogPosts,
} from "../../hooks/queries/useAdminBlogPosts";
import { adminApi } from "../../services/api/adminApi";
import { getApiErrorMessage } from "../../services/api/errors";
import { BlogPostModal } from "./components/BlogPostModal";
import { BlogPostsTable } from "./components/BlogPostsTable";
import {
  blogPostDefaultValues,
  blogPostSchema,
  slugify,
  type BlogPostFormValues,
} from "./blogTypes";

export function BlogsPage() {
  const queryClient = useQueryClient();
  const [params, setParams] = useSearchParams();
  const request = useMemo<AdminBlogListRequest>(() => ({ pageNumber: Math.max(1, Number(params.get("page")) || 1), pageSize: 20,
    search: params.get("search") || undefined, postId: params.get("postId") || undefined,
    isPublished: params.has("published") ? params.get("published") === "true" : undefined }), [params]);
  const blogPostsQuery = useAdminBlogPosts(request);
  const [commentsPage, setCommentsPage] = useState(1);
  const [commentsStatus, setCommentsStatus] = useState<BlogCommentModerationStatus | undefined>();


  const [editingPost, setEditingPost] = useState<AdminBlogPostDto | null>(null);
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [activeTab, setActiveTab] = useState<"details" | "comments">("details");
  const [deleteTarget, setDeleteTarget] = useState<AdminBlogPostDto | null>(
    null,
  );
  const [notice, setNotice] = useState<{
    type: "success" | "error";
    message: string;
  } | null>(null);

  const form = useForm<BlogPostFormValues>({
    resolver: zodResolver(blogPostSchema),
    defaultValues: blogPostDefaultValues,
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

  const commentsQuery = useAdminBlogPostComments(
    editingPost?.id,
    isModalOpen && activeTab === "comments",
    commentsPage, commentsStatus,
  );

  const saveMutation = useMutation({
    mutationFn: (values: BlogPostFormValues) => {
      const request = {
        ...values,
        imageUrl: values.imageUrl?.trim() || undefined,
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
    onError: (error) =>
      setNotice({ type: "error", message: getApiErrorMessage(error) }),
  });

  const deleteMutation = useMutation({
    mutationFn: (post: AdminBlogPostDto) => adminApi.deleteBlogPost(post.id),
    onSuccess: async () => {
      if (blogPostsQuery.data?.items.length === 1 && (request.pageNumber ?? 1) > 1) {
        const next = new URLSearchParams(params); next.set("page", String(request.pageNumber! - 1)); setParams(next);
      }
      await queryClient.invalidateQueries({ queryKey: ["admin-blog-posts"] });
      setDeleteTarget(null);
      setNotice({
        type: "success",
        message: "Blog post deleted successfully.",
      });
    },
    onError: (error) =>
      setNotice({ type: "error", message: getApiErrorMessage(error) }),
  });

  const approveCommentMutation = useMutation({
    mutationFn: (commentId: string) => adminApi.approveBlogComment(commentId),
    onSuccess: async () => {
      if (commentsStatus !== undefined && commentsQuery.data?.items.length === 1 && commentsPage > 1) setCommentsPage(commentsPage - 1);
      await queryClient.invalidateQueries({
        queryKey: ["admin-blog-comments"],
      });
      setNotice({ type: "success", message: "Comment approved successfully." });
    },
    onError: (error) =>
      setNotice({ type: "error", message: getApiErrorMessage(error) }),
  });

  const rejectCommentMutation = useMutation({
    mutationFn: (commentId: string) => adminApi.rejectBlogComment(commentId),
    onSuccess: async () => {
      if (commentsStatus !== undefined && commentsQuery.data?.items.length === 1 && commentsPage > 1) setCommentsPage(commentsPage - 1);
      await queryClient.invalidateQueries({
        queryKey: ["admin-blog-comments"],
      });
      setNotice({
        type: "success",
        message: "Comment rejected and retained for audit.",
      });
    },
    onError: (error) =>
      setNotice({ type: "error", message: getApiErrorMessage(error) }),
  });

  const togglePublishMutation = useMutation({
    mutationFn: (post: AdminBlogPostDto) =>
      adminApi.toggleBlogPostPublish(post.id),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["admin-blog-posts"] });
      setNotice({ type: "success", message: "Publication status toggled." });
    },
    onError: (error) =>
      setNotice({ type: "error", message: getApiErrorMessage(error) }),
  });

  function openCreateModal() {
    saveMutation.reset();
    setEditingPost(null);
    form.reset(blogPostDefaultValues);
    setActiveTab("details");
    setIsModalOpen(true);
  }

  function openEditModal(post: AdminBlogPostDto) {
    setCommentsPage(1); setCommentsStatus(undefined);
    saveMutation.reset();
    setEditingPost(post);
    form.reset({
      title: post.title,
      slug: post.slug,
      summary: post.summary,
      content: post.content,
      imageUrl: post.imageUrl || "",
      isPublished: post.isPublished,
      relatedProductIds: post.relatedProductIds,
    });
    setActiveTab("details");
    setIsModalOpen(true);
  }

  return (
    <div className="admin-page-grid">
      <AdminPageHeader
        title="Articles"
        description="Manage company news, product articles, and moderate customer comments."
        actions={
          <Button type="button" variant="primary" onClick={openCreateModal}>
            <Plus size={17} aria-hidden="true" />
            New article
          </Button>
        }
      />

      {notice ? <Notice type={notice.type} title={notice.message} /> : null}
      {blogPostsQuery.isError ? (
        <Notice type="error" title="Blog posts could not be loaded">
          {getApiErrorMessage(blogPostsQuery.error)}
          <div className="mt-3">
            <Button
              disabled={blogPostsQuery.isFetching}
              onClick={() => void blogPostsQuery.refetch()}
            >
              Try again
            </Button>
          </div>
        </Notice>
      ) : null}

      <form key={params.toString()} className="admin-panel flex flex-wrap items-end gap-3" onSubmit={event => {
        event.preventDefault(); const data = new FormData(event.currentTarget); const next = new URLSearchParams();
        if (data.get("search")) next.set("search", String(data.get("search"))); if (data.get("published")) next.set("published", String(data.get("published"))); setParams(next);
      }}><Field label="Search articles"><TextInput type="search" name="search" maxLength={150} defaultValue={request.search ?? ""} placeholder="Title or slug" /></Field>
        <Field label="Publication"><SelectInput name="published" defaultValue={request.isPublished === undefined ? "" : String(request.isPublished)}><option value="">All articles</option><option value="true">Published</option><option value="false">Draft</option></SelectInput></Field>
        <Button type="submit">Search</Button><Button onClick={() => setParams(new URLSearchParams())}>Reset</Button></form>
      <section className="admin-panel">
        <BlogPostsTable
          posts={blogPostsQuery.data?.items ?? []}
          isLoading={blogPostsQuery.isLoading}
          togglePublishPending={togglePublishMutation.isPending}
          deletePending={deleteMutation.isPending}
          onEdit={openEditModal}
          onDelete={(post) => {
            deleteMutation.reset();
            setDeleteTarget(post);
          }}
          onTogglePublish={(post) => togglePublishMutation.mutate(post)}
        />
        {blogPostsQuery.data && <Pagination data={blogPostsQuery.data} label="articles" busy={blogPostsQuery.isFetching} onPageChange={page => { const next = new URLSearchParams(params); next.set("page", String(page)); setParams(next); }} />}
      </section>
      <BlogModerationQueue />

      <BlogPostModal
        open={isModalOpen}
        editingPost={editingPost}
        activeTab={activeTab}
        form={form}
        comments={commentsQuery.data?.items}
        commentsPagination={commentsQuery.data}
        onCommentsPageChange={setCommentsPage}
        commentsStatus={commentsStatus}
        onCommentsStatusChange={value => { setCommentsStatus(value); setCommentsPage(1); }}
        commentsError={commentsQuery.isError ? getApiErrorMessage(commentsQuery.error) : undefined}
        onRetryComments={() => void commentsQuery.refetch()}
        commentsLoading={commentsQuery.isLoading}
        savePending={saveMutation.isPending}
        error={
          saveMutation.isError
            ? getApiErrorMessage(saveMutation.error)
            : undefined
        }
        moderationPending={
          approveCommentMutation.isPending || rejectCommentMutation.isPending
        }
        onClose={() => setIsModalOpen(false)}
        onSave={(values) => saveMutation.mutate(values)}
        onActiveTabChange={setActiveTab}
        onApproveComment={(commentId) =>
          approveCommentMutation.mutate(commentId)
        }
        onRejectComment={(commentId) => rejectCommentMutation.mutate(commentId)}
      />

      <ConfirmDialog
        open={deleteTarget !== null}
        title="Delete Blog Post"
        message="This permanently deletes the article, all its related product associations, and all user comments."
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
