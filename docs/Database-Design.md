# Thiết kế cơ sở dữ liệu SUBank

## Nguyên tắc

- SUBank sử dụng SQL Server làm cơ sở dữ liệu chính.
- ASP.NET Core Identity quản lý user, password, role và các thông tin liên quan đến authentication.
- Các bảng nghiệp vụ sử dụng `bigint IDENTITY` làm khóa chính nội bộ; `ApplicationUser` sử dụng khóa do ASP.NET Core Identity quản lý.
- Tiền được lưu bằng `decimal(18,0)` trong SQL và sử dụng `decimal` trong .NET vì hệ thống hiện chỉ hỗ trợ VND và không cho phép số tiền có phần thập phân.
- Thời gian nghiệp vụ được lưu theo UTC.
- Account Number được lưu dưới dạng chuỗi để giữ nguyên định dạng, bao gồm số `0` ở đầu.
- SQL Server là nguồn dữ liệu có thẩm quyền đối với Customer, Bank Account, balance và Financial Transaction.
- Redis không lưu balance và không thuộc ERD của SQL Server.
- `FinancialTransaction` chỉ ghi nhận chuyển động tiền đã hoàn tất.
- Các lần thử thất bại hoặc hành động bảo mật được ghi trong `AuditLog`.
- Thông tin cá nhân của Customer nằm trong `CustomerProfile`; Teller và Admin không có `CustomerProfile`.
- Statement được tạo từ dữ liệu hiện có và không có bảng riêng.

## Bảng nghiệp vụ chính

### ApplicationUser (`AspNetUsers` mở rộng)

`ApplicationUser` kế thừa ASP.NET Core Identity và đại diện cho tài khoản đăng nhập của Customer, Teller hoặc Admin.

Các field mở rộng chính:

| Cột | Kiểu | Mục đích |
|---|---|---|
| `Id` | `nvarchar(450)` | Primary Key do Identity quản lý |
| `TransactionPasswordHash` | `nvarchar(500)` | Hash mật khẩu giao dịch của Customer |
| `LockedAtUtc` | `datetimeoffset` | Thời điểm Identity Lockout |
| `IsAdminSuspended` | `bit` | Trạng thái Customer bị Admin khóa |
| `AdminSuspendedAtUtc` | `datetimeoffset` | Thời điểm Admin khóa |
| `AdminSuspensionReason` | `nvarchar(500)` | Lý do khóa |
| `AdminSuspendedByUserId` | `nvarchar(450)` | Admin thực hiện khóa |
| `IsActive` | `bit` | Trạng thái hoạt động |
| `CreatedAtUtc` | `datetimeoffset` | Thời điểm tạo |

ASP.NET Core Identity tiếp tục quản lý các field như:

- `UserName`
- `NormalizedUserName`
- `PasswordHash`
- `AccessFailedCount`
- `LockoutEnabled`
- `LockoutEnd`

Customer sử dụng số điện thoại làm username đăng nhập.

Teller và Admin sử dụng username nghiệp vụ.

Các bảng Identity quan trọng đối với SUBank gồm:

```text
AspNetUsers
AspNetRoles
AspNetUserRoles
```

Quan hệ User và Role trong ASP.NET Core Identity là nhiều-nhiều:

```text
AspNetUsers
      1
      │
      N
AspNetUserRoles
      N
      │
      1
AspNetRoles
```

Development Seed Data hiện gán một business role cho mỗi demo user:

```text
Customer
Teller
Admin
```

### CustomerProfile

`CustomerProfile` lưu thông tin cá nhân và thông tin nghiệp vụ của Customer.

| Cột | Kiểu | Mục đích |
|---|---|---|
| `Id` | `bigint` | Primary Key |
| `UserId` | `nvarchar(450)` | FK đến `AspNetUsers`, unique |
| `FullName` | `nvarchar(150)` | Họ tên Customer |
| `DateOfBirth` | `date` | Ngày sinh |
| `IdentityCardNumber` | `varchar(20)` | CCCD/CMND demo, unique |
| `Phone` | `varchar(20)` | Số điện thoại, unique |
| `Email` | `nvarchar(254)` | Email, unique |
| `PermanentAddress` | `nvarchar(500)` | Địa chỉ thường trú |
| `TemporaryAddress` | `nvarchar(500)` | Địa chỉ tạm trú |
| `CreatedAtUtc` | `datetimeoffset` | Thời điểm tạo |
| `UpdatedAtUtc` | `datetimeoffset` | Thời điểm cập nhật |

