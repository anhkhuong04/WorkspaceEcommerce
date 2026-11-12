import { ProductChecklist } from "../../../components/ui/ProductChecklist";
import { useId, useState } from "react";
import type {
  BlogCommentDto,
  BlogCommentModerationStatus,
  AdminBlogPostDto,
  PagedResult,
} from "@workspace-ecommerce/api-types";
import type { UseFormReturn } from "react-hook-form";
import { Controller } from "react-hook-form";
import {
  AdminTabs,
  Button,
  Field,
  Modal,
  Notice,
  SelectInput,
  TextArea,
  TextInput,
  Toggle,
} from "../../../components/ui/AdminUi";
import { ImagePickerField } from "../../../components/media/ImagePickerField";
import { Pagination } from "../../../components/ui/Pagination";
import type { BlogPostFormValues } from "../blogTypes";

type BlogPostModalProps = {
  open: boolean;
  editingPost: AdminBlogPostDto | null;
  activeTab: "details" | "comments";
  form: UseFormReturn<BlogPostFormValues>;
  comments: BlogCommentDto[] | undefined;
  commentsLoading: boolean;
  commentsPagination?: PagedResult<BlogCommentDto>;
  commentsStatus?: BlogCommentModerationStatus;
  commentsError?: string;
  onRetryComments: () => void;
  onCommentsPageChange: (page: number) => void;
  onCommentsStatusChange: (value: BlogCommentModerationStatus | undefined) => void;
  savePending: boolean;
  error?: string;
  moderationPending: boolean;
  onClose: () => void;
  onSave: (values: BlogPostFormValues) => void;
  onActiveTabChange: (tab: "details" | "comments") => void;
  onApproveComment: (commentId: string) => void;
  onRejectComment: (commentId: string) => void;
};

export function BlogPostModal({
  open,
  editingPost,
  activeTab,
  form,
  comments,
  commentsLoading,
  commentsPagination, commentsStatus, commentsError, onRetryComments, onCommentsPageChange, onCommentsStatusChange,
  savePending,
  error,
  moderationPending,
  onClose,
  onSave,
  onActiveTabChange,
  onApproveComment,
  onRejectComment,
}: BlogPostModalProps) {
  const formId = useId();
  const [uploading, setUploading] = useState(false);
  const busy = savePending || moderationPending || uploading;
  return (
    <Modal
      title={editingPost ? `Edit: ${editingPost.title}` : "New article"}
      open={open}
      busy={busy}
      error={error}
      onClose={onClose}
      footer={
        <>
          <Button
            type="button"
            disabled={busy}
            onClick={onClose}
          >
            Cancel
          </Button>
          {activeTab === "details" && (
            <Button
              type="submit"
              form={formId}
              variant="primary"
              disabled={busy}
            >
              {savePending ? "Saving..." : "Save"}
            </Button>
          )}
        </>
      }
    >
      {editingPost ? (
        <AdminTabs
          label="Article editor"
          value={activeTab}
          onValueChange={(value) =>
            onActiveTabChange(value as "details" | "comments")
          }
          tabs={[
            {
              value: "details",
              label: "Article details",
              content: (
                <BlogPostDetailsForm
                  form={form}
                  formId={formId}
                  onSave={values => { if (!busy) onSave(values); }}
                  onUploadingChange={setUploading}
                />
              ),
            },
            {
              value: "comments",
              label: "Comments",
              content: (
                <div className="grid gap-4">
                <Field label="Comment status"><SelectInput value={commentsStatus === undefined ? "" : String(commentsStatus)} onChange={event => onCommentsStatusChange(event.target.value === "" ? undefined : Number(event.target.value) as BlogCommentModerationStatus)}>
                  <option value="">All statuses</option><option value="0">Pending</option><option value="1">Approved</option><option value="2">Rejected</option>
                </SelectInput></Field>
                {commentsError && <Notice type="error" title={commentsError}><Button onClick={onRetryComments}>Try again</Button></Notice>}
                <p className="text-xs text-[var(--muted)]">Status counts below refer to comments on this page.</p>
                <BlogCommentsPanel
                  comments={comments}
                  commentsLoading={commentsLoading}
                  moderationPending={moderationPending}
                  onApproveComment={onApproveComment}
                  onRejectComment={onRejectComment}
                />
                {commentsPagination && <Pagination data={commentsPagination} label="article comments" busy={commentsLoading || moderationPending} onPageChange={onCommentsPageChange} />}
                </div>
              ),
            },
          ]}
        />
      ) : (
        <BlogPostDetailsForm
          form={form}
          formId={formId}
          onSave={values => { if (!busy) onSave(values); }}
          onUploadingChange={setUploading}
        />
      )}
    </Modal>
  );
}

