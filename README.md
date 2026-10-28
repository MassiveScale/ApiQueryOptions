# ApiQueryOptions

[![NuGet](https://img.shields.io/nuget/v/ApiQueryOptions.svg)](https://www.nuget.org/packages/ApiQueryOptions)
[![NuGet](https://img.shields.io/nuget/v/ApiQueryOptions.EntityFrameworkCore.svg?label=nuget%20EFCore)](https://www.nuget.org/packages/ApiQueryOptions.EntityFrameworkCore)
[![CI](https://github.com/MassiveScale/ApiQueryOptions/actions/workflows/ci.yml/badge.svg)](https://github.com/MassiveScale/ApiQueryOptions/actions/workflows/ci.yml)

A lightweight, OData-style query parameter library for ASP.NET Core REST APIs — without the overhead of a full OData implementation.

`ApiQueryOptions` parses `$filter`, `$expand`, `$orderby`, `$top`, `$skip`, and `$skiptoken` from HTTP query strings and applies them to any `IQueryable<T>` as deferred LINQ expression trees. Nothing is enumerated until your code calls `.ToList()` or `.ToListAsync()` — the full pipeline translates to a single SQL round-trip when used with EF Core.

---

## Packages

| Package                               | Target Frameworks   | Purpose                                                 |
| ------------------------------------- | ------------------- | ------------------------------------------------------- |
| `ApiQueryOptions`                     | `net8.0`, `net10.0` | Core parsing, `IQueryable<T>` extensions, model binding |
| `ApiQueryOptions.EntityFrameworkCore` | `net8.0`, `net10.0` | Adds `$expand` → `.Include()` for EF Core               |

---

## Installation

```shell
dotnet add package ApiQueryOptions
# optional EF Core companion:
dotnet add package ApiQueryOptions.EntityFrameworkCore
```

---

## Quick start

### 1. Register model binding (Program.cs)

```csharp
builder.Services.AddControllers();
builder.Services.AddApiQueryOptions();
```

To restrict which options are accepted globally:

```csharp
builder.Services.AddApiQueryOptions(new ApiQueryOptionsSettings
{
    ExpandEnabled   = false,   // disable $expand site-wide
    SkipTokenEnabled = false,
});
```

### 2. Accept options in a controller action

```csharp
[HttpGet]
public async Task<IActionResult> GetProducts(ApiQueryOptions<Product> options)
{
    var query = _db.Products
                   .Apply(options)      // filter + orderby + skip + top (+ expand with EF Core)
                   .AsNoTracking();

    return Ok(await query.ToListAsync());
}
```

> `Apply` is in `ApiQueryOptions.Extensions`. When the `ApiQueryOptions.EntityFrameworkCore` package is installed, the same `Apply` also turns `$expand` into `.Include()` calls. You don't need a `using` for the EF Core namespace.

---

## Supported query parameters

All parameters accept both the `$`-prefixed OData form (`$filter=...`) and the bare form (`filter=...`).

| Parameter    | Type    | Description                                     |
| ------------ | ------- | ----------------------------------------------- |
| `$filter`    | string  | Boolean filter expression (see syntax below)    |
| `$expand`    | string  | Comma-delimited navigation property paths       |
| `$orderby`   | string  | Comma-delimited sort list: `Name asc, Age desc` |
| `$top`       | integer | Maximum number of results to return             |
| `$skip`      | integer | Number of results to skip (offset-based paging) |
| `$skiptoken` | string  | Opaque cursor for page-based paging (see below) |

---

## Filter syntax

### Comparison operators

| Operator | Meaning               | Example               |
| -------- | --------------------- | --------------------- |
| `eq`     | Equals                | `Status eq 'Active'`  |
| `ne`     | Not equals            | `Status ne 'Deleted'` |
| `lt`     | Less than             | `Age lt 30`           |
| `gt`     | Greater than          | `Price gt 9.99`       |
| `le`     | Less than or equal    | `Score le 100`        |
| `ge`     | Greater than or equal | `Score ge 0`          |

String comparisons, including `lt`, `gt`, `le`, and `ge`, follow the `StringMatching` setting. The default is case-insensitive; see [String matching](#string-matching).

### Logical operators

| Operator | Meaning                         | Example                                     |
| -------- | ------------------------------- | ------------------------------------------- |
| `and`    | Logical AND (higher precedence) | `Age gt 18 and Age lt 65`                   |
| `or`     | Logical OR                      | `Status eq 'Active' or Status eq 'Pending'` |

Use parentheses to override precedence: `(A eq 1 or B eq 2) and C eq 3`.

### String functions

| Function                    | Meaning     | Example                         |
| --------------------------- | ----------- | ------------------------------- |
| `startswith(prop, 'value')` | Starts with | `startswith(Name, 'Al')`        |
| `endswith(prop, 'value')`   | Ends with   | `endswith(Email, '.com')`       |
| `contains(prop, 'value')`   | Contains    | `contains(Description, 'sale')` |

### Supported literal types

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

Values are converted to the property's type with the invariant culture, so results never depend on the server's culture. A value that does not fit the property type, such as Age eq 1.5 or Age eq 'abc' on an int property, throws InvalidQueryOptionException.


### Dotted property paths

Nested properties are supported with dot notation:

```
$filter=Address.City eq 'Seattle'
```

---

## IQueryable extensions

All extension methods build deferred LINQ expression trees. Nothing is executed until the caller materialises the query.

### Core package (`ApiQueryOptions.Extensions`)

```csharp
using ApiQueryOptions.Extensions;

// Apply all options in one call (filter → orderby → skip → top → expand):
IQueryable<T> result = query.Apply(options);

// Or apply individual options:
query = query.ApplyFilter(options.Filter!, options.Settings);
query = query.ApplyOrderBy(options.OrderBy!, options.Settings);
query = query.ApplySkip(options.Skip!, options.Settings);
query = query.ApplyTop(options.Top!, options.Settings);

// Apply only $filter (no null check needed), or count the filtered items across all pages:
IQueryable<T> filtered = query.ApplyFilter(options);
int total = query.Count(options);
```

`Apply` only handles `$expand` when the EF Core package is installed. Without it, `$expand` is ignored.

`Count(options)` and `ApplyFilter(options)` ignore `$orderby`, `$skip`, `$top`, and `$expand`, so the count is the total number of matching items, not the size of the current page. `Count` runs synchronously; on EF Core, use `CountAsync(options)` instead.

### EF Core package (`ApiQueryOptions.EntityFrameworkCore`)

Install the package and `Apply` turns `$expand` into `.Include()` calls. Nothing else changes in your code, and you don't need a `using` for the EF Core namespace. The whole pipeline translates to a single SQL query.

To apply only `$expand`, import the EF Core namespace:

```csharp
using ApiQueryOptions.EntityFrameworkCore.Extensions;

query = query.ApplyExpand(options.Expand!, options.Settings);
```

The same namespace adds `CountAsync(options)`, which counts the items that match `$filter` across all pages without blocking a thread:

```csharp
using ApiQueryOptions.EntityFrameworkCore.Extensions;

int total = await _db.Products.CountAsync(options, cancellationToken);
```

---

## Paged responses with `PagedResponse<T>`

`PagedResponse<T>` is a response envelope that pairs the current page of results with a `nextLink` URL for the following page. Use `PagedResponse.Create` — it computes the next-page URL from the current request, stripping all ApiQueryOptions-owned parameters and replacing them with a single `$skiptoken`, while forwarding any other query parameters unchanged.

```csharp
[HttpGet]
[ApiQueryOptions(DefaultPageSize = 25, MaxPageSize = 100)]
public async Task<IActionResult> Get(ApiQueryOptions<Product> options)
{
    List<Product> items = await _db.Products
                                   .Apply(options)
                                   .AsNoTracking()
                                   .ToListAsync();

    return Ok(PagedResponse.Create(items, options, Request));
}
```

**Response shape:**

```json
{
  "value": [ ... ],
  "nextLink": "https://api.example.com/api/products?$skiptoken=eyJmaWx0ZXIi...",
  "count": null
}
```

`nextLink` is `null` when the current page is the last page. Pass a `totalCount` to include the total and to suppress `nextLink` precisely when all records have been delivered:

```csharp
using ApiQueryOptions.EntityFrameworkCore.Extensions;

int total = await _db.Products.CountAsync(options);
List<Product> items = await _db.Products.Apply(options).AsNoTracking().ToListAsync();
return Ok(PagedResponse.Create(items, options, Request, totalCount: total));
```

`CountAsync(options)` counts every item that matches `$filter`, across all pages. Calling `.Count()` on the result of `Apply` would count only the current page, because `$skip` and `$top` are already applied. Without EF Core, use the synchronous `Count(options)` from `ApiQueryOptions.Extensions`.

The client follows `nextLink` verbatim — the token encodes `$filter`, `$orderby`, `$top`, and the advanced `$skip`, so the next request needs no additional parameters.

## Cursor-based pagination with `$skiptoken` (advanced)

`SkipTokenEncoder` serialises the current query state into a Base64URL token that can be passed back as `$skiptoken` to re-request the same "page shape" plus a new offset. `options.NextLink(Request, count)` handles this automatically; use the encoder directly only when you need lower-level control.

```csharp
using ApiQueryOptions.SkipToken;

// Encode manually:
string token = SkipTokenEncoder.Encode(options, skipOverride: currentSkip + pageSize);

// Decode on the next request:
var decodedOptions = SkipTokenEncoder.Decode<Product>(request.Query["$skiptoken"]!);
var results = await _db.Products.Apply(decodedOptions).ToListAsync();
```

`Decode` throws `FormatException` if the token is tampered with or malformed. With model binding, a malformed `$skiptoken` is added to `ModelState` instead, so an `[ApiController]` returns `400 Bad Request`.

---

## Configuration (`ApiQueryOptionsSettings`)

All settings use `init`-only properties and default to the most permissive values.

```csharp
var settings = new ApiQueryOptionsSettings
{
    FilterEnabled    = true,   // accept $filter
    ExpandEnabled    = true,   // accept $expand
    OrderByEnabled   = true,   // accept $orderby
    TopEnabled       = true,   // accept $top
    SkipEnabled      = true,   // accept $skip
    SkipTokenEnabled = true,   // accept $skiptoken
    StringMatching   = StringMatching.IgnoreCase,  // how $filter compares strings
    TieBreakerProperty = "Id", // appended to the sort when paging (default: null)
    MaxFilterDepth     = 32,   // max parenthesis nesting in $filter
    MaxFilterNodeCount = 100,  // max comparisons + functions + and/or in $filter
};
```

`StringMatching`, `TieBreakerProperty`, the page sizes, and the `*Enabled` switches can also be overridden for one controller or action with `[ApiQueryOptions(...)]`.

`MaxPageSize`, `DefaultPageSize`, `MaxFilterDepth`, and `MaxFilterNodeCount` must be positive; setting zero or a negative value throws `ArgumentOutOfRangeException` at startup. A request whose `$top` is zero or negative, or whose `$skip` is negative, is treated as if the parameter were absent.

A `$filter` that exceeds `MaxFilterDepth` or `MaxFilterNodeCount` throws `FilterParseException`, the same as a syntax error. The limits stop hostile input from exhausting the stack.

When `SkipTokenEnabled` is `false`, `NextLink` builds offset links instead: `$top` and `$skip` are replaced with the next page's values and every other parameter, including `$filter` and `$orderby`, is forwarded unchanged. `NextLink` returns `null` if `$skip` is disabled too, because the next page could not be requested.

When an option is disabled, any matching query key is **silently ignored** at parse time — no exception is thrown. The corresponding property on `ApiQueryOptions<T>` will be `null`.

Calling an `Apply*` extension method directly with a disabled setting does throw `QueryOptionDisabledException`.

### String matching

`StringMatching` controls how `$filter` compares strings in `eq`, `ne`, `lt`, `gt`, `le`, `ge`, `startswith`, `endswith`, and `contains`.
| Value                  | In memory                            | Database providers (EF Core)                                                                                         |
|------------------------|--------------------------------------|----------------------------------------------------------------------------------------------------------------------|
| `IgnoreCase` (default) | `StringComparison.OrdinalIgnoreCase` | `UPPER(column)` compared with a parameter upper-cased on the server. The database cannot use an index on the column. |
| `CaseSensitive`        | `StringComparison.Ordinal`           | Plain comparison. Case-sensitive only when the column collation is.                                                  |
| `Database`             | Plain .NET operators and methods     | Plain comparison. The column collation decides, and indexes can be used.                                             |

`IgnoreCase` is culture-invariant, so `'istanbul'` matches `Istanbul` on a server running in Turkish, and it is null-safe: rows with a `null` value never match a string function or an ordering operator, and `Name eq null` becomes a plain `IS NULL` check.

On EF Core, `IgnoreCase` wraps the column in `UPPER()`, which stops the database from using an index on that column. For large tables, give the column a case-insensitive collation (for example `SQL_Latin1_General_CP1_CI_AS` on SQL Server, or `NOCASE` on SQLite) and use `StringMatching.Database`. The comparison is then a plain `=` or `LIKE` that can use the index.

### Stable paging

Paging with `$top` and `$skip` is only stable when the sort order is unique. If several rows share the same sort value, the database may return them in a different order on each request, so a row can appear on two pages or on none. Set `TieBreakerProperty` to a unique column, usually the primary key:

```csharp
builder.Services.AddApiQueryOptions(o => o.TieBreakerProperty = "Id");
```

Whenever `Apply` applies `$top` or `$skip`, it adds `ThenBy(Id)` after the client's `$orderby`, or `OrderBy(Id)` when there is no `$orderby`. Nothing is added when `$orderby` already includes the property or when the request does not page. A tie-breaker that does not exist on `T` throws `InvalidQueryOptionException` when the query is built.

### Configurable parameter names

Each option exposes a `*ParameterNames` property that controls which query string keys it responds to. Names are tried left-to-right; the first match wins. The defaults reproduce the current OData behaviour so no change is needed unless you want aliases.

```csharp
builder.Services.AddApiQueryOptions(o =>
{
    // Accept "limit" as an alias for "$top"/"top"
    o.TopParameterNames = ["$top", "top", "limit"];

    // Accept "q" as an alias for "$filter"
    o.FilterParameterNames = ["$filter", "filter", "q"];
});
```

| Property                | Default                          |
| ----------------------- | -------------------------------- |
| `FilterParameterNames`  | `["$filter",    "filter"]`       |
| `ExpandParameterNames`  | `["$expand",    "expand"]`       |
| `OrderByParameterNames` | `["$orderby",   "orderby"]`      |
| `TopParameterNames`     | `["$top",       "top"]`          |
| `SkipParameterNames`    | `["$skip",      "skip"]`         |
| `SkipTokenParameterNames` | `["$skiptoken", "skiptoken"]`  |

Custom names are automatically excluded from the query-string passthrough in `NextLink`.

---

## Manual construction (without model binding)

```csharp
// From an HttpRequest (minimal API or middleware):
var options = ApiQueryOptions<Product>.FromRequest(request, settings);

// From an HttpContext — safe when context is null (returns empty no-op instance):
var options = ApiQueryOptions<Product>.FromRequest(httpContext, settings);

// From an IHttpContextAccessor (in a service):
var options = ApiQueryOptions<Product>.FromRequest(httpContextAccessor, settings);

// From any IQueryCollection:
var options = new ApiQueryOptions<Product>(queryCollection, settings);
```

---

## Error handling

| Exception                      | Base type                   | When                                                                                                                                                                                   |
|--------------------------------|-----------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `FilterParseException`         | `ApiQueryOptionsException`  | The `$filter` string has a syntax error or exceeds a filter limit. Exposes `RawFilter` and `Position`.                                                                                 |
| `InvalidQueryOptionException`  | `ApiQueryOptionsException`  | An option names an unknown property, uses a value that does not fit the property type, or uses an operator or function the type does not support. Exposes `OptionName` and `Property`. |
| `QueryOptionDisabledException` | `InvalidOperationException` | An `Apply*` method is called directly against a disabled option. This is a programming error, not bad client input.                                                                    |
| `FormatException`              | `SystemException`           | `SkipTokenEncoder.Decode` receives a malformed or tampered token.                                                                                                                      |

Every exception caused by bad client input derives from `ApiQueryOptionsException`, so one `catch` turns all of them into a `400 Bad Request`. `FilterParseException` is thrown lazily, on first access to `FilterQueryOption.FilterClause`, which normally happens inside `Apply`. With model binding, a malformed `$skiptoken` is added to `ModelState` instead of throwing.

---

## Full example — paged product listing

```csharp
// Program.cs
builder.Services.AddControllers();
builder.Services.AddApiQueryOptions();

// ProductsController.cs
[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly AppDbContext _db;
    public ProductsController(AppDbContext db) => _db = db;

    [HttpGet]
    [ApiQueryOptions(DefaultPageSize = 20, MaxPageSize = 100)]
    public async Task<IActionResult> Get(ApiQueryOptions<Product> options)
    {
        try
        {
            List<Product> items = await _db.Products
                                           .Apply(options)
                                           .AsNoTracking()
                                           .ToListAsync();

            return Ok(PagedResponse.Create(items, options, Request));
        }
        catch (ApiQueryOptionsException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
```

First request — note only `$filter` and `$orderby` are needed; the default page size kicks in automatically:

```
GET /api/products?$filter=Category eq 'Electronics' and Price lt 500&$orderby=Name asc
```

Response:

```json
{
  "value": [ ... ],
  "nextLink": "https://api.example.com/api/products?$skiptoken=eyJmaWx0ZXIi...",
  "count": null
}
```

Follow-up — the client passes `nextLink` verbatim; no need to re-specify `$filter`, `$top`, etc.:

```
GET /api/products?$skiptoken=eyJmaWx0ZXIi...
```

---

## Examples

The [`examples/`](examples/) folder contains two runnable ASP.NET Core projects using the PetShop domain:

| Project | Packages used | What it shows |
|---|---|---|
| [`examples/PetShop.Api`](examples/PetShop.Api/) | `ApiQueryOptions` | Filter, orderby, top/skip against an in-memory list |
| [`examples/PetShop.Api.EFCore`](examples/PetShop.Api.EFCore/) | `ApiQueryOptions` + `ApiQueryOptions.EntityFrameworkCore` | Full pipeline including `$expand` → `.Include()` with an EF Core in-memory database |

See [examples/README.md](examples/README.md) for example HTTP requests and the data model.

---

## Project structure

```
src/
  ApiQueryOptions/                        # Core package
    ApiQueryOptions.cs                    # ApiQueryOptions<T> — main entry point
    ApiQueryOptionsSettings.cs            # Per-option enable/disable, paging, StringMatching
    StringMatching.cs                     # IgnoreCase / CaseSensitive / Database
    PagedResponse.cs                      # PagedResponse<T> — paged response envelope
    Binding/
      ApiQueryOptionsModelBinder.cs       # IModelBinder implementation
      ApiQueryOptionsModelBinderProvider.cs
    Exceptions/
      ApiQueryOptionsException.cs         # Base type for bad client input
      FilterParseException.cs
      InvalidQueryOptionException.cs
      QueryOptionDisabledException.cs
    Extensions/
      IQueryableExtensions.cs             # Apply, ApplyFilter, ApplyOrderBy, ApplySkip, ApplyTop, Count
      ServiceCollectionExtensions.cs      # AddApiQueryOptions()
    Filter/
      FilterClause.cs / FilterNode.cs     # AST root & base type
      BinaryFilterNode.cs                 # Property op Value
      LogicalFilterNode.cs                # Left and/or Right
      FunctionFilterNode.cs               # startswith / endswith / contains
      FilterLexer.cs                      # Tokeniser
      FilterParser.cs                     # Recursive-descent parser
      FilterValueConverter.cs             # Invariant-culture literal conversion
      FilterOperator.cs / LogicalOperator.cs / StringFunction.cs
    Options/
      ExpandQueryOption.cs
      FilterQueryOption.cs
      OrderByItem.cs / OrderByQueryOption.cs
      SkipQueryOption.cs / TopQueryOption.cs
      SkipTokenQueryOption.cs
    SkipToken/
      SkipTokenEncoder.cs                 # Base64URL encode/decode

  ApiQueryOptions.EntityFrameworkCore/    # EF Core companion
    Extensions/
      EFQueryableExtensions.cs            # ApplyExpand, CountAsync

tests/
  ApiQueryOptions.Tests/                  # MSTest, AwesomeAssertions, NSubstitute
  ApiQueryOptions.EntityFrameworkCore.Tests/
```

---

## License

MIT © MassiveScale
