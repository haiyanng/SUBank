# Báo cáo sử dụng AI

Đây là tài liệu bằng chứng bắt buộc của dự án. File chỉ ghi nhận công việc thực tế có AI hỗ trợ. Không được tuyên bố đã có human review, thực thi UAT, Figma approval, deployment thành công hoặc test result nếu những việc đó chưa thực sự xảy ra.

## Entry 001 - Khởi tạo scaffold Milestone 0

- Ngày: 2026-08-26
- Công cụ/model AI: Codex; project không ghi nhận được model backend chính xác.
- Tính năng/công việc: tạo scaffold Milestone 0 của SUBank V2 mà không triển khai business functionality.
- Tóm tắt prompt/công việc thực tế: tạo solution .NET 10 theo Clean Architecture gồm Domain, Contracts, Application, Infrastructure, API, Blazor WebAssembly Client và ba test project; thêm các trang Bootstrap tối thiểu, Swagger, ProblemDetails, Development CORS, `/health`, file cấu hình repository và tài liệu ban đầu. Không triển khai authentication, database, Redis, transfer, QR, SignalR, AI, Teller hoặc Admin logic.
- File/module bị tác động: solution và project dưới `src/`, `client/`, `tests/`; cấu hình ở repository root; các file ban đầu trong `docs/`.
- Kiểm chứng đã thực hiện: `dotnet restore`; build solution; chạy ba test project rỗng; khởi động API; khởi động Client; kiểm tra HTTP cho Client root, `/health`, Swagger UI và OpenAPI JSON.
- Kết quả: scaffold build với 0 compiler warning và 0 error; hai host cùng các endpoint bắt buộc đều khởi động thành công.
- Vấn đề còn lại: test project chưa có test giả theo đúng chủ ý; toàn bộ business functionality được hoãn sang milestone sau.
- Human review: PENDING
- Kinh nghiệm rút ra: phải kiểm tra Git branch trước khi thay đổi; bảo toàn tracked file có sẵn dù file bị ẩn khỏi kết quả tìm kiếm thông thường; pin package version đã resolve thay vì để floating version; phải kiểm tra endpoint đang chạy thật thay vì chỉ dựa vào kết quả compile.

## Entry 002 - Review master specification V2.4

- Ngày: 2026-08-26
- Công cụ/model AI: Codex; project không ghi nhận được model backend chính xác.
- Tính năng/công việc: review `SUBank_V2.4_Codex_Master_Spec_FINAL.docx` mà không triển khai business code.
- Tóm tắt prompt/công việc thực tế: đọc specification được cung cấp, đóng vai trò Senior Tech Lead, giải thích cho người mới, phát hiện rủi ro về kiến trúc/bảo mật/data flow và đề xuất chỉnh sửa trước khi code.
- File/module bị tác động: không có file bị thay đổi trong quá trình review.
- Kiểm chứng đã thực hiện: trích xuất và review toàn bộ 1.098 paragraph của tài liệu, bao gồm nội dung trong bảng.
- Kết quả: chấp nhận hướng .NET, Blazor và Clean Architecture; phát hiện các điểm cần sửa gồm quản lý phiên bản, phạm vi dự án, cardinality của Identity role, trạng thái lockout bị trùng, thiết kế cookie/CORS/CSRF, race condition khi thay session, transfer idempotency, financial constraints, dữ liệu profile trùng lặp, public identifier, account enumeration, brute force transaction password và thứ tự milestone.
- Vấn đề còn lại: visual reference đính kèm vẫn thuộc quyền sở hữu/xác nhận của con người; tiêu đề DOCX gốc vẫn ghi V2.1 cho đến khi được cập nhật rõ ràng.
- Con người review: CHỜ XÁC NHẬN
- Kinh nghiệm rút ra: review kiến trúc trước migration giúp tránh sửa lại schema/auth tốn kém; optimistic concurrency và idempotency giải quyết hai vấn đề khác nhau; SignalR cải thiện UX nhưng không được trở thành nguồn bảo mật; độ an toàn của AI phụ thuộc backend authorization và fixed read-only allow-list.

