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
- Kết quả: không có entity/bảng `CustomerContact`; `CustomerProfile` giữ FullName, DateOfBirth, IdentityCardNumber demo, Phone, Email và hai địa chỉ; login dùng `UserName`; Identity Email/Phone không là nguồn contact nghiệp vụ; Teller/Admin không có CustomerProfile.
- Vấn đề còn lại: độ dài, nullability, unique/index và validation của Phone, Email, IdentityCardNumber sẽ được chốt trong Database Design/ERD.
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

## Entry 013 - P1 quy trình đổi địa chỉ

- Ngày: 2026-08-26
- Trạng thái hiện tại: ĐÃ GỠ BỎ khỏi phạm vi chạy của dự án ngày 2026-08-28 theo quyết định của chủ dự án; xem Entry 028. Nội dung bên dưới được giữ lại như lịch sử sử dụng AI, không mô tả tính năng hiện còn hoạt động.
- Công cụ/model AI: Codex; project không ghi nhận được model backend chính xác.
- Tính năng/công việc: Customer tạo yêu cầu đổi địa chỉ; Admin duyệt hoặc từ chối.
- Tóm tắt prompt/công việc thực tế: AI đã commit/push/merge realtime feature theo ủy quyền, sau đó tạo `feature/address-change-workflow` từ `develop`. Vertical slice bổ sung entity/migration, validation, service transaction, Customer/Admin API, Bootstrap UI, RBAC/audit và integration test.
- File/module bị tác động: Domain/Contracts/Application address change; Infrastructure service/EF migration; hai API controller; Blazor Customer/Admin page và navigation; integration test; README, database design và báo cáo này.
- Kiểm chứng đã thực hiện: build 0 warning/0 error; 24/24 test pass. Test bao phủ input rỗng, duplicate pending, Teller bị `403`, Admin approve/re-approve conflict, reject có lý do, profile chỉ thay đổi khi approve và cleanup dữ liệu.
- Kết quả: `CustomerProfile` vẫn là nguồn địa chỉ có thẩm quyền; request lưu lịch sử và business identifier `RequestNo`. Approve và profile update dùng cùng SQL transaction.
- Vấn đề còn lại: chưa có upload giấy tờ xác minh vì không thuộc phạm vi V2.4; UI là quy trình demo tối thiểu.
- Con người review: CHỜ XÁC NHẬN
- Kinh nghiệm rút ra: request workflow phải tách dữ liệu đề nghị khỏi profile authoritative; nếu update profile trước khi Admin duyệt thì approval chỉ còn là hình thức.

## Entry 014 - P1 sao kê tháng/năm và PDF

- Ngày: 2026-08-26
- Công cụ/model AI: Codex; project không ghi nhận được model backend chính xác.
- Tính năng/công việc: authorized statement read model theo tháng/năm và PDF tối thiểu render phía server.
- Tóm tắt prompt/công việc thực tế: AI merge address workflow và tạo `feature/account-statements`; sau đó bổ sung statement contract/service/controller, deterministic opening/closing balance, QuestPDF generator, Blazor query/download UI và integration test.
- File/module bị tác động: Contracts/Application statement abstraction; Infrastructure query/PDF generator/package; API controller; Blazor page, download JavaScript và navigation; tests; README/Architecture/báo cáo.
- Kiểm chứng đã thực hiện: restore; build 0 warning/0 error; 25/25 test pass. Test tạo transaction thật, kiểm tra debit/tổng/công thức closing balance, ownership `404`, month sai `422`, MIME PDF, kích thước và header `%PDF`.
- Kết quả: không tạo bảng Statement. Opening balance được suy ra từ current SQL balance và net movement kể từ đầu kỳ; closing balance bằng opening + credit - debit. PDF không chứa secret và được authorization giống JSON endpoint.
- Vấn đề còn lại: QuestPDF `2026.8.0` dùng Community License phù hợp dự án cá nhân/học tập; phải review lại license nếu chuyển sang tổ chức thương mại không còn đủ điều kiện. PDF hiện tối thiểu, trang trí nâng cao thuộc P2.
- Con người review: CHỜ XÁC NHẬN
- Kinh nghiệm rút ra: file download cũng phải qua ownership authorization; không thể dùng anchor URL trực tiếp khi access token chỉ nằm trong memory, nên Client phải gọi API có Bearer token rồi tạo download.

## Entry 015 - P1 SUBank QR nội bộ

- Ngày: 2026-08-26
- Công cụ/model AI: Codex; project không ghi nhận được model backend chính xác.
- Tính năng/công việc: tạo và đọc SUBank QR nội bộ để điền trước giao dịch chuyển tiền.
- Tóm tắt prompt/công việc thực tế: AI merge feature sao kê vào `develop`, tạo `feature/internal-qr-transfer`, rồi triển khai payload có version, tạo PNG, decode ảnh upload/camera phía server và điều hướng sang form transfer hiện hữu.
- File/module bị tác động: Contracts QR; Application service contract và payload rules; Infrastructure QRCoder/ZXing ImageSharp service; API controller/rate limit; Blazor QR page, camera JavaScript, transfer query prefill và navigation; unit/integration test; README và báo cáo này.
- Kiểm chứng đã thực hiện: restore; build toàn solution 0 warning/0 error; 32/32 test pass. Integration test tạo QR PNG thật bằng API rồi upload chính ảnh đó để decode; kiểm tra ownership khi tạo QR. Unit test bao phủ payload hợp lệ, foreign scheme, sai version/account/amount và duplicate query key.
- Kết quả: QR chỉ chứa account, amount/message tùy chọn và không chuyển tiền trực tiếp. Dữ liệu decode luôn quay lại form transfer để user review và đi qua toàn bộ validation, transaction password, idempotency, ownership và SQL transaction hiện hữu.
- Vấn đề còn lại: camera cần HTTPS và quyền trình duyệt; chưa browser-test trên thiết bị di động thật. Đây là QR riêng của đồ án, không tương thích và không được tuyên bố kết nối VietQR/NAPAS.
- Con người review: CHỜ XÁC NHẬN
- Kinh nghiệm rút ra: QR phải được coi là input không đáng tin cậy; giới hạn MIME/dung lượng/kích thước ảnh, allow-list scheme/version và rate limit decode ngăn parser ảnh trở thành đường tấn công. Dùng ImageSharp managed binding giúp môi trường deploy container không phụ thuộc native graphics library.

## Entry 016 - Hoàn thiện giao diện phục vụ demo

- Ngày: 2026-08-26
- Công cụ/model AI: Codex; project không ghi nhận được model backend chính xác.
- Tính năng/công việc: chuẩn hóa visual system Bootstrap và UX responsive cho các luồng demo hiện có; không thêm business feature và không triển khai AI.
- Tóm tắt prompt/công việc thực tế: chủ dự án yêu cầu hoàn thiện giao diện chạy thật trước khi dựng Figma, sau đó yêu cầu màn hình đăng nhập mang tinh thần VietinBank iPay và giao diện sau đăng nhập tham khảo SUBank V1. AI audit toàn bộ Blazor page, tạo branch `feature/frontend-polish`, đối chiếu source V1 công khai, rồi chuyển visual system sang đen/vàng `#111111` - `#B58A32` - `#9C7427` - `#E8D5AE` mà không sao chép React hay thêm thư viện UI mới.
- File/module bị tác động: Blazor layout/navigation và AuthLayout; global CSS; Home/Login/Forbidden/NotFound; Customer account/transaction/transfer/statement/QR/address pages; Teller deposit; Admin locked-user/audit/address pages; logo do chủ dự án cung cấp; README và báo cáo này.
- Kiểm chứng đã thực hiện: build toàn solution 0 warning/0 error; 32/32 test pass; API `/health`, Swagger và Client HTTPS hoạt động. Sau khi chủ dự án cài Redis, browser automation đã đăng nhập thật bằng Customer, tải dữ liệu thật và chụp kiểm tra login ở 775px, dashboard ở 1440px và 775px.
- Kết quả: URL gốc mở thẳng login; logo corgi và chữ SUBank không còn bị cắt khi thu cửa sổ; form tự chuyển một cột trước 992px. Giao diện sau đăng nhập dùng sidebar đen trên desktop, topbar/menu trượt trên tablet-mobile, thẻ tài khoản vàng/đen, quick action và màu trạng thái có ý nghĩa nghiệp vụ. Landing page giới thiệu `/welcome` được loại bỏ; logo SUBank điều hướng thẳng về màn hình chính của Customer, Teller hoặc Admin. Navigation vẫn hiển thị đúng theo vai trò và tái sử dụng toàn bộ API/luồng V2 hiện có.
- Vấn đề còn lại: cần chủ dự án review trực quan trên trình duyệt/màn hình thật; Figma tiếp tục giữ `PENDING` cho đến khi UI code được chốt. Chưa kiểm thử camera QR trên thiết bị di động thật.
- Con người review: CHỜ XÁC NHẬN
- Kinh nghiệm rút ra: breakpoint phải dựa trên độ rộng nội dung thực tế chứ không chỉ tên thiết bị; mốc 768px vẫn làm lockup thương hiệu tràn ở viewport 775px nên phải chuyển layout tại 992px. Tham khảo dự án cũ hiệu quả nhất khi giữ lại ngôn ngữ thiết kế tốt nhưng loại bỏ công nghệ và chức năng không còn phù hợp. Figma nên phản ánh UI đã được chạy và review thay vì đi trước một giao diện còn thay đổi.

## Entry 017 - Bổ sung tài khoản demo thứ hai cho mỗi Customer

- Ngày: 2026-08-26
- Công cụ/model AI: Codex; project không ghi nhận được model backend chính xác.
- Tính năng/công việc: bổ sung seed Development để hai Customer demo đều có hai tài khoản ngân hàng.
- Tóm tắt prompt/công việc thực tế: chủ dự án yêu cầu thêm cho mỗi Customer một tài khoản thứ hai. AI kiểm tra quan hệ dữ liệu hiện hữu, xác nhận `CustomerProfile` đã hỗ trợ nhiều `BankAccount`, rồi mở rộng seed mà không đổi schema hoặc tạo migration.
- File/module bị tác động: `DatabaseInitializer`, README, tài liệu thiết kế database và báo cáo này.
- Kiểm chứng đã thực hiện: chỉ chạy build theo quyết định hiện tại của chủ dự án; không viết hoặc chạy test trong giai đoạn hoàn thiện tính năng.
- Kết quả: `customer.a` có `1000000001` và `1000000003`; `customer.b` có `1000000002` và `1000000004`. Seed chỉ thêm account còn thiếu, không tạo trùng và không đặt lại balance của account đã tồn tại.
- Vấn đề còn lại: kiểm thử tự động và kiểm thử hồi quy seed được hoãn đến giai đoạn hoàn thiện project theo yêu cầu của chủ dự án.
- Con người review: CHỜ XÁC NHẬN
- Kinh nghiệm rút ra: dữ liệu seed chạy nhiều lần phải giữ tính idempotent và không được ghi đè trạng thái nghiệp vụ mà người dùng đã tạo trong database hiện hữu.

