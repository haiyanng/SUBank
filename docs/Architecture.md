# Kiến trúc hệ thống SUBank V2.4

## Kiểu kiến trúc

SUBank V2.4 là modular monolith được tổ chức theo Clean Architecture. Hệ thống có một backend deployable duy nhất, không sử dụng microservices, Event Bus hoặc distributed database.

Luồng xử lý bắt buộc:

```text
Blazor WebAssembly Client
        ├── HTTPS REST/JSON: request-response và toàn bộ nghiệp vụ chính
        └── SignalR qua secure WebSocket: realtime notification best-effort
                        ↓
ASP.NET Core API
        ↓
Application Use Case
        ↓
Domain Rule và Application Abstraction
        ↓
Infrastructure Implementation
        ↓
SQL Server/Azure SQL, Redis hoặc external provider
```

REST/JSON và SignalR hoạt động song song, không thay thế nhau. Login, account query, transfer, Teller Cash Deposit và Admin operation luôn đi qua REST API. SignalR chỉ thông báo `ForceLogout`, `TransactionReceived` hoặc `BalanceChanged`; khi nhận event, Client phải gọi REST API để lấy dữ liệu có thẩm quyền từ SQL. `ForceLogout` đi tới group của session bị thay thế hoặc thu hồi do logout/lockout; event ngân hàng chỉ đi tới group của active `sid` được cả Redis và durable session state trong SQL xác nhận, không broadcast theo user.

Controller chỉ binding request, đọc authentication context, gọi Application use case và map response. Controller không query trực tiếp banking `DbContext` và không chứa business rule làm thay đổi balance.

## Trách nhiệm của từng project

### `SUBank.Domain`

Chứa entity, enum, value rule và domain invariant. Project này không phụ thuộc EF Core, ASP.NET Core, Redis, SignalR, Blazor hoặc OpenAI SDK.

### `SUBank.Contracts`

Chứa request/response DTO và shared enum an toàn cho trình duyệt. Không expose Domain entity, EF entity, hash, secret hoặc internal persistence detail.

### `SUBank.Application`

Chứa use case, validation, authorization theo resource, interface của repository/provider và orchestration contract. Application phụ thuộc Domain và Contracts nhưng không biết concrete SQL, Redis hoặc OpenAI implementation.

### `SUBank.Infrastructure`

Triển khai EF Core, SQL persistence, ASP.NET Core Identity storage, token/session persistence, Redis adapter, OpenAI provider, PDF provider và system clock. Infrastructure triển khai interface do Application định nghĩa.

### `SUBank.Api`

Là composition root phía server, chứa controller, middleware, authentication/authorization pipeline, SignalR hub, OpenAPI và DI registration. Không chứa core banking business logic.

### `SUBank.Client`

Là Blazor WebAssembly UI, chỉ phụ thuộc Contracts. Client chứa page/component theo feature, API client, authentication state, role-aware navigation, SignalR connection và QR UI. Client không tham chiếu Infrastructure hoặc `DbContext`.

## Luồng authentication và session

Identity xác minh credential; SQL lưu `UserSession` bền vững và chỉ lưu hash refresh token. Redis là active-session pointer duy nhất của mỗi user. Access token nằm trong .NET/WASM memory; refresh token nằm trong HttpOnly `Secure` cookie. Protected request đi qua JWT authentication, Redis active-session check rồi SQL session/user-state check trước controller.

Customer dùng session/access token tuyệt đối 15 phút; Client kiểm tra timer và kiểm tra lại khi tab quay về foreground. Teller/Admin refresh access token theo nhu cầu, còn logical session tối đa bảy ngày. Không có heartbeat refresh nền.

Mỗi tab bind một `SessionId`; browser chỉ persist binding và logout intent không phải credential. Login, refresh và logout cùng sửa shared refresh cookie nên được serialize bằng Web Lock xuyên tab, giữ khóa đến khi `fetch`, việc đọc và kiểm tra tối thiểu response hoàn tất; không dùng lease có TTL. Response login không đọc được, sai cấu trúc hoặc lệch `SessionId` giữa header/body được fail closed và compensation ngay trong cùng khóa. Logout intent gắn session để tab cũ không khóa nhầm tab của session mới. Protected REST pipeline ngoài QR chụp generation/token và bỏ response cũ sau session change.

