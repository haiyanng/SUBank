# Triển khai SUBank

## Mục tiêu

Production/demo dùng một HTTPS origin: ASP.NET Core phục vụ Blazor WebAssembly, REST dưới `/api` và SignalR dưới `/hubs`. Development vẫn chạy Client và API trên hai port HTTPS riêng.

`dotnet publish src/SUBank.Api/SUBank.Api.csproj -c Release` tự publish Client và chép `wwwroot` vào artifact API. Không deploy riêng output Client khi dùng topology này.

## Cấu hình bắt buộc

Secret và connection string chỉ được cấp qua secret store/environment của nền tảng, không ghi vào repository:

- `Jwt__SigningKey`: ít nhất 32 byte ngẫu nhiên; không dùng key Development.
- `ConnectionStrings__DefaultConnection`: SQL Server runtime credential không có quyền DDL.
- `ActiveSession__RedisConnection`: Redis bật TLS khi provider hỗ trợ.
- `ActiveSession__KeyPrefix`: prefix riêng cho môi trường.
- `AllowedHosts`: danh sách hostname thật, phân tách bằng dấu `;`; Production không chấp nhận `*`.

`DatabaseInitialization:ApplyMigrationsOnStartup` và `SeedDemoData` mặc định là `false`. API fail-fast nếu một trong hai cờ bị bật ngoài Development. Migration Production phải chạy bằng job/principal riêng sau backup theo [Backup-Restore-Runbook.md](Backup-Restore-Runbook.md); runtime Production không có quyền tự migrate hoặc seed demo.

## TLS và reverse proxy

Production bật HSTS và HTTPS redirection. Nếu TLS kết thúc tại reverse proxy, proxy phải chuyển `X-Forwarded-For` và `X-Forwarded-Proto`.

Chỉ bật `DeploymentSecurity__UseForwardedHeaders=true` khi đã biết IP proxy tin cậy. Mỗi IP phải được khai báo trong `DeploymentSecurity__KnownProxies__0`, `__1`, ... . API từ chối khởi động nếu bật forwarded headers mà không có allow-list. Không tin toàn bộ forwarded headers từ Internet vì kẻ tấn công có thể giả mạo scheme/IP.

Nếu provider không công bố IP/CIDR proxy ổn định, chưa được tự bật chế độ tin mọi proxy. Phải chốt cấu hình theo tài liệu chính thức của provider và ghi bằng chứng bên dưới.

## Health check

- `/health/live`: process còn sống; không probe dependency.
- `/health/ready`: SQL Server và Redis sẵn sàng.
- `/health`: alias readiness để tương thích cấu hình cũ.

Load balancer chỉ gửi traffic khi readiness trả `200`. Readiness `503` không được coi là lỗi process để restart liên tục.

## Checklist bằng chứng trước demo

- [ ] Provider/URL/region và hostname đã chốt.
- [ ] HTTPS request được app nhận là `https`; HTTP bị redirect hoặc từ chối tại edge.
- [ ] `AllowedHosts` chỉ có hostname thật.
- [ ] Cookie refresh có `Secure`, `HttpOnly`, `SameSite=Strict` và đúng path.
- [ ] `/`, deep link `/accounts` và một asset `/_framework/*` trả về đúng nội dung.
- [ ] Route API không tồn tại trả `404`, không trả `index.html`.
- [ ] `/health/live` và `/health/ready` được cấu hình đúng vai trò.
- [ ] SignalR kết nối qua WebSocket và nhận đúng scheme/origin.
- [ ] Backup/PITR, alert và restore drill có evidence.

Cho đến khi các ô trên được kiểm tra trên host thật, phần phụ thuộc provider của X02 và X08 vẫn ở trạng thái chờ xác minh.
