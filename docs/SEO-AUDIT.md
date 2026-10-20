# SEO audit: metadata, history and crawling

Validated locally with `dotnet run --project tests/SeoRegression --no-restore`: 445 checks. Tests use an isolated database fixture and real MVC routing/Razor rendering. No production crawl, live database changes, Search Console submission or external schema-validator submission was performed.

## Structured data and metadata

Breadcrumb entries use literal `@type` dictionary keys, including the home entry. Rendered JSON-LD is parsed and checked for graph types, breadcrumb positions, visible labels and the current canonical URL. Tests cover home, regions, province landing/history, games/draws, statistics and static information pages. The header brand remains a span; each template has one content H1. Historical draw titles include the draw identifier and stored draw date. Filtered titles/descriptions describe their selected content. No keywords meta tag was added.

## URL parameters

Every historical date/draw retains its own canonical. Allowed parameters are route-specific: day for region landings; gameCode/offset for Vietlott; endDate for special-prize statistics; region/week for frequency statistics. These pages remain crawlable with noindex,follow. Tracking parameters are omitted from canonical URLs. Duplicate values, unsupported combinations, noncanonical numeric offsets and non-ISO filter dates return 400. Frequent-game offsets must be multiples of three, up to 99999. The existing weekday filter remains relative to available draws and is never permanently redirected to a fixed date. Stable gameCode landing aliases retain their existing 301 to the game route.

## Sitemaps and history

Catalog metadata is cached for two minutes with a refresh gate; result pages query fresh data. Sitemap responses advertise a 120-second HTTP cache lifetime. Catalog invalidation and downstream HTTP caches can take separate time to expire. URL deduplication uses ordinal paths, and duplicate lastmod values use their maximum stored update timestamp. Missing update metadata is omitted rather than replaced with the current time. The current Vietlott schema does not establish content-update timestamps, so draw dates are not presented as lastmod.

Keno entries require 20 distinct numbers in 1–80; Bingo18 requires three values in 1–6. Incomplete frequent-game records do not enter the catalog and do not render as indexable historical results. This checks payload structure, not official verification of results. Other games require numeric result content or stored prize numbers. Original source rows remain in storage. Empty/invalid regional JSON is excluded. Landing pages fall back to an available valid historical result if incomplete recent records would otherwise hide stored content.

History links, previous/next navigation and parent links are ordinary anchors with href values. A newly arrived draw can be opened before the catalog cache refreshes, without silently substituting another draw. Invalid identifiers, invalid/future dates and missing historical results return 404. Day boundaries use UTC+7 (Asia/Ho_Chi_Minh); displayed historical dates come from stored results.

## Deployment follow-up

Run the same HTTP checks against the deployed origin and real source data. Verify public origin configuration, canonical hosts, robots/index/child sitemap HTTP responses, representative historical dates and Keno/Bingo payload quality. Review SQL execution plans and sitemap refresh cost on the production dataset. The local fixture verifies behavior and generated HTML; it does not establish production data quality or SQL Server execution plans.
