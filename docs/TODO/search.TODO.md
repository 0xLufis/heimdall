# Search TODO

Scope: `src/frontend/web/app/components/search/*`, `composables/useHeimdallSearch.ts`, `utils/search/*`,
`server/api/search.get.ts`; backend `Controllers/V1/SearchController.cs`, `Services/GlobalSearchService.cs`,
`Services/SearchRanker.cs`, `Dtos/SearchDtos.cs`, migration `20261111103000_SearchTrigramIndexes`.
Tests: `tests/frontend/unit/SearchRankingNavigationAndRaces.test.ts` (+ the older search suites),
`tests/backend/App.Backend.Tests/GlobalSearchTests.cs`.

## Search engine decision (evaluated 2026-11-11)

Question: would an open-source Elasticsearch-style engine drive Heimdall search better than what is in the stack?
**Answer: no, not at this scale. Stay on PostgreSQL** (`ILIKE` substring matching, `pg_trgm` for typo tolerance and
for index support once tables grow), queried through the tenant-scoped `AppDbContext`.

"Open-source Elasticsearch" is ambiguous, so plainly: Elasticsearch has not been Apache-2.0 since 7.11 (2021). Its
source is triple-licensed AGPLv3 / SSPL / Elastic License 2.0 (AGPLv3 added August 2024); Elastic's own binaries and
Docker images are ELv2, which is not an open-source licence. The Apache-2.0 fork is **OpenSearch**.

| | OpenSearch 3.x | Meilisearch 1.x | Typesense | PostgreSQL 18 `ILIKE` + `pg_trgm` (+ FTS) |
|---|---|---|---|---|
| Licence | Apache-2.0 (Linux Foundation) | MIT (Community Edition; sharding is BUSL "Enterprise") | GPL-3.0 server, Apache-2.0 clients | PostgreSQL licence, already deployed |
| Extra runtime | JVM container, 1 GB default heap, docs ask for >= 4 GB host memory, `vm.max_map_count`, admin password bootstrap | 1 container, measured 101 MiB empty / 230-254 MiB with our 1,167 rows | 1 container, whole index held in RAM | none (the whole Postgres container uses ~160 MiB) |
| Identifier tokens (`L01-OP020`, hostnames, IPs, MACs, `TKT-F2AF7680`) | needs custom analyzers (n-gram / wildcard / keyword sub-fields) per field | splits on `-` `.` `:`; prefix on the last word only, no infix | infix only per opted-in field | substring match as typed, any position |
| Typo tolerance | `fuzziness`, per query | on by default, also on alphanumeric ids (`op020` finds `OP030`) | on by default | `pg_trgm` word similarity; applied only to digit-free tokens |
| Tenant filtering | filter field or document-level security: a second access model to keep in sync | tenant tokens (JWT with embedded filter) | scoped API keys | the existing EF global query filters, nothing new |
| Consistency with Postgres | async indexing pipeline (outbox or CDC), reindex jobs, drift handling | same | same | none: it is the source of truth |
| To build | index mappings, indexer, reindex, .NET client (`OpenSearch.Client`), ops | indexer, reindex, .NET SDK (`MeiliSearch`), ops | indexer, community .NET client, ops | one controller + service, one optional migration |

Measured on the dev data set (929 inventory rows of which 100 are stations, 56 controllers, 75 tickets, 107 users =
1,167 searchable rows; PostgreSQL 18.6 in `postgres_heimdall`, `shared_buffers` 128 MB):

- Today's size, real tables, plain `ILIKE` sequential scans, no extension: 0.13-0.47 ms execution per entity kind
  (`EXPLAIN ANALYZE`); `pgbench` over 1,000 runs: inventory 2.29 ms, stations 0.89 ms, controllers 0.34 ms, tickets
  0.35 ms, users 0.28 ms; one global search (all five statements) 4.3 ms, 2.9 ms with prepared statements.
- `pg_trgm` at today's size: 11 GIN trigram indexes build in 1-11 ms each and take 664 kB; the planner correctly keeps
  using sequential scans. A typo query (`word_similarity('siemns', ...)`) takes 6.4 ms.
