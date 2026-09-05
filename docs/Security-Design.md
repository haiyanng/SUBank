# Thiết kế bảo mật SUBank

## Trạng thái

Tài liệu này mô tả các cơ chế bảo mật đang có trong code hiện tại của SUBank.

Các cơ chế chính đã được triển khai gồm:

- ASP.NET Core Identity cho user, password, role và Identity Lockout.
- JWT Access Token.
- Refresh Token bằng `HttpOnly` cookie.
- Refresh Token rotation.
- SQL `UserSession` và `RefreshToken`.
- Redis Active Session để giới hạn một phiên đang hoạt động cho mỗi user.
- Role-based authorization và kiểm tra ownership ở backend.
- Admin Suspension tách riêng với Identity Lockout.
- CSRF protection cho các endpoint sử dụng refresh cookie.
- Rate Limiting cho các nghiệp vụ nhạy cảm.
- Idempotency và optimistic concurrency cho giao dịch.
- SignalR notification theo active session.
- Client session coordination giữa nhiều tab bằng Web Locks và browser storage.
- Audit Log và Application Log.

Một số cơ chế đã có code nhưng vẫn cần kiểm chứng thêm bằng browser thực tế hoặc môi trường Production, được ghi ở phần cuối tài liệu.

## Authentication và Identity

SUBank sử dụng ASP.NET Core Identity thông qua `ApplicationUser`.

Ba role nghiệp vụ hiện tại là:

```text
Customer
Teller
Admin
```

API hiện không có chức năng Customer tự đăng ký tài khoản.

Customer đăng nhập bằng số điện thoại.

Đối với Customer, backend kiểm tra đồng thời:

```text
ApplicationUser.UserName
        =
CustomerProfile.Phone
        =
username được nhập khi login
```

Số điện thoại Customer phải có định dạng hợp lệ theo rule của hệ thống.

Teller và Admin sử dụng username nghiệp vụ và không áp dụng rule Customer Phone nói trên.

Login password được ASP.NET Core Identity quản lý và hash theo cơ chế của framework.

SUBank còn có Transaction Password riêng dành cho nghiệp vụ chuyển tiền.

```text
Login Password
→ xác thực đăng nhập

Transaction Password
→ xác thực giao dịch chuyển tiền
```

`TransactionPasswordHash` được lưu trong `ApplicationUser` và được kiểm tra bằng `IPasswordHasher<ApplicationUser>`.

Plaintext password và Transaction Password không được lưu trong database.

Request logging hiện tại không ghi request body nên password gửi trong login/transfer không được đưa vào Application Log bởi middleware request logging.

## Identity lockout và Admin suspension

SUBank có hai cơ chế khóa tài khoản độc lập:

```text
Identity Lockout
        +
Admin Suspension
```

### Identity Lockout

Identity Lockout sử dụng cơ chế có sẵn của ASP.NET Core Identity.

Cấu hình hiện tại:

```text
MaxFailedAccessAttempts = 3
DefaultLockoutTimeSpan  = 15 phút
```

Khi login sai password:

```text
Login sai
   ↓
AccessFailedAsync
   ↓
AccessFailedCount tăng
   ↓
đủ 3 lần
   ↓
Identity Lockout 15 phút
```

Khi lockout được kích hoạt, SUBank còn ghi `LockedAtUtc` để phục vụ hiển thị và audit.

Nếu user đang có active session tại thời điểm bị Identity Lockout, backend thu hồi session tương ứng.

Login thành công reset `AccessFailedCount`.

Admin có endpoint để mở Identity Lockout của Customer trước khi hết 15 phút.

Thao tác mở lockout:

```text
LockoutEnd = null
AccessFailedCount = 0
LockedAtUtc = null
```

và tạo Audit Log.

### Admin Suspension

Admin Suspension là cơ chế riêng, không sử dụng `LockoutEnd`.

Các field liên quan gồm:

```text
IsAdminSuspended
AdminSuspendedAtUtc
AdminSuspensionReason
AdminSuspendedByUserId
```

Admin chỉ có thể suspend user thực sự có role `Customer` và có `CustomerProfile`.

Teller hoặc Admin không phải target hợp lệ của Customer Management.

Lý do khóa sau khi trim phải có:

```text
3 - 500 ký tự
```

Khi Admin suspend Customer:

```text
Admin
  ↓
đặt IsAdminSuspended = true
  ↓
ghi metadata suspension
  ↓
revoke UserSession trong SQL
  ↓
revoke RefreshToken trong SQL
  ↓
ghi AuditLog
  ↓
COMMIT SQL
  ↓
revoke Redis session
  ↓
SignalR ForceLogout
```

Việc cập nhật suspension, SQL session, refresh token và Audit Log được thực hiện trước khi commit transaction.

Redis revoke và SignalR `ForceLogout` được thực hiện sau commit theo cơ chế best-effort.

Resume Customer chỉ xóa trạng thái Admin Suspension.

```text
Resume Admin Suspension
≠
Clear Identity Lockout
```

Hai cơ chế khóa không tự động xóa trạng thái của nhau.

API hiện không có endpoint xóa Customer.

## Token và cookie

SUBank sử dụng hai loại token:

```text
Access Token
Refresh Token
```

### Access Token

Access Token là JWT.

JWT hiện chứa các claim chính:

```text
sub
jti
NameIdentifier
Name
sid
Role
```

Trong đó:

```text
sub
→ UserId

sid
→ SessionId

Role
→ Customer / Teller / Admin
```

JWT được ký bằng symmetric signing key sử dụng HMAC SHA-256.

Backend kiểm tra:

```text
Issuer
Audience
Lifetime
Signing Key
```

Signing key phải có tối thiểu 32 byte.

Giá trị mặc định hiện tại:

```text
AccessTokenMinutes      = 15
CustomerSessionMinutes  = 15
RefreshTokenDays        = 7
```

Đối với Customer:

```text
Login
  ↓
Access Token
  ↓
hết hạn tại deadline của Customer Session
  ↓
15 phút
```

Customer Session là absolute session, không được kéo dài bằng refresh.

Đối với Teller/Admin:

```text
Access Token
→ 15 phút

Logical Session / Refresh Token
→ tối đa 7 ngày
```

Teller/Admin có thể refresh Access Token khi logical session vẫn còn hiệu lực.

### Access Token trên Client

Blazor Client giữ Access Token trong object `ApiSession.Current` ở memory của WebAssembly.

Code hiện tại không lưu Access Token vào:

```text
localStorage
sessionStorage
```

Khi reload trang, Client sử dụng refresh cookie để khôi phục session thay vì lấy Access Token từ browser storage.

### Refresh Token

Refresh Token được tạo bằng random 64 byte.

Raw Refresh Token được gửi cho browser bằng cookie:

```text
subank_refresh
```

Cookie hiện được cấu hình:

```text
HttpOnly = true
Secure   = true
SameSite = Strict
Path     = /api/auth
```

Do `HttpOnly`, JavaScript không trực tiếp đọc được Refresh Token.

SQL không lưu raw Refresh Token.

Backend hash token bằng SHA-256 và chỉ lưu:

```text
TokenHash
```

### Refresh Token rotation

Mỗi lần refresh thành công:

```text
Refresh Token cũ
      ↓
revoke
      ↓
tạo Refresh Token mới
      ↓
ReplacedByTokenId
```

Token mới tiếp tục sử dụng cùng `SessionId`.

Backend sử dụng SQL locking và conditional update để hạn chế hai request đồng thời cùng rotate một refresh token.

Cấu hình hiện tại có:

```text
RefreshConcurrencyGraceSeconds = 30
```

Nếu token vừa bị rotate và request cạnh tranh xuất hiện trong grace period, backend trả conflict thay vì coi ngay là token theft.

