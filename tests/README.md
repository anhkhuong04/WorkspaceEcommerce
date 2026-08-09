# Manual Testing Portfolio - Workspace E-Commerce

> **INTERVIEW PORTFOLIO SAMPLE - SYNTHETIC TEST EXECUTION DATA**  
> Bộ tài liệu này được chuẩn bị từ mã nguồn và tài liệu công khai trong repository `WorkspaceEcommerce`. Kết quả chạy test, defect, mã đơn hàng, tài khoản, thời gian và evidence đều là dữ liệu mô phỏng trên môi trường local; không phải log production và không nên trình bày như lỗi đã từng xảy ra trong hệ thống thật.

## Mục đích

Bộ hồ sơ minh họa cách phân tích, thiết kế, thực thi và báo cáo kiểm thử thủ công cho một hệ thống e-commerce sử dụng ASP.NET Core Web API, PostgreSQL, JWT, VNPay demo và MiniLogistics sandbox.

## Tài liệu

| File | Nội dung |
|---|---|
| [01-test-case-document.md](01-test-case-document.md) | Phạm vi, test data và 36 test cases cho API/UI chính |
| [02-bug-report.md](02-bug-report.md) | 7 defect giả lập với bước tái hiện, severity, priority và retest |
| [03-test-summary-report.md](03-test-summary-report.md) | Kết quả test cycle, defect metrics, risk và release recommendation |
| [04-interview-notes.md](04-interview-notes.md) | Cách trình bày trung thực, câu hỏi thường gặp và giới hạn của bộ hồ sơ |

## Nguồn dùng để thiết kế

- `README.md`: kiến trúc, stack, môi trường local và cách chạy test.
- `task.md`: shipment integration, webhook security, tracking và acceptance criteria.
- ASP.NET Core controllers trong `src/WorkspaceEcommerce.Api/Controllers`: route, authorization và response status.
- Automated tests trong `tests/`: tham khảo business rules và các vùng có rủi ro.

## Quy ước an toàn

- Chỉ dùng domain `example.com`, số điện thoại giả và ID mẫu.
- Không lưu token thật, password thật, API key, webhook secret, connection string hoặc dữ liệu cá nhân.
- Evidence chỉ dùng tên file minh họa; khi demo thật nên chụp từ local/sandbox và che token/cookie.
- Không khẳng định các defect bên dưới tồn tại trên nhánh hiện tại. Có thể mô tả chúng là “defect scenarios mô phỏng để thể hiện kỹ năng báo cáo”.

