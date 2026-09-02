# Nhật ký lỗi và phát hiện kỹ thuật SUBank V2.4

- Cập nhật lần đầu: 2026-08-29
- Nhánh tại thời điểm tổng hợp: `feature/demo-experience-stability`
- Commit nền gần nhất: `c58e8e3` (`feat: stabilize customer demo experience`)
- Trạng thái con người review: **CHỜ XÁC NHẬN**

## 1. Mục đích và phạm vi

Tài liệu này là nguồn theo dõi vòng đời của lỗi và phát hiện kỹ thuật: biểu hiện, nguyên nhân, tác động, trạng thái, cách đã sửa hoặc hướng xử lý được đề xuất.

Nguồn bằng chứng:

- Git history và source code hiện tại.
- `docs/AI-Usage-Report.md`, đặc biệt Entry 002, 010, 016 và 023–032.
- Các lỗi đã được chủ dự án báo trực tiếp trong quá trình demo.
- Kết quả rà soát source ngày 2026-08-28, mang mã `R01`–`R21`.

Git không lưu mọi compiler error tạm thời trong lúc code và báo cáo AI không ghi từng lần build đỏ. Vì vậy, đây là danh sách đầy đủ theo bằng chứng còn lại, không tuyên bố phục dựng được những lỗi thoáng qua chưa từng được log.

## 2. Quy ước

### Trạng thái

- **CHƯA SỬA**: source hiện tại vẫn còn vấn đề; hướng xử lý mới chỉ là đề xuất.
- **ĐÃ SỬA CỤC BỘ**: đã giảm rủi ro nhưng chưa đóng toàn bộ kịch bản.
- **ĐÃ SỬA**: đã thay đổi code/config và có bằng chứng kiểm chứng tương ứng.
- **ĐÃ KHÔI PHỤC TẠM THỜI**: dịch vụ hoạt động lại nhờ sửa môi trường/restart; rủi ro tái diễn vẫn còn.
- **TẠM HOÃN THEO QUYẾT ĐỊNH**: đã ghi nhận nhưng chưa được phép sửa.
- **CẦN XÁC MINH**: có triệu chứng được báo nhưng thiếu log để kết luận nguyên nhân riêng.
- **KHÔNG CÒN ÁP DỤNG**: feature hoặc yêu cầu liên quan đã bị loại khỏi phạm vi.

### Mức độ

- **NGHIÊM TRỌNG**: có thể sai tiền, mất dữ liệu, vượt quyền hoặc lộ bí mật.
- **CAO**: ảnh hưởng xác thực/phiên, nghiệp vụ quan trọng hoặc khả năng triển khai.
- **TRUNG BÌNH**: ảnh hưởng ổn định, UX, cấu hình hoặc độ tin cậy của test.
- **THẤP**: bảo trì, tài liệu, format hoặc hoàn thiện nhỏ.

## 3. Rủi ro được phát hiện trước khi triển khai

Những mục này được tìm thấy khi review specification. Chúng không phải lỗi runtime đã xảy ra, nhưng được giữ lại để thấy vì sao kiến trúc hiện tại được chọn.

| Mã | Phát hiện ban đầu | Trạng thái | Cách xử lý/đối chiếu hiện tại |
|---|---|---|---|
| A01 | Phiên bản và phạm vi specification chưa nhất quán; tiêu đề DOCX cũ còn nhắc V2.1. | ĐÃ SỬA TRONG REPO | Chốt V2.4 và ghi thứ tự ưu tiên trong `PROJECT-BLUEPRINT.md`; file DOCX nguồn ngoài repo không bị ghi đè. |
| A02 | Cardinality của Identity role và ý định gộp Teller/Admin không rõ. | ĐÃ SỬA | Dùng Identity many-to-many; Teller và Admin là hai user/role riêng, có thể dùng chung Staff layout nhưng API policy tách biệt. |
| A03 | Trạng thái lockout có nguy cơ bị lưu trùng và mâu thuẫn. | ĐÃ SỬA CỤC BỘ | Identity là nguồn lockout chính; `LockedAtUtc` phục vụ audit. R14 cho thấy transaction password hiện vẫn dùng nhầm login counter. |
| A04 | Cookie refresh, Development CORS và CSRF chưa có ranh giới rõ. | ĐÃ SỬA CỤC BỘ | Dùng cookie `HttpOnly`/`Secure`, credentials CORS, custom CSRF header và Origin allow-list. Vẫn cần browser/deployment test cuối. |
| A05 | Thay active session có race condition; RedLock tạo độ phức tạp không cần thiết. | ĐÃ SỬA TRONG WORKING TREE – CHỜ DUYỆT | Redis Lua thay con trỏ và compare-delete/conditional-renew nguyên tử; refresh/logout dùng chung aggregate lock; SignalR route theo active session và tự phục hồi connection. Chưa chạy kiểm thử concurrency/Redis thật. |
| A06 | Transfer cần phân biệt optimistic concurrency và idempotency. | ĐÃ SỬA CỤC BỘ | Backend có SQL transaction, `RowVersion`, unique idempotency key và request hash. R01 và R15 cho thấy retry phía Client và request đồng thời chưa hoàn chỉnh. |
| A07 | Money constraint và invariant số dư chưa được chốt. | ĐÃ SỬA CỤC BỘ | Dùng `decimal(18,2)`, amount dương, tối đa hai số lẻ, balance không âm và transaction nguyên tử. Giới hạn trên của `decimal(18,2)` được bổ sung trong R13 nhưng chưa commit. |
| A08 | `CustomerContact`, Identity Email/Phone và `CustomerProfile` có thể trùng nguồn sự thật. | ĐÃ SỬA | Bỏ `CustomerContact`; `CustomerProfile` là nguồn thông tin nghiệp vụ duy nhất. |
| A09 | Thêm `Guid PublicId` hàng loạt có thể tăng index/mapping mà không thay thế authorization. | ĐÃ SỬA | Giữ `long` nội bộ; API dùng business identifier như `AccountNumber`, `ReferenceNo`. |
| A10 | Account resolution có nguy cơ enumeration. | ĐÃ SỬA | Chỉ trả tên đã che và bổ sung rate limit `AccountResolution`. |
| A11 | Transaction password có nguy cơ brute force. | ĐÃ SỬA CỤC BỘ | Đã có rate limit và audit. R14 ghi nhận việc dùng chung login lockout counter là chưa hợp lý. |
| A12 | Cụm từ “REST/JSON hoặc SignalR” có thể khiến hiểu SignalR thay thế luồng nghiệp vụ. | ĐÃ SỬA TÀI LIỆU | REST là luồng nghiệp vụ; SignalR chỉ thông báo best-effort sau commit. |
| A13 | Deadline một tuần có nguy cơ mở rộng phạm vi quá sớm. | ĐÃ SỬA QUY TRÌNH | Chia milestone/feature theo dependency; AI và deployment được hoãn khi core flow chưa ổn định. |

## 4. Lỗi và sự cố đã phát sinh trong quá trình xây dựng

