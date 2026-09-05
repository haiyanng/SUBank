# Triển khai SUBank

## Mô hình triển khai

### Development

Trong Development:

```text
Blazor Client
https://localhost:7081

ASP.NET Core API
https://localhost:7247

SQL Server
SUBankV2

Redis
localhost:6379
```

Client và API chạy riêng để thuận tiện phát triển.

Development cho phép API tự:

- chạy EF Core Migration;
- tạo Demo Seed Data.

### Production / Demo

Khi publish, ASP.NET Core API đồng thời publish Blazor WebAssembly Client và đưa static file của Client vào `wwwroot`.

Vì vậy có thể triển khai SUBank dưới cùng một HTTPS origin:

```text
/
→ Blazor WebAssembly

/api/*
→ REST API

/hubs/*
→ SignalR
```

Không cần deploy Client thành một application riêng nếu sử dụng topology này.

## Publish

Lệnh publish:

```bash
dotnet publish src/SUBank.Api/SUBank.Api.csproj -c Release
```

`SUBank.Api.csproj` có build target tự publish `SUBank.Client` và copy `wwwroot` vào artifact của API.

## Cấu hình bắt buộc

Các cấu hình Production quan trọng gồm:

```text
ConnectionStrings__DefaultConnection
Jwt__SigningKey
ActiveSession__RedisConnection
ActiveSession__KeyPrefix
AllowedHosts
```

Secret thật không được ghi trực tiếp vào repository.

JWT Signing Key phải có ít nhất 32 byte.

Production không cho phép:

```text
AllowedHosts = *
```

## Migration và Seed Data

Trong Development:

```text
ApplyMigrationsOnStartup = true
SeedDemoData = true
```

có thể được sử dụng để tạo database demo nhanh.

Ngoài Development, API từ chối khởi động nếu bật hai cấu hình này.

Vì vậy Production Migration phải được thực hiện riêng trước hoặc trong quá trình deployment.

Không chạy Demo Seed Data trên Production.

## HTTPS

Ngoài Development, API bật:

```text
HSTS
HTTPS Redirection
```

Nếu ứng dụng chạy sau reverse proxy, có thể bật Forwarded Headers.

Khi bật:

```text
DeploymentSecurity__UseForwardedHeaders=true
```

phải khai báo các proxy tin cậy trong:

```text
DeploymentSecurity__KnownProxies
```

## Health Check

SUBank có các endpoint:

```text
/health/live
→ kiểm tra process API

/health/ready
→ kiểm tra SQL Server và Redis

/health
→ readiness tương tự /health/ready
```

`/health/live` không phụ thuộc SQL hoặc Redis.

Readiness trả lỗi nếu dependency cần thiết không hoạt động.

## Logging

Production mặc định ghi Application Log ra console.

Rolling file mặc định tắt trong cấu hình Production.

Chi tiết logging nằm trong `Application-Logging.md`.

## Trước khi demo

Kiểm tra tối thiểu:

- HTTPS hoạt động.
- Trang Blazor tải được.
- Login hoạt động.
- SQL Server kết nối được.
- Redis kết nối được.
- `/health/ready` trả trạng thái Healthy.
- SignalR kết nối được.
- Production secret không nằm trong repository.
- Migration đã được áp dụng đúng database.

## Trạng thái hiện tại

Repository đã có cấu hình để publish SUBank thành một ứng dụng ASP.NET Core + Blazor WebAssembly cùng origin.

Provider triển khai Production/Demo cụ thể chưa phải một phần cố định của source code và cần được chọn, cấu hình và kiểm chứng riêng.