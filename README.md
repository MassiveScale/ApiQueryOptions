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
                   .Apply(options)      // EF Core: filter + orderby + skip + top + expand
                   .AsNoTracking();

    return Ok(await query.ToListAsync());
}
```

> The `Apply` extension is in `ApiQueryOptions.Extensions` (core) or `ApiQueryOptions.EntityFrameworkCore.Extensions` (EF Core). Import the EF Core namespace to get `$expand` → `.Include()` support.

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

String `eq`/`ne` comparisons use the `StringComparison` configured in `ApiQueryOptionsSettings` (default: `OrdinalIgnoreCase`).

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

| Type    | Example                           |
| ------- | --------------------------------- |
| String  | `'O''Brien'` (escape `'` as `''`) |
| Integer | `42`, `-5`                        |
| Decimal | `9.99`                            |
| Boolean | `true`, `false`                   |
| Null    | `null`                            |

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

// Apply all options in one call (filter → orderby → skip → top):
IQueryable<T> result = query.Apply(options);

// Or apply individual options:
query = query.ApplyFilter(options.Filter!, options.Settings);
query = query.ApplyOrderBy(options.OrderBy!, options.Settings);
query = query.ApplySkip(options.Skip!, options.Settings);
query = query.ApplyTop(options.Top!, options.Settings);
```

### EF Core package (`ApiQueryOptions.EntityFrameworkCore.Extensions`)

```csharp
using ApiQueryOptions.EntityFrameworkCore.Extensions;

// Apply all options including $expand → .Include():
IQueryable<T> result = query.Apply(options);   // filter + orderby + skip + top + expand

// Apply expand separately:
query = query.ApplyExpand(options.Expand!, options.Settings);
```

The EF Core `Apply` calls the core pipeline first, then adds `.Include()` calls for each navigation property — the entire operation translates to a single SQL query.

---

## Cursor-based pagination with `$skiptoken`

`SkipTokenEncoder` serialises the current query state into a Base64URL token that can be passed back as `$skiptoken` to re-request the same "page shape" plus a new offset.

```csharp
using ApiQueryOptions.SkipToken;

// In the action — encode the token for the next page:
var nextOptions = new ApiQueryOptions<Product>(
    new QueryCollection(new Dictionary<string, StringValues>
    {
        ["$filter"]  = options.Filter?.RawValue ?? string.Empty,
        ["$orderby"] = "Name asc",
        ["$top"]     = "25",
        ["$skip"]    = (currentSkip + 25).ToString(),
    }));

string nextToken = SkipTokenEncoder.Encode(nextOptions);
// Return nextToken in the response envelope as "@nextLink"

// On the next request — decode:
var decodedOptions = SkipTokenEncoder.Decode<Product>(request.Query["$skiptoken"]!);
var results = await _db.Products.Apply(decodedOptions).ToListAsync();
```

`Decode` throws `FormatException` if the token is tampered with or malformed.

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
    StringComparison = StringComparison.OrdinalIgnoreCase,  // string eq/ne/functions
};
```

When an option is disabled, any matching query key is **silently ignored** at parse time — no exception is thrown. The corresponding property on `ApiQueryOptions<T>` will be `null`.

Calling an `Apply*` extension method directly with a disabled setting does throw `QueryOptionDisabledException`.

---

## Manual construction (without model binding)

```csharp
// From an HttpRequest (in a minimal API or middleware):
var options = ApiQueryOptions<Product>.FromRequest(httpContext.Request, settings);

// From any IQueryCollection:
var options = new ApiQueryOptions<Product>(queryCollection, settings);
```

---

## Error handling

| Exception                      | When                                                                                                             |
| ------------------------------ | ---------------------------------------------------------------------------------------------------------------- |
| `FilterParseException`         | The `$filter` string contains a syntax error. Thrown lazily on first access to `FilterQueryOption.FilterClause`. |
| `QueryOptionDisabledException` | An `Apply*` method is called directly against a disabled option.                                                 |
| `FormatException`              | `SkipTokenEncoder.Decode` receives a malformed or tampered token.                                                |
| `InvalidOperationException`    | A filter or orderby references a property that does not exist on `T`.                                            |

`FilterParseException` exposes `RawFilter` and `Position` for diagnostic messages.

---

## Full example — paged product listing

```csharp
// Program.cs
builder.Services.AddControllers();
builder.Services.AddApiQueryOptions(new ApiQueryOptionsSettings
{
    SkipTokenEnabled = false,   // using offset paging instead
});

// ProductsController.cs
[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly AppDbContext _db;
    public ProductsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Get(ApiQueryOptions<Product> options)
    {
        try
        {
            var query = _db.Products.Apply(options).AsNoTracking();
            var total = await _db.Products.CountAsync();   // before paging
            var items = await query.ToListAsync();

            return Ok(new
            {
                total,
                count = items.Count,
                value = items,
            });
        }
        catch (FilterParseException ex)
        {
            return BadRequest(new { error = "Invalid $filter", detail = ex.Message, position = ex.Position });
        }
    }
}
```

Example request:

```
GET /api/products?$filter=Category eq 'Electronics' and Price lt 500
                 &$orderby=Name asc
                 &$top=20
                 &$skip=0
                 &$expand=Supplier
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
    ApiQueryOptionsSettings.cs            # Per-option enable/disable + StringComparison
    Binding/
      ApiQueryOptionsModelBinder.cs       # IModelBinder implementation
      ApiQueryOptionsModelBinderProvider.cs
    Exceptions/
      FilterParseException.cs
      QueryOptionDisabledException.cs
    Extensions/
      IQueryableExtensions.cs             # Apply, ApplyFilter, ApplyOrderBy, ApplySkip, ApplyTop
      ServiceCollectionExtensions.cs      # AddApiQueryOptions()
    Filter/
      FilterClause.cs / FilterNode.cs     # AST root & base type
      BinaryFilterNode.cs                 # Property op Value
      LogicalFilterNode.cs                # Left and/or Right
      FunctionFilterNode.cs               # startswith / endswith / contains
      FilterLexer.cs                      # Tokeniser
      FilterParser.cs                     # Recursive-descent parser
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
      EFQueryableExtensions.cs            # Apply (with expand), ApplyExpand

tests/
  ApiQueryOptions.Tests/                  # xUnit, FluentAssertions, Moq
  ApiQueryOptions.EntityFrameworkCore.Tests/
```

---

## License

MIT © MassiveScale
