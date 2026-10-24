# Hoàn thiện SEO Xem Nhanh — 24/10/2026

## Phạm vi và môi trường

ASP.NET Core MVC / .NET 6; bảng kết quả do Razor render từ SQL Server, dữ liệu Prizes là JSON chứa chuỗi số. ArchiveCatalog quản lý danh mục URL. Kênh WSS tiếp tục cập nhật trang chủ; chat SignalR chỉ bắt đầu kết nối khi mở. Bộ thu thập kết quả không nằm trong repository.

Đã đọc cấu trúc controller, schema/tài liệu database, các template, middleware URL, cấu hình môi trường và tests trước khi sửa. Chỉ đọc database đang cấu hình để audit; không chạy DDL, sửa kết quả, xuất bản tin, thay DNS hoặc deploy. Các thay đổi CSS có sẵn trước đợt SEO được giữ nguyên.

Domain hoạt động vẫn là `https://xemnhanh.vn`; domain dự kiến `https://xemnhanh.vn` chỉ xuất hiện trong tài liệu/template chuyển miền chưa kích hoạt. Views/services không hardcode hai hostname này. Cấu hình được hướng dẫn tại [site-settings.example.txt](site-settings.example.txt).

## Giai đoạn 1 — audit và lỗi đã xác nhận

- `SiteUrl` từng fallback theo Host chưa xác minh. Đã bỏ fallback và bắt buộc origin cấu hình ở mọi môi trường.
- Tên mặc định còn “Xổ Số Trực Tiếp”; đã đổi thành Xem Nhanh, thêm slogan tập trung. Production cần cấu hình `Site__SiteName=Xem Nhanh` nếu giá trị cũ đang override mặc định.
- Khi audit ban đầu repo chưa có tài sản Xem Nhanh. Sau đó đã nhận các file thật trong wwwroot/images: logo.png (2172×724), favicon.ico (6 kích thước), “Biểu tượng mắt xanh lưới cam.png” (1536×1024). Đã dùng nguyên file cho header/favicon/ảnh chia sẻ, không tự tạo ảnh thay thế. share.png cũ giữ cho tương thích URL, không còn được chọn mặc định. Logo có dòng thương hiệu xemnhanh.vn trong hình do người dùng cung cấp; href/canonical/sitemap/OG URL/JSON-LD vẫn dùng origin đang hoạt động.
- Exception middleware cũ trả exception.Message cho client và gửi body lỗi ra kênh Telegram. Đã bỏ việc gửi tự động, không đọc/log body; chi tiết exception chỉ nằm trong log phía server, client nhận 500 chung + mã trace.
- Bộ SEO regression không biên dịch vì test còn gọi `VietlottNewsSourceAdapter` đã bị gỡ khỏi mã nguồn. Đã bỏ assertion lỗi thời đó, giữ các kiểm tra chính sách URL và các phần tin hiện có; không tạo adapter giả.
- Truy vấn lịch sử có thể trả 404 với một đài được catalog chuẩn hóa sang miền khác dữ liệu nguồn. Đã thống nhất RegionSql với định danh HCM/Đắk Lắk/Đắk Nông, không chỉnh bản ghi gốc.
- Một bản cập nhật thiếu có thể đẩy mất giải đầy đủ khi gộp nguồn. Đã ưu tiên bản hoàn chỉnh, rồi giải hoàn chỉnh, rồi độ ưu tiên nguồn và metadata hiện có.
- Xem [danh sách 10 bảng MB cần kiểm tra lại](SEO-DATA-REVIEW-2026-10-24.md). Audit đọc 257 bản ghi từ 13/09 đến 23/10/2026; không đại diện toàn bộ database của máy chủ công khai.

## Giai đoạn 2 — metadata, canonical, breadcrumb, status

