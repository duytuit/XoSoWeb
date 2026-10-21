# News module

## Database setup

The application does not use EF migrations for these tables. Before enabling the news routes or the homepage news query, apply [`news-module.sql`](./news-module.sql) once to the configured SQL Server database using the normal DBA process. Back up the database first. Do not run the script against production without the site's deployment approval.

## Admin account

Configure both environment variables on the app host:

- `NewsAdmin__Username`
- `NewsAdmin__PasswordHash`

`PasswordHash` must be a BCrypt hash (cost 12 or higher recommended). Generate it in a trusted local .NET environment with the already-installed `BCrypt.Net-Next` package, e.g. `BCrypt.Net.BCrypt.HashPassword(password, 12)`. Do not put a plaintext password or hash in source control, appsettings files, logs, or chat. If either variable is missing or the hash is malformed, startup fails rather than leaving a partially configured admin.

Admin URL: `/quan-tri/tin-tuc/dang-nhap`. The configured account is assigned the server-side `NewsAdmin` role. The editor supports draft creation/editing, preview, submitting for review, publishing, withdrawing, revision history, and recording that a changed source was reviewed.

## Collection status

The scheduler interval is configured with `NewsCollection__IntervalMinutes` (default 60, clamped to 5–1440). `NewsCollection__AutomaticEnabled` controls scheduled checks and defaults to `true`. The admin **Lấy tin ngay** action records a manual check.

Official source pages checked on 2026-10-21:

- [Vietlott news](https://vietlott.vn/vi/tin-tuc)
- [Vietlott Jackpot winner announcements](https://vietlott.vn/vi/trung-thuong/ket-qua-trung-thuong/chu-nhan-jackpot)
- [Vietlott robots.txt](https://vietlott.vn/robots.txt) returned only the site name, not usable robots directives, on the check date.

The two content pages are reachable, but no public RSS/API endpoint or permitted HTML collection policy has been verified. The source adapter is intentionally blocked and makes **no outbound request**. Scheduled/manual runs therefore log a blocked result and create no article or draft. Do not enable HTML scraping until the source endpoint, access policy, and permitted reuse scope have been independently verified. The source URL allowlist and change tracker are preparatory safeguards, not evidence of permission.

Source records are unique by source key plus normalized URL hash. Tracking parameters are removed before hashing. Changed content is marked `NeedsReview`; it does not overwrite a published article. Conditional-request metadata fields are reserved for a future verified adapter.

## Editorial and SEO behavior

Only published articles whose publication time has arrived are public, appear on the homepage, or enter the sitemap. Empty categories are `noindex`; previews require the admin role and are `noindex`. Article content is stored as plain text and HTML-encoded at render time. No images are downloaded automatically. Editors may reference an existing local image under `/images/news/`.

The news routes are:

- `/tin-tuc`
- `/tin-tuc/vietlott`
- `/tin-tuc/trung-thuong-vietlott`
- `/tin-tuc/huong-dan`
- `/tin-tuc/bai-viet/{slug}`

Winner details are optional and should only be entered when stated by the cited source. In particular, draw date, source publication date, prize value, winner/ticket counts, received amount, tax treatment, and award date are separate fields.
