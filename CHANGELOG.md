# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.2.0] - YYYY-MM-DD

### Added

- **`options.Expands(propertyPath)`** on `ApiQueryOptions<T>` and **`Includes(propertyPath)`** on `ExpandQueryOption` — return whether `$expand` asked for a property, so a projection can load a related collection only when requested. Matching is case-insensitive, `$expand=lines.product` counts as asking for both `Lines` and `Lines.Product`, and `Expands` returns `false` when there is no `$expand`.

### Changed

- **`$expand` on projected queries** — `Apply` and `ApplyExpand` no longer throw EF Core's `InvalidOperationException` ("Cannot apply the 'Include' operation…") when the query has been projected with `Select`, to a DTO or to a related entity. They detect the projection, include nothing, and still apply `$filter`, `$orderby`, `$skip`, and `$top`. Each `$expand` path must still name a property of the projected type. `options.Expand` and `nextLink` keep the `$expand` the client sent. Entity queries are included as before.
- **`$expand` validation on entity queries** — paths are checked against the navigations in the EF Core model instead of the CLR properties, including navigations on derived types and many-to-many navigations. A path that names a plain column, such as `$expand=Title`, now throws `InvalidQueryOptionException` while the query is built, instead of an `InvalidOperationException` from EF Core when it runs.

## [1.1.0] - YYYY-MM-DD

### Added

- **Filtered counts** — `query.Count(options)` in the core package and `await query.CountAsync(options, cancellationToken)` in the EF Core package count every item that matches `$filter`, across all pages, ignoring `$orderby`, `$skip`, `$top`, and `$expand`. Calling `.Count()` on the result of `Apply` counts only the current page. The result is the `totalCount` to pass to `PagedResponse.Create`.
- **`ApplyFilter(options)`** — applies only the `$filter` from an `ApiQueryOptions<T>`, and returns the query unchanged when there is no filter. `Apply`, `Count`, and `CountAsync` all filter through it, so the count always matches the rows the pages come from.

### Fixed

- **README count examples** — the paging examples counted the whole table instead of the filtered items, which gave the wrong total whenever `$filter` was set.

## [1.0.0] - YYYY-MM-DD

First stable release. Everything below changed since `1.0.0-beta5`.

### Security

- **`$filter` nesting and size limits** — deeply nested parentheses no longer recurse until the process crashes with a stack overflow. New `MaxFilterDepth` (default `32`) and `MaxFilterNodeCount` (default `100`) settings on `ApiQueryOptionsSettings` and `ApiQueryOptionsBuilder` reject larger expressions with a `FilterParseException`. `FilterParser` gains a `FilterParser(int maxDepth, int maxNodeCount)` constructor, and `FilterQueryOption` gains a `FilterQueryOption(string, ApiQueryOptionsSettings)` constructor.

### Added

- **`StringMatching` setting** — controls how `$filter` compares strings: `IgnoreCase` (the default and the previous behavior), `CaseSensitive`, or `Database`. `Database` sends plain `=`, `LIKE`, and comparison operators with no `UPPER()`, so the column collation decides case sensitivity and the database can use an index. Available on `ApiQueryOptionsSettings`, `ApiQueryOptionsBuilder`, and `[ApiQueryOptions]`.
- **`TieBreakerProperty` setting** — names a unique column, such as `Id`, that `Apply` appends to the sort whenever `$top` or `$skip` is applied and `$orderby` does not already include it. Without it, rows with equal sort values can move between pages. Default `null` (nothing added). Available on `ApiQueryOptionsSettings`, `ApiQueryOptionsBuilder`, and `[ApiQueryOptions]`.
- **`ApiQueryOptionsException`** — a base type for every exception caused by bad client input, so one `catch` can return `400 Bad Request`. `FilterParseException` and the new `InvalidQueryOptionException` derive from it. `InvalidQueryOptionException` exposes `OptionName` and `Property`.
- **More `$filter` value types** — strings convert to `Guid`, `DateTime`, `DateTimeOffset`, `DateOnly`, `TimeOnly`, `TimeSpan`, and enums (member name in any case, or its number). Integer literals outside the `int` range are read as `long`, then `decimal`.
- **`lt`, `gt`, `le`, and `ge` on strings**, translated by EF Core into SQL comparisons and following `StringMatching`.
- **Symbol packages** — both packages publish `.snupkg` symbols, and CI builds are deterministic.

### Fixed

