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

REST/JSON và SignalR hoạt động song song, không thay thế nhau. Login, account query, transfer, Teller Cash Deposit và Admin operation luôn đi qua REST API. SignalR chỉ thông báo `ForceLogout`, `TransactionReceived` hoặc `BalanceChanged`; khi nhận event, Client phải gọi REST API để lấy dữ liệu có thẩm quyền từ SQL.

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

## Nguồn dữ liệu và external service

- SQL Server/Azure SQL là nguồn sự thật duy nhất cho user profile nghiệp vụ, account, balance, financial transaction, refresh-token history, user-session history và audit.
- Redis chỉ giữ active-session pointer, rate-limit counter và cache tham chiếu an toàn nếu thực sự cần. Không cache balance.
- SignalR chỉ gửi realtime UX event sau khi database commit. Client phải gọi lại API để lấy balance chính xác.
- OpenAI chỉ diễn giải kết quả read-only do backend đã authorization và tính toán xác định.

## Staff Portal và phân quyền

Teller và Admin dùng chung Staff Portal/layout để giảm số lượng UI phải bảo trì, nhưng vẫn là hai role và hai demo user riêng:

```text
teller → Teller
admin  → Admin
```

Teller chỉ nhìn thấy và truy cập dashboard cùng Cash Deposit. Admin chỉ nhìn thấy và truy cập dashboard, Locked Users, Address Requests và Audit Logs. Việc ẩn navigation chỉ phục vụ UX; API policy vẫn bắt buộc kiểm tra role.

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