| Mã | Ngày | Mức độ | Trạng thái | Biểu hiện/nguyên nhân | Cách xử lý đã thực hiện và bằng chứng |
|---|---|---|---|---|---|
| H01 | 2026-08-26 | TRUNG BÌNH | ĐÃ SỬA CỤC BỘ | P0 ban đầu chỉ có smoke test, chưa đủ bằng chứng cho auth, RBAC và luồng tiền. | Bổ sung test lockout/unlock, ownership/RBAC, atomicity, idempotency, rollback, concurrency, deposit và audit; Entry 010 ghi 18/18 pass. R21 ghi các khoảng trống test hạ tầng còn lại. |
| H02 | 2026-08-26 | TRUNG BÌNH | ĐÃ SỬA | Identity reset `AccessFailedCount` về 0 khi vừa lock, làm Admin hiển thị sai số lần thất bại. | Khi user đang lock và counter là 0, `StaffService` hiển thị `MaxFailedAccessAttempts`; `StaffService.cs:66-76`. |
| H03 | 2026-08-26 | CAO | ĐÃ SỬA | Hai access token được tạo trong cùng một giây có thể giống nhau. | Thêm claim `jti` bằng GUID cho mỗi token; `AuthService.cs:139`. |
| H04 | 2026-08-26 | CAO | ĐÃ SỬA | Connection string có MARS khiến EF Core không tạo savepoint an toàn trong transaction. | Bỏ `MultipleActiveResultSets=True` khỏi Development connection string; `appsettings.Development.json`. |
| H05 | 2026-08-26 | TRUNG BÌNH | ĐÃ SỬA | Seed cũ có thể bỏ qua toàn bộ user khi một phần profile/account đã tồn tại. | Viết seed idempotent theo từng user/profile/account, chỉ bổ sung dữ liệu còn thiếu; `DatabaseInitializer.cs`. |
| H06 | 2026-08-26 | TRUNG BÌNH | ĐÃ SỬA | Account-resolution endpoint thiếu giới hạn tần suất. | Bổ sung fixed-window rate-limit policy `AccountResolution` theo user/IP; `Program.cs:53-63`. |
| H07 | 2026-08-26 | TRUNG BÌNH | ĐÃ SỬA | Acceptance P0 còn thiếu Admin Audit Log API/UI và trang chi tiết giao dịch. | Bổ sung service/controller/page cho audit và transaction detail; được kiểm chứng trong Entry 010. |
| H08 | 2026-08-26–27 | TRUNG BÌNH | ĐÃ SỬA | Login/wordmark bị cắt khi thu hẹp cửa sổ; trong một trạng thái UI trung gian nút đăng nhập không hiển thị rõ. | Chuyển layout login sang một cột trước 992px, chỉnh wordmark/form và khôi phục submit button. Kết quả cuối nằm trong commit `deffc81`; root cause riêng của nút ở trạng thái trung gian không được log. |
| H09 | 2026-08-27 | CAO | ĐÃ KHÔI PHỤC TẠM THỜI | Client Blazor trắng dù host có thể trả HTML; runtime pack `browser-wasm` thiếu/chưa đủ và output cũ sau thay đổi .NET. | Dừng process lỗi, clean output, restore runtime `browser-wasm`, build và restart Client. Không có source fix riêng; cache cũ có thể cần hard refresh. Xem Entry 023. |
| H10 | 2026-08-28 | CAO | ĐÃ SỬA | API crash khi seed: code tiếp tục tạo `customer.a/customer.b` và va chạm unique email với hồ sơ cũ sau khi đổi identifier. | Migrate username/account tại chỗ, giữ `UserId`, `BankAccount.Id`, balance và FK giao dịch; vô hiệu user legacy mồ côi; chỉ thêm account thiếu. Commit `c58e8e3`. |
| H11 | 2026-08-28 | TRUNG BÌNH | ĐÃ SỬA | Dropdown account bị cắt và khó cuộn khi có bốn tài khoản; menu absolute nằm trong `.bank-card` có `overflow: hidden`. | Render danh sách theo normal flow dưới thẻ, giới hạn chiều cao responsive và cho cuộn; thay ARIA listbox chưa đầy đủ bằng nhóm button. Commit `c58e8e3`. |
| H12 | 2026-08-28 | CAO | ĐÃ KHÔI PHỤC TẠM THỜI | Login báo `TypeError: Failed to fetch`; host demo chạy profile HTTP trong khi `ApiBaseUrl` và CORS dùng HTTPS. | Dừng hai host HTTP, khởi động API/Client bằng profile HTTPS; xác minh API 7247, Client 7081 và CORS preflight. Không sửa business code; xem Entry 029. |
| H13 | 2026-08-28 | CAO | ĐÃ KHÔI PHỤC TẠM THỜI | Login báo “Dịch vụ kiểm soát phiên tạm thời không khả dụng” do Docker Desktop tắt và `subank-redis` dừng. | Mở Docker, start lại container, xác minh `PONG`, port 6379 và login. Restart policy vẫn là `no`, nên sự cố có thể tái diễn. |
| H14 | 2026-08-28 | CAO | ĐÃ SỬA | Vừa login lại đã nhận thông báo “đăng nhập ở nơi khác”; `ForceLogout` đến trễ từ hub/token cũ kết thúc nhầm phiên mới. | Stop hub cũ trước login, tuần tự hóa sync, bind connection với access token và bỏ event nếu token không còn là current. `RealtimeService.cs`, `Login.razor`, commit `c58e8e3`. |
| H15 | 2026-08-28 | CAO | ĐÃ SỬA | Reload trang đã login hiện `Unauthorized · Thử lại`; Router dựng page và gọi API trước khi refresh-cookie restore hoàn tất. | Thêm authentication bootstrap gate trong `App.razor`, chờ `TryRestoreAsync`, redirect route bảo vệ và xử lý 401 tập trung trong `ApiSession`. Commit `c58e8e3`. |
| H16 | 2026-08-28 | TRUNG BÌNH | CẦN XÁC MINH | Client từng chỉ hiện “Đang tải”. Không có log/entry đủ để kết luận đây là incident riêng hay biểu hiện của H09, H12 hoặc H15. | Ghi lại trung thực, không bịa root cause. Khi tái diễn cần lưu browser console, Network, API/Client log và URL/profile đang chạy. |

## 5. Kết quả rà soát source: R01–R21

Tại snapshot này, `R01`, `R02`, `R06`–`R08`, `R13`, `R15` và `R16` đã được sửa trong working tree nhưng chưa commit. `R03`–`R05`, `R09`–`R12`, `R14` và `R17`–`R21` chưa được sửa. Mọi dòng “Đề xuất” của các mục chưa sửa bên dưới chỉ là phương án để chủ dự án quyết định.

### R01 – Idempotency phía Client không bảo vệ retry thực tế

- Mức độ: **NGHIÊM TRỌNG**
- Trạng thái: **ĐÃ SỬA TRONG WORKING TREE – CHỜ DUYỆT**
- Phát hiện: `ApiSession.TransferAsync` và `DepositAsync` tự tạo `Guid.NewGuid()` cho mỗi lần gọi. Nếu server đã commit nhưng response bị mất, lần bấm lại có key mới và có thể tạo giao dịch thứ hai.
- Bằng chứng: `client/SUBank.Client/Services/ApiSession.cs:100-115`.
- Cách sửa: `ApiSession` nhận key do caller truyền vào. `Transfer.razor` và `StaffDeposit.razor` dùng `IdempotencyIntentTracker<TIntent>` để giữ cùng key khi retry cùng payload; key chỉ được xóa sau response thành công và tự đổi khi payload nghiệp vụ thay đổi. Mật khẩu giao dịch không thuộc intent vì backend cũng không đưa credential vào request hash.
- Kiểm chứng: build Release toàn solution thành công, 0 warning và 0 error; chưa chạy test theo quyết định của chủ dự án.

### R02 – Logout không thu hồi token family khi cookie đã rotate

- Mức độ: **CAO**
- Trạng thái: **ĐÃ SỬA TRONG WORKING TREE – CHỜ DUYỆT**
- Phát hiện: `LogoutAsync` return ngay nếu token truyền vào đã revoked. Token thay thế và Redis session cùng family có thể vẫn còn hiệu lực.
- Cách sửa: dùng `(UserId, SessionId)` làm token-family key. Refresh và logout cùng khóa hàng `UserSession` bằng SQL Server `UPDLOCK` trong transaction `ReadCommitted`. Logout chấp nhận cả token đã rotate/revoke/hết hạn làm revocation handle, thu hồi mọi refresh token còn hiệu lực trong family, đánh dấu `UserSession`, compare-delete Redis trước SQL commit và retry một lần nếu SQL deadlock. Nhờ cùng aggregate lock, refresh thắng thì logout chờ rồi bắt cả token mới; logout thắng thì refresh chờ và thấy session đã revoke.
- Kiểm chứng: `git diff --check` và build Release toàn solution thành công, 0 warning và 0 error; chưa chạy stale-cookie/concurrent refresh-logout test theo quyết định của chủ dự án.

### R03 – Teller đã bị lock vẫn có thể nạp tiền bằng access token cũ

- Mức độ: **CAO**
- Trạng thái: **CHƯA SỬA**
- Phát hiện: `CashDepositAsync` không kiểm tra Teller `IsActive`/lockout; role claim và Redis session cũ vẫn có thể hợp lệ.
- Bằng chứng: `src/SUBank.Infrastructure/Banking/StaffService.cs:21-64`.
- Đề xuất chưa thực hiện: kiểm tra trạng thái Teller trước nghiệp vụ tiền và thu hồi session khi user bị khóa/vô hiệu hóa.

### R04 – Form tiền điền sẵn dữ liệu nhạy cảm

- Mức độ: **CAO**
- Trạng thái: **CHƯA SỬA**
- Phát hiện: Transfer điền sẵn đích, amount, nội dung và transaction password `123456`; Teller deposit điền sẵn account/amount/content. Form giữ dữ liệu sau submit nên dễ thao tác lại nhầm.
- Bằng chứng: `client/SUBank.Client/Pages/Transfer.razor:16-20`, `StaffDeposit.razor:13`.
- Đề xuất chưa thực hiện: chỉ tự chọn source account; để trống đích, amount, nội dung và password; xóa password ngay sau submit.

### R05 – `OnValidSubmit` không có client form validation thực sự

- Mức độ: **CAO**
- Trạng thái: **CHƯA SỬA**
- Phát hiện: Transfer và StaffDeposit không có `DataAnnotationsValidator`; model không có rule/message nên dữ liệu rỗng/âm/sai format vẫn đi tới API.
- Bằng chứng: `Transfer.razor:5-20`, `StaffDeposit.razor:5-15`.
- Đề xuất chưa thực hiện: thêm validator, validation message và rule cho account, amount, description, transaction password; server validation vẫn là lớp có thẩm quyền.

### R06 – Access token không được refresh giữa phiên

- Mức độ: **CAO**
- Trạng thái: **ĐÃ SỬA TRONG WORKING TREE – CHỜ DUYỆT**
- Phát hiện: Client chỉ refresh khi bootstrap; `ExpiresAtUtc` không được dùng. Sau khi access token hết hạn, 401 xóa session dù refresh cookie còn hạn.
- Cách sửa: `ApiSession` dùng `SemaphoreSlim`, refresh trước expiry một phút, kiểm tra access token/generation sau khi chờ và phát `Changed` khi rotate thành công. Protected REST request ngoài phần QR được tạo lại và retry tối đa một lần sau Bearer `401`; transfer/deposit giữ nguyên idempotency key. Bearer challenge được expose qua Development CORS và active-session middleware gắn challenge rõ ràng, tránh coi sai transaction password là access-token expiry.
- Kiểm chứng: build Release toàn solution thành công, 0 warning và 0 error; chưa chạy browser/concurrency test.

