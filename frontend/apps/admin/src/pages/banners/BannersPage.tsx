import { useQuery } from "@tanstack/react-query";
import { Card, Table, Tag } from "antd";
import { AdminPageHeader } from "../../components/ui/AdminPageHeader";
import { adminApi } from "../../services/api/adminApi";

export function BannersPage() {
  const bannersQuery = useQuery({ queryKey: ["admin-banners"], queryFn: adminApi.getBanners });

  return (
    <div className="admin-page-grid">
      <AdminPageHeader title="Banners" description="Banner management foundation for homepage content, active state, links, and display order." />
      <Card>
        <Table
          rowKey="id"
          loading={bannersQuery.isLoading}
          dataSource={bannersQuery.data ?? []}
          pagination={false}
          columns={[
            { title: "Title", dataIndex: "title" },
            { title: "Image", dataIndex: "imageUrl", ellipsis: true },
            { title: "Link", dataIndex: "linkUrl", ellipsis: true },
            { title: "Sort", dataIndex: "sortOrder" },
            { title: "Status", dataIndex: "isActive", render: (value: boolean) => <Tag color={value ? "green" : "default"}>{value ? "Active" : "Inactive"}</Tag> }
          ]}
        />
      </Card>
    </div>
  );
}
