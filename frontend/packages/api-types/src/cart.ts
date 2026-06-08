export interface CartItemDto {
  id: string;
  productVariantId: string;
  productName: string | null;
  sku: string | null;
  quantity: number;
  unitPriceSnapshot: number;
  lineTotal: number;
}

export interface CartDto {
  id: string;
  customerId: string | null;
  sessionId: string | null;
  totalQuantity: number;
  totalAmount: number;
  items: CartItemDto[];
}

export interface AddCartItemRequest {
  sessionId: string;
  productVariantId: string;
  quantity: number;
}

export interface UpdateCartItemRequest {
  sessionId: string;
  quantity: number;
}