Nếu refresh token đã bị thay thế bị reuse ngoài khoảng grace, backend thu hồi logical session, ghi Audit Log và gửi `ForceLogout` best-effort.

Refresh Token mới không được kéo dài quá `UserSession.ExpiresAtUtc` ban đầu.

## Authorization

SUBank sử dụng cả:

```text
Role-based authorization
        +
Resource ownership
```

Backend có fallback authorization policy:

```text
endpoint không explicit AllowAnonymous
→ phải authenticated
```

Các endpoint public như login, refresh và health check được khai báo riêng.

### Customer

Các API Customer quan trọng yêu cầu role `Customer`.

Customer chỉ được đọc tài khoản thuộc chính mình.

Ownership được kiểm tra bằng `UserId` từ JWT kết hợp với quan hệ:

```text
ApplicationUser
        ↓
CustomerProfile
        ↓
BankAccount
```

Ví dụ khi đọc account:

```text
Account.CustomerProfile.UserId
        =
JWT UserId
```

Khi đọc transaction, backend cũng giới hạn transaction theo account thuộc Customer đó.

Thay đổi Account Number hoặc Reference Number trên request không tự tạo quyền truy cập dữ liệu của Customer khác.

### Teller

Cash Deposit yêu cầu:

```text
Role = Teller
```

Admin không tự động có quyền gọi Teller endpoint.

### Admin

Customer Management và Audit Log yêu cầu:

```text
Role = Admin
```

Teller không có quyền gọi các Admin endpoint.

Việc Client ẩn hoặc hiện menu theo role chỉ phục vụ UI.

Authorization thực sự được thực hiện tại API/backend.

## Active session và Redis

SUBank triển khai Single Active Session.

Mỗi user chỉ có một `SessionId` được Redis xem là active tại một thời điểm.

Redis key có dạng:

```text
subank:active-session:{UserId}
```

Value là:

```text
SessionId
```

Ví dụ:

```text
subank:active-session:user-123
        ↓
abc123-session
```

Khi login mới:

```text
User login
   ↓
tạo SessionId mới
   ↓
tạo UserSession trong SQL
   ↓
tạo RefreshToken
   ↓
Redis Replace active SessionId
   ↓
session cũ bị revoke
```

Redis sử dụng Lua script cho các thao tác:

```text
Replace
Renew
Revoke
```

để việc kiểm tra và thay đổi key diễn ra nguyên tử.

### Kiểm tra protected request

Sau JWT authentication, `ActiveSessionMiddleware` đọc:

```text
UserId
SessionId (sid)
```

và gọi `ActiveSessionValidator`.

Validator kiểm tra hai lớp:

```text
1. Redis
   SessionId có phải active session hiện tại?

2. SQL
   UserSession có tồn tại?
   chưa revoked?
   chưa expired?
   user còn active?
   không Admin Suspended?
   không Identity Locked?
```

Vì vậy Redis không phải lớp kiểm tra duy nhất.

```text
JWT
 ↓
Redis Active Session
 ↓
SQL UserSession + User state
 ↓
Authorization
 ↓
API
```

Nếu session không còn hợp lệ, request bị từ chối bằng `401`.

Nếu Redis hoặc dependency kiểm soát session không khả dụng, hệ thống không bỏ qua kiểm tra mà trả lỗi dependency, được API map thành `503`.

Đây là cơ chế fail-closed.

### SQL và Redis

Vai trò hai nguồn khác nhau:

```text
SQL Server
→ lịch sử UserSession
→ expiry
→ revocation
→ trạng thái user

Redis
→ SessionId nào đang active ngay lúc này
```

Redis không lưu balance và không được dùng để xác định số dư tài khoản.

## Client session và nhiều tab

Blazor Client triển khai thêm cơ chế điều phối session giữa nhiều browser tab.

### Tab Session Binding

Mỗi tab lưu SessionId của chính nó trong:

```text
sessionStorage
```

Key:

```text
subank_tab_session_id
```

