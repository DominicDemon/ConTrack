# Bàn giao dự án ConTrack cho AI Agent

Ngày khảo sát: 23/10/2026. Git root là `ConTrack/`; mã nguồn nằm trong `Source/`. Các đường dẫn bên dưới tính từ Git root.

## 1. Tổng quan dự án

Ứng dụng quản lý hợp đồng theo tenant: quản lý khách hàng, danh mục sản phẩm/dịch vụ, báo giá, mẫu và phiên bản hợp đồng, đàm phán, duyệt nội bộ, chứng từ ký, bàn giao, nghiệm thu, thanh toán và audit. Có giao diện quản trị hệ thống riêng để quản lý tenant. Căn cứ: `Source/Backend/ContractManagement.API/Domains/Controllers/`, `Source/Frontend/app/`, `Source/SystemAdmin/app/`.

| Phân hệ | Công nghệ và phạm vi có bằng chứng |
|---|---|
| Backend | C#/.NET 10, ASP.NET Core Web API, EF Core 10 và SQL Server; SignalR, YARP reverse proxy, OpenXML, Swagger. Căn cứ: `Source/Backend/ContractManagement.API/ContractManagement.API.csproj`, `Program.cs`, project Infrastructure. |
| Frontend | Next.js 16, React 19, TypeScript, Tailwind CSS 4, Radix UI, Axios, Zustand, React Hook Form/Zod; Tiptap cho nội dung hợp đồng và SignalR client. Dev port 3000. Căn cứ: `Source/Frontend/package.json`. |
| SystemAdmin | Ứng dụng Next.js/React/TypeScript riêng, cùng nhóm thư viện UI và Axios; dev port 3001. Màn tenant, dashboard, audit, health và hồ sơ tài khoản. Căn cứ: `Source/SystemAdmin/package.json`, `app/(protected)/`. |
| Flutter/Mobile | Flutter/Dart (SDK constraint `^3.12.2`), `http`, `flutter_dotenv`, `shared_preferences`; thư mục nền tảng Android/iOS/web/desktop hiện diện. Các màn thực tế ở `lib/pages/` gồm login, dashboard và catalog; chưa có bằng chứng ngang bằng toàn bộ nghiệp vụ hợp đồng trên web. Căn cứ: `Source/flutter/pubspec.yaml`, `lib/pages/`, `lib/services/`. |
| Shared | JSON tên hiển thị dùng chung cho Backend và hai web app; không phải thư viện DTO dùng chung. Backend embed vào assembly, web đọc lúc build. Flutter hiện khai báo tên riêng trong `lib/config/app_config.dart`. Căn cứ: `Source/Shared/README.md`, `application-branding.json`. |

Tên repo và tên hiển thị có thể khác nhau. Môi trường triển khai đang hoạt động, phiên bản thực tế đã deploy và mức độ sẵn sàng production: **Chưa xác định từ codebase**.

## 2. Cấu trúc thư mục & vai trò

- `Source/Backend/ContractManagement.API/`: composition root tại `Program.cs`; `Domains/Controllers`, `DTOs`, `Interfaces`, `Services`, `Policies` tổ chức theo nghiệp vụ; `Common`, `Filter`, `Middleware` xử lý response, quyền và hạ tầng HTTP. Nghiệp vụ chính nằm trong API project, không có project Domain/Application độc lập trong cấu trúc đã khảo sát.
- `Source/Backend/ContractManagement.Infrastructure/`: EF persistence `Central` và `Application`, entity/model, migration tách hai nhóm, multi-tenancy, provisioning, seed và SQL stored procedures. `CentralDbContext` quản lý dữ liệu trung tâm; `DbDtctechContext` sử dụng connection string của tenant hiện tại. Căn cứ: `MultiTenancy/DI/DependencyInjection.cs`, `MultiTenancy/Services/TenantDbContextFactory.cs`.
- `Source/Backend/ContractManagement.Tests/`: xUnit, EF InMemory và các nhóm test nghiệp vụ, persistence/integration, test doubles. API project có tham chiếu NUnit nhưng test project riêng dùng xUnit (`ContractManagement.Tests.csproj`).
- `Source/Backend/ContractManagement.RecoveryOps/`: project công cụ vận hành recovery riêng. Chi tiết quy trình vận hành: **Chưa xác định từ codebase** trong phạm vi khảo sát này.
- `Source/Frontend/`, `Source/SystemAdmin/`: App Router trong `app/`; web tenant có nhóm `(protected)` và trang public hợp đồng; `services/` bọc API, `lib/` chứa helper/RBAC/client HTTP, `components/` chứa UI. Hai ứng dụng được build riêng, Next config xuất `standalone`.
- `Source/flutter/lib/`: `pages`, `widgets`, `models`, `services`, `config`, `theme`, `utils`; client HTTP riêng của mobile.
- `Source/Shared/`: cấu hình branding được sử dụng lúc build; cần giữ cạnh ba phân hệ Backend/Frontend/SystemAdmin.
- `Source/Artifacts/`: mẫu hợp đồng và SQL seed test. `Sql/README.md` mô tả thứ tự seed và điều kiện migration; seed không thay thế schema/migration.
- `Source/scripts/`: smoke script về recovery và quyền truy cập tài nguyên. `Source/.github/workflows/react-doctor.yml` chứa cấu hình React Doctor; vì nằm dưới `Source/.github`, chưa có bằng chứng workflow này được GitHub tự chạy từ Git root hiện tại.
- `Source/graphify-out/`: graph và báo cáo kiến trúc có sẵn; đã đọc, không chạy lại Graphify. `GRAPH_REPORT.md` ghi ngày 21/10/2026, build từ `12f308d0`, khác HEAD `d45fbc5`; vì vậy chỉ dùng làm chỉ mục và đối chiếu source, không coi mọi cạnh graph là sự thật hiện tại. Báo cáo có cả cạnh suy luận và cảnh báo vòng import giữa các API client hợp đồng.