- Growth, measured on scaled copies in a scratch database (execution time; "trgm" = GIN trigram indexes available):

  | query | 1x seq | 100x trgm | 100x seq | 1000x trgm | 1000x seq |
  |---|---|---|---|---|---|
  | inventory `siemens` (9% of rows match, top 20) | 1.8 ms | 19 ms | 211 ms | 156 ms | 1,979 ms |
  | inventory serial number (selective) | 1.8 ms | 2.7 ms | 264 ms | 23 ms | 2,387 ms |
  | inventory two tokens (`siemens s7-1500`) | - | 14 ms | 233 ms | 185 ms | 2,115 ms |
  | station identifier (join of two tables, not indexable as written) | 0.4 ms | 32 ms | 39 ms | 187 ms | 733 ms |
  | controller hostname / IP prefix | 0.15 ms | 9 ms | 10 ms | 1.3-2.0 ms | 101-116 ms |
  | ticket title + description | 0.13 ms | 0.07 ms | 9.4 ms | 0.13 ms | 86 ms |
  | typo `siemns`, `<%` operator | 6.0 ms | 89 ms | 723 ms | 420 ms | 7,890 ms |
  | typo token + literal token (selective) | - | 0.3 ms | 714 ms | 2.8 ms | 8,024 ms |

  100x = 92,900 inventory rows / 10,000 stations / 5,600 controllers / 7,500 tickets / 10,700 users, 1000x = ten times
  that. Trigram indexes: 30 MB at 100x, about 210 MB at 1000x; building one takes 0.02-0.6 s at 100x and 2-6 s at 1000x.
- PostgreSQL full-text search alone is the wrong tool for these tokens: the parser turns `S7-1500` into `s7` and
  `-1500`, a MAC address into two-digit numbers, an IP address into one opaque token; it has no infix match (`p020`,
  `168.101`) and no typo tolerance. It is only worth adding for long natural-language text (ticket comments, manuals).
- Meilisearch 1.56.2 (the one dedicated engine run, as a throwaway container that was removed afterwards): starts in
  0.33 s, indexes the 1,167 rows in 50-71 ms into a 2 MB index, answers in 0-2 ms.
- Relevance on 25 representative queries over the same rows (top 20; "exact" = right first hit, every expected row,
  no unrelated row): PostgreSQL substring AND-matching + trigram typo fallback + application ranking **25/25**;
  Meilisearch defaults 10/25 (14 queries return unrelated rows: an exact IP or hostname lists every controller,
  `op020` also lists OP010 / OP030, a ticket number lists every ticket; `p020` finds nothing); Meilisearch tuned
  (`matchingStrategy: all`, `typoTolerance.disableOnNumbers`) 17/25; additionally treating `-` `.` `:` as
  non-separators 11/25 (partial identifiers stop matching).

Why PostgreSQL: latency is already a few milliseconds without any index; the queries are identifier fragments, which
substring matching answers exactly and token-based engines answer worse until they are tuned per field; tenant
scoping stays in the one place it is enforced today; there is no second store to feed, monitor and keep consistent.

