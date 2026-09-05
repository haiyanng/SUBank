# Backup và Restore Database

## Mục tiêu

SUBank sử dụng SQL Server làm nguồn dữ liệu chính cho:

- Customer;
- Bank Account;
- Financial Transaction;
- Audit Log;
- Authentication và Session history.

Backup cần được thực hiện trước các thay đổi database có nguy cơ làm mất dữ liệu.

Tài liệu này mô tả quy trình cơ bản. Nó không khẳng định môi trường Production hiện đã có hệ thống backup tự động.

## Development

Development sử dụng database:

```text
SUBankV2
```

API có thể tự chạy Migration và Seed Data khi Development configuration cho phép.

Với database chỉ chứa Demo Data, có thể xóa và seed lại nếu dữ liệu không cần giữ.

Nếu database có dữ liệu cần bảo toàn, nên backup trước khi:

- chạy migration phá hủy;
- thay đổi schema lớn;
- test migration;
- xóa hoặc reset database.

## Migration

EF Core Migration được lưu trong project Infrastructure.

Repository hiện có cả các migration thêm và xóa schema.

Ví dụ migration:

```text
RemoveAddressChangeWorkflow
```

đã từng xóa bảng `AddressChangeRequests`.

Migration dạng:

```sql
DROP TABLE ...
```

có thể làm mất dữ liệu.

Không nên coi `Down()` của EF Core Migration là cơ chế backup.

Nếu dữ liệu đã bị xóa khỏi database, việc chạy migration ngược không tự khôi phục lại các row cũ.

## Backup SQL Server

Với SQL Server tự quản lý, có thể tạo backup trước migration:

```sql
BACKUP DATABASE [SUBankV2]
TO DISK = N'D:\SQLBackups\SUBankV2.bak'
WITH CHECKSUM;
```

Sau khi tạo backup có thể kiểm tra:

```sql
RESTORE VERIFYONLY
FROM DISK = N'D:\SQLBackups\SUBankV2.bak'
WITH CHECKSUM;
```

Đường dẫn backup phải phù hợp với máy chạy SQL Server.

Không commit file `.bak` vào Git.

Nếu sử dụng database managed trên cloud, sử dụng cơ chế backup, snapshot hoặc Point-in-Time Restore của provider.

## Trước khi chạy migration

Thực hiện theo thứ tự:

```text
Xác nhận đúng database
        ↓
Kiểm tra Migration History
        ↓
Tạo backup / snapshot
        ↓
Xác nhận backup thành công
        ↓
Chạy migration
        ↓
Kiểm tra database
```

Có thể kiểm tra Migration History bằng:

```sql
SELECT [MigrationId], [ProductVersion]
FROM [dbo].[__EFMigrationsHistory]
ORDER BY [MigrationId];
```

Production không tự chạy Migration từ startup của SUBank.

Migration Production phải được thực hiện riêng.

## Restore

Không restore thử trực tiếp lên database đang chạy.

Nên restore sang database khác, ví dụ:

```text
SUBankV2_RestoreTest
```

Sau restore cần kiểm tra:

```text
Database mở được
Migration History đúng
Các bảng chính tồn tại
Dữ liệu chính đọc được
```

Có thể kiểm tra số lượng bản ghi:

```sql
SELECT COUNT(*) FROM CustomerProfiles;
SELECT COUNT(*) FROM BankAccounts;
SELECT COUNT(*) FROM FinancialTransactions;
SELECT COUNT(*) FROM AuditLogs;
```

Nếu cần kiểm tra toàn vẹn SQL Server:

```sql
DBCC CHECKDB (N'SUBankV2_RestoreTest');
```

Chỉ chuyển sang database đã restore sau khi xác nhận dữ liệu hợp lệ.

## Dữ liệu nhạy cảm

Backup database có thể chứa:

- thông tin Customer;
- địa chỉ;
- email;
- số điện thoại;
- Account Number;
- Transaction history;
- Audit Log.

Vì vậy:

- không commit backup vào Git;
- không gửi backup công khai;
- không ghi password hoặc connection string vào tài liệu;
- giới hạn người có quyền truy cập;
- sử dụng encryption của SQL Server hoặc provider khi triển khai thực tế.

## Production / Demo

Khi chọn database provider cần kiểm tra:

- provider có backup hay không;
- retention bao lâu;
- có Point-in-Time Restore hay không;
- restore được sang database mới hay không;
- backup có được mã hóa hay không.

Không nên ghi trong tài liệu rằng backup Production đã hoạt động nếu chưa thực sự cấu hình và thử restore.

## Trạng thái hiện tại

SUBank đã có EF Core Migration và quy trình Development database.

Backup/Restore Production phụ thuộc vào database provider được chọn khi deployment.

Cho đến khi có deployment thực tế và restore thử thành công, trạng thái phù hợp là:

```text
Có quy trình backup/restore
Chưa kiểm chứng trên môi trường Production/Demo thực tế
```