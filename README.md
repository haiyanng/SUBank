# SUBank V2

Đây là P0 của ứng dụng ngân hàng mô phỏng dạng modular monolith, xây dựng bằng .NET 10, ASP.NET Core Web API, Blazor WebAssembly, Bootstrap, SQL Server và ASP.NET Core Identity.

## Yêu cầu môi trường

- .NET SDK 10
- SQL Server local có hỗ trợ Windows Authentication
- Redis tại `localhost:6379` để login và protected API hoạt động trong Development
- Development HTTPS certificate (`dotnet dev-certs https --trust` nếu máy chưa tin cậy)

Database Development là `SUBankV2`. API tự áp dụng migration và tạo dữ liệu demo khi khởi động trong môi trường Development.

Để dựng lại database Development sạch (lệnh đầu tiên xóa toàn bộ dữ liệu trong đúng database `SUBankV2`):

```powershell
dotnet ef database drop --force --project src/SUBank.Infrastructure --startup-project src/SUBank.Api
dotnet ef database update --project src/SUBank.Infrastructure --startup-project src/SUBank.Api
dotnet run --project src/SUBank.Api --launch-profile https
```

Lần khởi động cuối tạo lại role, bốn user, hai CustomerProfile và hai tài khoản demo. Không chạy quy trình xóa database với connection string production.

## Chạy trên máy local

```powershell
dotnet restore
dotnet build SUBank.sln
dotnet test SUBank.sln --no-build
dotnet run --project src/SUBank.Api --launch-profile https
dotnet run --project client/SUBank.Client --launch-profile https
```

API chạy ở `https://localhost:7247`, cung cấp `/health` và Swagger tại `/swagger`. Client chạy ở `https://localhost:7081`.

## Tài khoản demo Development

| User | Role | Mật khẩu đăng nhập | Mật khẩu giao dịch |
|---|---|---|---|
| `customer.a` | Customer | `Demo@12345` | `123456` |
| `customer.b` | Customer | `Demo@12345` | `123456` |
| `teller` | Teller | `Demo@12345` | Không có |
| `admin` | Admin | `Demo@12345` | Không có |

Các secret trên chỉ là dữ liệu demo. Production phải cấp `ConnectionStrings__DefaultConnection`, `Jwt__SigningKey` và `ActiveSession__RedisConnection` từ secret store; cấu hình mặc định cố ý để trống nhằm fail-fast.

## Phạm vi

P0 đã có authentication/authorization, khóa và mở khóa user, tài khoản/lịch sử, chuyển tiền nội bộ, Teller cash deposit, migration/seed và audit cơ bản. P1 active-session control đã được triển khai trên feature branch; SignalR, QR, PDF, address workflow và AI chưa triển khai. Xem [PROJECT-BLUEPRINT.md](docs/PROJECT-BLUEPRINT.md).
