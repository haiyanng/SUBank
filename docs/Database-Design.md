# Thiết kế cơ sở dữ liệu SUBank V2.4 - P0/P1

## Nguyên tắc

- SQL Server/Azure SQL là nguồn sự thật cho Identity, profile, account, balance, giao dịch và audit.
- Primary key nghiệp vụ dùng `bigint IDENTITY`; identifier hiển thị dùng business reference theo Blueprint.
- Tiền dùng `decimal(18,2)`, thời gian dùng UTC, account number lưu dạng chuỗi.
- Chỉ lưu financial transaction đã commit; lần thử thất bại thuộc `AuditLog`.
- Email/Phone nghiệp vụ chỉ thuộc `CustomerProfile`; không có bảng `CustomerContact`.
- Teller và Admin không có `CustomerProfile`.

## Bảng nghiệp vụ P0

### ApplicationUser (`AspNetUsers` mở rộng)

| Cột | Kiểu | Quy tắc |
|---|---|---|
| `Id` | `nvarchar(450)` | PK do Identity quản lý |
| `TransactionPasswordHash` | `nvarchar(500)` | nullable; chỉ Customer cần |
| `LockedAtUtc` | `datetimeoffset` | nullable |
| `IsActive` | `bit` | required, default true |
| `CreatedAtUtc` | `datetimeoffset` | required |

Identity quản lý `UserName`, password hash, failure count, lockout và role. Customer có `UserName` bằng đúng `CustomerProfile.Phone`; Teller/Admin dùng username nghiệp vụ. Không ghi số liên hệ vào `AspNetUsers.PhoneNumber`.

### CustomerProfile

| Cột | Kiểu | Quy tắc |
|---|---|---|
| `Id` | `bigint` | PK, identity |
| `UserId` | `nvarchar(450)` | FK `AspNetUsers`, required, unique |
| `FullName` | `nvarchar(150)` | required |
| `DateOfBirth` | `date` | required |
| `IdentityCardNumber` | `varchar(20)` | required, unique, số CCCD/CMND demo |
| `Phone` | `varchar(20)` | required, unique |
| `Email` | `nvarchar(254)` | required, unique |
| `PermanentAddress` | `nvarchar(500)` | required |
| `TemporaryAddress` | `nvarchar(500)` | nullable |
| `CreatedAtUtc` | `datetimeoffset` | required |
| `UpdatedAtUtc` | `datetimeoffset` | nullable |

### BankAccount

| Cột | Kiểu | Quy tắc |
|---|---|---|
| `Id` | `bigint` | PK, identity |
| `CustomerProfileId` | `bigint` | FK, required |
| `AccountNumber` | `varchar(10)` | required, unique, đúng 10 chữ số |
| `Balance` | `decimal(18,2)` | required, check `Balance >= 0` |
| `Currency` | `char(3)` | required, check `Currency = 'VND'` |
| `Status` | `nvarchar(20)` | Active/Frozen/Closed |
| `RowVersion` | `rowversion` | concurrency token |
| `CreatedAtUtc` | `datetimeoffset` | required |

P0 chỉ cho account `Active` gửi, nhận hoặc nhận Cash Deposit.

Trong read model danh sách tài khoản hiện tại, account được tạo sớm nhất theo `CreatedAtUtc`, rồi `Id`, được đánh dấu `IsPrimary = true`; các account còn lại là phụ. Client chỉ đọc cờ này và không được suy luận tài khoản chính từ thứ tự account number. Khi dự án bổ sung nghiệp vụ đổi tài khoản chính, quy tắc tạm thời này phải được thay bằng quan hệ/cột nghiệp vụ được lưu bền vững.

### FinancialTransaction

| Cột | Kiểu | Quy tắc |
|---|---|---|
| `Id` | `bigint` | PK, identity |
| `ReferenceNo` | `varchar(30)` | required, unique |
| `SourceAccountId` | `bigint` | nullable FK `BankAccount` |
| `DestinationAccountId` | `bigint` | required FK `BankAccount` |
| `CreatedByUserId` | `nvarchar(450)` | required FK `AspNetUsers` |
| `Type` | `nvarchar(20)` | Transfer/CashDeposit |
| `Amount` | `decimal(18,2)` | required, check `Amount > 0` |
| `Description` | `nvarchar(280)` | nullable, untrusted text |
| `IdempotencyKey` | `varchar(64)` | required |
| `RequestHash` | `char(64)` | required; phát hiện cùng key khác payload |
| `CreatedAtUtc` | `datetimeoffset` | required |