Hướng dẫn áp dụng: `Source/AGENTS.md` chủ yếu quy định Graphify. Không tìm thấy `AGENTS.md` tại Git root; các AGENTS tìm thấy thêm trong `node_modules/next` thuộc dependency. Theo yêu cầu phiên này chỉ đọc artifact Graphify hiện có, không query/update hoặc sinh lại graph.

## 3. Luồng dữ liệu & nghiệp vụ

### Kết nối và ranh giới tenant

Web gọi API bằng Axios với `withCredentials: true`, base URL từ `NEXT_PUBLIC_API_URL`. API đăng ký YARP để proxy request không khớp controller tới Next.js; development có thể khởi chạy frontend qua `FrontendLauncher`. Căn cứ: `Source/Frontend/lib/axios-interceptor.ts`, `Source/Backend/ContractManagement.API/Program.cs`.

HTTP đi qua session rồi `TenantResolutionMiddleware`; tenant được tra cứu qua central database và đưa vào `ICurrentTenant` theo request. DI tạo `DbDtctechContext` với connection string đã resolve. Middleware có xử lý session/header không khớp và header fallback theo cấu hình; không được xem header do client gửi là quyền truy cập tự thân. Căn cứ: `Middleware/MultiTenancy/TenantResolutionMiddleware.cs`, `Infrastructure/MultiTenancy/DI/DependencyInjection.cs` (đều dưới Backend).

### Auth và quyền

1. Nhân viên gửi tài khoản/mật khẩu tới `/api/auth/login`, kèm `X-Tenant-Code`; controller lấy employee trong DB tenant, kiểm tra password hash và trạng thái, rồi lưu thông tin employee/tenant trong session. `/api/auth/me` trả hồ sơ, quyền và cờ bắt đổi mật khẩu; logout xóa phiên. Căn cứ: `Source/Backend/ContractManagement.API/Domains/Controllers/Authentication/AuthController.cs`, `Source/Frontend/services/auth-api.ts`.
2. Đây là session authentication tùy biến. `SessionAuthorizeAttribute` gọi `EmployeeSessionValidator` để xác minh session và quyền yêu cầu; kiểm tra quyền tài nguyên hợp đồng/file có service riêng. Không diễn giải hệ thống thành JWT auth chỉ vì có `UseAuthorization`. Căn cứ: `Filter/SessionAuthorizeAttribute.cs`, `Common/Security/`, `Domains/Services/Contract/ContractResourceAuthorizationService.cs`.
3. Session server dùng memory cache, idle timeout 30 phút; cookie HttpOnly, SameSite Lax, Secure bắt buộc ở production. Khi mở rộng nhiều instance cần xem lại session store. Đây là giới hạn được nêu ngay trong `Program.cs`.
4. SystemAdmin dùng `/api/system-auth/login|me|logout` và model tài khoản trung tâm riêng. Không dùng luồng login nhân viên tenant để thay thế. Căn cứ: `Source/SystemAdmin/services/auth-api.ts`, `Domains/Controllers/Admin/SystemAuthController.cs` trong API.
5. Khách hàng nhận link public, xin và xác minh OTP, sau đó dùng customer cookie HttpOnly để xem bản chia sẻ/bình luận; đây là phiên riêng với phiên nhân viên. Căn cứ: `Domains/Controllers/Contract/PublicCustomerContractAccessController.cs`, `Domains/Services/Contract/CustomerContractAccessService.cs` trong API và `Source/Frontend/app/(public)/public/contracts/`.
6. Recovery có các service xác minh email/quên mật khẩu, mã hóa, limiter và outbox worker; OTP hỗ trợ provider cấu hình. Không chép giá trị khóa hay cấu hình nhạy cảm khi tiếp tục làm việc. Căn cứ: đăng ký trong `Program.cs`, `Domains/Recovery/`, `Domains/CustomerAccess/`.

Mobile dùng HTTP client riêng, lưu cookie qua SharedPreferences, gắn `X-Tenant-Code` khi login. Endpoint mobile là `/Auth/...`, phụ thuộc base URL cấu hình. Client hiện xóa cookie khi gặp 401 **hoặc 403**, và cả một số lỗi kết nối; cần lưu ý khi so sánh hành vi với web. Căn cứ: `Source/flutter/lib/services/api_client.dart`, `auth_api.dart`, `lib/config/app_config.dart`.

