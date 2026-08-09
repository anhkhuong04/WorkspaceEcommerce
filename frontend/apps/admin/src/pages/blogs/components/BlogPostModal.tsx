import type { BlogCommentDto, BlogCommentModerationStatus, AdminBlogPostDto, AdminProductDto } from "@workspace-ecommerce/api-types";
import type { UseFormReturn } from "react-hook-form";
import { Controller } from "react-hook-form";
import { Button, Field, Modal, TextInput, Toggle } from "../../../components/ui/AdminUi";
import { ImagePickerField } from "../../../components/media/ImagePickerField";
import { formatLocalizedText } from "../../../utils/localizedText";
import type { BlogPostFormValues } from "../blogTypes";

type BlogPostModalProps = {
  open: boolean;
  editingPost: AdminBlogPostDto | null;
  activeTab: "details" | "comments";
  form: UseFormReturn<BlogPostFormValues>;
  products: AdminProductDto[];
  productsLoading: boolean;
  comments: BlogCommentDto[] | undefined;
  commentsLoading: boolean;
  savePending: boolean;
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
  products,
  productsLoading,
  comments,
  commentsLoading,
  savePending,
  moderationPending,
  onClose,
  onSave,
  onActiveTabChange,
  onApproveComment,
  onRejectComment
}: BlogPostModalProps) {
  return (
    <Modal
      title={editingPost ? `Edit: ${editingPost.title}` : "Create New Article"}
      open={open}
      onClose={onClose}
      footer={(
        <>
          <Button type="button" onClick={onClose}>Cancel</Button>
          {activeTab === "details" && (
            <Button type="button" variant="primary" disabled={savePending} onClick={form.handleSubmit(onSave)}>
              {savePending ? "Saving..." : "Save"}
            </Button>
          )}
        </>
      )}
    >
      {editingPost && (
        <div className="mb-4 flex border-b border-slate-200">
          <button
            type="button"
            onClick={() => onActiveTabChange("details")}
            className={`border-b-2 px-4 py-2 text-sm font-bold transition-all focus:outline-none ${
              activeTab === "details" ? "border-slate-900 text-slate-900" : "border-transparent text-slate-500 hover:text-slate-900"
            }`}
          >
            Article Details
          </button>
          <button
            type="button"
            onClick={() => onActiveTabChange("comments")}
            className={`border-b-2 px-4 py-2 text-sm font-bold transition-all focus:outline-none ${
              activeTab === "comments" ? "border-slate-900 text-slate-900" : "border-transparent text-slate-500 hover:text-slate-900"
            }`}
          >
            Comments Moderation
          </button>
        </div>
      )}

      {activeTab === "details" ? (
        <BlogPostDetailsForm form={form} products={products} productsLoading={productsLoading} />
      ) : (
        <BlogCommentsPanel
          comments={comments}
          commentsLoading={commentsLoading}
          moderationPending={moderationPending}
          onApproveComment={onApproveComment}
          onRejectComment={onRejectComment}
        />
      )}
    </Modal>
  );
}

function BlogPostDetailsForm({
  form,
  products,
  productsLoading
}: {
  form: UseFormReturn<BlogPostFormValues>;
  products: AdminProductDto[];
  productsLoading: boolean;
}) {
  return (
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
            <ImagePickerField label="Cover image" value={field.value ?? ""} folder="blogs" error={fieldState.error?.message} placeholder="https://images.unsplash.com/... or /images/..." onChange={field.onChange} />
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
                {productsLoading ? (
                  <div className="text-xs text-slate-400">Loading products...</div>
                ) : products.length ? (
                  <div className="grid gap-2">
                    {products.map((product) => {
                      const isChecked = field.value?.includes(product.id);
                      return (
                        <label key={product.id} className="flex cursor-pointer select-none items-center gap-2.5 text-xs font-semibold text-slate-700">
                          <input
                            type="checkbox"
                            checked={isChecked}
                            onChange={(event) => {
                              const next = event.target.checked
                                ? [...(field.value || []), product.id]
                                : (field.value || []).filter((id) => id !== product.id);
                              field.onChange(next);
                            }}
                            className="h-4.5 w-4.5 cursor-pointer rounded border-slate-300 text-slate-900 focus:ring-slate-600"
                          />
                          <span>{formatLocalizedText(product.name)}</span>
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
  );
}

function BlogCommentsPanel({
  comments,
  commentsLoading,
  moderationPending,
  onApproveComment,
  onRejectComment
}: {
  comments: BlogCommentDto[] | undefined;
  commentsLoading: boolean;
  moderationPending: boolean;
  onApproveComment: (commentId: string) => void;
  onRejectComment: (commentId: string) => void;
}) {
  if (commentsLoading) {
    return <div className="py-8 text-center text-xs text-slate-400 animate-pulse">Loading comments...</div>;
  }

  if (!comments?.length) {
    return <div className="py-8 text-center text-xs text-slate-400">No comments left on this article yet.</div>;
  }

  return (
    <div className="grid gap-4">
      <div className="flex flex-wrap gap-2 text-[10px] font-bold uppercase tracking-wide">
        {([0, 1, 2] as BlogCommentModerationStatus[]).map((status) => (
          <span key={status} className="rounded-full bg-slate-100 px-2 py-1 text-slate-600">
            {moderationLabel(status)}: {comments.filter((comment) => comment.moderationStatus === status).length}
          </span>
        ))}
      </div>
      <h3 className="mb-1 text-sm font-bold text-slate-900">Article Comments</h3>
      <div className="grid max-h-[360px] gap-3 overflow-y-auto pr-1">
        {comments.map((comment) => (
          <div key={comment.id} className="flex justify-between gap-4 rounded-2xl border border-slate-100 bg-slate-50/50 p-4">
            <div className="min-w-0">
              <div className="mb-1 flex items-center gap-2">
                <span className="text-xs font-bold text-slate-800">{comment.authorName}</span>
                <span className="text-[10px] text-slate-400">({comment.authorEmail})</span>
                <span className="text-[10px] text-slate-400">- {new Date(comment.createdAt).toLocaleDateString()}</span>
                <span className={`rounded-full px-2 py-0.5 text-[10px] font-bold ${moderationStyle(comment.moderationStatus)}`}>
                  {moderationLabel(comment.moderationStatus)}
                </span>
              </div>
              <p className="break-words whitespace-pre-wrap text-xs font-medium text-slate-600">{comment.content}</p>
            </div>
            <div className="flex h-fit gap-2">
              {comment.moderationStatus !== 1 ? <button type="button" disabled={moderationPending} onClick={() => onApproveComment(comment.id)} className="text-xs font-bold text-emerald-700 hover:text-emerald-900">Approve</button> : null}
              {comment.moderationStatus !== 2 ? <button type="button" disabled={moderationPending} onClick={() => onRejectComment(comment.id)} className="text-xs font-bold text-red-600 hover:text-red-800">Reject</button> : null}
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
  return status === 0 ? "bg-amber-100 text-amber-800" : status === 1 ? "bg-emerald-100 text-emerald-800" : "bg-red-100 text-red-800";
}
