using ApiQueryOptions;
using ApiQueryOptions.EntityFrameworkCore.Extensions;
using ApiQueryOptions.Exceptions;
using ApiQueryOptions.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetShop.EFCore.Data;
using PetShop.EFCore.Models;

namespace PetShop.EFCore.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class PetsController : ControllerBase
{
    private readonly PetShopDbContext _db;

    public PetsController(PetShopDbContext db) => _db = db;

    /// <summary>
    /// Returns a filtered, sorted, paged list of pets.
    /// Pass <c>$expand</c> to include related data as SQL JOINs.
    /// </summary>
    /// <remarks>
    /// Example requests:
    ///
    ///   Available dogs, most expensive first, with their owner:
    ///     GET /api/pets?$filter=Species eq 'Dog' and IsAvailable eq true
    ///                  &amp;$orderby=AdoptionFee desc
    ///                  &amp;$expand=Owner
    ///
    ///   First page of 5 cats, including upcoming appointments:
    ///     GET /api/pets?$filter=Species eq 'Cat'&amp;$top=5&amp;$skip=0&amp;$expand=Appointments
    ///
    ///   Full record with all related data:
    ///     GET /api/pets?$expand=Owner,Appointments
    /// </remarks>
    [HttpGet]
    public async Task<IActionResult> Get(ApiQueryOptions<Pet> options)
    {
        try
        {
            // .Apply() from ApiQueryOptions.EntityFrameworkCore.Extensions composes
            // the full pipeline (WHERE → ORDER BY → OFFSET/FETCH → JOIN) into a
            // single SQL round-trip when running against a real database.
            List<Pet> results = await _db.Pets
                                   .Apply(options)
                                   .AsNoTracking()
                                   .ToListAsync();

            return Ok(PagedResponse.Create(results, options, Request));
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

    /// <summary>
    /// Returns a single pet by ID, optionally including related data.
    /// </summary>
    /// <remarks>
    ///   GET /api/pets/1?$expand=Owner,Appointments
    /// </remarks>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, ApiQueryOptions<Pet> options)
    {
        IQueryable<Pet> query = _db.Pets.Where(p => p.Id == id);

        if (options.Expand is not null)
        {
            query = query.ApplyExpand(options.Expand, options.Settings);
        }

        Pet? pet = await query.AsNoTracking().FirstOrDefaultAsync();
        return pet is null ? NotFound() : Ok(pet);
    }

    /// <summary>
    /// Returns all owners and their associated pets.
    /// </summary>
    /// <remarks>
    ///   GET /api/pets/owners?$filter=contains(Name, 'Alice')&amp;$orderby=Name asc
    /// </remarks>
    [HttpGet("owners")]
    public async Task<IActionResult> GetOwners(ApiQueryOptions<Owner> options)
    {
        try
        {
            List<Owner> results = await _db.Owners
                                   .Apply(options)
                                   .AsNoTracking()
                                   .ToListAsync();

            return Ok(PagedResponse.Create(results, options, Request));
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
    }
}