### Vòng đời hợp đồng

- Khách hàng, catalog và báo giá cung cấp dữ liệu đầu vào; hợp đồng có version và snapshot các bên/items/pháp lý/tài chính. Mẫu hợp đồng có version, placeholder, validation và preview DOCX/PDF. Căn cứ: `Source/Backend/ContractManagement.API/Domains/Services/Contract/ContractService*.cs`, `Domains/Services/ContractTemplate/`, model/migration trong Infrastructure.
- Trạng thái chính được định nghĩa là `Draft → Negotiating → PendingApproval → PendingSignature → Signed → Completed`; ngoài ra có `Cancelled`, `Rejected`. Đây là vòng đời khái quát, không phải mọi thao tác đều được phép chuyển thẳng theo mũi tên. Duyệt có quyết định, rút yêu cầu và xử lý hàng loạt; trả về sửa khác với từ chối dứt điểm. Căn cứ: `Common/Enums/ContractStatus.cs`, `Domains/Services/Contract/ContractApprovalService.cs`.
- Chứng từ ký, bàn giao, nghiệm thu và thanh toán có trạng thái/quy tắc riêng. Hoàn tất hợp đồng phải qua readiness/gate; không chỉ cập nhật cột status. `ContractCompletionService` kiểm tra tiến độ và blocker như chưa ký, nghiệm thu/bàn giao chưa đủ, còn chứng từ đang xử lý; đồng thời quản lý ghi nhận/hủy thanh toán và hoàn tất/mở lại milestone. Căn cứ: `Domains/Services/Contract/ContractSigningService.cs`, `ContractHandoverService*.cs`, `ContractAcceptanceService*.cs`, `ContractCompletionService.cs`.
- Notification và thảo luận hợp đồng dùng SignalR; có hub nhân viên và hub khách hàng riêng, cùng publisher/worker và kiểm tra quyền. Audit nghiệp vụ và security audit là các phần riêng. Căn cứ: đăng ký hub/worker trong `Program.cs`, `Domains/Notifications/`, `Domains/Discussions/`, `ContractAuditWriter.cs`.
- File riêng tư được phục vụ qua storage/authorization service. Render PDF mẫu phụ thuộc LibreOffice cài ngoài ứng dụng; thiếu executable có lỗi render rõ ràng. Căn cứ: `Program.cs`, `Domains/Services/ContractTemplate/LibreOfficeContractTemplatePdfRenderer.cs`.

## 4. Quy chuẩn mã nguồn

- C# quan sát thấy PascalCase cho class/method, interface tiền tố `I`, method bất đồng bộ hậu tố `Async`, field riêng tiền tố `_`; controller/service/DTO chia theo nghiệp vụ. Service lớn tách partial file như `ContractService.Appendices.cs`, `ContractHandoverService.Mutations.cs`. Namespace có cả `ContractManagement.API.*` và `ContractManagement.*`; không giả định đã thống nhất toàn repo.
- Các `.csproj` bật nullable và implicit usings; API sinh XML documentation cho Swagger. Dependency được đăng ký tập trung trong `Program.cs` và Infrastructure DI.
- Lỗi được gom ở `Middleware/ExceptionHandlingMiddleware.cs`: business/RBAC có code và HTTP status, không tìm thấy trả 404, concurrency trả 409, PDF renderer có thể trả 503; lỗi chưa biết trả thông báo chung. Response có cả envelope `ApiResponse` lẫn dạng `{ code, message }`; client cần hỗ trợ đúng từng loại.
- TypeScript dùng strict mode, alias `@/*`; file web thường kebab-case, React component PascalCase. Axios interceptor unwrap `{ success, data }`, điều hướng 401 về login và `MustChangePassword` tới đổi mật khẩu. Căn cứ: `Source/Frontend/tsconfig.json`, `lib/axios-interceptor.ts`, `services/`.
- Hai web app dùng ESLint Next core-web-vitals/TypeScript. Frontend hạ một số rule legacy xuống warning và ghi rõ code mới không nên thêm warning; SystemAdmin không có cùng override. Flutter dùng `flutter_lints`. Căn cứ: hai `eslint.config.mjs`, `Source/flutter/analysis_options.yaml`.
- Quy định formatter toàn repo/Prettier bắt buộc: **Chưa xác định từ codebase**. Không tự áp format toàn bộ repo. Giữ kiểu của file đang sửa và không đổi tên cột legacy chỉ vì lỗi chính tả (ví dụ `Stutus` được SQL README ghi rõ).

## 5. Trạng thái & bước tiếp theo

### Trạng thái có thể xác nhận

Các module và test nêu trên đã hiện diện trong source; điều này không chứng minh tất cả tính năng đã được nghiệm thu hoặc deploy. Phiên khảo sát chỉ đọc và tạo tài liệu, không build, chạy test, seed hoặc migration để tránh sinh/sửa file ngoài phạm vi.

Đã chạy `git log --oneline -10`; repo không shallow nhưng chỉ có ba commit:

