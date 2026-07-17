import type { AdminBlogPostDto } from "@workspace-ecommerce/api-types";
import { Button, EmptyState, Pill, Toggle } from "../../../components/ui/AdminUi";

type BlogPostsTableProps = {
  posts: AdminBlogPostDto[];
  isLoading: boolean;
  togglePublishPending: boolean;
  deletePending: boolean;
  onEdit: (post: AdminBlogPostDto) => void;
  onDelete: (post: AdminBlogPostDto) => void;
  onTogglePublish: (post: AdminBlogPostDto) => void;
};

export function BlogPostsTable({
  posts,
  isLoading,
  togglePublishPending,
  deletePending,
  onEdit,
  onDelete,
  onTogglePublish
}: BlogPostsTableProps) {
  if (isLoading) {
    return (
      <div className="grid gap-3">
        {[0, 1, 2].map((item) => <div key={item} className="h-14 animate-pulse rounded-2xl bg-slate-100" />)}
      </div>
    );
  }

  if (posts.length === 0) {
    return <EmptyState>No blog posts created yet. Get started by clicking New Article.</EmptyState>;
  }

  return (
    <div className="overflow-x-auto">
      <table className="w-full min-w-[900px] text-left text-sm">
        <thead className="text-xs uppercase tracking-wide text-slate-500">
          <tr className="border-b border-slate-100">
            <th className="py-3 pr-4">Cover Image</th>
            <th className="py-3 pr-4">Title</th>
            <th className="py-3 pr-4">Slug</th>
            <th className="py-3 pr-4">Date</th>
            <th className="py-3 pr-4">Status</th>
            <th className="py-3 pr-4">Actions</th>
          </tr>
        </thead>
        <tbody>
          {posts.map((post) => (
            <tr key={post.id} className="border-b border-slate-100 last:border-0 hover:bg-slate-50/50">
              <td className="py-3 pr-4">
                {post.imageUrl ? (
                  <img className="h-12 w-20 rounded-lg object-cover ring-1 ring-slate-100" src={post.imageUrl} alt={post.title} />
                ) : (
                  <div className="grid h-12 w-20 place-items-center rounded-lg bg-slate-100 text-xs font-bold text-slate-400">No Image</div>
                )}
              </td>
              <td className="py-3 pr-4 font-bold text-slate-900">{post.title}</td>
              <td className="py-3 pr-4 font-mono text-xs text-slate-500">/news/{post.slug}</td>
              <td className="py-3 pr-4 text-xs text-slate-500">
                {post.publishedAt ? new Date(post.publishedAt).toLocaleDateString() : `Drafted ${new Date(post.createdAt).toLocaleDateString()}`}
              </td>
              <td className="py-3 pr-4">
                <div className="flex items-center gap-3">
                  <Toggle checked={post.isPublished} disabled={togglePublishPending} onChange={() => onTogglePublish(post)} />
                  <Pill tone={post.isPublished ? "green" : "slate"}>{post.isPublished ? "Published" : "Draft"}</Pill>
                </div>
              </td>
              <td className="py-3 pr-4">
                <div className="flex gap-2">
                  <Button type="button" onClick={() => onEdit(post)}>Edit</Button>
                  <Button type="button" variant="danger" disabled={deletePending} onClick={() => onDelete(post)}>Delete</Button>
                </div>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
