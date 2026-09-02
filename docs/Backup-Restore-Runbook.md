# Runbook backup, phục hồi và bảo toàn dữ liệu trước migration

## 1. Mục tiêu và giới hạn

Tài liệu này hướng dẫn bảo vệ dữ liệu SQL Server của SUBank trước một thay đổi schema có khả năng làm mất dữ liệu, đặc biệt là migration `20260828023954_RemoveAddressChangeWorkflow` xóa bảng `AddressChangeRequests`.

Runbook giải quyết phần có thể chuẩn bị trong repository: cách nhận diện trạng thái database, tạo và kiểm tra backup, xuất riêng dữ liệu sắp bị xóa, restore drill trên database tách biệt và mẫu bằng chứng vận hành. Tài liệu **không chứng minh** môi trường deploy thật đã có backup. X08 chỉ được đóng sau khi cấu hình backup/PITR trên nhà cung cấp, bật cảnh báo và có bằng chứng restore thành công.

Các nguyên tắc bắt buộc:

- Không sửa, xóa hoặc viết lại migration lịch sử đã có thể được áp dụng ở bất kỳ database nào.
- Không dùng `Down()` của migration như cơ chế khôi phục dữ liệu. `Down()` chỉ dựng lại bảng rỗng, không thể tái tạo các dòng đã bị `DROP TABLE` xóa.
- Không restore đè lên database đang chạy. Mọi restore drill phải dùng một database mới, có tên tách biệt và không được cấu hình cho ứng dụng production.
- Không chạy migration nếu chưa xác nhận đúng server, đúng database, trạng thái backup và nơi lưu bản sao.
- Không lưu connection string, mật khẩu, access key hoặc encryption key trong Git, log hay biên bản bằng chứng.

## 2. Rủi ro cụ thể của migration xóa quy trình đổi địa chỉ

Migration `20260828023954_RemoveAddressChangeWorkflow` thực hiện:

```sql
DROP TABLE [AddressChangeRequests];
```

Bảng lịch sử gồm các cột `Id`, `RequestNo`, `CustomerProfileId`, `PermanentAddress`, `TemporaryAddress`, `Status`, `RequestedAtUtc`, `DecidedAtUtc`, `DecidedByUserId` và `RejectionReason`. Trong đó địa chỉ là dữ liệu cá nhân, vì vậy bản export và backup phải được mã hóa, giới hạn quyền đọc và có thời hạn lưu rõ ràng.

Migration bù sau này chỉ nên được tạo mới theo quy trình EF Core thông thường. Không sửa migration `AddAddressChangeWorkflow` hoặc `RemoveAddressChangeWorkflow` để làm cho lịch sử ở các database khác nhau bị phân kỳ.

## 3. Vai trò tối thiểu

Trong dự án một người, cùng một người có thể kiêm nhiều vai trò nhưng vẫn phải ghi rõ từng lần thao tác:

| Vai trò | Trách nhiệm |
|---|---|
| Chủ thay đổi | Chốt migration cần áp dụng, phạm vi dữ liệu và thời điểm bảo trì |
| Người vận hành database | Chạy backup/export/verify/restore drill bằng tài khoản riêng có quyền tối thiểu |
| Người duyệt | Đối chiếu đúng server/database, kiểm tra bằng chứng và quyết định tiếp tục hoặc dừng |
| Người giữ khóa | Quản lý secret và khóa mã hóa độc lập với nơi chứa file backup |

Nếu chỉ có một người, cần thực hiện hai lượt kiểm tra tách biệt và lưu timestamp cho cả bước chuẩn bị lẫn bước duyệt.

## 4. Kiểm tra trước khi thao tác

### 4.1. Xác nhận đích chính xác

Chạy bằng công cụ quản trị SQL đã được phê duyệt, không chép credential vào command history:

```sql
SELECT
    @@SERVERNAME AS ServerName,
    DB_NAME() AS DatabaseName,
    ORIGINAL_LOGIN() AS LoginName,
    SYSUTCDATETIME() AS CheckedAtUtc;
```

Dừng ngay nếu `ServerName` hoặc `DatabaseName` không khớp phiếu triển khai. Không dựa vào tên cửa sổ terminal hoặc tên connection profile để suy đoán.

### 4.2. Xác định trạng thái migration và bảng