| Commit | Nội dung |
|---|---|
| `d45fbc5` | Add Source to branch Nhat |
| `0a3511f` | edit README |
| `41cc861` | Initial commit |

Các commit quá tổng quát để suy ra module nào đang làm dở. Không tìm thấy `TODO`, `FIXME`, `NotImplementedException` trong phần source nghiệp vụ đã quét (Backend, hai web app, Flutter `lib`, Shared, scripts, Artifacts; loại dependency/build output). Công việc đang dở được giao cụ thể: **Chưa xác định từ codebase**.

Trước phiên này working tree đã có thay đổi ở bốn file `Source/Artifacts/Sql/seed-rbac-test-employees.sql`, `seed-test-catalog.sql`, `seed-test-customers.sql`, `seed-test-departments.sql`. Không coi chúng là thay đổi của phiên bàn giao; không reset hoặc ghi đè. `Source/Artifacts/Sql/README.md` là điểm đọc đầu tiên nếu tiếp tục seed: provision tenant/migration trước, seed phòng ban → nhân viên → khách hàng → catalog, không seed production và không mặc định mẫu DOCX hiện có đã tương thích marker mới.

### Đề xuất cho phiên tiếp theo (không phải backlog đã được xác nhận)

1. Xác nhận nhiệm vụ cụ thể và review diff SQL đang có trước khi sửa; lịch sử hiện tại không đủ xác định ý định tác giả.
2. Khi được phép chạy kiểm tra có sinh output: dùng test project xUnit cho Backend; `lint`, `typecheck`, `build` theo package scripts của từng web app; `flutter analyze`/test cho mobile. Chạy smoke scripts sau khi đọc điều kiện môi trường. Trạng thái pass/fail hiện tại: **Chưa xác định từ codebase**.
3. Nếu tiếp tục hợp đồng, bắt đầu từ controller tương ứng rồi service/policy và test liên quan; giữ ranh giới tenant, quyền tài nguyên, snapshot/version và completion gate. Không dùng comment enum thay cho logic gate hiện hành.
4. Nếu tiếp tục mobile, đối chiếu phạm vi màn hình, API base path, xử lý 403 và quản lý cookie với web; chưa có căn cứ khẳng định mobile đã bao phủ vòng đời hợp đồng.
5. Trước triển khai, kiểm tra cấu hình qua secret store/environment, SQL Server, storage riêng tư, SMTP/OTP/recovery và LibreOffice. Với nhiều instance, đánh giá session store vì hiện dùng memory. Không ghi giá trị secrets vào tài liệu hay log.
6. Graphify đang khác HEAD; phiên sau cần đánh giá độ mới trước khi dựa vào quan hệ graph. Chỉ cập nhật graph khi phạm vi công việc cho phép thay đổi artifact; phiên bàn giao này không cập nhật.

## 6. Cập nhật bàn giao — Home và mua gói ConTrack

Cập nhật từ trao đổi với người dùng ngày 23/10/2026. Mục này xác định hướng công việc tiếp theo, bổ sung cho hiện trạng ở mục 1–5; không có nghĩa các chức năng bên dưới đã được triển khai hoặc nghiệm thu. Yêu cầu hiện tại là ghi nhận kế hoạch; chưa sửa mã nguồn hoặc chạy SQL.

### 6.1. Mục tiêu và các quyết định

**Người dùng đã xác nhận:** cần trang Home công khai và luồng khách hàng chọn/mua quyền sử dụng ConTrack. Dùng thử không được sử dụng Customer Support; chỉ thuê bao trả phí hợp lệ mới có quyền này. Đây là dịch vụ sau bán hàng của nền tảng ConTrack.

Phân biệt hai nhóm khách hàng và hai luồng tiền:

- **SaaS Customer:** doanh nghiệp mua ConTrack, sở hữu tenant và thuê bao dịch vụ nền tảng.
- **Counterparty:** khách hàng/nhà cung cấp của doanh nghiệp trong tenant, tham gia hợp đồng và truy cập bản chia sẻ qua OTP.
- Catalog và thanh toán hợp đồng hiện có trong tenant không thay thế catalog gói và thanh toán phí ConTrack ở Central DB.

**Đã thảo luận, chưa chốt chính sách cuối:** có trial 7/15/30 ngày và các kỳ trả phí 3/6/12 tháng. Đề xuất của Agent là một gói chức năng, trial mặc định 15 ngày, ba thời hạn mua; tính kỳ trả phí bằng tháng lịch. Nếu các thời hạn có cùng quyền lợi thì không cần coi chúng là ba cấp chức năng khác nhau.

**Chỉ là dữ liệu minh họa:** mockup dùng giá 900.000 / 1.620.000 / 2.880.000 đồng cho 3/6/12 tháng, cùng hạn mức mẫu. Người dùng chưa duyệt giá, mức giảm hoặc hạn mức này. Không đưa chúng thành cấu hình production/seed chính thức khi chưa chốt.

