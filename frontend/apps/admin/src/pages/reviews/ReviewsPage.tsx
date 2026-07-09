import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import type { AdminReviewListItemDto } from "@workspace-ecommerce/api-types";
import { useState } from "react";
import { AdminPageHeader } from "../../components/ui/AdminPageHeader";
import { Button, ConfirmDialog, EmptyState, Notice } from "../../components/ui/AdminUi";
import { adminApi } from "../../services/api/adminApi";
import { getApiErrorMessage } from "../../services/api/errors";

function StarDisplay({ rating }: { rating: number }) {
  return (
    <span className="inline-flex items-center gap-0.5" aria-label={`${rating} out of 5 stars`}>
      {[1, 2, 3, 4, 5].map((star) => (
        <svg
          key={star}
          viewBox="0 0 24 24"
          className={`h-4 w-4 ${star <= rating ? "fill-amber-400 text-amber-400" : "fill-slate-200 text-slate-200"}`}
          xmlns="http://www.w3.org/2000/svg"
        >
          <path d="M11.48 3.499a.562.562 0 0 1 1.04 0l2.125 5.111a.563.563 0 0 0 .475.345l5.518.442c.499.04.701.663.321.988l-4.204 3.602a.563.563 0 0 0-.182.557l1.285 5.385a.562.562 0 0 1-.84.61l-4.725-2.885a.562.562 0 0 0-.586 0L6.982 20.54a.562.562 0 0 1-.84-.61l1.285-5.386a.562.562 0 0 0-.182-.557l-4.204-3.602a.562.562 0 0 1 .321-.988l5.518-.442a.563.563 0 0 0 .475-.345L11.48 3.5z" />
        </svg>
      ))}
      <span className="ml-1 text-xs font-semibold text-slate-500">{rating}/5</span>
    </span>
  );
}

export function ReviewsPage() {
  const queryClient = useQueryClient();
  const [page, setPage] = useState(1);
  const [deleteTarget, setDeleteTarget] = useState<AdminReviewListItemDto | null>(null);
  const [notice, setNotice] = useState<{ type: "success" | "error"; message: string } | null>(null);

  const reviewsQuery = useQuery({
    queryKey: ["admin-reviews", page],
    queryFn: () => adminApi.getReviews(page, 20)
  });

  const deleteMutation = useMutation({
    mutationFn: (id: string) => adminApi.deleteReview(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["admin-reviews"] });
      setDeleteTarget(null);
      setNotice({ type: "success", message: "Review deleted successfully." });
      setTimeout(() => setNotice(null), 4000);
    },
    onError: (err) => {
      setNotice({ type: "error", message: getApiErrorMessage(err) });
      setTimeout(() => setNotice(null), 6000);
    }
  });

  const data = reviewsQuery.data;

  return (
    <div className="flex flex-col gap-6">
      <AdminPageHeader
        title="Reviews"
        description="Manage customer product reviews and ratings."
      />

      {notice && (
        <Notice
          type={notice.type === "success" ? "success" : "error"}
          title={notice.type === "success" ? "Success" : "Error"}
        >
          {notice.message}
        </Notice>
      )}

      {reviewsQuery.isLoading && (
        <p className="text-sm text-slate-500 animate-pulse">Loading reviews...</p>
      )}

      {reviewsQuery.isError && (
        <Notice type="error" title="Error">
          {getApiErrorMessage(reviewsQuery.error)}
        </Notice>
      )}

      {data && data.items.length === 0 && (
        <EmptyState>
          <p className="font-bold">No reviews yet</p>
          <p className="mt-1">Customer product reviews will appear here.</p>
        </EmptyState>
      )}

      {data && data.items.length > 0 && (
        <>
          <div className="overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-slate-200 bg-slate-50 text-left">
                  <th className="px-4 py-3 font-semibold text-slate-600">Product</th>
                  <th className="px-4 py-3 font-semibold text-slate-600">Customer</th>
                  <th className="px-4 py-3 font-semibold text-slate-600">Rating</th>
                  <th className="px-4 py-3 font-semibold text-slate-600">Comment</th>
                  <th className="px-4 py-3 font-semibold text-slate-600">Date</th>
                  <th className="px-4 py-3 font-semibold text-slate-600">Actions</th>
                </tr>
              </thead>
              <tbody>
                {data.items.map((review, idx) => (
                  <tr
                    key={review.id}
                    className={`border-b border-slate-100 transition hover:bg-slate-50 ${idx === data.items.length - 1 ? "border-b-0" : ""}`}
                  >
                    <td className="px-4 py-3">
                      <span className="font-semibold text-slate-800">{review.productName}</span>
                    </td>
                    <td className="px-4 py-3 text-slate-600">{review.customerName}</td>
                    <td className="px-4 py-3">
                      <StarDisplay rating={review.rating} />
                    </td>
                    <td className="max-w-xs px-4 py-3">
                      {review.comment ? (
                        <p className="line-clamp-2 text-slate-600">{review.comment}</p>
                      ) : (
                        <span className="text-slate-400 italic">No comment</span>
                      )}
                    </td>
                    <td className="whitespace-nowrap px-4 py-3 text-slate-500">
                      {new Date(review.createdAt).toLocaleDateString("vi-VN")}
                    </td>
                    <td className="px-4 py-3">
                      <Button
                        variant="danger"
                        onClick={() => setDeleteTarget(review)}
                        id={`delete-review-${review.id}`}
                      >
                        Delete
                      </Button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {/* Pagination */}
          {data.totalPages > 1 && (
            <div className="flex items-center justify-between">
              <p className="text-sm text-slate-500">
                Page {data.pageNumber} of {data.totalPages} &middot; {data.totalCount} total reviews
              </p>
              <div className="flex gap-2">
                <Button
                  variant="secondary"
                  onClick={() => setPage((p) => Math.max(1, p - 1))}
                  disabled={!data.hasPreviousPage}
                >
                  Previous
                </Button>
                <Button
                  variant="secondary"
                  onClick={() => setPage((p) => p + 1)}
                  disabled={!data.hasNextPage}
                >
                  Next
                </Button>
              </div>
            </div>
          )}
        </>
      )}

      {deleteTarget && (
        <ConfirmDialog
          open={!!deleteTarget}
          title="Delete Review"
          message={`Are you sure you want to delete this review from "${deleteTarget.customerName}" for "${deleteTarget.productName}"? This will update the product's rating.`}
          confirmLabel="Delete"
          busy={deleteMutation.isPending}
          onConfirm={() => deleteMutation.mutate(deleteTarget.id)}
          onCancel={() => setDeleteTarget(null)}
        />
      )}
    </div>
  );
}
