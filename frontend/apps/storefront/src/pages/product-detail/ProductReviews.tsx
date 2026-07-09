import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import type { StorefrontProductDetailDto } from "@workspace-ecommerce/api-types";
import type { FormEvent } from "react";
import { useState } from "react";
import { Link } from "react-router-dom";
import { useCustomerAuth } from "../../features/customer-auth/useCustomerAuth";
import { getApiErrorMessage } from "../../services/api/errors";
import { storefrontApi } from "../../services/api/storefrontApi";

function StarIcon({ filled, onClick, className = "" }: { filled: boolean; onClick?: () => void; className?: string }) {
  return (
    <svg
      viewBox="0 0 24 24"
      onClick={onClick}
      className={`h-5 w-5 ${onClick ? "cursor-pointer transition-transform hover:scale-110" : ""} ${
        filled ? "fill-amber-400 text-amber-400" : "fill-slate-200 text-slate-200"
      } ${className}`}
      xmlns="http://www.w3.org/2000/svg"
    >
      <path d="M11.48 3.499a.562.562 0 0 1 1.04 0l2.125 5.111a.563.563 0 0 0 .475.345l5.518.442c.499.04.701.663.321.988l-4.204 3.602a.563.563 0 0 0-.182.557l1.285 5.385a.562.562 0 0 1-.84.61l-4.725-2.885a.562.562 0 0 0-.586 0L6.982 20.54a.562.562 0 0 1-.84-.61l1.285-5.386a.562.562 0 0 0-.182-.557l-4.204-3.602a.562.562 0 0 1 .321-.988l5.518-.442a.563.563 0 0 0 .475-.345L11.48 3.5z" />
    </svg>
  );
}