### R07 – Realtime có thể ngừng hẳn sau lỗi kết nối

- Mức độ: **CAO**
- Trạng thái: **ĐÃ SỬA TRONG WORKING TREE – CHỜ DUYỆT**
- Phát hiện: token provider giữ `boundAccessToken`; automatic reconnect không retry `StartAsync` ban đầu và không có `Closed` retry sau khi hết chuỗi reconnect.
- Cách sửa: `ApiSession` cung cấp access token hiện hành và logical-session generation. `RealtimeService` giữ automatic reconnect cho lỗi ngắn, bổ sung retry 2/5/10/30 giây cho initial-start và `Closed`, dispose attempt lỗi, ngừng retry khi logout/đổi phiên/dispose và bỏ callback đến muộn bằng connection instance cộng session generation.
- Kiểm chứng: build Release toàn solution thành công, 0 warning và 0 error; chưa chạy browser/network-failure test theo quyết định của chủ dự án.

### R08 – Event ngân hàng broadcast theo user group có thể tới session cũ

- Mức độ: **CAO**
- Trạng thái: **ĐÃ SỬA TRONG WORKING TREE – CHỜ DUYỆT**
- Phát hiện: `ForceLogout` chỉ là event best-effort; server không đóng/remove connection cũ. Banking event gửi theo user group nên connection cũ có thể vẫn nhận event.
- Cách sửa: bỏ user group; Hub kiểm tra `sid` với Redis trước khi thêm connection vào session group; notifier resolve active `sid` tại thời điểm gửi và không fallback broadcast nếu Redis lỗi; endpoint Hub đóng connection khi JWT hết hạn. Socket cũ có thể chưa đóng vật lý tức thì nếu bỏ lỡ `ForceLogout`, nhưng không còn là target lâu dài của event ngân hàng, không gọi được REST và bị đóng khi token hết hạn. Event đã bắt đầu gửi đúng lúc session bị thay thế vẫn có một race rất hẹp vì Redis và SignalR không có transaction chung.
- Kiểm chứng: build Release toàn solution thành công, 0 warning và 0 error; chưa chạy test thay phiên với SignalR/Redis thật theo quyết định của chủ dự án.

### R09 – Production tự áp dụng migration khi API khởi động

- Mức độ: **CAO – TRIỂN KHAI**
- Trạng thái: **CHƯA SỬA**
- Phát hiện: API luôn gọi `DatabaseInitializer`; initializer luôn `MigrateAsync`, chỉ seed mới phụ thuộc Development. Runtime API phải có quyền DDL và có thể không start nếu migration lỗi.
- Bằng chứng: `src/SUBank.Api/Program.cs:103-107`, `DatabaseInitializer.cs:14-17`.
- Đề xuất chưa thực hiện: Production dùng migration job/command riêng hoặc cờ opt-in, backup và health gate trước rollout.

### R10 – Migration xóa bảng không thể phục hồi dữ liệu

- Mức độ: **CAO – DỮ LIỆU**
- Trạng thái: **CHƯA XỬ LÝ RỦI RO TRIỂN KHAI**
- Phát hiện: migration bù cho feature đổi địa chỉ dùng `DropTable`; `Down()` chỉ tạo schema rỗng. Việc drop đúng với yêu cầu bỏ feature nhưng dữ liệu cũ mất vĩnh viễn.
- Bằng chứng: `Migrations/20260828023954_RemoveAddressChangeWorkflow.cs:14-70`.
- Đề xuất chưa thực hiện: backup/export hoặc rename/archive bảng trước khi deploy DB có dữ liệu; DB demo rỗng có thể chấp nhận sau khi ghi quyết định.

### R11 – Topology Production same-origin trong tài liệu chưa tồn tại trong code

- Mức độ: **CAO – TRIỂN KHAI**
- Trạng thái: **CHƯA SỬA**
- Phát hiện: tài liệu nói API phục vụ Blazor cùng origin, nhưng API chưa publish Client, chưa có static files và SPA fallback; publish API riêng sẽ 404 tại `/`.
- Bằng chứng: `PROJECT-BLUEPRINT.md:122-124`, `Architecture.md:78-94`, `Program.cs:127-134`.
- Đề xuất chưa thực hiện: publish/copy Blazor assets vào API `wwwroot`, map static files/fallback và smoke-test `/`; hoặc sửa tài liệu nếu chọn deploy tách origin.

### R12 – Demo seed có thể chạy nhầm vào database thật

- Mức độ: **CAO – CẤU HÌNH**
- Trạng thái: **CHƯA SỬA**
- Phát hiện: seed chứa credential Admin/Teller/Customer và chỉ được gate bằng environment Development. Gán nhầm environment với connection thật sẽ seed tài khoản demo.
- Bằng chứng: `DatabaseInitializer.cs:11-34`, `Program.cs:106`.
- Đề xuất chưa thực hiện: cờ `SeedDemoData:Enabled` mặc định false, hard-fail ngoài Development và allow-list database demo.

### R13 – Chưa giới hạn giá trị tiền theo `decimal(18,2)`

- Mức độ: **TRUNG BÌNH**
- Trạng thái: **ĐÃ SỬA TRONG WORKING TREE – CHƯA COMMIT**
- Phát hiện ban đầu: validation chỉ kiểm amount dương và hai số lẻ; amount hoặc balance sau credit vượt precision SQL có thể thành 500 hoặc 409 không đúng ngữ nghĩa.
- Cách đã sửa: thêm `MaximumMonetaryValue = 9_999_999_999_999_999.99m`; `ValidateAmount` chặn amount vượt giới hạn; `ValidateCreditedBalance` chặn `currentBalance + amount` vượt giới hạn trước khi mutate.
- File đã thay đổi: `BankingRules.cs`, `BankingService.cs`, `StaffService.cs`.
- Kiểm chứng đã thực hiện: `dotnet build SUBank.sln -c Release --no-restore` thành công, 0 warning, 0 error.
- Chưa thực hiện: chưa thêm/chạy test reject `Max + 0.01`, overflow destination/deposit và invariant balance không đổi.

### R14 – Transaction password dùng chung login lockout counter

- Mức độ: **TRUNG BÌNH**
- Trạng thái: **CHƯA SỬA**
- Phát hiện: sai transaction password gọi `AccessFailedAsync`; transaction password đúng gọi `ResetAccessFailedCountAsync`. Hai luồng đang thay đổi cùng counter của login password.
- Bằng chứng: `BankingService.cs:85-99`.
- Đề xuất chưa thực hiện: tách counter/lockout cho transaction password hoặc trong phạm vi demo chỉ dùng rate limit + audit; không reset/tăng login counter từ giao dịch.

### R15 – Hai request cùng idempotency key đồng thời không replay đúng

- Mức độ: **TRUNG BÌNH**
- Trạng thái: **ĐÃ SỬA TRONG WORKING TREE – CHỜ DUYỆT**
- Phát hiện: hai request có thể cùng vượt replay check; request thua rơi vào concurrency/unique conflict và nhận 409 dù request winner đã thành công. Chưa thấy đường double-spend chắc chắn do transaction + rowversion + constraint hiện tại.
- Cách sửa: chuẩn hóa request hash bằng invariant culture; dùng chung `IdempotencyReplay` để kiểm tra actor, key, loại giao dịch và hash. Khi `SaveChanges` conflict, transaction thua rollback bằng token độc lập với request rồi được dispose; EF ChangeTracker được xóa trước khi query lại bản ghi của request thắng. Cùng payload trả response `Replayed = true`; payload khác hoặc conflict không có winner vẫn trả 409.
- File đã thay đổi: `BankingService.cs`, `StaffService.cs`, thêm `IdempotencyReplay.cs`.
- Kiểm chứng: build Release toàn solution thành công, 0 warning và 0 error; chưa chạy test concurrent theo quyết định của chủ dự án.

### R16 – Refresh lifetime/Redis TTL và refresh đồng thời chưa nhất quán

- Mức độ: **TRUNG BÌNH**
- Trạng thái: **ĐÃ SỬA TRONG WORKING TREE – CHỜ DUYỆT**
- Phát hiện: refresh token mới có expiry `now + 7d`, nhưng Redis TTL chỉ được set khi login. Hai refresh đồng thời dưới `Serializable` có thể deadlock hoặc nhầm request hợp lệ là token reuse.
- Cách sửa: chốt absolute logical-session lifetime theo `UserSession.ExpiresAtUtc`; replacement token, cookie và access token đều bị chặn tại mốc này. Redis có Lua conditional renew chỉ khi đúng active `sid`. Backend dùng `ReadCommitted` cùng `ExecuteUpdateAsync` có điều kiện để một request claim rotation; request thua trong grace 30 giây trả `409`, reuse sau grace mới revoke session; SQL deadlock 1205 được map thành conflict có chủ đích.
- Kiểm chứng: build Release toàn solution thành công, 0 warning và 0 error; chưa chạy multi-tab/Redis TTL/deadlock test.

### R17 – Sao kê có thể đọc nhiều snapshot không nhất quán