When a dedicated engine becomes justified: a searchable table approaching ~1M rows with unselective,
relevance-ranked queries (150-190 ms measured at 929k rows, and it grows linearly with the match count); search over
telemetry / event / audit streams or dashboards with facets and aggregations (that is OpenSearch's home ground);
language-aware search over long documents beyond what `tsvector` gives; semantic / vector search. If that day comes,
Meilisearch is the cheaper fit for interactive entity search and OpenSearch for event analytics; either needs an
outbox-driven indexer, a reindex job and an organization filter on every document, with PostgreSQL kept as the
source of truth and the tenant check repeated on the rows that are returned.

## Done
- [x] Server-side search: `GET /api/v1/search?q=&kinds=&limit=` (BFF: `GET /api/search`) returns stations,
  controllers, tickets, users and inventory in one request. Every query runs through the tenant-scoped
  `AppDbContext`, so the EF organization filters apply; platform administrators stay unscoped. Matching is
  case-insensitive substring per token (`ILIKE`), AND over tokens; candidates are ranked by `SearchRanker`, a port
  of `searchRanking.ts`, so server and browser order results the same way. No extension is needed.
- [x] Users are a search kind. They are returned only to callers who may administer users (SystemAdministration,
  ItAdministration or EngineeringAdministration policy) and only members of the caller's organization; the user
  directory is never downloaded to the browser for searching.
- [x] The browser no longer downloads the station / controller / ticket catalogues to search. They remain as the
  fallback when the endpoint cannot be reached (`searchSources.ts`, cached 30 s), and for a scope tag without any
  text (`@machine` alone lists the stations).
- [x] Typo tolerance on the server is a fallback for a kind that has no literal match: digit-free tokens of three
  or more characters are compared by edit distance (same rule as the browser). Kinds up to 2,000 rows are scanned;
  larger ones use `pg_trgm` to narrow the candidates when the extension is installed, otherwise the response says
  `typoTolerance: "partial"`.
- [x] Migration `20261111103000_SearchTrigramIndexes` (hand-written, idempotent, no model change): `pg_trgm` plus GIN
  trigram indexes on `inventory_items(name, display_name, serial_number, storage_location)`. Optional today; see
  "Search engine decision" for when it starts to matter.
- [x] Stale responses are discarded (sequence guard); clearing or shortening the query cancels in-flight results.
- [x] Ranking (`searchRanking.ts`): AND over query tokens, best field wins, exact > prefix > word > substring > typo;
  tokens containing digits are never typo-matched (OP20 is not OP30).
- [x] Every result deep-links to its entity (`searchNavigation.ts`): station workbench (`?search=&tree=`), controller
  (`?selected=&hostname=`), `/dashboard/tickets/<id>`, `/dashboard/users/<id>`, `/dashboard/inventory/<id>`.
- [x] Submitted queries route by explicit scope / identifier shape (`resolveSubmitRoute`) with the parameter each
  list page reads: machines `?search=`, clients `?hostname=`, tickets `?query=`, inventory `?query=`. Id-derived
  ticket numbers (`TKT-F2AF7680`) are recognised; they used to be sent to the inventory page.
- [x] Scope tags restrict which entity kinds are searched: `@machine`, `!controller`, `#ticket`, `*part`, `user:`,
  and `ip:` / `mac:` (controllers, with the address as the term).
- [x] Tags no source can filter on (`spec:`, `cost:`, `category:` ...) narrow the search as terms. They used to be
  passed to the inventory search, which ignored them and returned 50 unfiltered rows; `mfr:` is now sent as
  `manufacturer:`, the key the backend understands.
- [x] A station is no longer listed twice (the inventory search also returns station rows, which linked to the
  inventory page).
- [x] Dropdown: results grouped by entity type with counts, XSS-safe token highlighting, loading / no-match / error
  (retry) / partial-outage states, recent searches (localStorage), combobox + listbox ARIA, active item scrolled into view.
- [x] Keyboard: Enter opens the highlighted entity, Ctrl/Cmd+Enter opens it in a new tab, Tab only completes a
  suggestion (no focus trap); paste / cut / Ctrl+K are no longer intercepted.
- [x] Removed unused `AdvancedSearchUI.vue` and `useSearch.ts`.

## Open
- [ ] Apply the migration when wanted: `dotnet ef database update --project src/shared/App.Shared` (or the usual
  `just` target). Not applied to the dev database; the search works without it.
- [ ] Inventory filter tags (`mfr:`, `status:`, `type:` ...) still go through the older `GET /api/v1/inventory/search`,
  which has no ranking and no deterministic order; fold them into `/api/v1/search` as filters.
- [ ] Ticket search is not index-backed (number, title, station, assignee, tags, alarm code, description are OR-ed
  across a join). Fine for tens of thousands of tickets (86 ms measured at 75,000 without indexes); beyond that add
  trigram indexes on `title` / `description` and resolve the ticket number as an id-prefix range.
- [ ] `/dashboard/users` has no query parameter: a submitted `user:` search opens the directory unfiltered.
- [ ] `TagContextSearch.vue` / `useTagContextSearch.ts` are covered by tests but not mounted on any page: adopt or remove.
- [ ] Not search code, found while reading, for the owner: `GET /api/v1/auth/users` lists every user without a
  tenant filter; `ClientPcController.GetClientPcs` and the cached `InventoryController` reads use Redis keys without
  the organization, and the BFF ticket list keeps one in-memory store for all sessions. The fallback catalogues of the
  search read exactly those endpoints; the new endpoint does not.
