# Bản thiết kế dự án SUBank V2.4

## Quản lý tài liệu

- Phiên bản: 2.4
- Trạng thái: ĐÃ DUYỆT ĐỂ LẬP KẾ HOẠCH TRIỂN KHAI
- Ngày: 2026-08-26
- Nhóm thực hiện: một sinh viên phát triển với sự hỗ trợ của Codex
- Thời gian bàn giao: bảy ngày
- Tài liệu nguồn: `SUBank_V2.4_Codex_Master_Spec_FINAL.docx`
- Quy tắc ưu tiên: tài liệu trong repository này ghi lại các chỉnh sửa đã thống nhất đối với tài liệu nguồn. Khi hai tài liệu mâu thuẫn, file này được ưu tiên áp dụng cho đến khi tài liệu nguồn được cập nhật và review lại.

## Mục tiêu sản phẩm

SUBank V2.4 là ứng dụng ngân hàng mô phỏng có giao diện responsive. Hệ thống chỉ hỗ trợ luân chuyển tiền nội bộ SUBank và không được tuyên bố có kết nối NAPAS, VietQR, thanh toán liên ngân hàng, eKYC thật hoặc đủ điều kiện hoạt động như ngân hàng thực tế.

Hệ thống được xây dựng dưới dạng modular monolith bằng .NET 10 và Clean Architecture:

- Blazor WebAssembly Client độc lập, sử dụng Bootstrap và CSS tùy chỉnh có phạm vi rõ ràng.
- ASP.NET Core Web API.
- SQL Server ở máy local và Azure SQL cho bản cloud demo.
- Redis chỉ dùng để kiểm soát active session và rate limiting.
- SignalR cung cấp trải nghiệm realtime theo cơ chế best-effort, không bao giờ là nguồn xác thực bảo mật.
- OpenAI API cung cấp trợ lý đọc và phân tích giao dịch trong phạm vi giới hạn.

## Thứ tự ưu tiên bàn giao

### P0 - bắt buộc để demo

- Database schema, constraints, migrations, deterministic seed và hướng dẫn dựng lại database sạch.
- Role và demo user bằng ASP.NET Core Identity.
- Login, refresh, logout, `/me`, khóa sau ba lần sai và Admin mở khóa.
- Customer xem danh sách/chi tiết tài khoản và lịch sử/chi tiết giao dịch đúng quyền.
- Chuyển tiền nội bộ SUBank có transaction password, idempotency, SQL transaction nguyên tử, xử lý concurrency và audit.
- Teller cash deposit có SQL transaction nguyên tử và audit.
- Authenticated shell responsive và navigation thay đổi theo role.
- Unit test và integration test cho authentication, ownership, transfer, concurrency và deposit.

### Development Seed Data tối thiểu

Seed phải tạo bốn user riêng biệt bằng ASP.NET Core Identity:

| User | Role | Mục đích demo |
|---|---|---|
| `0900000001` | Customer | Chuyển tiền, xem account/history, tạo QR |
| `0900000002` | Customer | Nhận tiền, quét QR và kiểm chứng realtime |
| `teller` | Teller | Thực hiện Cash Deposit |
| `admin` | Admin | Mở khóa user và xem Audit Log |

Teller và Admin là hai user riêng, không gán đồng thời hai role này cho một demo user. Password và transaction password Development phải là dữ liệu demo rõ ràng, không tái sử dụng secret thật và được tài liệu hóa an toàn trong README dành cho Development.

### P1 - dự kiến trình diễn

- Redis kiểm soát một active session duy nhất.
- SignalR `ForceLogout` và thông báo số dư/giao dịch sau khi database commit.
- Tạo SUBank QR, quét bằng camera, đọc QR từ ảnh upload và điền trước thông tin transfer.
- Sao kê tháng/năm và xuất PDF ở mức tối thiểu.
- Trợ lý tài chính OpenAI chỉ đọc, sử dụng backend tool xác định và có fallback an toàn.