- Mức độ: **TRUNG BÌNH**
- Trạng thái: **CHƯA SỬA**
- Phát hiện: account/current balance, movements từ đầu kỳ và period rows được đọc bằng nhiều query `READ COMMITTED`; giao dịch commit xen giữa có thể làm opening/closing/rows lệch nhau.
- Bằng chứng: `src/SUBank.Infrastructure/Statements/StatementService.cs:15-34`.
- Đề xuất chưa thực hiện: dùng cùng `asOfUtc` và snapshot/repeatable-read transaction phù hợp cho toàn bộ query.

### R18 – Router Client chưa kiểm tra role

- Mức độ: **TRUNG BÌNH**
- Trạng thái: **CHƯA SỬA; BACKEND VẪN CHẶN QUYỀN**
- Phát hiện: `App.razor` chỉ kiểm tra có session; user sai role vẫn render page rồi API mới trả 403. Backend controller authorization vẫn là lớp bảo mật.
- Bằng chứng: `client/SUBank.Client/App.razor:16-24`.
- Đề xuất chưa thực hiện: map page→role tại router/layout, chuyển `/403` và xử lý 403 nhất quán; không coi việc ẩn menu là security control.

### R19 – Client chưa phân biệt không có phiên với lỗi hạ tầng

- Mức độ: **TRUNG BÌNH**
- Trạng thái: **CHƯA SỬA**
- Phát hiện: restore coi mọi non-success, kể cả 503, như chưa login. Transfer/Statements có initial API call không catch nên có thể rơi vào Blazor error UI. Phần QR không được đụng theo quyết định hiện tại.
- Bằng chứng: `ApiSession.cs:24-34`, `Transfer.razor:17`, `Statements.razor:30`.
- Đề xuất chưa thực hiện: phân biệt 401/no-session với 5xx/dependency outage; có bootstrap error + Retry; bọc initial load bằng loading/error state.

### R20 – `/health` luôn báo Healthy

- Mức độ: **TRUNG BÌNH**
- Trạng thái: **CHƯA SỬA**
- Phát hiện: endpoint trả constant 200, không kiểm tra SQL/Redis; hệ thống có thể route traffic dù login/nghiệp vụ không dùng được.
- Bằng chứng: `src/SUBank.Api/Program.cs:129-132`.
- Đề xuất chưa thực hiện: tách liveness/readiness; readiness kiểm tra SQL và Redis với timeout, không lộ connection/secret.

### R21 – Integration test chưa cô lập database và không dùng Redis thật

- Mức độ: **TRUNG BÌNH**
- Trạng thái: **CHƯA SỬA**
- Phát hiện: fixture dùng DB cố định `SUBankV2_Integration`, cleanup không xóa hết token/session/audit; Redis adapter bị thay bằng fake in-memory. Test chưa chứng minh Lua/TTL/key prefix, fresh migration hay seed idempotency.
- Bằng chứng: `tests/SUBank.IntegrationTests/SUBankWebApplicationFactory.cs:16-84`, cleanup trong `ApiSmokeTests.cs`.
- Đề xuất chưa thực hiện: DB riêng theo test run hoặc reset deterministic; suite hạ tầng dùng SQL Server + Redis disposable/Testcontainers; thêm migration/seed upgrade test.

## 6. Phát hiện QR đang tạm hoãn

Theo quyết định của chủ dự án, các mục sau chỉ được ghi lại và không được sửa trong giai đoạn này.

| Mã | Mức độ | Trạng thái | Phát hiện | Hướng xử lý khi mở lại scope |
|---|---|---|---|---|
| QR-01 | CAO | TẠM HOÃN THEO QUYẾT ĐỊNH | `Image.Load` decode/cấp phát toàn ảnh trước khi kiểm tra 4096×4096; file nén nhỏ có thể là decompression bomb. | Identify header/kích thước/pixel limit trước full decode; giới hạn memory. |
| QR-02 | TRUNG BÌNH | TẠM HOÃN THEO QUYẾT ĐỊNH | Ảnh corrupt/truncated đúng signature có thể ném exception chưa map và thành 500. | Bắt các invalid-image exception cụ thể và trả 422 an toàn. |
| QR-03 | TRUNG BÌNH | TẠM HOÃN THEO QUYẾT ĐỊNH | Request MIME được tin cậy nhiều hơn actual image format. | Content sniff actual format và đối chiếu allow-list PNG/JPEG/WebP. |
| QR-04 | TRUNG BÌNH | TẠM HOÃN THEO QUYẾT ĐỊNH | Camera có thể start nhiều lần, giữ stream cũ hoặc capture khi `videoWidth/videoHeight` chưa sẵn sàng. | Stop stream cũ, có `cameraReady`, disable capture trước metadata/play và reset state khi dispose. |

## 7. Phát hiện bổ sung chưa cấp mã R

| Mã | Mức độ | Trạng thái | Phát hiện | Đề xuất chưa thực hiện |
|---|---|---|---|---|
| X01 | TRUNG BÌNH | CHƯA SỬA | `DefaultConnection` chỉ được kiểm tra null, không chặn chuỗi rỗng; JWT chưa validate issuer/audience rỗng hoặc lifetime không dương. Production có thể fail với thông báo thấp hơn tầng thay vì fail-fast rõ ràng. | Options validation khi startup cho connection, JWT, Redis prefix và lifetime. |
| X02 | TRUNG BÌNH | CHƯA SỬA / PHỤ THUỘC HOST | API chưa cấu hình HTTPS redirection, HSTS, forwarded headers; `AllowedHosts` là `*`. Nếu edge host không đảm nhận đúng, ranh giới HTTPS-only trong tài liệu không được enforce. | Chốt trách nhiệm TLS tại edge hay app; cấu hình forwarded headers/HSTS/AllowedHosts theo nền tảng deploy. |
| X03 | THẤP | CHƯA SỬA | `Database-Design.md` mô tả một số enum là `varchar` và uppercase, trong khi migration dùng `nvarchar` với `Transfer/CashDeposit`. | Chọn representation chuẩn và đồng bộ tài liệu/model/constraint. |
| X04 | THẤP | CHƯA SỬA | `index.html` khai báo `lang="en"` và Blazor fatal-error UI còn tiếng Anh trong khi giao diện/tài liệu dùng tiếng Việt. | Đổi `lang="vi"` và Việt hóa thông báo lỗi. |
| X05 | THẤP | CHƯA SỬA | `dotnet format --verify-no-changes` phát hiện khác biệt line ending/format/import; build vẫn thành công. | Chạy format có kiểm soát sau khi chốt line-ending policy; tránh format hàng loạt migration trong cùng commit sửa logic. |
| X06 | TRUNG BÌNH | CHƯA SỬA / ĐỂ THỰC HÀNH | Client logout không kiểm tra HTTP status và luôn xóa session trong memory. Nếu Redis/SQL lỗi trước khi server thu hồi thành công, reload có thể khôi phục phiên từ cookie còn sống. | Phân biệt “đã logout trên thiết bị” với “server chưa xác nhận thu hồi”, hiển thị cảnh báo/retry và bảo đảm cookie được hết hạn theo failure contract đã chốt. |
| X07 | THẤP | CHƯA SỬA | Login replacement vẫn dùng helper revoke không lấy cùng aggregate lock với logout. Quyền truy cập vẫn an toàn nhờ Redis, nhưng hai luồng hiếm khi chạy đồng thời có thể làm `RevocationReason` cuối cùng là `LOGOUT` hoặc `REPLACED` tùy thứ tự ghi. | Cho mọi full-session revocation dùng chung lock/atomic update hoặc chốt quy tắc ưu tiên reason; không cần sửa để đóng R02. |

## 8. Các thay đổi không được phân loại là lỗi

- Đổi logo corgi thành wordmark `SUBank 3S`, đổi màu, tagline và bố cục login là thay đổi thiết kế.
- Yêu cầu URL gốc mở login, bỏ landing page và logo điều hướng theo role là thay đổi UX.
- Customer đăng nhập bằng số điện thoại, bổ sung account demo và đổi thứ tự menu là thay đổi yêu cầu/dữ liệu demo.
- Gỡ bỏ quy trình đổi địa chỉ là quyết định thu hẹp phạm vi. R10 chỉ ghi rủi ro dữ liệu của migration bù.
- `IdentityNumber` → `IdentityCardNumber` là rename ngữ nghĩa và rename cột/index bằng migration; không thêm cột và không phải lỗi dữ liệu.
- AI feature được stash/hoãn và Figma `PENDING` là quyết định phạm vi, không phải bug.

## 9. Tóm tắt trạng thái hiện tại

- `R01`, `R02`, `R06`–`R08`, `R13`, `R15` và `R16`: đã sửa trong working tree, build Release thành công, chưa test, chưa commit/push.
- `R03`–`R05`, `R09`–`R12`, `R14` và `R17`–`R21`: chưa sửa.
- `QR-01`–`QR-04`: tạm hoãn theo quyết định.
- `X01`–`X07`: mới ghi nhận, chưa sửa.
- Không có package NuGet có lỗ hổng đã biết trong lần quét gần nhất.
- Kết quả baseline trước R13: build Release 0 warning/0 error; 31/31 test pass. Kết quả này không phủ các kịch bản R01–R21.