## Entry 003 - Tổng hợp quyết định V2.4

- Ngày: 2026-08-26
- Công cụ/model AI: Codex; project không ghi nhận được model backend chính xác.
- Tính năng/công việc: ghi lại các chỉnh sửa V2.4 đã thống nhất và thiết lập kế hoạch triển khai bảy ngày.
- Tóm tắt prompt/công việc thực tế: chọn V2.4 làm phiên bản chính thức; lập kế hoạch cho một sinh viên cùng Codex với deadline một tuần; dùng Identity lockout khi phù hợp; giữ `decimal(18,2)`; Development tách port và production cùng origin; giữ QR generation/camera/upload và PDF; chờ logo do chủ dự án cung cấp; dùng hosting demo miễn phí/chi phí thấp; sửa các vấn đề thiết kế đã review.
- File/module bị tác động: `docs/PROJECT-BLUEPRINT.md`, `docs/AI-Usage-Report.md`.
- Kiểm chứng đã thực hiện: kiểm tra tính nhất quán của tài liệu với quyết định do chủ dự án đưa ra và master specification đã review.
- Kết quả: blueprint đã sửa quy định rõ Identity lockout, Identity role many-to-many, yêu cầu HTTPS khi Development cross-origin, CSRF, thay Redis session nguyên tử, bắt buộc transfer idempotency, financial constraints, public identifier, phạm vi PDF/QR, ranh giới API billing của AI, test theo vertical slice và trình tự ưu tiên bảy ngày.
- Vấn đề còn lại: chờ con người duyệt corrected blueprint; chưa triển khai business code.
- Con người review: CHỜ XÁC NHẬN
- Kinh nghiệm rút ra: dự án một tuần cần thứ tự cắt phạm vi rõ ràng; tính đúng đắn của money/auth P0 phải quan trọng hơn số lượng tính năng; free hosting vẫn có cold start, quota, persistence và billing caveat; ChatGPT Plus và OpenAI API có billing riêng.

## Entry 004 - Chuẩn hóa tài liệu sang tiếng Việt

- Ngày: 2026-08-26
- Công cụ/model AI: Codex; project không ghi nhận được model backend chính xác.
- Tính năng/công việc: chuyển toàn bộ tài liệu hiện có của dự án sang tiếng Việt.
- Tóm tắt prompt/công việc thực tế: chủ dự án yêu cầu tất cả tài liệu phải viết bằng tiếng Việt. Giữ nguyên tên file, code identifier, API route và thuật ngữ kỹ thuật cần thiết để dễ đối chiếu khi triển khai.
- File/module bị tác động: `README.md`, `docs/Architecture.md`, `docs/Security-Design.md`, `docs/AI-Design.md`, `docs/Figma-Status.md`, `docs/PROJECT-BLUEPRINT.md`, `docs/AI-Usage-Report.md`, `docs/diagrams/README.md`.
- Kiểm chứng đã thực hiện: rà soát heading, trạng thái human review, prompt template, các quyết định V2.4 và tìm kiếm nội dung tiếng Anh còn lại cần dịch.
- Kết quả: toàn bộ văn bản diễn giải trong các tài liệu hiện có đã được chuyển sang tiếng Việt; tên công nghệ và identifier kỹ thuật được giữ nguyên có chủ ý.
- Vấn đề còn lại: cần human review để xác nhận cách dùng thuật ngữ tiếng Việt/tiếng Anh phù hợp với báo cáo của trường.
- Con người review: CHỜ XÁC NHẬN
- Kinh nghiệm rút ra: không nên dịch tên class, field, endpoint hoặc thuật ngữ kỹ thuật theo cách làm mất khả năng đối chiếu với code; cần phân biệt nội dung tiếng Anh chưa dịch với identifier tiếng Anh được giữ lại có chủ ý.

## Entry 005 - Chốt separation of duties cho Teller và Admin