- Home title: “Xem Nhanh - Kết quả xổ số hôm nay, XSMB, XSMT, XSMN, Vietlott”; description theo yêu cầu. Trang miền thêm XSMB/XSMT/XSMN; lịch sử giữ ngày/mã kỳ thật, số 0 đầu, canonical riêng và link kỳ liền kề có dữ liệu.
- Metadata dùng chung giữ WebSite/BreadcrumbList, mỗi ListItem dùng `@type`. Header thương hiệu vẫn không phải H1. Không thêm keywords, rating, Product, Review, FAQ, Event hoặc loại schema xổ số tự chế.
- Organization chỉ có khi cấu hình tên vận hành thật; logo schema chỉ có khi file được cấu hình. Mặc định không có đơn vị/tác giả giả.
- LogoUrl/FaviconUrl/DefaultShareImage tập trung trong cấu hình; mặc định trỏ đúng các file đã cung cấp và kiểm tra; đường dẫn local phải tồn tại. Cả ba file đã kiểm tra HTTP 200 với MIME ảnh, URL tên tiếng Việt được percent-encode khi chia sẻ. ShareImagePath là alias để tương thích deployment cũ. Chưa có tài sản thương hiệu thì không sinh đường dẫn ảnh giả. URL ảnh bài HTTPS ngoài site được giữ đúng origin, không bị nối vào domain website.
- Thêm twitter:title/description và thẻ ảnh khi có ảnh thật. Ngày bài/JSON-LD/OG sử dụng UTC có Z, hiển thị theo giờ Việt Nam. Hướng dẫn dùng Article; tin dùng NewsArticle.
- Bộ lọc thống kê/weekday/offset noindex nhưng crawl được, canonical riêng, bỏ tracking. Phân trang tin có nội dung riêng cho phép index và title riêng theo trang; trang rỗng vẫn noindex, vượt số trang trả 404.
- Không đổi URL đang hoạt động. Các URL MVC/alias đài vẫn redirect đúng đường dẫn tương đương. URL/ngày/kỳ không hợp lệ trả 404, query không hợp lệ trả 400. Lỗi 500 không redirect về trang chủ.

## Giai đoạn 3 — sitemap và liên kết

- Sitemap chỉ chọn ArchiveLink indexable. Bảng giải thiếu cấu trúc vẫn tra cứu được nhưng noindex và bị loại khỏi sitemap; trang miền không có bảng đầy đủ cũng noindex. Giữ số trùng ở các vị trí giải khác nhau.
- Danh mục thống kê mặc định vào sitemap khi có dữ liệu đầy đủ trong đúng khoảng ngày mặc định. Công cụ Vietlott dùng danh mục đã có dữ liệu; trang lịch/cơ cấu chưa xác minh vẫn không vào sitemap.
- XML giữ namespace, dedup path, partition 10.000 URL; lastmod lấy update nội dung đã lưu, không gán DateTime.Now. Vietlott chưa có update timestamp đáng tin nên bỏ lastmod.
- Thêm cache XML theo origin, ngày, partition và PublicContentVersion (TTL 2 phút). Sửa/duyệt/gỡ/xóa tin invalidate version sau commit; ArchiveCatalog.Invalidate cũng tăng version. Quick statistics và thống kê Vietlott đọc version này. Writer ngoài process chưa có event tích hợp: còn TTL catalog 2 phút / quick stats 5 phút và TTL riêng của từng cache; không hứa invalidation tức thời xuyên process. HTTP cache downstream cũng cần purge/expiry.
- Menu desktop gọn: Kết quả | Vietlott | Thống kê | Lịch quay | Tin tức. Các mục con và footer giữ các route hiện có. Thống kê có liên kết kết quả nguồn; Nhịp số liên kết từng ngày nguồn bằng anchor crawl được.

## Giai đoạn 4 — dữ liệu, thống kê, tin và thương hiệu

- Structural validator kiểm tra nhóm giải, số lượng và độ dài theo MB/MT/MN; đây là kiểm tra cấu trúc, không khẳng định đối soát chính thức. Template hiển thị rõ thiếu/đủ cấu trúc và đường dẫn báo sai.
- Weekly frequency bỏ bảng thiếu cấu trúc, công bố số bảng dùng/loại, phân biệt ngày thiếu dữ liệu với số không xuất hiện. Soi cầu/Nhịp số tiếp tục dừng chuỗi khi thiếu dữ liệu. Kết quả chính luôn có trong HTML.
- Bài chưa có tác giả/biên tập thật bị chặn duyệt; tin Vietlott/trúng thưởng cần tên + URL nguồn; ảnh đại diện cần alt. Bản nháp/xem trước vẫn được bảo vệ. Sửa không đổi nội dung không cập nhật UpdatedAt; redirect slug chỉ hướng đến bài published đã đến thời gian công bố.
- Giới thiệu ngắn và slogan theo yêu cầu; footer giữ câu “Cập nhật kết quả xổ số nhanh chóng — dễ xem, dễ tra cứu mỗi ngày.” Không thêm lời hứa về thứ hạng hoặc số liệu người dùng.

## Giai đoạn 5 — hiệu năng và mobile

Giữ bảng HTML từ server, khoảng quảng cáo có giữ chỗ, compression ngoài Development và cache static fingerprint sẵn có. Logo gốc 565.299 byte được giữ nguyên theo file cung cấp, không tự tạo bản thay thế; favicon 44.301 byte. Ảnh chia sẻ 1.106.385 byte chỉ được tham chiếu trong metadata, không tự tải như ảnh nội dung trang. Nếu muốn giảm dung lượng logo, chủ website cung cấp bản tối ưu tương ứng. main.js được defer; logic reconnect/live/chat không đổi. SignalR vẫn lazy-load khi mở chat.