SessionId này không phải credential.

Nó dùng để ngăn một tab cũ âm thầm nhận nhầm session mới từ shared refresh cookie.

### Logout Intent

Trạng thái session đang hoặc đã bị logout được lưu trong:

```text
localStorage
```

Key:

```text
subank_logout_pending
```

Dữ liệu này cũng không phải credential.

Client sử dụng:

```text
storage event
BroadcastChannel
visibilitychange
focus
pageshow
```

để các tab nhận biết thay đổi session.

### Web Locks

Các request làm thay đổi refresh cookie:

```text
login
refresh
logout
```

được điều phối bằng Web Locks API.

Lock hiện có tên:

```text
subank-auth-cookie-http
```

Mục tiêu là tránh hai tab đồng thời thay đổi shared refresh cookie theo thứ tự không kiểm soát.

Cấu hình Client hiện tại:

```text
chờ Web Lock tối đa: 45 giây
auth request timeout: 30 giây
```

Nếu browser không hỗ trợ Web Locks, Client từ chối thực hiện cookie-auth flow thay vì chạy không có coordination.

Code hiện hướng tới Chrome/Edge hiện đại cho cơ chế này.

### Stale response protection

`ApiSession` có `sessionGeneration`.

Khi session thay đổi trong lúc một protected request đang chạy:

```text
request cũ
   ↓
session thay đổi
   ↓
response cũ quay về
   ↓
generation mismatch
   ↓
bỏ response
```

Request cũ không được tự động áp dụng vào session mới.

Customer không dùng proactive refresh cho protected request.

Teller/Admin có thể refresh Access Token khi token sắp hết hạn và retry protected request tối đa một lần sau authentication failure.

## Logout

Logout được thiết kế để thu hồi logical session chứ không chỉ xóa UI.

Client trước tiên:

```text
Clear local logical session
        ↓
block tab restore
        ↓
gửi logout request
```

Logout request sử dụng:

```text
Refresh Cookie
X-SUBank-CSRF
X-SUBank-Session-ID
```

Backend kiểm tra SessionId mà tab muốn logout.

Nếu refresh cookie vẫn thuộc đúng session:

```text
revoke UserSession
revoke toàn bộ RefreshToken của session
commit SQL
revoke Redis
SignalR ForceLogout
```

Nếu shared cookie đã thuộc một session khác nhưng tab cũ vẫn có Access Token hợp lệ của session cũ, backend có bearer-based fallback để chỉ revoke session cũ.

Endpoint:

```text
/api/auth/reject-session
```

cho phép thu hồi session xác định bởi:

```text
JWT sub
JWT sid
```

mà không cần sử dụng refresh cookie.

Client chỉ coi cookie logout được xác nhận khi server trả marker thu hồi session phù hợp.

Nếu server chưa xác nhận logout, Client vẫn khóa local UI và không tự restore session đó.

## CSRF, CORS và HTTPS

### CSRF

Hai endpoint sử dụng refresh cookie:

```text
POST /api/auth/refresh
POST /api/auth/logout
```

được bảo vệ bởi `RefreshCookieProtectionMiddleware`.

Request phải có:

```text
X-SUBank-CSRF: 1
```

Nếu request có header `Origin`, backend chỉ chấp nhận:

```text
same origin

hoặc

Development Client origin được cấu hình
```

Refresh cookie còn sử dụng:

```text
SameSite=Strict
```

để bổ sung lớp bảo vệ đối với cross-site request.

### CORS

Trong Development, API cấu hình một Client origin cụ thể từ:

```text
Cors:ClientOrigin
```

và cho phép credential.

CORS policy này chỉ được bật trong Development.

Production/demo được thiết kế để Client và API chạy cùng origin.

### HTTPS

Refresh cookie luôn có:

```text
Secure = true
```

Ngoài Development, API bật:

```text
HSTS
HTTPS Redirection
```

Nếu triển khai sau reverse proxy và bật forwarded headers, code yêu cầu phải cấu hình `KnownProxies`.

