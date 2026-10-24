> Cập nhật Xem Nhanh ngày 24/10/2026: xem [báo cáo và checklist mới](SEO-XEM-NHANH-2026-10-24.md). Các kết quả đo/giới hạn bên dưới là lịch sử của đợt cũ. SiteUrl hiện bắt buộc khai báo ở mọi môi trường; không còn fallback theo Host. Không có ảnh chia sẻ thương hiệu mặc định khi chưa cung cấp file.

# Triển khai SEO và chuẩn bị đổi tên miền

## Kiến trúc đã kiểm tra

Ứng dụng ASP.NET Core MVC, .NET 6, Razor render HTML từ server. HomeController đọc SQL Server bằng ADO qua EF Core; JSON Prizes được giữ dưới dạng chuỗi số. main.js nhận WebSocket ở trang chủ. Module API, thống kê và biểu mẫu hỗ trợ được giữ lại. Không đổi framework, không thay dữ liệu xổ số, không chạy DDL trên database.

## Cấu hình bắt buộc trước khi triển khai

Xem `site-settings.example.txt`. Dùng biến môi trường `Site__SiteUrl`, `Site__SiteName`, `Site__OrganizationName`, `Site__ContactEmail`, `Site__ContactPhone`, `Site__ContactAddress`. SiteUrl là origin HTTPS công khai, không có path/query/fragment; production thiếu/sai cấu hình sẽ dừng khởi động. Không dùng Host header để suy ra URL SEO. Đặt `AllowedHosts` đúng các hostname đang phục vụ tại IIS/application configuration. Kiểm tra canonical bằng Host khác trong môi trường thử nghiệm.

`appsettings*.json` đang bị .gitignore loại khỏi Git từ trước. Tệp example được lưu bằng đuôi .txt để không bị bỏ sót. Cấu hình Development trên máy đã được bổ sung origin localhost riêng và noindex; phải tái tạo bằng biến môi trường hoặc file riêng ở máy khác. Production không có fallback domain trong mã nguồn. Điền SiteUrl hiện tại khi triển khai; chưa bật chuyển domain.

`Site__LiveSocketUrl` giữ endpoint WSS thực tế đang dùng; máy hiện tại đã chuyển endpoint cũ sang cấu hình. Phải mang cấu hình này lên môi trường mới để giữ cập nhật trực tiếp. Chưa cấu hình thì giao diện thông báo rõ, bảng SQL vẫn hiển thị. Không đưa token của endpoint vào HTML.

`Site__ShareImagePath` mặc định `/images/share.png` (ảnh tĩnh trong repo). `SearchConsoleVerification` chỉ điền token thật. `AnalyticsId` chỉ điền mã GA4 thật dạng G-; để trống thì không có script analytics. Analytics và schema không tự xác minh tài khoản hay bảo đảm thứ hạng.

Staging dùng `ASPNETCORE_ENVIRONMENT=Staging`, origin HTTPS riêng và hai secret `Site__StagingUsername`, `Site__StagingPassword`. Thiếu secret thì ứng dụng không khởi động. Middleware yêu cầu Basic authentication, trả X-Robots-Tag noindex, sitemap rỗng; có thể bổ sung VPN/IP allowlist tại IIS. Không lưu secret vào Git. Các môi trường khác Production cũng luôn noindex.

IIS Integration cần nhận diện request HTTPS đúng. Ứng dụng redirect HTTPS 308, có hiệu lực trước static files; URL được chuẩn hóa chữ thường/bỏ slash cuối bằng 301. IIS TLS/bindings hoặc proxy không có trong repo nên phải kiểm tra thực tế. Với reverse proxy ngoài IIS, chỉ cấu hình forwarded headers từ proxy/IP đáng tin; không bật tin cậy toàn bộ X-Forwarded-* để tránh vòng lặp/spoofing. HTTP + URL sai chữ hoa được middleware chuyển trực tiếp về origin chuẩn. Domain cũ chưa được redirect sang domain mới.

## Routing và index

