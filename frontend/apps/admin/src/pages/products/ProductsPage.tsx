import { useQuery } from "@tanstack/react-query";
import { Card, Table, Tag } from "antd";
import { AdminPageHeader } from "../../components/ui/AdminPageHeader";
import { adminApi } from "../../services/api/adminApi";

export function ProductsPage() {
  const productsQuery = useQuery({ queryKey: ["admin-products"], queryFn: adminApi.getProducts });

  return (
    <div className="admin-page-grid">
      <AdminPageHeader title="Products" description="Product management foundation for product, SKU, pricing, stock, and visibility operations." />
      <Card>
        <Table
          rowKey="id"
          loading={productsQuery.isLoading}
          dataSource={productsQuery.data ?? []}
          pagination={{ pageSize: 8 }}
          columns={[
            { title: "Product", dataIndex: "name" },
            { title: "Category", dataIndex: "categoryName" },
            { title: "Variants", dataIndex: "variants", render: (variants: unknown[]) => variants.length },
            { title: "Featured", dataIndex: "isFeatured", render: (value: boolean) => value ? <Tag color="blue">Featured</Tag> : <Tag>Standard</Tag> },
            { title: "Status", dataIndex: "isActive", render: (value: boolean) => <Tag color={value ? "green" : "default"}>{value ? "Active" : "Inactive"}</Tag> }
          ]}
        />
      </Card>
    </div>
  );
}