Quan hệ:

```text
ApplicationUser 1 ─── 0..1 CustomerProfile
```

Không phải mọi `ApplicationUser` đều có `CustomerProfile`.

```text
Customer → có CustomerProfile
Teller   → không có CustomerProfile
Admin    → không có CustomerProfile
```

`CustomerProfile.UserId` là unique nên một Customer user chỉ có tối đa một Customer Profile.

### BankAccount

`BankAccount` đại diện cho tài khoản ngân hàng nội bộ SUBank.

| Cột | Kiểu | Mục đích |
|---|---|---|
| `Id` | `bigint` | Primary Key |
| `CustomerProfileId` | `bigint` | FK đến `CustomerProfile` |
| `AccountNumber` | `varchar(10)` | Số tài khoản, unique |
| `Balance` | `decimal(18,0)` | Số dư hiện tại |
| `Currency` | `char(3)` | Tiền tệ, hiện chỉ dùng VND |
| `Status` | `nvarchar(20)` | Trạng thái tài khoản |
| `RowVersion` | `rowversion` | Optimistic concurrency token |
| `CreatedAtUtc` | `datetimeoffset` | Thời điểm tạo |

Quan hệ:

```text
CustomerProfile 1 ─── N BankAccount
```

Một Customer có thể sở hữu nhiều Bank Account.

Mỗi Bank Account chỉ thuộc một Customer Profile.

`AccountNumber` được lưu bằng chuỗi thay vì kiểu số vì đây là business identifier và có thể bắt đầu bằng `0`.

Ví dụ:

```text
0900000001
```

Nếu lưu bằng kiểu số, số `0` ở đầu có thể bị mất.

`Balance` sử dụng `decimal(18,0)` vì SUBank chỉ sử dụng VND và không hỗ trợ số tiền thập phân.

`RowVersion` được sử dụng để hỗ trợ optimistic concurrency khi nhiều request cùng cập nhật một tài khoản.

Trong read model hiện tại, tài khoản được tạo sớm nhất được đánh dấu là tài khoản chính để hiển thị. Nếu hệ thống sau này hỗ trợ thay đổi tài khoản chính, trạng thái này nên được lưu bằng dữ liệu nghiệp vụ riêng.

### FinancialTransaction

`FinancialTransaction` ghi nhận các chuyển động tiền đã hoàn tất trong SUBank.

Các loại giao dịch chính hiện tại:

```text
Transfer
CashDeposit
```

| Cột | Kiểu | Mục đích |
|---|---|---|
| `Id` | `bigint` | Primary Key |
| `ReferenceNo` | `varchar(30)` | Mã tham chiếu giao dịch, unique |
| `SourceAccountId` | `bigint` | FK đến Bank Account nguồn, nullable |
| `DestinationAccountId` | `bigint` | FK đến Bank Account nhận |
| `CreatedByUserId` | `nvarchar(450)` | FK đến `AspNetUsers` |
| `Type` | `nvarchar(20)` | Loại giao dịch |
| `Amount` | `decimal(18,0)` | Số tiền |
| `Description` | `nvarchar(280)` | Nội dung giao dịch |
| `IdempotencyKey` | `varchar(64)` | Nhận diện request |
| `RequestHash` | `char(64)` | Kiểm tra cùng key nhưng khác payload |
| `CreatedAtUtc` | `datetimeoffset` | Thời điểm giao dịch |

Đối với Transfer:

```text
SourceAccountId      = tài khoản gửi
DestinationAccountId = tài khoản nhận

SourceAccountId != DestinationAccountId
Amount > 0
```

Đối với Cash Deposit:

```text
SourceAccountId      = null
DestinationAccountId = tài khoản được nộp tiền
Amount > 0
```

Cash Deposit không có Bank Account nguồn vì tiền mặt đến từ bên ngoài hệ thống.

`CreatedByUserId` cho biết user thực hiện nghiệp vụ.