Người dùng muốn mockup đơn giản để xem bố cục, không tự mở rộng thành nhiều màn tương tác khi chỉ được yêu cầu “vẽ mockup”. Sau đó người dùng yêu cầu giữ thiết kế Home/mua gói đã dựng để chỉnh sửa và sử dụng tiếp. Đã lưu bản tham chiếu trong `Source/Artifacts/Mockups/contrack-home-pricing/`: mở `index.html` để xem hai màn, chỉnh `source.html` và xuất lại theo `README.md`. Ưu tiên kế thừa bố cục này khi phát triển, không tự thiết kế lại từ đầu. Đây vẫn là mockup, chưa tích hợp Next.js/API/DB; các giá/hạn mức chưa được duyệt. Bản lưu giữ nguyên mockup ban đầu, chưa thêm nhãn Customer Support theo quyết định mới nên cần bổ sung khi sửa UI.

Tác vụ xuất bản qua Sites là preview riêng: project đã tạo nhưng chưa deploy thành công do bước đồng bộ nguồn bị chặn; không có URL production đã xác nhận. Bản mockup trong repo có thể mở độc lập, không cần Sites. Không tiếp tục publish nếu người dùng chỉ yêu cầu sửa code hoặc thiết kế local.

### 6.2. Những điểm cần chốt trước khi viết phần phụ thuộc

| ID | Quyết định còn mở | Đề xuất để thảo luận, chưa phải yêu cầu đã duyệt |
|---|---|---|
| D01 | Thời hạn và điều kiện trial | 15 ngày, một lần/doanh nghiệp, bắt đầu khi tenant sẵn sàng; xác định cách nhận diện doanh nghiệp |
| D02 | Giá, tiền tệ, thuế, hạn mức, phạm vi tính năng | Chốt tài khoản, hợp đồng, dung lượng, số trang OCR; xác định hạn mức tích lũy hay theo kỳ |
| D03 | Xác nhận thanh toán | Chuyển khoản/QR, nhân viên nền tảng xác nhận; vai trò cụ thể chưa chốt vì report ghi khác nhau giữa SystemAdmin/Sales |
| D04 | Chuyển trial sang trả phí và gia hạn | Giữ tenant/dữ liệu; chốt ngày bắt đầu kỳ trả phí khi mua sớm, mua muộn, provisioning chậm hoặc thất bại |
| D05 | Hết hạn, đình chỉ và quyền support | Chốt chỉ đọc hay chặn nghiệp vụ, thời gian giữ dữ liệu; quyền xem/trả lời ticket cũ khi hết hạn chưa xác định |
| D06 | Tài khoản SaaS và Manager tenant | Chốt liên kết danh tính, email verification, login/recovery và cách chuyển vào tenant; không đồng nhất với Counterparty OTP |
| D07 | Vị trí Home và đăng nhập | Chốt route/domain; `/` của Frontend hiện là trang đăng nhập, nhiều redirect đang trỏ về đó |
| D08 | Database thực tế | SQL người dùng gửi đã chạy hay chỉ là đề xuất? Chưa có câu trả lời; chưa xác minh database đang hoạt động |

Có thể chuẩn bị bố cục và hợp đồng API trong khi chờ các quyết định không liên quan; không tự chốt chính sách thương mại hoặc sửa DB theo giả định.

### 6.3. Phạm vi UI và hai luồng mục tiêu

**Home:** giới thiệu sản phẩm đúng tính năng/scope, liên kết xem gói, dùng thử và đăng nhập. **Màn chọn/mua gói:** hiển thị thời hạn, tổng tiền, giá quy đổi nếu có, quyền lợi/hạn mức; ghi rõ trial không có Customer Support, trả phí có. Giá và quyền lợi lấy từ dữ liệu gói do backend quản lý; backend tính lại tiền khi tạo đơn, không tin số tiền client gửi.

Luồng đề xuất:

1. **Trial:** Home → chọn dùng thử → đăng ký/xác minh doanh nghiệp → cấp tenant và Manager → tenant sẵn sàng → bắt đầu trial → sử dụng nghiệp vụ được phép, không có support.
2. **Mua trực tiếp:** Home → chọn thời hạn → đăng ký/xác minh → tạo đơn lưu gói/giá tại thời điểm mua → hướng dẫn thanh toán → xác nhận tiền → provisioning/kích hoạt → mở quyền sử dụng và support.
3. **Trial lên trả phí:** mua gói và xác nhận tiền trên doanh nghiệp hiện hữu → cập nhật thuê bao/quyền → giữ tenant, tài khoản và dữ liệu; không tạo tenant thứ hai.

Mua gói không hoàn tất chỉ vì có màn bảng giá. Cần có trạng thái chờ xác nhận, provisioning thất bại/thử lại, thành công và quyền truy cập thực tế. Trial là ngoại lệ cần cập nhật trong quy tắc report “thanh toán đủ mới provision tenant”.

### 6.4. Nền tảng có thể tái sử dụng và điểm đọc source

