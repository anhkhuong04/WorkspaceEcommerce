import type { ApiResponse } from "@workspace-ecommerce/api-types";

export interface ApiClientOptions {
  baseUrl: string;
  getAccessToken?: () => string | null;
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

  get<T>(path: string): Promise<T> {
    return this.send<T>(path, { method: "GET" });
  }

  post<TResponse, TBody>(path: string, body: TBody): Promise<TResponse> {
    return this.send<TResponse>(path, {
      method: "POST",
      body: JSON.stringify(body)
    });
  }

  put<TResponse, TBody>(path: string, body: TBody): Promise<TResponse> {
    return this.send<TResponse>(path, {
      method: "PUT",
      body: JSON.stringify(body)
    });
  }

  delete<T>(path: string): Promise<T> {
    return this.send<T>(path, { method: "DELETE" });
  }

  private async send<T>(path: string, init: RequestInit): Promise<T> {
    const headers = new Headers(init.headers);
    headers.set("Accept", "application/json");

    if (init.body) {
      headers.set("Content-Type", "application/json");
    }

    const accessToken = this.options.getAccessToken?.();
    if (accessToken) {
      headers.set("Authorization", `Bearer ${accessToken}`);
    }

    const response = await fetch(`${this.options.baseUrl}${path}`, {
      ...init,
      headers
    });

    const envelope = (await response.json()) as ApiResponse<T>;
    if (!response.ok || !envelope.success) {
      const errors = envelope.errors.length > 0 ? envelope.errors : ["API request failed."];
      throw new ApiClientError(errors[0], response.status, errors, envelope.traceId);
    }

    return envelope.data as T;
  }
}