Bỏ title lặp lại trên từng ô của ma trận Nhịp số (đã có header ngày/cặp số) và thêm nhãn ĐB cho trình đọc màn hình. Soi cầu chỉ render template của những ngày thực sự được dùng, bỏ tooltip thừa trong từng chữ số lịch sử; vẫn giữ bảng 107 vị trí, nội dung giải và các số tạo cầu.

Số đo HTML chưa nén từ Kestrel cục bộ + database thật, trước/sau tối ưu payload và bổ sung metadata/logo trong đợt này (cùng URL/dữ liệu):

| Trang | Trước (byte) | Sau (byte) |
| --- | ---: | ---: |
| Soi cầu | 486.840 | 244.755 |
| Nhịp số | 571.331 | 357.427 |

Đây không phải Lighthouse/PageSpeed, Core Web Vitals hoặc số đo người dùng thực. Thời gian request lab phụ thuộc cache/JIT và không dùng để khẳng định website công khai nhanh hơn. Đã kiểm tra inventory công cụ ở cuối đợt: apps và browsers đều rỗng, nên chưa chụp/kiểm tra trực quan mobile. Tông màu/cỡ chữ responsive ở các đợt trước được giữ lại.

## Giai đoạn 6 — nghiệm thu và giới hạn

Kết quả nghiệm thu cục bộ:

| Bộ kiểm tra | Kết quả |
| --- | --- |
| Build ứng dụng và các project kiểm tra | Thành công |
| SEO regression (fixture + routing/MVC/Razor thật) | 893 kiểm tra đạt |
| Local HTTP audit với database đang cấu hình | 80/80 đạt; gồm 40 URL lấy mẫu từ sitemap |
| Nhịp số/Thống kê nhanh | 45 kiểm tra đạt |
| Soi cầu | 11 kiểm tra đạt |
| JavaScript main/chat/soi-cau/number-rhythm | node --check đạt |
| git diff --check | Đạt; chỉ cảnh báo chuẩn hóa LF/CRLF, không có lỗi whitespace |

[Log kiểm tra SEO](seo-evidence-2026-10-24/seo-regression.txt), [HTTP với DB thật](seo-evidence-2026-10-24/local-http-audit.txt), [audit dữ liệu readonly](seo-evidence-2026-10-24/readonly-database-audit.txt). Các log còn lại nằm cùng thư mục evidence.

HTTP audit xác nhận `/xo-so-dak-lak/13-09-2026` từ 404 đã trả 200 sau khi thống nhất filter miền. 10 bảng MB thiếu vẫn được giữ để đối soát; không tự thêm số.

Lệnh tái lập:

```powershell
dotnet build --no-restore -p:UseAppHost=false
dotnet run --project tests/SeoRegression --no-restore
dotnet run --project tests/NumberRhythmRegression --no-restore
dotnet run --project tests/SoiCauRegression --no-restore
# Chỉ đọc database đang cấu hình, không chạy collector/writer:
dotnet run --project tests/SeoRegression -- --audit-data
dotnet run --project tests/SeoRegression -- --audit-http --origin https://xemnhanh.vn
```

HTTP audit là Kestrel cục bộ với database thật: có kiểm tra HTML/H1/canonical/JSON-LD của các nhóm trang và lấy mẫu 40 URL trong sitemap. Không phải crawl toàn bộ website production. Không khởi động chat, retention, feed hay gửi hỗ trợ. Bộ unit/integration còn lại dùng fixture, không ghi kết quả vào database thật.

Chưa nghiệm thu được: origin public qua công cụ web không truy cập được; chưa có quyền Search Console/DNS; chưa có thông tin đơn vị vận hành; chưa xác minh đầy đủ lịch, ngoại lệ và văn bản cơ cấu giải/effective date. Trang chính thức XSKT TP HCM đọc được bảng kết quả/cơ cấu của các kỳ hiển thị; trang Vietlott trả lỗi/403. Không suy từ đó ra lịch/quy định áp dụng chung cho mọi đài hoặc tự điền hiệu lực. Các trang thông tin này tiếp tục ghi rõ chưa xác minh và noindex.

Đã tạo công cụ audit/danh sách yêu cầu đối soát. Chưa thu thập lại 10 bảng MB vì collector và quyền truy cập nguồn không nằm trong repo. Không tự đệm số hoặc tạo lại kết quả. .NET 6 có cảnh báo hết hỗ trợ; chưa đổi framework trong đợt SEO này. Chat end-to-end cần Redis test riêng/browser; chưa chứng nhận một phiên quay thật hoặc chat thật.

## Dữ liệu chủ website cần cung cấp

