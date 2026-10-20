# Station normalization deployment

Canonical identities: HCM / TP HCM / `/xo-so-tp-hcm`; DL / Đắk Lắk / `/xo-so-dak-lak`; DNO / Đắk Nông / `/xo-so-dak-nong`.

The application works with the legacy schema before migration. All archive navigation, internal province links, canonical URLs, breadcrumbs and sitemap entries share the station catalog. Alias routes return HTTP 301, preserving the date and query string.

Run `docs/station-normalization.sql` with a SQL Server deployment tool that understands GO batches. It snapshots original identities and payloads, adds source metadata, normalizes existing rows and installs a write trigger for external collectors. No source result rows are deleted. Schedule the backfill for a maintenance window on large tables. The script supports reruns; the audit snapshot is retained. Review the final duplicate report against the original payloads in `StationNormalizationAudit`.

Collectors are outside this repository. At collection time, resolve the station from its known code/name, then upsert by (station ID, draw date, source name), preserving results from other sources. Supply `SourceName` and `SourcePriority` through trusted collector configuration. Larger priority values mean a more authoritative source. Do not infer authority from spelling or collection time. Extend `StationCatalog` and the trigger aliases together when adding another historical spelling.

The read path groups aliases by station and date. For each prize it uses source priority, then total populated number count, update time, and row ID. Missing prize groups can be supplied by a lower-priority row; conflicting arrays are not concatenated, and leading zeroes are preserved. Original conflicting rows stay available for review. Legacy rows have source `legacy-unknown` and priority 0: their true authority cannot be recovered from this schema, so verify sources before assigning higher priorities. No production database was modified by this code change.

Validation: `dotnet run --project tests/SeoRegression --no-restore`.