- Ngày: 2026-08-26
- Công cụ/model AI: Codex; project không ghi nhận được model backend chính xác.
- Tính năng/công việc: chốt mô hình user/role nhân viên và đồng bộ tài liệu kiến trúc, bảo mật.
- Tóm tắt prompt/công việc thực tế: chủ dự án cân nhắc gộp Teller và Admin, sau đó xác nhận cần ít nhất hai user riêng. Giải pháp được chốt là dùng chung Staff Portal/layout để giảm UI trùng lặp nhưng giữ hai role, hai user và API policy độc lập theo nguyên tắc separation of duties.
- File/module bị tác động: `docs/PROJECT-BLUEPRINT.md`, `docs/Architecture.md`, `docs/Security-Design.md`, `docs/AI-Usage-Report.md`.
- Kiểm chứng đã thực hiện: đối chiếu role responsibility, seed requirement, navigation, endpoint authorization và negative test cần thiết giữa các tài liệu.
- Kết quả: Development Seed Data được quy định có tối thiểu bốn user gồm hai Customer, một Teller và một Admin; Teller/Admin dùng chung Staff Portal nhưng bị tách quyền ở API; tài liệu yêu cầu test chéo role trả `403`.
- Vấn đề còn lại: Identity, seed, Staff Portal, policy và authorization test chưa được triển khai trong code.
- Con người review: CHỜ XÁC NHẬN
- Kinh nghiệm rút ra: gộp layout không đồng nghĩa gộp quyền; tách Teller/Admin giúp chứng minh RBAC và ngăn người tạo giao dịch tiền đồng thời có quyền quản trị; negative authorization test là bằng chứng quan trọng khi bảo vệ.

## Entry 006 - Chốt giao tiếp REST/SignalR và SUBank QR nội bộ

- Ngày: 2026-08-26
- Công cụ/model AI: Codex; project không ghi nhận được model backend chính xác.
- Tính năng/công việc: làm rõ trách nhiệm của REST/JSON và SignalR, đồng thời chốt QR nội bộ do SUBank tự thiết kế.
- Tóm tắt prompt/công việc thực tế: chủ dự án hỏi vì sao Architecture ghi “HTTPS REST/JSON hoặc SignalR” và xác nhận muốn tự làm QR để demo thay vì dùng NAPAS/VietQR. Tài liệu được sửa để thể hiện REST và SignalR hoạt động song song, đồng thời định nghĩa SUBank QR nội bộ cùng ranh giới bảo mật.
- File/module bị tác động: `docs/Architecture.md`, `docs/PROJECT-BLUEPRINT.md`, `docs/AI-Usage-Report.md`.
- Kiểm chứng đã thực hiện: đối chiếu luồng request-response, post-commit notification, QR payload, transfer control và tuyên bố phạm vi sản phẩm giữa Architecture và Blueprint.
- Kết quả: REST/JSON được xác định là đường nghiệp vụ chính; SignalR chỉ gửi realtime notification best-effort; SUBank QR dùng payload `subank://transfer` phiên bản 1, hỗ trợ QR tĩnh/động, camera/upload và luôn quay lại normal Transfer API.
- Vấn đề còn lại: QR library, scanner library, payload parser, API resolve và security test chưa được triển khai; visual QR cần human review khi có UI.
- Con người review: CHỜ XÁC NHẬN
- Kinh nghiệm rút ra: từ “hoặc” dễ làm sai kiến trúc giao tiếp; realtime event không thay thế request-response hoặc nguồn dữ liệu SQL; QR nội bộ an toàn khi chỉ là untrusted input được resolve, review và xác nhận lại qua transfer flow có đầy đủ control.

## Entry 007 - Chốt chiến lược primary key và public identifier