- **Case-insensitive matching on `null` values** — a `null` string property, or `Name eq null`, threw `NullReferenceException` in memory and failed on EF Core. `Name eq null` is now a plain null check, and rows with a `null` value never match a string function.
- **Case-insensitive matching under other cultures** — matching is culture-invariant. Under `tr-TR`, `'istanbul'` now matches `Istanbul`.
- **`StringComparison` values other than `OrdinalIgnoreCase` were case-sensitive** — `CurrentCultureIgnoreCase` and `InvariantCultureIgnoreCase` were silently treated as `Ordinal`. The setting is replaced by `StringMatching`.
- **Fractional values on integer properties** — `Age eq 1.5` was rounded and matched `Age` 2. It now throws `InvalidQueryOptionException`, as do values that overflow the property type.
- **Culture-dependent value conversion** — filter values are converted with the invariant culture, so results no longer depend on the server's culture.
- **Unterminated string literals** — `Name eq 'Alp` was silently accepted. It now throws `FilterParseException` with the position of the opening quote.
- **`$expand` with an unknown path** — failed inside EF Core when the query ran. Each segment is now checked before `.Include()` is called, unknown segments throw `InvalidQueryOptionException`, and names are matched in any case.
- **Properties that differ only by case, and indexers** — `GetProperty` threw `AmbiguousMatchException` or picked an indexer. An exact-case match is now preferred, and indexers are ignored.
- **`AddApiQueryOptions` called more than once** — inserted the model binder provider each time. The provider is now registered once and the settings from the last call win.
- **Assembly versions in released packages** — the publish workflow now passes the release version to the build, so the DLL file and informational versions match the package version.
- **Skip tokens with custom parameter names** — a `$skiptoken` now restores `$filter`, `$orderby`, `$top`, `$skip`, and `$expand` no matter which `*ParameterNames` are configured. Previously the second page lost its state when, for example, `FilterParameterNames` was `["q"]`.
- **`[ApiQueryOptions]` attribute kept custom parameter names** — `ApplyTo` reset every `*ParameterNames` list to its default. It now copies all settings it does not override.
- **`NextLink(int)` on model-bound options returns a full URL** — the model binder now builds options from the `HttpRequest`, matching the README quick start. Before, it returned only the bare token. The binder also caches its factory per type and the attribute-resolved settings per action instead of using reflection on every request.
- **Malformed `$skiptoken` returns `400 Bad Request`** — the model binder adds a `ModelState` error (keyed by the skip-token parameter name) instead of throwing a `TargetInvocationException` that surfaced as `500`.
- **`FilterQueryOption.FilterClause` is thread-safe** — concurrent first reads could see `null` while another thread was still parsing.
- **EF Core query-plan cache pollution** — `$filter` values are now sent as SQL parameters instead of inlined literals, so EF Core and the database reuse one plan per filter shape.
- **Next-page cursor overflow** — a `$skip` near `int.MaxValue` no longer wraps to a negative cursor; no next link is produced instead.

### Changed

- **Breaking:** `ApiQueryOptionsSettings.StringComparison` and `ApiQueryOptionsBuilder.StringComparison` are removed. Use `StringMatching` instead: `OrdinalIgnoreCase` becomes `StringMatching.IgnoreCase` (the default), and `Ordinal` becomes `StringMatching.CaseSensitive`.
- **Breaking:** a `$filter` or `$orderby` that names an unknown property throws `InvalidQueryOptionException` instead of `InvalidOperationException`. A value that cannot be converted to the property type, a string function on a non-string property, `null` compared with a non-nullable value type, and an operator the type does not support also throw `InvalidQueryOptionException` instead of `FormatException`, `InvalidCastException`, `OverflowException`, `ArgumentException`, or `InvalidOperationException`. Catch `ApiQueryOptionsException` to handle all client errors.
- **Breaking:** `FilterParseException` derives from `ApiQueryOptionsException` instead of directly from `Exception`.
- **Breaking:** `lt`, `gt`, `le`, and `ge` with `null` throw `InvalidQueryOptionException`. Before, they matched nothing, or threw `InvalidOperationException` for strings.
- **Breaking:** query keys are looked up with the `IQueryCollection`'s own key comparison. `HttpRequest.Query` is case-insensitive, so requests behave as before; a `QueryCollection` built over a case-sensitive dictionary now needs exact-case keys.
- Next-page links percent-encode query parameter names that contain `&`, `=`, `+`, `#`, `%`, or other characters that would change their meaning. Before, such names were copied unencoded and produced a broken link. All other link output is unchanged.
- Property lookups, string method lookups, and the validated `*ParameterNames` set are cached instead of being rebuilt for every request.
- The packages' shared metadata moved to `src/Directory.Build.props`. Both packages are versioned `1.0.0`.
- **Breaking:** the EF Core package no longer has its own `Apply` extension. The core `Apply` now applies `$expand` as `.Include()` calls whenever the `ApiQueryOptions.EntityFrameworkCore` package is installed, with no extra `using` needed. Before, importing both `ApiQueryOptions.Extensions` and `ApiQueryOptions.EntityFrameworkCore.Extensions` in the same file caused an ambiguous call error (CS0121). Importing only the core namespace silently ignored `$expand`. Code that imported only the EF Core namespace needs `using ApiQueryOptions.Extensions;`. `ApplyExpand` is unchanged.
- The core `Apply` throws `ArgumentNullException` when `options` is `null`.
- **Breaking:** when `SkipTokenEnabled` is `false`, `NextLink` builds an offset link (`$top` and the next `$skip`, other parameters forwarded) instead of a `$skiptoken` the server would ignore. It returns `null` when `$skip` is also disabled. `GetNextToken`, and `NextLink(int)` on an instance without a request, return `null` when skip tokens are disabled.
- **Breaking:** `MaxPageSize`, `DefaultPageSize`, `MaxFilterDepth`, and `MaxFilterNodeCount` throw `ArgumentOutOfRangeException` when set to zero or a negative value. `TopQueryOption` rejects values below `1` and `SkipQueryOption` rejects negative values. A request with `$top` of zero or less, or a negative `$skip`, is treated as if the parameter were absent, the same as a non-numeric value.
- **Breaking:** `ApiQueryOptionsModelBinder` throws `ArgumentException` when constructed with a type that is not a closed `ApiQueryOptions<T>`.

### Removed

- **Breaking:** the `Flurl` package dependency. Next-page links are built with ASP.NET Core's `UriHelper` instead. Projects that used Flurl through this package need their own reference to it.

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
