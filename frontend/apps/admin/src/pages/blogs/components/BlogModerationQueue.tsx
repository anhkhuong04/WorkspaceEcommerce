import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import type { BlogCommentModerationStatus } from "@workspace-ecommerce/api-types";
import { Link } from "react-router-dom";
import {
  Button,
  Field,
  Notice,
  SelectInput,
} from "../../../components/ui/AdminUi";
import { Pagination } from "../../../components/ui/Pagination";
import { adminApi } from "../../../services/api/adminApi";
import { getApiErrorMessage } from "../../../services/api/errors";

export function BlogModerationQueue() {
  const cache = useQueryClient();
  const [page, setPage] = useState(1);
  const [status, setStatus] = useState<BlogCommentModerationStatus | undefined>(
    0,
  );
  const query = useQuery({
    queryKey: ["admin-blog-comments", "queue", status, page],
    queryFn: () =>
      adminApi.getBlogCommentsPage({ pageNumber: page, pageSize: 20, status }),
  });
  const moderation = useMutation({
    mutationFn: ({ id, approve }: { id: string; approve: boolean }) =>
      approve
        ? adminApi.approveBlogComment(id)
        : adminApi.rejectBlogComment(id),
    onSuccess: async () => {
      if (status !== undefined && query.data?.items.length === 1 && page > 1) setPage(page - 1);
      await cache.invalidateQueries({ queryKey: ["admin-blog-comments"] });
    },
  });
  return (
    <section className="admin-panel grid gap-4">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h2 className="text-lg font-semibold">Comment moderation</h2>
          <p className="mt-1 text-sm text-[var(--muted)]">
            Review comments across all articles. Rejected comments are retained
            for audit.
          </p>
        </div>
        <Field label="Moderation status">
          <SelectInput
            value={status === undefined ? "" : String(status)}
            onChange={(event) => {
              setStatus(
                event.target.value === ""
                  ? undefined
                  : (Number(event.target.value) as BlogCommentModerationStatus),
              );
              setPage(1);
            }}
          >
            <option value="">All statuses</option>
            <option value="0">Pending</option>
            <option value="1">Approved</option>
            <option value="2">Rejected</option>
          </SelectInput>
        </Field>
      </div>
      {query.isLoading && <p role="status">Loading comments...</p>}
      {query.isError && (
        <Notice type="error" title="Comments could not be loaded">
          <Button onClick={() => void query.refetch()}>Try again</Button>
        </Notice>
      )}
      {moderation.isError && (
        <Notice type="error" title={getApiErrorMessage(moderation.error)} />
      )}
      {query.data?.items.length === 0 && (
        <p className="py-6 text-center text-sm text-[var(--muted)]">
          No comments in this queue.
        </p>
      )}
      {query.data?.items.map((comment) => (
        <article key={comment.id} className="grid gap-2 rounded-lg border p-4">
          <p className="text-sm font-medium">
            {comment.authorName}{" "}
            <span className="font-normal text-[var(--muted)]">
              · {new Date(comment.createdAt).toLocaleString()}
            </span>
          </p>
          <p className="whitespace-pre-wrap break-words text-sm">
            {comment.content}
          </p>
          <div className="flex flex-wrap gap-2">
            <Link
              className="admin-button admin-button-ghost"
              to={`/blogs?postId=${comment.blogPostId}`}
            >
              View article
            </Link>
            {comment.moderationStatus !== 1 && (
              <Button
                disabled={moderation.isPending}
                onClick={() =>
                  moderation.mutate({ id: comment.id, approve: true })
                }
              >
                Approve
              </Button>
            )}
            {comment.moderationStatus !== 2 && (
              <Button
                variant="danger"
                disabled={moderation.isPending}
                onClick={() =>
                  moderation.mutate({ id: comment.id, approve: false })
                }
              >
                Reject
              </Button>
            )}
          </div>
        </article>
      ))}
      {query.data && (
        <Pagination
          data={query.data}
          label="comments"
          busy={query.isFetching || moderation.isPending}
          onPageChange={setPage}
        />
      )}
    </section>
  );
}
