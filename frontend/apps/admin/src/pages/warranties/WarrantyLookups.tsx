import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { formatMoney } from "@workspace-ecommerce/shared-utils";
import {
  Button,
  Field,
  Notice,
  SelectInput,
  TextInput,
} from "../../components/ui/AdminUi";
import { Pagination } from "../../components/ui/Pagination";
import { adminApi } from "../../services/api/adminApi";
import { getApiErrorMessage } from "../../services/api/errors";
import { formatLocalizedText } from "../../utils/localizedText";

export function VariantLookup({
  value,
  onChange,
}: {
  value: string;
  onChange: (value: string) => void;
}) {
  const [draft, setDraft] = useState("");
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const [chosenLabel, setChosenLabel] = useState("");
  const query = useQuery({
    queryKey: ["admin-products", "variant-lookup", search, page],
    queryFn: () =>
      adminApi.getProducts({
        search: search || undefined,
        pageNumber: page,
        pageSize: 10,
      }),
  });
  const options =
    query.data?.items.flatMap((product) =>
      product.variants.map((variant) => ({
        id: variant.id,
        label: `${variant.sku} · ${formatLocalizedText(product.name)} · ${variant.name}`,
      })),
    ) ?? [];
  return (
    <div className="grid gap-3">
      <Field label="Find product / SKU">
        <div className="flex gap-2">
          <TextInput
            type="search"
            maxLength={150}
            value={draft}
            onChange={(event) => setDraft(event.target.value)}
            placeholder="Name or SKU"
          />
          <Button
            onClick={() => {
              setSearch(draft.trim());
              setPage(1);
            }}
          >
            Find SKU
          </Button>
        </div>
      </Field>
      {query.isError && (
        <Notice type="error" title={getApiErrorMessage(query.error)}>
          <Button onClick={() => void query.refetch()}>Retry SKUs</Button>
        </Notice>
      )}
      <Field label="Product variant">
        <SelectInput
          required
          value={value}
          disabled={query.isFetching}
          onChange={(event) => {
            onChange(event.target.value);
            setChosenLabel(
              options.find((option) => option.id === event.target.value)
                ?.label ?? "",
            );
          }}
        >
          <option value="">
            {query.isPending ? "Loading SKUs..." : "Choose a SKU"}
          </option>
          {value && !options.some((option) => option.id === value) && (
            <option value={value}>{chosenLabel}</option>
          )}
          {options.map((option) => (
            <option key={option.id} value={option.id}>
              {option.label}
            </option>
          ))}
        </SelectInput>
      </Field>
      {query.data && (
        <Pagination
          data={query.data}
          label="products with SKUs"
          busy={query.isFetching}
          onPageChange={setPage}
        />
      )}
    </div>
  );
}

export function WarrantyPlanLookup({
  value,
  onChange,
}: {
  value: string;
  onChange: (value: string) => void;
}) {
  const [search, setSearch] = useState("");
  const [draft, setDraft] = useState("");
  const [page, setPage] = useState(1);
  const [chosenLabel, setChosenLabel] = useState("");
  const query = useQuery({
    queryKey: ["admin", "warranty-plans", "lookup", search, page],
    queryFn: () =>
      adminApi.getWarrantyPlans({
        pageNumber: page,
        pageSize: 20,
        search: search || undefined,
        isActive: true,
      }),
  });
  return (
    <div className="grid gap-3">
      <Field label="Find warranty plan">
        <div className="flex gap-2">
          <TextInput
            type="search"
            maxLength={150}
            value={draft}
            onChange={(event) => setDraft(event.target.value)}
          />
          <Button
            onClick={() => {
              setSearch(draft.trim());
              setPage(1);
            }}
          >
            Find plan
          </Button>
        </div>
      </Field>
      {query.isError && (
        <Notice type="error" title={getApiErrorMessage(query.error)}>
          <Button onClick={() => void query.refetch()}>Retry plans</Button>
        </Notice>
      )}
      <Field label="Warranty plan">
        <SelectInput
          required
          disabled={query.isFetching}
          value={value}
          onChange={(event) => {
            onChange(event.target.value);
            const plan = query.data?.items.find(
              (item) => item.id === event.target.value,
            );
            setChosenLabel(plan ? `${plan.code} · ${plan.name}` : "");
          }}
        >
          <option value="">Choose an active plan</option>
          {value && !query.data?.items.some((item) => item.id === value) && (
            <option value={value}>{chosenLabel}</option>
          )}
          {query.data?.items.map((item) => (
            <option key={item.id} value={item.id}>
              {item.code} · {item.name}
            </option>
          ))}
        </SelectInput>
      </Field>
      {query.data && (
        <Pagination
          data={query.data}
          label="active plans"
          busy={query.isFetching}
          onPageChange={setPage}
        />
      )}
    </div>
  );
}