## SignalR security

`BankingHub` yêu cầu authenticated user.

Khi kết nối Hub:

```text
JWT
 ↓
UserId + sid
 ↓
ActiveSessionValidator
 ↓
valid
 ↓
join group session:{SessionId}
```

Nếu SessionId không hợp lệ, Hub abort connection.

SignalR endpoint cho phép JWT access token được truyền qua `access_token` query parameter riêng cho:

```text
/hubs/banking
```

Hub được cấu hình:

```text
CloseOnAuthenticationExpiration = true
```

Các realtime event hiện có gồm:

```text
BalanceChanged
TransactionReceived
ForceLogout
```

`BalanceChanged` và `TransactionReceived` chỉ được gửi tới active session sau khi backend kiểm tra lại active session.

`ForceLogout` được gửi theo SessionId.

SignalR là lớp hỗ trợ realtime UX.

Nó không phải nguồn có thẩm quyền quyết định authentication hoặc balance.

Nếu SignalR gửi thất bại, dữ liệu nghiệp vụ đã commit trong SQL không bị rollback.

## Bảo vệ nghiệp vụ tiền

### Transfer

Transfer yêu cầu:

```text
Role Customer
```

và được bảo vệ thêm bằng Rate Limiting.

Backend kiểm tra:

```text
User còn active
không Admin Suspended
không Identity Locked
Source Account hợp lệ
Destination Account hợp lệ
Source != Destination
Source thuộc Customer hiện tại
hai account đang Active
Amount hợp lệ
đủ Balance
Transaction Password đúng
Idempotency-Key hợp lệ
```

Transaction Password được verify từ `TransactionPasswordHash`.

### SQL Transaction

Transfer chạy trong SQL transaction:

```text
Debit Source Account
        +
Credit Destination Account
        +
Create FinancialTransaction
        +
Create AuditLog
        ↓
COMMIT
```

Nếu persistence thất bại:

```text
ROLLBACK
```

Sau khi commit thành công mới gửi realtime notification.

### Idempotency

Transfer bắt buộc header:

```text
Idempotency-Key
```

Backend tạo Request Hash từ nội dung nghiệp vụ.

Nếu cùng request được gửi lại với cùng key:

```text
không chuyển tiền lần hai
→ trả lại transaction trước
```

Nếu cùng key nhưng payload khác:

```text
Conflict
```

Cash Deposit của Teller cũng áp dụng Idempotency Key.

### Concurrency

`BankAccount.RowVersion` được cấu hình làm optimistic concurrency token.

Nếu hai request cùng cập nhật balance và xảy ra concurrency conflict:

```text
DbUpdateConcurrencyException
        ↓
Conflict
```

Cơ chế này kết hợp với SQL transaction để giảm nguy cơ double-spend trong phạm vi project.

Balance luôn được đọc và cập nhật trong SQL Server.

Redis không tham gia tính toán balance.

## Input, enumeration và dữ liệu nhạy cảm

Validation quan trọng được thực hiện tại server, không chỉ dựa vào Client.

Các rule hiện có kiểm tra những dữ liệu như:

```text
login shape
Account Number
Amount
Transaction Password
Idempotency Key
Description
Admin Suspension Reason
QR payload
```

Account resolution sử dụng exact Account Number và được Rate Limit.

Endpoint account resolution hiện được phép cho:

```text
Customer
Teller
```

QR Decode chỉ cho role Customer, có Rate Limiting và giới hạn file:

```text
PNG
JPEG
WebP

tối đa 5 MB
```

Backend không sử dụng browser-supplied role để quyết định quyền.

Role và UserId được lấy từ JWT đã được server xác thực.

## Rate Limiting

Các policy được cấu hình trong API gồm:

