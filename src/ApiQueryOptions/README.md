# ApiQueryOptions

[![NuGet](https://img.shields.io/nuget/v/ApiQueryOptions.svg)](https://www.nuget.org/packages/ApiQueryOptions)
[![CI](https://github.com/MassiveScale/ApiQueryOptions/actions/workflows/ci.yml/badge.svg)](https://github.com/MassiveScale/ApiQueryOptions/actions/workflows/ci.yml)

Lightweight OData-style query parameter parsing for ASP.NET Core REST APIs — without the overhead of a full OData stack.

Parses `$filter`, `$expand`, `$orderby`, `$top`, `$skip`, and `$skiptoken` from HTTP query strings and applies them to any `IQueryable<T>` as deferred LINQ expression trees. Nothing is enumerated until you call `.ToList()` or `.ToListAsync()`.

For EF Core support (`$expand` → `.Include()`), see [ApiQueryOptions.EntityFrameworkCore](https://www.nuget.org/packages/ApiQueryOptions.EntityFrameworkCore).

---

## Installation

```shell
dotnet add package ApiQueryOptions
```

---

## Quick start

### 1. Register in `Program.cs`

```csharp
builder.Services.AddControllers();
builder.Services.AddApiQueryOptions();
```

Restrict options globally:

```csharp
builder.Services.AddApiQueryOptions(new ApiQueryOptionsSettings
{
    ExpandEnabled    = false,
    SkipTokenEnabled = false,
    MaxPageSize      = 100,
    DefaultPageSize  = 25,
});
```

### 2. Accept options in a controller action

```csharp
[HttpGet]
public IActionResult Get(ApiQueryOptions<Product> options)
{
    List<Product> items = _db.Products.Apply(options).ToList();
    return Ok(new { value = items, nextLink = options.NextLink(items.Count) });
}
```

### 3. Override settings per action or controller

```csharp
[ApiController]
[Route("api/[controller]")]
[ApiQueryOptions(DefaultPageSize = 20, MaxPageSize = 100)]
public class ProductsController : ControllerBase
{
    [HttpGet]
    public IActionResult Get(ApiQueryOptions<Product> options) { ... }

    // Method-level attribute wins over controller-level
    [HttpGet("archive")]
    [ApiQueryOptions(MaxPageSize = 500, FilterEnabled = QueryOptionState.Disabled)]
    public IActionResult GetArchive(ApiQueryOptions<Product> options) { ... }
}
```

The attribute can also set `StringMatching` and `TieBreakerProperty`. Unset properties (`0` for integers, `null` for `TieBreakerProperty`, `QueryOptionState.Default` for toggles, and a `StringMatching` that is never assigned) inherit from the registered settings unchanged.

---

## Supported query parameters

Both the `$`-prefixed OData form (`$filter=...`) and the bare form (`filter=...`) are accepted.

| Parameter    | Type    | Description                                    |
| ------------ | ------- | ---------------------------------------------- |
| `$filter`    | string  | Boolean filter expression                      |
| `$expand`    | string  | Comma-delimited navigation property paths      |
| `$orderby`   | string  | Sort list: `Name asc, CreatedAt desc`          |
| `$top`       | integer | Maximum results to return                      |
| `$skip`      | integer | Results to skip (offset-based paging)          |
| `$skiptoken` | string  | Opaque cursor token (cursor-based paging)      |

---

## Filter syntax

### Comparison operators

| Operator | Meaning               | Example                |
| -------- | --------------------- | ---------------------- |
| `eq`     | Equals                | `Status eq 'Active'`   |
| `ne`     | Not equals            | `Status ne 'Deleted'`  |
| `lt`     | Less than             | `Age lt 30`            |
| `gt`     | Greater than          | `Price gt 9.99`        |
| `le`     | Less than or equal    | `Score le 100`         |
| `ge`     | Greater than or equal | `Score ge 0`           |

String comparisons, including `lt`, `gt`, `le`, and `ge`, follow the `StringMatching` setting (default: case-insensitive). See [String matching](#string-matching).

### Logical operators

```
$filter=Age gt 18 and Age lt 65
$filter=Status eq 'Active' or Status eq 'Pending'
$filter=(Region eq 'US' or Region eq 'CA') and IsActive eq true
```

### String functions

| Function                    | Example                         |
| --------------------------- | ------------------------------- |
| `startswith(prop, 'value')` | `startswith(Name, 'Al')`        |
| `endswith(prop, 'value')`   | `endswith(Email, '.com')`       |
| `contains(prop, 'value')`   | `contains(Description, 'sale')` |

### Literal types

| Type          | Example                                  | Notes                                                                                                                  |
|---------------|------------------------------------------|------------------------------------------------------------------------------------------------------------------------|
| String        | `'O''Brien'`                             | Escape `'` as `''`                                                                                                     |
| Integer       | `42`, `-5`, `5000000000`                 | Values outside the `int` range are read as `long`, then `decimal`                                                      |
| Decimal       | `9.99`                                   | Rejected for an integer property instead of being rounded                                                              |
| Boolean       | `true`, `false`                          |                                                                                                                        |
| Null          | `null`                                   | Only with `eq` and `ne`                                                                                                |
| Date and time | `'2024-01-15'`, `'2024-01-15T10:30:00Z'` | Written as a string; read as `DateTime`, `DateTimeOffset`, `DateOnly`, `TimeOnly`, or `TimeSpan` to match the property |
| GUID          | `'5f0c2b8e-8f3a-4c51-9d6e-2a7b1c0d9e4f'` | Written as a string                                                                                                    |
| Enum          | `'Active'`, `1`                          | Member name in any case, or its number                                                                                 |

Values are converted to the property's type with the invariant culture. A value that does not fit, such as Age eq 1.5 on an int property, throws InvalidQueryOptionException.


### Dotted property paths

```
$filter=Address.City eq 'Seattle'
```

---

## Pagination

### Offset-based (`$top` + `$skip`)

```
GET /api/products?$top=25&$skip=0
GET /api/products?$top=25&$skip=25
```

### Cursor-based with `NextLink`

`NextLink` encodes the current query state into a Base64URL `$skiptoken` and returns it as the next-page cursor. Returns `null` on the last page.

```csharp
[HttpGet]
public IActionResult Get(ApiQueryOptions<Product> options)
{
    List<Product> items = _db.Products.Apply(options).ToList();
    int total = _db.Products.Count(options);  // every item matching $filter, across all pages

    return Ok(new
    {
        value    = items,
        nextLink = options.NextLink(items.Count, total),  // null when no more pages
    });
}
```

`Count(options)` applies only `$filter`, so it returns the total number of matching items. Calling `.Count()` on the result of `Apply` would count only the current page. On EF Core, use `CountAsync(options)` from the [ApiQueryOptions.EntityFrameworkCore](https://www.nuget.org/packages/ApiQueryOptions.EntityFrameworkCore) package so the count does not block a thread.

The client passes the returned cursor as `$skiptoken` on the next request:

```
GET /api/products?$skiptoken=<token>
```

When `SkipTokenEnabled` is `false`, `NextLink` returns an offset link instead (`$top` and `$skip` set for the next page, all other parameters forwarded), or `null` if `$skip` is also disabled. A malformed `$skiptoken` on a model-bound action is added to `ModelState`, so an `[ApiController]` returns `400 Bad Request`.

---

## IQueryable extensions

```csharp
using ApiQueryOptions.Extensions;

IQueryable<T> result = query.Apply(options);            // filter → orderby → skip → top → expand*

// Call these on the unpaged query; they do not remove a Skip or Take that is already applied.
IQueryable<T> filtered = query.ApplyFilter(options);    // $filter only; unchanged when there is none
int total = query.Count(options);                       // items matching $filter, across all pages

query = query.ApplyFilter(options.Filter!, settings);
query = query.ApplyOrderBy(options.OrderBy!, settings);
query = query.ApplySkip(options.Skip!, settings);
query = query.ApplyTop(options.Top!, settings);
```

\* `$expand` is applied as `.Include()` calls when the [ApiQueryOptions.EntityFrameworkCore](https://www.nuget.org/packages/ApiQueryOptions.EntityFrameworkCore) package is installed and the query is a query of entities, and ignored otherwise. When you project to a DTO, load related data in the projection and check `options.Expands("PropertyName")`, which returns `true` when the client asked for that property and `false` when there is no `$expand`.

---

## Configuration (`ApiQueryOptionsSettings`)

All properties use `init`-only setters and default to the most permissive values.

| Property                  | Type                    | Default                       | Description                                      |
|---------------------------|-------------------------|-------------------------------|--------------------------------------------------|
| `FilterEnabled`           | `bool`                  | `true`                        | Accept `$filter`                                 |
| `ExpandEnabled`           | `bool`                  | `true`                        | Accept `$expand`                                 |
| `OrderByEnabled`          | `bool`                  | `true`                        | Accept `$orderby`                                |
| `TopEnabled`              | `bool`                  | `true`                        | Accept `$top`                                    |
| `SkipEnabled`             | `bool`                  | `true`                        | Accept `$skip`                                   |
| `SkipTokenEnabled`        | `bool`                  | `true`                        | Accept `$skiptoken`                              |
| `MaxPageSize`             | `int?`                  | `null`                        | Upper limit for `$top`; silently clamps          |
| `DefaultPageSize`         | `int?`                  | `null`                        | Default `$top` when none provided                |
| `MaxFilterDepth`          | `int`                   | `32`                          | Max parenthesis nesting in `$filter`             |
| `MaxFilterNodeCount`      | `int`                   | `100`                         | Max comparisons, functions, and `and`/`or` nodes |
| `StringMatching`          | `StringMatching`        | `IgnoreCase`                  | How `$filter` compares strings                   |
| `TieBreakerProperty`      | `string?`               | `null`                        | Unique sort column appended when paging          |
| `FilterParameterNames`    | `IReadOnlyList<string>` | `["$filter", "filter"]`       | Query keys recognised as `$filter`               |
| `ExpandParameterNames`    | `IReadOnlyList<string>` | `["$expand", "expand"]`       | Query keys recognised as `$expand`               |
| `OrderByParameterNames`   | `IReadOnlyList<string>` | `["$orderby", "orderby"]`     | Query keys recognised as `$orderby`              |
| `TopParameterNames`       | `IReadOnlyList<string>` | `["$top", "top"]`             | Query keys recognised as `$top`                  |
| `SkipParameterNames`      | `IReadOnlyList<string>` | `["$skip", "skip"]`           | Query keys recognised as `$skip`                 |
| `SkipTokenParameterNames` | `IReadOnlyList<string>` | `["$skiptoken", "skiptoken"]` | Query keys recognised as `$skiptoken`            |

The page sizes and filter limits must be positive; zero or a negative value throws `ArgumentOutOfRangeException`. A `$top` of zero or less, or a negative `$skip`, is ignored like an absent parameter. A `$filter` that exceeds either filter limit throws `FilterParseException`.

When an option is disabled, any matching query key is silently ignored and the corresponding property on `ApiQueryOptions<T>` will be `null`.

`*ParameterNames` lists are tried left-to-right; the first match wins. Custom names are automatically excluded from `NextLink` query-string passthrough.

### String matching
| Value                  | In memory                            | Database providers (EF Core)                                                                                         |
|------------------------|--------------------------------------|----------------------------------------------------------------------------------------------------------------------|
| `IgnoreCase` (default) | `StringComparison.OrdinalIgnoreCase` | `UPPER(column)` compared with a parameter upper-cased on the server. The database cannot use an index on the column. |
| `CaseSensitive`        | `StringComparison.Ordinal`           | Plain comparison. Case-sensitive only when the column collation is.                                                  |
| `Database`             | Plain .NET operators and methods     | Plain comparison. The column collation decides, and indexes can be used.                                             |

`IgnoreCase` is culture-invariant and null-safe: rows with a `null` value never match a string function or an ordering operator, and `Name eq null` becomes a plain null check.

On EF Core, `IgnoreCase` wraps the column in `UPPER()`, which stops the database from using an index on it. For large tables, give the column a case-insensitive collation and use `StringMatching.Database`.

### Stable paging

Paging is only stable when the sort order is unique; otherwise rows with equal sort values can move between pages. Set `TieBreakerProperty` to a unique column such as `Id`. When `Apply` applies `$top` or `$skip`, it appends that column to the sort (or sorts by it alone when there is no `$orderby`), unless `$orderby` already includes it. An unknown tie-breaker throws `InvalidQueryOptionException`.

---

## Error handling

| Exception                      | Base type                   | When                                                                                                                                                                                   |
|--------------------------------|-----------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `FilterParseException`         | `ApiQueryOptionsException`  | The `$filter` string has a syntax error or exceeds a filter limit. Exposes `RawFilter` and `Position`.                                                                                 |
| `InvalidQueryOptionException`  | `ApiQueryOptionsException`  | An option names an unknown property, uses a value that does not fit the property type, or uses an operator or function the type does not support. Exposes `OptionName` and `Property`. |
| `QueryOptionDisabledException` | `InvalidOperationException` | An `Apply*` method is called directly against a disabled option. This is a programming error, not bad client input.                                                                    |
| `FormatException`              | `SystemException`           | `SkipTokenEncoder.Decode` receives a malformed or tampered token.                                                                                                                      |

Every exception caused by bad client input derives from ApiQueryOptionsException, so one catch (ApiQueryOptionsException) can return 400 Bad Request for all of them.

---

## Manual construction

```csharp
// From an HttpRequest (minimal API or middleware):
ApiQueryOptions<Product> options = ApiQueryOptions<Product>.FromRequest(request, settings);

// From an HttpContext — safe when context is null (returns empty no-op instance):
ApiQueryOptions<Product> options = ApiQueryOptions<Product>.FromRequest(httpContext, settings);

// From an IHttpContextAccessor (in a service):
ApiQueryOptions<Product> options = ApiQueryOptions<Product>.FromRequest(httpContextAccessor, settings);

// From any IQueryCollection:
ApiQueryOptions<Product> options = new(queryCollection, settings);
```

---

## Source & examples

Full documentation, runnable examples, and source: [github.com/MassiveScale/ApiQueryOptions](https://github.com/MassiveScale/ApiQueryOptions)
