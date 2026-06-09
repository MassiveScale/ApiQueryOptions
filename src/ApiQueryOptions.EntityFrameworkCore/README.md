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

### 2. Use the EF Core `Apply` extension

```csharp
using ApiQueryOptions.EntityFrameworkCore.Extensions;

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
using ApiQueryOptions.EntityFrameworkCore.Extensions;

// Full pipeline — filter → orderby → skip → top → expand:
IQueryable<T> result = query.Apply(options);

// Expand only:
IQueryable<T> result = query.ApplyExpand(options.Expand!, options.Settings);
```

The EF Core `Apply` runs the core pipeline first, then appends `.Include()` calls for each path listed in `$expand`. The whole chain is deferred and translates to a single SQL round-trip.

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

---

## Supported frameworks

| Framework | EF Core version |
| --------- | --------------- |
| `net8.0`  | 8.0.x           |
| `net10.0` | 10.0.x          |

---

## Source & documentation

Full documentation, runnable examples, and source: [github.com/MassiveScale/ApiQueryOptions](https://github.com/MassiveScale/ApiQueryOptions)