- `Source/Frontend/app/page.tsx`, `lib/axios-interceptor.ts`, các route recovery/protected: kiểm tra ảnh hưởng khi đổi `/` thành Home, gồm logout, session hết hạn, redirect và đường dẫn email. Không thay trang login đơn lẻ rồi để redirect cũ trỏ sai.
- `Source/Frontend/services/`, `lib/`, `components/`: bám cách tổ chức hiện tại; tách API công khai khỏi interceptor ép đăng nhập nhân viên.
- `Source/SystemAdmin/app/(protected)/tenants/`, `services/`: giao diện quản trị tenant hiện có; mở rộng quản lý gói, đơn và xác nhận tiền theo quyền đã chốt.
- `Source/Backend/ContractManagement.API/Domains/Controllers/Admin/TenantsController.cs`: điểm vào quản trị tenant hiện tại.
- `Source/Backend/ContractManagement.Infrastructure/MultiTenancy/Services/TenantProvisioningService.cs`, `EfCoreTenantDatabaseInitializer.cs`: tái sử dụng provisioning; cần thiết kế retry không tạo trùng database/Manager khi dùng trong luồng mua gói.
- `Source/Backend/ContractManagement.Infrastructure/Persistence/Central/CentralDbContext.cs`, `Migrations/Central/`: nơi mở rộng dữ liệu nền tảng. Chưa thấy entity/API SaaS commerce hoàn chỉnh trong lần khảo sát.
- `Source/Backend/ContractManagement.API/Middleware/MultiTenancy/TenantResolutionMiddleware.cs`, `Program.cs`: endpoint danh mục công khai/đăng ký trước tenant phải được định tuyến đúng, không bắt người chưa có tenant gửi tenant context; giữ kiểm tra quyền ở endpoint nghiệp vụ tenant.
- SystemAdmin và nhân viên có luồng đăng nhập/key phiên khác nhau nhưng cùng dùng cấu hình `HttpContext.Session`/cookie `ContractManagement.Session`; public Counterparty có cookie riêng. Không mô tả cả ba là ba session store độc lập.

### 6.5. SQL đã được người dùng cung cấp và vấn đề phải giải quyết

Nguồn là attachment `Pasted text.txt`, bản tự ghi “3.0”, nằm ngoài repo tại `C:/Users/ADMIN/.codex/attachments/d9ddf9dd-8286-4873-81ab-78d158aad029/Pasted text.txt`. Đây không phải bằng chứng schema đã triển khai; đường dẫn attachment chỉ có trên máy của phiên này. Nếu phiên khác không truy cập được, cần lấy lại nguồn trước khi chỉnh schema. Chưa chạy script, chưa sửa script, chưa đối chiếu đầy đủ mọi bảng.

Script có nhóm `CUSTOMER`, `CUSTOMER_BUSINESS`, `PLAN`, `PLAN_VERSION`, `SUBSCRIPTION`, `SAAS_INVOICE`, `SAAS_PAYMENT`, `TICKET_SUPPORT` và các bảng message/assignment. Có thể dùng làm đầu vào thiết kế Central DB, nhưng không được tin nhận định trong comment “tương thích 100% EF Core”.

| Vấn đề đã thấy | Hướng xử lý cần làm |
|---|---|
| Trigger `trg_CheckSubscriptionPaidForTicket` chặn khi bất kỳ subscription nối được không hợp lệ; tenant đã mua có thể vẫn bị chặn bởi trial/lịch sử cũ | Kiểm tra sự tồn tại của quyền trả phí hợp lệ đúng tenant, thời gian hiệu lực, trạng thái thanh toán và quyền support. Không nối quyền bằng `tenant_id OR customer_id` mà bỏ kiểm tra quan hệ sở hữu. Backend phải kiểm tra quyền dù UI đã ẩn nút |
| `PLAN` chứa hạn mức/quyền support có thể sửa trực tiếp; `PLAN_VERSION` chủ yếu chứa giá/thời gian | Version hóa hoặc snapshot quyền lợi đã mua; tránh sửa gói làm thay đổi ngầm quyền của thuê bao cũ |
| Chưa thể hiện rõ trial một lần và kỳ tháng lịch; thời hạn chủ yếu là ngày và chuỗi billing cycle | Chốt mô hình trial/kỳ mua, ngày bắt đầu/kết thúc, các ràng buộc và lịch sử chuyển đổi; không mặc định 3 tháng là 90 ngày |
| Subscription bắt buộc có tenant, invoice có thể chưa có subscription; chưa có liên kết chắc chắn đến gói đặt trước provisioning | Thiết kế bản ghi đơn mua với gói/thời hạn/giá snapshot, liên kết customer → đơn → thanh toán → subscription → tenant; xác định tạo tenant metadata ở bước nào |
| Thanh toán chưa thể hiện rõ actor/thời điểm xác nhận và cơ chế chống tác động lặp | Ghi nhận xác nhận theo quyền, tiền thực nhận, tham chiếu; transaction/concurrency và idempotency cho xác nhận, kích hoạt, provisioning |
| Central/Tenant chỉ được phân phần bằng comment, không có `USE` chuyển DB | Không chạy nguyên file để tạo cả hai nhóm trong một DB. Tách migration đúng context và đối chiếu schema thực tế trước |
| `tbl_Contract` thiếu `CreatedEmployeeId`, `UpdatedEmployeeId`; kiểu `ContractType`, `ContractDirection` là INT trong khi entity dùng byte | Giữ schema tenant theo EF migration hiện tại; không dùng script tổng hợp để thay thế phần đang hoạt động |
| `sp_SyncVerifiedOcrKeysToContract` gán Completed=5, template dự phòng=1, hash toàn số 0 và ngày ký hiện tại khi thiếu dữ liệu | Không dùng procedure này để nhập dữ liệu thật. Thiết kế OCR/import riêng; không kéo vào đợt Home/mua gói |

