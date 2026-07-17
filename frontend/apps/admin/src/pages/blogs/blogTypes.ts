import { z } from "zod";

export const blogPostSchema = z.object({
  title: z.string().trim().min(1, "Title is required.").max(250, "Title is too long."),
  slug: z.string().trim().min(1, "Slug is required.").max(250, "Slug is too long."),
  summary: z.string().trim().min(1, "Summary is required.").max(1000, "Summary is too long."),
  content: z.string().min(1, "Content is required."),
  imageUrl: z.string().trim().max(1000, "Image URL is too long.").optional().or(z.literal("")),
  isPublished: z.boolean(),
  relatedProductIds: z.array(z.string())
});

export type BlogPostFormValues = z.infer<typeof blogPostSchema>;

export const blogPostDefaultValues: BlogPostFormValues = {
  title: "",
  slug: "",
  summary: "",
  content: "",
  imageUrl: "",
  isPublished: false,
  relatedProductIds: []
};

export function slugify(text: string): string {
  return text
    .toLowerCase()
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .replace(/[^\w\s-]/g, "")
    .replace(/[\s_]+/g, "-")
    .replace(/^-+|-+$/g, "");
}