Ví dụ:

```text
Transfer
CreatedByUserId → Customer

CashDeposit
CreatedByUserId → Teller
```

`ReferenceNo` là business identifier của giao dịch.

```text
Id
→ dùng nội bộ trong database

ReferenceNo
→ dùng để hiển thị, tra cứu và đối chiếu
```

`IdempotencyKey` giúp hạn chế cùng một request chuyển tiền bị xử lý nhiều lần.

Nếu cùng một key và cùng payload được gửi lại, hệ thống có thể trả kết quả trước đó thay vì thực hiện giao dịch lần nữa.

`RequestHash` giúp phát hiện trường hợp cùng Idempotency Key nhưng payload đã thay đổi.

### Quan hệ BankAccount và FinancialTransaction

Một Bank Account có thể tham gia Financial Transaction theo hai vai trò:

```text
BankAccount
   │
   ├── SourceAccount
   │
   └── DestinationAccount
```

Vì vậy giữa `BankAccount` và `FinancialTransaction` có hai quan hệ riêng:

```text
BankAccount 1 ─── N FinancialTransaction
                SourceAccountId

BankAccount 1 ─── N FinancialTransaction
                DestinationAccountId
```

Đây là hai foreign key khác nhau và phải được thể hiện riêng trên ERD.

### AuditLog

`AuditLog` ghi nhận các sự kiện nghiệp vụ và bảo mật quan trọng.

| Cột | Kiểu | Mục đích |
|---|---|---|
| `Id` | `bigint` | Primary Key |
| `UserId` | `nvarchar(450)` | FK đến `AspNetUsers`, có thể null |
| `Action` | `varchar(80)` | Hành động |
| `EntityType` | `varchar(80)` | Loại đối tượng liên quan |
| `EntityId` | `nvarchar(100)` | Identifier của đối tượng |
| `Result` | `nvarchar(20)` | Success hoặc Failure |
| `IpAddress` | `varchar(45)` | IP request nếu có |
| `CorrelationId` | `varchar(100)` | Liên kết với Application Log |
| `Details` | `nvarchar(1000)` | Thông tin bổ sung |
| `CreatedAtUtc` | `datetimeoffset` | Thời điểm |

Ví dụ Audit Log:

```text
Login Failure
Transfer Failure
Cash Deposit
Admin Suspend Customer
Admin Unsuspend Customer
Session Revocation
```

`FinancialTransaction` và `AuditLog` có mục đích khác nhau:

```text
FinancialTransaction
→ tiền đã di chuyển như thế nào

AuditLog
→ ai đã thực hiện hành động gì và kết quả ra sao
```

Một Cash Deposit thành công có thể tạo cả:

```text
FinancialTransaction
+
AuditLog
```

Một Transfer thất bại chỉ cần Audit Log vì chưa có tiền thực sự di chuyển.

Application Log không phải bảng SQL và không thay thế Audit Log.

## Bảng authentication và session

### RefreshToken

Entity `RefreshToken` được map vào bảng:

```text
RefreshTokens
```

Raw refresh token không được lưu trực tiếp trong database. SUBank lưu hash của token.

| Cột | Kiểu | Mục đích |
|---|---|---|
| `Id` | `bigint` | Primary Key |
| `UserId` | `nvarchar(450)` | FK đến `AspNetUsers` |
| `SessionId` | `varchar(64)` | Logical Session Identifier |
| `TokenHash` | `char(64)` | Hash của refresh token, unique |
| `ExpiresAtUtc` | `datetimeoffset` | Thời điểm hết hạn |
| `RevokedAtUtc` | `datetimeoffset` | Thời điểm thu hồi |
| `CreatedAtUtc` | `datetimeoffset` | Thời điểm tạo |
| `ReplacedByTokenId` | `bigint` | Self-reference đến token thay thế |

Foreign key hiện có:

```text
RefreshTokens.UserId
    → AspNetUsers.Id

RefreshTokens.ReplacedByTokenId
    → RefreshTokens.Id
```

Refresh Token có thể được rotate.

Ví dụ:

```text
Logical Session A
│
├── Refresh Token 1 → revoked
├── Refresh Token 2 → revoked
└── Refresh Token 3 → current
```