## Entry 018 - Tinh gọn bộ chọn tài khoản và nhóm thao tác

- Ngày: 2026-08-26
- Công cụ/model AI: Codex; project không ghi nhận được model backend chính xác.
- Tính năng/công việc: tinh gọn dashboard Customer sau khi bổ sung tài khoản thứ hai; không thay đổi API hoặc nghiệp vụ ngân hàng.
- Tóm tắt prompt/công việc thực tế: chủ dự án yêu cầu chỉ hiển thị một thẻ tài khoản, dùng nút thả xuống để đổi tài khoản đang xem, gom các hành động ra cùng một khu vực, rút gọn nhãn QR/Sao kê và đồng bộ icon màu vàng trên nền nhạt.
- File/module bị tác động: trang `Accounts.razor`, global visual system trong `app.css` và báo cáo này.
- Kiểm chứng đã thực hiện: chỉ chạy build Client theo quyết định hiện tại của chủ dự án; không viết hoặc chạy test trong giai đoạn hoàn thiện tính năng.
- Kết quả: account đầu tiên mặc định là tài khoản chính; account thứ hai có thể chọn từ dropdown. Lịch sử giao dịch luôn dùng account đang chọn. Năm thao tác Lịch sử, Chuyển tiền, Sao kê, QR và Đổi địa chỉ dùng bộ SVG nhất quán, màu vàng và nền nhạt; không còn action trùng trong thẻ hoặc tiêu đề “Thao tác nhanh”.
- Vấn đề còn lại: cần chủ dự án review trực quan tương tác dropdown và khoảng cách ở màn hình thật; kiểm thử tự động được hoãn theo yêu cầu.
- Con người review: CHỜ XÁC NHẬN
- Kinh nghiệm rút ra: khi một màn hình có nhiều account, nên giữ một điểm tập trung và cho user đổi context; mọi action phụ thuộc account phải đọc cùng context đang chọn để tránh gây hiểu nhầm.

## Entry 019 - Hoàn thiện điều hướng Customer và hồ sơ chỉ đọc

- Ngày: 2026-08-27
- Công cụ/model AI: Codex; project không ghi nhận được model backend chính xác.
- Tính năng/công việc: sắp xếp lại menu và nhóm thao tác của Customer, di chuyển bộ chọn tài khoản, đồng thời bổ sung trang xem hồ sơ của chính Customer đang đăng nhập.
- Tóm tắt prompt/công việc thực tế: chủ dự án yêu cầu menu có Trang chủ, Thông tin khách hàng và QR; QR nằm giữa nhóm thao tác, Lịch sử giao dịch nằm cuối và nút chọn tài khoản ở góc dưới thẻ. AI kiểm tra source để tránh tạo liên kết chết, xác nhận chưa có profile API/UI rồi triển khai lát cắt đọc hồ sơ tối thiểu xuyên Contracts, Application, Infrastructure, API và Blazor.
- File/module bị tác động: Customer navigation; trang Accounts và global CSS; profile contract/service/controller/page; đăng ký dependency injection; ApiSession; README và báo cáo này.
- Kiểm chứng đã thực hiện: build toàn solution; không viết hoặc chạy test theo quyết định hiện tại của chủ dự án trong giai đoạn hoàn thiện tính năng.
- Kết quả: menu Customer dùng nhãn Trang chủ, QR và Thông tin khách hàng. Năm thao tác được sắp theo thứ tự Chuyển tiền, Sao kê, QR, Đổi địa chỉ, Lịch sử giao dịch. Bộ chọn tài khoản nằm ở góc dưới bên phải thẻ, danh sách mở lên trên và cuộn khi dài; tài khoản đang chọn được truyền đúng sang Chuyển tiền, Sao kê, QR và Lịch sử. `/api/profile` chỉ lấy hồ sơ từ user ID trong access token, chỉ cho role Customer, không nhận ID từ Client, không trả khóa database và che CCCD/CMND trước khi trả response.
- Vấn đề còn lại: cần chủ dự án review trực quan trên màn hình thật; kiểm thử tự động được hoãn đến giai đoạn hoàn thiện project theo yêu cầu.
- Con người review: CHỜ XÁC NHẬN
- Kinh nghiệm rút ra: một mục menu phải dẫn tới luồng hoạt động thật thay vì trang giả; API hồ sơ cá nhân nên suy ra chủ sở hữu từ authentication context và tối thiểu hóa PII ngay trong response.

## Entry 020 - Customer đăng nhập bắt buộc bằng số điện thoại

- Ngày: 2026-08-27
- Công cụ/model AI: Codex; project không ghi nhận được model backend chính xác.
- Tính năng/công việc: đổi login identifier của Customer sang đúng số điện thoại trong hồ sơ, giữ username nghiệp vụ cho Teller/Admin.
- Tóm tắt prompt/công việc thực tế: chủ dự án yêu cầu tên đăng nhập Customer bắt buộc là số điện thoại của chính Customer. AI audit Identity, authentication, seed, profile, giao diện và tài liệu; sau đó bổ sung quy tắc số điện thoại chuẩn 10 chữ số, kiểm tra ánh xạ khi login và cơ chế đổi username seed cũ tại chỗ.
- File/module bị tác động: Application customer-login rule; Infrastructure AuthService và DatabaseInitializer; Login, Customer dashboard/sidebar; README, Blueprint, Security Design, Database Design và báo cáo này.
- Kiểm chứng đã thực hiện: build toàn solution; không viết hoặc chạy test theo quyết định hiện tại của chủ dự án trong giai đoạn hoàn thiện tính năng.
- Kết quả: `0900000001` và `0900000002` là login Customer Development; `teller` và `admin` không đổi. Seed dùng `UserManager.SetUserNameAsync` để giữ nguyên `UserId`, profile, account, transaction, audit và session history. Auth từ chối Customer nếu username không đúng định dạng hoặc không trùng `CustomerProfile.Phone`. UI dùng họ tên để chào và che bớt số điện thoại ở sidebar.
- Vấn đề còn lại: nếu làm chức năng đổi số điện thoại, phải cập nhật profile/username nguyên tử và thu hồi phiên đăng nhập cũ. Fixture integration test đã được đổi đồng bộ sang số điện thoại, nhưng chưa chạy test theo quyết định hiện tại của chủ dự án.
- Con người review: CHỜ XÁC NHẬN
- Kinh nghiệm rút ra: đổi login identifier là thay đổi dữ liệu định danh chứ không chỉ đổi label giao diện; phải giữ khóa user ổn định, fail-fast khi có xung đột và xác định rõ nguồn sự thật để tránh hai giá trị lệch nhau.

## Entry 021 - Cân chỉnh tagline thương hiệu trên màn hình đăng nhập

- Ngày: 2026-08-27
- Công cụ/model AI: Codex; project không ghi nhận được model backend chính xác.
- Tính năng/công việc: tăng kích thước và độ giãn chữ của tagline `Simple. Secure. Seamless.` trên màn hình đăng nhập.
- Tóm tắt prompt/công việc thực tế: chủ dự án yêu cầu tagline lớn hơn và có chiều dài thị giác gần bằng cụm logo cùng chữ SUBank, sau đó chốt giữ khoảng cách ký tự ban đầu. AI điều chỉnh responsive font-size riêng cho login và giữ `letter-spacing: .18em`, không thay đổi tagline sidebar hay nghiệp vụ.
- File/module bị tác động: `Login.razor.css` và báo cáo này.
- Kiểm chứng đã thực hiện: build Client; không chạy test theo quyết định hiện tại của chủ dự án.
- Kết quả: tagline desktop lớn và trải ngang gần bằng brand lockup bằng cỡ chữ thay vì kéo giãn ký tự; tablet/mobile có cỡ chữ riêng để không tràn màn hình nhỏ.
- Vấn đề còn lại: cần chủ dự án review trực quan trên màn hình thật.
- Con người review: CHỜ XÁC NHẬN
- Kinh nghiệm rút ra: độ dài thị giác của tagline phụ thuộc đồng thời vào font-size và letter-spacing; breakpoint nhỏ cần giảm cả hai thay vì chỉ thu cỡ chữ.

## Entry 022 - Chuyển logo corgi thành wordmark SUBank 3S

- Ngày: 2026-08-27
- Công cụ/model AI: Codex; project không ghi nhận được model backend chính xác.
- Tính năng/công việc: thay logo ảnh corgi bằng wordmark chữ `SUBank 3S` trên toàn bộ giao diện.
- Tóm tắt prompt/công việc thực tế: chủ dự án yêu cầu giữ chữ SUBank, thêm ký hiệu 3S cách điệu đơn giản phía sau và dùng cả cụm làm logo. AI rà soát mọi tham chiếu ảnh, tạo component Blazor dùng chung rồi thay tại login, sidebar và mobile header.
- File/module bị tác động: component `BrandWordmark`; Login; NavMenu; global CSS, scoped CSS; favicon SVG, host page và báo cáo này.
- Kiểm chứng đã thực hiện: build Client; không chạy test theo quyết định hiện tại của chủ dự án.
- Kết quả: `SU` dùng màu vàng chủ đạo, `Bank` dùng màu tương phản theo nền; `3S` cao bằng SUBank, đứng cùng baseline và dùng nét vàng đậm tối giản, không có khung hoặc nền huy hiệu. Logo là HTML/CSS nên sắc nét ở mọi kích thước và có accessible label `SUBank 3S`; ảnh corgi không còn được giao diện tham chiếu. Favicon Blazor mặc định được thay bằng monogram `3S` cùng bảng màu.
- Vấn đề còn lại: file ảnh corgi được giữ lại nhưng không sử dụng để có thể hoàn tác; cần chủ dự án review tỷ lệ wordmark trên màn hình thật.
- Con người review: CHỜ XÁC NHẬN
- Kinh nghiệm rút ra: wordmark nên là component dùng chung thay vì lặp markup; CSS custom property giúp mỗi layout điều chỉnh kích thước mà không phân nhánh component.

## Entry 023 - Khắc phục màn hình trắng của Blazor WebAssembly

