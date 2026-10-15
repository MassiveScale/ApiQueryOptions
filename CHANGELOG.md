# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Security

- **`$filter` nesting and size limits** — deeply nested parentheses no longer recurse until the process crashes with a stack overflow. New `MaxFilterDepth` (default `32`) and `MaxFilterNodeCount` (default `100`) settings on `ApiQueryOptionsSettings` and `ApiQueryOptionsBuilder` reject larger expressions with a `FilterParseException`. `FilterParser` gains a `FilterParser(int maxDepth, int maxNodeCount)` constructor, and `FilterQueryOption` gains a `FilterQueryOption(string, ApiQueryOptionsSettings)` constructor.

### Fixed

- **Skip tokens with custom parameter names** — a `$skiptoken` now restores `$filter`, `$orderby`, `$top`, `$skip`, and `$expand` no matter which `*ParameterNames` are configured. Previously the second page lost its state when, for example, `FilterParameterNames` was `["q"]`.
- **`[ApiQueryOptions]` attribute kept custom parameter names** — `ApplyTo` reset every `*ParameterNames` list to its default. It now copies all settings it does not override.
- **`NextLink(int)` on model-bound options returns a full URL** — the model binder now builds options from the `HttpRequest`, matching the README quick start. Before, it returned only the bare token. The binder also caches its factory per type and the attribute-resolved settings per action instead of using reflection on every request.
- **Malformed `$skiptoken` returns `400 Bad Request`** — the model binder adds a `ModelState` error (keyed by the skip-token parameter name) instead of throwing a `TargetInvocationException` that surfaced as `500`.
- **`FilterQueryOption.FilterClause` is thread-safe** — concurrent first reads could see `null` while another thread was still parsing.
- **EF Core query-plan cache pollution** — `$filter` values are now sent as SQL parameters instead of inlined literals, so EF Core and the database reuse one plan per filter shape.
- **Next-page cursor overflow** — a `$skip` near `int.MaxValue` no longer wraps to a negative cursor; no next link is produced instead.

### Changed

- **Breaking:** the EF Core `Apply` extension is renamed to `ApplyWithExpand`. Importing both `ApiQueryOptions.Extensions` and `ApiQueryOptions.EntityFrameworkCore.Extensions` in the same file caused an ambiguous call error (CS0121).
- **Breaking:** when `SkipTokenEnabled` is `false`, `NextLink` builds an offset link (`$top` and the next `$skip`, other parameters forwarded) instead of a `$skiptoken` the server would ignore. It returns `null` when `$skip` is also disabled. `GetNextToken`, and `NextLink(int)` on an instance without a request, return `null` when skip tokens are disabled.
- **Breaking:** `MaxPageSize`, `DefaultPageSize`, `MaxFilterDepth`, and `MaxFilterNodeCount` throw `ArgumentOutOfRangeException` when set to zero or a negative value. `TopQueryOption` rejects values below `1` and `SkipQueryOption` rejects negative values. A request with `$top` of zero or less, or a negative `$skip`, is treated as if the parameter were absent, the same as a non-numeric value.
- **Breaking:** `ApiQueryOptionsModelBinder` throws `ArgumentException` when constructed with a type that is not a closed `ApiQueryOptions<T>`.

## [1.0.0-beta5] - 2026-06-14

### Fixed

- **EF Core LINQ translation for case-insensitive string comparisons** — `$filter` expressions with case-insensitive string equality and string functions (startswith, endswith, contains) now correctly translate to SQL.  
  
## [1.0.0-beta4] - 2026-06-12

(Version bump — no user-facing changes)

## [1.0.0-beta3] - 2026-06-12

### Added

- **`PagedResponse<T>`** — paged response envelope that pairs a result page with a `nextLink` cursor URL. `PagedResponse.Create` strips all ApiQueryOptions-owned query parameters from the current request and replaces them with a single `$skiptoken`, forwarding any other parameters unchanged.
- **Configurable query parameter names** — each option now exposes a `*ParameterNames` property (`FilterParameterNames`, `TopParameterNames`, `SkipParameterNames`, `SkipTokenParameterNames`, `OrderByParameterNames`, `ExpandParameterNames`) on both `ApiQueryOptionsSettings` and `ApiQueryOptionsBuilder`. Defaults preserve the existing OData pair (`["$filter", "filter"]`, etc.) so there is no breaking change. Names are tried left-to-right and the first match wins. Custom names are automatically excluded from the `NextLink` query-string passthrough.
- **`ApiQueryOptions.FromRequest<T>(HttpContext?, ...)`** overload — returns an empty no-op instance when the context is `null`, safe for use in middleware and services where `HttpContext` may be absent.
- **`ApiQueryOptions.FromRequest<T>(IHttpContextAccessor, ...)`** overload — resolves `HttpContext` from the accessor and delegates to the nullable overload.

### Changed

- `ApiQueryOptions<T>` constructor parameter `query` is now typed `IQueryCollection?` (was non-nullable). Passing `null` produces an empty instance with no parsed options, matching pre-existing null-guard behavior.
