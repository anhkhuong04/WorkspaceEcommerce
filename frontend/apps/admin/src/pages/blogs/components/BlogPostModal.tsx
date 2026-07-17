import type { BlogCommentDto, AdminBlogPostDto, AdminProductDto } from "@workspace-ecommerce/api-types";
import type { UseFormReturn } from "react-hook-form";
import { Controller } from "react-hook-form";
import { Button, Field, Modal, TextInput, Toggle } from "../../../components/ui/AdminUi";
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
  deleteCommentPending: boolean;
  onClose: () => void;
  onSave: (values: BlogPostFormValues) => void;
  onActiveTabChange: (tab: "details" | "comments") => void;
  onDeleteComment: (commentId: string) => void;
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
  deleteCommentPending,
  onClose,
  onSave,
  onActiveTabChange,
  onDeleteComment
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
          deleteCommentPending={deleteCommentPending}
          onDeleteComment={onDeleteComment}
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
  );
}

function BlogCommentsPanel({
  comments,
  commentsLoading,
  deleteCommentPending,
  onDeleteComment
}: {
  comments: BlogCommentDto[] | undefined;
  commentsLoading: boolean;
  deleteCommentPending: boolean;
  onDeleteComment: (commentId: string) => void;
}) {
  if (commentsLoading) {
    return <div className="py-8 text-center text-xs text-slate-400 animate-pulse">Loading comments...</div>;
  }

  if (!comments?.length) {
    return <div className="py-8 text-center text-xs text-slate-400">No comments left on this article yet.</div>;
  }

  return (
    <div className="grid gap-4">
      <h3 className="mb-1 text-sm font-bold text-slate-900">Approved Article Comments</h3>
      <div className="grid max-h-[360px] gap-3 overflow-y-auto pr-1">
        {comments.map((comment) => (
          <div key={comment.id} className="flex justify-between gap-4 rounded-2xl border border-slate-100 bg-slate-50/50 p-4">
            <div className="min-w-0">
              <div className="mb-1 flex items-center gap-2">
                <span className="text-xs font-bold text-slate-800">{comment.authorName}</span>
                <span className="text-[10px] text-slate-400">({comment.authorEmail})</span>
                <span className="text-[10px] text-slate-400">- {new Date(comment.createdAt).toLocaleDateString()}</span>
              </div>
              <p className="break-words whitespace-pre-wrap text-xs font-medium text-slate-600">{comment.content}</p>
            </div>
            <button
              type="button"
              disabled={deleteCommentPending}
              onClick={() => onDeleteComment(comment.id)}
              className="h-fit text-xs font-bold text-red-600 hover:text-red-800"
            >
              Delete
            </button>
          </div>
        ))}
      </div>
    </div>
  );
}