- Ngày: 2026-08-27
- Công cụ/model AI: Codex; project không ghi nhận được model backend chính xác.
- Tính năng/công việc: chẩn đoán và khôi phục môi trường chạy Client sau khi giao diện chỉ hiển thị trang trắng.
- Tóm tắt prompt/công việc thực tế: chủ dự án báo Client hiện trang trắng sau thay đổi wordmark. AI kiểm tra tiến trình và log Development Server, xác định lỗi nằm ở bộ file sinh ra và runtime WebAssembly thay vì CSS/logo, sau đó dừng tiến trình lỗi, clean output, restore rõ runtime `browser-wasm`, build và khởi động lại Client.
- File/module bị tác động: output sinh tự động trong `client/SUBank.Client/bin` và `client/SUBank.Client/obj`; source giao diện không cần sửa để xử lý sự cố. Báo cáo này được cập nhật để lưu dấu vết.
- Kiểm chứng đã thực hiện: Client build thành công với 0 warning, 0 error; tiến trình chạy không còn exception; trang gốc, isolated CSS, JavaScript runtime có fingerprint và Client WASM đều trả HTTP 200. Không chạy test theo quyết định hiện tại của chủ dự án.
- Kết quả: Client hoạt động lại tại `https://localhost:7081`; nguyên nhân là runtime pack `browser-wasm` chưa đầy đủ cùng output cũ sau khi môi trường .NET thay đổi, không phải thay đổi giao diện `SUBank 3S`.
- Vấn đề còn lại: trình duyệt đã mở từ trước có thể giữ asset/cache cũ; cần hard refresh hoặc mở tab riêng tư để nhận manifest và fingerprint mới.
- Con người review: CHỜ XÁC NHẬN
- Kinh nghiệm rút ra: với Blazor WebAssembly, trang HTML có thể trả 200 nhưng UI vẫn trắng nếu runtime hoặc assembly fingerprint bị thiếu; khi chẩn đoán phải kiểm tra đồng thời log host, JavaScript runtime và file WASM, không chỉ kiểm tra endpoint gốc.

## Entry 024 - Chuẩn hóa nội dung ô tên đăng nhập

- Ngày: 2026-08-27
- Công cụ/model AI: Codex; project không ghi nhận được model backend chính xác.
- Tính năng/công việc: rút gọn nội dung hiển thị của trường đăng nhập.
- Tóm tắt prompt/công việc thực tế: chủ dự án yêu cầu bỏ câu hướng dẫn riêng cho Customer và chỉ hiển thị nội dung chung là tên đăng nhập.
- File/module bị tác động: trang `Login.razor` và báo cáo này.
- Kiểm chứng đã thực hiện: build Client; không chạy test theo quyết định hiện tại của chủ dự án.
- Kết quả: nhãn hiển thị `Tên đăng nhập`, placeholder hiển thị `Nhập tên đăng nhập`; quy tắc Customer dùng số điện thoại ở backend không thay đổi.
- Vấn đề còn lại: không có.
- Con người review: CHỜ XÁC NHẬN
- Kinh nghiệm rút ra: nội dung UI có thể dùng thuật ngữ chung để form gọn hơn, trong khi quy tắc định danh và kiểm tra bảo mật vẫn phải được giữ ở backend.

## Entry 025 - Đồng bộ số điện thoại, username và tài khoản chính

- Ngày: 2026-08-28
- Công cụ/model AI: Codex; project không ghi nhận được model backend chính xác.
- Tính năng/công việc: sửa seed Development và dữ liệu demo để số điện thoại, tên đăng nhập và số tài khoản chính của Customer là cùng một giá trị.
- Tóm tắt prompt/công việc thực tế: khi khởi động demo, API crash vì code seed đã bị lệch khỏi README, tiếp tục tạo `customer.a/customer.b` và đụng unique email của hồ sơ hiện hữu. Chủ dự án đồng thời chốt ba định danh của Customer demo phải giống nhau. AI đối chiếu database, Identity, account và transaction relation rồi sửa seed theo hướng migration tại chỗ.
- File/module bị tác động: `DatabaseInitializer`; giá trị mặc định của Transfer và Teller deposit; fixture account trong integration test; README, Database Design, Project Blueprint và báo cáo này.
- Kiểm chứng đã thực hiện: build toàn solution thành công với 0 warning, 0 error; không chạy test theo quyết định hiện tại của chủ dự án. API khởi động qua seed cũ thành công; `/health`, Swagger và Client trả HTTP 200; đăng nhập `0900000001` trả HTTP 200; API account trả tài khoản chính `0900000001` và tài khoản phụ `1000000003`.
- Kết quả: Customer A dùng `0900000001` và Customer B dùng `0900000002` đồng thời cho phone, username và primary account. Seed đổi username/tài khoản cũ tại chỗ, giữ `UserId`, `BankAccount.Id`, balance và foreign key transaction; account phụ chỉ được thêm khi còn thiếu. User legacy mồ côi do lần seed lỗi được vô hiệu hóa.
- Vấn đề còn lại: thay đổi đang nằm trên working tree của `develop` vì môi trường trước đó từ chối quyền tạo branch; cần chuyển sang branch sửa lỗi trước khi commit. Stash AI vẫn được giữ nguyên và không tham gia thay đổi này.
- Con người review: CHỜ XÁC NHẬN

- Kinh nghiệm rút ra: tài liệu seed và code seed phải được kiểm tra cùng nhau sau khi merge các feature phụ thuộc; đổi số tài khoản bằng cách cập nhật bản ghi hiện hữu sẽ giữ toàn bộ lịch sử vì transaction tham chiếu khóa nội bộ thay vì chuỗi account number.

## Entry 026 - Bổ sung tài khoản phụ và ưu tiên thao tác QR

- Ngày: 2026-08-28
- Công cụ/model AI: Codex; project không ghi nhận được model backend chính xác.
- Tính năng/công việc: thêm hai tài khoản phụ mới cho mỗi Customer demo và chuyển QR đứng ngay sau Chuyển tiền.
- Tóm tắt prompt/công việc thực tế: chủ dự án yêu cầu mỗi Customer có thêm hai account number khác rõ ràng với số điện thoại, đồng thời đưa QR lên trước Sao kê ở menu và dashboard. AI giữ constraint account number 10 chữ số nên dùng các số dạng `1234567890`, không đổi schema.
- File/module bị tác động: Development seed; `NavMenu.razor`; dashboard `Accounts.razor`; README, Database Design và báo cáo này.
- Kiểm chứng đã thực hiện: build toàn solution thành công với 0 warning, 0 error; không chạy test theo quyết định hiện tại của chủ dự án. API và Client được restart; đăng nhập cả hai Customer và gọi account API xác nhận mỗi người có đúng bốn tài khoản.
- Kết quả: Customer `0900000001` có `0900000001`, `1000000003`, `1234567890`, `1234567891`; Customer `0900000002` có `0900000002`, `1000000004`, `2234567890`, `2234567891`. Menu và dashboard đều có thứ tự `Chuyển tiền → QR → Sao kê`.
- Vấn đề còn lại: các thay đổi vẫn chưa commit và đang nằm trên working tree của `develop`; stash AI tiếp tục được giữ nguyên.
- Con người review: CHỜ XÁC NHẬN
- Kinh nghiệm rút ra: khi thêm dữ liệu demo nên tuân theo constraint hiện có và dùng seed idempotent để lần chạy sau không tạo trùng hoặc đặt lại số dư.

## Entry 027 - Sửa dropdown chọn tài khoản khi có nhiều account

- Ngày: 2026-08-28
- Công cụ/model AI: Codex; project không ghi nhận được model backend chính xác.
- Tính năng/công việc: sửa danh sách chọn tài khoản trên dashboard Customer sau khi mỗi Customer có bốn tài khoản.
- Tóm tắt prompt/công việc thực tế: chủ dự án báo nút thả tài khoản bị lỗi. AI kiểm tra markup và CSS, xác định menu tuyệt đối đang nằm trong `.bank-card` có `overflow: hidden`, khiến danh sách dài bị cắt và vùng cuộn quá thấp.
- File/module bị tác động: `Accounts.razor`, global CSS và báo cáo này.
- Kiểm chứng đã thực hiện: build Client thành công với 0 warning, 0 error; restart Development Client và kiểm tra host trả HTTP 200. Không chạy test theo quyết định hiện tại của chủ dự án.
- Kết quả: nút mũi tên vẫn ở góc dưới phải của thẻ; khi mở, danh sách được render theo normal flow ngay dưới thẻ, không bị clipping, không che khu thao tác và có giới hạn chiều cao responsive để cuộn trên màn hình thấp. ARIA listbox không đầy đủ được thay bằng nhóm button có trạng thái nhấn phù hợp.
- Vấn đề còn lại: cần chủ dự án review trực quan trên kích thước màn hình đang dùng; thay đổi chưa commit.
- Con người review: CHỜ XÁC NHẬN
- Kinh nghiệm rút ra: popover không nên nằm trong ancestor có `overflow: hidden` trừ khi dùng portal; với danh sách dài và Blazor thuần, normal flow là giải pháp đơn giản, ổn định và responsive hơn.

## Entry 028 - Gỡ bỏ quy trình đổi địa chỉ

- Ngày: 2026-08-28
- Công cụ/model AI: Codex; project không ghi nhận được model backend chính xác.
- Tính năng/công việc: loại bỏ toàn bộ vertical slice đổi địa chỉ khỏi phạm vi hiện hành của SUBank V2.4.
- Tóm tắt prompt/công việc thực tế: chủ dự án yêu cầu bỏ toàn bộ những thứ liên quan đến chức năng đổi địa chỉ. AI đã rà soát Domain, Contracts, Application, Infrastructure, API, Blazor UI, integration test và tài liệu; xóa code chạy, route, menu, trang giao diện và test của feature. Migration đã áp dụng và Audit Log cũ được giữ làm lịch sử; một migration bù được tạo để xóa bảng khỏi schema hiện tại mà không sửa lịch sử migration.
- File/module bị tác động: entity/enum/contract/interface/service/configuration/controller/page của address workflow; `SUBankDbContext`, dependency injection, `ApiSession`, menu, dashboard, hồ sơ, CSS, integration test; README, Architecture, Security Design, Project Blueprint, Database Design và báo cáo này.
- Kiểm chứng đã thực hiện: `dotnet build SUBank.sln --no-restore` thành công với 0 warning, 0 error; API khởi động và áp dụng migration xóa bảng; `/health`, Swagger và Client trả HTTP 200; hai route đổi địa chỉ cũ trả HTTP 404. Không chạy test theo quyết định hiện tại của chủ dự án.
- Kết quả: code hiện hành không còn endpoint Customer/Admin, UI hay dependency cho quy trình đổi địa chỉ. `CustomerProfile.PermanentAddress` và `TemporaryAddress` vẫn được giữ vì là dữ liệu hồ sơ chỉ đọc, không phải chức năng thay đổi địa chỉ.
- Vấn đề còn lại: các migration cũ và entry AI lịch sử vẫn nhắc đến feature để bảo toàn lịch sử kỹ thuật và tính trung thực của báo cáo; chúng không làm feature hoạt động trở lại.
- Con người review: CHỜ XÁC NHẬN
- Kinh nghiệm rút ra: không xóa hoặc sửa migration đã có thể được áp dụng ở database; khi thu hồi một feature có schema, cần xóa model hiện hành rồi tạo migration bù để mọi database cũ và database tạo mới đều hội tụ về cùng schema cuối.