Logout commit thu hồi `UserSession`/token family trong SQL trước, rồi compare-delete Redis và gửi SignalR best-effort. Expected-session header cùng bearer quyết định session nào được thu hồi; nếu shared cookie đã thuộc session mới, server giữ cookie mới và chỉ revoke session cũ từ JWT claims. Cleanup phiên login bị Client từ chối dùng endpoint bearer-bound không mutate cookie.

## Nguồn dữ liệu và external service

- SQL Server/Azure SQL là nguồn sự thật duy nhất cho user profile nghiệp vụ, account, balance, financial transaction, refresh-token history, user-session history và audit.
- Redis chỉ giữ active-session pointer, rate-limit counter và cache tham chiếu an toàn nếu thực sự cần. Không cache balance.
- SignalR chỉ gửi realtime UX event sau khi database commit và chỉ tới active-session group. Client tự phục hồi connection với backoff, nhưng event có thể bị bỏ lỡ nên luôn phải gọi lại API để lấy balance chính xác.
- OpenAI chỉ diễn giải kết quả read-only do backend đã authorization và tính toán xác định.
- Statement là read model từ `FinancialTransaction`; số dư account và ledger được đọc trong một transaction `RepeatableRead` ngắn với cùng `asOfUtc`, rồi giải phóng transaction trước khi QuestPDF render. Không có bảng Statement và không cần Chromium.

## Luồng Application Log

API dùng Serilog tại composition root để nhận toàn bộ event từ abstraction `ILogger`; Domain và Application không phụ thuộc trực tiếp Serilog. Console JSON là đầu ra chính cho platform collector. Rolling file chỉ bật mặc định trong Development và có giới hạn ngày, số file, dung lượng.

`CorrelationIdMiddleware` tạo hoặc kiểm tra correlation ID, mở logging scope và trả mã trong response. Sau routing, request middleware ghi method, route template, status và elapsed time; không ghi raw path, route value, query hoặc body. `ApiExceptionHandler` ghi exception kỹ thuật và trả ProblemDetails có cùng correlation ID. EF interceptor chỉ tự bổ sung mã này cho `AuditLog` được tạo trong request hiện tại.

Chi tiết vận hành và ranh giới dữ liệu nằm tại [Application-Logging.md](Application-Logging.md). Technical log không phải SQL audit, ledger hoặc cơ chế backup.

## Staff Portal và phân quyền

Teller và Admin dùng chung Staff Portal/layout để giảm số lượng UI phải bảo trì, nhưng vẫn là hai role và hai demo user riêng:

```text
teller → Teller
admin  → Admin
```

Teller chỉ nhìn thấy và truy cập dashboard cùng chức năng nộp tiền mặt. Admin chỉ nhìn thấy và truy cập dashboard, Quản lý người dùng và Nhật ký kiểm toán. Trang Quản lý người dùng hiển thị toàn bộ tài khoản, cho phép lọc trạng thái hoạt động/bị khóa và chỉ cung cấp thao tác mở khóa khi tài khoản thực sự đang bị khóa. Việc ẩn navigation chỉ phục vụ UX; API policy vẫn bắt buộc kiểm tra role.

Một user không được gán đồng thời Teller và Admin trong seed mặc định. Cách tách này giữ nguyên separation of duties: người tạo cash deposit không đồng thời có quyền mở khóa user hoặc xử lý administrative request.

## Topology theo môi trường

### Development

Blazor Client và API chạy trên hai HTTPS localhost port riêng. Client gọi API bằng CORS với exact origin và credential policy rõ ràng.

### Production/demo

ASP.NET Core API phục vụ Blazor WASM static assets dưới cùng một HTTPS origin:

```text
https://subank-demo.example/
├── /             Blazor WASM và SPA fallback
├── /api/*        REST API
├── /hubs/*       SignalR
└── /health       Health check
```

Việc đóng gói cùng origin chỉ là quyết định deployment/authentication. Ranh giới Client, API, Application, Domain và Infrastructure vẫn được giữ nguyên trong code.