### P2 - cắt trước nếu có nguy cơ trễ

- Quản lý beneficiary.
- Biểu đồ nâng cao và phần trang trí vượt ngoài visual concept đã khóa.
- Cache dữ liệu tham chiếu.
- Freeze/unfreeze tài khoản.
- Trang trí PDF nâng cao.
- Monitoring độc lập trên cloud khi chưa có credential.

Tính đúng đắn của P0 quan trọng hơn số lượng tính năng P1/P2. Không tính năng P1/P2 nào được làm mất ổn định authentication, authorization, tính đúng của balance hoặc luồng transfer.

## Dependency giữa các project

- `SUBank.Domain` -> không phụ thuộc project nào.
- `SUBank.Contracts` -> không phụ thuộc project nào.
- `SUBank.Application` -> Domain, Contracts.
- `SUBank.Infrastructure` -> Application, Domain.
- `SUBank.Api` -> Application, Infrastructure, Contracts.
- `SUBank.Client` -> chỉ Contracts.

Controller chỉ xử lý vấn đề vận chuyển dữ liệu HTTP. Controller không được query trực tiếp banking `DbContext` hoặc chứa business logic làm thay đổi balance.

Teller và Admin có thể dùng chung Staff Portal/layout để giảm UI trùng lặp, nhưng authorization vẫn tách biệt:

- Cash Deposit yêu cầu role Teller.
- Unlock User và Audit Logs yêu cầu role Admin.
- Teller truy cập Admin endpoint phải nhận `403`.
- Admin truy cập Teller Cash Deposit endpoint phải nhận `403`.
- Navigation ẩn theo role chỉ phục vụ UX và không thay thế API policy.

## Mô hình Identity đã hiệu chỉnh

ASP.NET Core Identity là nguồn sự thật của authentication. Quan hệ role là nhiều-nhiều thông qua `AspNetUserRoles`, dù mỗi demo user của SUBank chỉ được gán một business role.

Sử dụng các field sẵn có của Identity:

- `AccessFailedCount`
- `LockoutEnabled`
- `LockoutEnd`

Không tạo `FailedLoginAttempts` hoặc `IsLocked` custom vì chúng trùng chức năng. `ApplicationUser` có thể bổ sung:

- `LockedAtUtc`: hiển thị thời điểm khóa cho Admin và cung cấp audit context.
- `TransactionPasswordHash`: credential riêng, độc lập với login password.
- `IsActive`.
- `CreatedAtUtc`.

Lần đăng nhập sai thứ ba liên tiếp khóa user cho đến khi Admin reset `LockoutEnd`, `AccessFailedCount` và `LockedAtUtc`. Đăng nhập thành công reset số lần thất bại. Thao tác khóa và mở khóa đều phải được audit.

## Authentication và cookie

### Môi trường Development

Client và API chạy trên hai HTTPS localhost port khác nhau. API phải:

- Chỉ cho phép đúng Client origin đã cấu hình.
- Cho phép gửi credential; không kết hợp credential với wildcard origin.
- Sử dụng refresh cookie có `HttpOnly`, `Secure` và chính sách `SameSite` đã được review.
- Kiểm tra request origin và áp dụng chống CSRF cho refresh/logout sử dụng cookie.

Access token có thời hạn ngắn và chỉ được giữ trong memory của Blazor. Không lưu access token vào `localStorage` hoặc `sessionStorage`. Khi reload trang, Client gọi refresh bằng HttpOnly cookie để nhận access token mới và giữ lại trong memory.

### Môi trường Production/demo

API phục vụ Blazor WASM đã publish dưới cùng một HTTPS origin. REST endpoint dùng `/api`, SignalR dùng `/hubs`, còn client-side route fallback về `index.html`. Cách triển khai này giảm độ phức tạp của CORS, cookie và CSRF nhưng vẫn giữ nguyên ranh giới giữa các project và layer.

