import { useQuery } from "@tanstack/react-query";
import { Card, Table, Tag } from "antd";
import { AdminPageHeader } from "../../components/ui/AdminPageHeader";
import { adminApi } from "../../services/api/adminApi";

export function CategoriesPage() {
  const categoriesQuery = useQuery({ queryKey: ["admin-categories"], queryFn: adminApi.getCategories });

  return (
    <div className="admin-page-grid">
      <AdminPageHeader title="Categories" description="Category tree management foundation with active state and sort order." />
      <Card>
        <Table
          rowKey="id"
          loading={categoriesQuery.isLoading}
          dataSource={categoriesQuery.data ?? []}
          pagination={false}
          columns={[
            { title: "Name", dataIndex: "name" },
            { title: "Slug", dataIndex: "slug" },
            { title: "Sort", dataIndex: "sortOrder" },
            { title: "Status", dataIndex: "isActive", render: (value: boolean) => <Tag color={value ? "green" : "default"}>{value ? "Active" : "Inactive"}</Tag> }
          ]}
        />
      </Card>
    </div>
  );
}
