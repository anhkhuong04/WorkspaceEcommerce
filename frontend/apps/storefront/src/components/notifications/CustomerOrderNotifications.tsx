import { HubConnectionBuilder, LogLevel } from "@microsoft/signalr";
import { useQueryClient } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { NavLink } from "react-router-dom";
import { formatOrderStatus } from "@workspace-ecommerce/shared-utils";
import type { OrderStatus } from "@workspace-ecommerce/api-types";
import { useCustomerAuth } from "../../features/customer-auth/useCustomerAuth";
import { getNotificationHubUrl } from "../../services/api/storefrontApi";

interface OrderStatusChangedNotification {
  orderId: string;
  orderCode: string;
  newStatus: OrderStatus;
  cancellationReason?: string | null;
  customerMessage?: string | null;
}

export function CustomerOrderNotifications() {
  const queryClient = useQueryClient();
  const { session } = useCustomerAuth();
  const [notification, setNotification] = useState<OrderStatusChangedNotification | null>(null);

  useEffect(() => {
    if (!session?.accessToken) return;

    const connection = new HubConnectionBuilder()
      .withUrl(getNotificationHubUrl(), { accessTokenFactory: () => session.accessToken })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    connection.on("order_status_changed", (payload: OrderStatusChangedNotification) => {
      setNotification(payload);
      void queryClient.invalidateQueries({ queryKey: ["customer", "orders"] });
      void queryClient.invalidateQueries({ queryKey: ["customer", "order", payload.orderId] });
    });

    void connection.start().catch(() => undefined);
    return () => {
      connection.off("order_status_changed");
      void connection.stop();
    };
  }, [queryClient, session?.accessToken]);

  if (!notification) return null;

  const statusText = formatOrderStatus(notification.newStatus);
  return (
    <aside className="fixed bottom-5 right-5 z-[100] w-[min(420px,calc(100vw-2.5rem))] rounded-2xl border border-slate-200 bg-white p-4 text-slate-950 shadow-2xl" role="status" aria-live="polite">
      <div className="flex items-start justify-between gap-4">
        <div>
          <p className="font-black">Order {notification.orderCode}: {statusText}</p>
          {notification.cancellationReason ? <p className="mt-1 text-sm text-red-700">Reason: {notification.cancellationReason}</p> : null}
          {notification.customerMessage ? <p className="mt-1 text-sm text-slate-600">{notification.customerMessage}</p> : null}
          <NavLink to={`/account/orders/${notification.orderId}`} className="mt-3 inline-flex text-sm font-bold text-[var(--brand)] hover:underline" onClick={() => setNotification(null)}>
            View order
          </NavLink>
        </div>
        <button type="button" className="grid h-8 w-8 shrink-0 place-items-center rounded-full text-lg text-slate-500 hover:bg-slate-100" aria-label="Dismiss notification" onClick={() => setNotification(null)}>×</button>
      </div>
    </aside>
  );
}