function BlogPostDetailsForm({
  form,
  formId,
  onSave,
  onUploadingChange,
}: {
  form: UseFormReturn<BlogPostFormValues>;
  formId: string;
  onSave: (values: BlogPostFormValues) => void;
  onUploadingChange: (value: boolean) => void;
}) {
  return (
    <form
      id={formId}
      className="grid gap-4"
      noValidate
      onSubmit={form.handleSubmit(onSave)}
    >
      <Controller
        control={form.control}
        name="title"
        render={({ field, fieldState }) => (
          <Field label="Title" error={fieldState.error?.message}>
            <TextInput
              {...field}
              placeholder="Introduce our new product lineup"
            />
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
            <ImagePickerField
              label="Cover image"
              value={field.value ?? ""}
              folder="blogs"
              error={fieldState.error?.message}
              placeholder="https://images.unsplash.com/... or /images/..."
              onChange={field.onChange}
              onUploadingChange={onUploadingChange}
            />
          )}
        />
      </div>

      <Controller
        control={form.control}
        name="summary"
        render={({ field, fieldState }) => (
          <Field
            label="Summary (Short intro shown in listing)"
            error={fieldState.error?.message}
          >
            <TextArea
              {...field}
              rows={3}
              placeholder="Brief description of the blog post contents..."
            />
          </Field>
        )}
      />

      <Controller
        control={form.control}
        name="content"
        render={({ field, fieldState }) => (
          <Field
            label="Article Content (supports plaintext, linebreaks, HTML)"
            error={fieldState.error?.message}
          >
            <TextArea
              {...field}
              rows={9}
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
            <Field label="Published">
              <Toggle checked={field.value} onChange={field.onChange} />
            </Field>
          )}
        />

        <Controller
          control={form.control}
          name="relatedProductIds"
          render={({ field, fieldState }) => (
            <ProductChecklist
              label="Related products"
              selectedIds={field.value ?? []}
              onChange={field.onChange}
              error={fieldState.error?.message}
            />
          )}
        />
      </div>
    </form>
  );
}

function BlogCommentsPanel({
  comments,
  commentsLoading,
  moderationPending,
  onApproveComment,
  onRejectComment,
}: {
  comments: BlogCommentDto[] | undefined;
  commentsLoading: boolean;
  moderationPending: boolean;
  onApproveComment: (commentId: string) => void;
  onRejectComment: (commentId: string) => void;
}) {
  if (commentsLoading) {
    return (
      <div className="py-8 text-center text-xs text-slate-500 animate-pulse">
        Loading comments...
      </div>
    );
  }

  if (!comments?.length) {
    return (
      <div className="py-8 text-center text-xs text-slate-500">
        No comments left on this article yet.
      </div>
    );
  }

  return (
    <div className="grid gap-4">
      <div className="flex flex-wrap gap-2 text-xs font-medium">
        {([0, 1, 2] as BlogCommentModerationStatus[]).map((status) => (
          <span
            key={status}
            className="rounded-full bg-slate-100 px-2 py-1 text-slate-600"
          >
            {moderationLabel(status)}:{" "}
            {
              comments.filter((comment) => comment.moderationStatus === status)
                .length
            }
          </span>
        ))}
      </div>
      <h3 className="mb-1 text-sm font-medium text-slate-900">
        Article Comments
      </h3>
      <div className="grid max-h-[360px] gap-3 overflow-y-auto pr-1">
        {comments.map((comment) => (
          <div
            key={comment.id}
            className="flex min-w-0 flex-col gap-3 rounded-lg border border-slate-200 p-4"
          >
            <div className="min-w-0">
              <div className="mb-2 flex flex-wrap items-center gap-2">
                <span className="text-sm font-medium text-slate-800">
                  {comment.authorName}
                </span>
                <span className="break-all text-xs text-[var(--muted)]">
                  ({comment.authorEmail})
                </span>
                <span className="text-xs text-[var(--muted)]">
                  - {new Date(comment.createdAt).toLocaleDateString()}
                </span>
                <span
                  className={`rounded-full px-2 py-1 text-xs font-medium ${moderationStyle(comment.moderationStatus)}`}
                >
                  {moderationLabel(comment.moderationStatus)}
                </span>
              </div>
              <p className="break-words whitespace-pre-wrap text-sm leading-6 text-slate-700">
                {comment.content}
              </p>
            </div>
            <div className="flex h-fit gap-2">
              {comment.moderationStatus !== 1 ? (
                <Button
                  type="button"
                  disabled={moderationPending}
                  onClick={() => onApproveComment(comment.id)}
                  className="text-emerald-700"
                >
                  Approve
                </Button>
              ) : null}
              {comment.moderationStatus !== 2 ? (
                <Button
                  type="button"
                  disabled={moderationPending}
                  onClick={() => onRejectComment(comment.id)}
                  variant="danger"
                >
                  Reject
                </Button>
              ) : null}
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}

function moderationLabel(status: BlogCommentModerationStatus): string {
  return status === 0 ? "Pending" : status === 1 ? "Approved" : "Rejected";
}

function moderationStyle(status: BlogCommentModerationStatus): string {
  return status === 0
    ? "bg-amber-100 text-amber-800"
    : status === 1
      ? "bg-emerald-100 text-emerald-800"
      : "bg-red-100 text-red-800";
}
