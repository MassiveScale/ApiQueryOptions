# ApiQueryOptions — Examples

Two runnable ASP.NET Core web API projects demonstrating `ApiQueryOptions` against the same PetShop domain.

A [Postman collection](ApiQueryOptions.postman_collection.json) is included that exercises every endpoint and query parameter for both APIs. Import it into Postman and set the `petshop_url` / `petshop_efcore_url` collection variables to match the ports your apps are running on (defaults match `launchSettings.json`).

---

## PetShop.Api — core package only

**Project:** `examples/PetShop.Api/`

Uses `ApiQueryOptions` (no EF Core). All data is held in an in-memory `List<Pet>` — no database or connection string required. Demonstrates:

- `$filter` with comparison operators, `and`/`or`, and string functions
- `$orderby` with `asc` / `desc`
- `$top` / `$skip` offset paging
- Error handling for malformed `$filter`

### Run

```shell
cd examples/PetShop.Api
dotnet run
```

### Example requests

```
# All available dogs, cheapest first
GET http://localhost:5000/api/pets?$filter=Species eq 'Dog' and IsAvailable eq true&$orderby=AdoptionFee asc

# First page of 3, skipping cats
GET http://localhost:5000/api/pets?$filter=Species ne 'Cat'&$top=3&$skip=0&$orderby=Name asc

# Pets whose name contains "bell" (case-insensitive)
GET http://localhost:5000/api/pets?$filter=contains(Name, 'bell')

# Pets whose breed starts with "Golden"
GET http://localhost:5000/api/pets?$filter=startswith(Breed, 'Golden')

# Pets costing between $50 and $150
GET http://localhost:5000/api/pets?$filter=AdoptionFee ge 50 and AdoptionFee le 150&$orderby=AdoptionFee asc

# Single pet by ID
GET http://localhost:5000/api/pets/1
```

---

## PetShop.Api.EFCore — core + EF Core companion

**Project:** `examples/PetShop.Api.EFCore/`

Uses `ApiQueryOptions` **and** `ApiQueryOptions.EntityFrameworkCore`. The data model adds navigation properties (`Owner`, `Appointments`) so `$expand` translates to EF Core `.Include()` calls. Runs against an EF Core in-memory database seeded on startup. Demonstrates:

- Everything from the core example, plus:
- `$expand=Owner` — includes the pet's owner (LEFT JOIN)
- `$expand=Appointments` — includes the pet's scheduled appointments
- `$expand=Owner,Appointments` — both at once
- The entire pipeline (filter → order → page → expand) sent as a single SQL round-trip against a real provider

### Run

```shell
cd examples/PetShop.Api.EFCore
dotnet run
```

### Example requests

```
# Available dogs with owner info, most expensive first
GET http://localhost:5000/api/pets?$filter=Species eq 'Dog' and IsAvailable eq true&$orderby=AdoptionFee desc&$expand=Owner

# First 5 cats with their upcoming appointments
GET http://localhost:5000/api/pets?$filter=Species eq 'Cat'&$top=5&$skip=0&$expand=Appointments

# Full record — pet + owner + appointments
GET http://localhost:5000/api/pets/1?$expand=Owner,Appointments

# All pets with no owner yet (available for adoption)
GET http://localhost:5000/api/pets?$filter=IsAvailable eq true&$orderby=AdoptionFee asc

# Owners whose name contains "alice"
GET http://localhost:5000/api/pets/owners?$filter=contains(Name, 'alice')
```

---

## Data model

```
Owner  ──< Pet >──< Appointment
  Id        Id        Id
  Name      Name      PetId → Pet.Id
  Email     Species   ScheduledAt
  Phone     Breed     Reason
            AgeInMonths   IsCompleted
            AdoptionFee
            IsAvailable
            OwnerId → Owner.Id (nullable)
```

The core example (`PetShop.Api`) uses only the `Pet` model without navigation properties.

---

## Query parameter reference

| Parameter    | Example value                                      |
|--------------|----------------------------------------------------|
| `$filter`    | `Species eq 'Dog' and AdoptionFee lt 200`         |
| `$orderby`   | `AdoptionFee desc, Name asc`                       |
| `$top`       | `10`                                               |
| `$skip`      | `20`                                               |
| `$expand`    | `Owner,Appointments` *(EF Core example only)*      |

The `$` prefix is optional — `filter=...` works the same as `$filter=...`.
