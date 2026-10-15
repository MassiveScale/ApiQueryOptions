using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ApiQueryOptions.Binding;

/// <summary>
/// Inserts the <see cref="ApiQueryOptionsModelBinderProvider"/> at the front of
/// <see cref="MvcOptions.ModelBinderProviders"/> unless it is already registered.
/// </summary>
internal sealed class ApiQueryOptionsMvcOptionsSetup : IConfigureOptions<MvcOptions>
{
    /// <summary>
    /// Adds the model binder provider to <paramref name="options"/>.
    /// </summary>
    public void Configure(MvcOptions options)
    {
        if (!options.ModelBinderProviders.Any(provider => provider is ApiQueryOptionsModelBinderProvider))
        {
            options.ModelBinderProviders.Insert(0, new ApiQueryOptionsModelBinderProvider());
        }
    }
}
