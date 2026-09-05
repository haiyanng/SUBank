# SUBank Project Blueprint

## 1. Tổng quan

SUBank 3S là ứng dụng Online Banking mô phỏng được xây dựng nhằm minh họa các nghiệp vụ ngân hàng số
cơ bản trong phạm vi đồ án.

Hệ thống hỗ trợ ba nhóm người dùng:

- Customer
- Teller
- Admin

SUBank chỉ thực hiện các nghiệp vụ nội bộ trong hệ thống. Project không kết nối NAPAS, VietQR, ngân
hàng bên ngoài hoặc hệ thống eKYC thực tế.

Project hiện không hỗ trợ Customer tự đăng ký. Trong môi trường Development, Customer được tạo thông
qua Seed Data để đại diện cho các khách hàng đã hoàn tất quy trình đăng ký ngoài phạm vi demo.

## 2. Công nghệ sử dụng

SUBank sử dụng các công nghệ chính:

- Backend: ASP.NET Core 10 Web API.
- Frontend: Blazor WebAssembly.
- UI: Bootstrap và custom CSS.
- Database: SQL Server.
- ORM: Entity Framework Core.
- Authentication: ASP.NET Core Identity và JWT.
- Session: Redis.
- Realtime: SignalR.
- API Documentation: Swagger.
- PDF: tạo PDF ở backend.

Solution được tổ chức theo kiến trúc phân lớp, tham khảo các nguyên tắc của Clean Architecture, với
các project Domain, Contracts, Application, Infrastructure, API và Client.

Project hiện sử dụng service abstraction/service implementation để tổ chức nghiệp vụ; không triển
khai CQRS hoặc MediatR.

## 3. Cấu trúc hệ thống

Các project chính:

- SUBank.Domain
- SUBank.Contracts
- SUBank.Application
- SUBank.Infrastructure
- SUBank.Api
- SUBank.Client

Quan hệ phụ thuộc chính:

SUBank.Domain
        ↑
SUBank.Application
        ↑
SUBank.Infrastructure
        ↑
SUBank.Api

SUBank.Contracts
    ↑         ↑
SUBank.Api   SUBank.Client

SUBank.Client là Blazor WebAssembly frontend và giao tiếp với backend thông qua HTTP API.

Business logic không được đặt trực tiếp trong UI. Controller tiếp nhận request, kiểm tra thông tin
cần thiết và gọi các service abstraction. Các implementation liên quan đến EF Core, Identity, Redis,
QR, PDF và hạ tầng khác nằm trong Infrastructure.

Chi tiết kiến trúc được trình bày trong Architecture.md.

## 4. Vai trò người dùng

### 4.1 Customer

Customer có thể:

- Đăng nhập và đăng xuất.
- Xem danh sách tài khoản và số dư.
- Xem lịch sử và chi tiết giao dịch.
- Thực hiện chuyển tiền nội bộ SUBank.
- Chuyển tiền thông qua SUBank QR.
- Xem thông tin hồ sơ cá nhân.
- Xem thông báo realtime.
- Xem sao kê theo tháng hoặc năm.
- Xuất sao kê PDF.

Customer chỉ được truy cập dữ liệu thuộc quyền của mình.

### 4.2 Teller

Teller đại diện cho nhân viên giao dịch.

Các chức năng chính:

- Đăng nhập với role Teller.
- Tìm kiếm và xem thông tin cần thiết của Customer.
- Thực hiện Cash Deposit vào tài khoản Customer.

Teller không có quyền sử dụng các chức năng quản trị dành cho Admin.

### 4.3 Admin

Admin chịu trách nhiệm quản lý Customer và theo dõi hoạt động quản trị.

Các chức năng chính:

- Tìm kiếm Customer.
- Xem chi tiết Customer.
- Khóa Customer kèm lý do.
- Mở khóa Customer.
- Thu hồi phiên đang hoạt động khi Customer bị khóa.
- Xem Audit Log.

Hệ thống không cung cấp chức năng xóa Customer thật.

## 5. Authentication và Authorization

SUBank sử dụng ASP.NET Core Identity làm nền tảng quản lý user, password, role và login lockout.

Authentication sử dụng:

- JWT Access Token.
- Refresh Token trong HttpOnly cookie.
- Redis Active Session.
- Role-based Authorization.

Access token không được lưu persistent trong localStorage hoặc sessionStorage.

Customer có phiên hết hạn tuyệt đối sau 15 phút kể từ lúc đăng nhập. Teller và Admin sử dụng access
token 15 phút, refresh theo nhu cầu và logical session tối đa bảy ngày.

Hệ thống hỗ trợ Single Active Session. Khi cùng một user đăng nhập bằng session mới, session cũ
không còn được chấp nhận cho protected API.

