using System.Reflection;
using ApiQueryOptions.Binding;
using ApiQueryOptions.Extensions;
using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using Moq;

namespace ApiQueryOptions.Tests;

[TestClass]
public class ModelBinderTests
{
    private static DefaultHttpContext BuildHttpContext(params (string key, string value)[] queryPairs)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.QueryString = new QueryString(
            "?" + string.Join("&", queryPairs.Select(p => $"{Uri.EscapeDataString(p.key)}={Uri.EscapeDataString(p.value)}")));
        return ctx;
    }

    private static DefaultModelBindingContext BuildBindingContext(
        HttpContext httpContext,
        Type modelType,
        ActionDescriptor? actionDescriptor = null)
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton(new ApiQueryOptionsSettings());
        ServiceProvider services = serviceCollection.BuildServiceProvider();
        httpContext.RequestServices = services;

        var metadataProvider = new EmptyModelMetadataProvider();
        ModelMetadata metadata = metadataProvider.GetMetadataForType(modelType);

        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            actionDescriptor ?? new ActionDescriptor());

        return new DefaultModelBindingContext
        {
            ModelMetadata = metadata,
            ModelName = "options",
            ModelState = new ModelStateDictionary(),
            ActionContext = actionContext,
        };
    }

    private static ControllerActionDescriptor BuildControllerDescriptor(MethodInfo methodInfo)
    {
        return new ControllerActionDescriptor
        {
            MethodInfo = methodInfo,
            ControllerTypeInfo = methodInfo.DeclaringType!.GetTypeInfo(),
        };
    }

    [TestMethod]
    public async Task ModelBinder_SetsResult_WithTop()
    {
        DefaultHttpContext httpContext = BuildHttpContext(("$top", "5"));
        var settings = new ApiQueryOptionsSettings();
        var binder = new ApiQueryOptionsModelBinder(typeof(ApiQueryOptions<BinderEntity>), settings);
        DefaultModelBindingContext ctx = BuildBindingContext(httpContext, typeof(ApiQueryOptions<BinderEntity>));

        await binder.BindModelAsync(ctx);

        ctx.Result.IsModelSet.Should().BeTrue();
        ApiQueryOptions<BinderEntity> result = ctx.Result.Model.Should().BeOfType<ApiQueryOptions<BinderEntity>>().Subject;
        result.Top!.Value.Should().Be(5);
    }

    [TestMethod]
    public async Task ModelBinder_NoQuery_AllNullOptions()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.RequestServices = new ServiceCollection()
            .AddSingleton(new ApiQueryOptionsSettings())
            .BuildServiceProvider();

        var settings = new ApiQueryOptionsSettings();
        var binder = new ApiQueryOptionsModelBinder(typeof(ApiQueryOptions<BinderEntity>), settings);
        DefaultModelBindingContext ctx = BuildBindingContext(httpContext, typeof(ApiQueryOptions<BinderEntity>));

        await binder.BindModelAsync(ctx);

        ApiQueryOptions<BinderEntity> result = ctx.Result.Model.Should().BeOfType<ApiQueryOptions<BinderEntity>>().Subject;
        result.Filter.Should().BeNull();
        result.Top.Should().BeNull();
    }

    [TestMethod]
    public void AddApiQueryOptions_RegistersSettings()
    {
        var services = new ServiceCollection();
        services.AddControllers();
        var settings = new ApiQueryOptionsSettings { TopEnabled = false };
        services.AddApiQueryOptions(settings);

        ServiceProvider provider = services.BuildServiceProvider();
        ApiQueryOptionsSettings resolved = provider.GetRequiredService<ApiQueryOptionsSettings>();
        resolved.TopEnabled.Should().BeFalse();
    }

    [TestMethod]
    public void AddApiQueryOptions_DefaultSettings_Registered()
    {
        var services = new ServiceCollection();
        services.AddControllers();
        services.AddApiQueryOptions();

        ServiceProvider provider = services.BuildServiceProvider();
        ApiQueryOptionsSettings resolved = provider.GetRequiredService<ApiQueryOptionsSettings>();
        resolved.TopEnabled.Should().BeTrue();
    }

    [TestMethod]
    public void BinderProvider_ReturnsNull_ForNonQueryOptionsType()
    {
        ServiceProvider services = new ServiceCollection()
            .AddSingleton(new ApiQueryOptionsSettings())
            .BuildServiceProvider();

        var metadataProvider = new EmptyModelMetadataProvider();
        ModelMetadata metadata = metadataProvider.GetMetadataForType(typeof(string));

        var mockCtx = new Mock<ModelBinderProviderContext>();
        mockCtx.Setup(c => c.Metadata).Returns(metadata);
        mockCtx.Setup(c => c.Services).Returns(services);

        var provider = new ApiQueryOptionsModelBinderProvider();
        provider.GetBinder(mockCtx.Object).Should().BeNull();
    }

    [TestMethod]
    public void BinderProvider_ReturnsNonNull_ForQueryOptionsType()
    {
        ServiceProvider services = new ServiceCollection()
            .AddSingleton(new ApiQueryOptionsSettings())
            .BuildServiceProvider();

        var metadataProvider = new EmptyModelMetadataProvider();
        ModelMetadata metadata = metadataProvider.GetMetadataForType(typeof(ApiQueryOptions<BinderEntity>));

        var mockCtx = new Mock<ModelBinderProviderContext>();
        mockCtx.Setup(c => c.Metadata).Returns(metadata);
        mockCtx.Setup(c => c.Services).Returns(services);

        var provider = new ApiQueryOptionsModelBinderProvider();
        provider.GetBinder(mockCtx.Object).Should().NotBeNull();
    }

    [TestMethod]
    public void BinderProvider_WhenNoSettingsRegistered_UsesDefaults()
    {
        ServiceProvider services = new ServiceCollection().BuildServiceProvider();

        var metadataProvider = new EmptyModelMetadataProvider();
        ModelMetadata metadata = metadataProvider.GetMetadataForType(typeof(ApiQueryOptions<BinderEntity>));

        var mockCtx = new Mock<ModelBinderProviderContext>();
        mockCtx.Setup(c => c.Metadata).Returns(metadata);
        mockCtx.Setup(c => c.Services).Returns(services);

        var provider = new ApiQueryOptionsModelBinderProvider();
        provider.GetBinder(mockCtx.Object).Should().NotBeNull();
    }

    [TestMethod]
    public async Task ModelBinder_ApiQueryOptionsAttribute_DefaultPageSize_OnController_AppliesDefault()
    {
        DefaultHttpContext httpContext = BuildHttpContext();
        ControllerActionDescriptor descriptor = BuildControllerDescriptor(
            typeof(ClassAttributeController).GetMethod(nameof(ClassAttributeController.SomeAction))!);
        var binder = new ApiQueryOptionsModelBinder(typeof(ApiQueryOptions<BinderEntity>), new ApiQueryOptionsSettings());
        DefaultModelBindingContext ctx = BuildBindingContext(httpContext, typeof(ApiQueryOptions<BinderEntity>), descriptor);

        await binder.BindModelAsync(ctx);

        ApiQueryOptions<BinderEntity> result = ctx.Result.Model.Should().BeOfType<ApiQueryOptions<BinderEntity>>().Subject;
        result.Top!.Value.Should().Be(30);
    }

    [TestMethod]
    public async Task ModelBinder_ApiQueryOptionsAttribute_DefaultPageSize_OnMethod_AppliesDefault()
    {
        DefaultHttpContext httpContext = BuildHttpContext();
        ControllerActionDescriptor descriptor = BuildControllerDescriptor(
            typeof(MethodAttributeController).GetMethod(nameof(MethodAttributeController.DefaultPageSizeAction))!);
        var binder = new ApiQueryOptionsModelBinder(typeof(ApiQueryOptions<BinderEntity>), new ApiQueryOptionsSettings());
        DefaultModelBindingContext ctx = BuildBindingContext(httpContext, typeof(ApiQueryOptions<BinderEntity>), descriptor);

        await binder.BindModelAsync(ctx);

        ApiQueryOptions<BinderEntity> result = ctx.Result.Model.Should().BeOfType<ApiQueryOptions<BinderEntity>>().Subject;
        result.Top!.Value.Should().Be(20);
    }

    [TestMethod]
    public async Task ModelBinder_ApiQueryOptionsAttribute_FilterDisabled_OnMethod_DisablesFilter()
    {
        DefaultHttpContext httpContext = BuildHttpContext(("$filter", "Name eq 'x'"));
        ControllerActionDescriptor descriptor = BuildControllerDescriptor(
            typeof(MethodAttributeController).GetMethod(nameof(MethodAttributeController.FilterDisabledAction))!);
        var binder = new ApiQueryOptionsModelBinder(typeof(ApiQueryOptions<BinderEntity>), new ApiQueryOptionsSettings());
        DefaultModelBindingContext ctx = BuildBindingContext(httpContext, typeof(ApiQueryOptions<BinderEntity>), descriptor);

        await binder.BindModelAsync(ctx);

        ApiQueryOptions<BinderEntity> result = ctx.Result.Model.Should().BeOfType<ApiQueryOptions<BinderEntity>>().Subject;
        result.Filter.Should().BeNull();
        result.Settings.FilterEnabled.Should().BeFalse();
    }

    [TestMethod]
    public async Task ModelBinder_ApiQueryOptionsAttribute_MaxPageSize_MethodOverridesController()
    {
        DefaultHttpContext httpContext = BuildHttpContext(("$top", "999"));
        ControllerActionDescriptor descriptor = BuildControllerDescriptor(
            typeof(ClassAttributeController).GetMethod(nameof(ClassAttributeController.OverridingAction))!);
        var binder = new ApiQueryOptionsModelBinder(typeof(ApiQueryOptions<BinderEntity>), new ApiQueryOptionsSettings());
        DefaultModelBindingContext ctx = BuildBindingContext(httpContext, typeof(ApiQueryOptions<BinderEntity>), descriptor);

        await binder.BindModelAsync(ctx);

        ApiQueryOptions<BinderEntity> result = ctx.Result.Model.Should().BeOfType<ApiQueryOptions<BinderEntity>>().Subject;
        result.Top!.Value.Should().Be(25);
    }

    [TestMethod]
    public async Task ModelBinder_ApiQueryOptionsAttribute_MaxPageSize_OnController_ClampsTop()
    {
        DefaultHttpContext httpContext = BuildHttpContext(("$top", "999"));
        ControllerActionDescriptor descriptor = BuildControllerDescriptor(
            typeof(ClassAttributeController).GetMethod(nameof(ClassAttributeController.SomeAction))!);
        var binder = new ApiQueryOptionsModelBinder(typeof(ApiQueryOptions<BinderEntity>), new ApiQueryOptionsSettings());
        DefaultModelBindingContext ctx = BuildBindingContext(httpContext, typeof(ApiQueryOptions<BinderEntity>), descriptor);

        await binder.BindModelAsync(ctx);

        ApiQueryOptions<BinderEntity> result = ctx.Result.Model.Should().BeOfType<ApiQueryOptions<BinderEntity>>().Subject;
        result.Top!.Value.Should().Be(75);
    }

    [TestMethod]
    public async Task ModelBinder_ApiQueryOptionsAttribute_MaxPageSize_OnMethod_ClampsTop()
    {
        DefaultHttpContext httpContext = BuildHttpContext(("$top", "999"));
        ControllerActionDescriptor descriptor = BuildControllerDescriptor(
            typeof(MethodAttributeController).GetMethod(nameof(MethodAttributeController.MaxPageSizeAction))!);
        var binder = new ApiQueryOptionsModelBinder(typeof(ApiQueryOptions<BinderEntity>), new ApiQueryOptionsSettings());
        DefaultModelBindingContext ctx = BuildBindingContext(httpContext, typeof(ApiQueryOptions<BinderEntity>), descriptor);

        await binder.BindModelAsync(ctx);

        ApiQueryOptions<BinderEntity> result = ctx.Result.Model.Should().BeOfType<ApiQueryOptions<BinderEntity>>().Subject;
        result.Top!.Value.Should().Be(50);
    }

    [TestMethod]
    public async Task ModelBinder_NoAttribute_UsesRegisteredSettings()
    {
        DefaultHttpContext httpContext = BuildHttpContext(("$top", "10"));
        ControllerActionDescriptor descriptor = BuildControllerDescriptor(
            typeof(MethodAttributeController).GetMethod(nameof(MethodAttributeController.NoAttributeAction))!);
        var settings = new ApiQueryOptionsSettings { MaxPageSize = 100 };
        var binder = new ApiQueryOptionsModelBinder(typeof(ApiQueryOptions<BinderEntity>), settings);
        DefaultModelBindingContext ctx = BuildBindingContext(httpContext, typeof(ApiQueryOptions<BinderEntity>), descriptor);

        await binder.BindModelAsync(ctx);

        ApiQueryOptions<BinderEntity> result = ctx.Result.Model.Should().BeOfType<ApiQueryOptions<BinderEntity>>().Subject;
        result.Top!.Value.Should().Be(10);
        result.Settings.MaxPageSize.Should().Be(100);
    }

    [TestMethod]
    public async Task ModelBinder_NonControllerActionDescriptor_UsesRegisteredSettings()
    {
        DefaultHttpContext httpContext = BuildHttpContext(("$top", "10"));
        var settings = new ApiQueryOptionsSettings { MaxPageSize = 100 };
        var binder = new ApiQueryOptionsModelBinder(typeof(ApiQueryOptions<BinderEntity>), settings);

        DefaultModelBindingContext ctx = BuildBindingContext(
            httpContext,
            typeof(ApiQueryOptions<BinderEntity>),
            new ActionDescriptor());

        await binder.BindModelAsync(ctx);

        ApiQueryOptions<BinderEntity> result = ctx.Result.Model.Should().BeOfType<ApiQueryOptions<BinderEntity>>().Subject;
        result.Top!.Value.Should().Be(10);
        result.Settings.MaxPageSize.Should().Be(100);
    }
}