| Loại | URL | Chính sách |
| --- | --- | --- |
| Miền | `/xo-so-mien-bac`, `/xo-so-mien-trung`, `/xo-so-mien-nam` | Nội dung mới nhất; canonical riêng |
| Miền/ngày | `/xo-so-mien-bac/dd-MM-yyyy` | Ngày chính xác UTC+7, không fallback sang hôm nay |
| Tỉnh/đài | `/xo-so-{slug}` | Slug lấy từ tên thực trong database, chỉ tạo nếu có dữ liệu |
| Tỉnh/ngày | `/xo-so-{slug}/dd-MM-yyyy` | Tỉnh + ngày chính xác |
| Trò chơi | `/vietlott/{slug}` | Chỉ trò chơi có dữ liệu |
| Kỳ | `/vietlott/{slug}/ky/{DrawNo}` | Mã kỳ giữ số 0 đầu; kết quả giữ dạng chuỗi |
| Tham số tương đối | `?day=1..7` | Giữ chức năng lọc theo thứ; noindex, canonical tự tham chiếu, không redirect sang ngày cố định |
| Tham số kỳ cũ | `?gameCode=...&offset=...` | offset=0 chuyển 301 sang trang game tương đương; offset>0 noindex và canonical riêng |
| Thống kê | `/thong-ke`, `/thong-ke-tan-suat-loto` | Mặc định canonical riêng; bộ lọc noindex và canonical tự tham chiếu |
| Tracking không đổi nội dung | `utm_*` và query không được dùng | Canonical loại query tracking |
| URL MVC cũ | `/Home/Index`, `/Home/XoSoMienBac`... | 301 trực tiếp tới đường dẫn tương đương, giữ query |
| Không tồn tại/ngày sai/tương lai/không dữ liệu | URL tương ứng | 404; tham số sai kiểu/phạm vi trả 400 |

Giới hạn offset 100.000, weekday 1–7, ngày từ năm 1753 đến hiện tại Việt Nam. Các bộ lọc không được đưa vào sitemap. Không chặn các URL noindex công khai trong robots để crawler đọc được metadata. Các liên kết phân trang offset cũ vẫn crawl được. Trang lịch sử có liên kết trước/sau tới kỳ có dữ liệu; trang tỉnh và game có danh sách kỳ gần đây. Miền Bắc có bản ghi tổng hợp trùng slug miền thì dùng trang miền, tránh bản sao tỉnh giả.

## Sitemap và cache

`/robots.txt`: text/plain, HTTP 200. `/sitemap.xml`: XML namespace sitemap, URL từ SiteUrl; tự chuyển sang sitemap index khi trên 10.000 URL, các phần `/sitemap-N.xml`. URL được khử trùng, loại query, chỉ đưa trang dữ liệu và các trang thông tin đã có route. Các trang miền/Vietlott rỗng không được liệt kê. Thống kê hiện không đưa vào sitemap để tránh danh mục không có dữ liệu.

ArchiveCatalog chỉ cache danh mục URL, không cache bảng kết quả. Danh mục lấy từ dữ liệu tồn tại, loại ngày tương lai và bản ghi JSON rỗng; TTL 2 phút, semaphore chống nhiều lần dựng đồng thời trong một process. Sitemap, trang miền và lịch sử dùng lại danh mục này, không truy vấn toàn bộ lịch sử ở mỗi request. Kết quả SQL luôn đọc mới, nên metadata cache không che mất số mới trên trang đang có. Kỳ/đài mới có thể mất tối đa 2 phút để xuất hiện trong điều hướng/sitemap; có hàm `ArchiveCatalog.Invalidate()` nếu tích hợp cùng process với writer sau này. Writer kết quả hiện không nằm trong repository, nên chưa có event invalidation xuyên process.

lastmod chỉ lấy từ XoSoResults.UpdatedAt và xuất ngày lưu thực tế; không dùng thời điểm request. Vietlott chưa có timestamp sửa kết quả đáng tin cậy nên không tự gán lastmod. Không giả định CreatedAt là lần đối soát/sửa cuối. Khi dữ liệu rất lớn, chuyển sitemap sang job tạo file/snapshot và phân trang danh mục trong DB; hiện cache vẫn giữ toàn bộ metadata URL trong RAM, cần đo bộ nhớ theo dung lượng thật.

Truy vấn Vietlott đã gộp lấy giải của các kỳ trong một lệnh theo danh sách ID có tham số, thay vì một lệnh mỗi kỳ; bộ lọc game được áp dụng trước khi xếp hạng. Thống kê tần suất lọc miền ngay tại SQL và chặn phạm vi vượt qua ngày hiện tại. Chưa thêm cache cho bảng số để không trả kết quả cũ trong giờ quay. Truy cập ngày/kỳ mới đọc SQL ngay cả khi danh mục điều hướng chưa kịp làm mới; regression kiểm tra tình huống này. Danh mục kiểm tra JSON bằng .NET để chạy được trên SQL Server hiện tại không hỗ trợ ISJSON/OPENJSON; đọc thêm cột Prizes khi làm mới cache. Trang tỉnh lọc theo tên tỉnh thực tế để gom dữ liệu khi các nguồn lưu mã tỉnh khác nhau.

## Dữ liệu và minh bạch

