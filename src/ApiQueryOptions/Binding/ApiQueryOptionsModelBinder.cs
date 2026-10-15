using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ApiQueryOptions.Binding;

/// <summary>
/// Model binder that constructs <see cref="ApiQueryOptions{T}"/> from the current
/// HTTP request, injecting the registered <see cref="ApiQueryOptionsSettings"/>.
/// A per-action or per-controller <see cref="ApiQueryOptionsAttribute"/> is applied on top of
/// the registered settings when present.
/// The bound instance keeps a reference to the request, so <see cref="ApiQueryOptions{T}.NextLink(int, int?)"/>
/// returns an absolute URL. A malformed <c>$skiptoken</c> is reported as a model state error,
/// which <see cref="ApiControllerAttribute"/> turns into a <c>400 Bad Request</c>.
/// </summary>
public sealed class ApiQueryOptionsModelBinder : IModelBinder
{
    private static readonly ConcurrentDictionary<Type, Func<HttpRequest, ApiQueryOptionsSettings, object>> _factories = new();
    private static readonly MethodInfo _createFactoryMethod =
        typeof(ApiQueryOptionsModelBinder).GetMethod(nameof(CreateFactory), BindingFlags.NonPublic | BindingFlags.Static)!;

    private readonly Func<HttpRequest, ApiQueryOptionsSettings, object> _factory;
    private readonly ApiQueryOptionsSettings _settings;
    private readonly ConditionalWeakTable<ActionDescriptor, ApiQueryOptionsSettings> _settingsByAction = new();
    private readonly ConditionalWeakTable<ActionDescriptor, ApiQueryOptionsSettings>.CreateValueCallback _resolveSettings;

    /// <summary>
    /// Initializes a new instance for the given closed <see cref="ApiQueryOptions{T}"/> type
    /// using the supplied <paramref name="settings"/>.
    /// </summary>
    /// <param name="optionsType">The closed generic <see cref="ApiQueryOptions{T}"/> type to construct.</param>
    /// <param name="settings">The settings to inject into the constructed instance.</param>
    /// <exception cref="ArgumentException">
    /// When <paramref name="optionsType"/> is not a closed <see cref="ApiQueryOptions{T}"/> type.
    /// </exception>
    public ApiQueryOptionsModelBinder(Type optionsType, ApiQueryOptionsSettings settings)
    {
        ArgumentNullException.ThrowIfNull(optionsType);
        ArgumentNullException.ThrowIfNull(settings);

        if (!optionsType.IsConstructedGenericType || optionsType.GetGenericTypeDefinition() != typeof(ApiQueryOptions<>))
        {
            throw new ArgumentException(
                $"Type '{optionsType}' is not a closed {typeof(ApiQueryOptions<>).Name} type.", nameof(optionsType));
        }

        _factory = _factories.GetOrAdd(optionsType, static type =>
            (Func<HttpRequest, ApiQueryOptionsSettings, object>)_createFactoryMethod
                .MakeGenericMethod(type.GetGenericArguments()[0])
                .Invoke(null, null)!);
        _settings = settings;
        _resolveSettings = ResolveSettings;
    }

    /// <summary>
    /// Constructs an <see cref="ApiQueryOptions{T}"/> instance from the current HTTP request,
    /// merging any per-action or per-controller <see cref="ApiQueryOptionsAttribute"/> into the
    /// registered settings before construction.
    /// </summary>
    /// <param name="bindingContext">The active model binding context.</param>
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);

        ApiQueryOptionsSettings settings = GetSettings(bindingContext.ActionContext);

        try
        {
            object instance = _factory(bindingContext.HttpContext.Request, settings);
            bindingContext.Result = ModelBindingResult.Success(instance);
        }
        catch (FormatException ex)
        {
            // Only a malformed $skiptoken throws FormatException during construction.
            bindingContext.ModelState.TryAddModelError(settings.SkipTokenParameterNames[0], ex.Message);
            bindingContext.Result = ModelBindingResult.Failed();
        }

        return Task.CompletedTask;
    }

    private static Func<HttpRequest, ApiQueryOptionsSettings, object> CreateFactory<T>() =>
        static (request, settings) => ApiQueryOptions<T>.FromRequest(request, settings);

    private ApiQueryOptionsSettings GetSettings(ActionContext actionContext)
    {
        if (actionContext.ActionDescriptor is not ControllerActionDescriptor descriptor)
        {
            return _settings;
        }

        return _settingsByAction.GetValue(descriptor, _resolveSettings);
    }

    private ApiQueryOptionsSettings ResolveSettings(ActionDescriptor actionDescriptor)
    {
        var descriptor = (ControllerActionDescriptor)actionDescriptor;

        ApiQueryOptionsAttribute? attr =
            descriptor.MethodInfo.GetCustomAttribute<ApiQueryOptionsAttribute>()
            ?? descriptor.ControllerTypeInfo.GetCustomAttribute<ApiQueryOptionsAttribute>();

        return attr is null ? _settings : attr.ApplyTo(_settings);
    }
}