Theo quyết định ngày 2026-08-29, AI chỉ tiếp tục các lỗi nontrivial về concurrency, snapshot, state machine hoặc deployment integration. `R03`–`R05`, `R09`, `R12`, `R14`, `R18` và các việc cơ học tương tự được giữ lại để chủ dự án thực hành dù mức độ có thể cao. Thứ tự lỗi khó dự kiến sau R02 là `R17` → `R19` → `R11` → `R20`; `R21` để giai đoạn test cuối. `R10` và cấu hình production security cần chốt môi trường deploy trước khi thay đổi.

## 10. Nhật ký cập nhật

| Ngày | Thay đổi | Con người review |
|---|---|---|
| 2026-08-29 | Tổng hợp rủi ro thiết kế, H01–H16, R01–R21, QR-01–QR-04 và X01–X05; không sửa thêm code. | CHỜ XÁC NHẬN |
| 2026-08-29 | Sửa R01: Client giữ `Idempotency-Key` theo một ý định chuyển/nộp tiền qua các lần retry; build Release 0 warning/0 error, không chạy test. | CHỜ XÁC NHẬN |
| 2026-08-29 | Sửa R15: request transfer/deposit thua race cùng key query lại winner sau rollback và trả replay đúng; hash được chuẩn hóa invariant; build Release 0 warning/0 error, không chạy test. | CHỜ XÁC NHẬN |
| 2026-08-29 | Sửa R06 + R16: single-flight access-token refresh, retry một lần, absolute session lifetime, Redis conditional TTL renew và atomic concurrent rotation; build Release 0 warning/0 error, không chạy test. | CHỜ XÁC NHẬN |
| 2026-08-29 | Sửa R07 + R08: SignalR dùng token hiện hành, tự retry initial/closed failure, chặn callback cũ; Hub và notification chỉ phục vụ active-session group, connection đóng khi JWT hết hạn; build Release 0 warning/0 error, không chạy test. | CHỜ XÁC NHẬN |
| 2026-08-29 | Sửa R02: refresh/logout dùng chung khóa `UserSession`; logout từ token đã rotate thu hồi toàn family và Redis theo cơ chế idempotent, có deadlock retry; build Release 0 warning/0 error, không chạy test. | CHỜ XÁC NHẬN |

## 11. Phụ lục cập nhật trạng thái (append-only)

Các mục dưới đây là bản ghi mới hơn theo thời gian và có hiệu lực khi xác định trạng thái hiện tại. Nội dung ở các mục phía trên được giữ nguyên để bảo toàn lịch sử, không bị xóa hoặc ghi đè.

### 2026-08-29 – R17: Đồng nhất dữ liệu khi tạo sao kê

- Trạng thái mới: **ĐÃ SỬA TRONG WORKING TREE – CHỜ DUYỆT**.
- Cách sửa: đọc số dư account và toàn bộ movement dùng để tính sao kê trong một SQL transaction `RepeatableRead` ngắn. Account được đọc trước để giữ shared lock; `asOfUtc` được lấy sau lần đọc này và là cận trên chung của các query ledger.
- Cơ sở đúng: mọi luồng chuyển/nộp tiền hiện tại đều cập nhật account và ghi `FinancialTransaction` trong cùng SQL transaction, nên writer của đúng account không thể commit xen giữa cửa sổ đọc.
- Thứ tự giao dịch được cố định theo `CreatedAtUtc`, sau đó `Id`. SQL deadlock 1205 được tự retry một lần; nếu lần thứ hai vẫn deadlock, API trả conflict an toàn để người dùng thử lại.
- Transaction được kết thúc trước khi tính/map DTO và trước khi render PDF, tránh giữ khóa trong phần xử lý CPU.
- Không đổi schema, API contract, package, Client hoặc phần QR.
- Kiểm chứng: build Release toàn solution thành công, 0 warning và 0 error; không chạy hoặc thêm test theo quyết định hiện tại của chủ dự án.
- Rủi ro còn lại: chưa kiểm chứng transfer/deposit đồng thời với sao kê trên SQL Server thật; `RepeatableRead` có thể chặn writer cùng account trong thời gian ngắn. Quy tắc cho kỳ sao kê nằm hoàn toàn trong tương lai không thuộc phạm vi R17 và chưa thay đổi.

### Nhật ký cập nhật nối tiếp

| Ngày | Thay đổi | Con người review |
|---|---|---|
| 2026-08-29 | Sửa R17: đồng nhất account balance, movements và period rows trong một read window có `asOfUtc`; giải phóng transaction trước khi render PDF. | CHỜ XÁC NHẬN |

## 12. Phụ lục Application Log (append-only)

### 2026-08-30 – Hoàn thiện structured Application Log

- Trạng thái: **ĐÃ TRIỂN KHAI TRONG WORKING TREE – CHỜ DUYỆT**.
- API dùng Serilog nhận event từ `ILogger`, ghi JSON Lines ra console; Development bật rolling file theo ngày và dung lượng trong `src/SUBank.Api/logs/`.
- Request completion log chỉ chủ động ghi method, route template, status và elapsed time. Correlation ID được allow-list, đưa vào logging scope, response header và ProblemDetails.
- EF interceptor tự điền `AuditLog.CorrelationId` cho audit mới phát sinh trong HTTP request; không đổi schema vì cột đã tồn tại. Startup/background audit không có HTTP context vẫn có thể để `null`.
- Lớp loại thuộc tính nhạy cảm loại `RequestPath`, query/body/token/password/connection string và một số identifier top-level khỏi event trước sink. Đây không phải sanitizer tổng quát; developer vẫn không được log DTO, nested secret hoặc PII.
- Cấu hình Development hiện tại: 10 MiB mỗi file, roll theo ngày/dung lượng, tối đa 31 file và tuổi tối đa 14 ngày. Ngưỡng chạm trước có thể xóa file sớm hơn.
- Kiểm chứng: restore thành công; build toàn solution 0 warning, 0 error; API chạy trên HTTP/HTTPS; rolling file được tạo; route value/query/body giả không xuất hiện trong request completion event; correlation header và ProblemDetails khớp. Không chạy hoặc thêm test theo quyết định hiện tại.
- Redis local đang không khả dụng nên successful-login/Admin Audit API chưa được dùng để đọc lại correlation từ SQL. Failure `503` đã được ghi technical log đúng correlation.
- Không thay đổi QR.

Hai lỗi triển khai được phát hiện và sửa ngay trong cùng phạm vi:

1. Dùng nhầm `StatusCodes.StatusServiceUnavailable` làm build lỗi; đã sửa thành `Status503ServiceUnavailable`.
2. Scope mặc định của ASP.NET Core tự thêm raw `RequestPath`, làm lộ route value dù message đã dùng route template; đã thêm enricher loại thuộc tính và snapshot route template trước pipeline để exception-handled request không biến thành `<unmapped>`.

### X08 – Chưa có backup và restore được kiểm chứng

- Mức độ: **CAO đối với production; TRUNG BÌNH đối với demo học tập**.
- Trạng thái: **CHƯA SỬA**.
- Phát hiện: rolling application log chỉ giới hạn file local. Repo chưa có lịch backup SQL, bản sao ở vị trí độc lập, retention cho backup, restore script, cảnh báo backup thất bại hoặc bằng chứng restore drill. `AuditLog` và `FinancialTransaction` nằm trong SQL không tự trở thành backup.
- Rủi ro: filesystem container có thể mất khi redeploy; lỗi host/ổ đĩa hoặc thao tác sai database có thể làm mất cả dữ liệu chính và log local.
- Hướng xử lý khi chốt nền tảng deploy: dùng backup/PITR của database provider, xuất bản sao định kỳ sang storage độc lập, mã hóa và phân quyền, quy định retention, giám sát job, viết runbook rồi thực hiện restore drill trước demo/production quan trọng.
- Không được đánh dấu đã sửa chỉ vì provider quảng cáo backup; cần cấu hình thực tế và bằng chứng restore thành công.

### Nhật ký cập nhật nối tiếp

| Ngày | Thay đổi | Con người review |
|---|---|---|
| 2026-08-30 | Hoàn thiện Application Log; ghi X08 về khoảng trống backup/restore. | CHỜ XÁC NHẬN |

### 2026-08-30 – Bổ sung kiểm chứng exception response

- Kiểm tra exception-handled request phát hiện ProblemDetails đã có `correlationId` nhưng `X-Correlation-ID` bị mất khi exception middleware dựng lại response.
- Cách sửa: đăng ký `Response.OnStarting` trong correlation middleware để gắn lại header ngay trước khi gửi response.
- Kiểm chứng: gọi `/api/auth/refresh` không có cookie nhận `401`; response header `X-Correlation-ID`, ProblemDetails `correlationId` và request log cùng mang `exception-route-check-02`. Request log giữ đúng route template `api/auth/refresh`, không còn `<unmapped>`.
- Build Release toàn solution sau sửa thành công, 0 warning và 0 error; không chạy test.

## 13. Phụ lục hoàn tất lượt sửa lỗi non-QR (append-only)

### 2026-08-31 – Trạng thái mới nhất

Phụ lục này chỉ ghi thêm trạng thái mới. Mọi mô tả và trạng thái lịch sử ở phía trên được giữ nguyên.

