import { useQuery } from "@tanstack/react-query";
import { adminApi } from "../../services/api/adminApi";
import type { AdminBlogListRequest, BlogCommentModerationStatus } from "@workspace-ecommerce/api-types";

export function useAdminBlogPosts(request: AdminBlogListRequest = {}) {
  return useQuery({
    queryKey: ["admin-blog-posts", request],
    queryFn: () => adminApi.getBlogPostsPage(request)
  });
}

export function useAdminBlogPostComments(postId: string | undefined, enabled: boolean, pageNumber = 1, status?: BlogCommentModerationStatus) {
  return useQuery({
    queryKey: ["admin-blog-comments", postId, pageNumber, status],
    queryFn: () => adminApi.getBlogCommentsPage({ postId, pageNumber, pageSize: 20, status }),
    enabled: enabled && postId !== undefined
  });
}