## Thiết kế active session

Mỗi user chỉ có một logical active session. Redis lưu active-session pointer với TTL đồng bộ với thời gian sống của refresh/session. Thay thế session phải là thao tác nguyên tử bằng Redis atomic command hoặc Lua script phù hợp; cấm sử dụng RedLock.

Logical session dùng thời hạn tuyệt đối bảy ngày theo cấu hình hiện tại. Mọi refresh token được rotate trong cùng session phải kế thừa `UserSession.ExpiresAtUtc`; không dùng sliding refresh để cộng thêm bảy ngày sau mỗi lần gọi. Redis chỉ renew TTL khi key vẫn chứa đúng `sid` và chỉ renew tới thời gian còn lại của mốc tuyệt đối. Hai refresh cùng token được phân xử bằng atomic conditional update; request thua trong grace period nhận `409` để retry thay vì bị kết luận nhầm là token reuse. `(UserId, SessionId)` là token-family key; logout từ bất kỳ token nhận diện được trong family phải khóa cùng `UserSession` với refresh rồi thu hồi toàn family và Redis, không chỉ token nằm trong cookie hiện tại.

Sau authentication và trước khi chạy nghiệp vụ, protected request kiểm tra `sub` và `sid` trong JWT với Redis. Session thiếu, không khớp hoặc đã revoke không được chạy protected operation. Khi Redis không hoạt động, hệ thống phải fail closed: trả `503` cho dependency unavailable, không được âm thầm bỏ qua session security.

SQL chỉ lưu session history và refresh token dưới dạng hash. Không log raw token, raw refresh token, session identifier, Redis key hoặc secret. SignalR `ForceLogout` gửi đến group của session cũ và chỉ cải thiện UX; Redis cùng middleware mới là lớp bảo mật có thẩm quyền. Hub chỉ chấp nhận `sid` đang active; `BalanceChanged` và `TransactionReceived` chỉ gửi tới group của active `sid`, không gửi theo user group. Client reconnect bằng access token hiện hành và tự retry có backoff, nhưng REST/SQL vẫn là nguồn sự thật nếu event bị bỏ lỡ.

## Biểu diễn tiền

- Độ chính xác trong SQL: `decimal(18,2)`.
- Kiểu dữ liệu .NET: `decimal`; tuyệt đối không dùng `double` hoặc `float`.
- Tiền tệ: chỉ VND.
- Amount phải lớn hơn 0 và có tối đa hai chữ số thập phân.
- UI format theo quy ước VND và thông thường ẩn phần thập phân bằng 0 không có ý nghĩa.

## Khóa nội bộ và identifier dùng ở API

SUBank dùng `long` làm primary key nội bộ cho các bảng nghiệp vụ vì kiểu dữ liệu này nhỏ, tuần tự, dễ debug và có hiệu năng index/join tốt trên SQL Server. Không thêm cột `PublicId` dạng GUID/ULID hàng loạt nếu entity đã có business identifier phù hợp hoặc không cần được truy cập trực tiếp từ Client.

Quy ước theo entity:

| Entity | Khóa nội bộ | Identifier dùng ở API/UI |
|---|---|---|
| `ApplicationUser` | Identity user ID | Lấy từ authentication context; không cho Customer truyền user ID để đọc profile của mình |
| `CustomerProfile` | `long Id` | Không cần public identifier; dùng `/api/profile` |
| `BankAccount` | `long Id` | `AccountNumber` dạng string và unique |
| `FinancialTransaction` | `long Id` | `ReferenceNo` do server tạo và unique |
| `Beneficiary` | `long Id` | Có thể dùng `Id` trong endpoint nhưng bắt buộc kiểm tra ownership |
| `RefreshToken` | `long Id` | Không expose |
| `UserSession` | `long Id` | Không expose |
| `AuditLog` | `long Id` | Chỉ phục vụ authorized Admin query; không cần public GUID |
| `AiQueryLog` | `long Id` | Không expose |