## Entry 029 - Sửa lỗi Client không gọi được API khi chạy demo

- Ngày: 2026-08-28
- Công cụ/model AI: Codex; project không ghi nhận được model backend chính xác.
- Tính năng/công việc: chẩn đoán lỗi `TypeError: Failed to fetch` trên màn hình đăng nhập Development.
- Tóm tắt prompt/công việc thực tế: chủ dự án báo Client không fetch được dữ liệu. AI kiểm tra port, cấu hình `ApiBaseUrl`, CORS và launch profile; xác định lần chạy demo trước đã dùng profile HTTP mặc định trong khi Client và CORS được cấu hình cho HTTPS. AI dừng hai host HTTP và khởi động lại API/Client bằng launch profile `https` như README quy định.
- File/module bị tác động: không thay đổi business code; chỉ cập nhật báo cáo này. Runtime API và Client được khởi động lại.
- Kiểm chứng đã thực hiện: API `https://localhost:7247/health` trả HTTP 200; Client `https://localhost:7081` trả HTTP 200; preflight đăng nhập trả HTTP 204 cùng `Access-Control-Allow-Origin: https://localhost:7081` và `Access-Control-Allow-Credentials: true`. Không chạy test theo quyết định hiện tại của chủ dự án.
- Kết quả: Client và API dùng đúng cặp origin Development; lỗi fetch do API HTTPS không chạy đã được xử lý.
- Vấn đề còn lại: tab đang mở tại URL HTTP hoặc giữ asset cũ cần được đóng hoặc hard refresh trước khi dùng URL HTTPS.
- Con người review: CHỜ XÁC NHẬN
- Kinh nghiệm rút ra: khi Client có `ApiBaseUrl` HTTPS và CORS khóa theo origin, cả hai host phải chạy đúng launch profile; HTTP 200 ở port khác không chứng minh browser có thể gọi API được cấu hình.

## Entry 030 - Khôi phục Redis cho kiểm soát phiên Development

- Ngày: 2026-08-28
- Công cụ/model AI: Codex; project không ghi nhận được model backend chính xác.
- Tính năng/công việc: xử lý thông báo `Dịch vụ kiểm soát phiên tạm thời không khả dụng` khi đăng nhập.
- Tóm tắt prompt/công việc thực tế: AI kiểm tra port `6379`, Docker engine, container và vị trí phát sinh lỗi trong `RedisActiveSessionStore`; xác định Docker Desktop đang tắt và container `subank-redis` đã dừng. AI khởi động Docker Desktop rồi khởi động lại đúng container hiện có, không thay Redis bằng bộ nhớ tạm và không bỏ fail-closed session control.
- File/module bị tác động: không thay đổi business code; chỉ cập nhật báo cáo này. Trạng thái runtime của Docker Desktop và container `subank-redis` được khôi phục.
- Kiểm chứng đã thực hiện: `redis-cli ping` trả `PONG`; TCP `localhost:6379` mở; API thực hiện thành công luồng login và ghi nhận session sau khi Redis trở lại. Không chạy test theo quyết định hiện tại của chủ dự án.
- Kết quả: đăng nhập Development hoạt động lại với Redis thật. Container vẫn có restart policy `no` vì đề nghị đổi sang `unless-stopped` không được cấp quyền; khi Docker Desktop bị tắt, cần mở Docker và chạy lại container.
- Vấn đề còn lại: Docker Desktop không phải thành phần được host bởi solution; người demo phải giữ Docker Desktop và `subank-redis` ở trạng thái running.
- Con người review: CHỜ XÁC NHẬN
- Kinh nghiệm rút ra: lỗi dependency phải được xác minh từ port và service thật trước khi sửa code; với kiểm soát phiên bảo mật, fail-closed khi Redis mất kết nối an toàn hơn âm thầm chuyển sang memory store và làm sai cam kết một active session.

## Entry 031 - Ngăn ForceLogout của phiên cũ kết thúc nhầm phiên mới

- Ngày: 2026-08-28
- Công cụ/model AI: Codex; project không ghi nhận được model backend chính xác.
- Tính năng/công việc: sửa thông báo phiên bị thay thế xuất hiện ngay sau khi người dùng vừa đăng nhập lại thành công.
- Tóm tắt prompt/công việc thực tế: sau khi Redis được khôi phục, chủ dự án thấy thông báo tài khoản đăng nhập ở nơi khác dù mới nhập thông tin đăng nhập. AI đối chiếu log và luồng `ReplaceAsync`/SignalR, xác định phiên kiểm tra trước đó đã bị thay thế và `RealtimeService` vẫn giữ thông báo/kết nối cũ. Client được sửa để dừng hub cũ trước login, tuần tự hóa thao tác đồng bộ, gắn mỗi hub với access token lúc tạo và bỏ qua `ForceLogout` đến trễ nếu token đó không còn là phiên hiện tại.
- File/module bị tác động: `RealtimeService.cs`, `Login.razor` và báo cáo này.
- Kiểm chứng đã thực hiện: build riêng Client thành công với 0 warning, 0 error; Client HTTPS được restart. Không chạy test theo quyết định hiện tại của chủ dự án.
- Kết quả: login mới xóa thông báo cũ và tạo kết nối realtime mới; event của kết nối cũ không thể gọi `EndFromServer` lên phiên mới.
- Vấn đề còn lại: chưa thực hiện kiểm thử tự động hai browser context theo yêu cầu hoãn test; cần bổ sung khi bước vào giai đoạn kiểm thử cuối dự án.
- Con người review: CHỜ XÁC NHẬN
- Kinh nghiệm rút ra: sự kiện realtime bất đồng bộ phải mang hoặc capture định danh phiên và kiểm tra lại trước khi thay đổi authentication state; chỉ biết user ID là không đủ khi nhiều phiên nối tiếp nhau trên cùng một SPA.

## Entry 032 - Chặn Unauthorized khi reload Client

- Ngày: 2026-08-28
- Công cụ/model AI: Codex; project không ghi nhận được model backend chính xác.
- Tính năng/công việc: sửa race condition khôi phục phiên khiến trang Customer hiện `Unauthorized` và nút `Thử lại` sau khi reload.
- Tóm tắt prompt/công việc thực tế: chủ dự án báo reload trang đã đăng nhập làm lộ lỗi `Unauthorized · Thử lại`. AI kiểm tra `App.razor`, `ApiSession` và lifecycle của trang Accounts; xác định Router dựng trang nghiệp vụ và gọi API trước khi `/api/auth/refresh` hoàn tất. App được thêm bootstrap gate để chờ restore, route bảo vệ chuyển về login nếu không có phiên, và API `401` giữa phiên được xử lý tập trung thay vì hiển thị như lỗi tải dữ liệu thông thường.
- File/module bị tác động: `App.razor`, `ApiSession.cs`, component `RedirectToLogin.razor`, global CSS và báo cáo này.
- Kiểm chứng đã thực hiện: build Client thành công với 0 warning, 0 error; Client và API HTTPS được restart, cả hai host trả HTTP 200. Không chạy test theo quyết định hiện tại của chủ dự án.
- Kết quả: trong lúc reload, UI hiển thị `Đang khôi phục phiên...` và chưa khởi tạo trang nghiệp vụ. Refresh thành công mới render route; refresh thất bại hoặc API trả `401` sẽ chuyển về login, còn nút `Thử lại` chỉ phục vụ lỗi tải dữ liệu không phải lỗi authentication.
- Vấn đề còn lại: chưa chạy browser automation cho reload do chủ dự án hoãn test đến giai đoạn cuối; cần xác nhận trực quan bằng hard refresh trên browser đang giữ refresh cookie.
- Con người review: CHỜ XÁC NHẬN
- Kinh nghiệm rút ra: ứng dụng SPA dùng access token trong memory phải có authentication bootstrap gate; việc API bảo vệ đúng bằng `401` không đủ nếu UI vẫn cho component nghiệp vụ chạy trước khi trạng thái phiên được khôi phục.

## Entry 033 - Tổng hợp nhật ký lỗi và phát hiện kỹ thuật

- Ngày: 2026-08-29
- Công cụ/model AI: Codex; project không ghi nhận được model backend chính xác.
- Tính năng/công việc: tổng hợp lịch sử lỗi, sự cố môi trường, rủi ro thiết kế và kết quả code review mà không sửa thêm business code.
- Tóm tắt prompt/công việc thực tế: chủ dự án yêu cầu ghi lại toàn bộ lỗi có bằng chứng từ lúc bắt đầu xây dựng, nêu cách sửa đối với mục đã xử lý và giữ nguyên R01–R21 cho con người quyết định. AI đối chiếu Git history, source hiện tại và Entry 001–032, phân biệt lỗi thật, sự cố chỉ được restart, rủi ro được phát hiện sớm và thay đổi yêu cầu không phải bug.
- File/module bị tác động: tạo `docs/Issue-Register.md`; thêm liên kết trong `README.md`; cập nhật báo cáo này. Ba file code của R13 đã thay đổi từ yêu cầu trước và không bị thay đổi thêm trong công việc tài liệu này.
- Kiểm chứng đã thực hiện: kiểm tra đủ R01–R21 trên source; đối chiếu commit và AI Usage entry; kiểm tra Markdown, Git diff và trạng thái working tree. Không chạy test và không khởi động demo vì task chỉ thay đổi tài liệu.
- Kết quả: Issue Register ghi rõ H01–H16, R01–R21, các phát hiện QR đang hoãn và các mục bổ sung. R13 được ghi đúng là đã sửa trong working tree, build Release thành công nhưng chưa test/commit; các R khác chưa sửa. Không có đề xuất nào được viết như thể đã triển khai.
- Vấn đề còn lại: Git và report không thể phục dựng những compiler error tạm thời chưa từng được log. Chủ dự án cần duyệt mức độ và quyết định issue nào được sửa tiếp.
- Con người review: CHỜ XÁC NHẬN
- Kinh nghiệm rút ra: issue register phải tách trạng thái sửa khỏi bằng chứng kiểm chứng; restart dịch vụ không được gọi là source-code fix; quyết định UX/phạm vi không nên bị ghi nhầm thành bug.