Identity Lockout và Admin Suspension là hai cơ chế độc lập:

- Identity Lockout xử lý đăng nhập sai nhiều lần.
- Admin Suspension cho phép Admin chủ động khóa Customer.

Khi Customer bị Admin khóa, active session của Customer được thu hồi.

Chi tiết token, cookie, Redis session, multi-tab handling, CSRF/CORS và các quy tắc bảo mật được
trình bày trong Security-Design.md.

## 6. Database

Database Development mặc định là:

`SUBankV2`

Project sử dụng SQL Server và Entity Framework Core Code First.

Các nhóm dữ liệu chính gồm:

- ASP.NET Core Identity.
- Customer Profile.
- Bank Account.
- Financial Transaction.
- User Session và Refresh Token.
- Audit Log.
- Các dữ liệu hỗ trợ nghiệp vụ liên quan.

CustomerProfile lưu thông tin nghiệp vụ của Customer và liên kết với Identity user.

BankAccount đại diện cho tài khoản ngân hàng.

FinancialTransaction ghi nhận các chuyển động tiền đã hoàn tất trong hệ thống.

Tiền được xử lý bằng decimal; không sử dụng float hoặc double cho balance và transaction amount.

Chi tiết bảng, khóa, constraint và quan hệ được trình bày trong Database-Design.md.

## 7. Development Seed Data

API tạo dữ liệu demo khi chạy trong môi trường Development.

Seed hiện tại gồm:

- 5 Customer.
- 1 Teller.
- 1 Admin.
- 5 Customer Profile.
- 17 tài khoản ngân hàng demo.

Các user Development:

- 0900000001 — Customer.
- 0900000002 — Customer.
- 0900000003 — Customer.
- 0900000004 — Customer.
- 0900000005 — Customer.
- teller — Teller.
- admin — Admin.

Đối với Customer demo, số điện thoại đồng thời được sử dụng làm username đăng nhập.

Thông tin đăng nhập và danh sách tài khoản demo đầy đủ được ghi trong README.md ở thư mục gốc.

Seed Data chỉ phục vụ Development/Demo và không đại diện cho quy trình onboarding Customer thực tế.

## 8. Chuyển tiền nội bộ

SUBank hỗ trợ chuyển tiền giữa các tài khoản nội bộ.

Một giao dịch chuyển tiền hợp lệ phải đáp ứng các điều kiện chính:

- Customer đã authentication.
- Tài khoản nguồn thuộc Customer.
- Tài khoản nguồn và tài khoản nhận khác nhau.
- Tài khoản nhận tồn tại và hợp lệ.
- Số tiền lớn hơn 0.
- Tài khoản nguồn đủ số dư.
- Transaction Password hợp lệ.
- Có Idempotency Key.

Debit tài khoản nguồn, credit tài khoản nhận và tạo bản ghi giao dịch được xử lý trong SQL
transaction để đảm bảo tính nguyên tử.

Hệ thống sử dụng concurrency control để hạn chế xung đột khi nhiều request cùng tác động lên
balance.

Redis không được sử dụng làm nguồn sự thật cho số dư. SQL Server là nguồn dữ liệu có thẩm quyền đối
với account và transaction.

## 9. Cash Deposit

Cash Deposit là nghiệp vụ dành cho Teller.

Teller chọn Customer hoặc account hợp lệ và nhập số tiền cần nộp.

Backend kiểm tra authorization và dữ liệu đầu vào trước khi cập nhật balance.

Việc cập nhật balance và tạo Financial Transaction được thực hiện trong cùng SQL transaction.

Các thao tác quan trọng được ghi Audit Log.

Admin không được sử dụng Teller Cash Deposit endpoint chỉ vì có quyền Admin; authorization được kiểm
tra theo role của từng nghiệp vụ.

## 10. SUBank QR

SUBank sử dụng QR nội bộ, không phải VietQR hoặc NAPAS QR.

QR dùng để hỗ trợ điền thông tin vào form chuyển tiền. Ví dụ:

```text
subank://transfer?v=1&account=0900000001
```

QR có thể chứa version, account number, amount tùy chọn và message tùy chọn.

Hệ thống hỗ trợ:

- Tạo QR.
- Quét QR bằng camera.
- Đọc QR từ ảnh upload PNG/JPEG/WebP.
- Điền trước thông tin chuyển tiền.

QR không chứa password, transaction password, access token, session ID hoặc balance.

Sau khi đọc QR, backend vẫn phải xác minh tài khoản nhận. Customer vẫn phải review giao dịch và hoàn
thành quy trình transfer thông thường.

Camera trên trình duyệt yêu cầu HTTPS.