```sql
SELECT [MigrationId], [ProductVersion]
FROM [dbo].[__EFMigrationsHistory]
WHERE [MigrationId] IN
(
    N'20260826054339_AddAddressChangeWorkflow',
    N'20260828023954_RemoveAddressChangeWorkflow'
)
ORDER BY [MigrationId];

SELECT OBJECT_ID(N'dbo.AddressChangeRequests', N'U') AS AddressChangeRequestsObjectId;
```

Phân nhánh theo kết quả:

| Migration xóa | Bảng | Xử lý |
|---|---|---|
| Chưa có | Còn tồn tại | Thực hiện backup đầy đủ và export bảng theo mục 5 trước khi chạy migration |
| Chưa có | Không tồn tại | Trạng thái bất thường; dừng triển khai và điều tra lịch sử thao tác/schema |
| Đã có | Không tồn tại | Không thể export từ database hiện tại; dùng backup/PITR trước migration theo mục 6 |
| Đã có | Vẫn tồn tại | Trạng thái bất thường; dừng, kiểm tra migration history và thao tác thủ công |

### 4.3. Điều kiện cho phép tiếp tục

Chỉ tiếp tục khi đồng thời có:

- cửa sổ thay đổi và người chịu trách nhiệm;
- bản backup trước migration tại vị trí độc lập với filesystem của ứng dụng;
- `RESTORE VERIFYONLY ... WITH CHECKSUM` thành công đối với SQL Server self-managed, hoặc trạng thái snapshot/backup thành công từ provider;
- export riêng `AddressChangeRequests` nếu bảng vẫn còn dữ liệu;
- hash, kích thước, row count và vị trí lưu của artifact;
- phương án restore sang database tách biệt;
- xác nhận ứng dụng không tự chạy migration ngoài cửa sổ đã duyệt.

## 5. Nhánh A: database chưa áp dụng migration xóa

### 5.1. Ghi nhận dữ liệu trước thay đổi

```sql
SELECT
    COUNT_BIG(*) AS RowCount,
    MIN([RequestedAtUtc]) AS OldestRequestedAtUtc,
    MAX([RequestedAtUtc]) AS NewestRequestedAtUtc
FROM [dbo].[AddressChangeRequests];

SELECT [Status], COUNT_BIG(*) AS RowCount
FROM [dbo].[AddressChangeRequests]
GROUP BY [Status]
ORDER BY [Status];
```

Không đưa nội dung địa chỉ, CCCD, số điện thoại hoặc dữ liệu cá nhân vào ticket/log. Chỉ lưu số lượng tổng hợp trong bằng chứng.

### 5.2. Tạo full backup trước migration

Với SQL Server tự quản lý, lệnh minh họa dưới đây phải được thay bằng tên database và đường dẫn tuyệt đối đã duyệt. Đường dẫn là filesystem nhìn từ tiến trình SQL Server, không nhất thiết là máy chạy ứng dụng.

```sql
USE [master];
GO

BACKUP DATABASE [SUBankV2]
TO DISK = N'D:\SQLBackups\SUBankV2\SUBankV2_pre_RemoveAddressChangeWorkflow_YYYYMMDDTHHMMSSZ.bak'
WITH COPY_ONLY, CHECKSUM, COMPRESSION, INIT, STATS = 10;
GO
```

`COPY_ONLY` tránh làm thay đổi chuỗi differential backup hiện hữu. Không dùng `INIT` nếu đường dẫn chưa được tạo riêng cho lần chạy này vì nó có thể ghi đè media set cũ.

Nếu dùng Azure SQL Database hoặc database managed không hỗ trợ file `.bak`, sử dụng point-in-time restore/snapshot/export chính thức của provider. Ghi lại resource ID, UTC timestamp, retention và trạng thái job; không tuyên bố có backup chỉ vì gói dịch vụ quảng cáo tính năng đó.

### 5.3. Xuất riêng `AddressChangeRequests`

Full backup là artifact phục hồi chính. Export bảng là lớp bảo vệ bổ sung để có thể tra cứu dữ liệu cũ mà không phải đưa toàn bộ database phục hồi vào ứng dụng.

Một lựa chọn cho SQL Server self-managed là `bcp` native format. Chạy bằng cơ chế xác thực an toàn của môi trường; không đặt mật khẩu trực tiếp trên command line:

```powershell
bcp "SELECT Id, RequestNo, CustomerProfileId, PermanentAddress, TemporaryAddress, Status, RequestedAtUtc, DecidedAtUtc, DecidedByUserId, RejectionReason FROM [SUBankV2].[dbo].[AddressChangeRequests] ORDER BY Id" queryout "D:\SQLExports\AddressChangeRequests_YYYYMMDDTHHMMSSZ.bcp" -n -T -S "TEN_SQL_SERVER"
```

Sau khi export:

```powershell
Get-Item -LiteralPath "D:\SQLExports\AddressChangeRequests_YYYYMMDDTHHMMSSZ.bcp" | Select-Object FullName, Length, LastWriteTimeUtc
Get-FileHash -Algorithm SHA256 -LiteralPath "D:\SQLExports\AddressChangeRequests_YYYYMMDDTHHMMSSZ.bcp"
```

Không dùng CSV đơn giản cho bảng này nếu chưa có quy tắc escape/encoding được kiểm chứng vì địa chỉ và lý do từ chối có thể chứa dấu phẩy, xuống dòng hoặc Unicode. File export chứa PII phải được mã hóa và không được commit vào repository.

### 5.4. Kiểm tra backup trước khi chạy migration

```sql
RESTORE HEADERONLY
FROM DISK = N'D:\SQLBackups\SUBankV2\SUBankV2_pre_RemoveAddressChangeWorkflow_YYYYMMDDTHHMMSSZ.bak';
GO

RESTORE VERIFYONLY
FROM DISK = N'D:\SQLBackups\SUBankV2\SUBankV2_pre_RemoveAddressChangeWorkflow_YYYYMMDDTHHMMSSZ.bak'
WITH CHECKSUM;
GO
```

`VERIFYONLY` chỉ chứng minh SQL Server đọc được backup và kiểm tra một số cấu trúc/checksum. Nó **không** thay thế restore drill.

Chỉ áp dụng migration sau khi các bước trên thành công và bằng chứng đã được lưu. Sau migration, xác nhận migration history có bản ghi mới và bảng đã biến mất đúng theo quyết định phạm vi.

## 6. Nhánh B: database đã áp dụng migration xóa

Nếu migration xóa đã có trong `__EFMigrationsHistory`, dữ liệu không còn trong database hiện tại. Không chạy `database update` về migration cũ với kỳ vọng lấy lại dữ liệu; `Down()` chỉ tạo bảng rỗng.

Thực hiện theo thứ tự:

1. Xác định UTC timestamp migration được chạy từ deployment log, SQL audit/provider activity log hoặc bằng chứng thay đổi.
2. Chọn backup/snapshot/PITR có thời điểm ngay trước timestamp đó.
3. Restore vào database mới, ví dụ `SUBankV2_Recovery_Address_YYYYMMDD`; không restore đè database hiện tại.
4. Xác nhận database recovery có bảng và số dòng mong đợi.
5. Export `AddressChangeRequests` từ database recovery theo mục 5.3.
6. Giữ ứng dụng production trỏ vào database hiện tại. Dữ liệu cũ chỉ được đọc/xử lý theo quyết định nghiệp vụ và bảo vệ PII.
7. Nếu không có backup/PITR trước migration, ghi nhận mất dữ liệu không thể phục hồi; không tạo dữ liệu giả để che khoảng trống.

Việc nhập dữ liệu lịch sử trở lại hệ thống không nằm trong migration và không được tự động thực hiện, vì chức năng đổi địa chỉ đã bị loại khỏi phạm vi. Nếu sau này cần lưu trữ lịch sử, tạo thiết kế archive/migration mới được review thay vì sửa migration cũ.

## 7. Restore drill trên database tách biệt

### 7.1. SQL Server self-managed

Đọc logical file name trước:

```sql
RESTORE FILELISTONLY
FROM DISK = N'D:\SQLBackups\SUBankV2\SUBankV2_pre_RemoveAddressChangeWorkflow_YYYYMMDDTHHMMSSZ.bak';
GO
```

Sau đó restore vào **tên database mới** và **đường dẫn file mới**. Các placeholder phải được đối chiếu với kết quả `FILELISTONLY`:

```sql
USE [master];
GO

RESTORE DATABASE [SUBankV2_RestoreDrill_YYYYMMDD]
FROM DISK = N'D:\SQLBackups\SUBankV2\SUBankV2_pre_RemoveAddressChangeWorkflow_YYYYMMDDTHHMMSSZ.bak'
WITH
    MOVE N'LOGICAL_DATA_NAME' TO N'D:\SQLData\SUBankV2_RestoreDrill_YYYYMMDD.mdf',
    MOVE N'LOGICAL_LOG_NAME' TO N'D:\SQLData\SUBankV2_RestoreDrill_YYYYMMDD_log.ldf',
    CHECKSUM,
    RECOVERY,
    STATS = 10;
GO
```

Không thêm `REPLACE`. Nếu database drill đã tồn tại, dừng và chọn tên mới thay vì ghi đè.

### 7.2. Provider managed

Dùng chức năng restore/copy database của provider để tạo resource/database mới tại cùng thời điểm recovery. Khóa network access về IP/service account của người drill, không nối deployment đang chạy vào database recovery và không seed dữ liệu demo lên đó.

### 7.3. Kiểm tra sau restore

```sql
DBCC CHECKDB (N'SUBankV2_RestoreDrill_YYYYMMDD') WITH NO_INFOMSGS;
GO

USE [SUBankV2_RestoreDrill_YYYYMMDD];
GO

SELECT TOP (20) [MigrationId], [ProductVersion]
FROM [dbo].[__EFMigrationsHistory]
ORDER BY [MigrationId] DESC;

SELECT COUNT_BIG(*) AS CustomerProfileCount FROM [dbo].[CustomerProfiles];
SELECT COUNT_BIG(*) AS BankAccountCount FROM [dbo].[BankAccounts];
SELECT COUNT_BIG(*) AS FinancialTransactionCount FROM [dbo].[FinancialTransactions];
SELECT COUNT_BIG(*) AS AuditLogCount FROM [dbo].[AuditLogs];

IF OBJECT_ID(N'dbo.AddressChangeRequests', N'U') IS NOT NULL
BEGIN
    SELECT COUNT_BIG(*) AS AddressChangeRequestCount
    FROM [dbo].[AddressChangeRequests];
END;
```

Đối chiếu row count và các aggregate không nhạy cảm với số liệu trước backup. Nếu cần smoke test ứng dụng, dùng deployment tách biệt và tài khoản chỉ đọc; tắt migration/seed startup. Không dùng khách hàng thật để kiểm tra.

Database drill chỉ được xóa sau khi đã lưu bằng chứng và được phê duyệt. Xóa database là thao tác phá hủy nên không có script tự động trong repository này.

## 8. Mục tiêu RPO, RTO và retention

Các giá trị dưới đây là **mục tiêu đề xuất**, chưa phải cam kết đã đạt. Chủ dự án phải chốt lại theo nền tảng deploy và ngân sách:

| Môi trường | RPO mục tiêu | RTO mục tiêu | Backup/PITR đề xuất | Retention đề xuất |
|---|---:|---:|---|---|
| Development local | 24 giờ | 4 giờ | Full backup trước migration phá hủy và cuối ngày có thay đổi dữ liệu quan trọng | 7 bản ngày + 4 bản tuần |
| Demo public | 1 giờ | 2 giờ | Provider PITR nếu có; thêm snapshot/full backup trước mỗi release | PITR 7–14 ngày + 4 snapshot tuần |
| Production-like | 15 phút | 2 giờ | PITR/log backup theo provider; full backup/snapshot định kỳ và bản sao độc lập | PITR tối thiểu 14–35 ngày; monthly archive theo yêu cầu pháp lý/nghiệp vụ |

- **RPO** là lượng dữ liệu tối đa có thể mất tính theo thời gian. Ví dụ RPO 1 giờ đòi hỏi recovery point không cũ hơn một giờ.
- **RTO** là thời gian tối đa để khôi phục dịch vụ ở mức đã định nghĩa, gồm phát hiện, quyết định, restore và xác minh.
- Retention cho backup chứa PII không được kéo dài vô thời hạn. Khi hết hạn, xóa theo chính sách của provider/storage và lưu bằng chứng xóa, không xóa thủ công thiếu kiểm soát.

Nếu gói miễn phí không hỗ trợ PITR hoặc không đạt mục tiêu trên, phải ghi rõ khoảng cách và hạ mức kỳ vọng của môi trường; không mô tả demo như production-ready.

