# Chat cộng đồng

## Thành phần và cách chạy

Tích hợp vào Razor/Bootstrap/JS thuần hiện có. `Startup.cs` đăng ký SignalR tại `/community-chat/hub`, API danh tính tại `POST /community-chat/identity`, store Redis và subscriber Redis pub/sub riêng. `_Layout.cshtml` thêm partial `_Chat.cshtml`. Không thay đổi socket, dịch vụ hoặc giao thức kết quả xổ số.

Chạy website như trước bằng `dotnet run`, sau khi cấu hình `ConnectionStrings:Redis` hiện có và các cấu hình website/database sẵn có. Không cần thêm NuGet package hay SQL migration. SignalR server có trong shared ASP.NET Core framework. SignalR JS 6.0.25 được đóng gói tại `wwwroot/lib/signalr/`, tải khi mở chat lần đầu; không phụ thuộc CDN khi sử dụng.

Các file của tính năng:

- `Chat/ChatIdentity.cs`: cookie khách được Data Protection bảo vệ; xác thực, sinh và kiểm tra danh tính.
- `Chat/ChatStore.cs`: Redis, Lua atomic, lịch sử, chống trùng, reactions, báo cáo, khóa và presence.
- `Chat/ChatHub.cs`: các lệnh hub và subscriber phát sự kiện tới các kết nối local.
- `Chat/ChatOriginMiddleware.cs`: kiểm tra same-origin cho HTTP và WebSocket chat.
- `Startup.cs`, `Views/Shared/_Layout.cshtml`: điểm tích hợp.
- `Views/Shared/_Chat.cshtml`, `wwwroot/css/chat.css`, `wwwroot/js/chat.js`: giao diện responsive, focus, bàn phím và trạng thái gửi.
- `tests/ChatIntegration/`: host và kiểm tra trình duyệt độc lập, chỉ sử dụng Redis test.

## Lưu trữ và định danh

Redis tự tạo keys trong namespace `community:{chat}:`; không có bước migration. Tin được lưu trong hash và sorted set theo sequence ID tăng đơn điệu; lấy 50 tin/trang. Giữ tối đa 3 ngày hoặc 2.000 tin gần nhất, chạm giới hạn nào trước thì xóa trước. Reconnect lấy bù từng trang theo ID rồi làm mới reaction/xóa của cửa sổ đang xem. Nếu mất kết nối lâu tới mức tin đã hết hạn 3 ngày hoặc ra khỏi 2.000 tin lưu trữ, không thể khôi phục các tin đã hết thời gian giữ.

Cookie `chat-guest` có HttpOnly, SameSite=Strict, Secure khi HTTPS, hạn 365 ngày. Token không xuất hiện trong payload broadcast hoặc localStorage. Profile có hạn 365 ngày. Khóa idempotency `(identity ID, client message UUID)` giữ 3 ngày; retry trả về cùng tin, không broadcast thêm. Rate limit và mutation đều được Redis xử lý để áp dụng trên toàn bộ instance. Lưu ý giới hạn rate dùng cửa sổ cố định, có thể có burst ở biên cửa sổ.

Website hiện chưa có luồng đăng nhập thực sự (nút đăng nhập là giao diện). Khách được server cấp ID riêng, emoji và nền pastel. Khi ứng dụng có authentication middleware, hub ưu tiên principal đã xác thực và claim `NameIdentifier`; không đọc role/ID từ dữ liệu người dùng gửi. Không cấp `ChatAdmin` cho khách. Việc bổ sung đăng nhập sau này cần gọi `UseAuthentication()` sau routing, trước authorization/endpoints bằng scheme của website.

Chỉ principal đã xác thực có role `ChatAdmin` mới gọi được `Delete(messageId)` hoặc `Ban(userId)`. Không có endpoint nâng quyền. Các thao tác này dùng công cụ quản trị đã xác thực của đơn vị vận hành; không thêm màn hình quản trị công khai. Báo cáo được lưu tại hash `community:{chat}:reports`, field `messageId:reporterId` với thời gian server; quản trị viên có thể xem qua Redis được bảo vệ hoặc tích hợp vào trang quản trị. Báo cáo tự xóa sau 7 ngày; cần có quy trình duyệt định kỳ; báo cáo không tự xóa tin hay tự khóa người gửi.

