# Thiết kế bảo mật SUBank V2.4

## Trạng thái

ĐANG TRIỂN KHAI THEO FEATURE - ACTIVE SESSION ĐÃ CÓ CODE; BẰNG CHỨNG BROWSER/REDIS/CONCURRENCY CÒN CHỜ

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

Login endpoint hiện có rate limiting riêng theo IP, 30 request/phút; chưa có partition riêng theo username. Thông báo lỗi không được tiết lộ password, hash hoặc trạng thái nội bộ không cần thiết.

## Token và cookie

- Access token là JWT thời hạn ngắn, chứa tối thiểu `sub`, role và `sid`.
- Blazor chỉ giữ access token trong .NET/WASM memory; cấm lưu access token vào `localStorage`, `sessionStorage`, cookie do JavaScript đọc được hoặc `history.state`.
- Refresh token là chuỗi ngẫu nhiên entropy cao, chỉ gửi bằng `HttpOnly`, `Secure` cookie.
- Browser chỉ lưu dữ liệu điều phối không phải credential: `SessionId` theo tab trong `sessionStorage`, và logout intent gắn đúng `SessionId` trong `localStorage`. Không dùng `history.state` hoặc cookie JavaScript làm fallback vì các bản sao đó có thể mất/thoái lui khi điều hướng. Storage lỗi, dữ liệu sai cấu trúc hoặc không đọc/ghi lại đúng phải fail closed. Những giá trị này không có thẩm quyền authentication.
- SQL chỉ lưu hash của refresh token.
- Refresh token được rotate sau mỗi lần refresh thành công và token cũ bị revoke.
- Customer có logical session tuyệt đối theo `Jwt:CustomerSessionMinutes`, hiện là 15 phút kể từ lần đăng nhập. Access token Customer hết hạn đúng mốc này; Client dùng thời lượng server trả, `Stopwatch` và đối chiếu wall clock để xóa giao diện. Khi browser suspend tab, `visibilitychange`, `focus` và `pageshow` buộc kiểm tra lại ngay lúc quay lại foreground. Reload chỉ khôi phục phần thời gian còn lại và không cộng thêm 15 phút; SQL vẫn chặn đúng deadline nếu timer giao diện bị trì hoãn.
- Teller/Admin có logical session tuyệt đối theo `Jwt:RefreshTokenDays`, hiện là bảy ngày. Client refresh access token theo nhu cầu trước protected request khi còn không quá một phút hoặc khi SignalR reconnect; không có heartbeat/timer refresh nền. Protected request ngoài phạm vi QR chỉ retry tối đa một lần bằng request mới. Customer không proactive refresh và không retry protected request bằng refresh token.
- Refresh token thay thế, cookie và Redis TTL của mọi role không được kéo dài quá mốc `UserSession.ExpiresAtUtc` ban đầu.
- Tab đã bind `SessionId` chỉ được bootstrap/non-bootstrap refresh đúng session đó; mismatch khóa restore thay vì âm thầm đổi identity hoặc role. Tab mới chưa có binding có thể nhận session từ shared HttpOnly cookie rồi tự bind.
- Pipeline protected request ngoài phạm vi QR chụp session generation và chính bearer token dùng để gửi. Response cũ bị bỏ nếu session đổi trong lúc chờ; request không được replay dưới token/session mới.
- Backend dùng atomic conditional update để chỉ một request được rotate cùng refresh token. Request đồng thời thua trong grace period trả `409`; reuse ngoài grace mới thu hồi toàn bộ session.
- `(UserId, SessionId)` là ranh giới token family. Logout bằng refresh token nhận diện được trong family, kể cả token đã rotate/revoke/hết hạn, revoke toàn bộ refresh token còn hiệu lực cùng `UserSession`. Nếu cookie đã thuộc session mới, bearer `sub`/`sid` của tab cũ chỉ được revoke đúng session cũ và cookie mới phải được giữ nguyên. Token cookie không nhận diện được chỉ được xác nhận session revoked khi bearer hợp lệ xác định đúng session.
- Refresh và logout khóa cùng hàng `UserSession` trước khi rotate/revoke để token kế tiếp không lọt qua race. Session replacement và security event cũng phải revoke token/session liên quan.
- Login, refresh và logout cùng mutate refresh cookie nên Client dùng một Web Lock xuyên tab, giữ khóa cho đến khi `fetch`, response body và kiểm tra response tối thiểu hoàn tất. Không có lease TTL có thể hết hạn khi request còn chạy. Browser không hỗ trợ Web Locks hoặc không đọc/ghi được `sessionStorage`/`localStorage` phải fail closed; phạm vi demo hỗ trợ Chrome/Edge hiện đại. Request có timeout 30 giây, chờ khóa tối đa 45 giây; Backend vẫn phải xử lý rotation/revocation nguyên tử vì browser abort không chứng minh server đã dừng.
- Khi logout, Client lưu logout intent theo đúng `SessionId`, block restore và xóa UI trước khi gọi server; tab thuộc session khác không bị khóa nhầm. Cookie request gửi CSRF header và expected-session header nhưng không gửi bearer. Server commit SQL revocation trước, sau đó Redis compare-delete và SignalR `ForceLogout` best-effort. Client chỉ coi cookie logout được xác nhận khi nhận `X-SUBank-Session-Revoked: 1`; nếu thiếu marker hoặc request lỗi, endpoint bearer-bound với `credentials: omit` thu hồi đúng session cũ mà không mutate shared cookie. Cookie chỉ bị expire khi nó thuộc session đang logout hoặc không còn nhận diện được. Phiên login bị Client từ chối dùng cùng đường bearer-bound. JavaScript kiểm tra access token không rỗng, `SessionId` Guid N lowercase, thời hạn và user/roles ngay trong Web Lock; body không đọc được, sai schema hoặc lệch `SessionId` với header thì chặn restore và thử thu hồi bù trước khi nhả khóa. Nếu không xác định được session, browser chặn toàn bộ restore cho đến lần login tường minh; nếu header/body có hai ID canonical khác nhau, cả hai đều được đưa vào compensation.

