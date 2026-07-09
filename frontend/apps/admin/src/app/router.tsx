import { createBrowserRouter } from "react-router-dom";
import { AdminLayout } from "../components/layout/AdminLayout";
import { ProtectedRoute } from "../features/auth/ProtectedRoute";
import { BannersPage } from "../pages/banners/BannersPage";
import { BlogsPage } from "../pages/blogs/BlogsPage";
import { CategoriesPage } from "../pages/categories/CategoriesPage";
import { CouponsPage } from "../pages/coupons/CouponsPage";
import { DashboardPage } from "../pages/dashboard/DashboardPage";
import { LoginPage } from "../pages/login/LoginPage";
import { OrdersPage } from "../pages/orders/OrdersPage";
import { ProductsPage } from "../pages/products/ProductsPage";
import { ReviewsPage } from "../pages/reviews/ReviewsPage";

export const router = createBrowserRouter([
  { path: "/login", element: <LoginPage /> },
  {
    path: "/",
    element: (
      <ProtectedRoute>
        <AdminLayout />
      </ProtectedRoute>
    ),
    children: [
      { index: true, element: <DashboardPage /> },
      { path: "categories", element: <CategoriesPage /> },
      { path: "products", element: <ProductsPage /> },
      { path: "coupons", element: <CouponsPage /> },
      { path: "orders", element: <OrdersPage /> },
      { path: "banners", element: <BannersPage /> },
      { path: "blogs", element: <BlogsPage /> },
      { path: "reviews", element: <ReviewsPage /> }
    ]
  }
]);