Tác vụ `Chat/ChatRetentionService.cs` dọn tin và báo cáo khi khởi động, sau đó mỗi phút; đọc lịch sử cũng dọn trước khi trả dữ liệu. Tin hết hạn được dọn khỏi Redis chậm nhất khoảng một phút khi ứng dụng/Redis hoạt động. Khi nâng cấp, các key chống trùng cũ được rút TTL còn tối đa 3 ngày tính từ lần dọn, không kéo dài TTL ngắn hơn; không thay đổi keys của kết quả xổ số. Không cần SQL migration. Chính sách này không phải giới hạn RAM toàn Redis hoặc giới hạn dung lượng file AOF trên đĩa.

## Triển khai một/nhiều instance

Không tự deploy production. Trước khi triển khai cần cấu hình:

1. Tất cả instance dùng **cùng Redis database** và Redis pub/sub; Lua lưu tin/reaction và publish trong cùng thao tác. Subscriber của từng instance broadcast tới SignalR local, vì vậy không cần thêm SignalR Redis backplane thứ hai. Dùng DB Redis riêng cho môi trường test/staging/production để tránh trộn phòng chat.
2. Bật persistence/backup Redis, ACL, xác thực và TLS theo hạ tầng; tránh eviction keys chat (`noeviction` cùng capacity phù hợp). AOF `appendfsync always` nếu cần đảm bảo ack bền vững qua sự cố máy; `everysec` có thể mất khoảng một giây ghi khi Redis bị crash. Redis pub/sub không phải event log, việc lấy bù dựa trên lịch sử đã lưu.
3. `Chat:DataProtectionKeysPath` (biến môi trường `Chat__DataProtectionKeysPath`) trỏ tới thư mục key ring **bền vững, dùng chung giữa các instance**, cấp quyền đọc/ghi cho service account, giữ qua restart/deploy. Application name cố định `XoSoWeb.CommunityChat`. Bảo vệ thư mục keys ở tầng hạ tầng; không commit keys. Thiếu key cũ sẽ buộc khách nhận danh tính mới.
4. Bật WebSocket ở IIS/reverse proxy. Khi chạy nhiều instance, bật sticky sessions cho toàn bộ `/community-chat/hub` (negotiate và transport), kể cả fallback long polling/SSE. Giữ proxy idle timeout đủ cho heartbeat SignalR. HTTPS bắt buộc ở production.
5. Nếu TLS kết thúc ở proxy, cấu hình Forwarded Headers chỉ tin các proxy đã xác định để `Request.Scheme`, host và IP phản ánh request thật. Không tin tùy ý `X-Forwarded-For` từ Internet. Origin phải trùng scheme/host của request website.

Presence dùng kết nối với TTL 75 giây, heartbeat ứng dụng 25 giây, đếm tập danh tính duy nhất qua mọi instance/tab. Khi disconnect sạch thì loại ngay, khi kết nối chết bất ngờ thì loại theo TTL vào heartbeat/thao tác presence tiếp theo.

## Bảo mật và giới hạn