Development tách HTTPS port và chỉ cho phép exact Client origin cùng credential. Production dùng same origin. Refresh/logout bắt buộc custom CSRF header; nếu request có `Origin` thì giá trị phải là same-origin hoặc exact Development Client origin. Refresh cookie dùng `SameSite=Strict`.

## Authorization

Hệ thống áp dụng cả role-based authorization và resource ownership:

- Customer chỉ truy cập account, transaction, statement, beneficiary và AI data của chính mình.
- Teller Cash Deposit yêu cầu role Teller.
- Unlock User và xem Audit Log yêu cầu role Admin.
- Teller gọi Admin endpoint phải nhận `403`.
- Admin gọi Teller Cash Deposit endpoint phải nhận `403`.
- Việc ẩn menu trên Client không thay thế API authorization.
- Thay đổi identifier trong URL/DTO không được giúp Customer truy cập dữ liệu người khác.
- Fallback authorization policy là deny-by-default: endpoint không được đánh dấu public phải authenticated; health, static/SPA fallback và public auth endpoint phải explicit anonymous.

## Active session và Redis

Redis lưu một active `sid` cho mỗi user. Thay session phải nguyên tử. Protected request kiểm tra `sub`/`sid` sau JWT authentication và trước khi chạy use case.

Khi active key thiếu, không khớp hoặc Redis unavailable, hệ thống không được bỏ qua kiểm tra. Sau khi Redis xác nhận `sid`, protected request và Hub còn đối chiếu `UserSession` cùng trạng thái active/lockout bền vững trong SQL. Trả `401` cho session không hợp lệ và `503` khi dependency cần thiết unavailable. SignalR `ForceLogout` chỉ cải thiện UX; Redis cùng SQL mới là lớp có thẩm quyền bảo mật.

Trạng thái implementation: đã có Redis adapter, atomic replace/compare-delete/conditional-renew bằng Lua, SQL `UserSession`, middleware fail-closed, kiểm tra durable session/user lock, absolute session lifetime theo role, concurrent refresh claim, full-family logout, cookie `Secure`, CSRF custom header kèm Origin allow-list, correlation ID và rate limit cho login/transaction password/Teller deposit. Refresh/logout dùng chung `UserSession` aggregate lock. Lockout thu hồi SQL trước Redis; login activation lỗi có compensation ở Redis và SQL. Logout dùng expected `SessionId`, bearer fallback, SQL durable-first và post-commit Redis/SignalR best-effort. Client có foreground expiry check, per-tab binding, session-specific logout intent, Web Lock giữ xuyên suốt auth-cookie fetch, BroadcastChannel/storage notification có rerun và generation guard chống stale response. SignalR dùng group theo session, kiểm tra Redis + SQL, đóng khi JWT hết hạn và Client reconnect bằng token hiện hành. Build đã thành công nhưng các thay đổi này chưa được chạy browser nhiều tab, Redis integration hoặc concurrency/network-failure test theo quyết định hiện tại của chủ dự án.

