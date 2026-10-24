# Danh sách kiểm tra lại dữ liệu miền Bắc — 24/10/2026

Nguồn: truy vấn chỉ đọc database đang cấu hình trên máy làm việc; tối đa 10.000 bản ghi gần nhất. Không sửa/xóa dữ liệu, không gửi yêu cầu thu thập lại.

- Bản ghi đọc: 257
- Khoảng dữ liệu: 2026-09-13 – 2026-10-23
- Bản ghi dùng biến thể tên/mã của 3 đài đã biết: 20
- Bảng giải thiếu cấu trúc trước/sau gộp: 10/10

| URL cần đối soát | Nhóm giải | Số lượng đang lưu | Độ dài đang lưu |
| --- | --- | --- | --- |
| /xo-so-mien-bac/15-10-2026 | 3 | 5 | 5 |
| /xo-so-mien-bac/14-10-2026 | 3 | 4 | 5 |
| /xo-so-mien-bac/11-10-2026 | 3 | 5 | 5 |
| /xo-so-mien-bac/08-10-2026 | 3 | 5 | 5 |
| /xo-so-mien-bac/05-10-2026 | 3 | 5 | 5 |
| /xo-so-mien-bac/02-10-2026 | 3 | 3 | 5 |
| /xo-so-mien-bac/29-09-2026 | 3 | 4 | 5 |
| /xo-so-mien-bac/27-09-2026 | 3 | 5 | 5 |
| /xo-so-mien-bac/26-09-2026 | 3 | 5 | 5 |
| /xo-so-mien-bac/25-09-2026 | 3 | 4 | 5 |

Các dòng trên đều cần đối chiếu lại nhóm G3 miền Bắc (chuẩn cấu trúc có 6 kết quả, mỗi kết quả 5 chữ số). Không bổ sung số bằng suy đoán hoặc tự đệm số. Trang vẫn truy cập được, có nhãn chưa đầy đủ và noindex; không đưa vào sitemap.

Quy trình xử lý: người vận hành kiểm tra bản công bố chính thức → yêu cầu collector đang vận hành thu thập lại đúng đài/ngày → so sánh nguồn và giữ bản gốc → commit bản đối soát qua writer hiện có → invalidate cache trong cùng process hoặc chờ TTL của writer ngoài process → chạy lại lệnh audit và URL Inspection. Collector không nằm trong repository này; chưa có endpoint/quyền để thực thi thu thập lại.