### 6.6. Checklist triển khai có thể theo dõi

Checklist ban đầu được lập khi chưa triển khai. Cập nhật FE ngày 24/10/2026 ở mục 6.9; S04 mới hoàn thành phần giao diện với dữ liệu minh họa, chưa hoàn thành tích hợp danh mục gói thật. Chỉ đánh dấu hoàn thành khi có bằng chứng; tên endpoint/bảng mới phải chốt trong thiết kế, không coi tên gợi ý trong trao đổi là API đã tồn tại.

- [ ] **S01 — Chốt scope:** giải quyết D01–D08; ghi quyết định, người xác nhận và ngày; đồng bộ các phần report liên quan.
- [ ] **S02 — Chốt mô hình dữ liệu:** nhận schema thực tế, đối chiếu EF; thiết kế catalog/version/kỳ mua, đăng ký doanh nghiệp, đơn hàng, subscription, thanh toán và quyền support; chuẩn bị migration Central có thể review.
- [ ] **S03 — Danh mục gói:** API đọc gói công khai, giá và quyền lợi; quản trị phiên bản gói theo quyền; validation và không làm đổi đơn đã đặt.
- [ ] **S04 — Home và chọn gói:** route công khai, bố cục responsive, tổng tiền/quyền lợi rõ ràng, trạng thái tải/lỗi/rỗng; bảo toàn luồng login hiện có.
- [ ] **S05 — Trial xuyên suốt:** đăng ký/xác minh → provision → đăng nhập sử dụng; không trùng tenant, bắt đầu tính trial đúng lúc, backend từ chối Customer Support.
- [ ] **S06 — Mua trả phí xuyên suốt:** tạo đơn → hướng dẫn thanh toán → xác nhận theo quyền → kích hoạt/provision → sử dụng; xử lý tiền chưa xác nhận, lỗi và retry.
- [ ] **S07 — Chuyển đổi và hết hạn:** trial lên paid giữ dữ liệu, gia hạn đúng ngày, xử lý trạng thái hết hạn theo D05; đồng bộ quyền và tránh dùng cache/session cũ để vượt hạn mức.
- [ ] **S08 — Quyền support:** UI và API cùng kiểm tra entitlement; trial bị từ chối, paid còn hiệu lực được phép; hành vi ticket cũ theo quyết định D05. Xây đầy đủ ticket là hạng mục sau nếu chưa nằm trong đợt này.
- [ ] **S09 — Nghiệm thu:** kiểm thử phù hợp, lưu kết quả và danh sách phần còn thiếu; cập nhật handover/report theo thực tế.

Thứ tự đề xuất: S01–S04 → hoàn thành luồng trial S05 → luồng trả phí S06 → S07–S09. Không mở rộng sang OCR, ký điện tử, mobile toàn bộ hoặc promotion phức tạp chỉ vì có bảng trong SQL.

### 6.7. Điều kiện nghiệm thu và cách cập nhật tiến độ

- Khách chưa đăng nhập xem được Home và các gói; chọn đúng kỳ, tổng tiền và quyền lợi; dữ liệu giá giả không bị trình bày là giá đã duyệt.
- Đăng ký lặp, xác nhận tiền lặp hoặc retry provisioning không tạo trùng doanh nghiệp/tenant/Manager/quyền sử dụng.
- Tenant trial không tạo được ticket kể cả gọi API trực tiếp. Tenant paid hợp lệ có lịch sử trial/thuê bao hết hạn vẫn được hỗ trợ; không lấy quyền từ tenant khác.
- Người dùng không thể tự đổi tenant, plan, số tiền hoặc trạng thái thanh toán trong request để nhận quyền trả phí.
- Thời hạn đúng với tháng lịch, cuối tháng và múi giờ đã chốt; cập nhật giá gói không thay đổi đơn/quyền lợi đã mua.
- Chuyển trial lên paid giữ nguyên hợp đồng, file và tài khoản; hết hạn áp đúng chính sách, không tự xóa dữ liệu.
- Login/recovery/logout và các luồng hợp đồng hiện tại không bị hỏng do đổi Home, route hoặc tenant middleware.
- Kiểm tra backend theo test liên quan trong `Source/Backend/ContractManagement.Tests/`; frontend theo `lint`, `typecheck`, `build`; kiểm tra UI desktop/mobile và migration trên DB kiểm thử khi bắt đầu triển khai. Không ghi “đã pass” nếu chưa chạy.

Mỗi lần hoàn thành một Sxx, ghi ngay bên dưới: **trạng thái (chưa làm/đang làm/chờ quyết định/đã nghiệm thu), file hoặc migration liên quan, kiểm thử và kết quả, việc còn lại**. Kết quả kiểm thử FE với API phiên giả lập được ghi ở mục 6.9; chưa kiểm chứng luồng SaaS xuyên suốt với backend thật.