## Entry 034 - Sửa idempotency phía Client cho retry giao dịch

- Ngày: 2026-08-29
- Công cụ/model AI: Codex; project không ghi nhận được model backend chính xác.
- Tính năng/công việc: sửa riêng R01 và dừng để chủ dự án review trước khi xử lý lỗi tiếp theo.
- Tóm tắt prompt/công việc thực tế: chủ dự án yêu cầu bắt đầu sửa danh sách lỗi nhưng sau đó thu hẹp phạm vi, chỉ sửa R01 và không đụng QR. AI xác nhận Client đang sinh key mới trong mỗi lần gọi transfer/deposit, chuyển quyền sở hữu key về page và giữ key theo một logical transaction intent qua các lần retry.
- File/module bị tác động: `ApiSession.cs`, `Transfer.razor`, `StaffDeposit.razor`, helper mới `IdempotencyIntentTracker.cs`, `Issue-Register.md` và báo cáo này.
- Kiểm chứng đã thực hiện: rà soát toàn bộ call site của `TransferAsync`/`DepositAsync`, chạy `git diff --check` và build Release toàn solution thành công với 0 warning, 0 error. Không chạy test theo quyết định hiện tại của chủ dự án.
- Kết quả: retry cùng source/destination/account, amount và description tái sử dụng cùng `Idempotency-Key`; đổi payload tạo key mới; response thành công mới kết thúc intent. Transaction password không tham gia định danh intent vì không phải nội dung giao dịch và backend không đưa nó vào request hash.
- Vấn đề còn lại: chưa mô phỏng tình huống server commit nhưng response bị ngắt bằng integration/browser test; R15 về hai request đồng thời cùng key ở backend là lỗi riêng và chưa được sửa; R02–R12, R14–R21 vẫn giữ nguyên.
- Con người review: CHỜ XÁC NHẬN
- Kinh nghiệm rút ra: idempotency key phải sống lâu bằng ý định nghiệp vụ, không phải lâu bằng một HTTP request; transport layer không thể tự quyết định khi nào người dùng đang retry cùng một giao dịch.

## Entry 035 - Khép kín idempotency đồng thời ở Backend

- Ngày: 2026-08-29
- Công cụ/model AI: Codex; project không ghi nhận được model backend chính xác.
- Tính năng/công việc: ưu tiên sửa R15, một lỗi concurrency khó, sau khi R01 phía Client được duyệt.
- Tóm tắt prompt/công việc thực tế: chủ dự án yêu cầu tiếp tục sửa lỗi khó trước và chưa làm lỗi dễ. AI chọn R15 vì đây là phần Backend phụ thuộc trực tiếp vào R01: hai request cùng actor và key có thể cùng vượt pre-check, request thua nhận concurrency/unique conflict dù winner đã commit. AI rà soát độc lập cả transfer và cash deposit trước khi sửa.
- File/module bị tác động: `BankingService.cs`, `StaffService.cs`, helper mới `IdempotencyReplay.cs`, `Issue-Register.md` và báo cáo này.
- Kiểm chứng đã thực hiện: kiểm tra unique index `(CreatedByUserId, IdempotencyKey)`, `RowVersion`, SQL transaction, audit và realtime notification; chạy `git diff --check`; build Release toàn solution thành công với 0 warning, 0 error. Không chạy hoặc thêm test theo quyết định hiện tại của chủ dự án.
- Kết quả: request thua rollback và dispose transaction, xóa EF ChangeTracker rồi query lại winner. Cùng type/hash trả response replay thành công; hash/type khác vẫn trả conflict; conflict không liên quan idempotency giữ lỗi số dư/dữ liệu. Chỉ winner ghi audit và phát realtime. Decimal trong request hash được format bằng invariant culture.
- Vấn đề còn lại: chưa chạy concurrent integration test; thay thuật toán hash có rủi ro tương thích với key đang sống qua thời điểm deploy, nhưng key Client hiện chỉ nằm trong memory và demo không có rolling deployment. Nếu triển khai production thật nên version hóa hash hoặc lưu canonical payload rõ ràng.
- Con người review: CHỜ XÁC NHẬN
- Kinh nghiệm rút ra: unique index chỉ ngăn ghi trùng, chưa tự biến request thua thành replay đúng; phải kết thúc transaction lỗi, xóa tracking state rồi đọc kết quả winner bằng đúng actor, key, type và payload hash.

## Entry 036 - Làm mới token an toàn và nhất quán lifetime phiên

- Ngày: 2026-08-29
- Công cụ/model AI: Codex; project không ghi nhận được model backend chính xác.
- Tính năng/công việc: sửa cụm lỗi khó R06 + R16 về access-token refresh, refresh-token rotation đồng thời và Redis TTL.
- Tóm tắt prompt/công việc thực tế: chủ dự án yêu cầu tiếp tục ưu tiên lỗi khó và chưa làm lỗi dễ. AI rà soát Client, SQL token family, `UserSession`, Redis active-session pointer, CORS và realtime interaction; chốt logical session có thời hạn tuyệt đối thay vì sliding vô hạn, serialize refresh trong một WASM tab và dùng atomic conditional update ở Backend để phân xử hai request rotate cùng token.
- File/module bị tác động: `ApiSession.cs`; `AuthService.cs`, `JwtOptions.cs`; `IActiveSessionStore.cs`, `RedisActiveSessionStore.cs`; `ActiveSessionMiddleware.cs`, `Program.cs`, hai appsettings; cập nhật fake store để solution compile; `Security-Design.md`, `PROJECT-BLUEPRINT.md`, `Database-Design.md`, `Issue-Register.md` và báo cáo này.
- Kiểm chứng đã thực hiện: build Release toàn solution thành công với 0 warning, 0 error; `git diff --check`; rà lại rằng request retry luôn được tạo mới và transfer/deposit giữ nguyên idempotency key. Không chạy hoặc thêm test theo quyết định hiện tại của chủ dự án.
- Kết quả: Client refresh trước expiry một phút, chỉ một refresh chạy trong một tab và protected REST request ngoài QR retry tối đa một lần sau Bearer `401`. Session generation ngăn refresh cũ hồi sinh phiên đã logout/ForceLogout. Backend giữ mốc hết hạn tuyệt đối ban đầu, conditional-renew Redis TTL, dùng `ExecuteUpdateAsync` để một request claim rotation, trả `409` cho concurrent loser trong grace 30 giây và chỉ coi reuse ngoài grace là sự cố bảo mật.
- Vấn đề còn lại: chưa kiểm chứng bằng browser nhiều tab, token expiry thật, Redis TTL và SQL deadlock; bootstrap vẫn chưa phân biệt đầy đủ `401` với `503` vì đó là R19; SignalR reconnect vẫn dùng token capture và retry chưa hoàn chỉnh vì thuộc R07/R08; các method QR không được chuyển sang pipeline refresh theo yêu cầu hoãn QR.
- Con người review: CHỜ XÁC NHẬN
- Kinh nghiệm rút ra: rotation phải được thiết kế như một state machine đồng thời giữa cookie, SQL và Redis; semaphore phía Client chỉ giải quyết một tab, còn Backend vẫn cần atomic claim và lifetime chung làm nguồn sự thật.

## Entry 037 - Khôi phục realtime và cô lập active session

- Ngày: 2026-08-29
- Công cụ/model AI: Codex; project không ghi nhận được model backend chính xác.
- Tính năng/công việc: sửa cụm lỗi khó R07 + R08 về vòng đời SignalR và ranh giới active session.
- Tóm tắt prompt/công việc thực tế: sau khi được giải thích chi tiết hai lỗi, chủ dự án yêu cầu tiếp tục sửa. AI đối chiếu Client reconnect, access-token refresh, Redis active-session pointer, Hub group và post-commit notifier; giữ SignalR là best-effort nhưng loại bỏ việc broadcast kéo dài tới mọi connection của cùng user.
- File/module bị tác động: `ApiSession.cs`, `RealtimeService.cs`; `IActiveSessionStore.cs`, `RedisActiveSessionStore.cs`; `BankingHub.cs`, `SignalRRealtimeNotifier.cs`, `Program.cs`; fake active-session store để solution compile; `Architecture.md`, `Security-Design.md`, `PROJECT-BLUEPRINT.md`, `Issue-Register.md` và báo cáo này.
- Kiểm chứng đã thực hiện: chạy `git diff --check`; build Release toàn solution thành công với 0 warning, 0 error. Không chạy hoặc thêm test theo quyết định hiện tại của chủ dự án.
- Kết quả: SignalR lấy access token hiện hành, phân biệt logical session với token rotation, bỏ callback connection cũ và retry initial/`Closed` failure theo backoff tối đa 30 giây cho tới khi logout. Server bỏ user group, xác minh `sid` với Redis khi Hub kết nối, resolve active `sid` khi phát event ngân hàng và đóng Hub khi JWT hết hạn. Redis lỗi làm bỏ qua event best-effort thay vì broadcast hoặc làm giao dịch đã commit báo thất bại.
- Vấn đề còn lại: chưa browser-test mất mạng dài, token expiry, nhiều tab hoặc session replacement với Redis/SignalR thật. Một race rất hẹp vẫn tồn tại giữa lúc notifier đọc active `sid` và lúc login mới thay pointer vì Redis và SignalR không có transaction chung; REST middleware vẫn là lớp có thẩm quyền. Phần QR không thay đổi theo quyết định hoãn.
- Con người review: CHỜ XÁC NHẬN
- Kinh nghiệm rút ra: automatic reconnect không xử lý initial-start failure và không retry mãi sau `Closed`; đồng thời user group không phù hợp với hệ thống chỉ cho một active session. Connection lifecycle phải gắn với logical-session generation, còn event nhạy cảm phải route theo active `sid`.

## Entry 038 - Thu hồi token family khi logout

