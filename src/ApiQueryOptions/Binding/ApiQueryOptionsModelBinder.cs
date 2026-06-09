using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ApiQueryOptions.Binding;

/// <summary>
/// Model binder that constructs <see cref="ApiQueryOptions{T}"/> from the current
/// HTTP request query string, injecting the registered <see cref="ApiQueryOptionsSettings"/>.
/// A per-action or per-controller <see cref="ApiQueryOptionsAttribute"/> is applied on top of
/// the registered settings when present.
/// </summary>
public sealed class ApiQueryOptionsModelBinder : IModelBinder
{
    private readonly Type _optionsType;
    private readonly ApiQueryOptionsSettings _settings;

    /// <summary>
    /// Initializes a new instance for the given closed <see cref="ApiQueryOptions{T}"/> type
    /// using the supplied <paramref name="settings"/>.
    /// </summary>
    /// <param name="optionsType">The closed generic <see cref="ApiQueryOptions{T}"/> type to construct.</param>
    /// <param name="settings">The settings to inject into the constructed instance.</param>
    public ApiQueryOptionsModelBinder(Type optionsType, ApiQueryOptionsSettings settings)
    {
        _optionsType = optionsType;
        _settings = settings;
    }

    /// <summary>
    /// Constructs an <see cref="ApiQueryOptions{T}"/> instance from the current HTTP request
    /// query string, merging any per-action or per-controller page-size attributes into the
    /// registered settings before construction.
    /// </summary>
    /// <param name="bindingContext">The active model binding context.</param>
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        IQueryCollection query = bindingContext.HttpContext.Request.Query;

        ApiQueryOptionsSettings settings = ResolveSettings(bindingContext.ActionContext);

        ConstructorInfo constructorInfo = _optionsType.GetConstructor(
            [typeof(IQueryCollection), typeof(ApiQueryOptionsSettings)])!;

        object instance = constructorInfo.Invoke([query, settings]);
        bindingContext.Result = ModelBindingResult.Success(instance);
        return Task.CompletedTask;
    }

    private ApiQueryOptionsSettings ResolveSettings(ActionContext actionContext)
    {
        if (actionContext?.ActionDescriptor is not ControllerActionDescriptor descriptor)
        {
            return _settings;
        }

        ApiQueryOptionsAttribute? attr =
            descriptor.MethodInfo.GetCustomAttribute<ApiQueryOptionsAttribute>()
            ?? descriptor.ControllerTypeInfo.GetCustomAttribute<ApiQueryOptionsAttribute>();

        return attr is null ? _settings : attr.ApplyTo(_settings);
    }
}