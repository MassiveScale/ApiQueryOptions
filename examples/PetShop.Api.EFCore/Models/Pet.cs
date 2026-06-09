namespace PetShop.EFCore.Models;

/// <summary>
/// Represents an animal available for adoption at the pet shop.
/// </summary>
public sealed class Pet
{
    /// <summary>
    /// The fee required to adopt this pet, in the local currency.
    /// </summary>
    public decimal AdoptionFee { get; set; }

    /// <summary>
    /// The pet's age in months.
    /// </summary>
    public int AgeInMonths { get; set; }

    /// <summary>
    /// The collection of appointments for this pet, such as veterinary visits or adoption meetings.
    /// </summary>
    public ICollection<Appointment> Appointments { get; set; } = [];

    /// <summary>
    /// The pet's breed, if applicable. This may be <c>null</c> for mixed-breed pets or when the breed is unknown.
    /// </summary>
    public string? Breed { get; set; }

    /// <summary>
    /// The unique identifier for this pet, assigned by the database upon insertion.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Indicates whether this pet is currently available for adoption. If <c>false</c>, the pet may have already been adopted or is otherwise not available.
    /// </summary>
    public bool IsAvailable { get; set; }

    /// <summary>
    /// The pet's name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The pet's owner, if it has one.
    /// </summary>
    public Owner? Owner { get; set; }

    /// <summary>
    /// The foreign key for the pet's owner.
    /// </summary>
    public int? OwnerId { get; set; }

    /// <summary>
    /// The species of the pet, such as "Dog", "Cat", etc.
    /// </summary>
    public string Species { get; set; } = string.Empty;
}