[ApiQueryOptions(DefaultPageSize = 30, MaxPageSize = 75)]
internal sealed class ClassAttributeController
{
    /// <summary>
    /// Action that inherits both class-level attribute values.
    /// </summary>
    public void SomeAction()
    { }

    /// <summary>
    /// Action whose method-level attribute overrides the class-level MaxPageSize.
    /// </summary>
    [ApiQueryOptions(MaxPageSize = 25)]
    public void OverridingAction()
    { }
}

internal sealed class MethodAttributeController
{
    /// <summary>
    /// Action with a MaxPageSize override.
    /// </summary>
    [ApiQueryOptions(MaxPageSize = 50)]
    public void MaxPageSizeAction()
    { }

    /// <summary>
    /// Action with a DefaultPageSize override.
    /// </summary>
    [ApiQueryOptions(DefaultPageSize = 20)]
    public void DefaultPageSizeAction()
    { }

    /// <summary>
    /// Action with FilterEnabled disabled.
    /// </summary>
    [ApiQueryOptions(FilterEnabled = QueryOptionState.Disabled)]
    public void FilterDisabledAction()
    { }

    /// <summary>
    /// Action with no attribute — uses registered settings.
    /// </summary>
    public void NoAttributeAction()
    { }
}

internal sealed class BinderEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}