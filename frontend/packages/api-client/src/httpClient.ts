import type { ApiResponse } from "@workspace-ecommerce/api-types";

export interface ApiClientOptions {
  baseUrl: string;
  getAccessToken?: () => string | null;
  getLanguage?: () => string | null;
  onUnauthorized?: () => void;
}

export class ApiClientError extends Error {
  constructor(
    message: string,
    public readonly statusCode: number,
    public readonly errors: string[],
    public readonly traceId?: string
  ) {
    super(message);
    this.name = "ApiClientError";
  }
}

export class ApiClient {
  constructor(private readonly options: ApiClientOptions) {}

  get<T>(path: string, headers?: HeadersInit): Promise<T> {
    return this.send<T>(path, { method: "GET", headers });
  }

  async getBlob(path: string): Promise<Blob> {
    const headers = this.createHeaders();
    headers.set("Accept", "application/pdf");

    const response = await fetch(`${this.options.baseUrl}${path}`, {
      method: "GET",
      headers,
      credentials: "include"
    });

    if (!response.ok) {
      let envelope: ApiResponse<unknown> | null = null;
      try {
        envelope = (await response.json()) as ApiResponse<unknown>;
      } catch {
        // A proxy may return a non-JSON error page. Keep the public error generic.
      }

      if (response.status === 401) {
        this.options.onUnauthorized?.();
      }

      const errors = envelope?.errors?.length ? envelope.errors : ["File download failed."];
      throw new ApiClientError(errors[0], response.status, errors, envelope?.traceId);
    }

    return response.blob();
  }

  post<TResponse, TBody>(path: string, body: TBody): Promise<TResponse> {
    return this.send<TResponse>(path, {
      method: "POST",
      body: JSON.stringify(body)
    });
  }

  postForm<TResponse>(path: string, body: FormData): Promise<TResponse> {
    return this.send<TResponse>(path, {
      method: "POST",
      body
    });
  }

  put<TResponse, TBody>(path: string, body: TBody): Promise<TResponse> {
    return this.send<TResponse>(path, {
      method: "PUT",
      body: JSON.stringify(body)
    });
  }

  patch<TResponse, TBody>(path: string, body: TBody): Promise<TResponse> {
    return this.send<TResponse>(path, {
      method: "PATCH",
      body: JSON.stringify(body)
    });
  }

  delete<T>(path: string): Promise<T> {
    return this.send<T>(path, { method: "DELETE" });
  }

  private async send<T>(path: string, init: RequestInit): Promise<T> {
    const headers = this.createHeaders(init.headers);
    headers.set("Accept", "application/json");

    if (init.body && !(init.body instanceof FormData)) {
      headers.set("Content-Type", "application/json");
    }

    const response = await fetch(`${this.options.baseUrl}${path}`, {
      ...init,
      headers,
      // Customer refresh credentials live in an HttpOnly cookie. Sending
      // credentials explicitly keeps the cross-origin storefront contract
      // aligned with the API's credentialed CORS policy.
      credentials: "include"
    });

    const envelope = (await response.json()) as ApiResponse<T>;
    if (!response.ok || !envelope.success) {
      if (response.status === 401) {
        this.options.onUnauthorized?.();
      }

      const errors = envelope.errors.length > 0 ? envelope.errors : ["API request failed."];
      throw new ApiClientError(errors[0], response.status, errors, envelope.traceId);
    }

    return envelope.data as T;
  }

  private createHeaders(initialHeaders?: HeadersInit): Headers {
    const headers = new Headers(initialHeaders);
    const accessToken = this.options.getAccessToken?.();
    if (accessToken) {
      headers.set("Authorization", `Bearer ${accessToken}`);
    }

    const language = this.options.getLanguage?.();
    if (language) {
      headers.set("Accept-Language", language);
    }

    return headers;
  }
}
