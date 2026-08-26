# Thiết kế cơ sở dữ liệu SUBank V2.4 - P0

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

Identity quản lý `UserName`, password hash, failure count, lockout và role. Login bằng `UserName`.

### CustomerProfile

| Cột | Kiểu | Quy tắc |
|---|---|---|
| `Id` | `bigint` | PK, identity |
| `UserId` | `nvarchar(450)` | FK `AspNetUsers`, required, unique |
| `FullName` | `nvarchar(150)` | required |
| `DateOfBirth` | `date` | required |
| `IdentityNumber` | `varchar(20)` | required, unique, dữ liệu demo |
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
| `Status` | `varchar(20)` | Active/Frozen/Closed |
| `RowVersion` | `rowversion` | concurrency token |
| `CreatedAtUtc` | `datetimeoffset` | required |

P0 chỉ cho account `Active` gửi, nhận hoặc nhận Cash Deposit.

### FinancialTransaction

| Cột | Kiểu | Quy tắc |
|---|---|---|
| `Id` | `bigint` | PK, identity |
| `ReferenceNo` | `varchar(30)` | required, unique |
| `SourceAccountId` | `bigint` | nullable FK `BankAccount` |
| `DestinationAccountId` | `bigint` | required FK `BankAccount` |
| `CreatedByUserId` | `nvarchar(450)` | required FK `AspNetUsers` |
| `Type` | `varchar(20)` | TRANSFER/CASH_DEPOSIT |
| `Amount` | `decimal(18,2)` | required, check `Amount > 0` |
| `Description` | `nvarchar(280)` | nullable, untrusted text |
| `IdempotencyKey` | `varchar(64)` | required |
| `RequestHash` | `char(64)` | required; phát hiện cùng key khác payload |
| `CreatedAtUtc` | `datetimeoffset` | required |

Unique index: `(CreatedByUserId, IdempotencyKey)`. Check theo loại:

- TRANSFER: source required và source khác destination.
- CASH_DEPOSIT: source null.

### AuditLog

| Cột | Kiểu | Quy tắc |
|---|---|---|
| `Id` | `bigint` | PK, identity |
| `UserId` | `nvarchar(450)` | nullable FK `AspNetUsers` |
| `Action` | `varchar(80)` | required |
| `EntityType` | `varchar(80)` | nullable |
| `EntityId` | `nvarchar(100)` | nullable business reference |
| `Result` | `varchar(20)` | Success/Failure |
| `IpAddress` | `varchar(45)` | nullable |
| `CorrelationId` | `varchar(100)` | nullable |
| `Details` | `nvarchar(1000)` | nullable, đã lọc secret/PII |
| `CreatedAtUtc` | `datetimeoffset` | required |

## Bảng chuẩn bị cho P1

Các bảng sau thuộc schema tổng thể nhưng chỉ triển khai khi vertical slice tương ứng bắt đầu: `Beneficiary`, `AddressChangeRequest`, `RefreshToken`, `UserSession`, `AiQueryLog`. Riêng `RefreshToken` cần trong Auth P0 nên sẽ được triển khai cùng Authentication.

### RefreshToken

| Cột | Kiểu | Quy tắc |
|---|---|---|
| `Id` | `bigint` | PK, identity |
| `UserId` | `nvarchar(450)` | required FK |
| `SessionId` | `varchar(64)` | required |
| `TokenHash` | `char(64)` | required, unique |
| `ExpiresAtUtc` | `datetimeoffset` | required |
| `RevokedAtUtc` | `datetimeoffset` | nullable |
| `CreatedAtUtc` | `datetimeoffset` | required |
| `ReplacedByTokenId` | `bigint` | nullable self FK |

## Index tối thiểu

- Identity normalized username.
- `CustomerProfile.UserId`, `IdentityNumber`, `Phone`, `Email` unique.
- `BankAccount.AccountNumber` unique; `CustomerProfileId`.
- `FinancialTransaction.ReferenceNo` unique.
- `FinancialTransaction.SourceAccountId, CreatedAtUtc`.
- `FinancialTransaction.DestinationAccountId, CreatedAtUtc`.
- `FinancialTransaction.CreatedByUserId, IdempotencyKey` unique.
- `AuditLog.CreatedAtUtc`; `AuditLog.UserId, CreatedAtUtc`.
- `RefreshToken.TokenHash` unique; `RefreshToken.UserId, SessionId`.

## Format business identifier

- `AccountNumber`: 10 chữ số; seed bắt đầu từ `1000000001`.
- `ReferenceNo`: `SUB` + UTC `yyyyMMddHHmmssfff` + 6 ký tự ngẫu nhiên viết hoa; unique constraint là lớp bảo vệ cuối.
- `IdempotencyKey`: client tạo GUID dạng `N` hoặc chuỗi tương đương tối đa 64 ký tự.

## Seed P0

- `customer.a`, role Customer, account `1000000001`, balance `100000000.00`.
- `customer.b`, role Customer, account `1000000002`, balance `50000000.00`.
- `teller`, role Teller.
- `admin`, role Admin.
- Dữ liệu đều tổng hợp; password/transaction password chỉ dành cho Development và không tái sử dụng secret thật.

## Quy tắc 3NF

- User security tách khỏi Customer profile.
- Customer profile tách khỏi BankAccount để một Customer có nhiều account.
- Giao dịch tham chiếu account/user, không lặp profile data.
- Statement là query từ FinancialTransaction, không có bảng Statement.
- Balance được giữ trên BankAccount để thực thi giao dịch hiệu quả; FinancialTransaction cung cấp lịch sử và khả năng đối soát.
