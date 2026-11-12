import { Star, Trash2 } from "lucide-react";
import { Pagination } from "../../components/ui/Pagination";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import type { AdminReviewListItemDto } from "@workspace-ecommerce/api-types";
import { useState } from "react";
import { AdminPageHeader } from "../../components/ui/AdminPageHeader";
import {
  ConfirmDialog,
  EmptyState,
  IconButton,
  Notice,
  TableViewport,
} from "../../components/ui/AdminUi";
import { useAdminReviews } from "../../hooks/queries/useAdminReviews";
import { adminApi } from "../../services/api/adminApi";
import { getApiErrorMessage } from "../../services/api/errors";

function StarDisplay({ rating }: { rating: number }) {
  return (
    <span
      className="inline-flex items-center gap-0.5"
      aria-label={`${rating} out of 5 stars`}
    >
      {[1, 2, 3, 4, 5].map((star) => (
        <Star
          key={star}
          size={15}
          aria-hidden="true"
          className={
            star <= rating
              ? "fill-amber-500 text-amber-600"
              : "fill-slate-100 text-slate-400"
          }
        />
      ))}
      <span className="ml-1 text-xs font-semibold text-slate-500">
        {rating}/5
      </span>
    </span>
  );
}

export function ReviewsPage() {
  const queryClient = useQueryClient();
  const [page, setPage] = useState(1);
  const [deleteTarget, setDeleteTarget] =
    useState<AdminReviewListItemDto | null>(null);
  const [notice, setNotice] = useState<{
    type: "success" | "error";
    message: string;
  } | null>(null);

  const reviewsQuery = useAdminReviews(page, 20);

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
    },
  });

  const data = reviewsQuery.data;

  return (
    <div className="admin-page-grid">
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
        <p className="text-sm text-slate-500 animate-pulse">
          Loading reviews...
        </p>
      )}

      {reviewsQuery.isError && (
        <Notice type="error" title="Error">
          {getApiErrorMessage(reviewsQuery.error)}
        </Notice>
      )}

      {data && data.items.length === 0 && (
        <EmptyState>
          <p className="font-medium">No reviews yet</p>
          <p className="mt-1">Customer product reviews will appear here.</p>
        </EmptyState>
      )}

      {data && data.items.length > 0 && (
        <>
          <section className="admin-panel">
            <TableViewport label="Customer reviews">
              <table className="w-full min-w-[780px] text-sm">
                <thead>
                  <tr className="border-b border-slate-200 bg-slate-50 text-left">
                    <th className="px-4 py-3 font-semibold text-slate-600">
                      Product
                    </th>
                    <th className="px-4 py-3 font-semibold text-slate-600">
                      Customer
                    </th>
                    <th className="px-4 py-3 font-semibold text-slate-600">
                      Rating
                    </th>
                    <th className="px-4 py-3 font-semibold text-slate-600">
                      Comment
                    </th>
                    <th className="px-4 py-3 font-semibold text-slate-600">
                      Date
                    </th>
                    <th className="px-4 py-3 font-semibold text-slate-600">
                      Actions
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {data.items.map((review, idx) => (
                    <tr
                      key={review.id}
                      className={`border-b border-slate-100 transition hover:bg-slate-50 ${idx === data.items.length - 1 ? "border-b-0" : ""}`}
                    >
                      <td className="px-4 py-3">
                        <span className="font-semibold text-slate-800">
                          {review.productName}
                        </span>
                      </td>
                      <td className="px-4 py-3 text-slate-600">
                        {review.customerName}
                      </td>
                      <td className="px-4 py-3">
                        <StarDisplay rating={review.rating} />
                      </td>
                      <td className="max-w-xs px-4 py-3">
                        {review.comment ? (
                          <div>
                            <p className="line-clamp-2 text-slate-600">
                              {review.comment}
                            </p>
                            {review.comment.length > 160 ? (
                              <details className="mt-2">
                                <summary className="cursor-pointer text-xs font-medium text-[var(--brand)]">
                                  Read full review
                                </summary>
                                <p className="mt-2 text-sm leading-6">
                                  {review.comment}
                                </p>
                              </details>
                            ) : null}
                          </div>
                        ) : (
                          <span className="text-slate-500 italic">
                            No comment
                          </span>
                        )}
                      </td>
                      <td className="whitespace-nowrap px-4 py-3 text-slate-500">
                        {new Date(review.createdAt).toLocaleDateString("vi-VN")}
                      </td>
                      <td className="px-4 py-3">
                        <IconButton
                          label={`Delete review by ${review.customerName}`}
                          variant="danger"
                          disabled={deleteMutation.isPending}
                          onClick={() => {
                            deleteMutation.reset();
                            setDeleteTarget(review);
                          }}
                          id={`delete-review-${review.id}`}
                        >
                          <Trash2 size={17} aria-hidden="true" />
                        </IconButton>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </TableViewport>
          </section>

          <Pagination
            data={data}
            label="reviews"
            busy={reviewsQuery.isFetching}
            onPageChange={setPage}
          />
        </>
      )}

      {deleteTarget && (
        <ConfirmDialog
          open={!!deleteTarget}
          title="Delete Review"
          message={`Are you sure you want to delete this review from "${deleteTarget.customerName}" for "${deleteTarget.productName}"? This will update the product's rating.`}
          confirmLabel="Delete"
          busy={deleteMutation.isPending}
          error={
            deleteMutation.isError
              ? getApiErrorMessage(deleteMutation.error)
              : undefined
          }
          onConfirm={() => deleteMutation.mutate(deleteTarget.id)}
          onCancel={() => setDeleteTarget(null)}
        />
      )}
    </div>
  );
}
