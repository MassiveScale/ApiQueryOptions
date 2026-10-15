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
using ApiQueryOptions.Extensions;

[HttpGet]
public async Task<IActionResult> Get(ApiQueryOptions<Order> options)
{
    List<Order> items = await _db.Orders
                                 .Apply(options)        // filter + orderby + skip + top + expand
                                 .AsNoTracking()
                                 .ToListAsync();

    return Ok(new { value = items, nextLink = options.NextLink(items.Count) });
}
```

### 3. Request with `$expand`

```
GET /api/orders?$filter=Status eq 'Open'&$orderby=CreatedAt desc&$top=20&$expand=Customer,Lines
```

EF Core eagerly loads the `Customer` navigation property and `Lines` collection as part of the same SQL query — no N+1 queries.

---

## What this package adds

| Feature                          | `ApiQueryOptions` | This package |
| -------------------------------- | :---------------: | :----------: |
| `$filter` → `Where()`           | ✔                 | ✔            |
| `$orderby` → `OrderBy()`        | ✔                 | ✔            |
| `$top` → `Take()`               | ✔                 | ✔            |
| `$skip` → `Skip()`              | ✔                 | ✔            |
| Cursor pagination (`NextLink`)   | ✔                 | ✔            |
| `$expand` → `.Include()`        |                   | ✔            |
| Async `.ToListAsync()` support   |                   | ✔            |

---

## EF Core extensions

```csharp
using ApiQueryOptions.Extensions;

// Full pipeline — filter → orderby → skip → top → expand:
IQueryable<T> result = query.Apply(options);

// Expand only (needs this package's namespace):
using ApiQueryOptions.EntityFrameworkCore.Extensions;
IQueryable<T> result = query.ApplyExpand(options.Expand!, options.Settings);
```

`Apply` runs filter, orderby, skip, and top, then appends `.Include()` calls for each path listed in `$expand`. The whole chain is deferred and translates to a single SQL round-trip. Filter values are sent as SQL parameters, so EF Core and the database reuse one cached plan per filter shape.

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

Each segment is checked against the entity's public properties before `.Include()` is called, and names are matched in any case (`$expand=customer` works). A segment after a collection, such as `Product` in `Lines.Product`, is looked up on the collection's element type. An unknown segment throws `InvalidQueryOptionException` (a subclass of `ApiQueryOptionsException`), so a bad `$expand` can be returned as `400 Bad Request` like any other client error.

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
