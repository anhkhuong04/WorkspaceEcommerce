import type { OrderStatus, PaymentMethod, PaymentStatus } from "@workspace-ecommerce/api-types";
import i18n from "./i18n";

export const formatOrderStatus = (status: OrderStatus): string => i18n.t(`orderStatus.${status}`);
export const formatPaymentStatus = (status: PaymentStatus): string => i18n.t(`paymentStatus.${status}`);
export const formatPaymentMethod = (method: PaymentMethod): string => i18n.t(`paymentMethod.${method}`);