- Ngày: 2026-08-29
- Công cụ/model AI: Codex; project không ghi nhận được model backend chính xác.
- Tính năng/công việc: sửa lỗi khó R02 về logout bằng refresh cookie đã rotate và race logout/refresh.
- Tóm tắt prompt/công việc thực tế: chủ dự án yêu cầu AI tiếp tục sửa các lỗi khó, còn lỗi nghiêm trọng nhưng dễ để lại làm bài thực hành. AI chọn R02 vì bỏ early-return đơn thuần vẫn có thể lọt replacement token được tạo đồng thời; luồng được thiết kế lại quanh logical-session aggregate lock.
- File/module bị tác động: `AuthService.cs`; `Security-Design.md`, `PROJECT-BLUEPRINT.md`, `Database-Design.md`, `Issue-Register.md` và báo cáo này. Không đổi API contract, schema, package, Client hoặc phần QR.
- Kiểm chứng đã thực hiện: chạy `git diff --check`; build Release toàn solution thành công với 0 warning, 0 error. Không chạy hoặc thêm test theo quyết định hiện tại của chủ dự án.
- Kết quả: refresh và logout lấy cùng SQL Server `UPDLOCK` trên `UserSession` trong transaction `ReadCommitted`. Logout chấp nhận token cũ/đã revoke/hết hạn làm revocation handle, cập nhật mọi active token cùng `(UserId, SessionId)`, đánh dấu history, compare-delete Redis trước SQL commit và retry một lần khi deadlock. Unknown token vẫn no-op để không lộ trạng thái.
- Vấn đề còn lại: chưa chạy stale-cookie, repeated logout, concurrent refresh/logout và old-session/new-session test. Redis và SQL không có distributed transaction; nếu commit SQL lỗi sau Redis delete, hệ thống chủ động fail-closed và login mới là đường phục hồi. X06 ghi nhận Client chưa báo đúng khi server logout lỗi; X07 ghi nhận race nhỏ ở audit reason khi login replacement chạy đồng thời. Lỗi R03–R05, R09, R12, R14 và R18 được giữ lại để chủ dự án thực hành; R17 là lỗi khó kế tiếp.
- Con người review: CHỜ XÁC NHẬN
- Kinh nghiệm rút ra: refresh token rotation biến một token thành chuỗi, nhưng `SessionId` mới là ranh giới phiên. Logout phải nhắm vào logical session và dùng cùng khóa với refresh; thu hồi riêng cookie token không đủ bảo đảm.

## Entry 039 - Đồng nhất snapshot sao kê

- Ngày: 2026-08-29
- Công cụ/model AI: Codex; project không ghi nhận được model backend chính xác.
- Tính năng/công việc: sửa lỗi khó R17 về số dư đầu/cuối kỳ và danh sách giao dịch có thể được ghép từ nhiều trạng thái database khác nhau.
- Tóm tắt prompt/công việc thực tế: chủ dự án yêu cầu tiếp tục sửa lỗi khó, chưa làm các lỗi dễ và chưa đụng QR. AI đối chiếu công thức opening/closing, transaction boundary của transfer/cash deposit, SQL Server isolation và rủi ro deadlock; chọn cửa sổ đọc `RepeatableRead` thay vì yêu cầu bật `Snapshot` ở runtime.
- File/module bị tác động: `StatementService.cs`; `Architecture.md`, `PROJECT-BLUEPRINT.md`, `Database-Design.md`, `Issue-Register.md` và báo cáo này. Không đổi schema, API contract, package, Client, test hoặc phần QR.
- Kiểm chứng đã thực hiện: build Release toàn solution thành công với 0 warning, 0 error; rà soát diff. Không chạy hoặc thêm test theo quyết định hiện tại của chủ dự án.
- Kết quả: service đọc account trước trong một transaction ngắn, sau đó chốt `asOfUtc` và áp cùng cận trên cho movement dùng tính opening lẫn rows trong kỳ. Thứ tự rows ổn định theo thời gian rồi `Id`; transaction kết thúc trước khi map DTO/render PDF; SQL deadlock 1205 được retry một lần và lỗi lặp lại được map thành conflict an toàn.
- Vấn đề còn lại: chưa chạy test cạnh tranh giữa statement với transfer/deposit trên SQL Server thật. `RepeatableRead` dựa trên invariant mọi money movement phải cập nhật account và ledger trong cùng transaction, đồng thời có thể chặn writer cùng account trong thời gian ngắn. Quy tắc kỳ tương lai chưa thay đổi vì ngoài phạm vi R17.
- Con người review: CHỜ XÁC NHẬN
- Kinh nghiệm rút ra: nhiều query có cùng filter thời gian chưa tạo thành một snapshot. Muốn báo cáo tài chính nhất quán phải chốt cả transaction boundary, thứ tự lấy mốc thời gian và invariant của mọi writer; phần render file không được kéo dài thời gian giữ khóa database.

## Entry 040 - Hoàn thiện Application Log

- Ngày: 2026-08-30
- Công cụ/model AI: Codex; project không ghi nhận được model backend chính xác.
- Tính năng/công việc: hoàn thiện structured Application Log cho ASP.NET Core API, request logging an toàn, rolling file và correlation với SQL Audit Log.
- Tóm tắt prompt/công việc thực tế: chủ dự án hỏi hệ thống đã có Application Log chưa, hỏi khả năng ghi file/backup rồi yêu cầu hoàn thiện. AI audit toàn bộ `ILogger`, middleware, ProblemDetails, cấu hình, `AuditLog` và môi trường chạy; phân biệt technical log với audit/ledger và không tuyên bố rolling file là backup.
- File/module bị tác động: Application correlation abstraction; API Serilog configuration, logging options, correlation/request middleware, exception handler và appsettings; Infrastructure EF audit interceptor/DI; README, Architecture, Security Design, Blueprint, Database Design, Issue Register, tài liệu Application Logging và báo cáo này. Không thay đổi schema, API nghiệp vụ, Client, test hoặc phần QR.
- Package thêm: `Serilog.AspNetCore` 10.0.0, `Serilog.Formatting.Compact` 3.0.0 và `Serilog.Sinks.File` 7.0.0 trong project API.
- Kiểm chứng đã thực hiện: restore thành công; build toàn solution 0 warning/0 error; khởi động API HTTPS/HTTP; xác nhận file JSON Lines được tạo; xác nhận response header và ProblemDetails dùng correlation ID; gửi request có route value/query/body giả rồi kiểm tra request completion event chỉ còn route template. Không chạy hoặc thêm test theo quyết định hiện tại của chủ dự án.
- Kết quả: console nhận structured log ở mọi môi trường; Development ghi file roll theo ngày/dung lượng với giới hạn 10 MiB, tối đa 31 file và 14 ngày; request `4xx/5xx` được phân mức; `/health` thành công ở Debug; top-level property nhạy cảm bị loại trước sink. Correlation ID được validate, đưa vào scope/response và tự điền cho audit phát sinh trong HTTP request mà không cần migration.
- Phát hiện trong lúc triển khai: bản đầu dùng sai hằng `StatusCodes.StatusServiceUnavailable` làm build lỗi và đã đổi sang `Status503ServiceUnavailable`. Kiểm tra file thật phát hiện scope ASP.NET Core tự thêm raw `RequestPath` chứa số tài khoản dù message dùng route template; đã bổ sung enricher loại thuộc tính này và kiểm tra lại bằng một route/account/query giả. Exception-handled request từng mất endpoint thành `<unmapped>`; route template sau đó được snapshot trước khi gọi pipeline. Exception handler cũng xóa response header đã gắn sớm; middleware hiện gắn lại `X-Correlation-ID` bằng `OnStarting` và đã kiểm tra header khớp ProblemDetails.
- Vấn đề còn lại: Redis local không chạy nên login kiểm tra trả `503`; technical log/ProblemDetails của failure path đã được kiểm chứng, nhưng chưa thực hiện một successful login để đọc lại `AuditLog.CorrelationId` qua Admin API. Rolling file không phải backup. Repo chưa có lịch backup SQL, off-site copy, restore script hoặc restore drill; được ghi thành X08 trong Issue Register.
- Con người review: CHỜ XÁC NHẬN
- Kinh nghiệm rút ra: safe message template chưa đủ nếu logging scope/framework tự enrich raw request data; phải nhìn output sink thật. Correlation là chìa khóa nối log, không phải credential. Retention chỉ giới hạn file local, còn backup phải có bản sao độc lập và bằng chứng restore.

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

## Entry 041 - Hoàn tất lượt sửa lỗi non-QR và kiểm chứng artifact

- Ngày: 2026-08-31
- Công cụ/model AI: Codex; project không ghi nhận được model backend chính xác.
- Tính năng/công việc: sửa các phát hiện còn lại R03–R05, R09–R12, R14, R18–R20 và X01–X08 trong phạm vi có thể xử lý tại repository; giữ QR và giai đoạn test đúng quyết định của chủ dự án.
- Tóm tắt prompt/công việc thực tế: chủ dự án yêu cầu tiếp tục và sau đó sửa toàn bộ lỗi. AI đối chiếu Issue Register với source hiện hành, chia audit cho các luồng auth/client và deployment, sửa theo dependency, build/publish rồi chạy chính artifact cùng origin. Một lượt review cuối phát hiện path publish chỉ an toàn trên Windows và Client logout xóa session dù response có thể chưa vào controller; cả hai được sửa trước khi bàn giao.
- File/module bị tác động: auth/session/logout; Teller deposit và transfer; Client router/bootstrap/form/sao kê; database initializer và startup validation; health check; API static hosting/publish target; deployment security; line-ending policy; README, Architecture/Blueprint/Security/Database Design, Deployment, Backup-Restore Runbook và Issue Register. QR không bị thay đổi.
- Package thêm trong toàn working tree: `Serilog.AspNetCore` 10.0.0, `Serilog.Formatting.Compact` 3.0.0 và `Serilog.Sinks.File` 7.0.0 thuộc Entry 040; lượt sửa lỗi này không thêm package khác.
- Kiểm chứng đã thực hiện: build Release toàn solution 0 warning/0 error; format verification thành công; publish API tự restore/publish Client thành công. Artifact thực trả 200 cho `/`, `/accounts` và framework JS; API route giả trả 404 ProblemDetails; liveness 200; readiness 503 khi SQL/Redis không sẵn sàng; logout trả cookie hết hạn và marker xác nhận. Không chạy hoặc thêm test theo quyết định hiện tại.
- Kết quả: các lỗi non-QR có thể đóng bằng code/tài liệu trong repository đã được xử lý trong working tree. Runtime Production không tự migration/seed; demo seed có allow-list; Client phân biệt no-session với outage; role route deny-by-default; health phân liveness/readiness; artifact API phục vụ Client cùng origin.
- Vấn đề còn lại: R21 chờ giai đoạn test; X02 cần hostname/proxy/TLS thật; X08 cần backup/PITR/alert và restore drill thật. R10 không thể tái tạo dữ liệu đã mất mà chỉ có thể phục hồi từ backup. QR-01–QR-04 tiếp tục tạm hoãn. Các thay đổi chưa commit/push.
- Con người review: CHỜ XÁC NHẬN
- Kinh nghiệm rút ra: build thành công trên Windows chưa chứng minh target MSBuild chạy trên Linux; path và restore graph phải được thiết kế đa nền tảng. Với logout, có HTTP response chưa có nghĩa request đã đi qua controller; Client chỉ được xóa state khi server trả bằng chứng theo contract.