- Payload SignalR đầu vào tối đa 4 KiB, buffer transport 8 KiB trong host chính; tin 1–500 UTF-16 code units, tên 2–40 sau chuẩn hóa Unicode, không HTML/control/zero-width. Danh xưng quản trị bị chặn trong tên. Reaction whitelist 👍 ❤️ 😂 🎉.
- Tin: tối đa 5 thao tác/10 giây/danh tính và 20/IP. Các thao tác khác: 90/phút/danh tính và 300/IP. Gửi sai nội dung/retry cũng tiêu thụ quota để chống spam. IP chỉ dùng phía server.
- Dùng `textContent` để hiển thị nội dung, không `innerHTML`. Tên không làm khóa định danh. Không có số online hay tin giả trong sản phẩm.
- Giữ tối đa 200 tin đã xác nhận và 20 tin chờ/lỗi trong DOM. Khi đang đọc tin cũ và cửa sổ đầy, tin mới được lấy lại khi bấm “Tin nhắn mới ↓”. Có thể tải các trang cũ theo cửa sổ 200 tin. Tin gửi lỗi giữ trong bộ nhớ trang, có nút thử lại/bỏ; không lưu bản nháp khi tải lại trang.
- Ẩn người gửi chỉ lưu ID trong localStorage của thiết bị; nút “Bỏ ẩn” khôi phục. Report gửi server, không tiết lộ người báo cáo cho phòng.
- Thu nhỏ/đóng đều ẩn giao diện và giữ kết nối đã mở để cập nhật badge. Không tự mở hoặc phát âm thanh. Emoji chèn tại con trỏ, desktop Enter gửi/Shift+Enter xuống dòng; Enter IME không gửi. Mobile gửi bằng nút, dùng VisualViewport để tránh bàn phím che composer.

## Kiểm tra

Host test không gọi SQL, upstream xổ số hay Redis đang cấu hình cho website. Nó dùng **Redis local riêng ở port 6389**, ephemeral Data Protection và route `/fixture`. Không triển khai test host lên production.

```powershell
# Terminal 1: Redis test riêng, tắt persistence
redis-server --port 6389 --save '""' --appendonly no

# Terminal 2
dotnet run --project tests/ChatIntegration

# Terminal 3: cần Playwright và Chromium đã cài
# PLAYWRIGHT_MODULE có thể trỏ đến đường dẫn module Playwright hiện có.
node tests/ChatIntegration/chat-ui.cjs

# Kiểm tra thời hạn lưu với Redis test riêng tại port 6389
dotnet run --project tests/ChatIntegration -p:UseAppHost=false -- --retention-check

dotnet build --no-restore
dotnet run --project tests/SeoRegression --no-restore
```

Test fixture seed lịch sử và tin test chỉ tồn tại trong project test, được loại khỏi compile/publish của website chính. Bộ browser test dùng hai context độc lập: realtime tin/reaction bật/tắt, cookie sau reload, đổi tên, số online nhiều tab, HTML, IME, con trỏ emoji, badge/Escape/focus, offline và lấy bù hơn 50 tin, retry UUID, rate limit, từ chối quyền admin, cookie giả và Origin ngoài, giới hạn DOM, giữ scroll, phân trang, popup mobile và bo góc. Ảnh nghiệm thu lưu `tests/ChatIntegration/desktop.png`, `mobile.png`. Keyboard điện thoại thật và hạ tầng nhiều instance cần smoke test trên staging khi có thiết bị/hạ tầng tương ứng.

Kết quả tại workspace ngày 20/10/2026: build thành công; browser suite trên hai phiên Chromium độc lập thành công (bao gồm báo cáo/ẩn, payload trên 4 KiB và reduced motion); 457 kiểm tra SeoRegression và 5 kiểm tra support UI thành công. Không chạy upstream WebSocket xổ số thực tế hoặc deploy production. Build còn cảnh báo framework .NET 6 đã hết hỗ trợ và không truy cập được nguồn audit NuGet trong môi trường kiểm tra; nâng framework là công việc riêng cần lên kế hoạch cho vận hành.

Cập nhật chính sách lưu cho máy RAM 3 GB/ổ đĩa trống 8 GB: 10 kiểm tra Redis retention thành công, gồm xóa tin trên 3 ngày và index tương ứng, giữ tin mới, xóa báo cáo trên 7 ngày, giữ báo cáo mới, retry không trùng, TTL chống trùng 3 ngày, giới hạn 2.000 bản ghi ở hash/index và rút TTL mã chống trùng cũ. Nếu website đang chạy, cần khởi động lại ứng dụng để nạp mã mới; không cần đổi appsettings hoặc schema SQL.

Tham khảo cấu hình chính thức: [SignalR configuration](https://learn.microsoft.com/en-us/aspnet/core/signalr/configuration?view=aspnetcore-6.0), [SignalR security](https://learn.microsoft.com/en-us/aspnet/core/signalr/security).