export function ProductReviews({ slug, product }: { slug: string; product: StorefrontProductDetailDto }) {
  const queryClient = useQueryClient();
  const { isAuthenticated } = useCustomerAuth();

  const [rating, setRating] = useState(5);
  const [comment, setComment] = useState("");
  const [formError, setFormError] = useState<string | null>(null);
  const [formSuccess, setFormSuccess] = useState(false);

  const reviewsQuery = useQuery({
    queryKey: ["storefront", "product-reviews", slug],
    queryFn: () => storefrontApi.getProductReviews(slug),
    enabled: !!slug
  });

  const submitReviewMutation = useMutation({
    mutationFn: () => storefrontApi.submitReview(slug, { rating, comment }),
    onSuccess: () => {
      setFormSuccess(true);
      setFormError(null);
      setComment("");
      setRating(5);
      queryClient.invalidateQueries({ queryKey: ["storefront", "product-reviews", slug] });
      queryClient.invalidateQueries({ queryKey: ["storefront", "product", slug] });
    },
    onError: (error) => {
      setFormError(getApiErrorMessage(error));
      setFormSuccess(false);
    }
  });

  function handleSubmit(e: FormEvent) {
    e.preventDefault();
    if (rating < 1 || rating > 5) return;
    submitReviewMutation.mutate();
  }

  const reviewsData = reviewsQuery.data;

  return (
    <section className="grid gap-8 border-t border-slate-100 pt-8 lg:grid-cols-[420px_minmax(0,1fr)]">
      <div className="min-w-0">
        <h2 className="ui-h2 text-slate-950">Customer Reviews</h2>
        
        {reviewsData ? (
          <div className="mt-6">
            <div className="flex items-end gap-3">
              <span className="text-4xl font-black text-slate-950">{reviewsData.averageRating.toFixed(1)}</span>
              <div className="pb-1.5">
                <div className="flex gap-1">
                  {[1, 2, 3, 4, 5].map((star) => (
                    <StarIcon key={star} filled={star <= Math.round(reviewsData.averageRating)} />
                  ))}
                </div>
                <span className="mt-1 block text-sm font-semibold text-slate-500">
                  Based on {reviewsData.reviewCount} {reviewsData.reviewCount === 1 ? "review" : "reviews"}
                </span>
              </div>
            </div>
          </div>
        ) : reviewsQuery.isLoading ? (
          <div className="mt-6 h-16 w-48 animate-pulse rounded bg-slate-100" />
        ) : null}

        <div className="mt-8 rounded-[var(--radius-card)] border border-slate-100 bg-[#f8f9fa] p-6">
          <h3 className="text-lg font-black text-slate-950">Write a Review</h3>
          {isAuthenticated ? (
            <form onSubmit={handleSubmit} className="mt-5 grid gap-5">
              {formError && (
                <div className="rounded-[var(--radius-control)] bg-red-50 p-4 text-sm font-semibold text-red-700">
                  {formError}
                </div>
              )}
              {formSuccess && (
                <div className="rounded-[var(--radius-control)] bg-emerald-50 p-4 text-sm font-semibold text-emerald-700">
                  Your review has been submitted successfully!
                </div>
              )}
              
              <div>
                <label className="mb-2 block text-sm font-bold text-slate-700">Your Rating</label>
                <div className="flex gap-1">
                  {[1, 2, 3, 4, 5].map((star) => (
                    <StarIcon
                      key={star}
                      filled={star <= rating}
                      onClick={() => setRating(star)}
                      className="h-8 w-8"
                    />
                  ))}
                </div>
              </div>
              
              <div>
                <label htmlFor="review-comment" className="mb-2 block text-sm font-bold text-slate-700">
                  Your Review <span className="text-slate-400 font-normal">(Optional)</span>
                </label>
                <textarea
                  id="review-comment"
                  value={comment}
                  onChange={(e) => setComment(e.target.value)}
                  rows={4}
                  className="ui-control w-full resize-none rounded-[var(--radius-control)] border border-slate-200 bg-white p-3 text-sm focus:border-slate-950 focus:ring-1 focus:ring-slate-950"
                  placeholder="What did you like or dislike?"
                  disabled={submitReviewMutation.isPending}
                />
              </div>
              
              <button
                type="submit"
                disabled={submitReviewMutation.isPending}
                className="ui-control rounded-[var(--radius-control)] bg-slate-950 px-6 py-3 font-bold text-white transition hover:bg-slate-800 disabled:opacity-50"
              >
                {submitReviewMutation.isPending ? "Submitting..." : "Submit Review"}
              </button>
            </form>
          ) : (
            <div className="mt-4">
              <p className="text-sm text-slate-600">Please log in to write a review for this product.</p>
              <Link
                to="/login"
                className="mt-4 inline-block rounded-[var(--radius-control)] bg-slate-900 px-5 py-2.5 text-sm font-bold text-white transition hover:bg-slate-800"
              >
                Log In
              </Link>
            </div>
          )}
        </div>
      </div>

      <div className="min-w-0">
        {reviewsQuery.isLoading && (
          <div className="grid gap-6">
            {[1, 2, 3].map((i) => (
              <div key={i} className="h-32 animate-pulse rounded-[var(--radius-card)] bg-slate-100" />
            ))}
          </div>
        )}

        {reviewsData && reviewsData.reviews.length === 0 && (
          <div className="rounded-[var(--radius-card)] border border-dashed border-slate-200 bg-white p-12 text-center">
            <h3 className="text-lg font-black text-slate-900">No reviews yet</h3>
            <p className="mt-2 text-sm text-slate-500">Be the first to share your experience with this product!</p>
          </div>
        )}

        {reviewsData && reviewsData.reviews.length > 0 && (
          <div className="grid gap-6">
            {reviewsData.reviews.map((review) => (
              <article key={review.id} className="rounded-[var(--radius-card)] border border-slate-100 bg-white p-6 shadow-sm">
                <div className="flex items-start justify-between gap-4">
                  <div>
                    <h4 className="font-bold text-slate-950">{review.customerName}</h4>
                    <div className="mt-1 flex items-center gap-2">
                      <div className="flex gap-0.5">
                        {[1, 2, 3, 4, 5].map((star) => (
                          <StarIcon key={star} filled={star <= review.rating} className="h-4 w-4" />
                        ))}
                      </div>
                      <time dateTime={review.createdAt} className="text-xs font-semibold text-slate-500">
                        {new Date(review.createdAt).toLocaleDateString("en-US", { year: "numeric", month: "long", day: "numeric" })}
                      </time>
                    </div>
                  </div>
                </div>
                {review.comment && (
                  <div className="mt-4 text-sm leading-relaxed text-slate-700">
                    {review.comment}
                  </div>
                )}
              </article>
            ))}
          </div>
        )}
      </div>
    </section>
  );
}
