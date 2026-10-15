using ApiQueryOptions.Binding;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace ApiQueryOptions.Extensions;

/// <summary>
/// Service collection extensions for registering ApiQueryOptions model binding.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the <see cref="ApiQueryOptionsModelBinderProvider"/> and optionally
    /// a singleton <see cref="ApiQueryOptionsSettings"/> so that all auto-bound
    /// <see cref="ApiQueryOptions{T}"/> parameters share the same configuration.
    /// Calling it more than once registers the model binder provider once and keeps the
    /// settings from the last call.
    /// </summary>
    /// <example>
    /// <code>
    /// builder.Services.AddControllers();
    /// builder.Services.AddApiQueryOptions(new ApiQueryOptionsSettings { FilterEnabled = false });
    /// </code>
    /// </example>
    public static IServiceCollection AddApiQueryOptions(
        this IServiceCollection services,
        ApiQueryOptionsSettings? settings = null)
    {
        ApiQueryOptionsSettings resolved = settings ?? new ApiQueryOptionsSettings();
        services.RemoveAll<ApiQueryOptionsSettings>();
        services.AddSingleton(resolved);

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IConfigureOptions<MvcOptions>, ApiQueryOptionsMvcOptionsSetup>());

        return services;
    }

    /// <summary>
    /// Registers the <see cref="ApiQueryOptionsModelBinderProvider"/> and a singleton
    /// <see cref="ApiQueryOptionsSettings"/> configured via an inline lambda, so that all
    /// auto-bound <see cref="ApiQueryOptions{T}"/> parameters share the same configuration.
    /// </summary>
    /// <param name="services">
    /// The <see cref="IServiceCollection"/> to add the services to.
    /// </param>
    /// <param name="configure">
    /// A delegate that configures an <see cref="ApiQueryOptionsBuilder"/> used to build the
    /// <see cref="ApiQueryOptionsSettings"/> singleton.
    /// </param>
    /// <example>
    /// <code>
    /// builder.Services.AddControllers();
    /// builder.Services.AddApiQueryOptions(o =>
    /// {
    ///     o.FilterEnabled = false;
    ///     o.MaxPageSize = 100;
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddApiQueryOptions(
        this IServiceCollection services,
        Action<ApiQueryOptionsBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        ApiQueryOptionsBuilder builder = new();
        configure(builder);

        return services.AddApiQueryOptions(builder.Build());
    }
}