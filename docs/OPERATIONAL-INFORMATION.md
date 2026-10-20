# Thông tin vận hành cần chủ website bổ sung

Kiểm tra cấu hình local `appsettings.json`, `appsettings.Development.json` và các biến môi trường `Site__...`: sáu mục dưới đây chưa được điền. Đây là tình trạng trên máy phát triển, chưa xác nhận cấu hình production.

| Nội dung còn thiếu | Cấu hình | Chủ website cần cung cấp |
| --- | --- | --- |
| Đơn vị vận hành | `Site__OrganizationName` | Tên thật của đơn vị chịu trách nhiệm; chỉ dùng tên đã xác nhận, không lấy tên thương hiệu thay cho doanh nghiệp |
| Email liên hệ | `Site__ContactEmail` | Địa chỉ đang hoạt động và có người tiếp nhận |
| Điện thoại | `Site__ContactPhone` | Số liên hệ được phép công bố |
| Địa chỉ | `Site__ContactAddress` | Địa chỉ liên hệ thực tế được phép công bố |
| Nguồn tổng hợp | `Site__DataSourceDescription` | Tên và đường dẫn nguồn thực tế, phạm vi dữ liệu tổng hợp; không tự gán nguồn chính thức |
| Quy trình đối soát | `Site__VerificationProcess` | Cách kiểm tra đang áp dụng, người/bộ phận phụ trách, cách đính chính; chỉ công bố sau khi chủ website xác nhận |

Chủ website cũng cần xác nhận kênh tiếp nhận báo sai, người theo dõi và thời gian phản hồi thực tế nếu muốn công bố. Không tự đặt cam kết phản hồi hoặc tuyên bố mọi kết quả đã đối soát. Cấu hình nhận hỗ trợ Telegram (`TelegramContact__BotToken`, `TelegramContact__ChatId`) cần được xác nhận trong hệ thống triển khai; không đưa bí mật vào tài liệu công khai hoặc Git.

## Các trang đã rà soát

- `/gioi-thieu`: giới thiệu chức năng, đơn vị vận hành từ cấu hình và liên hệ. Khi thiếu tên đơn vị, trang ghi rõ chưa công bố.
- `/nguon-du-lieu`: giữ giải thích dữ liệu tổng hợp và yêu cầu đối chiếu kết quả chính thức, kể cả khi chủ website điền mô tả nguồn. Không tự tạo danh sách nguồn hoặc quy trình đối soát.
- `/support`: biểu mẫu báo lỗi/báo sai và hướng dẫn gửi URL, ngày quay, tỉnh/trò chơi, mã kỳ, nguồn đối chiếu. Thông tin liên hệ chỉ hiện khi có cấu hình thật.
- `/terms`: giữ giải thích về nguồn tổng hợp, giới hạn dữ liệu và đối chiếu kết quả chính thức.
- Footer: dùng đúng đoạn giới thiệu và câu ngắn đã được yêu cầu; không thêm số liệu truy cập hoặc các tuyên bố “nhanh nhất”, “chính xác nhất”.

Organization JSON-LD chỉ được sinh khi `OrganizationName` có giá trị. Hiện local không có giá trị nên không sinh Organization. Chủ website chịu trách nhiệm xác nhận tính thực tế của thông tin được cấu hình; mã không thể tự xác minh tư cách đơn vị vận hành.

## Kiểm tra gửi hỗ trợ

`dotnet run --project tests/SeoRegression --no-restore`: 457 kiểm tra đạt. Bao gồm CSRF, dữ liệu nhập không hợp lệ, gửi báo sai qua MVC, xác nhận Telegram, phản hồi lỗi HTTP, `ok:false`, JSON không hợp lệ, lỗi mạng và thiếu cấu hình. Mọi phản hồi Telegram đều dùng transport giả lập, không gửi tin thật.

`node tests/SeoRegression/support-ui.cjs`: 5 tình huống đạt. Giao diện chỉ báo thành công và xóa biểu mẫu khi HTTP thành công đồng thời API trả `success:true`; các lỗi giữ lại nội dung, bật lại nút gửi và không hiển thị thành công. Chặn gửi lặp khi yêu cầu còn chờ, gửi kèm CSRF và thông báo trạng thái hỗ trợ trình đọc màn hình.

Chưa kiểm tra việc tin nhắn thực sự đến người tiếp nhận trên production. Chỉ xác nhận phần đó sau khi chủ website xác minh cấu hình và cho phép gửi một báo cáo thử.