## Bảo vệ nghiệp vụ tiền

- Chỉ Application use case chuyên biệt được thay đổi balance.
- Transfer và Teller Cash Deposit chạy trong explicit SQL transaction.
- Balance authorization luôn dùng dữ liệu SQL, không dùng cache.
- Transfer bắt buộc có idempotency key và optimistic concurrency bằng `RowVersion`.
- Transaction password attempt có rate limiting, safe error và audit.
- Teller Cash Deposit có rate limit riêng theo Teller để giảm spam request dùng idempotency key mới.
- SignalR notification chỉ gửi sau khi commit, chỉ tới session đang active và không được dùng làm nguồn balance.

## Input, enumeration và dữ liệu nhạy cảm

- Mọi DTO đều được validate ở server.
- QR payload, transaction description và AI question là untrusted input.
- Account resolve yêu cầu authentication, exact match, rate limit và response tối thiểu để giảm enumeration.
- Không expose internal database ID dạng dễ đoán, hash, raw token, Redis key, secret hoặc unnecessary profile data.
- Chỉ dùng EF Core LINQ hoặc parameterized SQL; cấm SQL ghép chuỗi từ input user/AI.

## Logging, audit và secret

- Secret nằm trong user-secrets hoặc environment/secret setting của provider; không commit giá trị thật.
- Không log password, transaction password, raw JWT, raw refresh token, API key, connection-string secret hoặc full sensitive identity data.
- Header correlation chỉ được chấp nhận khi dài tối đa 100 ký tự và thuộc allow-list chữ/số/`-`/`_`/`.`; giá trị khác được thay bằng GUID do server sinh. Correlation ID không phải credential.
- Request logger chỉ ghi method, route template, status và elapsed time; không đọc raw path, route value, query, body, cookie hoặc authorization header.
- Global exception handler ghi technical log và trả safe ProblemDetails kèm `correlationId`; response cũng có `X-Correlation-ID`.
- Enricher loại bỏ một allow-list top-level property nhạy cảm trước sink, nhưng không phải sanitizer tổng quát cho nested object hoặc exception. Mọi lời gọi `ILogger` vẫn phải tuân thủ quy tắc không truyền secret/PII.
- Security/business event quan trọng được ghi `AuditLog` với actor, action, result, target và thời gian.

Console/file technical log, SQL `AuditLog` và `FinancialTransaction` là ba nguồn riêng. Rolling file Development không phải backup; xem [Application-Logging.md](Application-Logging.md).

## Security test tối thiểu

- Sai login password ba lần khóa user; login đúng reset failure count.
- Chỉ Admin mở khóa được user.
- Teller bị `403` ở Admin endpoint; Admin bị `403` ở Teller Cash Deposit.
- Customer A không truy cập được account/transaction của Customer B.
- Refresh-token rotation, reuse, revoke; logout bằng cookie hiện hành và cookie cũ sau rotation đều phải thu hồi toàn family.
- Session mới làm session cũ nhận `401` dù SignalR bị ngắt.
- Session cũ không nhận `BalanceChanged` hoặc `TransactionReceived` sau khi bị thay thế.
- SignalR reconnect dùng access token hiện hành, tự retry sau initial/closed failure và ngừng retry khi logout.
- Tab cũ bootstrap không được revoke hoặc nhận nhầm session mới từ shared cookie.
- Hai tab login/refresh/logout đồng thời không được làm response `Set-Cookie` cũ ghi đè cookie mới.
- Logout khi cookie đã thuộc session khác chỉ revoke session của bearer cũ và giữ cookie session mới.
- Logout mất mạng vẫn khóa UI; hidden Customer tab phải hết phiên ngay khi quay lại foreground.
- Response protected request cũ sau khi session đổi không được cập nhật UI hoặc retry dưới session mới.
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