## 11. Sao kê và PDF

Customer có thể xem sao kê theo tháng hoặc năm.

Dữ liệu sao kê được tạo từ account và transaction hiện có, không sử dụng bảng Statement riêng.

Hệ thống hỗ trợ xuất sao kê thành PDF ở backend.

Customer chỉ được tạo và xem sao kê của tài khoản thuộc quyền sở hữu của mình.

## 12. Realtime Notification

SignalR được sử dụng để cải thiện trải nghiệm realtime.

Một số thay đổi như giao dịch, số dư hoặc trạng thái session có thể được thông báo đến Client sau
khi nghiệp vụ chính đã hoàn tất.

SignalR không phải nguồn sự thật của dữ liệu và không thay thế authentication hoặc authorization.

Nếu realtime event bị mất, Client vẫn có thể lấy trạng thái hiện tại thông qua REST API.

## 13. Logging và Audit

SUBank phân biệt ba loại dữ liệu.

- Financial Transaction ghi nhận chuyển động tiền đã hoàn tất.
- Audit Log ghi nhận các hành động nghiệp vụ hoặc bảo mật quan trọng như actor, action, target,
  result, timestamp và correlation context.
- Application Log dùng để chẩn đoán hoạt động của ứng dụng và exception.

Trong Development, application log được ghi ra console và rolling file.

Hệ thống không chủ động ghi password, token hoặc secret vào log.

Chi tiết được trình bày trong Application-Logging.md.

## 14. Testing

Project sử dụng cả Manual Testing và Automated Testing.

Manual Testing là phần chính để kiểm tra các luồng nghiệp vụ, giao diện và hành vi end-to-end trong
quá trình phát triển.

Automated Test được giữ ở phạm vi nhỏ, tập trung vào một số logic và business rule quan trọng thay
vì cố đạt coverage cao hoặc bao phủ mọi edge case.

Các nhóm ưu tiên gồm:

- Authentication và authorization quan trọng.
- Ownership.
- Transfer.
- Cash Deposit.
- Validation và business rule cốt lõi.

Automated Test không thay thế hoàn toàn quá trình kiểm thử thủ công.

## 15. Health Check

Backend cung cấp các health endpoint:

- `/health/live`
- `/health/ready`
- `/health`

Health Check được sử dụng để kiểm tra trạng thái API và các dependency quan trọng như SQL Server và
Redis.

## 16. Deployment

Trong Development:

- API và Blazor Client chạy trên HTTPS localhost.
- SQL Server chạy local.
- Redis mặc định tại localhost:6379.

Khi publish API, Blazor Client được publish cùng artifact để bản demo hoặc Production có thể chạy
cùng một HTTPS origin.

Các production secret như database connection string, JWT signing key và Redis connection string
không được commit trực tiếp vào source code.

Chi tiết cấu hình và quy trình triển khai được trình bày trong Deployment.md.

Quy trình backup và restore database được trình bày trong Backup-Restore-Runbook.md.

## 17. Phạm vi chưa triển khai

Các chức năng sau không thuộc phiên bản hiện tại:

- Customer self-registration.
- eKYC thực tế.
- Chuyển tiền liên ngân hàng.
- NAPAS/VietQR integration.
- AI Financial Assistant.
- AI/Fraud Detection.
- ML.NET Fraud Model.
- Microservices.
- Kafka/RabbitMQ/Event Bus.
- Loan.
- Card management.
- Foreign exchange.
- Interest calculation.

Các chức năng này có thể được xem xét trong hướng phát triển tương lai nhưng không được mô tả là
chức năng hiện có của SUBank.

## 18. Tài liệu liên quan

- Architecture.md — kiến trúc hệ thống.
- Database-Design.md — thiết kế database.
- Security-Design.md — authentication, authorization và session security.
- Application-Logging.md — application logging.
- Deployment.md — deployment.
- Backup-Restore-Runbook.md — backup và restore.
- Issue-Register.md — lỗi và các phát hiện kỹ thuật trong quá trình phát triển.
- AI-Design.md — phạm vi thiết kế AI.
- AI-Usage-Report.md — báo cáo sử dụng AI trong quá trình phát triển.
- Figma-Status.md — trạng thái thiết kế Figma.
- diagrams/ — các sơ đồ của hệ thống.

## 19. Giới hạn của project

SUBank là ứng dụng phục vụ mục đích học tập và demo.

Project mô phỏng một số nguyên tắc thường gặp trong hệ thống ngân hàng như authentication,
authorization, transaction atomicity, audit và session management, nhưng không được coi là core
banking system hoàn chỉnh hoặc sản phẩm đủ điều kiện triển khai cho ngân hàng thực tế.
