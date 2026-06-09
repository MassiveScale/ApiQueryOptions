using System.Text.Json.Serialization;

namespace PetShop.EFCore.Models;

/// <summary>A scheduled vet appointment for a pet.</summary>
public sealed class Appointment
{
    public int Id { get; set; }
    public int PetId { get; set; }
    public DateTime ScheduledAt { get; set; }
    public string Reason { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }

    // Back-navigation excluded from serialization to prevent circular references.
    [JsonIgnore]
    public Pet Pet { get; set; } = null!;
}