# SUBank 3S

SUBank 3S là ứng dụng Online Banking được xây dựng bằng .NET 10, ASP.NET Core Web API, Blazor WebAssembly, Bootstrap, SQL Server và ASP.NET Core Identity.

## Tech Stack

* Backend: ASP.NET Core 10 Web API
* Frontend: Blazor WebAssembly + Bootstrap
* Database: SQL Server + Entity Framework Core
* Authentication: ASP.NET Core Identity + JWT
* Cache & Session: Redis
* Realtime: SignalR
* API Documentation: Swagger

## Yêu cầu môi trường

* .NET SDK 10
* SQL Server hỗ trợ Windows Authentication
* Redis tại `localhost:6379`
* Development HTTPS certificate

Nếu máy chưa trust HTTPS certificate:

```powershell
dotnet dev-certs https --trust
```

Database Development mặc định là `SUBankV2`.

API tự động áp dụng migration và tạo dữ liệu demo khi khởi động trong môi trường Development.

## Chạy project

### 1. Restore và build

```powershell
dotnet restore
dotnet build SUBank.sln
dotnet test SUBank.sln --no-build
```

### 2. Chạy API

```powershell
dotnet run --project src/SUBank.Api --launch-profile https
```

API:

`https://localhost:7247`

Swagger:

`https://localhost:7247/swagger`

### 3. Chạy Blazor Client

Mở terminal khác:

```powershell
dotnet run --project client/SUBank.Client --launch-profile https
```

Client:

`https://localhost:7081`

## Reset database Development

> Lệnh dưới đây sẽ xóa toàn bộ dữ liệu trong database `SUBankV2`.

```powershell
dotnet ef database drop --force --project src/SUBank.Infrastructure --startup-project src/SUBank.Api

dotnet ef database update --project src/SUBank.Infrastructure --startup-project src/SUBank.Api

dotnet run --project src/SUBank.Api --launch-profile https
```

Sau khi seed, hệ thống có:

* 5 Customer
* 1 Teller
* 1 Admin
* 5 Customer Profile
* 17 tài khoản ngân hàng demo

## Tài khoản demo

| Username     | Role     | Password     | Transaction Password |
| ------------ | -------- | ------------ | -------------------- |
| `0900000001` | Customer | `Demo@12345` | `123456`             |
| `0900000002` | Customer | `Demo@12345` | `123456`             |
| `0900000003` | Customer | `Demo@12345` | `123456`             |
| `0900000004` | Customer | `Demo@12345` | `123456`             |
| `0900000005` | Customer | `Demo@12345` | `123456`             |
| `teller`     | Teller   | `Demo@12345` | —                    |
| `admin`      | Admin    | `Demo@12345` | —                    |

### Tài khoản ngân hàng demo

* Customer `0900000001`: `0900000001`, `1000000003`, `1234567890`, `1234567891`
* Customer `0900000002`: `0900000002`, `1000000004`, `2234567890`, `2234567891`
* Customer `0900000003`: `3000000001`, `3000000002`, `3000000003`
* Customer `0900000004`: `4000000001`, `4000000002`, `4000000003`
* Customer `0900000005`: `5000000001`, `5000000002`, `5000000003`

Dữ liệu trên chỉ phục vụ Development/Demo.

## Chức năng chính

### Customer

* Đăng nhập và quản lý phiên
* Xem danh sách tài khoản và số dư
* Xem lịch sử giao dịch
* Chuyển tiền nội bộ
* Chuyển tiền bằng SUBank QR
* Xem hồ sơ cá nhân
* Xem thông báo realtime
* Xem sao kê tháng/năm
* Xuất sao kê PDF

### Teller

* Đăng nhập với quyền Teller
* Tìm kiếm và xem thông tin khách hàng
* Thực hiện Cash Deposit

### Admin

* Quản lý Customer
* Tìm kiếm và xem chi tiết Customer
* Khóa/mở khóa Customer
* Thu hồi phiên của Customer khi bị khóa
* Xem Audit Log

## Authentication & Security

Hệ thống sử dụng:

* ASP.NET Core Identity
* JWT Access Token
* HttpOnly Refresh Token
* Identity Lockout
* Role-based Authorization
* Redis Active Session Control
* Single Active Session
* Audit Log

Customer bị khóa bởi Admin sẽ không thể tiếp tục sử dụng protected API và phiên đang hoạt động sẽ bị thu hồi.

## Application Log

Trong Development, API ghi log tại:

```text
src/SUBank.Api/logs/subank-api-YYYYMMDD.log
```

Theo dõi log bằng:

```powershell
Get-Content -Encoding UTF8 src/SUBank.Api/logs/subank-api-*.log -Wait
```

## Health Check

```text
/health/live
/health/ready
/health
```

Health check được sử dụng để kiểm tra trạng thái API, SQL Server và Redis.

## Documentation

Tài liệu chi tiết nằm trong thư mục `docs/`:

* `PROJECT-BLUEPRINT.md` — kiến trúc và phạm vi project
* `Deployment.md` — hướng dẫn deployment
* `Backup-Restore-Runbook.md` — backup và restore database
* `Application-Logging.md` — logging
* `Issue-Register.md` — lỗi và các phát hiện kỹ thuật

## Ghi chú

* Project hiện không hỗ trợ Customer tự đăng ký tài khoản.
* Customer demo được tạo bằng Development Seed Data.
* Trong hệ thống ngân hàng thực tế, việc tạo Customer thuộc quy trình onboarding/KYC.
* AI/Fraud Detection hiện chưa được triển khai.
* Secret production không được lưu trực tiếp trong source code.
