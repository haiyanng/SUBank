# Thiết kế Application Log

## Mục tiêu

Application Log của SUBank dùng để theo dõi hoạt động kỹ thuật của ASP.NET Core API, bao gồm:

- request HTTP;
- lỗi hệ thống;
- lỗi dependency;
- thời gian xử lý;
- startup và runtime của API.

Application Log không dùng để lưu giao dịch ngân hàng và không thay thế `AuditLog` hoặc `FinancialTransaction`.

```text
Application Log
→ thông tin kỹ thuật

AuditLog
→ hành động nghiệp vụ và bảo mật

FinancialTransaction
→ giao dịch tiền đã hoàn tất
```

## Công nghệ

API sử dụng Serilog.

Log được ghi dưới dạng JSON Lines bằng `RenderedCompactJsonFormatter`.

Mỗi log event là một JSON object trên một dòng.

Các sink hiện tại:

```text
Console
→ mọi môi trường

Rolling File
→ bật trong Development
```

## Request Logging

`ApplicationRequestLoggingMiddleware` ghi một bản tóm tắt sau khi request hoàn tất.

Thông tin chính gồm:

```text
HTTP Method
Route Template
Status Code
Elapsed Time
Correlation ID
```

Ví dụ route được ghi:

```text
api/accounts/{accountNumber}
```

thay vì ghi Account Number thực tế.

Middleware không chủ động ghi:

```text
Request Body
Response Body
Query String
Cookie
Authorization Header
```

Mức log theo HTTP status:

```text
2xx / 3xx → Information
4xx       → Warning
5xx       → Error
```

Health Check thành công được ghi ở mức `Debug`.

## Correlation ID

SUBank sử dụng header:

```text
X-Correlation-ID
```

để liên kết request, error response, Application Log và Audit Log.

Nếu Client gửi Correlation ID hợp lệ, API sử dụng giá trị đó.

Nếu không có hoặc không hợp lệ, server tự tạo GUID mới.

Correlation ID tối đa 100 ký tự và chỉ chấp nhận:

```text
chữ
số
-
_
.
```

Giá trị này được:

```text
gán vào HttpContext.TraceIdentifier
        ↓
đưa vào logging scope
        ↓
trả lại trong response header
        ↓
đưa vào ProblemDetails
```

Correlation ID chỉ dùng để tra cứu và liên kết log, không dùng để authentication hoặc authorization.

## Bảo vệ dữ liệu nhạy cảm

`SensitiveLogPropertyEnricher` loại bỏ một số property nhạy cảm trước khi log được ghi.

Các property được lọc gồm những nhóm như:

```text
Password
TransactionPassword
AccessToken
RefreshToken
Authorization
Cookie
SigningKey
ConnectionString
ApiKey
TokenHash
SessionId
RedisKey
IdentityCardNumber
AccountNumber
Phone
Email
```

Ngoài ra request logging không đọc body hoặc credential từ request.

Enricher chỉ là lớp bảo vệ bổ sung. Code vẫn không được chủ động đưa password, token, connection string hoặc dữ liệu khách hàng nhạy cảm vào `ILogger`.

## Console và Rolling File

Console JSON được bật ở mọi môi trường.

Trong Development, file logging được bật với cấu hình hiện tại:

```text
Directory              = logs
FileSizeLimitBytes      = 10485760
RetainedFileCountLimit  = 31
RetainedDays            = 14
```

File được roll theo ngày và theo dung lượng.

Dạng file:

```text
src/SUBank.Api/logs/subank-api-YYYYMMDD.log
```

Trong cấu hình gốc, file logging mặc định tắt.

Điều này phù hợp với deployment container, nơi console thường là output chính cho hệ thống hosting thu thập.

## Audit Log

`AuditLog` được lưu trong SQL Server và dùng cho các sự kiện nghiệp vụ hoặc bảo mật.

Ví dụ:

```text
LOGIN_SUCCESS
LOGIN_FAILED
IDENTITY_LOCKOUT_TRIGGERED
TRANSFER
TRANSFER_FAILED
CASH_DEPOSIT
CUSTOMER_SUSPENDED_BY_ADMIN
REFRESH_TOKEN_REUSE
```

Application Log và Audit Log có mục đích khác nhau:

```text
Application Log
→ hệ thống xảy ra chuyện gì?

Audit Log
→ user nào đã thực hiện hành động gì?
```

Không nên lưu các sự kiện nghiệp vụ quan trọng chỉ bằng Application Log.

## Error Handling

API sử dụng `ApiExceptionHandler` để chuyển exception thành HTTP response phù hợp.

Một số mapping chính:

```text
AuthenticationException      → 401
NotFoundException            → 404
ConflictException            → 409
BusinessRuleException        → 422
DependencyUnavailableException → 503
Unhandled Exception          → 500
```

Response lỗi sử dụng `ProblemDetails` và chứa `correlationId` để hỗ trợ tra cứu log.

Dependency unavailable được ghi log ở mức `Warning`.

Unhandled system error được ghi ở mức `Error`.

## Giới hạn

Application Log hiện chỉ được triển khai ở ASP.NET Core API.

Blazor WebAssembly chạy trong browser và chưa có hệ thống tập trung để gửi Client log về server.

Rolling file cũng không phải cơ chế backup.

Backup dữ liệu SQL Server và Audit Log được xử lý như một vấn đề vận hành riêng, không thuộc trách nhiệm của Application Log.