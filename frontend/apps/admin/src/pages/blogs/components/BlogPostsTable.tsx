import type { AdminBlogPostDto } from "@workspace-ecommerce/api-types";
import { FileText, Pencil, Trash2 } from "lucide-react";
import {
  EmptyState,
  IconButton,
  Pill,
  TableViewport,
  Toggle,
} from "../../../components/ui/AdminUi";
type Props = {
  posts: AdminBlogPostDto[];
  isLoading: boolean;
  togglePublishPending: boolean;
  deletePending: boolean;
  onEdit: (p: AdminBlogPostDto) => void;
  onDelete: (p: AdminBlogPostDto) => void;
  onTogglePublish: (p: AdminBlogPostDto) => void;
};
export function BlogPostsTable({
  posts,
  isLoading,
  togglePublishPending,
  deletePending,
  onEdit,
  onDelete,
  onTogglePublish,
}: Props) {
  if (isLoading)
    return (
      <div className="grid gap-3" role="status" aria-label="Loading articles">
        {[0, 1, 2].map((i) => (
          <div key={i} className="h-16 animate-pulse rounded-lg bg-slate-100" />
        ))}
      </div>
    );
  if (!posts.length)
    return (
      <EmptyState>
        No articles yet. Create an article to share news with your customers.
      </EmptyState>
    );
  return (
    <TableViewport label="Articles">
      <table className="w-full min-w-[650px]">
        <thead>
          <tr>
            <th>Article</th>
            <th>Date</th>
            <th>Publication</th>
            <th>
              <span className="sr-only">Actions</span>
            </th>
          </tr>
        </thead>
        <tbody>
          {posts.map((p) => (
            <tr key={p.id} className="border-b border-slate-100 last:border-0">
              <td>
                <div className="flex items-center gap-3">
                  {p.imageUrl ? (
                    <img
                      src={p.imageUrl}
                      alt=""
                      loading="lazy"
                      width={80}
                      height={48}
                      className="h-12 w-20 shrink-0 rounded-lg bg-slate-50 object-cover"
                    />
                  ) : (
                    <span className="grid h-12 w-20 shrink-0 place-items-center rounded-lg bg-slate-100 text-slate-400">
                      <FileText size={20} aria-hidden="true" />
                    </span>
                  )}
                  <p className="max-w-md font-medium">{p.title}</p>
                </div>
              </td>
              <td className="whitespace-nowrap text-xs text-[var(--muted)]">
                {p.publishedAt
                  ? new Date(p.publishedAt).toLocaleDateString()
                  : `Drafted ${new Date(p.createdAt).toLocaleDateString()}`}
              </td>
              <td>
                <div className="flex items-center gap-2">
                  <Toggle
                    label={`Publication of ${p.title}`}
                    checked={p.isPublished}
                    disabled={togglePublishPending}
                    onChange={() => onTogglePublish(p)}
                  />
                  <Pill tone={p.isPublished ? "green" : "slate"}>
                    {p.isPublished ? "Published" : "Draft"}
                  </Pill>
                </div>
              </td>
              <td>
                <div className="flex gap-1">
                  <IconButton
                    label={`Edit ${p.title}`}
                    onClick={() => onEdit(p)}
                  >
                    <Pencil size={17} aria-hidden="true" />
                  </IconButton>
                  <IconButton
                    label={`Delete ${p.title}`}
                    variant="danger"
                    disabled={deletePending}
                    onClick={() => onDelete(p)}
                  >
                    <Trash2 size={17} aria-hidden="true" />
                  </IconButton>
                </div>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </TableViewport>
  );
}