- Ngày: 2026-08-26
- Công cụ/model AI: Codex; project không ghi nhận được model backend chính xác.
- Tính năng/công việc: quyết định cách dùng internal primary key và identifier trong API trước khi thiết kế ERD.
- Tóm tắt prompt/công việc thực tế: chủ dự án yêu cầu giải thích lý do dùng `long Id` cùng `Guid PublicId`, sau đó giao Tech Lead chọn phương án phù hợp. Quyết định cuối là không thêm GUID hàng loạt; dùng `long` cho khóa nội bộ và dùng business identifier theo từng entity.
- File/module bị tác động: `docs/PROJECT-BLUEPRINT.md`, `docs/AI-Usage-Report.md`.
- Kiểm chứng đã thực hiện: đối chiếu nhu cầu index/join SQL Server, khả năng debug, API route, account enumeration, ownership authorization và độ phức tạp trong deadline một tuần.
- Kết quả: `BankAccount` dùng `AccountNumber`, `FinancialTransaction` dùng `ReferenceNo`, `AddressChangeRequest` dùng `RequestNo`; entity nội bộ không có public identifier nếu không cần; `Beneficiary.Id` chỉ được dùng cùng ownership check.
- Vấn đề còn lại: độ dài, format và chiến lược sinh `AccountNumber`, `ReferenceNo`, `RequestNo` sẽ được chốt trong Database Design/ERD.
- Con người review: CHỜ XÁC NHẬN
- Kinh nghiệm rút ra: GUID khó đoán không thay thế authorization; pattern `long + PublicId` hợp lệ nhưng gây thêm cột/index/mapping; business identifier có ý nghĩa thường đơn giản và dễ bảo vệ hơn cho một modular monolith demo.

## Entry 008 - Chốt CustomerProfile là nguồn contact duy nhất

- Ngày: 2026-08-26
- Công cụ/model AI: Codex; project không ghi nhận được model backend chính xác.
- Tính năng/công việc: đơn giản hóa mô hình contact/profile trước khi thiết kế ERD.
- Tóm tắt prompt/công việc thực tế: chủ dự án yêu cầu bỏ qua CustomerContact và chỉ sử dụng CustomerProfile. Quyết định được cụ thể hóa để tránh trùng Email/Phone giữa business profile và ASP.NET Core Identity.
- File/module bị tác động: `docs/PROJECT-BLUEPRINT.md`, `docs/Security-Design.md`, `docs/AI-Usage-Report.md`.
- Kiểm chứng đã thực hiện: đối chiếu login identifier, quan hệ `ApplicationUser` - `CustomerProfile`, profile endpoint, seed username và ownership authorization.
- Kết quả: không có entity/bảng `CustomerContact`; `CustomerProfile` giữ FullName, DateOfBirth, IdentityNumber demo, Phone, Email và hai địa chỉ; login dùng `UserName`; Identity Email/Phone không là nguồn contact nghiệp vụ; Teller/Admin không có CustomerProfile.
- Vấn đề còn lại: độ dài, nullability, unique/index và validation của Phone, Email, IdentityNumber sẽ được chốt trong Database Design/ERD.
- Con người review: CHỜ XÁC NHẬN
- Kinh nghiệm rút ra: một khái niệm nghiệp vụ cần một nguồn sự thật; dùng Email đồng thời cho login Identity và contact profile tạo nhu cầu đồng bộ không cần thiết; username-only login giúp mô hình demo đơn giản hơn.

## Entry 009 - Hoàn thiện P0 có database, bảo mật và luồng tiền cốt lõi

