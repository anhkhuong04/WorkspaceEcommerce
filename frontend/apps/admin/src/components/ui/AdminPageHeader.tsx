import { Typography } from "antd";

interface AdminPageHeaderProps {
  title: string;
  description: string;
}

export function AdminPageHeader({ title, description }: AdminPageHeaderProps) {
  return (
    <div className="admin-page-header">
      <div>
        <Typography.Text className="admin-eyebrow">Admin</Typography.Text>
        <Typography.Title level={2} style={{ margin: 0 }}>{title}</Typography.Title>
      </div>
      <Typography.Paragraph type="secondary" style={{ maxWidth: 520, margin: 0 }}>{description}</Typography.Paragraph>
    </div>
  );
}
