# Kiến trúc hệ thống SUBank

## Kiểu kiến trúc

SUBank là một ứng dụng monolith được tổ chức theo **kiến trúc phân lớp, tham khảo các nguyên tắc của Clean Architecture**. Hệ thống có một backend deployable chính, không sử dụng microservices, Event Bus hoặc distributed database.

Project hiện sử dụng service abstraction và service implementation để tổ chức nghiệp vụ; không triển khai CQRS hoặc MediatR.

Luồng xử lý chính:

Blazor WebAssembly Client
        ├── HTTPS REST/JSON: request-response và nghiệp vụ chính
        └── SignalR: realtime notification
                        ↓
ASP.NET Core API
        ↓
Application Abstraction / Rule
        ↓
Infrastructure Service Implementation
        ↓
EF Core / Identity / Redis / các provider hạ tầng
        ↓
SQL Server

REST API và SignalR hoạt động song song nhưng không thay thế nhau. Login, account query, transfer, Teller Cash Deposit và Admin operation được xử lý qua REST API.

SignalR được sử dụng cho các thông báo realtime như thay đổi giao dịch, số dư hoặc yêu cầu đăng xuất. Dữ liệu nhận qua SignalR không được xem là nguồn sự thật; Client có thể gọi lại REST API để lấy trạng thái hiện tại từ backend.

Controller chịu trách nhiệm tiếp nhận request, đọc authentication context, kiểm tra các thông tin HTTP cần thiết và gọi service abstraction. Controller không trực tiếp query banking `DbContext` hoặc chứa logic thay đổi balance.

## Trách nhiệm của từng project

### `SUBank.Domain`

Chứa các entity, enum và một số business rule cốt lõi không phụ thuộc vào UI hoặc hạ tầng bên ngoài.

Domain không phụ thuộc trực tiếp vào EF Core, ASP.NET Core, Redis, SignalR hoặc Blazor.

### `SUBank.Contracts`

Chứa các request/response DTO và kiểu dữ liệu dùng để trao đổi giữa API và Client.

Contracts không expose trực tiếp các thông tin nội bộ như password hash, refresh-token hash, secret hoặc persistence detail không cần thiết.

### `SUBank.Application`

Chứa các abstraction và business rule được các phần khác của hệ thống sử dụng.

Application định nghĩa các interface như banking service, authentication service, session service, profile service, statement service và các abstraction liên quan.

Application phụ thuộc vào Domain và Contracts nhưng không trực tiếp cấu hình SQL Server, Redis hoặc ASP.NET Core Identity.

Trong phiên bản hiện tại, một phần orchestration nghiệp vụ vẫn được triển khai trong các service thuộc Infrastructure. Vì vậy SUBank được mô tả là kiến trúc phân lớp tham khảo Clean Architecture, không phải Clean Architecture thuần túy.

### `SUBank.Infrastructure`

Chứa các implementation liên quan đến hạ tầng và phần lớn service implementation của hệ thống hiện tại.

Infrastructure chịu trách nhiệm cho các thành phần như:

- Entity Framework Core và SQL persistence.
- ASP.NET Core Identity.
- Banking service và Staff service implementation.
- Authentication và session persistence.
- Redis active-session integration.
- QR processing.
- PDF generation.
- Realtime notifier và các provider hạ tầng khác.

Infrastructure triển khai các abstraction được định nghĩa ở Application khi phù hợp.

### `SUBank.Api`

Là entry point và composition root phía server.

API chứa:

- Controller.
- Middleware.
- Authentication và authorization configuration.
- SignalR Hub.
- Health Check.
- Swagger/OpenAPI.
- Dependency Injection registration.
- Logging configuration.

API không trực tiếp chứa core logic thay đổi balance; các nghiệp vụ này được chuyển cho service tương ứng xử lý.

### `SUBank.Client`

Là Blazor WebAssembly frontend.

Client chứa page/component theo chức năng, API client, authentication state, navigation theo role, SignalR connection và giao diện QR.

Client giao tiếp với backend thông qua API và không truy cập trực tiếp SQL Server hoặc `DbContext`.

## Luồng authentication và session

ASP.NET Core Identity được sử dụng để xác minh thông tin đăng nhập và quản lý user/role.

Sau khi đăng nhập thành công, backend cấp JWT Access Token và quản lý session của user. Refresh Token được lưu trong HttpOnly `Secure` cookie, còn Access Token chỉ được giữ trong memory của Blazor Client và không được lưu persistent trong `localStorage` hoặc `sessionStorage`.

Redis được sử dụng để kiểm soát active session. Protected request phải vượt qua authentication và session validation trước khi được thực hiện nghiệp vụ.