### 6.8. Tài liệu tham chiếu và cảnh báo về độ nhất quán

Đã nhận `Source/Report-1_Project Introduction (7).docx`, `Source/Report-2_Project Management Plan (8).docx`, `Source/Report-3_Software Requirement Specification (8).docx`, `Source/Report-4_Software Design Specification.docx` và `Source/ConTrack_Report_Alignment_Change_Notes.docx`. Các report còn hoàn thiện, không tự xem mọi chi tiết là yêu cầu đã chốt.

Riêng đợt SaaS cần thống nhất vai trò xác nhận tiền, trial ngoại lệ với provision sau thanh toán, tài khoản SaaS/Manager, chính sách Suspended và quyền support. Giữ yêu cầu mục tiêu nhưng phân biệt Designed/Partial/Implemented/đã kiểm chứng. Các lệch khác về portal Counterparty, ký điện tử, duyệt nhiều cấp, trạng thái hợp đồng và nội dung mẫu TalentHub/Spring Security trong Report 4 vẫn cần xử lý riêng, không lấy đó làm lý do refactor toàn bộ khi đang làm Home/mua gói.

### 6.9. Cập nhật FE Home/Pricing — 24/10/2026

**Phạm vi người dùng duyệt:** làm trước hai màn `/home` và `/pricing`; `/` tiếp tục là đăng nhập. Trial 15 ngày và giá mẫu được dùng để review giao diện, không phải chính sách thương mại chính thức. D07 đã chốt vị trí tạm; D01/D02 còn phải chốt chính sách thật.

**Đã triển khai (S04 một phần):**

- `Source/Frontend/app/(marketing)/`: layout công khai và hai route; `/pricing` dùng Suspense cho phần đọc query. Không nằm trong layout bắt đăng nhập.
- `Source/Frontend/components/marketing/`: header/footer, Home, thẻ gói, quyền lợi, FAQ và tổng tiền. Dùng theme token, Geist, shadcn/ui và ThemeToggle hiện có; responsive, hỗ trợ sáng/tối. Tên hiển thị lấy `APP_NAME` (cấu hình hiện tại là eContract).
- `Source/Frontend/lib/pricing-preview.ts`: dữ liệu minh họa tập trung, trial 15 ngày, 3 tháng 900.000đ, 6 tháng 1.620.000đ, 12 tháng 2.880.000đ; lưu lựa chọn bằng query và chuẩn hóa URL quay lại. Hạn mức minh họa: trial 3 tài khoản/10 hợp đồng/500 MB; paid 10 tài khoản/5 GB. Trial không gồm Customer Support; paid hiển thị support khi còn hiệu lực. Đây chỉ là nội dung UI, chưa phải entitlement được backend thực thi.
- `Source/Frontend/hooks/use-pricing-session.ts` và `services/auth-api.ts`: đọc `/api/auth/me` qua public Axios client để phân biệt đang tải/khách/đã đăng nhập/lỗi; 401 không ép khách rời trang giá; lỗi mạng/server có nút thử lại.
- `Source/Frontend/app/page.tsx`: nhận `returnTo` chỉ cho phép route `/pricing`, giữ kỳ gói sau đăng nhập; vẫn ưu tiên đổi mật khẩu bắt buộc. Đăng nhập thông thường giữ `defaultPage` cũ. Thêm liên kết Home/Pricing ở footer login.
- Người chưa đăng nhập chuyển sang login hiện có; người đã đăng nhập xem thông tin tenant và trạng thái chưa mở thanh toán/trial. Chưa có form đăng ký SaaS, tạo đơn, cổng thanh toán hoặc kích hoạt thuê bao; nút mua chưa thực hiện giao dịch. Mockup HTML gốc vẫn được giữ riêng.

**Kiểm chứng:** `npm run typecheck` đạt; ESLint toàn FE không có lỗi (318 cảnh báo, các file mới không có cảnh báo). `npm run build -- --webpack` đạt và sinh được hai route. Build Turbopack mặc định lỗi tiến trình xử lý CSS/font trong môi trường kiểm thử; chưa sửa cấu hình build. Playwright/Chrome với API auth giả lập đã qua: trial và kỳ 3/6/12, tổng tiền, reload giữ lựa chọn, điều khiển radio bằng bàn phím, đăng nhập quay về gói đã chọn, từ chối URL ngoài, ưu tiên đổi mật khẩu bắt buộc, 401/lỗi/thử lại/loading; desktop 1440px và mobile 390px không tràn ngang, sáng/tối, không có lỗi runtime trình duyệt. Chưa kiểm thử end-to-end với backend/DB thật.

**Tiếp theo:** review giao diện; chốt danh mục/hạn mức/giá thật và mô hình tài khoản SaaS; nối API danh mục (kèm trạng thái rỗng/lỗi), đăng ký/xác minh doanh nghiệp, trial và thanh toán theo S02–S08. Không đánh dấu toàn bộ S04–S09 hoàn thành chỉ vì hai màn FE đã chạy.
