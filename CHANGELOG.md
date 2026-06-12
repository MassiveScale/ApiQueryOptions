# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.0.0-beta3] - 2026-06-12

### Added

- **`PagedResponse<T>`** — paged response envelope that pairs a result page with a `nextLink` cursor URL. `PagedResponse.Create` strips all ApiQueryOptions-owned query parameters from the current request and replaces them with a single `$skiptoken`, forwarding any other parameters unchanged.
- **Configurable query parameter names** — each option now exposes a `*ParameterNames` property (`FilterParameterNames`, `TopParameterNames`, `SkipParameterNames`, `SkipTokenParameterNames`, `OrderByParameterNames`, `ExpandParameterNames`) on both `ApiQueryOptionsSettings` and `ApiQueryOptionsBuilder`. Defaults preserve the existing OData pair (`["$filter", "filter"]`, etc.) so there is no breaking change. Names are tried left-to-right and the first match wins. Custom names are automatically excluded from the `NextLink` query-string passthrough.
- **`ApiQueryOptions.FromRequest<T>(HttpContext?, ...)`** overload — returns an empty no-op instance when the context is `null`, safe for use in middleware and services where `HttpContext` may be absent.
- **`ApiQueryOptions.FromRequest<T>(IHttpContextAccessor, ...)`** overload — resolves `HttpContext` from the accessor and delegates to the nullable overload.

### Changed

- `ApiQueryOptions<T>` constructor parameter `query` is now typed `IQueryCollection?` (was non-nullable). Passing `null` produces an empty instance with no parsed options, matching pre-existing null-guard behavior.
