# SUBank V2

Đây là P0 của ứng dụng ngân hàng mô phỏng dạng modular monolith, xây dựng bằng .NET 10, ASP.NET Core Web API, Blazor WebAssembly, Bootstrap, SQL Server và ASP.NET Core Identity.

## Yêu cầu môi trường

- .NET SDK 10
- SQL Server local có hỗ trợ Windows Authentication
- Redis tại `localhost:6379` để login, protected API và realtime session hoạt động trong Development
- Development HTTPS certificate (`dotnet dev-certs https --trust` nếu máy chưa tin cậy)

Database Development là `SUBankV2`. API tự áp dụng migration và tạo dữ liệu demo khi khởi động trong môi trường Development.

Để dựng lại database Development sạch (lệnh đầu tiên xóa toàn bộ dữ liệu trong đúng database `SUBankV2`):

```powershell
dotnet ef database drop --force --project src/SUBank.Infrastructure --startup-project src/SUBank.Api
dotnet ef database update --project src/SUBank.Infrastructure --startup-project src/SUBank.Api
dotnet run --project src/SUBank.Api --launch-profile https
```

Lần khởi động cuối tạo lại role, bốn user, hai CustomerProfile và bốn tài khoản demo. Không chạy quy trình xóa database với connection string production.

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

| Số điện thoại / tên đăng nhập | Role | Mật khẩu đăng nhập | Mật khẩu giao dịch |
|---|---|---|---|
| `0900000001` | Customer | `Demo@12345` | `123456` |
| `0900000002` | Customer | `Demo@12345` | `123456` |
| `teller` | Teller | `Demo@12345` | Không có |
| `admin` | Admin | `Demo@12345` | Không có |

Tài khoản ngân hàng demo:

- Customer `0900000001`: `1000000001` và `1000000003`.
- Customer `0900000002`: `1000000002` và `1000000004`.

Customer bắt buộc đăng nhập bằng đúng số điện thoại trong `CustomerProfile`; Teller và Admin tiếp tục dùng username nghiệp vụ. Seed Development tự đổi `customer.a`/`customer.b` cũ sang số điện thoại mà vẫn giữ nguyên user ID và dữ liệu liên quan.

Các secret trên chỉ là dữ liệu demo. Production phải cấp `ConnectionStrings__DefaultConnection`, `Jwt__SigningKey` và `ActiveSession__RedisConnection` từ secret store; cấu hình mặc định cố ý để trống nhằm fail-fast.

## Phạm vi

P0 đã có authentication/authorization, khóa và mở khóa user, tài khoản/lịch sử, chuyển tiền nội bộ, Teller cash deposit, migration/seed và audit cơ bản. P1 đã có active-session control, SignalR notification, hồ sơ khách hàng chỉ đọc, address workflow, sao kê tháng/năm kèm PDF server và SUBank QR nội bộ; AI chưa triển khai. QR hỗ trợ tạo ảnh, quét camera hoặc upload PNG/JPEG/WebP rồi điền trước form chuyển tiền; camera trên trình duyệt yêu cầu HTTPS. Xem [PROJECT-BLUEPRINT.md](docs/PROJECT-BLUEPRINT.md).

Giao diện dùng Bootstrap và visual system riêng của SUBank, hỗ trợ responsive từ 320px, trạng thái loading/error/empty và điều hướng theo Customer/Teller/Admin. Figma vẫn `PENDING` cho đến khi giao diện chạy thật được chủ dự án duyệt.