## Entry 042 - Hardening luồng authentication/session nhiều tab

- Ngày: 2026-09-01
- Công cụ/model AI: Codex; project không ghi nhận được model backend chính xác.
- Tính năng/công việc: rà soát và khép kín auth-cookie coordination, per-tab session binding, logout confirmation/fallback, stale async response và Customer absolute session; không thay đổi QR.
- Tóm tắt prompt/công việc thực tế: chủ dự án yêu cầu tiếp tục sửa lỗi. AI audit client/server contract theo các race login/refresh/logout giữa nhiều tab; thay lease TTL bằng Web Lock giữ xuyên `fetch` và response body; thêm compensation khi browser đã nhận cookie nhưng response login không hợp lệ; khóa stale `ForceLogout`/request bằng generation; sau đó publish và smoke-test artifact thực.
- Quyết định kỹ thuật: access token vẫn chỉ ở .NET/WASM memory; refresh token vẫn chỉ ở HttpOnly `Secure` cookie. `sessionStorage` chỉ giữ SID theo tab, `localStorage` chỉ giữ logout intent không phải credential. Không dùng `history.state`/JavaScript cookie fallback. Storage/Web Locks không khả dụng hoặc dữ liệu malformed thì fail closed.
- Xử lý response login: JavaScript kiểm tra access token không rỗng, Guid N lowercase, expiry và user/roles ngay trong Web Lock. Body không đọc được/sai schema sẽ block SID và gọi cookie logout bù trừ; không xác định được SID thì block toàn bộ restore. Header/body có hai SID canonical khác nhau thì block toàn bộ restore và thử thu hồi cả hai trước khi nhả Web Lock.
- Xử lý logout: UI/session bị block ngay; cookie logout chỉ thành công khi có `X-SUBank-Session-Revoked: 1`. Thiếu marker/status lỗi/network failure sẽ thử `reject-session` bằng bearer cũ với `credentials: omit`, không mutate cookie của session mới.
- File/module bị tác động trong lượt hardening: `ApiSession.cs`, `RealtimeService.cs`, `index.html`, `AuthController.cs`, `RefreshCookieProtectionMiddleware.cs`, CORS/API startup và tài liệu README/Architecture/Blueprint/Security/Issue Register. Working tree còn chứa các thay đổi non-QR của các entry trước.
- Package: không thêm package mới trong lượt hardening này.
- Kiểm chứng: inline JavaScript parse thành công; format verification thành công; build Release toàn solution 0 warning/0 error; publish artifact Release thành công. Artifact trả 200 cho `/`, `/accounts`, framework JavaScript, Swagger và liveness; readiness 503 đúng vì sandbox không có SQL/Redis. Logout không cookie/bearer trả 204, xóa cookie nhưng không trả sai marker thu hồi. Không chạy hoặc thêm test theo quyết định hiện tại.
- Vấn đề còn lại: R21 chờ browser/multi-tab/network-failure/SQL-Redis concurrency test; X02 cần TLS/proxy/hostname thật; X08 cần backup/PITR và restore drill thật; QR-01–QR-04 tiếp tục tạm hoãn. Web Locks/`sessionStorage`/`localStorage` là dependency của browser trong phạm vi demo; quota logout-intent và outcome ambiguity khi browser abort là residual availability cần bằng chứng môi trường.
- Kinh nghiệm rút ra: serialize đến khi `fetch` trả header là chưa đủ; phải giữ khóa qua cả body/validation vì cookie có thể đã được browser nhận. Browser abort chỉ là tín hiệu Client, không phải rollback phía server; backend vẫn cần state machine và transaction nguyên tử.
- Con người review: CHỜ XÁC NHẬN

## Entry 043 - Sửa lỗi non-QR theo phạm vi rút gọn

- Ngày: 2026-09-02
- Công cụ/model AI: Codex; project không ghi nhận được model backend chính xác.
- Tính năng/công việc: sửa các lỗi ngoài QR ảnh hưởng trực tiếp đến demo, an toàn trạng thái phiên, tính đúng của giao dịch/audit và hiển thị dữ liệu.
- Tóm tắt prompt/công việc thực tế: chủ dự án yêu cầu tiếp tục nhưng không làm quá phức tạp. AI dừng việc mở rộng rà soát, sửa phần code đang dở để build lại, sau đó chỉ xử lý request cancellation, phân loại lỗi SQL, audit/correlation, xóa UI ngay khi logout/hết hạn và một số lỗi giao diện rõ ràng.
- File/module bị tác động chính: `BankingService`, `StaffService`, `IdempotencyReplay`, exception/request logging middleware, `ApiSession`, Login/NavMenu, lịch sử/chi tiết giao dịch, tài khoản, sao kê/PDF và Admin Audit Log.
- Package: không thêm package mới.
- Kiểm chứng: format thành công; build Release toàn solution thành công với 0 warning, 0 error; không chạy test theo quyết định hiện tại.
- Kết quả: solution trở lại trạng thái build xanh; lỗi kỹ thuật không còn bị ghi nhầm là thất bại nghiệp vụ; dữ liệu riêng tư được ẩn ngay khi phiên kết thúc; tiền và thời gian hiển thị nhất quán hơn.
- Vấn đề còn lại: R21 và kiểm thử trình duyệt/môi trường vẫn tạm hoãn; các vấn đề phụ thuộc host/backup giữ nguyên; QR hoàn toàn ngoài phạm vi lượt này. Thay đổi chưa commit/push.
- Con người review: CHỜ XÁC NHẬN
- Kinh nghiệm rút ra: với đồ án demo cần ưu tiên lỗi có tác động thực tế và giữ phạm vi vừa đủ; không biến mọi residual risk thành một hệ thống production phức tạp.

## Entry 044 - Hoàn thiện lịch sử giao dịch và sửa lỗi nhập số tiền

- Ngày: 2026-09-02
- Công cụ/model AI: Codex; project không ghi nhận được model backend chính xác.
- Tính năng/công việc: hoàn thiện lịch sử giao dịch Customer và xử lý fatal error xuất hiện khi nhập số tiền.
- Tóm tắt prompt/công việc thực tế: API log xác nhận lỗi nhập tiền xảy ra trước `POST /api/transfers`. AI thay validation decimal phụ thuộc chuyển đổi runtime bằng kiểm tra trực tiếp, bảo vệ toàn bộ phần chuẩn hóa form; sau đó tái sử dụng API lịch sử hiện có để xây trang chọn tài khoản, tìm kiếm, lọc tiền vào/ra, mở chi tiết và cập nhật sau giao dịch.
- File/module bị tác động chính: `Transfer.razor`, `StaffDeposit.razor`, `Transactions.razor`, `TransactionDetail.razor`, `Accounts.razor`, `NavMenu.razor`, `ApiSession.cs`, `app.css`, Issue Register và báo cáo này.
- Package: không thêm package mới; không thay đổi API/database và không sửa chức năng QR.
- Kiểm chứng: build Release toàn solution thành công với 0 warning, 0 error; Client/API trả HTTP 200; không chạy test theo quyết định hiện tại.
- Kết quả: lịch sử hỗ trợ giao dịch gần đây, chọn tài khoản, tìm theo mã/nội dung/tài khoản, lọc nhanh, mở chi tiết và reload qua response thành công hoặc SignalR; không có khối tổng thu chi.
- Vấn đề còn lại: cần chủ dự án hard-refresh và xác nhận lỗi nhập số tiền không còn trên trình duyệt thật; các phạm vi test/deploy/backup và QR giữ nguyên.
- Con người review: CHỜ XÁC NHẬN
- Kinh nghiệm rút ra: với Blazor WASM, validation tiền nên dùng phép so sánh `decimal` rõ ràng và server vẫn là biên có thẩm quyền; chức năng lịch sử nhỏ có thể lọc client-side trên tập 100 bản ghi mà không cần mở rộng backend.

## Entry 045 - Đổi nhãn và thêm lối tắt Thông tin cá nhân

- Ngày: 2026-09-02
- Công cụ/model AI: Codex; project không ghi nhận được model backend chính xác.
- Tính năng/công việc: đổi thuật ngữ giao diện Customer từ “Thông tin khách hàng” sang “Thông tin cá nhân” và thêm nút truy cập nhanh trên trang chủ.
- Tóm tắt prompt/công việc thực tế: chủ dự án yêu cầu đổi nhãn và thêm nút ở khu vực thao tác nền trắng. AI chỉ sửa text/UI, giữ nguyên API và dữ liệu hồ sơ.
- File/module bị tác động: `NavMenu.razor`, `Profile.razor`, `Accounts.razor`, `app.css` và báo cáo này.
- Package: không thêm package.
- Kiểm chứng: build Client Release thành công 0 warning/0 error; Client và API health cùng trả HTTP 200; không chạy test.
- Kết quả: menu và trang hồ sơ dùng “Thông tin cá nhân”; trang chủ có quick action cùng style vàng nhạt dẫn đến `/profile`.
- Vấn đề còn lại: cần chủ dự án hard-refresh để xác nhận bố cục 5 nút trên trình duyệt thật; thay đổi chưa commit/push.
- Con người review: CHỜ XÁC NHẬN
- Kinh nghiệm rút ra: thay đổi thuật ngữ UI không cần đổi contract/domain nếu ý nghĩa dữ liệu không đổi.

## Entry 046 - Chọn tài khoản khi tạo QR nhận tiền