| Mã | Trạng thái mới | Cách xử lý/giới hạn |
|---|---|---|
| R03 | ĐÃ SỬA TRONG WORKING TREE – CHỜ DUYỆT | Nộp tiền kiểm tra Teller tồn tại, `IsActive` và lockout trước nghiệp vụ. Khi login failure làm Identity lock user, active session Redis và persisted token family bị thu hồi, SignalR nhận force-logout. |
| R04 | ĐÃ SỬA TRONG WORKING TREE – CHỜ DUYỆT | Form chuyển/nộp tiền không còn điền sẵn tài khoản đích, số tiền, nội dung hoặc mật khẩu giao dịch. Mật khẩu giao dịch luôn bị xóa sau mỗi lần gửi; form nghiệp vụ được reset sau thành công. |
| R05 | ĐÃ SỬA TRONG WORKING TREE – CHỜ DUYỆT | Transfer và Teller deposit có `DataAnnotationsValidator`, message theo field, rule tài khoản 10 số, amount dương/2 chữ số lẻ/không vượt `decimal(18,2)`, description tối đa 280 và transaction password 6 số. Server validation vẫn là lớp có thẩm quyền. |
| R09 | ĐÃ SỬA TRONG WORKING TREE – CHỜ DUYỆT | Migration/seed khi startup dùng cờ opt-in. Cấu hình gốc/Production tắt cả hai; Development mới bật. Production runtime không cần quyền DDL. |
| R10 | ĐÃ XỬ LÝ PHẦN REPOSITORY – CẦN EVIDENCE MÔI TRƯỜNG | Thêm `Backup-Restore-Runbook.md`: phân nhánh DB đã/chưa chạy migration, full backup, export bảng, `VERIFYONLY`, restore drill DB riêng và bằng chứng. Dữ liệu đã bị xóa không thể được code tự tái tạo; không sửa migration lịch sử. |
| R11 | ĐÃ SỬA TRONG WORKING TREE – CHỜ DUYỆT | Publish API tự restore/publish Blazor Client và chép vào `wwwroot`; dùng path `/` đa nền tảng, không tạo project reference Api→Client. API phục vụ static file/SPA fallback nhưng giữ `/api`, `/hubs`, `/health`, `/swagger` không rơi nhầm vào `index.html`. |
| R12 | ĐÃ SỬA TRONG WORKING TREE – CHỜ DUYỆT | Demo seed cần cờ riêng, chỉ được chạy trong Development và connection phải khớp chính xác allow-list data source/database trước bất kỳ lần ghi nào. |
| R14 | ĐÃ SỬA TRONG WORKING TREE – CHỜ DUYỆT | Sai/đúng transaction password không còn tăng/reset login lockout counter. Phạm vi demo dùng rate limit riêng 10 request/phút và audit `TRANSACTION_PASSWORD_FAILED`. |
| R18 | ĐÃ SỬA TRONG WORKING TREE – CHỜ DUYỆT | Router dùng allow-list page→role và deny-by-default; sai role đi `/403`. `Session.Changed` bắt buộc re-render gate. Backend authorization vẫn là security boundary. |
| R19 | ĐÃ SỬA TRONG WORKING TREE – CHỜ DUYỆT | Bootstrap chỉ coi 401 refresh là không có phiên; lỗi network/5xx hiện state riêng có Retry. Transfer và Statements có loading/error/retry/empty state cho initial load. |
| R20 | ĐÃ SỬA TRONG WORKING TREE – CHỜ DUYỆT | `/health/live` là process liveness; `/health/ready` và `/health` kiểm tra SQL Server + Redis với timeout 3 giây, trả 503 khi dependency lỗi và response chỉ chứa `status`. |
| R21 | TẠM HOÃN ĐẾN GIAI ĐOẠN TEST | Fixture đã được bổ sung cấu hình migration/seed allow-list để không hỏng startup, nhưng việc tách DB theo run và dùng SQL Server/Redis disposable vẫn chưa làm theo quyết định hoãn test của chủ dự án. |
| X01 | ĐÃ SỬA TRONG WORKING TREE – CHỜ DUYỆT | Startup fail-fast với connection/JWT/Redis rỗng, signing key dưới 32 byte, token lifetime/grace không hợp lệ và seed allow-list thiếu. |
| X02 | ĐÃ XỬ LÝ PHẦN REPOSITORY – PHỤ THUỘC HOST | Production bật HSTS/HTTPS redirect, cấm wildcard `AllowedHosts`; forwarded headers chỉ bật với danh sách IP proxy tin cậy và chạy trước HSTS/redirect. Cần chốt provider và test TLS/proxy thật mới đóng hoàn toàn. |
| X03 | ĐÃ SỬA TRONG WORKING TREE – CHỜ DUYỆT | Tài liệu database/blueprint đã khớp representation `nvarchar(20)` và `Transfer`/`CashDeposit` trong model/migration. |
| X04 | ĐÃ SỬA TRONG WORKING TREE – CHỜ DUYỆT | `index.html` dùng `lang="vi"`; fatal/reload/dismiss UI của Blazor được Việt hóa. |
| X05 | ĐÃ SỬA TRONG WORKING TREE – CHỜ DUYỆT | Thêm `.gitattributes`, đánh dấu binary và chốt line ending; migration C# giữ UTF-8 BOM. `dotnet format --verify-no-changes` thành công. |
| X06 | ĐÃ SỬA TRONG WORKING TREE – CHỜ DUYỆT | Logout endpoint luôn expire cookie khi đã vào controller và trả marker header. Client chỉ xóa session local khi nhận marker; response từ proxy/middleware trước controller hoặc network failure giữ phiên và cho retry. |
| X07 | ĐÃ SỬA TRONG WORKING TREE – CHỜ DUYỆT | Mọi full-session revocation dùng cùng `UserSession` lock/transaction và quy tắc ưu tiên reason xác định: reuse > locked > activation failure > replaced > logout. |
| X08 | ĐÃ XỬ LÝ PHẦN REPOSITORY – PHỤ THUỘC HOST | Runbook đã chốt RPO/RTO/retention, mã hóa, quyền, alert và mẫu evidence. Chỉ đóng sau khi provider có PITR/backup thật và restore drill thành công. |

Các mục R01, R02, R06–R08, R13 và R15–R17 giữ nguyên bằng chứng sửa ở các phụ lục trước. Tính đến trạng thái working tree ngày 2026-08-31, toàn bộc lỗi non-QR có thể xử lý hoàn toàn trong repository đã được sửa. Ba nhóm chưa thể đóng hoàn toàn là R21 do hoãn giai đoạn test, X02/X08 do chưa chốt host và chưa có evidence môi trường. QR-01–QR-04 vẫn tạm hoãn và không bị sửa trong lượt này.

### Kiểm chứng lượt cuối

- `dotnet build SUBank.sln -c Release --no-restore`: thành công, 0 warning, 0 error.
- `dotnet format SUBank.sln --verify-no-changes --no-restore`: thành công.
- `dotnet publish src/SUBank.Api/SUBank.Api.csproj -c Release -o .artifacts/publish-final --no-restore`: thành công; nested Client target tự restore và publish.
- Smoke artifact cùng origin: `/` 200 HTML, `/accounts` 200 HTML, framework JavaScript 200, route API không tồn tại 404 ProblemDetails, `/health/live` 200 và `/health/ready` 503 khi dependency không sẵn sàng.
- Logout endpoint đã trả `204`, cookie hết hạn đúng path `/api/auth` và `X-SUBank-Refresh-Cookie-Cleared: 1`.
- Không chạy hoặc thêm test theo quyết định hoãn test của chủ dự án.
- Chưa commit hoặc push các thay đổi trong lượt này.

### Nhật ký cập nhật nối tiếp

| Ngày | Thay đổi | Con người review |
|---|---|---|
| 2026-08-31 | Hoàn tất các sửa lỗi non-QR có thể xử lý trong repository; giữ R21, X02, X08 và QR ở trạng thái minh bạch theo dependency/quyết định phạm vi. | CHỜ XÁC NHẬN |

Ghi chú đính chính append-only: cụm "toàn bộc lỗi" trong đoạn tóm tắt ngay phía trên là lỗi chính tả; nội dung đúng là "toàn bộ lỗi". Không thay đổi trạng thái kỹ thuật.

## 14. Phụ lục hardening authentication/session phía Client (append-only)

### 2026-09-01 – Trạng thái X09–X38

Phụ lục này chỉ nối thêm phát hiện và kết quả mới; không thay thế hay xóa bất kỳ nội dung lịch sử nào phía trên.