- Ngày: 2026-08-26
- Công cụ/model AI: Codex; project không ghi nhận được model backend chính xác.
- Tính năng/công việc: triển khai P0 xuyên suốt database, Identity, API, Blazor Client và test; không triển khai các tính năng P1/P2.
- Tóm tắt prompt/công việc thực tế: chủ dự án yêu cầu hoàn thiện P0 theo Blueprint V2.4. AI hỗ trợ tạo schema EF Core/migration/seed, bốn demo user và ba role, JWT access token, refresh-token rotation trong cookie HttpOnly, lockout, ownership/RBAC, account/history, transfer, Teller cash deposit, Admin unlock, ProblemDetails, giao diện Bootstrap tối thiểu và test.
- File/module bị tác động: các project dưới `src/`, `client/`, `tests/`; migration và cấu hình Development; `README.md`; `docs/Database-Design.md`, ERD và báo cáo này.
- Kiểm chứng đã thực hiện: restore; build toàn solution với 0 warning/0 error; 6 test pass; migration áp dụng vào SQL Server local; xác minh seed; khởi động API và Client trên hai HTTPS port; `/health`, Swagger và Client root trả 200; login Customer trả JWT/role; kiểm tra ownership/RBAC; transfer hai chiều và gửi lặp cùng idempotency key; Teller deposit rồi hoàn tác chính xác dữ liệu kiểm thử; kiểm tra lại số dư seed.
- Kết quả: P0 chạy được với SQL Server/Identity và các luồng demo cốt lõi. Transfer đầu tạo giao dịch, lần lặp trả `Replayed=true`; số dư sau live verification trở lại `100.000.000,00` và `50.000.000,00` VND. Cấu hình production không chứa connection string hoặc JWT signing key dùng được.
- Vấn đề còn lại: coverage mới là nền tảng, chưa có stress test đồng thời thực sự cho double-spend. Đây là hardening cần làm trước khi gọi sản phẩm production, không được che giấu trong demo. QR, Redis, SignalR, PDF, address workflow và AI vẫn thuộc P1/P2.
- Con người review: CHỜ XÁC NHẬN
- Kinh nghiệm rút ra: test chạy trong sandbox có thể thất bại SSPI dù code đúng vì Windows Authentication; phải chạy integration test bằng đúng OS identity. Live test luồng tiền phải có kế hoạch hoàn nguyên số dư. CORS cho refresh cookie cần `AllowCredentials`, phía browser cần gửi credentials và production secret phải fail-fast khi chưa cấu hình.

## Entry 010 - Audit lại và đóng acceptance criteria P0

- Ngày: 2026-08-26
- Công cụ/model AI: Codex; project không ghi nhận được model backend chính xác.
- Tính năng/công việc: kiểm tra lại tuyên bố hoàn thành P0, sửa lỗi được test phát hiện và nâng bộ test từ smoke test thành bằng chứng nghiệp vụ.
- Tóm tắt prompt/công việc thực tế: chủ dự án hỏi P0 đã thật sự hoàn tất chưa và yêu cầu làm nốt. AI thừa nhận coverage trước đó chưa đáp ứng Blueprint, bổ sung test trực tiếp với SQL Server cho toàn bộ vòng đời auth, lockout/unlock, ownership/RBAC, transfer atomicity/idempotency/rollback/concurrency, Teller deposit và audit; test tự hoàn nguyên giao dịch và số dư.
- File/module bị tác động: Application banking rules và unit test; Infrastructure auth/banking/seed; API rate limiting và Admin audit endpoint; Blazor transaction-detail/audit UI; integration test; README và báo cáo này.
- Kiểm chứng đã thực hiện: `dotnet restore SUBank.sln`; build 0 warning/0 error; 18/18 test pass gồm 9 integration test; concurrent test gửi hai transfer tổng cộng vượt số dư và xác nhận chỉ một commit; live HTTPS trả 200 cho `/health`, Swagger và Client; SQL xác nhận không còn transaction có key `test-%`, không user nào bị khóa và số dư trở về đúng seed.
- Kết quả: phát hiện và sửa ba vấn đề mà smoke test cũ bỏ sót: Identity reset `AccessFailedCount` về 0 khi lockout khiến Admin hiển thị sai; access token refresh có thể giống token cũ trong cùng một giây do thiếu `jti`; MARS làm EF mất savepoint trong transaction. Đồng thời bổ sung Admin audit log, transaction detail UI, account-resolution rate limit và seed có khả năng tự bổ sung dữ liệu bị thiếu.
- Vấn đề còn lại: developer HTTPS certificate trên máy hiện chưa được trust nên trình duyệt có thể cảnh báo cho đến khi chạy `dotnet dev-certs https --trust`. P1/P2 vẫn chưa triển khai đúng phạm vi.
- Con người review: CHỜ XÁC NHẬN
- Kinh nghiệm rút ra: build xanh và smoke test không chứng minh tính đúng của luồng tiền; test concurrency phải kiểm tra cả số response thành công, số transaction được ghi và invariant số dư. Test tốt có thể phát hiện hành vi framework khó thấy như lockout counter reset và JWT trùng trong cùng timestamp.

