using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;

namespace ApiQueryOptions.Binding;

/// <summary>
/// Registers <see cref="ApiQueryOptionsModelBinder"/> for any parameter of type
/// <see cref="ApiQueryOptions{T}"/>.
/// </summary>
public sealed class ApiQueryOptionsModelBinderProvider : IModelBinderProvider
{
    /// <summary>
    /// Returns an <see cref="ApiQueryOptionsModelBinder"/> when the parameter type is a
    /// closed <see cref="ApiQueryOptions{T}"/>; otherwise returns <c>null</c>.
    /// </summary>
    /// <param name="context">The model binder provider context.</param>
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        Type modelType = context.Metadata.ModelType;

        if (!modelType.IsGenericType ||
            modelType.GetGenericTypeDefinition() != typeof(ApiQueryOptions<>))
        {
            return null;
        }

        ApiQueryOptionsSettings settings = context.Services.GetService<ApiQueryOptionsSettings>()
            ?? new ApiQueryOptionsSettings();

        return new ApiQueryOptionsModelBinder(modelType, settings);
    }
}