export function WarrantyOrderItemLookup({
  variantId,
  value,
  onChange,
}: {
  variantId: string;
  value: string;
  onChange: (value: string) => void;
}) {
  const [search, setSearch] = useState("");
  const [draft, setDraft] = useState("");
  const [page, setPage] = useState(1);
  const [orderId, setOrderId] = useState("");
  const orders = useQuery({
    queryKey: ["admin-orders", "warranty-lookup", search, page],
    queryFn: () =>
      adminApi.getOrders({
        search: search || undefined,
        pageNumber: page,
        pageSize: 10,
      }),
  });
  const order = useQuery({
    queryKey: ["admin-order", orderId],
    queryFn: () => adminApi.getOrder(orderId),
    enabled: !!orderId,
  });
  const items =
    order.data?.items.filter(
      (item) =>
        item.productVariantId === variantId &&
        order.data?.status !== 6 &&
        order.data?.status !== 7,
    ) ?? [];
  return (
    <div className="grid gap-3">
      <Field label="Find order">
        <div className="flex gap-2">
          <TextInput
            type="search"
            maxLength={150}
            value={draft}
            onChange={(event) => setDraft(event.target.value)}
            placeholder="Code, buyer or phone"
          />
          <Button
            onClick={() => {
              setSearch(draft.trim());
              setPage(1);
              setOrderId("");
              onChange("");
            }}
          >
            Find order
          </Button>
        </div>
      </Field>
      {orders.isError && (
        <Notice type="error" title={getApiErrorMessage(orders.error)}>
          <Button onClick={() => void orders.refetch()}>Retry orders</Button>
        </Notice>
      )}
      <Field label="Order">
        <SelectInput
          value={orderId}
          disabled={orders.isFetching}
          onChange={(event) => {
            setOrderId(event.target.value);
            onChange("");
          }}
        >
          <option value="">Choose an order</option>
          {orders.data?.items.map((item) => (
            <option key={item.id} value={item.id}>
              {item.orderCode} · {item.customerName} ·{" "}
              {formatMoney(item.totalAmount)}
            </option>
          ))}
        </SelectInput>
      </Field>
      {orders.data && (
        <Pagination
          data={orders.data}
          label="order choices"
          busy={orders.isFetching}
          onPageChange={(next) => {
            setPage(next);
            setOrderId("");
            onChange("");
          }}
        />
      )}
      {order.isError && (
        <Notice type="error" title={getApiErrorMessage(order.error)}>
          <Button onClick={() => void order.refetch()}>
            Retry order items
          </Button>
        </Notice>
      )}
      {orderId && (
        <Field label="Purchased item of this SKU">
          <SelectInput
            value={value}
            disabled={order.isFetching}
            onChange={(event) => onChange(event.target.value)}
          >
            <option value="">
              {order.isFetching
                ? "Loading items..."
                : items.length
                  ? "Choose purchased item"
                  : "This order has no eligible item of this SKU"}
            </option>
            {items.map((item) => (
              <option key={item.id} value={item.id}>
                {item.skuSnapshot} · {item.productNameSnapshot} · purchased{" "}
                {item.quantity}
              </option>
            ))}
          </SelectInput>
        </Field>
      )}
    </div>
  );
}