| Mã | Trạng thái mới | Phát hiện và cách xử lý |
|---|---|---|
| X09 | ĐÃ SỬA TRONG WORKING TREE – CHỜ DUYỆT | Customer trước đó có thể được refresh như staff và không hết UI đúng 15 phút từ login. Đã chốt absolute Customer session/access-token 15 phút, không proactive refresh/retry; timer monotonic kèm wall-clock và foreground check buộc xóa UI đúng mốc. |
| X10 | ĐÃ SỬA TRONG WORKING TREE – CHỜ DUYỆT | Trang sao kê có thể hiển thị kết quả cũ nhưng tải PDF theo bộ lọc mới. Đã snapshot account/năm/tháng của kết quả, xóa statement khi filter đổi và chỉ tải PDF theo snapshot đang hiển thị. |
| X11 | ĐÃ SỬA TRONG WORKING TREE – CHỜ DUYỆT | Response async cũ của page/request có thể ghi đè state mới sau reload, dispose hoặc session change. Đã thêm version/generation guard, snapshot bearer/session và bỏ kết quả stale trước khi mutate UI. |
| X12 | ĐÃ SỬA TRONG WORKING TREE – CHỜ DUYỆT | Development HTTP không tương thích refresh cookie `Secure` và exact-origin CORS. Đã chỉ giữ launch profile HTTPS, tách port API/Client trong Development; Production/demo giữ same-origin. |
| X13 | ĐÃ SỬA TRONG WORKING TREE – CHỜ DUYỆT | Nhiều tab dùng chung HttpOnly refresh cookie nhưng có identity/session cục bộ khác nhau, dẫn tới restore nhầm phiên. Đã bind `SessionId` riêng theo tab và từ chối bootstrap/non-bootstrap mismatch. |
| X14 | ĐÃ SỬA TRONG WORKING TREE – CHỜ DUYỆT | Realtime reload account có thể đổi lựa chọn về account đầu tiên/primary hoặc để response cũ ghi đè. Đã giữ account đang chọn khi còn tồn tại, fallback rõ ràng và dùng load-version guard. |
| X15 | ĐÃ SỬA TRONG WORKING TREE – CHỜ DUYỆT | Production có thể tự migration/seed khi startup. Đã đổi thành opt-in, hard-fail ngoài Development và allow-list chính xác database demo trước khi ghi. |
| X16 | ĐÃ SỬA TRONG WORKING TREE – CHỜ DUYỆT | Login có thể ghi SQL session/token rồi lỗi khi activate Redis, để lại ghost session. Đã compensation Redis/SQL bằng cancellation token độc lập và audit activation failure. |
| X17 | ĐÃ SỬA TRONG WORKING TREE – CHỜ DUYỆT | Middleware chỉ tin Redis nên session/user bị revoke/lock bền vững trong SQL có thể còn qua trong một số failure window. Đã bổ sung validator đối chiếu Redis, `UserSession`, user active và lockout trước controller/Hub. |
| X18 | ĐÃ SỬA TRONG WORKING TREE – CHỜ DUYỆT | Teller cash deposit thiếu policy rate-limit riêng và use case chưa tự kiểm tra Teller active/lockout. Đã thêm `CashDeposit` limiter và kiểm tra identity ngay trong service, không chỉ dựa vào UI/controller. |
| X19 | ĐÃ SỬA TRONG WORKING TREE – CHỜ DUYỆT | Logout từng revoke Redis trước khi SQL commit, có thể mất durable revocation khi SQL lỗi. Đã commit `UserSession`/token family trong SQL trước, sau đó compare-delete Redis và SignalR best-effort. |
| X20 | ĐÃ SỬA TRONG WORKING TREE – CHỜ DUYỆT | Bắt đầu login mới từng xóa `Current` trước khi biết login thành công, nên nhập sai credential làm mất phiên đang dùng. Đã giữ phiên hiện tại đến khi response mới được validate/bind thành công. |
| X21 | ĐÃ SỬA TRONG WORKING TREE – CHỜ DUYỆT | Một số `IdentityResult` khi lock/unlock/reset counter bị bỏ qua và unlock không cùng transaction. Đã buộc kiểm tra kết quả và gom update unlock trong SQL transaction. |
| X22 | ĐÃ SỬA TRONG WORKING TREE – CHỜ DUYỆT | Request A có thể nhận 401 sau khi session B thay thế rồi bị retry bằng token B, khiến ý định cũ chạy dưới identity/session mới. Đã chụp generation và bearer; chỉ refresh/retry khi vẫn cùng logical session, response stale bị bỏ. |
| X23 | ĐÃ SỬA TRONG WORKING TREE – CHỜ DUYỆT | Logout lỗi từng để UI/session local tiếp tục hoạt động hoặc xóa state mà không có bằng chứng server. Đã block UI/restore ngay, yêu cầu marker thu hồi, fallback bearer-bound và hiển thị `logout-unconfirmed` khi cả hai đường không xác nhận. |
| X24 | ĐÃ SỬA TRONG WORKING TREE – CHỜ DUYỆT | Timer Customer có thể bị browser throttle/suspend khi tab ẩn. Đã kiểm tra expiry lại trên `visibilitychange`, `focus`, `pageshow`; server SQL vẫn là lớp chặn deadline có thẩm quyền. |
| X25 | ĐÃ SỬA TRONG WORKING TREE – CHỜ DUYỆT | Tab đã bind có thể bootstrap từ shared cookie của session khác. Đã so expected tab SID với response SID và fail closed thay vì đổi identity âm thầm. |
| X26 | ĐÃ SỬA TRONG WORKING TREE – CHỜ DUYỆT | Compensation bootstrap mismatch nếu dùng shared cookie có thể thu hồi nhầm session mới của tab khác. Bản vá tạm này đã bị loại bỏ; tab cũ chỉ block restore cục bộ/session-specific và không dùng cookie để revoke session không thuộc nó. |
| X27 | ĐÃ SỬA TRONG WORKING TREE – CHỜ DUYỆT | Login/refresh/logout đồng thời có thể nhận `Set-Cookie` đảo thứ tự. Đã thay lease TTL bằng Web Lock thật, giữ xuyên `fetch`, đọc và validate response. |
| X28 | ĐÃ SỬA TRONG WORKING TREE – CHỜ DUYỆT | `204` logout khi không có cookie/bearer chỉ chứng minh cookie đã clear, không chứng minh session đã revoke. Client nay chỉ xác nhận khi có `X-SUBank-Session-Revoked: 1`; nếu thiếu thì dùng bearer fallback. |
| X29 | ĐÃ SỬA TRONG WORKING TREE – CHỜ DUYỆT | Login đã tạo session nhưng Client từ chối do stale generation/storage/mismatch có thể để lại session sống. Đã block restore và gọi `reject-session` bằng chính bearer vừa nhận với `credentials: omit`. |
| X30 | ĐÃ SỬA TRONG WORKING TREE – CHỜ DUYỆT | Trạng thái logout giữa tab có thể bị bỏ lỡ do event đến trong lúc callback đang chạy. Đã dùng `BroadcastChannel`, `storage`, focus/pageshow, cờ rerun và generation guard; storage hỏng phải fail closed. |
| X31 | ĐÃ SỬA TRONG WORKING TREE – CHỜ DUYỆT | Một logout marker duy nhất có thể bị session mới ghi đè và mở lại session cũ. Đã lưu danh sách logout intent theo SID, TTL 32 ngày, không giới hạn số entry bằng cách cắt mất intent còn hiệu lực. |
| X32 | ĐÃ SỬA TRONG WORKING TREE – CHỜ DUYỆT | Các biểu diễn Guid khác nhau có thể làm so sánh SID sai. Protocol nay chỉ chấp nhận Guid format `N` lowercase 32 ký tự tại biên browser/API. |
| X33 | ĐÃ SỬA TRONG WORKING TREE – CHỜ DUYỆT | Lỗi Redis/SignalR sau SQL commit từng có thể làm API báo thất bại dù durable revocation đã thành công. Đã chốt SQL durable-first; Redis cleanup và `ForceLogout` là post-commit best-effort có log. |
| X34 | ĐÃ SỬA TRONG WORKING TREE – CHỜ DUYỆT | Auth/CSRF error response có thể bị cache. Login, refresh, logout, reject-session, `/me` và invalid-CSRF response nay đều gắn `Cache-Control: no-store` và `Pragma: no-cache`. |
| X35 | ĐÃ SỬA TRONG WORKING TREE – CHỜ DUYỆT | Browser có thể đã nhận `Set-Cookie` nhưng Client không đọc/deserialize/validate được body login, tạo ghost session. JavaScript nay validate response tối thiểu ngay trong Web Lock; dùng SID header/body để block và cookie compensation trước khi nhả khóa. Header/body canonical khác nhau thì chặn toàn bộ restore và thử thu hồi cả hai ID. |
| X36 | ĐÃ SỬA TRONG WORKING TREE – CHỜ DUYỆT | `history.state` không bền qua mọi route entry và fallback có thể làm mất tab binding; malformed/unavailable storage từng có nguy cơ fail open. Đã chỉ dùng `sessionStorage` cho tab SID, `localStorage` cho shared intent; không có fallback và mọi lỗi storage đều fail closed. |
| X37 | ĐÃ SỬA TRONG WORKING TREE – CHỜ DUYỆT | Callback `ForceLogout` cũ có thể await qua thời điểm login mới rồi xóa nhầm phiên mới. `EndFromServerAsync` và expiry nay khóa chung `refreshGate`, kiểm tra expected generation trước/sau await; chỉ navigate khi thực sự kết thúc đúng phiên. |
| X38 | CHƯA ĐÓNG – RESIDUAL/CHỜ R21 VÀ MÔI TRƯỜNG | Phạm vi browser phụ thuộc Web Locks, `sessionStorage`, `localStorage`; logout-intent traffic cực lớn có thể đầy quota và fail closed. Abort/timeout phía browser không chứng minh server đã dừng, nên có thể có outcome ambiguity/availability error dù backend atomicity ngăn split-brain. Cần browser thật, multi-tab, network-failure, SQL/Redis concurrency test trong R21 trước khi đóng. |