`OrganizationName`, thông tin liên hệ, `DataSourceDescription`, `VerificationProcess` phải do chủ website cung cấp đúng thực tế. Không điền mẫu giả. Schema Organization chỉ xuất khi có tên thật và tên này cũng hiển thị ở footer/trang giới thiệu. WebSite và BreadcrumbList khớp nội dung hiển thị; không thêm Review/Product/FAQ.

Database hiện không lưu trạng thái pending/drawing/complete/verified cho từng kỳ. Vì vậy dữ liệu đã lưu được ghi là chưa xác nhận hoàn tất/đối soát; WebSocket có trạng thái chờ dữ liệu, đang nhận, mất kết nối và nguồn ngừng gửi. Không suy diễn “hoàn tất” từ số giải hoặc thời gian. XoSoResults.UpdatedAt hiển thị nguyên thời gian nguồn, không khẳng định timezone chưa được xác nhận. Vietlott DrawDate là DATE trong tài liệu schema: chỉ hiển thị ngày và nói rõ chưa có giờ quay; không tự bịa giờ.

Đã bỏ tuyên bố “nhanh nhất/chính xác nhất”, nhãn cập nhật hôm nay không có căn cứ; khi dữ liệu cũ ghi ngày kết quả gần nhất. Nút Tin Xổ Số/Android/iOS không có đích đã được ẩn. Biểu mẫu hỗ trợ vẫn có CSRF và chỉ gửi khi người dùng bấm gửi; chưa gửi thử ra Telegram.

Các nội dung còn thiếu: tên pháp lý đơn vị vận hành; email/điện thoại/địa chỉ xác thực; danh sách nguồn được phép dùng; quy trình và người chịu trách nhiệm đối soát; trạng thái từng kỳ; thời gian quay và thời gian cập nhật Vietlott. Chính sách bảo mật cũ còn các chỗ trống về công cụ bên thứ ba, cookie, thời hạn lưu trữ, quốc gia máy chủ và email; chủ website cần xác nhận trước công bố, không tự điền giả.

## Kiểm tra triển khai và Search Console

1. Chạy `dotnet build` và `dotnet run --project tests/SeoRegression/SeoRegression.csproj`. Regression dùng fixture tách biệt; không ghi vào database, không gửi hỗ trợ, không kết nối Redis/WebSocket bên ngoài.
2. Dùng SQL production ở môi trường được phép: đối chiếu trang chủ, 3 miền, tỉnh/ngày và game/kỳ với dữ liệu gốc; thử refresh và tắt JS vẫn có kết quả. Kiểm tra ngày không dữ liệu trả 404. Xem kế hoạch SQL và đọc `seo-query-indexes.sql` trước khi quyết định áp dụng index; không chạy DDL tự động.
3. Kiểm tra HTTPS, canonical, og:url, JSON-LD và sitemap không có localhost/staging. robots/sitemap phải HTTP 200, MIME đúng. Thử chữ hoa/slash cuối, URL MVC cũ và `?day=` để xác nhận không có vòng lặp và không đổi nghĩa chức năng.
4. Ở màn hình 360/390/768px, kiểm tra bảng nhiều tỉnh, Keno, số dài, menu, cuộn ngang trong bảng và quảng cáo không che nội dung. CSS hiện có overflow-x cho bảng và kích thước banner cố định, nhưng vẫn cần kiểm tra hình ảnh trên trình duyệt thật.
5. Trong Search Console do chủ website quản lý, xác minh quyền sở hữu; điền token HTML nếu dùng URL-prefix verification, hoặc DNS với Domain property. Gửi `/sitemap.xml`; dùng URL Inspection cho các URL lấy trực tiếp từ sitemap: `/`, một miền, một tỉnh, ngày có dữ liệu, game và kỳ. Không dùng ngày hoặc tỉnh ví dụ nếu database không có.
6. Đo Lighthouse/PageSpeed trước và sau trên cùng máy chủ, mạng, viewport và cache state; lấy median nhiều lần. Đây là lab. LCP/INP/CLS thực tế cần CrUX hoặc RUM đủ lượt truy cập; không suy ra từ kích thước JS/CSS hoặc một request HTTP.

## Checklist chuyển domain (chưa kích hoạt)

