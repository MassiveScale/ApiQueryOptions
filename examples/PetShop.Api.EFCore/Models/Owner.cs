using System.Text.Json.Serialization;

namespace PetShop.EFCore.Models;

/// <summary>A pet owner with one or more pets.</summary>
public sealed class Owner
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;

    // Back-navigation excluded from serialization to prevent circular references.
    [JsonIgnore]
    public ICollection<Pet> Pets { get; set; } = [];
}