## Entry 011 - P1 active session control

- Ngày: 2026-08-26
- Công cụ/model AI: Codex; project không ghi nhận được model backend chính xác.
- Tính năng/công việc: triển khai feature P1 kiểm soát một active session cho mỗi user; không triển khai SignalR hoặc feature P1 tiếp theo.
- Tóm tắt prompt/công việc thực tế: chủ dự án yêu cầu chia P1 thành các feature branch phụ thuộc, mỗi feature phải dừng để review/commit/merge. AI đã audit toàn bộ P0 và docs, lập thứ tự feature, tạo `feature/active-session-control` từ `develop` đã pull, sau đó thêm Redis active-session pointer, SQL session history, middleware và test.
- File/module bị tác động: Domain `UserSession`; Application session abstraction/exception; Infrastructure Redis adapter, auth flow, EF configuration/migration; API middleware/rate limiting/cookie; Client CSRF header; integration test; README và tài liệu thiết kế.
- Kiểm chứng đã thực hiện: restore; build toàn solution 0 warning/0 error; 22/22 test pass bằng Windows identity; migration áp dụng vào SQL Server Development; API HTTPS khởi động; `/health` và Swagger trả 200; khi Redis không chạy, login trả ProblemDetails `503` và session/token vừa tạo được revoke. Middleware test xác nhận active session được qua, session cũ bị chặn và dependency unavailable fail-closed.
- Kết quả: Redis là nguồn thẩm quyền của active `sid`; Lua script thay con trỏ và compare-delete nguyên tử; SQL chỉ lưu lịch sử. Refresh rotation dùng SQL transaction isolation `Serializable`; refresh-token reuse thu hồi session. Cookie refresh luôn `Secure`; refresh/logout có custom CSRF header và Origin allow-list; request có correlation ID.
- Vấn đề còn lại: máy kiểm thử không có Redis runtime hoặc Docker, nên chưa có bằng chứng integration với Redis thật cho Lua success path; đã có bằng chứng fail-closed qua adapter thật khi Redis unavailable. Cookie/CSRF cần browser test thật. SignalR `ForceLogout` là feature kế tiếp và chưa được làm.
- Con người review: CHỜ XÁC NHẬN
- Kinh nghiệm rút ra: test double-spend không được tạo hai login cho cùng user sau khi có single-session; hai request concurrent phải dùng cùng access token. In-memory fake chỉ kiểm tra contract/middleware, không thay thế integration Redis thật. Rate limiter theo IP phải đủ cho test suite hợp lệ nhưng Identity lockout vẫn là lớp chặn brute-force theo user.

## Entry 012 - P1 SignalR realtime notifications

- Ngày: 2026-08-26
- Công cụ/model AI: Codex; project không ghi nhận được model backend chính xác.
- Tính năng/công việc: thêm SignalR `ForceLogout`, `BalanceChanged` và `TransactionReceived` theo cơ chế best-effort.
- Tóm tắt prompt/công việc thực tế: sau khi chủ dự án cho phép commit/push/merge active-session feature, AI đã merge feature đó vào `develop`, tạo `feature/realtime-notifications` từ `develop` mới nhất và triển khai vertical slice realtime. Hub chỉ chấp nhận JWT hợp lệ; notification giao dịch chỉ phát sau SQL commit; Blazor nhận event rồi tải lại dữ liệu qua REST.
- File/module bị tác động: Contracts realtime DTO; Application notifier abstraction; API BankingHub/notifier/JWT hub configuration; Infrastructure Auth/Banking/Staff services; Blazor realtime service, app lifecycle, status component và trang account/transaction; integration tests; README và tài liệu.
- Kiểm chứng đã thực hiện: restore; build toàn solution 0 warning/0 error; 23/23 test pass. Integration test kết nối SignalR Long Polling thật với JWT, login lần hai và nhận `ForceLogout`; token cũ đồng thời bị REST trả `401`. Test transfer/deposit xác nhận notification chỉ xuất hiện ở nhánh commit mới, không phát lại khi idempotency replay.
- Kết quả: SignalR không mang balance làm nguồn sự thật. Client nhận event, hiển thông báo và refetch REST. Lỗi gửi event được technical log nhưng không làm giao dịch đã commit thất bại.
- Vấn đề còn lại: chưa browser-test WebSocket trên HTTPS deployment và chưa có Redis runtime local; integration test dùng TestServer Long Polling và active-session store có cùng contract. Dự án chỉ nhắm một API instance nên không thêm SignalR backplane.
- Con người review: CHỜ XÁC NHẬN
- Kinh nghiệm rút ra: realtime event phải phát sau commit và không được biến thành dependency của core banking. `ForceLogout` cho UX nhanh, nhưng request authorization vẫn phải do Redis active-session middleware quyết định.