Account number được lưu dạng chuỗi với constraint rõ ràng về độ dài/format, không coi là kiểu số. `ReferenceNo` là business reference để hiển thị, tìm kiếm và đối chiếu khi demo.

Identifier khó đoán không thay thế authorization. Mọi resource endpoint vẫn phải kiểm tra ownership hoặc role. Nếu Customer đổi account number, reference number hoặc beneficiary ID trong URL/DTO, backend không được trả dữ liệu của Customer khác.

Account resolution yêu cầu authentication, account number khớp chính xác, rate limiting và chỉ trả dữ liệu hiển thị người nhận ở mức tối thiểu. Không trả balance hoặc profile. Response dành cho Teller và Customer có thể khác nhau theo use case.

## CustomerProfile và beneficiary đã hiệu chỉnh

Không tạo entity hoặc bảng `CustomerContact`. `CustomerProfile` là nguồn sự thật duy nhất cho thông tin cá nhân và liên hệ của Customer, gồm tối thiểu:

- `FullName`
- `DateOfBirth`
- `IdentityCardNumber` là số CCCD/CMND demo tổng hợp
- `Phone`
- `Email`
- `PermanentAddress`
- `TemporaryAddress`

`ApplicationUser` chỉ phục vụ login/security/role và liên kết 1-0..1 với `CustomerProfile`. Customer bắt buộc có `ApplicationUser.UserName` bằng đúng `CustomerProfile.Phone`; Teller và Admin dùng username nghiệp vụ. `CustomerProfile.Phone` vẫn là nguồn sự thật, còn username Customer là bản sao một chiều phục vụ Identity login. Không dùng Email làm login identifier và không dùng các cột Email/Phone có sẵn trong schema Identity làm nguồn contact nghiệp vụ. Nếu bổ sung luồng đổi số điện thoại, profile và username phải được cập nhật nguyên tử để không khóa nhầm quyền đăng nhập.

Teller và Admin không cần `CustomerProfile` vì họ không phải Customer. Endpoint `/api/profile` xác định Customer từ authentication context và không nhận CustomerId từ Client.

Vì beneficiary chỉ là tài khoản nội bộ SUBank, beneficiary tham chiếu `DestinationAccountId` bằng foreign key thay vì chỉ lưu account-number string chưa được bảo vệ. Áp dụng unique constraint cho `(CustomerId, DestinationAccountId)`.

## Constraint của financial transaction

`FinancialTransaction` chỉ ghi nhận chuyển động tiền đã commit. Những lần thử thất bại được ghi trong `AuditLog`.

Invariant tại Application và database:

- `Amount > 0`.
- `TRANSFER`: source và destination đều bắt buộc và phải khác nhau.
- `CASH_DEPOSIT`: source phải null và destination bắt buộc.
- Mỗi lần thay đổi balance tạo đúng một financial transaction đã commit.
- `ReferenceNo` do server tạo và phải unique.
- Currency của account là VND.
- Balance không được âm.
- Thời gian được lưu theo UTC, ưu tiên `DateTimeOffset` khi phù hợp.

Nếu giữ cột transaction status, phải giải thích mục đích rõ ràng; không được thêm lần thử thất bại thành financial transaction giả.

## Tính đúng đắn của transfer

Transfer yêu cầu:

- Customer đã authentication và có Redis session đang active.
- Source thuộc Customer và đang Active.
- Destination có trạng thái hợp lệ để nhận tiền.
- Source khác destination.
- Amount dương, đúng scale và source có đủ balance theo dữ liệu SQL.
- Transaction password riêng biệt và hợp lệ.
- User xác nhận rõ ràng.
- Idempotency key bắt buộc.

Idempotency key là unique trong phạm vi actor/use case đã authentication. Gửi lại cùng key và payload trả kết quả cũ; dùng cùng key với payload khác trả `409`.