1. Đã nhận logo/favicon/icon chia sẻ và dùng nguyên file. Chủ website xác nhận quyền sử dụng; nếu cần ảnh share đúng tỉ lệ 1200×630 riêng, cung cấp file đó để thay cấu hình; hiện dùng icon 1536×1024 có thật.
2. Tên người/đơn vị vận hành, email/điện thoại/địa chỉ, nguồn được phép dùng, quy trình đối soát và thông tin quyền ảnh/bài.
3. Nguồn lịch/cơ cấu chính thức, ngày kiểm tra và thời gian hiệu lực; endpoint/quyền collector để yêu cầu thu thập lại bảng thiếu.
4. Token Search Console, GA4 thật nếu muốn bật; quyền quản lý cả domain cũ/mới và thông tin TLS/proxy/IIS đã xác minh. Không gửi secret vào Git.

## Checklist triển khai và rollback

Trước triển khai: sao lưu build/config hiện tại; xác nhận Site__SiteUrl=https://xemnhanh.vn, Site__SiteName=Xem Nhanh, AllowedHosts tương ứng; giữ endpoint WSS thực; cung cấp file asset rồi cấu hình đường dẫn; điền đơn vị thật hoặc để trống; staging HTTPS + Basic auth + noindex. Chạy build/tests, đối chiếu dữ liệu và audit readonly. Không áp dụng SQL migration/index tự động.

Sau triển khai được người vận hành phê duyệt: kiểm tra HTTP/HTTPS, 200/301/404/500, Host giả, canonical/og:url/WebSite/sitemap chỉ có domain đang phục vụ, ảnh/file CSS/JS trả 200, các kỳ lịch sử, noindex trang thiếu, bảng trước JS, reconnect WSS và chat. Đo PageSpeed/Lighthouse nhiều lần trên cùng máy/mạng/cache và phân biệt với CrUX/RUM.

Rollback: khôi phục artifact/config đã sao lưu; không đụng dữ liệu kết quả. Giữ SiteUrl của domain thực đang phục vụ, tắt rule đổi domain nếu đã bật ở đợt triển khai sau; purge cache IIS/CDN và restart process để bỏ snapshot. Nếu thiếu SiteUrl hoặc file asset, sửa cấu hình/file hoặc rollback thay vì bật fallback Host.

## Chuẩn bị chuyển domain — chưa kích hoạt

- Template `iis-domain-redirect.config.example` giữ enabled=false, ánh xạ xemnhanh.vn/{path}?query → xemnhanh.vn/{path}?query bằng 301. Không có rule này trong web.config chạy thật.
- Chưa xác nhận quyền sở hữu/DNS/bindings của domain mới. Chỉ bật sau khi quyền quản lý, TLS cả hai domain, nội dung và từng path đã được kiểm tra. Không redirect mọi URL về home.
- Khi chuyển thật: đổi đồng bộ SiteUrl/AllowedHosts ở domain mới; kiểm tra canonical/sitemap/OG/JSON-LD chỉ domain mới, cập nhật internal link, bật redirect ở site cũ, giữ HTTPS và redirect ít nhất 1 năm, tốt hơn lâu dài.
- Chủ website xác minh hai Search Console properties, gửi sitemap mới và Change of Address khi phù hợp. Theo dõi URL Inspection/canonical Google chọn, crawl/404/5xx/sitemap/CWV; click, impression, CTR theo truy vấn/trang đích. Số URL sitemap không phải số trang đã index; không dùng site: để kết luận đầy đủ index.

Tài liệu chính thức đã đối chiếu: [title links](https://developers.google.com/search/docs/appearance/title-link), [site names](https://developers.google.com/search/docs/appearance/site-names), [sitemaps](https://developers.google.com/search/docs/crawling-indexing/sitemaps/build-sitemap), [chuyển site](https://developers.google.com/search/docs/crawling-indexing/site-move-with-url-changes), [Organization](https://developers.google.com/search/docs/appearance/structured-data/organization). Không cam kết thứ hạng, index hoặc rich result.

## Nhóm tệp thay đổi

- Cấu hình/metadata: SiteSettings, SeoMetadata, _Seo, Startup, NewsDateTime; giữ nguyên và dùng ba asset thương hiệu mới được cung cấp trong wwwroot/images.
- URL/dữ liệu/sitemap: StationCatalog, LotteryDataQuality, ArchiveCatalog, SeoController, PublicContentVersion, các controller đọc kết quả/thống kê.
- Nội dung và giao diện: _Layout, Index, Information, các template miền/lịch sử/thống kê, News/Index và News/Detail.
- Tin tức/cache: NewsArticleService, NewsAdminController, cache statistics/quick statistics.
- Lỗi HTTP: ExceptionMiddleware.
- Kiểm thử: tests/SeoRegression và DatabaseAudit, assertion accessibility trong NumberRhythmRegression.
- Triển khai: site-settings.example.txt, redirect template disabled, báo cáo này và danh sách dữ liệu cần kiểm tra.