```text
Login
→ 30 request / phút
→ partition theo IP

AccountResolution
→ 20 request / phút
→ partition theo username hoặc IP

TransactionPassword
→ 10 request / phút
→ partition theo UserId hoặc IP

CashDeposit
→ 20 request / phút
→ partition theo UserId hoặc IP

QrDecode
→ 15 request / phút
→ partition theo UserId
```

Code hiện áp dụng:

```text
Login
→ Login policy

Transfer
→ TransactionPassword policy

Teller Cash Deposit
→ CashDeposit policy

Account Resolve
→ AccountResolution policy

QR Decode
→ QrDecode policy
```

Khi vượt giới hạn, API trả:

```text
429 Too Many Requests
```

## Logging, audit và secret

SUBank sử dụng Application Log và Audit Log với mục đích khác nhau.

```text
Application Log
→ lỗi kỹ thuật
→ HTTP request summary
→ dependency failure

Audit Log
→ sự kiện nghiệp vụ và bảo mật
```

Request logging hiện chỉ ghi:

```text
HTTP Method
Route Template
Status Code
Elapsed Time
```

Middleware không đọc hoặc ghi:

```text
Request Body
Cookie
Authorization Header
Query value
raw route value
```

Correlation ID được nhận qua:

```text
X-Correlation-ID
```

Giá trị do Client gửi chỉ được chấp nhận nếu:

```text
1 - 100 ký tự
```

và chỉ gồm:

```text
A-Z
a-z
0-9
-
_
.
```

Nếu không hợp lệ, server tự tạo GUID.

API lỗi trả `ProblemDetails` có `correlationId`.

Các secret/config quan trọng gồm:

```text
ConnectionStrings:DefaultConnection
Jwt:SigningKey
ActiveSession:RedisConnection
```

`appsettings.json` không chứa giá trị thật cho các secret này.

Ứng dụng fail startup nếu các cấu hình bắt buộc như DB connection, Redis connection hoặc JWT signing key bị thiếu.

Production cũng yêu cầu `AllowedHosts` phải là host cụ thể thay vì `*`.

Chi tiết Application Log nằm trong `Application-Logging.md`.

## Security test tối thiểu

Repo hiện có automated test cho một số luồng bảo mật và giao dịch quan trọng.

`ActiveSessionMiddlewareTests` kiểm tra:

```text
active session được chấp nhận
replaced session bị từ chối
session dependency unavailable → fail closed
```

Integration tests hiện kiểm tra các luồng như:

```text
login → me → refresh → logout

login lần hai
→ access token của session cũ bị vô hiệu

login lần hai
→ ForceLogout tới session cũ

sai password 3 lần
→ Identity Lockout

Admin unlock
→ Customer login lại được

anonymous
→ protected API bị 401

Customer
→ Admin endpoint bị 403

Customer
→ Teller endpoint bị 403

Customer A
→ không đọc được account Customer B

Transfer
→ atomic
→ audit
→ idempotent

Transfer lỗi
→ rollback

Concurrent Transfer
→ không tạo negative balance

Teller Cash Deposit
→ atomic
→ audit
→ idempotent

BankAccount RowVersion
→ concurrency token
```

Các test này là automated test thực sự có trong repo hiện tại.

## Nội dung chưa kiểm chứng

Code hiện đã có cơ chế multi-tab, Web Locks, browser storage coordination và session restore protection.

Chưa có browser E2E test riêng để kiểm chứng đầy đủ các trường hợp như:

```text
nhiều tab login đồng thời
nhiều tab refresh đồng thời
logout khi một tab khác vừa login
mất mạng đúng lúc refresh/logout
browser suspend và resume tab
Web Locks behavior trên browser thật
```

Các nội dung Production sau cũng không thể được xác nhận chỉ từ source code:

```text
Production secret đã được cấu hình trên hosting provider
HTTPS certificate thực tế đã hoạt động
Redis Production đã kết nối thành công
SQL Production đã backup/restore thành công
penetration test thực tế
```

Các mục này chỉ nên được đánh dấu hoàn thành sau khi có deployment hoặc test evidence tương ứng.