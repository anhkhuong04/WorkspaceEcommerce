import { createStorefrontApi, ApiClient } from "@workspace-ecommerce/api-client";

const baseUrl = import.meta.env.VITE_API_BASE_URL ?? "";

export const storefrontApi = createStorefrontApi(new ApiClient({ baseUrl }));
