import { createBrowserRouter } from "react-router-dom";
import { AdminLayout } from "../components/layout/AdminLayout";
import { ProtectedRoute } from "../features/auth/ProtectedRoute";
import { DashboardPage } from "../pages/dashboard/DashboardPage";
import { LoginPage } from "../pages/login/LoginPage";
import { AdminErrorPage } from "../pages/AdminErrorPage";

export const router = createBrowserRouter([
  { path: "/login", element: <LoginPage /> },
  {
    path: "/",
    errorElement: <AdminErrorPage />,
    HydrateFallback: () => (
      <p role="status" className="p-6 text-sm text-[var(--muted)]">
        Loading admin console...
      </p>
    ),
    element: (
      <ProtectedRoute>
        <AdminLayout />
      </ProtectedRoute>
    ),
    children: [
      { path: "*", element: <AdminErrorPage notFound /> },
      { path: "shipping", HydrateFallback: () => <p role="status">Loading shipping...</p>, lazy: async () => ({ Component: (await import("../pages/shipping/ShippingRatesPage")).ShippingRatesPage }) },
      { index: true, element: <DashboardPage /> },
      { path: "operations", lazy: async () => ({ Component: (await import("../pages/operations/OperationsPage")).OperationsPage }) },
      {
        path: "categories",
        lazy: async () => ({
          Component: (await import("../pages/categories/CategoriesPage"))
            .CategoriesPage,
        }),
      },
      {
        path: "products",
        lazy: async () => ({
          Component: (await import("../pages/products/ProductsPage"))
            .ProductsPage,
        }),
      },
      {
        path: "coupons",
        lazy: async () => ({
          Component: (await import("../pages/coupons/CouponsPage")).CouponsPage,
        }),
      },
      {
        path: "orders",
        lazy: async () => ({
          Component: (await import("../pages/orders/OrdersPage")).OrdersPage,
        }),
      },
      {
        path: "banners",
        lazy: async () => ({
          Component: (await import("../pages/banners/BannersPage")).BannersPage,
        }),
      },
      {
        path: "blogs",
        lazy: async () => ({
          Component: (await import("../pages/blogs/BlogsPage")).BlogsPage,
        }),
      },
      {
        path: "reviews",
        lazy: async () => ({
          Component: (await import("../pages/reviews/ReviewsPage")).ReviewsPage,
        }),
      },
      {
        path: "warranties",
        lazy: async () => ({
          Component: (await import("../pages/warranties/WarrantiesPage"))
            .WarrantiesPage,
        }),
      },
    ],
  },
]);