- Ngày: 2026-09-02
- Công cụ/model AI: Codex; project không ghi nhận được model backend chính xác.
- Tính năng/công việc: bổ sung danh sách chọn tài khoản nhận tiền trong luồng tạo QR của Customer.
- Tóm tắt prompt/công việc thực tế: chủ dự án yêu cầu phần tài khoản trong “Tạo QR nhận tiền” cho phép chọn một trong các tài khoản của Customer. AI rà soát toàn bộ đường đi từ Client đến API và xác nhận contract/backend đã hỗ trợ đúng số tài khoản nên chỉ sửa giao diện.
- File/module bị tác động: `QrTransfer.razor` và báo cáo này.
- Package: không thêm package.
- Kiểm chứng: build Client Release thành công 0 warning/0 error; Client và API health cùng trả HTTP 200; không chạy test theo yêu cầu hiện tại của chủ dự án.
- Kết quả: dropdown chỉ hiển thị tài khoản đang hoạt động, ưu tiên tài khoản truyền từ trang chủ rồi đến tài khoản chính; đổi lựa chọn sẽ xóa QR cũ để tránh nhầm tài khoản nhận.
- Vấn đề còn lại: thay đổi chưa commit/push và cần chủ dự án xác nhận trực quan trên trình duyệt.
- Con người review: CHỜ XÁC NHẬN
- Kinh nghiệm rút ra: khi dữ liệu đích đã nằm trong request và được backend kiểm tra quyền sở hữu, thêm lựa chọn tài khoản chỉ cần quản lý trạng thái UI; không nên mở rộng API không cần thiết.

## Entry 047 - Rút gọn nhãn tài khoản trong dropdown QR

- Ngày: 2026-09-02
- Công cụ/model AI: Codex; project không ghi nhận được model backend chính xác.
- Tính năng/công việc: rút gọn nội dung từng lựa chọn tài khoản khi tạo QR nhận tiền.
- Tóm tắt prompt/công việc thực tế: chủ dự án yêu cầu bỏ chữ “Tài khoản chính” và “Tài khoản phụ”. AI giữ lại số tài khoản, số dư và loại tiền để người dùng vẫn phân biệt được các lựa chọn.
- File/module bị tác động: `QrTransfer.razor` và báo cáo này.
- Package: không thêm package.
- Kiểm chứng: thay đổi chỉ tác động text hiển thị; build Client được thực hiện ngay sau thay đổi; không chạy test theo yêu cầu hiện tại của chủ dự án.
- Kết quả: dropdown QR không còn nhãn tài khoản chính/phụ; logic lựa chọn và kiểm tra tài khoản không đổi.
- Vấn đề còn lại: thay đổi chưa commit/push và cần chủ dự án xác nhận trực quan.
- Con người review: CHỜ XÁC NHẬN
- Kinh nghiệm rút ra: dropdown nhiều tài khoản có thể dùng số tài khoản và số dư làm thông tin phân biệt mà không cần thêm nhãn loại tài khoản.

## Entry 048 - Bỏ tiêu đề phụ tại thanh đầu trang

- Ngày: 2026-09-02
- Công cụ/model AI: Codex; project không ghi nhận được model backend chính xác.
- Tính năng/công việc: xóa hai dòng “SUBank Digital Banking” và “Không gian giao dịch an toàn” khỏi layout sau đăng nhập.
- Tóm tắt prompt/công việc thực tế: chủ dự án yêu cầu bỏ hai dòng chữ trên thanh đầu trang. AI đồng thời xóa CSS không còn sử dụng và giữ trạng thái “Kết nối an toàn” căn phải.
- File/module bị tác động: `MainLayout.razor`, `MainLayout.razor.css` và báo cáo này.
- Package: không thêm package.
- Kiểm chứng: build Client được thực hiện ngay sau thay đổi; không chạy test theo yêu cầu hiện tại của chủ dự án.
- Kết quả: hai dòng chữ đã được loại bỏ, thanh đầu trang không còn khoảng UI rỗng và nhãn kết nối vẫn giữ đúng vị trí.
- Vấn đề còn lại: thay đổi chưa commit/push và cần chủ dự án xác nhận trực quan.
- Con người review: CHỜ XÁC NHẬN
- Kinh nghiệm rút ra: khi xóa markup cần xóa selector CSS chỉ phục vụ markup đó để tránh style mồ côi hoặc vô tình áp dụng lên phần tử kế tiếp.

## Entry 049 - Xóa toàn bộ thanh trạng thái tĩnh

- Ngày: 2026-09-02
- Công cụ/model AI: Codex; project không ghi nhận được model backend chính xác.
- Tính năng/công việc: xóa nhãn “Kết nối an toàn” cùng toàn bộ thanh header và đường phân cách chứa nhãn này.
- Tóm tắt prompt/công việc thực tế: sau khi xác định nhãn kết nối chỉ là text tĩnh và có thể gây hiểu nhầm, chủ dự án yêu cầu bỏ cả nhãn lẫn vùng giao diện chứa nó.
- File/module bị tác động: `MainLayout.razor`, `MainLayout.razor.css` và báo cáo này.
- Package: không thêm package.
- Kiểm chứng: build Client được thực hiện ngay sau thay đổi; không chạy test theo yêu cầu hiện tại của chủ dự án.
- Kết quả: nội dung trang bắt đầu trực tiếp trong workspace, không còn thanh trắng, khoảng trống hoặc đường viền phân cách phía trên.
- Vấn đề còn lại: thay đổi chưa commit/push và cần chủ dự án xác nhận trực quan.
- Con người review: CHỜ XÁC NHẬN
- Kinh nghiệm rút ra: không nên hiển thị trạng thái bảo mật hoặc kết nối nếu giao diện đó không được liên kết với tín hiệu trạng thái thật của hệ thống.

## Entry 050 - Mở rộng trang Quản lý người dùng

- Ngày: 2026-09-02
- Công cụ/model AI: Codex; project không ghi nhận được model backend chính xác.
- Tính năng/công việc: đổi trang “Người dùng bị khóa” thành “Quản lý người dùng” với bộ lọc Tất cả, Hoạt động và Bị khóa.
- Tóm tắt prompt/công việc thực tế: chủ dự án yêu cầu Admin xem được toàn bộ người dùng thay vì chỉ danh sách bị khóa. AI bổ sung contract/API danh sách người dùng, truy vấn role và trạng thái từ ASP.NET Core Identity, rồi lọc ba trạng thái trên Client; endpoint cũ vẫn được giữ để tương thích.
- File/module bị tác động: `StaffContracts.cs`, `IStaffService.cs`, `StaffService.cs`, `AdminController.cs`, `ApiSession.cs`, `StaffUsers.razor`, `NavMenu.razor`, `Architecture.md`, `PROJECT-BLUEPRINT.md` và báo cáo này.
- Package/database: không thêm package, không đổi schema và không cần migration.
- Kiểm chứng: build toàn solution Release thành công 0 warning/0 error; Client và API health chạy được; không chạy test theo yêu cầu hiện tại của chủ dự án.
- Kết quả: Admin có thể xem tên đăng nhập, vai trò, trạng thái, số lần sai và thời điểm khóa; nút mở khóa chỉ xuất hiện với người dùng đang bị khóa.
- Vấn đề còn lại: cần chủ dự án đăng nhập Admin để duyệt giao diện và thao tác mở khóa; thay đổi chưa commit/push.
- Con người review: CHỜ XÁC NHẬN
- Kinh nghiệm rút ra: trạng thái bị khóa phải được tính từ `LockoutEnabled` và `LockoutEnd`, còn `LockedAtUtc` chỉ phục vụ hiển thị/audit; không dùng nó làm nguồn sự thật.

## Entry 051 - Xóa user seed legacy không còn liên kết

- Ngày: 2026-09-02
- Công cụ/model AI: Codex; project không ghi nhận được model backend chính xác.
- Tính năng/công việc: xóa `customer.a` và `customer.b` khỏi database demo, đồng thời làm seed tự dọn legacy user an toàn.
- Tóm tắt prompt/công việc thực tế: chủ dự án nhận thấy hai username cũ xuất hiện ở trạng thái ngừng hoạt động trên trang Quản lý người dùng và yêu cầu xóa. AI truy vấn trực tiếp database trước khi xóa để phân biệt user rác với user có lịch sử.
- Dữ liệu đã xác minh: mỗi legacy user có 0 hồ sơ, 0 tài khoản, 0 giao dịch tạo, 0 audit, 0 refresh token và 0 session; hai Customer thật `0900000001/0900000002` vẫn giữ hồ sơ, bốn tài khoản mỗi người và toàn bộ giao dịch.
- File/module bị tác động: `DatabaseInitializer.cs`, `README.md`, `Database-Design.md`, `Issue-Register.md` và báo cáo này.
- Package/database: không thêm package, không đổi schema hoặc migration; xóa đúng hai hàng Identity legacy cùng role liên quan qua `UserManager`.
- Kiểm chứng: build toàn solution Release thành công 0 warning/0 error; API khởi động và chạy seed thành công; truy vấn lại database chỉ còn hai Customer thật; không chạy test theo yêu cầu hiện tại của chủ dự án.
- Kết quả: trang Quản lý người dùng không còn `customer.a/customer.b`; seed chỉ tự xóa legacy user không có lịch sử nghiệp vụ/audit, còn user có lịch sử được giữ lại và vô hiệu hóa.
- Vấn đề còn lại: Docker daemon/Redis trên máy hiện không chạy nên `/health` trả 503 dù API đã seed và xóa dữ liệu thành công; cần bật lại Redis trước khi đăng nhập demo. Thay đổi chưa commit/push.
- Con người review: CHỜ XÁC NHẬN
- Kinh nghiệm rút ra: không dùng lockout để che dữ liệu seed rác; trước khi xóa user phải kiểm tra toàn bộ quan hệ và giữ nguyên user thật đang sở hữu profile, account và transaction.

## Entry 052 - Khởi động lại môi trường demo

- Ngày: 2026-09-02
- Công cụ/model AI: Codex; project không ghi nhận được model backend chính xác.
- Tính năng/công việc: khởi động dependency và xác nhận môi trường demo sau thay đổi dữ liệu.
- Tóm tắt prompt/công việc thực tế: chủ dự án yêu cầu chạy demo. AI phát hiện Docker Desktop và container Redis chưa hoạt động, khởi động lại Docker/`subank-redis`, nhận diện API và Client cũ vẫn giữ đúng cổng nên không tạo thêm tiến trình trùng.
- File/module bị tác động: chỉ báo cáo này; không sửa source code.
- Kiểm chứng: `https://localhost:7081` trả HTTP 200; `https://localhost:7247/health` trả HTTP 200 với trạng thái `Healthy`.
- Kết quả: Redis, API và Blazor Client đều sẵn sàng cho demo.
- Vấn đề còn lại: chưa commit/push; cần chủ dự án xác nhận trực quan trên trình duyệt.
- Con người review: CHỜ XÁC NHẬN
- Kinh nghiệm rút ra: trước khi chạy thêm tiến trình cần kiểm tra cổng; lỗi bind “address already in use” có thể chỉ ra instance mong muốn đã chạy, không phải lỗi ứng dụng.
