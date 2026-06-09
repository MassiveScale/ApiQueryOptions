using ApiQueryOptions;
using ApiQueryOptions.Exceptions;
using ApiQueryOptions.Extensions;
using Microsoft.AspNetCore.Mvc;
using PetShop.Api.Models;

namespace PetShop.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class PetsController : ControllerBase
{
    // Static in-memory dataset — no database required for this example.
    private static readonly IReadOnlyList<Pet> _pets =
    [
        new() { Id =  1, Name = "Buddy",    Species = "Dog",  Breed = "Labrador Retriever", AgeInMonths = 24, AdoptionFee = 150.00m, IsAvailable = true  },
        new() { Id =  2, Name = "Whiskers", Species = "Cat",  Breed = "Domestic Shorthair", AgeInMonths = 36, AdoptionFee =  75.00m, IsAvailable = true  },
        new() { Id =  3, Name = "Goldie",   Species = "Fish", Breed = "Goldfish",            AgeInMonths =  6, AdoptionFee =  10.00m, IsAvailable = true  },
        new() { Id =  4, Name = "Max",      Species = "Dog",  Breed = "German Shepherd",     AgeInMonths = 18, AdoptionFee = 200.00m, IsAvailable = false },
        new() { Id =  5, Name = "Luna",     Species = "Cat",  Breed = "Siamese",             AgeInMonths = 12, AdoptionFee = 100.00m, IsAvailable = true  },
        new() { Id =  6, Name = "Charlie",  Species = "Dog",  Breed = "Beagle",              AgeInMonths = 48, AdoptionFee = 125.00m, IsAvailable = true  },
        new() { Id =  7, Name = "Tweety",   Species = "Bird", Breed = "Canary",              AgeInMonths =  9, AdoptionFee =  50.00m, IsAvailable = false },
        new() { Id =  8, Name = "Bella",    Species = "Dog",  Breed = "Golden Retriever",    AgeInMonths = 30, AdoptionFee = 175.00m, IsAvailable = true  },
        new() { Id =  9, Name = "Shadow",   Species = "Cat",  Breed = "Persian",             AgeInMonths = 60, AdoptionFee =  90.00m, IsAvailable = false },
        new() { Id = 10, Name = "Nemo",     Species = "Fish", Breed = "Clownfish",           AgeInMonths =  3, AdoptionFee =  15.00m, IsAvailable = true  },
    ];

    /// <summary>
    /// Returns a filtered, sorted, and paged list of pets.
    /// </summary>
    /// <remarks>
    /// Example requests:
    ///
    ///   All available dogs, cheapest first:
    ///     GET /api/pets?$filter=Species eq 'Dog' and IsAvailable eq true&amp;$orderby=AdoptionFee asc
    ///
    ///   First page of 3, skipping cats:
    ///     GET /api/pets?$filter=Species ne 'Cat'&amp;$top=3&amp;$skip=0&amp;$orderby=Name asc
    ///
    ///   Pets with "buddy" in the name (case-insensitive by default):
    ///     GET /api/pets?$filter=contains(Name, 'buddy')
    /// </remarks>
    [HttpGet]
    [ApiQueryOptions(DefaultPageSize = 5, MaxPageSize = 10)]
    public IActionResult Get(ApiQueryOptions<Pet> options)
    {
        try
        {
            var results = _pets.AsQueryable()
                               .Apply(options)
                               .ToList();

            return Ok(new
            {
                nextLink = options.NextLink(results.Count),
                count = results.Count,
                value = results
            });
        }
        catch (FilterParseException ex)
        {
            return BadRequest(new
            {
                error = "Invalid $filter expression.",
                detail = ex.Message,
                position = ex.Position,
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Returns a single pet by ID.</summary>
    [HttpGet("{id:int}")]
    public IActionResult GetById(int id)
    {
        Pet? pet = _pets.FirstOrDefault(p => p.Id == id);
        return pet is null ? NotFound() : Ok(pet);
    }
}