import { ApiClientError } from "@workspace-ecommerce/api-client";

export function getApiErrorMessage(error: unknown): string {
  if (error instanceof ApiClientError) {
    return error.errors.join(" ");
  }

  if (error instanceof Error) {
    return error.message;
  }

  return "Request failed. Please try again.";
}
