import { useQuery } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { storefrontApi } from "../../services/api/storefrontApi";
import { getApiErrorMessage } from "../../services/api/errors";

export function BlogListPage() {
  const { t, i18n } = useTranslation();
  const blogPostsQuery = useQuery({
    queryKey: ["storefront-blog-posts"],
    queryFn: storefrontApi.getBlogPosts
  });

  return (
    <div className="mx-auto max-w-[1400px] px-5 py-12 sm:px-8 lg:px-10 lg:py-20">
      <div className="mb-12 max-w-2xl text-left">
        <h1 className="text-4xl font-black tracking-tight text-slate-900 sm:text-5xl">
          {t("blog.title")}
        </h1>
        <p className="mt-4 text-base font-semibold text-slate-500 sm:text-lg">
          {t("blog.description")}
        </p>
      </div>

      {blogPostsQuery.isError ? (
        <div className="rounded-2xl border border-red-100 bg-red-50/50 p-6 text-sm text-red-800">
          <p className="font-bold">{t("blog.loadError")}</p>
          <p className="mt-1 text-xs text-red-600/80">{getApiErrorMessage(blogPostsQuery.error)}</p>
        </div>
      ) : null}

      {blogPostsQuery.isLoading ? (
        <div className="grid gap-8 sm:grid-cols-2 lg:grid-cols-3">
          {[0, 1, 2].map((item) => (
            <div key={item} className="group flex flex-col">
              <div className="aspect-[16/10] animate-pulse rounded-2xl bg-slate-100" />
              <div className="mt-5 h-3.5 w-1/4 animate-pulse rounded-full bg-slate-100" />
              <div className="mt-3 h-5 w-4/5 animate-pulse rounded-full bg-slate-100" />
              <div className="mt-2 h-3.5 w-full animate-pulse rounded-full bg-slate-100" />
            </div>
          ))}
        </div>
      ) : blogPostsQuery.data?.length ? (
        <div className="grid gap-8 sm:grid-cols-2 lg:grid-cols-3">
          {blogPostsQuery.data.map((post) => (
            <article
              key={post.id}
              className="group flex flex-col overflow-hidden rounded-2xl bg-white border border-slate-100 shadow-sm transition-all duration-300 hover:-translate-y-1 hover:shadow-md"
            >
              <Link to={`/news/${post.slug}`} className="aspect-[16/10] overflow-hidden bg-slate-50">
                {post.imageUrl ? (
                  <img
                    src={post.imageUrl}
                    alt={post.title}
                    className="h-full w-full object-cover transition duration-500 group-hover:scale-[1.02]"
                    loading="lazy"
                  />
                ) : (
                  <div className="grid h-full w-full place-items-center bg-gradient-to-br from-slate-100 to-slate-200 text-slate-400 font-extrabold text-lg select-none">
                    Workspace Ecom
                  </div>
                )}
              </Link>
              <div className="flex flex-1 flex-col p-6">
                <time
                  dateTime={post.publishedAt}
                  className="text-xs font-bold text-slate-400"
                >
                  {post.publishedAt ? new Date(post.publishedAt).toLocaleDateString(i18n.resolvedLanguage === "vi" ? "vi-VN" : "en-US") : ""}
                </time>
                <h2 className="mt-2.5 text-lg font-black leading-tight text-slate-900 group-hover:text-teal-700 transition">
                  <Link to={`/news/${post.slug}`}>{post.title}</Link>
                </h2>
                <p className="mt-3 text-sm leading-relaxed text-slate-600 line-clamp-3">
                  {post.summary}
                </p>
                <div className="mt-6 flex items-center justify-between border-t border-slate-50 pt-4">
                  <Link
                    to={`/news/${post.slug}`}
                    className="inline-flex items-center gap-1.5 text-xs font-black uppercase tracking-wider text-teal-700 hover:text-teal-900 transition-colors"
                  >
                    {t("blog.readArticle")}
                    <span aria-hidden="true">→</span>
                  </Link>
                </div>
              </div>
            </article>
          ))}
        </div>
      ) : (
        <div className="rounded-3xl border border-dashed border-slate-200 p-20 text-center">
          <p className="text-lg font-bold text-slate-800">{t("blog.empty")}</p>
          <p className="mt-1 text-sm font-semibold text-slate-500">{t("blog.emptyHint")}</p>
        </div>
      )}
    </div>
  );
}