## Mẫu ghi nhận cho các entry tiếp theo

Sao chép phần sau sau mỗi milestone có AI hỗ trợ đáng kể:

```text
## Entry NNN - Tên milestone hoặc tính năng

- Ngày:
- Công cụ/model AI:
- Tính năng/công việc:
- Tóm tắt prompt/công việc thực tế:
- File/module bị tác động:
- Kiểm chứng đã thực hiện:
- Kết quả:
- Vấn đề còn lại:
- Con người review: CHỜ XÁC NHẬN | ĐÃ REVIEW kèm bằng chứng/tham chiếu
- Kinh nghiệm rút ra:
```

## Thư viện prompt tái sử dụng

Các prompt dưới đây là template phục vụ lập kế hoạch, không phải bằng chứng rằng chúng đã được thực thi. Khi một template thực sự được dùng, phải ghi tóm tắt task thật và kết quả vào một entry có ngày ở phía trên.

### Review kiến trúc và phạm vi

```text
Đóng vai trò Senior .NET Tech Lead. Review milestone này theo SUBank V2.4 trước khi code. Hãy phát hiện việc mở rộng phạm vi, vi phạm dependency rule, rủi ro bảo mật, thay đổi schema/API, acceptance criteria còn thiếu và test bắt buộc. Giải thích kết quả cho người mới bắt đầu. Không sửa code trước khi các quyết định thiết kế được duyệt.
```

### Triển khai vertical slice

```text
Chỉ triển khai [TÍNH NĂNG] dưới dạng một vertical slice xuyên suốt Domain, Application, Infrastructure, API, Blazor UI, test và tài liệu. Giữ đúng dependency của Clean Architecture. Không thêm công nghệ tương lai không liên quan. Trước khi sửa, kiểm tra Git branch và working tree. Sau khi sửa, restore, build, chạy test liên quan, khởi động host bị tác động, kiểm tra endpoint/UI state thực tế, sửa lỗi và thêm entry trung thực vào AI Usage Report với trạng thái Con người review: CHỜ XÁC NHẬN.
```

### Review bảo mật ngân hàng

```text
Threat-model luồng ngân hàng sau: [LUỒNG]. Kiểm tra authentication, active session, role và ownership authorization, input validation, idempotency, concurrency, SQL atomicity, lộ secret/PII, brute force, enumeration, logging, rollback behavior và negative test. Phân loại phát hiện thành Blocker, Critical, Major và Minor. Không tuyên bố control đã tồn tại nếu chưa có bằng chứng từ code hoặc test.
```

### Review triển khai transfer

```text
Review transfer implementation để phát hiện double-spend và duplicate-submit. Kiểm tra source ownership, account status, amount scale, balance từ SQL, transaction-password throttling, idempotency key bắt buộc, một SQL transaction cho debit/credit/transaction record, RowVersion conflict, rollback, audit handling, SignalR sau commit, ProblemDetails response và concurrent integration test.
```

### Bàn giao kiến thức cho con người

```text
Giải thích milestone vừa hoàn thành từ số 0: hành động của user, HTTP request, trách nhiệm của controller, Application use case, Domain rule, công việc của Infrastructure/database, response, UI state và test. Sau đó liệt kê nội dung tôi cần tự kiểm tra, lỗi phổ biến và ba câu hỏi tôi phải trả lời được khi bảo vệ dự án.
```
