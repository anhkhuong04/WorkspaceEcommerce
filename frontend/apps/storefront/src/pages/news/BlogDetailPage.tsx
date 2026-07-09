import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useParams, Link } from "react-router-dom";
import { useForm, Controller } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { useState } from "react";
import { storefrontApi } from "../../services/api/storefrontApi";
import { getApiErrorMessage } from "../../services/api/errors";
import { ProductCard } from "../../components/ui/ProductCard";

const commentSchema = z.object({
  authorName: z.string().trim().min(1, "Name is required.").max(100, "Name is too long."),
  authorEmail: z.string().trim().min(1, "Email is required.").email("Invalid email address.").max(150, "Email is too long."),
  content: z.string().trim().min(1, "Comment content is required.").max(2000, "Comment is too long.")
});

type CommentFormValues = z.infer<typeof commentSchema>;

const defaultValues: CommentFormValues = {
  authorName: "",
  authorEmail: "",
  content: ""
};

export function BlogDetailPage() {
  const { slug } = useParams<{ slug: string }>();
  const queryClient = useQueryClient();
  const [successMessage, setSuccessMessage] = useState<string | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const blogPostQuery = useQuery({
    queryKey: ["storefront-blog-detail", slug],
    queryFn: () => storefrontApi.getBlogPost(slug || ""),
    enabled: !!slug
  });

  const form = useForm<CommentFormValues>({
    resolver: zodResolver(commentSchema),
    defaultValues
  });

  const commentMutation = useMutation({
    mutationFn: (values: CommentFormValues) =>
      storefrontApi.submitBlogComment(slug || "", values),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["storefront-blog-detail", slug] });
      form.reset(defaultValues);
      setSuccessMessage("Your comment has been posted successfully.");
      setErrorMessage(null);
      setTimeout(() => setSuccessMessage(null), 5000);
    },
    onError: (error) => {
      setErrorMessage(getApiErrorMessage(error));
      setSuccessMessage(null);
    }
  });

  if (blogPostQuery.isLoading) {
    return (
      <div className="mx-auto max-w-[900px] px-5 py-20 text-center">
        <div className="mx-auto h-8 w-8 animate-spin rounded-full border-4 border-teal-700 border-t-transparent" />
        <p className="mt-4 text-sm font-semibold text-slate-400">Loading article...</p>
      </div>
    );
  }

  if (blogPostQuery.isError || !blogPostQuery.data) {
    return (
      <div className="mx-auto max-w-[900px] px-5 py-20 text-center">
        <h1 className="text-2xl font-black text-slate-900">Article not found</h1>
        <p className="mt-2 text-slate-500 font-semibold">The article you are looking for does not exist or has been unpublished.</p>
        <Link to="/news" className="mt-6 inline-flex h-11 items-center justify-center rounded-full bg-teal-700 px-6 text-sm font-bold text-white hover:bg-teal-800 transition">
          Back to News
        </Link>
      </div>
    );
  }

  const post = blogPostQuery.data;

  return (
    <div className="bg-white">
      {/* Article Header */}
      <header className="mx-auto max-w-[900px] px-5 pt-12 sm:px-8 lg:px-10 lg:pt-20 text-center">
        <Link
          to="/news"
          className="inline-flex items-center gap-1 text-xs font-bold uppercase tracking-wider text-teal-700 hover:text-teal-900 transition"
        >
          ← Back to News
        </Link>
        <time className="block mt-4 text-xs font-bold text-slate-400">
          {post.publishedAt ? new Date(post.publishedAt).toLocaleDateString() : ""}
        </time>
        <h1 className="mt-4 text-3xl font-black leading-tight text-slate-900 sm:text-4xl md:text-5xl tracking-tight max-w-3xl mx-auto">
          {post.title}
        </h1>
        <p className="mt-6 text-base sm:text-lg font-medium text-slate-500 max-w-2xl mx-auto leading-relaxed border-l-4 border-teal-700 pl-4 text-left">
          {post.summary}
        </p>
      </header>

      {/* Cover Image */}
      {post.imageUrl && (
        <div className="mx-auto max-w-[1200px] px-5 my-10 sm:px-8 lg:px-10">
          <div className="aspect-[21/9] w-full overflow-hidden rounded-3xl bg-slate-50 shadow-sm">
            <img
              src={post.imageUrl}
              alt={post.title}
              className="h-full w-full object-cover"
            />
          </div>
        </div>
      )}

      {/* Article Body */}
      <main className="mx-auto max-w-[760px] px-5 py-6 sm:px-8">
        <article className="prose prose-slate max-w-none">
          {post.content.split("\n").map((paragraph, index) => {
            const trimmed = paragraph.trim();
            if (!trimmed) return <div key={index} className="h-4" />;
            return (
              <p key={index} className="text-slate-700 leading-relaxed text-base font-medium mb-6 whitespace-pre-wrap">
                {trimmed}
              </p>
            );
          })}
        </article>
      </main>

      {/* Related Products */}
      {post.relatedProducts && post.relatedProducts.length > 0 && (
        <section className="border-t border-slate-100 bg-slate-50/50 py-16 lg:py-24">
          <div className="mx-auto max-w-[1400px] px-5 sm:px-8 lg:px-10">
            <div className="mb-10 text-left">
              <h2 className="text-2xl font-black text-slate-950 sm:text-3xl tracking-tight">
                Featured Products in this Article
              </h2>
              <p className="mt-2 text-sm font-semibold text-slate-500">
                Explore the workspace furniture and configurations mentioned above.
              </p>
            </div>
            <div className="grid gap-6 sm:grid-cols-2 lg:grid-cols-4">
              {post.relatedProducts.map((product) => (
                <ProductCard key={product.id} product={product} />
              ))}
            </div>
          </div>
        </section>
      )}

      {/* Comments Section */}
      <section className="mx-auto max-w-[760px] px-5 py-16 sm:px-8 border-t border-slate-100">
        <h2 className="text-2xl font-black text-slate-950 tracking-tight mb-8">
          Discussion ({post.comments?.length || 0})
        </h2>

        {/* Comment Form */}
        <form
          onSubmit={form.handleSubmit((values) => commentMutation.mutate(values))}
          className="mb-12 rounded-3xl border border-slate-100 bg-slate-50/50 p-6 sm:p-8"
          noValidate
        >
          <h3 className="text-sm font-bold text-slate-900 mb-6">Leave a comment</h3>

          {successMessage && (
            <div className="mb-6 rounded-2xl bg-green-50 border border-green-100 p-4 text-xs font-bold text-green-800">
              {successMessage}
            </div>
          )}

          {errorMessage && (
            <div className="mb-6 rounded-2xl bg-red-50 border border-red-100 p-4 text-xs font-bold text-red-800">
              {errorMessage}
            </div>
          )}

          <div className="grid gap-4 sm:grid-cols-2">
            <Controller
              control={form.control}
              name="authorName"
              render={({ field, fieldState }) => (
                <div>
                  <label htmlFor="authorName" className="block text-xs font-bold text-slate-500 mb-2">Name</label>
                  <input
                    {...field}
                    id="authorName"
                    type="text"
                    className={`w-full rounded-2xl border bg-white px-4 py-3 text-sm focus:border-teal-600 focus:outline-none focus:ring-1 focus:ring-teal-600 ${
                      fieldState.error ? "border-red-300 focus:border-red-500 focus:ring-red-500" : "border-slate-200"
                    }`}
                    placeholder="John Doe"
                  />
                  {fieldState.error && <p className="mt-1 text-[11px] font-bold text-red-600">{fieldState.error.message}</p>}
                </div>
              )}
            />

            <Controller
              control={form.control}
              name="authorEmail"
              render={({ field, fieldState }) => (
                <div>
                  <label htmlFor="authorEmail" className="block text-xs font-bold text-slate-500 mb-2">Email (will not be published)</label>
                  <input
                    {...field}
                    id="authorEmail"
                    type="email"
                    className={`w-full rounded-2xl border bg-white px-4 py-3 text-sm focus:border-teal-600 focus:outline-none focus:ring-1 focus:ring-teal-600 ${
                      fieldState.error ? "border-red-300 focus:border-red-500 focus:ring-red-500" : "border-slate-200"
                    }`}
                    placeholder="john@example.com"
                  />
                  {fieldState.error && <p className="mt-1 text-[11px] font-bold text-red-600">{fieldState.error.message}</p>}
                </div>
              )}
            />
          </div>

          <div className="mt-4">
            <Controller
              control={form.control}
              name="content"
              render={({ field, fieldState }) => (
                <div>
                  <label htmlFor="content" className="block text-xs font-bold text-slate-500 mb-2">Comment</label>
                  <textarea
                    {...field}
                    id="content"
                    rows={4}
                    className={`w-full rounded-2xl border bg-white px-4 py-3 text-sm focus:border-teal-600 focus:outline-none focus:ring-1 focus:ring-teal-600 ${
                      fieldState.error ? "border-red-300 focus:border-red-500 focus:ring-red-500" : "border-slate-200"
                    }`}
                    placeholder="What are your thoughts on this article?"
                  />
                  {fieldState.error && <p className="mt-1 text-[11px] font-bold text-red-600">{fieldState.error.message}</p>}
                </div>
              )}
            />
          </div>

          <div className="mt-6 flex justify-end">
            <button
              type="submit"
              disabled={commentMutation.isPending}
              className="inline-flex h-11 items-center justify-center rounded-full bg-teal-700 px-6 text-sm font-bold text-white hover:bg-teal-800 transition disabled:opacity-50"
            >
              {commentMutation.isPending ? "Posting..." : "Post Comment"}
            </button>
          </div>
        </form>

        {/* Comments Feed */}
        {post.comments && post.comments.length > 0 ? (
          <div className="grid gap-6">
            {post.comments.map((comment) => (
              <div key={comment.id} className="flex gap-4 items-start border-b border-slate-50 pb-6 last:border-0">
                <div className="grid h-10 w-10 place-items-center rounded-full bg-teal-50 text-sm font-extrabold text-teal-800 uppercase shrink-0">
                  {comment.authorName[0]}
                </div>
                <div className="min-w-0">
                  <div className="flex items-baseline gap-2">
                    <span className="text-sm font-bold text-slate-900">{comment.authorName}</span>
                    <span className="text-[10px] text-slate-400">• {new Date(comment.createdAt).toLocaleDateString()}</span>
                  </div>
                  <p className="mt-1.5 text-sm leading-relaxed text-slate-600 whitespace-pre-wrap font-medium break-words">
                    {comment.content}
                  </p>
                </div>
              </div>
            ))}
          </div>
        ) : (
          <div className="py-8 text-center text-slate-400 text-sm font-semibold">
            No comments yet. Start the conversation!
          </div>
        )}
      </section>
    </div>
  );
}