Debit, credit, chèn transaction record và financial success audit cần tính nguyên tử phải chạy trong cùng một SQL transaction. Sử dụng `RowVersion` cho optimistic concurrency. Conflict không an toàn phải rollback và trả `409`; cấm Redis balance cache và distributed lock.

Việc nhập transaction password phải có rate limiting theo user/session, thông báo lỗi an toàn và audit event. Không bao giờ log transaction password hoặc hash của nó.

## Thiết kế QR

SUBank V2.4 tự thiết kế và triển khai **SUBank QR nội bộ**. Hệ thống không tích hợp NAPAS/VietQR, không sử dụng settlement liên ngân hàng và không được mô tả tính năng này như một kết nối thanh toán thật.

Payload phiên bản đầu tiên:

```text
QR tĩnh: subank://transfer?v=1&account=0900000001
QR động: subank://transfer?v=1&account=0900000001&amount=500000&message=TienAn
```

Payload chỉ chứa dữ liệu hỗ trợ nhập transfer gồm version, account number, amount tùy chọn và message tùy chọn. Không đưa CustomerId, internal database ID, balance, password, transaction password, token, session ID hoặc recipient name có thẩm quyền vào QR.

V2.4 bao gồm:

- Tạo SUBank QR tĩnh và động.
- Quét bằng camera qua HTTPS.
- Quét từ ảnh upload để dự phòng khi demo trên laptop.

QR là input không đáng tin cậy và chỉ được dùng để điền trước form transfer thông thường. Phải validate scheme, version, account format, amount range/scale, message length và kích thước payload đã decode. Backend đã authentication luôn resolve tên người nhận từ SQL. User review, transaction password, confirmation, idempotency, ownership, balance và concurrency check vẫn bắt buộc.

Luồng bắt buộc:

```text
Quét camera hoặc upload ảnh QR
        ↓
Client decode và validate cấu trúc ban đầu
        ↓
Authenticated API resolve account number từ SQL
        ↓
Hiển thị người nhận thật và điền trước form Transfer
        ↓
User review amount/message
        ↓
Nhập transaction password và Confirm
        ↓
POST /api/transfers với idempotency key
        ↓
SQL transaction, audit và post-commit SignalR
```

QR không có money-movement engine riêng và không được bỏ qua bất kỳ control nào của transfer thông thường. Camera scan yêu cầu HTTPS; upload ảnh là fallback bắt buộc cho laptop/demo. Nếu cần nói về trải nghiệm tương tự sản phẩm thị trường, chỉ được mô tả đây là luồng mô phỏng quen thuộc, không tuyên bố tương thích hoặc tham gia VietQR/NAPAS.

## Sao kê và PDF

Statement là authorized read model được truy vấn từ `FinancialTransaction`; không tạo bảng Statement. V2.4 có chức năng xuất PDF tối thiểu ở server. Trước khi chọn thư viện phải kiểm tra license, khả năng chạy Docker, tình trạng bảo trì và khả năng render ổn định. Trang trí nâng cao là P2.

## Trợ lý AI

ChatGPT Plus không cung cấp lượt sử dụng OpenAI API. Trợ lý được deploy cần OpenAI API billing riêng, project budget, API key và environment secret.

Trợ lý chỉ expose allow-listed read-only tool. Backend kiểm tra authorization cho mọi tool call của Customer hiện tại và tính kết quả tiền bằng deterministic SQL cùng `decimal`. Không có write tool, arbitrary SQL, balance mutation hoặc Admin action.

AI input, transaction description và provider output đều là dữ liệu không đáng tin cậy. Phải giới hạn input/history, kiểm thử prompt injection, cấu hình timeout, xử lý `429`, rate limit theo user, chỉ gửi dữ liệu tối thiểu và có failure path không cần AI. Core banking vẫn phải hoạt động khi OpenAI unavailable.

## Logging và audit

Phân biệt ba loại dữ liệu:

- `FinancialTransaction`: sự thật về chuyển động tiền đã commit.
- `AuditLog`: actor, hành động, target, kết quả, correlation context và thời gian của sự kiện bảo mật/nghiệp vụ.
- Structured technical log: chẩn đoán và exception thông qua `ILogger`/Serilog.

Global ProblemDetails handling, correlation ID, safe request logging và secret redaction là hạ tầng nền tảng, không phải việc để cuối dự án. Audit cho lần thử thất bại có thể được ghi riêng sau khi financial transaction rollback; lỗi ghi audit phải được technical log nhưng không được tạo financial transaction giả.

## Chính sách kiểm thử

Test phải đi cùng từng vertical slice, không dồn đến cuối:

- Test migration/seed đi cùng data milestone.
- Test login/lockout/refresh đi cùng authentication.
- Test session replacement đi cùng Redis integration.
- Test ownership đi cùng account/transaction endpoint.
- Test validation, idempotency, atomicity và concurrency đi cùng transfer.
- Test role và atomicity đi cùng Teller/Admin operation.
- Test allow-list, injection, timeout và `429` đi cùng AI.

Các bằng chứng UAT `Actual Result`, screenshot, Figma approval, deployment success và human review phải giữ trạng thái `CHỜ THỰC HIỆN` cho đến khi con người thật sự tạo bằng chứng.

## Trình tự triển khai trong bảy ngày

1. Hiệu chỉnh thiết kế, ERD, EF mapping, constraints, migration, deterministic seed và data tests.
2. Identity login/lockout, JWT, refresh-cookie rotation, logout, `/me`, CSRF/CORS, Login UI và auth tests.
3. Account đúng quyền, transaction history/detail, dashboard, ownership tests và responsive shell.
4. Transaction password, idempotent atomic transfer, concurrency, audit, confirmation/result UI và transfer tests.
5. Teller deposit, Admin unlock, Redis session enforcement và SignalR `ForceLogout` nếu P0 vẫn ổn định.
6. QR generate/upload/camera, statement PDF và constrained AI theo đúng thứ tự ưu tiên.
7. Docker same-origin deployment, Azure SQL, Upstash Redis, smoke test, responsive QA, sửa lỗi nghiêm trọng và hoàn thiện tài liệu nộp. Không bắt đầu tính năng mới trong ngày cuối.

## Mục tiêu triển khai demo

- Render free Docker Web Service: API, Blazor đã publish và SignalR.
- Azure SQL free offer: database có thẩm quyền.
- Upstash Redis free tier: active session và rate limiting.
- OpenAI API: external dependency có tính phí riêng theo mức sử dụng thấp.

Tài liệu phải gọi đây là bản demo miễn phí/chi phí thấp, không được cam kết production hosting hoàn toàn miễn phí. Phải ghi lại và kiểm tra trước buổi bảo vệ các giới hạn như sleep, cold start, quota, archival, không có SLA và thay đổi từ provider.

## Phạm vi hoãn và cấm triển khai

- React/Vite.
- Customer self-registration và eKYC thật.
- Thanh toán liên ngân hàng/NAPAS/VietQR thật.
- Redis balance cache, RedLock hoặc SignalR backplane khi chỉ có một API instance.
- ML.NET fraud model.
- Microservices, Kafka, RabbitMQ, Event Bus và Event Sourcing.
- AI write tool hoặc arbitrary SQL.
- Lưu access token trong persistent storage của trình duyệt.
- Admin chỉnh balance trực tiếp.
- FX, loan, card, interest, cheque book và stop cheque.

## Quy tắc kiểm soát thay đổi

Không được âm thầm thay đổi schema, authentication, API, session, money hoặc quyết định bảo mật trong lúc triển khai. Trước khi code, phải ghi lại thay đổi đề xuất, lý do, đánh đổi và quyết định của con người trong blueprint này hoặc một ADR được liên kết.