## 9. Mã hóa, phân quyền và vị trí lưu

- Bật encryption at rest của database, backup storage và bản sao độc lập; truyền backup qua TLS.
- Khóa/mật khẩu giải mã phải nằm trong secret manager hoặc cơ chế bảo mật của provider, tách khỏi backup và repository.
- Tài khoản chạy backup chỉ có quyền backup cần thiết; tài khoản restore drill chỉ có quyền trên resource drill. Người xem ứng dụng/log không mặc định được tải backup.
- Bật MFA cho tài khoản quản trị provider nếu nền tảng hỗ trợ.
- Giữ ít nhất một bản sao ở failure domain khác với database chính. Persistent volume cùng host/container không phải bản sao độc lập.
- Audit lượt tạo, tải, restore và xóa backup. Không ghi tên file chứa PII vào log public nếu tên đó tiết lộ dữ liệu nhạy cảm.

## 10. Giám sát và cảnh báo

Cần cảnh báo cho các tình huống:

- backup/snapshot/PITR job thất bại hoặc không chạy đúng lịch;
- recovery point mới nhất cũ hơn RPO;
- retention bị thay đổi hoặc backup bị xóa ngoài kế hoạch;
- storage gần đầy;
- checksum/verify thất bại;
- restore drill quá RTO hoặc kiểm tra dữ liệu không khớp;
- quyền truy cập backup/encryption key bị thay đổi.

Kênh cảnh báo phải đến một nơi được theo dõi thực sự, ví dụ email hoặc dashboard của provider. Chụp màn hình cấu hình không đủ; cần ít nhất một lần gửi cảnh báo thử có timestamp và người xác nhận.

## 11. Mẫu bằng chứng cho mỗi backup/restore drill

Không điền secret hoặc dữ liệu cá nhân vào mẫu:

```text
Mã lần thực hiện:
Môi trường:
Provider / SQL Server version:
Server/resource ID đã che bớt:
Database:
Người thực hiện:
Người duyệt:
Thời điểm bắt đầu UTC:
Thời điểm kết thúc UTC:

Migration/schema trước backup:
Recovery point UTC:
Loại backup: full / snapshot / PITR / export
Vị trí lưu (đã che thông tin nhạy cảm):
Mã hóa at rest: BẬT/TẮT/CHƯA XÁC MINH
Kích thước artifact:
SHA-256 (nếu có file):
Retention đến UTC:
Job/provider status:
RESTORE VERIFYONLY kết quả (nếu áp dụng):

Database drill tách biệt:
DBCC CHECKDB kết quả:
Migration history kiểm tra:
Các row count/aggregate đã đối chiếu:
AddressChangeRequests row count trước migration/recovery:
RPO thực tế:
RTO thực tế:
Cảnh báo thử đã nhận:

Sai lệch/rủi ro còn lại:
Quyết định: ĐẠT / KHÔNG ĐẠT / CHẤP NHẬN CÓ ĐIỀU KIỆN
Đường dẫn evidence nội bộ:
Ngày drill tiếp theo:
```

## 12. Tiêu chí hoàn tất R10 và X08

### R10 – dữ liệu của migration xóa bảng

Phần repository được giảm thiểu khi runbook này được review. R10 chỉ có thể coi là xử lý đầy đủ cho một database cụ thể khi:

- đã xác định database thuộc nhánh A hay B;
- có backup/recovery point trước migration;
- đã export hoặc phục hồi được `AddressChangeRequests`, hoặc có quyết định bằng văn bản rằng database demo không có dữ liệu cần giữ;
- bằng chứng có row count, timestamp và người duyệt;
- không sửa migration lịch sử.

### X08 – backup và restore

Không được đóng X08 chỉ vì đã có tài liệu này. Còn phụ thuộc nền tảng deploy:

- chọn database provider và gói dịch vụ;
- cấu hình lịch backup/PITR thật;
- cấu hình storage độc lập, encryption, IAM và retention;
- cấu hình cảnh báo và kiểm tra cảnh báo;
- chạy restore drill thật, đạt RPO/RTO đã chốt;
- lưu evidence đã được con người review.

Cho đến khi đủ các điều kiện trên, trạng thái đúng là: **ĐÃ CÓ RUNBOOK TRONG REPO – CHƯA KIỂM CHỨNG TRÊN MÔI TRƯỜNG DEPLOY**.
