import { createBrowserRouter } from "react-router-dom";
import { StorefrontLayout } from "../components/layout/StorefrontLayout";
import { CustomerProtectedRoute } from "../features/customer-auth/CustomerProtectedRoute";
import {
  AccountLoyaltyPage,
  AccountOrderDetailPage,
  AccountOrdersPage,
  AccountOverviewPage,
  AccountProfilePage
} from "../pages/account/AccountPages";
import { CartPage } from "../pages/cart/CartPage";
import { CheckoutPage } from "../pages/checkout/CheckoutPage";
import { CheckoutSuccessPage } from "../pages/checkout/CheckoutSuccessPage";
import { HomePage } from "../pages/home/HomePage";
import { LoginPage } from "../pages/login/LoginPage";
import { OrderLookupPage } from "../pages/order-lookup/OrderLookupPage";
import { ProductDetailPage } from "../pages/product-detail/ProductDetailPage";
import { ProductListPage } from "../pages/product-list/ProductListPage";
import { BlogListPage } from "../pages/news/BlogListPage";
import { BlogDetailPage } from "../pages/news/BlogDetailPage";

export const router = createBrowserRouter([
  {
    path: "/",
    element: <StorefrontLayout />,
    children: [
      { index: true, element: <HomePage /> },
      { path: "products", element: <ProductListPage /> },
      { path: "products/:slug", element: <ProductDetailPage /> },
      { path: "news", element: <BlogListPage /> },
      { path: "news/:slug", element: <BlogDetailPage /> },
      { path: "cart", element: <CartPage /> },
      {
        path: "checkout",
        element: (
          <CustomerProtectedRoute>
            <CheckoutPage />
          </CustomerProtectedRoute>
        )
      },
      {
        path: "checkout/success",
        element: (
          <CustomerProtectedRoute>
            <CheckoutSuccessPage />
          </CustomerProtectedRoute>
        )
      },
      { path: "login", element: <LoginPage /> },
      { path: "orders/lookup", element: <OrderLookupPage /> },
      {
        path: "account",
        element: (
          <CustomerProtectedRoute>
            <AccountOverviewPage />
          </CustomerProtectedRoute>
        )
      },
      {
        path: "account/profile",
        element: (
          <CustomerProtectedRoute>
            <AccountProfilePage />
          </CustomerProtectedRoute>
        )
      },
      {
        path: "account/orders",
        element: (
          <CustomerProtectedRoute>
            <AccountOrdersPage />
          </CustomerProtectedRoute>
        )
      },
      {
        path: "account/loyalty",
        element: (
          <CustomerProtectedRoute>
            <AccountLoyaltyPage />
          </CustomerProtectedRoute>
        )
      },
      {
        path: "account/orders/:id",
        element: (
          <CustomerProtectedRoute>
            <AccountOrderDetailPage />
          </CustomerProtectedRoute>
        )
      }
    ]
  }
]);