- Giữ URL paths; cấu hình origin mới, TLS/DNS/bindings và dữ liệu trước. Đổi SiteUrl trên site mới, kiểm tra đồng thời canonical, sitemap, JSON-LD và og:url.
- File `iis-domain-redirect.config.example` là template riêng cho IIS site domain cũ, mặc định disabled. Thay hostname cố định ở cả condition và action, escape dấu chấm trong regex host. Không lấy domain đích từ query/Host. Chỉ bật khi URL tương ứng ở domain mới đã qua kiểm tra. Giữ path và query; không redirect mọi URL về homepage.
- Xác minh cả hai property Search Console, gửi sitemap mới và dùng Change of Address khi phù hợp. Duy trì redirect ít nhất một năm, tốt nhất lâu dài; duy trì TLS domain cũ và theo dõi 404/5xx. [Hướng dẫn chuyển website của Google](https://developers.google.com/search/docs/crawling-indexing/site-move-with-url-changes), [Change of Address](https://support.google.com/webmasters/answer/9370220).
- Thử URL trang chủ, miền, tỉnh, ngày và kỳ cùng query từ domain cũ: 301 đúng trang tương ứng; domain mới 200, canonical tự tham chiếu, không loop. Sao lưu config trước bật; rollback bằng tắt rule và khôi phục SiteUrl khi cần.

## Kết quả kiểm tra và giới hạn

Đợt kiểm tra 18/10/2026: build thành công; regression 85 kiểm tra đã chạy thành công, gồm xác thực staging, danh mục tương thích SQL cũ và tỉnh có nhiều mã nguồn. Có kiểm tra HTTP thật bằng Kestrel và compiled Razor, metadata, một H1, 404, sitemap index/partition, domain change, SQL date/DrawNo parameters và giữ số 0 đầu qua fixture. `node --check wwwroot/js/main.js` thành công. Nhật ký: `tests/SeoRegression/last-run.txt`. Kiểm tra bổ sung 100 URL nội bộ với database đang cấu hình: tất cả trả HTTP 200; các trang HTML có đúng một H1/canonical và noindex ở Development. URL giả/ngày sai đã xác nhận 404. Tắt response compression riêng ở Development để dotnet watch chèn script browser refresh được.

Đo tĩnh từ layout HEAD trước sửa và layout mới: tài nguyên local tải trực tiếp 5 → 3, tổng byte chưa nén 486.989 → 327.738 tại thời điểm đo; stylesheet bên ngoài 3 → 1. Số đo không bao gồm tài nguyên con của CSS/CDN và không phải benchmark thời gian tải. Có bật response compression HTTPS và cache static fingerprint một năm, file không fingerprint một giờ; cần kiểm tra Content-Encoding qua IIS thật.

Không truy cập được website công khai qua công cụ web trong phiên này. Công cụ trình duyệt không có browser được kết nối, nên chưa kiểm tra hình ảnh mobile. Chưa đo Lighthouse/PageSpeed, LCP/INP/CLS hoặc lưu lượng thật; chưa xác nhận SQL execution plan/index/schema trên DB production, HTTPS/proxy IIS, source feed hoặc một chu kỳ quay trực tiếp. Không công bố các mục này là đã nghiệm thu. .NET 6 và một số warning nullable/unused có sẵn vẫn được build báo; mạng NuGet audit không truy cập được nên cảnh báo NU1900, không ảnh hưởng kết quả build/kiểm thử từ package cache.

## Danh sách tệp

- `Services/SiteSettings.cs`, `SeoMetadata.cs`, `ArchiveCatalog.cs`: cấu hình, URL, metadata và danh mục dữ liệu.
- `Controllers/HomeController.cs`, `HomeController.Archive.cs`, `SeoController.cs`: truy vấn chính xác, routes, robots/sitemap và validation.
- `Middlewares/PublicUrlMiddleware.cs`, `StagingAccessMiddleware.cs`, `Startup.cs`: URL, staging, compression/cache/HTTPS.
- `Models/SeoModels.cs`, `XoSoResult.cs`: model archive, liên kết và timestamp nguồn.
- `Views/Shared/_Layout.cshtml`, `_Seo.cshtml`, `_Contact.cshtml`; `Views/Home/Archive.cshtml`, `Information.cshtml`, `_ResultStatus.cshtml`: layout metadata, thông tin, dữ liệu archive.
- Các view `Index`, ba miền, `Vietlott`, `_XoSoRegionResults`, `_XoSoRegionDayResults`, `About`, `Privacy`, `Terms`, `Support`: heading, links, cấu hình domain, trạng thái và liên hệ.
- `wwwroot/js/main.js`, `wwwroot/css/style.css`, `wwwroot/images/share.png`: socket, tài nguyên, ảnh chia sẻ.
- `XoSoWeb.csproj`, `tests/SeoRegression/*`: tách test khỏi app build và bộ regression không dùng gói test bên ngoài.
- Các file docs trong thay đổi này; appsettings private trên máy cũng được bổ sung cấu hình như mô tả phía trên.
