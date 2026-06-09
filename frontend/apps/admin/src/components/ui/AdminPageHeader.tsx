import type { ReactNode } from "react";
import { Space, Typography } from "antd";

interface AdminPageHeaderProps {
  title: string;
  description: string;
  actions?: ReactNode;
}

export function AdminPageHeader({ title, description, actions }: AdminPageHeaderProps) {
  return (
    <div className="admin-page-header">
      <div>
        <Typography.Text className="admin-eyebrow">Admin</Typography.Text>
        <Typography.Title level={2} style={{ margin: 0 }}>{title}</Typography.Title>
      </div>
      <Space size={18} align="center" className="admin-page-header-side">
        <Typography.Paragraph type="secondary" style={{ maxWidth: 520, margin: 0 }}>{description}</Typography.Paragraph>
        {actions}
      </Space>
    </div>
  );
}
