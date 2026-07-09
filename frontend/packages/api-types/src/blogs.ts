import { StorefrontProductListItemDto } from "./catalog";

export interface BlogCommentDto {
  id: string;
  blogPostId: string;
  authorName: string;
  authorEmail: string;
  content: string;
  isApproved: boolean;
  createdAt: string;
}

export interface AdminBlogPostDto {
  id: string;
  title: string;
  slug: string;
  summary: string;
  content: string;
  imageUrl?: string;
  isPublished: boolean;
  publishedAt?: string;
  createdAt: string;
  updatedAt: string;
  relatedProductIds: string[];
}

export interface StorefrontBlogPostDto {
  id: string;
  title: string;
  slug: string;
  summary: string;
  content: string;
  imageUrl?: string;
  publishedAt?: string;
  relatedProducts: StorefrontProductListItemDto[];
  comments: BlogCommentDto[];
}

export interface CreateBlogPostRequest {
  title: string;
  slug: string;
  summary: string;
  content: string;
  imageUrl?: string;
  isPublished: boolean;
  relatedProductIds: string[];
}

export interface UpdateBlogPostRequest {
  title: string;
  slug: string;
  summary: string;
  content: string;
  imageUrl?: string;
  isPublished: boolean;
  relatedProductIds: string[];
}

export interface CreateCommentRequest {
  authorName: string;
  authorEmail: string;
  content: string;
}
