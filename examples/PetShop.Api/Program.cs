using ApiQueryOptions;
using ApiQueryOptions.Extensions;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// Register ApiQueryOptions with default settings (all options enabled).
// Adjust ApiQueryOptionsSettings to restrict what callers can use, e.g.:
//
//   builder.Services.AddApiQueryOptions(new ApiQueryOptionsSettings
//   {
//       ExpandEnabled    = false,   // no $expand — this example has no navigation properties
//       SkipTokenEnabled = false,   // using offset paging ($top / $skip) instead
//   });
builder.Services.AddApiQueryOptions();

WebApplication app = builder.Build();

app.UseAuthorization();
app.MapControllers();

app.Run();