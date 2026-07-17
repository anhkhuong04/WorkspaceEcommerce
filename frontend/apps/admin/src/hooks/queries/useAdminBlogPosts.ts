import { useQuery } from "@tanstack/react-query";
import { adminApi } from "../../services/api/adminApi";

export function useAdminBlogPosts() {
  return useQuery({
    queryKey: ["admin-blog-posts"],
    queryFn: adminApi.getBlogPosts
  });
}

export function useAdminBlogPostComments(postId: string | undefined, enabled: boolean) {
  return useQuery({
    queryKey: ["admin-blog-comments", postId],
    queryFn: () => adminApi.getBlogPostComments(postId ?? ""),
    enabled: enabled && postId !== undefined
  });
}
