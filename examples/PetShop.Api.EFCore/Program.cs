using ApiQueryOptions;
using ApiQueryOptions.Extensions;
using Microsoft.EntityFrameworkCore;
using PetShop.EFCore.Data;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// Register ApiQueryOptions — all options enabled by default.
// The EF Core example uses $expand, so ExpandEnabled must stay true.
builder.Services.AddApiQueryOptions(new ApiQueryOptionsSettings()
{
     DefaultPageSize = 10,
     MaxPageSize = 100
});

// Use an in-memory database so the example runs without any external dependency.
// Replace with UseSqlServer / UseNpgsql / UseSqlite for a real database.
builder.Services.AddDbContext<PetShopDbContext>(opt =>
    opt.UseInMemoryDatabase("PetShop"));

WebApplication app = builder.Build();

// Seed the in-memory database on startup.
using (IServiceScope scope = app.Services.CreateScope())
{
    PetShopDbContext db = scope.ServiceProvider.GetRequiredService<PetShopDbContext>();
    db.Database.EnsureCreated();
}

app.UseAuthorization();
app.MapControllers();

app.Run();