### Kiểm chứng sau hardening

- `dotnet format SUBank.sln --verify-no-changes --no-restore`: thành công.
- `dotnet build SUBank.sln -c Release --no-restore`: thành công, 0 warning, 0 error.
- Parse inline JavaScript bằng Node: thành công, 1 script hợp lệ.
- Publish artifact Release mới: thành công; API tự publish Blazor Client cùng origin.
- Smoke artifact: `/` và `/accounts` 200 HTML; framework JavaScript 200; Swagger JSON 200; `/health/live` 200; `/health/ready` 503 đúng vì SQL Server/Redis không sẵn sàng trong sandbox.
- Logout không có cookie/bearer: 204, `no-store`, cookie-clear marker có mặt và không trả sai session-revoked marker.
- Không chạy hoặc thêm test theo quyết định hoãn test của chủ dự án. R21, X02, X08 và QR-01–QR-04 tiếp tục giữ nguyên trạng thái/phạm vi.
- Toàn bộ thay đổi vẫn ở branch `feature/demo-experience-stability`, chưa commit hoặc push.

### Nhật ký cập nhật nối tiếp

| Ngày | Thay đổi | Con người review |
|---|---|---|
| 2026-09-01 | Hardening auth/session Client, ghi X09–X38, kiểm chứng Release artifact; không thay đổi QR và không chạy test suite. | CHỜ XÁC NHẬN |

## 15. Phụ lục sửa lỗi non-QR theo phạm vi rút gọn (append-only)

### 2026-09-02 – Trạng thái X39–X47

Theo quyết định của chủ dự án, lượt này chỉ xử lý lỗi rõ ràng ảnh hưởng an toàn dữ liệu hoặc khả năng demo; không tiếp tục mở rộng hardening và không kiểm tra/sửa QR.

| Mã | Trạng thái mới | Phát hiện và cách xử lý |
|---|---|---|
| X39 | ĐÃ SỬA TRONG WORKING TREE – CHỜ DUYỆT | Client ánh xạ rõ lỗi session mismatch/revoked/unconfirmed thay vì hiện thông báo kỹ thuật khó hiểu. |
| X40 | ĐÃ SỬA TRONG WORKING TREE – CHỜ DUYỆT | Log phía Client không còn ghi giá trị `SessionId` thô. Correlation ID vẫn được giữ để tra cứu sự cố. |
| X41 | ĐÃ SỬA TRONG WORKING TREE – CHỜ DUYỆT | Phát hiện refresh-token reuse được commit trạng thái thu hồi và audit bền vững trong SQL trước; Redis/SignalR chỉ là hậu xử lý best-effort. |
| X42 | ĐÃ SỬA TRONG WORKING TREE – CHỜ DUYỆT | Bổ sung validation server cho login, số tài khoản, mật khẩu giao dịch, mô tả và idempotency key; không chỉ tin validation của giao diện. |
| X43 | ĐÃ SỬA TRONG WORKING TREE – CHỜ DUYỆT | Audit thất bại chỉ bắt các lỗi nghiệp vụ đã biết. Lỗi kỹ thuật hoặc lỗi sau commit không còn bị kết luận sai thành giao dịch thất bại. |
| X44 | ĐÃ SỬA TRONG WORKING TREE – CHỜ DUYỆT | Request bị trình duyệt/người dùng hủy được trả và ghi log là HTTP 499, không còn bị báo nhầm 500. |
| X45 | ĐÃ SỬA TRONG WORKING TREE – CHỜ DUYỆT | Chỉ lỗi concurrency hoặc unique constraint mới trả 409; lỗi lưu SQL khác trả 503 thay vì bị che thành xung đột dữ liệu. Danh sách giao dịch/audit có thứ tự phụ theo `Id` để ổn định. |
| X46 | ĐÃ SỬA TRONG WORKING TREE – CHỜ DUYỆT | Admin Audit Log hiển thị Correlation ID để nối audit nghiệp vụ với Application Log. |
| X47 | ĐÃ SỬA TRONG WORKING TREE – CHỜ DUYỆT | Logout/hết hạn xóa dữ liệu riêng tư khỏi UI trước khi chờ Web Lock; chặn submit login/logout lặp; bỏ layout 404 lồng nhau; route lịch sử sai không còn treo “Đang tải”; tiền hiển thị 2 số lẻ, giao dịch có dấu vào/ra và thời gian/sao kê dùng UTC+7 nhất quán. |

### Kiểm chứng lượt rút gọn

- `dotnet format SUBank.sln --no-restore`: thành công.
- `dotnet build SUBank.sln -c Release --no-restore`: thành công, 0 warning và 0 error.
- Không chạy hoặc thêm test theo quyết định hiện tại.
- QR không được kiểm tra hoặc sửa; các mục QR-01–QR-04 giữ nguyên.
- Các thay đổi chưa commit hoặc push.

### Nhật ký cập nhật nối tiếp

| Ngày | Thay đổi | Con người review |
|---|---|---|
| 2026-09-02 | Sửa gọn X39–X47 ngoài QR, đưa solution về trạng thái build xanh; dừng mở rộng hardening. | CHỜ XÁC NHẬN |

## 16. Phụ lục lỗi nhập số tiền và lịch sử giao dịch (append-only)

### 2026-09-02 – Trạng thái X48

| Mã | Trạng thái mới | Phát hiện và cách xử lý |
|---|---|---|
| X48 | ĐÃ SỬA TRONG WORKING TREE – CHỜ DEMO XÁC NHẬN | Khi Customer nhập số tiền, Blazor hiện fatal error trước khi API nhận `POST /api/transfers`. Đã bỏ `RangeAttribute(typeof(decimal), ...)` phụ thuộc chuyển đổi kiểu runtime trên WASM, kiểm tra giới hạn bằng phép so sánh `decimal` trực tiếp và đưa chuẩn hóa dữ liệu form vào `try/catch`. Áp dụng cùng cách cho form Teller deposit. |

- Hoàn thiện trang lịch sử giao dịch bằng API sẵn có: chọn tài khoản, tìm kiếm, lọc tiền vào/ra, danh sách ngắn gọn, mở chi tiết và tự cập nhật sau giao dịch/SignalR; không hiển thị tổng thu chi.
- Nút Lịch sử giao dịch được đặt ngay sau QR ở menu và thao tác trang chủ; không thay đổi chức năng QR.
- Build Release toàn solution thành công, 0 warning và 0 error; không chạy test.

### Nhật ký cập nhật nối tiếp

| Ngày | Thay đổi | Con người review |
|---|---|---|
| 2026-09-02 | Sửa X48 và hoàn thiện trải nghiệm lịch sử giao dịch ngoài QR. | CHỜ XÁC NHẬN |

## 17. Phụ lục dọn user seed legacy (append-only)

### 2026-09-02 – Trạng thái X49

| Mã | Trạng thái mới | Phát hiện và cách xử lý |
|---|---|---|
| X49 | ĐÃ SỬA TRONG WORKING TREE – CHỜ DEMO XÁC NHẬN | Trang Quản lý người dùng làm lộ hai user seed cũ `customer.a` và `customer.b` ở trạng thái ngừng hoạt động. Kiểm tra trực tiếp database xác nhận mỗi user có 0 hồ sơ, 0 tài khoản, 0 giao dịch tạo, 0 audit, 0 refresh token và 0 session; chỉ còn role Identity. Đã xóa hai bản ghi khỏi database và sửa seed để chỉ xóa legacy user không có lịch sử nghiệp vụ/audit; nếu có lịch sử thì chỉ vô hiệu hóa để bảo toàn dữ liệu. |

### Nhật ký cập nhật nối tiếp

| Ngày | Thay đổi | Con người review |
|---|---|---|
| 2026-09-02 | Dọn hai user seed legacy không còn liên kết; hai Customer thật và toàn bộ lịch sử giao dịch được giữ nguyên. | CHỜ XÁC NHẬN |

## 18. Phụ lục tách Identity lockout và Admin suspension (append-only)

### 2026-09-02 – Trạng thái X50

| Mã | Trạng thái mới | Phát hiện và cách xử lý |
|---|---|---|
| X50 | ĐÃ SỬA TRONG WORKING TREE – CHỜ DEMO XÁC NHẬN | Trang Quản lý người dùng cũ lấy cả Customer/Teller/Admin và dùng chung thao tác Identity unlock nên Admin có thể tác động nhầm staff, đồng thời chưa có khóa thủ công có lý do. Đã đổi API và UI thành Customer-only, tìm theo họ tên/số điện thoại, xem chi tiết và tách `Identity Lockout` 15 phút khỏi `Admin suspension`. Suspension dùng các cột riêng, bắt buộc lý do, revoke session/refresh token, ghi Audit Log và gửi ForceLogout đúng nguyên nhân; resume không xóa Identity lockout và clear Identity lockout không gỡ suspension. Không cung cấp endpoint xóa Customer. |

### Nhật ký cập nhật nối tiếp

| Ngày | Thay đổi | Con người review |
|---|---|---|
| 2026-09-02 | Hoàn thiện quản lý Customer và tách hai cơ chế khóa; migration mới đã tạo, solution build thành công; chưa chạy test theo quyết định hiện tại. | CHỜ XÁC NHẬN |