Unique index: `(CreatedByUserId, IdempotencyKey)`. Check theo loại:

- Transfer: source required và source khác destination.
- CashDeposit: source null.

### AuditLog

| Cột | Kiểu | Quy tắc |
|---|---|---|
| `Id` | `bigint` | PK, identity |
| `UserId` | `nvarchar(450)` | nullable FK `AspNetUsers` |
| `Action` | `varchar(80)` | required |
| `EntityType` | `varchar(80)` | nullable |
| `EntityId` | `nvarchar(100)` | nullable business reference |
| `Result` | `nvarchar(20)` | Success/Failure |
| `IpAddress` | `varchar(45)` | nullable |
| `CorrelationId` | `varchar(100)` | nullable |
| `Details` | `nvarchar(1000)` | nullable, đã lọc secret/PII |
| `CreatedAtUtc` | `datetimeoffset` | required |

`CorrelationId` được EF interceptor tự bổ sung cho `AuditLog` mới khi `SaveChanges` diễn ra trong một HTTP request có correlation context. Event từ startup/background có thể để `null`; mã này chỉ dùng liên kết SQL audit với technical log, không dùng authorization. Application/technical log nằm ở console/rolling file, không phải một bảng database. Hiện chưa có retention/archive riêng cho `AuditLog` và chưa có quy trình backup/restore database được kiểm chứng.

## Bảng P1

`RefreshToken` đã được triển khai trong Auth P0. `UserSession` được triển khai trong feature active-session P1. `AiQueryLog` chỉ được thêm khi vertical slice tương ứng bắt đầu; `Beneficiary` thuộc P2.

### RefreshToken

| Cột | Kiểu | Quy tắc |
|---|---|---|
| `Id` | `bigint` | PK, identity |
| `UserId` | `nvarchar(450)` | required FK |
| `SessionId` | `varchar(64)` | required |
| `TokenHash` | `char(64)` | required, unique |
| `ExpiresAtUtc` | `datetimeoffset` | required; không vượt mốc hết hạn tuyệt đối của `UserSession` |
| `RevokedAtUtc` | `datetimeoffset` | nullable |
| `CreatedAtUtc` | `datetimeoffset` | required |
| `ReplacedByTokenId` | `bigint` | nullable self FK |

### UserSession

| Cột | Kiểu | Quy tắc |
|---|---|---|
| `Id` | `bigint` | PK, identity; không expose |
| `UserId` | `nvarchar(450)` | required FK |
| `SessionId` | `varchar(64)` | required; unique theo user; không log/expose |
| `CreatedAtUtc` | `datetimeoffset` | required |
| `ExpiresAtUtc` | `datetimeoffset` | required; mốc hết hạn tuyệt đối của logical session |
| `RevokedAtUtc` | `datetimeoffset` | nullable |
| `RevocationReason` | `varchar(50)` | nullable; mã lý do an toàn |

SQL chỉ lưu lịch sử. Redis key theo user mới là nguồn quyết định `sid` đang active; không lưu raw access/refresh token trong bảng này.

Khi rotate refresh token, token thay thế giữ nguyên mốc `UserSession.ExpiresAtUtc`. Redis TTL được conditional-renew tới đúng phần thời gian còn lại và không biến session thành sliding lifetime. Cặp `(UserId, SessionId)` xác định một token family; refresh và logout dùng hàng `UserSession` tương ứng làm aggregate lock. Logout từ một member bất kỳ thu hồi mọi refresh token chưa revoke trong family mà không cần lần theo self-FK `ReplacedByTokenId`.

## Index tối thiểu

