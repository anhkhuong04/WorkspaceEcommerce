# Tài liệu WorkspaceEcommerce

Tài liệu dành cho developer đọc và bảo trì hệ thống đang được triển khai trong repository. Bắt đầu bằng [hướng dẫn chạy local](backend/development/guide.md), sau đó chọn phần công việc bên dưới.

## Ba điểm đọc chính

| Phần | Nội dung | Đọc tiếp khi cần |
| --- | --- | --- |
| [Backend](backend/README.md) | Kiến trúc, trách nhiệm module, luồng request và checkout | Business rules, data/consistency, API conventions |
| [Frontend](frontend/README.md) | Storefront/admin, tổ chức code, API, session và cache | Shared packages, router và testing |
| [Services](services/README.md) | VNPay, MiniLogistics, SMTP, Google, object storage và telemetry | Configuration matrix, outbox và runbook |

```text
React storefront/admin
        | HTTP / SignalR
ASP.NET Core API + hosted workers
        |                    |
   PostgreSQL         External services
```

Services ở đây là các dịch vụ bên ngoài được web tích hợp. Application services và hosted workers thuộc Backend; SignalR là endpoint của API.

## Cấu trúc thư mục

```text
docs/
├── README.md
├── backend/
│   ├── README.md
│   ├── api/
│   ├── architecture/
│   ├── product/
│   ├── adr/
│   ├── development/
│   ├── performance/
│   ├── runbooks/
│   ├── currency-and-money.md
│   └── order-receipts.md
├── frontend/
│   ├── README.md
│   └── ui-ux-style.md
└── services/
    ├── README.md
    └── runbooks/
```

`backend/development` chứa hướng dẫn chạy và kiểm thử toàn repository, bao gồm frontend, để tránh nhân bản tài liệu dùng chung. `backend/runbooks` quản lý triển khai, phục hồi và bảo hành; `services/runbooks` quản lý tích hợp, cấu hình, credentials, media, outbox và telemetry. ADR nằm cùng Backend và giữ nguyên nội dung quyết định.

## Tài liệu tham chiếu

| Cần tra cứu | Tài liệu sở hữu nội dung |
| --- | --- |
| Quy tắc tiền, checkout, trạng thái đơn, loyalty, identity, bảo hành | [Business rules](backend/product/business-rules.md) |
| Schema, transaction, locking, snapshot, outbox/inbox, migration | [Data and consistency](backend/architecture/data-and-consistency.md) |
| Route families, envelope, lỗi, auth/ownership, paging và file | [API conventions](backend/api/conventions.md) |
| Định hướng hình ảnh và tiêu chí review UI/UX (đề xuất) | [Frontend UI/UX style](frontend/ui-ux-style.md) |
| Cài đặt, chạy local, cấu hình và xử lý lỗi môi trường | [Development guide](backend/development/guide.md) |
| Test layers, lệnh kiểm tra và yêu cầu coverage | [Testing and quality](backend/development/testing.md) |
| Chức năng còn thiếu và giới hạn hiện tại | [Known limitations](backend/development/known-limitations.md) |
| Kiểu dữ liệu và định dạng tiền | [Currency and money](backend/currency-and-money.md) |
| Quyền truy cập, render và cấu hình biên nhận PDF | [Order receipts](backend/order-receipts.md) |
| Lý do cho quyết định kiến trúc đã được chấp nhận | [ADR index](backend/adr/README.md) |

## Vận hành

- [Configuration matrix](services/runbooks/configuration-matrix.md), [credential rotation](services/runbooks/credential-rotation.md).
- [Production release](backend/runbooks/production-release.md), [disaster recovery](backend/runbooks/disaster-recovery.md), [observability](services/runbooks/observability-and-alerting.md).
- [Outbox operations](services/runbooks/background-outbox-operations.md), [media scanning policy](services/runbooks/media-malware-scanning.md), [warranty operations](backend/runbooks/warranty-operations.md).
- [Query measurement](backend/performance/prh-008-query-plan-runbook.md), [load/resilience](backend/performance/prh-016-load-resilience-runbook.md).

## Quy tắc bảo trì tài liệu

- Source, tests, migrations, configuration validators và CI là bằng chứng hiện trạng; tiếp theo là ADR đã chấp nhận, rồi tài liệu hiện trạng. Ticket cũ và báo cáo mô phỏng không xác định hành vi hiện tại.
- Mỗi nội dung có một nơi sở hữu trong bảng trên. Các guide giới thiệu và dẫn link; không chép lại lệnh, toàn bộ DTO, option hay danh sách endpoint.
- Thay đổi một quy tắc hoặc contract thì cập nhật tài liệu sở hữu cùng code/tests. Khi di chuyển tài liệu, cập nhật các link trong repository.
- ADR giữ nguyên bối cảnh quyết định; dùng ADR mới hoặc ghi chú supersession khi quyết định thay đổi. Runbook giữ các bước thao tác và kiểm tra vận hành.
- Tính năng dự kiến thuộc issue/proposal có nhãn rõ ràng. Credentials, dữ liệu khách hàng và evidence của từng lần chạy không đưa vào tài liệu Git.
- [README gốc](../README.md) chỉ dẫn đường; `.agents/` chỉ chứa hướng dẫn cho agent. Không dùng hai nơi này làm bản sao của tài liệu sản phẩm.
