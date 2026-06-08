export interface ApiResponse<T> {
  success: boolean;
  data: T | null;
  errors: string[];
  traceId: string;
}

export interface PagedResult<T> {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

export interface PaginationRequest {
  pageNumber?: number;
  pageSize?: number;
}