Các token thuộc cùng logical session sử dụng cùng `SessionId`.

### UserSession

Entity `UserSession` được map vào bảng:

```text
UserSessions
```

| Cột | Kiểu | Mục đích |
|---|---|---|
| `Id` | `bigint` | Primary Key |
| `UserId` | `nvarchar(450)` | FK đến `AspNetUsers` |
| `SessionId` | `varchar(64)` | Logical Session Identifier |
| `CreatedAtUtc` | `datetimeoffset` | Thời điểm tạo session |
| `ExpiresAtUtc` | `datetimeoffset` | Mốc hết hạn tuyệt đối |
| `RevokedAtUtc` | `datetimeoffset` | Thời điểm thu hồi |
| `RevocationReason` | `varchar(50)` | Lý do thu hồi |

Foreign key:

```text
UserSessions.UserId
    → AspNetUsers.Id
```

SQL Server lưu lịch sử session.

Redis được sử dụng để xác định session nào hiện đang active.

```text
SQL Server
→ lịch sử session bền vững

Redis
→ active-session pointer hiện tại
```

### Quan hệ giữa RefreshToken và UserSession

Cả hai bảng đều có:

```text
UserId
SessionId
```

Ví dụ:

```text
UserSession
UserId    = USER-1
SessionId = SESSION-A

RefreshToken
UserId    = USER-1
SessionId = SESSION-A
```

Tuy nhiên, **không có Foreign Key trực tiếp**:

```text
RefreshTokens.SessionId
    X
UserSessions.SessionId
```

Hai bảng được liên kết ở tầng Application bằng cùng `UserId` và `SessionId`.

EF Core mapping hiện tại:

```text
UserSessions
UNIQUE (UserId, SessionId)

RefreshTokens
INDEX (UserId, SessionId)
```

Index của `RefreshTokens` không unique vì một logical session có thể có nhiều refresh token trong quá trình token rotation.

Có thể hiểu:

```text
AspNetUsers
     │
     ├────< UserSessions
     │
     └────< RefreshTokens

UserSession.SessionId
        ⋮
        ⋮ liên kết logic
        ⋮
RefreshToken.SessionId
```

Khi vẽ ERD vật lý, không vẽ Foreign Key trực tiếp giữa `RefreshToken` và `UserSession`.

## Index chính

Các index quan trọng gồm:

- Identity normalized username.
- `CustomerProfile.UserId` unique.
- `CustomerProfile.IdentityCardNumber` unique.
- `CustomerProfile.Phone` unique.
- `CustomerProfile.Email` unique.
- `BankAccount.AccountNumber` unique.
- Index theo `BankAccount.CustomerProfileId`.
- `FinancialTransaction.ReferenceNo` unique.
- Index phục vụ truy vấn transaction theo Source Account.
- Index phục vụ truy vấn transaction theo Destination Account.
- Index phục vụ Idempotency.
- Index theo thời gian của Audit Log.
- `RefreshToken.TokenHash` unique.
- Index `RefreshToken (UserId, SessionId)`.
- Unique index `UserSession (UserId, SessionId)`.

EF Core Configuration và Migration hiện tại là nguồn xác nhận cuối cùng cho tên và cấu hình index vật lý.

## Format business identifier

### AccountNumber

Account Number hiện sử dụng 10 chữ số và được lưu dưới dạng chuỗi.

Ví dụ:

```text
0900000001
3000000001
5000000003
```

### ReferenceNo

`ReferenceNo` là mã tham chiếu giao dịch do server tạo.

Mã này phải unique và được sử dụng để hiển thị hoặc đối chiếu giao dịch.

### IdempotencyKey

`IdempotencyKey` được Client tạo cho các request yêu cầu idempotency, đặc biệt là Transfer.

Business identifier không thay thế authorization. Backend vẫn phải kiểm tra ownership và role của user.

## Seed Development

Development Seed Data hiện tạo:

- 5 Customer.
- 1 Teller.
- 1 Admin.
- 5 Customer Profile.
- 17 Bank Account.

Các tài khoản demo:

```text
Customer 0900000001
├── 0900000001
├── 1000000003
├── 1234567890
└── 1234567891

Customer 0900000002
├── 0900000002
├── 1000000004
├── 2234567890
└── 2234567891

Customer 0900000003
├── 3000000001
├── 3000000002
└── 3000000003

Customer 0900000004
├── 4000000001
├── 4000000002
└── 4000000003

Customer 0900000005
├── 5000000001
├── 5000000002
└── 5000000003

teller → Teller
admin  → Admin
```

Đối với Customer demo:

```text
Phone = UserName
```

Seed Data chỉ phục vụ Development/Demo.

Password, Transaction Password, số dư và thông tin Customer trong seed không phải dữ liệu thật.

## Quy tắc 3NF

Database tách dữ liệu theo trách nhiệm:

```text
ApplicationUser
→ authentication, security và role

CustomerProfile
→ thông tin Customer

BankAccount
→ tài khoản và current balance

FinancialTransaction
→ lịch sử chuyển động tiền

AuditLog
→ lịch sử hành động nghiệp vụ và bảo mật

UserSession / RefreshToken
→ authentication session
```

Cách tách này hạn chế việc lặp dữ liệu.

User security không được lặp lại trong Customer Profile.

```text
ApplicationUser
        │
        0..1
        │
CustomerProfile
```

Customer Profile được tách khỏi Bank Account để một Customer có thể sở hữu nhiều tài khoản:

```text
CustomerProfile
        1
        │
        N
BankAccount
```

Financial Transaction tham chiếu đến Bank Account và User thay vì sao chép lại thông tin Customer.

Statement được tạo từ dữ liệu hiện có nên không có bảng Statement riêng.

`Balance` được lưu trên `BankAccount` để phục vụ truy vấn và thực hiện giao dịch hiệu quả.

`FinancialTransaction` lưu lịch sử các chuyển động tiền đã hoàn tất.

## Tính nhất quán của giao dịch

Các nghiệp vụ làm thay đổi balance phải thực hiện các bước liên quan trong cùng một SQL transaction.

Đối với Transfer:

```text
Debit Source Account
        +
Credit Destination Account
        +
Create FinancialTransaction
        ↓
COMMIT
```

Nếu một bước thất bại:

```text
ROLLBACK
```

Hệ thống không được cập nhật một tài khoản nhưng bỏ sót tài khoản còn lại hoặc không tạo Financial Transaction tương ứng.

`RowVersion` trên Bank Account hỗ trợ phát hiện concurrency conflict khi nhiều request cùng cập nhật dữ liệu.

Redis không được sử dụng để lưu hoặc tính balance.

## Tính nhất quán của sao kê

Statement không có bảng riêng.

Dữ liệu sao kê được tạo từ:

```text
BankAccount
+
FinancialTransaction
```

Khi tạo sao kê, hệ thống phải đọc balance và transaction history trong một cửa sổ dữ liệu nhất quán để tránh trường hợp hai giá trị thuộc hai thời điểm khác nhau.

Các nghiệp vụ thay đổi balance luôn phải cập nhật Bank Account và tạo Financial Transaction trong cùng SQL transaction.

Thứ tự giao dịch trong sao kê sử dụng thời gian tạo và `Id` để giữ kết quả ổn định khi nhiều giao dịch có timestamp gần nhau.

PDF chỉ được tạo sau khi dữ liệu sao kê đã được đọc xong. Việc render PDF không phải một phần của transaction thay đổi balance.

## Quan hệ chính dùng cho ERD

Các quan hệ chính của SUBank:

```text
AspNetRoles
      1
      │
      N
AspNetUserRoles
      N
      │
      1
AspNetUsers
      │
      ├──── 0..1 CustomerProfile
      │              │
      │              1
      │              │
      │              N
      │          BankAccount
      │           ↙       ↘
      │      Source         Destination
      │          ↘         ↙
      │      FinancialTransaction
      │
      ├────< AuditLog
      │
      ├────< UserSession
      │
      └────< RefreshToken
```

`RefreshToken` và `UserSession` cùng sử dụng `UserId` và `SessionId` để xác định logical session nhưng không có Foreign Key trực tiếp giữa hai bảng.

Redis, SignalR, QR, PDF và Statement không phải bảng SQL nên không xuất hiện như entity trong ERD.