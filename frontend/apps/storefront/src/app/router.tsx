import { createBrowserRouter } from "react-router-dom";
import { StorefrontLayout } from "../components/layout/StorefrontLayout";
import { CartPage } from "../pages/cart/CartPage";
import { CheckoutPage } from "../pages/checkout/CheckoutPage";
import { CheckoutSuccessPage } from "../pages/checkout/CheckoutSuccessPage";
import { HomePage } from "../pages/home/HomePage";
import { OrderLookupPage } from "../pages/order-lookup/OrderLookupPage";
import { ProductDetailPage } from "../pages/product-detail/ProductDetailPage";
import { ProductListPage } from "../pages/product-list/ProductListPage";

export const router = createBrowserRouter([
  {
    path: "/",
    element: <StorefrontLayout />,
    children: [
      { index: true, element: <HomePage /> },
      { path: "products", element: <ProductListPage /> },
      { path: "products/:slug", element: <ProductDetailPage /> },
      { path: "cart", element: <CartPage /> },
      { path: "checkout", element: <CheckoutPage /> },
      { path: "checkout/success", element: <CheckoutSuccessPage /> },
      { path: "orders/lookup", element: <OrderLookupPage /> }
    ]
  }
]);
