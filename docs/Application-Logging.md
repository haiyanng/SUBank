# Thiết kế Application Log

## Mục tiêu và phạm vi

Application Log hiện áp dụng cho ASP.NET Core API. Mục tiêu là chẩn đoán startup, dependency, exception và vòng đời HTTP request mà không biến log kỹ thuật thành nơi lưu dữ liệu ngân hàng hoặc thông tin xác thực.

Blazor WebAssembly chạy trong trình duyệt nên không tự động gửi console log của Client về API. Nếu sau này cần thu thập lỗi Client, phải thiết kế một luồng riêng có giới hạn dữ liệu và sự đồng ý phù hợp.

## Ba nguồn dữ liệu khác nhau

| Nguồn | Mục đích | Nơi lưu hiện tại |
|---|---|---|
| Application/technical log | Chẩn đoán host, dependency, exception và thời gian xử lý request | Console JSON ở mọi môi trường; rolling file trong Development |
| `AuditLog` | Lịch sử actor, hành động, target và kết quả của sự kiện bảo mật/nghiệp vụ | SQL Server |
| `FinancialTransaction` | Sự thật đã commit về chuyển động tiền | SQL Server |

Application Log không thay thế `AuditLog`, và cả hai không thay thế `FinancialTransaction`.

## Luồng correlation

1. API đọc header `X-Correlation-ID` nếu giá trị dài tối đa 100 ký tự và chỉ gồm chữ, số, dấu `-`, `_`, `.`; nếu không hợp lệ, server sinh GUID mới.
2. Correlation ID trở thành `HttpContext.TraceIdentifier`, được trả lại trong response header và đi vào logging scope.
3. ProblemDetails có extension `correlationId` để người dùng gửi mã tra cứu mà không cần gửi dữ liệu nhạy cảm.
4. EF Core interceptor tự điền cùng mã vào `AuditLog.CorrelationId` khi audit mới được lưu trong HTTP request. Audit từ startup hoặc background không có request context có thể để `null`.

Correlation ID chỉ dùng để liên kết sự kiện, không phải credential và không được dùng để authorization.

## Dữ liệu của một request log

Request completion log chỉ chủ động ghi:

- HTTP method;
- route template, ví dụ `api/accounts/{accountNumber}`;
- status code;
- thời gian xử lý theo mili giây;
- correlation ID cùng metadata kỹ thuật của logger.

Middleware không đọc hoặc ghi raw URL, route value, query string, request/response body, cookie hay header xác thực. Một enricher loại bỏ thêm các top-level property có tên nhạy cảm như `RequestPath`, `Password`, `AccessToken`, `RefreshToken`, `ConnectionString`, `IdentityCardNumber` và `AccountNumber` trước khi event đi tới sink.

Đây là lớp phòng thủ bổ sung, không phải bộ lọc bí mật tổng quát cho mọi object hoặc nội dung exception. Lập trình viên vẫn không được truyền DTO, body, token, mật khẩu, CCCD, số tài khoản đầy đủ hay connection string vào `ILogger`.

## Mức log

- Request `2xx` và `3xx`: `Information`.
- Request `4xx`: `Warning`.
- Request `5xx`: `Error`.
- `/health` thành công: `Debug`, nên không được ghi khi minimum level là `Information`.
- Dependency unavailable: exception event ở `Warning`; lỗi hệ thống không dự kiến ở `Error`.

## Console, file và retention

Console và file dùng JSON Lines qua `RenderedCompactJsonFormatter`: mỗi event là một JSON object trên một dòng, không phải một JSON array.

Section `ApplicationLogging` có các cấu hình:

| Khóa | Development hiện tại | Ý nghĩa |
|---|---:|---|
| `FileEnabled` | `true` | Bật file sink; cấu hình gốc/production mặc định là `false` |
| `Directory` | `logs` | Thư mục tương đối, bắt buộc nằm dưới API content root |
| `FileSizeLimitBytes` | `10485760` | Tối đa 10 MiB cho một file trước khi roll |
| `RetainedFileCountLimit` | `31` | Giới hạn số file còn giữ |
| `RetainedDays` | `14` | Giới hạn tuổi file |

File được roll theo ngày và theo dung lượng, có dạng `src/SUBank.Api/logs/subank-api-YYYYMMDD.log`. Khi đồng thời có giới hạn tuổi, số file và dung lượng, file có thể bị xóa sớm hơn 14 ngày nếu chạm ngưỡng khác. Thư mục/file log đã được `.gitignore` loại khỏi Git.

Xem log local bằng PowerShell:

```powershell
Get-Content -Encoding UTF8 src/SUBank.Api/logs/subank-api-*.log -Wait
Select-String -Path src/SUBank.Api/logs/subank-api-*.log -Pattern 'mã-correlation-cần-tra'
```

## Production và backup

Trong container/demo production, console JSON là đầu ra chính để nền tảng hosting thu thập. File sink mặc định tắt vì filesystem container có thể read-only hoặc mất khi redeploy. Chỉ bật file khi có persistent volume, phân quyền đọc phù hợp và quy trình vận hành rõ ràng.

Rolling file không phải backup hoặc archive. Repo hiện chưa có lịch backup SQL, nơi lưu bản sao độc lập, script restore hay bằng chứng restore drill. `AuditLog` và `FinancialTransaction` nằm trong SQL cũng chỉ được bảo vệ khi backup database của provider thực sự được cấu hình và kiểm chứng.

## Kiểm chứng ngày 2026-08-30

- API tạo rolling file và ghi startup/request event dạng JSON Lines.
- Response trả `X-Correlation-ID`; ProblemDetails trả cùng `correlationId`.
- Request tới route có số tài khoản và query thử nghiệm chỉ ghi route template, không ghi route value/query/body vào request completion event.
- Dependency Redis không khả dụng được ghi ở `Warning`, còn request tương ứng được ghi `503` ở `Error` với cùng correlation ID.
- Toàn solution build thành công với 0 warning, 0 error. Không chạy hoặc thêm test theo quyết định hiện tại của chủ dự án.
