# News module

## Database setup

The application does not use EF migrations for these tables. Before enabling the news routes or the homepage news query, apply [`news-module.sql`](./news-module.sql) once to the configured SQL Server database using the normal DBA process. Back up the database first. Do not run the script against production without the site's deployment approval.

For an existing news database, apply [`news-thumbnail.sql`](./news-thumbnail.sql) before deploying the rich-text editor; it adds the nullable thumbnail URL column.

## Admin account

Configure both environment variables on the app host:

- `NewsAdmin__Username`
- `NewsAdmin__PasswordHash`

`PasswordHash` must be a BCrypt hash (cost 12 or higher recommended). Generate it in a trusted local .NET environment with the already-installed `BCrypt.Net-Next` package, e.g. `BCrypt.Net.BCrypt.HashPassword(password, 12)`. Do not put a plaintext password or hash in source control, appsettings files, logs, or chat. If either variable is missing or the hash is malformed, startup fails rather than leaving a partially configured admin.

Admin URL: `/quan-tri/tin-tuc/dang-nhap`. The configured account is assigned the server-side `NewsAdmin` role. The editor supports draft creation/editing, preview, submitting for review, publishing, withdrawing, revision history, and recording that a changed source was reviewed.

The editor uses TinyMCE 6.8.5 from jsDelivr (GPL license). Rich text is sanitized on save and render using an allowlist; scripts, event handlers, and non-HTTPS URLs are removed. HTTPS image URLs may be embedded directly and are loaded from their source by readers' browsers, so those sources can learn reader network metadata and may later remove the image. Uploaded JPG, PNG, or WebP files are converted to WebP in the browser, limited to 10 MB and 40 megapixels, then stored under `wwwroot/images/news`. The server validates the WebP container and dimensions without decoding uploaded data. Uploading a featured image also sends a generated 640×360 WebP thumbnail used on article lists; HTTPS images are not downloaded or resized by the server.

The admin article table shows 20 articles per page. The public news categories already use 10 articles per page.

Administrators can permanently delete an article from the admin list after a browser confirmation. Its revision history and slug redirects are deleted by the database cascade; source records are retained with the article link cleared.

## Collection status

The collector reads only the latest headline, publication date, and article URL from [Vietlott's prize-news listing](https://vietlott.vn/vi/tin-tuc/tin-trung-thuong). It does not fetch article pages, copy body text, generate an AI rewrite, or download images. A new item is saved as a `Draft` in the `trung-thuong-vietlott` category with a short attribution and link to the original; an editor must review and publish it. An existing normalized source URL is skipped as a duplicate.

The admin **Lấy bài mới nhất ngay** button runs the same collector manually. Automatic runs are scheduled daily at 08:00 and 20:00 Vietnam time by default. Configure `NewsCollection__AutomaticEnabled=false` to disable scheduled runs, or set `NewsCollection__MorningHour` and `NewsCollection__EveningHour` (0–23) to choose other local hours. The worker uses the configured Windows or IANA Vietnam timezone.

The listing was checked on 2026-10-21 and `robots.txt` returned only the text `Vietlott`, not usable directives. The adapter therefore makes only the user-requested, low-frequency request to the listing URL; redirects are not followed. It enforces HTTPS, the exact Vietlott host/path, a 20-second timeout, a 2 MB response limit, and creates no content if the expected listing structure or publication date cannot be parsed. This metadata-only implementation is not an assertion of permission to reproduce Vietlott article text; it does not do so.

## Editorial and SEO behavior

Only published articles whose publication time has arrived are public, appear on the homepage, or enter the sitemap. Empty categories are `noindex`; previews require the admin role and are `noindex`. Article content is sanitized against an HTML allowlist before it is rendered. No images are downloaded automatically. Editors may reference an existing local image under `/images/news/`.

The news routes are:

- `/tin-tuc`
- `/tin-tuc/vietlott`
- `/tin-tuc/trung-thuong-vietlott`
- `/tin-tuc/huong-dan`
- `/tin-tuc/bai-viet/{slug}`

Winner details are optional and should only be entered when stated by the cited source. In particular, draw date, source publication date, prize value, winner/ticket counts, received amount, tax treatment, and award date are separate fields.
