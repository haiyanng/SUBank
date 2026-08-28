# Thiết kế bảo mật SUBank V2.4

## Trạng thái

ĐANG TRIỂN KHAI THEO FEATURE - ACTIVE SESSION ĐÃ CÓ CODE VÀ TEST NỀN TẢNG

Tài liệu này mô tả control dự kiến. Một control chỉ được coi là hoàn thành khi có code, cấu hình và test evidence tương ứng.

## Authentication và Identity

- Sử dụng ASP.NET Core Identity và password hashing mặc định của framework.
- Không có customer self-registration.
- Development seed tạo tối thiểu bốn user riêng: hai Customer, một Teller và một Admin.
- Teller và Admin không dùng chung user và không được gán chéo role trong seed mặc định.
- Customer đăng nhập bằng `ApplicationUser.UserName` bắt buộc trùng chính xác `CustomerProfile.Phone` ở dạng 10 chữ số; Teller và Admin dùng username nghiệp vụ. Không sử dụng Email làm login identifier.
- Email và Phone nghiệp vụ chỉ nằm trong `CustomerProfile`; không tạo bảng `CustomerContact` và không dùng Identity Email/Phone làm nguồn contact của Customer.
- `CustomerProfile.Phone` là nguồn sự thật. Username Customer chỉ phản chiếu một chiều để Identity đăng nhập; mọi luồng đổi số điện thoại tương lai phải cập nhật cả hai giá trị nguyên tử và thu hồi phiên cũ.
- Login password và transaction password là hai credential khác nhau, có hash riêng.
- Không lưu hoặc log plaintext password, transaction password hoặc hash của chúng.

## Login lockout

Sử dụng trạng thái lockout sẵn có của Identity: `AccessFailedCount`, `LockoutEnabled` và `LockoutEnd`. Không tạo `FailedLoginAttempts` hoặc `IsLocked` custom trùng chức năng.

Lần sai thứ ba liên tiếp khóa user cho đến khi Admin mở khóa. Có thể bổ sung `LockedAtUtc` chỉ để hiển thị/audit. Đăng nhập thành công reset failure count. Admin unlock phải reset failure count, lockout end và locked timestamp, đồng thời ghi audit.

Login endpoint có rate limiting riêng theo IP/user. Thông báo lỗi không được tiết lộ password, hash hoặc trạng thái nội bộ không cần thiết.

## Token và cookie

- Access token là JWT thời hạn ngắn, chứa tối thiểu `sub`, role và `sid`.
- Blazor chỉ giữ access token trong memory; cấm lưu vào `localStorage` hoặc `sessionStorage`.
- Refresh token là chuỗi ngẫu nhiên entropy cao, chỉ gửi bằng `HttpOnly`, `Secure` cookie.
- SQL chỉ lưu hash của refresh token.
- Refresh token được rotate sau mỗi lần refresh thành công và token cũ bị revoke.
- Logout, session replacement và security event phải revoke token/session liên quan.

Development tách HTTPS port và chỉ cho phép exact Client origin cùng credential. Production dùng same origin. Endpoint refresh/logout sử dụng cookie phải có CSRF control, bao gồm SameSite policy phù hợp, kiểm tra origin và anti-forgery strategy khi cần.

## Authorization

Hệ thống áp dụng cả role-based authorization và resource ownership:

- Customer chỉ truy cập account, transaction, statement, beneficiary và AI data của chính mình.
- Teller Cash Deposit yêu cầu role Teller.
- Unlock User và xem Audit Log yêu cầu role Admin.
- Teller gọi Admin endpoint phải nhận `403`.
- Admin gọi Teller Cash Deposit endpoint phải nhận `403`.
- Việc ẩn menu trên Client không thay thế API authorization.
- Thay đổi identifier trong URL/DTO không được giúp Customer truy cập dữ liệu người khác.

## Active session và Redis

Redis lưu một active `sid` cho mỗi user. Thay session phải nguyên tử. Protected request kiểm tra `sub`/`sid` sau JWT authentication và trước khi chạy use case.

Khi active key thiếu, không khớp hoặc Redis unavailable, hệ thống không được bỏ qua kiểm tra. Trả `401` cho session không hợp lệ và `503` khi dependency cần thiết unavailable. SignalR `ForceLogout` chỉ cải thiện UX; middleware và Redis mới có thẩm quyền bảo mật.

Trạng thái implementation: đã có Redis adapter, atomic replace/compare-delete bằng Lua, SQL `UserSession`, middleware fail-closed, refresh/logout gắn với active `sid`, cookie `Secure`, CSRF custom header kèm Origin allow-list, correlation ID và rate limit cho login/transaction password. SignalR đã gửi `ForceLogout` best-effort cho session cũ; integration test vẫn xác nhận REST của session cũ bị Redis/middleware trả `401`.

## Bảo vệ nghiệp vụ tiền

- Chỉ Application use case chuyên biệt được thay đổi balance.
- Transfer và Teller Cash Deposit chạy trong explicit SQL transaction.
- Balance authorization luôn dùng dữ liệu SQL, không dùng cache.
- Transfer bắt buộc có idempotency key và optimistic concurrency bằng `RowVersion`.
- Transaction password attempt có rate limiting, safe error và audit.
- SignalR notification chỉ gửi sau khi commit và không được dùng làm nguồn balance.

## Input, enumeration và dữ liệu nhạy cảm

- Mọi DTO đều được validate ở server.
- QR payload, transaction description và AI question là untrusted input.
- Account resolve yêu cầu authentication, exact match, rate limit và response tối thiểu để giảm enumeration.
- Không expose internal database ID dạng dễ đoán, hash, raw token, Redis key, secret hoặc unnecessary profile data.
- Chỉ dùng EF Core LINQ hoặc parameterized SQL; cấm SQL ghép chuỗi từ input user/AI.

## Logging, audit và secret

- Secret nằm trong user-secrets hoặc environment/secret setting của provider; không commit giá trị thật.
- Không log password, transaction password, raw JWT, raw refresh token, API key, connection-string secret hoặc full sensitive identity data.
- Request có correlation ID nhưng không log sensitive body.
- Global exception handler ghi technical log và trả safe ProblemDetails.
- Security/business event quan trọng được ghi `AuditLog` với actor, action, result, target và thời gian.

## Security test tối thiểu

- Sai login password ba lần khóa user; login đúng reset failure count.
- Chỉ Admin mở khóa được user.
- Teller bị `403` ở Admin endpoint; Admin bị `403` ở Teller Cash Deposit.
- Customer A không truy cập được account/transaction của Customer B.
- Refresh-token rotation, reuse, revoke và logout.
- Session mới làm session cũ nhận `401` dù SignalR bị ngắt.
- Duplicate transfer không chuyển tiền hai lần.
- Concurrent transfer không double-spend.
- AI không thực thi write tool hoặc arbitrary SQL.

## Nội dung còn chờ bằng chứng

- Threat model đã được con người review.
- Cookie/CSRF behavior đã test trên browser thật.
- Secret đã cấu hình trên provider.
- HTTPS deployment đã kiểm chứng.
- Penetration/security test result.

Các nội dung trên phải giữ trạng thái chờ cho đến khi có evidence thật.
