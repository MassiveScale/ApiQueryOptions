# ApiQueryOptions.EntityFrameworkCore

[![NuGet](https://img.shields.io/nuget/v/ApiQueryOptions.EntityFrameworkCore.svg)](https://www.nuget.org/packages/ApiQueryOptions.EntityFrameworkCore)
[![CI](https://github.com/MassiveScale/ApiQueryOptions/actions/workflows/ci.yml/badge.svg)](https://github.com/MassiveScale/ApiQueryOptions/actions/workflows/ci.yml)

EF Core companion for [ApiQueryOptions](https://www.nuget.org/packages/ApiQueryOptions).

Translates `$expand` into `.Include()` calls and applies the full query pipeline (filter → orderby → skip → top → expand) as a single deferred SQL query. Nothing is enumerated until you call `.ToListAsync()`.

---

## Installation

```shell
dotnet add package ApiQueryOptions
dotnet add package ApiQueryOptions.EntityFrameworkCore
```

---

## Quick start

### 1. Register in `Program.cs`

```csharp
builder.Services.AddControllers();
builder.Services.AddApiQueryOptions();
```

### 2. Call `Apply`

With this package installed, the core `Apply` also handles `$expand`. You don't need a `using` for this package's namespace.

```csharp
using ApiQueryOptions.EntityFrameworkCore.Extensions;
using ApiQueryOptions.Extensions;

[HttpGet]
public async Task<IActionResult> Get(ApiQueryOptions<Order> options)
{
    int total = await _db.Orders.CountAsync(options);  // every order matching $filter, across all pages

    List<Order> items = await _db.Orders
                                 .Apply(options)        // filter + orderby + skip + top + expand
                                 .AsNoTracking()
                                 .ToListAsync();

    return Ok(PagedResponse.Create(items, options, Request, totalCount: total));
}
```

`CountAsync(options)` needs this package's namespace. Leave it out when you don't need the total.

### 3. Request with `$expand`

```
GET /api/orders?$filter=Status eq 'Open'&$orderby=CreatedAt desc&$top=20&$expand=Customer,Lines
```

EF Core eagerly loads the `Customer` navigation property and `Lines` collection as part of the same SQL query — no N+1 queries.

---

## What this package adds

| Feature                             | `ApiQueryOptions` | This package |
|-------------------------------------|:-----------------:|:------------:|
| `$filter` → `Where()`               | ✔                 | ✔            |
| `$orderby` → `OrderBy()`            | ✔                 | ✔            |
| `$top` → `Take()`                   | ✔                 | ✔            |
| `$skip` → `Skip()`                  | ✔                 | ✔            |
| Cursor pagination (`NextLink`)      | ✔                 | ✔            |
| Filtered count (`Count`)            | ✔                 | ✔            |
| `$expand` → `.Include()`            |                   | ✔            |
| Async `.ToListAsync()` support      |                   | ✔            |
| Async filtered count (`CountAsync`) |                   | ✔            |

---

## EF Core extensions

```csharp
using ApiQueryOptions.Extensions;

// Full pipeline — filter → orderby → skip → top → expand:
IQueryable<T> result = query.Apply(options);

// Expand only, and the async filtered count (both need this package's namespace):
using ApiQueryOptions.EntityFrameworkCore.Extensions;
IQueryable<T> result = query.ApplyExpand(options.Expand!, options.Settings);
int total = await query.CountAsync(options, cancellationToken);
```

`CountAsync(options)` applies only `$filter` and ignores `$orderby`, `$skip`, `$top`, and `$expand`, so it returns the total number of matching items rather than the size of the current page. It reads the filter from `options`, which a `$skiptoken` restores, so the total stays the same on every page.

`Apply` runs filter, orderby, skip, and top, then appends `.Include()` calls for each path listed in `$expand` when the query is a query of entities (see "Expand on projected queries" below for DTO projections). The whole chain is deferred and translates to a single SQL round-trip. Filter values are sent as SQL parameters, so EF Core and the database reuse one cached plan per filter shape.

You can import both `ApiQueryOptions.Extensions` and `ApiQueryOptions.EntityFrameworkCore.Extensions` in the same file. There is only one `Apply`, so the call is never ambiguous.

---

## Nested expand paths

Dot-delimited paths are supported for deep includes:

```
$expand=Customer,Lines.Product
```

This generates:

```csharp
.Include(o => o.Customer)
.Include(o => o.Lines).ThenInclude(l => l.Product)
```

Each segment is checked against the navigations in the EF Core model before `.Include()` is called, and names are matched in any case (`$expand=customer` works). Navigations declared on derived types and many-to-many navigations are found too. A segment that does not exist, or that names a plain column such as `Lines.Quantity`, throws `InvalidQueryOptionException` (a subclass of `ApiQueryOptionsException`), so a bad `$expand` can be returned as `400 Bad Request` like any other client error.

---

## Expand on projected queries

`.Include()` only works on a query of entities straight from a `DbSet` (with `Where`, `OrderBy`, `AsNoTracking`, and similar operators on top). API responses are usually projected to a DTO first, and EF Core throws if `.Include()` is called after that `Select`. `Apply` checks for this itself:

| Query passed to `Apply`                                              | What `$expand` does                                                                                                                                        |
|----------------------------------------------------------------------|------------------------------------------------------------------------------------------------------------------------------------------------------------|
| An entity query: `db.Orders`, `db.Orders.Where(...)`, `FromSql(...)` | Each path becomes `.Include()`.<br>A path that is not a navigation throws `InvalidQueryOptionException`                                                    |
| A projected query: `db.Orders.Select(o => new OrderDto { ... })`     | Nothing is included; your projection loads what was asked for.<br>Each path must still name a property of the DTO, otherwise `InvalidQueryOptionException` |
| A projection to a related entity: `db.Lines.Select(l => l.Order)`    | Treated as a projected query, because EF Core cannot include after it either                                                                               |
| A query that is not EF Core, such as a list's `AsQueryable()`        | Nothing is included. Paths are checked against the properties of `T`                                                                                       |

`$filter`, `$orderby`, `$skip`, and `$top` are applied either way, and `options.Expand` and `nextLink` keep the `$expand` the client sent. In the projection, call `options.Expands(...)` to load a related collection only when it was requested. EF Core evaluates the condition before it builds the SQL, so a collection that was not requested is never queried:

```csharp
ApiQueryOptions<OrderDto> options = ApiQueryOptions<OrderDto>.FromRequest(Request);

IQueryable<OrderDto> orders = db.Orders.Select(o => new OrderDto
{
    Id = o.Id,
    Status = o.Status,
    Lines = options.Expands(nameof(OrderDto.Lines))
        ? o.Lines.Select(l => new LineDto { Product = l.Product.Name, Quantity = l.Quantity }).ToList()
        : null,
});

List<OrderDto> page = await orders.Apply(options).ToListAsync(cancellationToken);
```

`Expands` is case-insensitive, returns `false` when there is no `$expand`, and also returns `true` for the start of a longer path: `$expand=lines.product` expands both `Lines` and `Lines.Product`. The same check is available as `options.Expand.Includes(...)` on a non-null `ExpandQueryOption`.

---

## Case-insensitive filters and indexes

The default `StringMatching.IgnoreCase` compares `UPPER(column)` with an upper-cased parameter. That works on every database, but it prevents the database from using an index on the column. When a filtered string column is large or hot, give it a case-insensitive collation and switch to `StringMatching.Database`, which sends a plain `=` or `LIKE` that can use the index:

```csharp
// Model configuration (SQL Server example)
modelBuilder.Entity<Customer>()
    .Property(c => c.Name)
    .UseCollation("SQL_Latin1_General_CP1_CI_AS");

// Program.cs
builder.Services.AddApiQueryOptions(o => o.StringMatching = StringMatching.Database);
```

`[ApiQueryOptions(StringMatching = StringMatching.Database)]` applies the same setting to a single controller or action.

---

## Supported frameworks

| Framework | EF Core version |
| --------- | --------------- |
| `net8.0`  | 8.0.x           |
| `net10.0` | 10.0.x          |

---

## Source & documentation

Full documentation, runnable examples, and source: [github.com/MassiveScale/ApiQueryOptions](https://github.com/MassiveScale/ApiQueryOptions)