Customer có phiên hết hạn tuyệt đối sau 15 phút kể từ lúc đăng nhập. Client theo dõi thời gian còn lại và kiểm tra lại trạng thái khi tab trở về foreground.

Teller và Admin sử dụng Access Token 15 phút, có thể refresh theo nhu cầu và có logical session tối đa bảy ngày.

Hệ thống hỗ trợ **Single Active Session**. Khi user đăng nhập bằng session mới, session cũ không còn được chấp nhận cho protected API.

Client có xử lý việc đồng bộ login, refresh và logout giữa nhiều tab để hạn chế xung đột khi các tab cùng sử dụng refresh cookie.

Khi logout hoặc session bị thu hồi, backend cập nhật trạng thái session trước khi thực hiện các thao tác realtime hoặc Redis cleanup liên quan. SignalR hỗ trợ Client phản ứng nhanh hơn nhưng không phải lớp bảo mật có thẩm quyền.

Chi tiết cơ chế token, cookie, Redis session, Web Lock, multi-tab handling và session revocation nằm trong Security-Design.md.

## Nguồn dữ liệu và external service

- SQL Server là nguồn dữ liệu chính cho Customer Profile, Bank Account, Financial Transaction, session history, refresh-token data và Audit Log.
- Redis được sử dụng cho Active Session và một số cơ chế hỗ trợ như rate limiting; không dùng Redis làm nguồn sự thật cho balance.
- SignalR cung cấp realtime notification sau nghiệp vụ. Nếu event bị mất, Client vẫn có thể gọi REST API để lấy dữ liệu hiện tại.
- Statement được tạo từ dữ liệu account và `FinancialTransaction`; hệ thống không tạo bảng Statement riêng.
- PDF statement được tạo ở backend.

Các chức năng AI hiện chưa được triển khai nên không nằm trong kiến trúc runtime hiện tại.

## Luồng Application Log

API sử dụng structured logging thông qua `ILogger` và cấu hình logging ở server.

Application log được ghi ra console. Trong môi trường Development, rolling file được bật để hỗ trợ theo dõi và debug.

`CorrelationIdMiddleware` sử dụng correlation ID để liên kết request với log và các thông tin lỗi liên quan.

Request logging chỉ ghi các thông tin cần thiết như HTTP method, route template, status code, thời gian xử lý và correlation context; không chủ động ghi request body, password, token hoặc secret.

Exception được xử lý ở tầng API và trả response lỗi phù hợp mà không expose thông tin nhạy cảm không cần thiết.

Chi tiết vận hành và phạm vi dữ liệu log nằm tại Application-Logging.md. Application Log không thay thế Audit Log, Financial Transaction hoặc cơ chế backup.

## Staff Portal và phân quyền

Teller và Admin có thể dùng chung Staff Portal/layout để giảm số lượng UI phải bảo trì, nhưng vẫn là hai role và hai demo user riêng:

Teller truy cập các chức năng thuộc nghiệp vụ Teller như Cash Deposit.

Admin truy cập các chức năng quản lý Customer và Audit Log.

Trang quản lý Customer hỗ trợ tìm kiếm, xem chi tiết, khóa và mở khóa Customer. Identity Lockout và Admin Suspension là hai trạng thái độc lập.

Khóa Customer bởi Admin yêu cầu lý do, ghi Audit Log và thu hồi active session của Customer. Mở Admin Suspension không tự động xóa Identity Lockout và ngược lại.

Hệ thống không cung cấp thao tác xóa Customer thật.

Việc ẩn navigation theo role chỉ phục vụ UX. Backend vẫn phải kiểm tra authorization cho mỗi protected endpoint.

Một user không được gán đồng thời Teller và Admin trong Development Seed Data mặc định. API quản lý Customer không được dùng để khóa hoặc chỉnh sửa Teller/Admin.

## Topology theo môi trường

### Development

Blazor Client và API chạy trên hai HTTPS localhost port riêng.

Client gọi API qua HTTPS và CORS chỉ cho phép origin Development đã được cấu hình.

SQL Server chạy local và Redis mặc định tại `localhost:6379`.

### Production/demo

Khi publish API, Blazor WebAssembly Client được publish cùng artifact và có thể được phục vụ dưới cùng một HTTPS origin:

```text
https://subank-demo.example/
├── /             Blazor WASM và SPA fallback
├── /api/*        REST API
├── /hubs/*       SignalR
└── /health       Health Check
```

Cách triển khai cùng origin giúp giảm độ phức tạp của CORS và cookie trong môi trường demo/Production.

Việc đóng gói cùng origin chỉ là quyết định deployment. Ranh giới giữa Client, API, Application, Domain và Infrastructure vẫn được giữ trong source code.