- Identity normalized username.
- `CustomerProfile.UserId`, `IdentityCardNumber`, `Phone`, `Email` unique.
- `BankAccount.AccountNumber` unique; `CustomerProfileId`.
- `FinancialTransaction.ReferenceNo` unique.
- `FinancialTransaction.SourceAccountId, CreatedAtUtc`.
- `FinancialTransaction.DestinationAccountId, CreatedAtUtc`.
- `FinancialTransaction.CreatedByUserId, IdempotencyKey` unique.
- `AuditLog.CreatedAtUtc`; `AuditLog.UserId, CreatedAtUtc`.
- `RefreshToken.TokenHash` unique; `RefreshToken.UserId, SessionId`.
- `UserSession.UserId, SessionId` unique; `UserSession.UserId, RevokedAtUtc`.

## Format business identifier

- `AccountNumber`: 10 chữ số; tài khoản chính của Customer demo trùng với số điện thoại/tên đăng nhập.
- `ReferenceNo`: `SUB` + UTC `yyyyMMddHHmmssfff` + 6 ký tự ngẫu nhiên viết hoa; unique constraint là lớp bảo vệ cuối.
- `IdempotencyKey`: client tạo GUID dạng `N` hoặc chuỗi tương đương tối đa 64 ký tự.

## Seed P0

- `0900000001`, role Customer:
  - tài khoản chính `0900000001`, balance mở đầu `100000000.00`;
  - account `1000000003`, balance mở đầu `20000000.00`;
  - account `1234567890`, balance mở đầu `15000000.00`;
  - account `1234567891`, balance mở đầu `5000000.00`.
- `0900000002`, role Customer:
  - tài khoản chính `0900000002`, balance mở đầu `50000000.00`;
  - account `1000000004`, balance mở đầu `10000000.00`;
  - account `2234567890`, balance mở đầu `15000000.00`;
  - account `2234567891`, balance mở đầu `5000000.00`.
- `teller`, role Teller.
- `admin`, role Admin.
- Database Development cũ được đổi username `customer.a`/`customer.b` và số tài khoản chính `1000000001`/`1000000002` tại chỗ, giữ nguyên ID và toàn bộ foreign key liên quan. Nếu còn user Identity legacy trùng nhưng không có hồ sơ, giao dịch hoặc audit thì seed xóa bản ghi đó; user có lịch sử không bao giờ bị xóa tự động.
- Seed bổ sung từng profile/account còn thiếu và không đặt lại balance của account đã tồn tại.
- Dữ liệu đều tổng hợp; password/transaction password chỉ dành cho Development và không tái sử dụng secret thật.

## Quy tắc 3NF

- User security tách khỏi Customer profile.
- Customer profile tách khỏi BankAccount để một Customer có nhiều account.
- Giao dịch tham chiếu account/user, không lặp profile data.
- Statement là query từ FinancialTransaction, không có bảng Statement.
- Balance được giữ trên BankAccount để thực thi giao dịch hiệu quả; FinancialTransaction cung cấp lịch sử và khả năng đối soát.

## Tính nhất quán của sao kê

Sao kê suy ra số dư đầu kỳ từ `BankAccount.Balance` hiện tại và biến động trong `FinancialTransaction`. Vì hai nguồn này được đọc bằng nhiều câu SQL, toàn bộ lần đọc phải nằm trong một transaction `RepeatableRead` ngắn: account được đọc trước để giữ shared lock, `asOfUtc` chỉ được lấy sau đó, và các movement đều bị giới hạn tới cùng mốc thời gian. Mọi luồng làm thay đổi số dư bắt buộc vừa cập nhật account vừa ghi transaction trong cùng SQL transaction; writer tới sau sẽ chờ lần đọc sao kê kết thúc thay vì commit xen giữa.

Thứ tự dòng sao kê là `CreatedAtUtc`, sau đó `Id` để ổn định khi hai giao dịch có cùng timestamp. Deadlock 1205 được retry một lần. Việc tạo DTO và PDF diễn ra sau commit để rút ngắn thời gian giữ khóa. Phương án này không yêu cầu bật database snapshot isolation; nếu tải production cần tránh writer bị chặn ngắn, thay đổi sang `Snapshot` phải đi kèm cấu hình database và kiểm thử deployment riêng.
