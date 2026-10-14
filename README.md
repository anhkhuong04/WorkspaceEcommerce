# WorkspaceEcommerce

Hệ thống thương mại điện tử gồm ASP.NET Core API, PostgreSQL và hai ứng dụng React: storefront cho khách hàng, admin cho quản trị. Backend là modular monolith; các module dùng chung tiến trình và cơ sở dữ liệu.

Phạm vi hiện tại: catalog/content, giỏ hàng, checkout, coupon, COD/chuyển khoản/VNPay, vận chuyển MiniLogistics, tài khoản/2FA, loyalty, media, biên nhận PDF và bảo hành theo feature flag.

## Bắt đầu

1. [Chạy dự án local](docs/backend/development/guide.md): toolchain, Docker, cấu hình, migration và frontend.
2. [Backend](docs/backend/README.md): kiến trúc, module và luồng xử lý.
3. [Frontend](docs/frontend/README.md): storefront/admin, shared packages, API và session.
4. [Services](docs/services/README.md): các dịch vụ bên ngoài và hợp đồng tích hợp.

[Mục lục tài liệu](docs/README.md) dẫn đến quy tắc nghiệp vụ, API conventions, ADR và runbook. [Testing](docs/backend/development/testing.md) mô tả lệnh kiểm tra và phạm vi coverage cho developer.

## Repository

| Đường dẫn | Nội dung |
| --- | --- |
| `src/WorkspaceEcommerce.*` | Backend: Domain, Application, Infrastructure, Api |
| `frontend/apps` | Storefront và admin |
| `frontend/packages` | API types, HTTP client và utilities dùng chung |
| `tests` | Automated tests backend |
| `scripts` | Kiểm tra migration, regression, browser, performance và release |
| `docs` | Tài liệu hiện trạng và vận hành |
| `.agents` | Hướng dẫn thao tác cho coding agent; bắt đầu từ [AGENTS.md](AGENTS.md) |

Phiên bản toolchain được khai báo trong [global.json](global.json), [.node-version](.node-version), [frontend/package.json](frontend/package.json) và [docker-compose.yml](docker-compose.yml). Compose phục vụ phát triển local; API integration tests cần Docker để chạy PostgreSQL Testcontainers.

Giới hạn sản phẩm và vận hành được ghi tại [Known limitations](docs/backend/development/known-limitations.md). Cấu hình nhạy cảm dùng `.env` hoặc `appsettings.Local.json` được Git bỏ qua; xem [Configuration matrix](docs/services/runbooks/configuration-matrix.md) trước khi cấu hình môi trường triển khai.
