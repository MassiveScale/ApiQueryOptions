using Microsoft.EntityFrameworkCore;
using PetShop.EFCore.Models;

namespace PetShop.EFCore.Data;

public sealed class PetShopDbContext : DbContext
{
    public PetShopDbContext(DbContextOptions<PetShopDbContext> options) : base(options) { }

    public DbSet<Pet> Pets => Set<Pet>();
    public DbSet<Owner> Owners => Set<Owner>();
    public DbSet<Appointment> Appointments => Set<Appointment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Pet>()
            .HasOne(p => p.Owner)
            .WithMany(o => o.Pets)
            .HasForeignKey(p => p.OwnerId)
            .IsRequired(false);

        modelBuilder.Entity<Appointment>()
            .HasOne(a => a.Pet)
            .WithMany(p => p.Appointments)
            .HasForeignKey(a => a.PetId);

        SeedData(modelBuilder);
    }

    private static void SeedData(ModelBuilder mb)
    {
        mb.Entity<Owner>().HasData(
            new Owner { Id = 1, Name = "Alice Johnson", Email = "alice@example.com", Phone = "555-0101" },
            new Owner { Id = 2, Name = "Bob Smith", Email = "bob@example.com", Phone = "555-0102" },
            new Owner { Id = 3, Name = "Carol Williams", Email = "carol@example.com", Phone = "555-0103" }
        );

        mb.Entity<Pet>().HasData(
            new Pet { Id = 1, Name = "Buddy", Species = "Dog", Breed = "Labrador Retriever", AgeInMonths = 24, AdoptionFee = 150.00m, IsAvailable = false, OwnerId = 1 },
            new Pet { Id = 2, Name = "Whiskers", Species = "Cat", Breed = "Domestic Shorthair", AgeInMonths = 36, AdoptionFee = 75.00m, IsAvailable = false, OwnerId = 2 },
            new Pet { Id = 3, Name = "Goldie", Species = "Fish", Breed = "Goldfish", AgeInMonths = 6, AdoptionFee = 10.00m, IsAvailable = true, OwnerId = null },
            new Pet { Id = 4, Name = "Max", Species = "Dog", Breed = "German Shepherd", AgeInMonths = 18, AdoptionFee = 200.00m, IsAvailable = true, OwnerId = null },
            new Pet { Id = 5, Name = "Luna", Species = "Cat", Breed = "Siamese", AgeInMonths = 12, AdoptionFee = 100.00m, IsAvailable = false, OwnerId = 3 },
            new Pet { Id = 6, Name = "Charlie", Species = "Dog", Breed = "Beagle", AgeInMonths = 48, AdoptionFee = 125.00m, IsAvailable = true, OwnerId = null },
            new Pet { Id = 7, Name = "Bella", Species = "Dog", Breed = "Golden Retriever", AgeInMonths = 30, AdoptionFee = 175.00m, IsAvailable = true, OwnerId = null },
            new Pet { Id = 8, Name = "Shadow", Species = "Cat", Breed = "Persian", AgeInMonths = 60, AdoptionFee = 90.00m, IsAvailable = true, OwnerId = null },
            new Pet { Id = 9, Name = "Nemo", Species = "Fish", Breed = "Clownfish", AgeInMonths = 3, AdoptionFee = 15.00m, IsAvailable = true, OwnerId = null },
            new Pet { Id = 10, Name = "Tweety", Species = "Bird", Breed = "Canary", AgeInMonths = 9, AdoptionFee = 50.00m, IsAvailable = false, OwnerId = 1 }
        );

        mb.Entity<Appointment>().HasData(
            new Appointment { Id = 1, PetId = 1, ScheduledAt = new DateTime(2025, 3, 15, 10, 0, 0), Reason = "Annual checkup", IsCompleted = true },
            new Appointment { Id = 2, PetId = 1, ScheduledAt = new DateTime(2025, 6, 20, 9, 0, 0), Reason = "Vaccination booster", IsCompleted = false },
            new Appointment { Id = 3, PetId = 2, ScheduledAt = new DateTime(2025, 4, 5, 14, 0, 0), Reason = "Dental cleaning", IsCompleted = true },
            new Appointment { Id = 4, PetId = 5, ScheduledAt = new DateTime(2025, 7, 10, 11, 0, 0), Reason = "Spay/neuter consult", IsCompleted = false }
        );
    }
}