# Lịch quay và cơ cấu giải

## Routes

- `/lich-quay` — danh mục lịch quay.
- `/lich-quay/mien-bac`, `/lich-quay/mien-trung`, `/lich-quay/mien-nam` — lịch theo miền; bộ lọc tỉnh/đài và thứ dùng query string.
- `/lich-quay/vietlott` — lịch Vietlott, chọn trò chơi và thứ qua query string.
- `/co-cau-giai-thuong` — danh mục cơ cấu giải.
- `/co-cau-giai-thuong/mien-bac`, `/co-cau-giai-thuong/mien-trung`, `/co-cau-giai-thuong/mien-nam` — cơ cấu theo miền.
- `/co-cau-giai-thuong/vietlott/{game}` — cơ cấu theo slug trò chơi hiện có.

Các trang đang `noindex` và không nằm trong sitemap cho đến khi có dữ liệu hữu ích được xác minh. Bộ lọc lịch cũng `noindex`; URL không được chấp nhận nếu có query ngoài danh sách cho phép.

## Dữ liệu và xác minh

`LotteryReferenceCatalog` là catalog dữ liệu tập trung. Không lấy ngày ghi nhận kết quả trong cơ sở dữ liệu làm lịch phát hành. Hiện danh sách giờ quay, ngoại lệ theo ngày, cơ cấu giải, giá vé và các phiên bản hiệu lực đều để trống vì kiểm tra nguồn hiện có chưa xác nhận được các chi tiết đó.

Các nguồn đã kiểm tra ngày 21/10/2026:

- Vietlott: <https://vietlott.vn/vi/home>. Các trang/đường dẫn trò chơi được thử không tải đầy đủ thể lệ và lịch để xác minh từng thông tin.
- Công ty TNHH MTV Xổ số kiến thiết TP. Hồ Chí Minh: <https://www.xskthcm.com/>. Nội dung trang được đọc không xác nhận giờ quay hoặc văn bản cơ cấu giải.

Chưa xác minh được trang/văn bản nguồn phù hợp cho lịch và cơ cấu các miền Bắc, Trung, các tỉnh/đài khác và từng trò chơi Vietlott. Giao diện phải tiếp tục ghi rõ phần thiếu; không tự thêm giờ, giá trị thưởng, điều kiện trúng hay ngày hiệu lực.

Khi có văn bản chính thức, lưu từng URL nguồn, ngày kiểm tra và mốc hiệu lực cùng dữ liệu tương ứng trong catalog. Không ghi đè phiên bản cơ cấu cũ; ngoại lệ lịch phải có ngày áp dụng và nguồn. `LotteryReferenceScheduleException` và `LotteryPrizeStructureVersion` biểu diễn các trường cần lưu.

Ngày “hôm nay” được xác định theo `Asia/Ho_Chi_Minh`, với tên timezone Windows tương đương làm fallback.
