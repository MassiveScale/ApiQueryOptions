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

Unset properties (`0` for integers, `QueryOptionState.Default` for toggles) inherit from the registered settings unchanged.

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

String `eq`/`ne` comparisons respect the `StringComparison` in `ApiQueryOptionsSettings` (default: `OrdinalIgnoreCase`).

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

| Type    | Example                           |
| ------- | --------------------------------- |
| String  | `'O''Brien'` (escape `'` as `''`) |
| Integer | `42`, `-5`                        |
| Decimal | `9.99`                            |
| Boolean | `true`, `false`                   |
| Null    | `null`                            |

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
    int total = _db.Products.Count();

    return Ok(new
    {
        value    = items,
        nextLink = options.NextLink(items.Count, total),  // null when no more pages
    });
}
```

The client passes the returned cursor as `$skiptoken` on the next request:

```
GET /api/products?$skiptoken=<token>
```

---

## IQueryable extensions

```csharp
using ApiQueryOptions.Extensions;

IQueryable<T> result = query.Apply(options);            // filter → orderby → skip → top

query = query.ApplyFilter(options.Filter!, settings);
query = query.ApplyOrderBy(options.OrderBy!, settings);
query = query.ApplySkip(options.Skip!, settings);
query = query.ApplyTop(options.Top!, settings);
```

---

## Configuration (`ApiQueryOptionsSettings`)

All properties use `init`-only setters and default to the most permissive values.

| Property          | Type               | Default               | Description                            |
| ----------------- | ------------------ | --------------------- | -------------------------------------- |
| `FilterEnabled`   | `bool`             | `true`                | Accept `$filter`                       |
| `ExpandEnabled`   | `bool`             | `true`                | Accept `$expand`                       |
| `OrderByEnabled`  | `bool`             | `true`                | Accept `$orderby`                      |
| `TopEnabled`      | `bool`             | `true`                | Accept `$top`                          |
| `SkipEnabled`     | `bool`             | `true`                | Accept `$skip`                         |
| `SkipTokenEnabled`| `bool`             | `true`                | Accept `$skiptoken`                    |
| `MaxPageSize`     | `int?`             | `null`                | Upper limit for `$top`; silently clamps|
| `DefaultPageSize` | `int?`             | `null`                | Default `$top` when none provided      |
| `StringComparison`| `StringComparison` | `OrdinalIgnoreCase`   | Used for string `eq`/`ne` and functions|

When an option is disabled, any matching query key is silently ignored and the corresponding property on `ApiQueryOptions<T>` will be `null`.

---

## Error handling

| Exception                      | When                                                                                          |
| ------------------------------ | --------------------------------------------------------------------------------------------- |
| `FilterParseException`         | `$filter` contains a syntax error — exposes `RawFilter` and `Position`                        |
| `QueryOptionDisabledException` | An `Apply*` method is called directly against a disabled option                               |
| `FormatException`              | `SkipTokenEncoder.Decode` receives a malformed or tampered token                              |
| `InvalidOperationException`    | A filter or orderby expression references a property that does not exist on `T`               |

---

## Manual construction

```csharp
// From an HttpRequest (minimal APIs, middleware):
ApiQueryOptions<Product> options = ApiQueryOptions<Product>.FromRequest(httpContext.Request, settings);

// From any IQueryCollection:
ApiQueryOptions<Product> options = new(queryCollection, settings);
```

---

## Source & examples

Full documentation, runnable examples, and source: [github.com/MassiveScale/ApiQueryOptions](https://github.com/MassiveScale/ApiQueryOptions)
