namespace PetShop.Api.Models;

/// <summary>Represents an animal available for adoption at the pet shop.</summary>
public sealed class Pet
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Species { get; set; } = string.Empty;
    public string? Breed { get; set; }
    public int AgeInMonths { get; set; }
    public decimal AdoptionFee { get; set; }
    public bool IsAvailable { get; set; }
}