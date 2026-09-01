# SUBank V2

Đây là SUBank V2.4, ứng dụng ngân hàng mô phỏng dạng modular monolith xây dựng bằng .NET 10, ASP.NET Core Web API, Blazor WebAssembly, Bootstrap, SQL Server và ASP.NET Core Identity. Repository hiện đã vượt phạm vi scaffold P0 và đang hoàn thiện các feature P1.

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

Lần khởi động cuối tạo lại role, bốn user, hai CustomerProfile và tám tài khoản demo. Không chạy quy trình xóa database với connection string production.

## Chạy trên máy local

```powershell
dotnet restore
dotnet build SUBank.sln
dotnet test SUBank.sln --no-build
dotnet run --project src/SUBank.Api --launch-profile https
dotnet run --project client/SUBank.Client --launch-profile https
```

API chạy ở `https://localhost:7247`. `/health/live` kiểm tra tiến trình; `/health/ready` và `/health` kiểm tra SQL Server cùng Redis. Swagger có tại `/swagger`. Client Development chạy tách port ở `https://localhost:7081`.

Hai project Development chỉ cung cấp launch profile `https`; profile HTTP cũ đã bỏ vì không tương thích với refresh cookie `Secure` và exact-origin CORS.

Khi publish API, target MSBuild tự restore/publish Blazor Client và chép asset vào cùng artifact để demo/Production chạy cùng origin. Cấu hình host, TLS, proxy, migration và checklist smoke test nằm trong [Deployment.md](docs/Deployment.md). Quy trình backup, export dữ liệu trước migration và restore drill nằm trong [Backup-Restore-Runbook.md](docs/Backup-Restore-Runbook.md).

## Application Log

API ghi log có cấu trúc dạng JSON Lines ra console. Trong Development, rolling file được bật tại `src/SUBank.Api/logs/subank-api-YYYYMMDD.log`; file log bị Git bỏ qua và không được commit. Có thể theo dõi bằng:

```powershell
Get-Content -Encoding UTF8 src/SUBank.Api/logs/subank-api-*.log -Wait
```

Request log chỉ dùng route template, status, thời gian xử lý và correlation ID; không thu raw URL/query/body/token/mật khẩu. Cấu hình gốc dùng cho production tắt file sink để ưu tiên stdout của nền tảng hosting. Xem phạm vi, retention và giới hạn backup tại [Application-Logging.md](docs/Application-Logging.md).

## Tài khoản demo Development

| Tên đăng nhập | Role | Mật khẩu đăng nhập | Mật khẩu giao dịch |
|---|---|---|---|
| `0900000001` | Customer | `Demo@12345` | `123456` |
| `0900000002` | Customer | `Demo@12345` | `123456` |
| `teller` | Teller | `Demo@12345` | Không có |
| `admin` | Admin | `Demo@12345` | Không có |

Tài khoản ngân hàng demo:

- Customer `0900000001`: tài khoản chính `0900000001`; tài khoản phụ `1000000003`, `1234567890`, `1234567891`.
- Customer `0900000002`: tài khoản chính `0900000002`; tài khoản phụ `1000000004`, `2234567890`, `2234567891`.

Với Customer demo, số điện thoại, tên đăng nhập và số tài khoản chính là cùng một giá trị. Teller và Admin tiếp tục dùng username nghiệp vụ. Seed Development tự đổi username `customer.a`/`customer.b` và số tài khoản chính cũ tại chỗ để giữ nguyên ID, số dư và lịch sử giao dịch; bản ghi Identity legacy trùng nhưng không có hồ sơ, giao dịch hoặc audit sẽ được xóa an toàn.

Phiên và access token Customer hết hạn tuyệt đối sau 15 phút từ lúc đăng nhập. Tab foreground tự trở về login; tab bị browser suspend kiểm tra lại ngay khi visible/focus/pageshow. Reload chỉ khôi phục phần thời gian còn lại và server vẫn từ chối protected API sau deadline. Teller/Admin dùng access token 15 phút, refresh theo nhu cầu và logical session tối đa bảy ngày.

Access token không được lưu persistent và refresh token chỉ nằm trong HttpOnly cookie. Browser chỉ giữ `SessionId`/logout intent không phải credential. Mỗi tab bind đúng session; login nơi khác thay session cũ. Login, refresh và logout được serialize xuyên tab bằng Web Lock giữ xuyên suốt request để tránh response cookie cũ ghi đè cookie mới. Phạm vi demo hỗ trợ Chrome/Edge hiện đại và fail closed nếu Web Locks, `sessionStorage` hoặc `localStorage` không khả dụng. Client kiểm tra tối thiểu response login ngay trong Web Lock; response không đọc được, sai cấu trúc hoặc lệch `SessionId` sẽ bị chặn và thu hồi bù trước khi nhả khóa. Logout khóa UI ngay; nếu cả cookie logout lẫn bearer fallback đều không xác nhận thu hồi, trang login hiển thị trạng thái `logout-unconfirmed`.

Các secret trên chỉ là dữ liệu demo. Production phải cấp `ConnectionStrings__DefaultConnection`, `Jwt__SigningKey` và `ActiveSession__RedisConnection` từ secret store; cấu hình mặc định cố ý để trống nhằm fail-fast.

## Phạm vi

P0 đã có authentication/authorization, khóa và mở khóa user, tài khoản/lịch sử, chuyển tiền nội bộ, Teller cash deposit, migration/seed và audit cơ bản. P1 đã có active-session control, SignalR notification, hồ sơ khách hàng chỉ đọc, sao kê tháng/năm kèm PDF server và SUBank QR nội bộ; AI chưa triển khai. QR hỗ trợ tạo ảnh, quét camera hoặc upload PNG/JPEG/WebP rồi điền trước form chuyển tiền; camera trên trình duyệt yêu cầu HTTPS. Xem [PROJECT-BLUEPRINT.md](docs/PROJECT-BLUEPRINT.md) và [Nhật ký lỗi/phát hiện kỹ thuật](docs/Issue-Register.md).

Giao diện dùng Bootstrap và visual system riêng của SUBank, hỗ trợ responsive từ 320px, trạng thái loading/error/empty và điều hướng theo Customer/Teller/Admin. Figma vẫn `PENDING` cho đến khi giao diện chạy thật được chủ dự án duyệt.
