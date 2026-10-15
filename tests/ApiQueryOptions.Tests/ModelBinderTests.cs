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

    [TestMethod]
    public async Task ModelBinder_BoundInstance_NextLinkIsAbsoluteUrl()
    {
        DefaultHttpContext httpContext = BuildHttpContext(("$top", "2"), ("tenant", "a"));
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("api.example.com");
        httpContext.Request.Path = "/pets";
        var binder = new ApiQueryOptionsModelBinder(typeof(ApiQueryOptions<BinderEntity>), new ApiQueryOptionsSettings());
        DefaultModelBindingContext ctx = BuildBindingContext(httpContext, typeof(ApiQueryOptions<BinderEntity>));

        await binder.BindModelAsync(ctx);

        ApiQueryOptions<BinderEntity> result = ctx.Result.Model.Should().BeOfType<ApiQueryOptions<BinderEntity>>().Subject;
        string? nextLink = result.NextLink(resultCount: 2);
        nextLink.Should().StartWith("https://api.example.com/pets?tenant=a&$skiptoken=");
    }

    [TestMethod]
    public async Task ModelBinder_MalformedSkipToken_AddsModelStateError()
    {
        DefaultHttpContext httpContext = BuildHttpContext(("$skiptoken", "!!!not-a-token!!!"));
        var binder = new ApiQueryOptionsModelBinder(typeof(ApiQueryOptions<BinderEntity>), new ApiQueryOptionsSettings());
        DefaultModelBindingContext ctx = BuildBindingContext(httpContext, typeof(ApiQueryOptions<BinderEntity>));

        await binder.BindModelAsync(ctx);

        ctx.Result.IsModelSet.Should().BeFalse();
        ctx.ModelState.IsValid.Should().BeFalse();
        ctx.ModelState["$skiptoken"]!.Errors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Contain("skip token");
    }

    [TestMethod]
    public async Task ModelBinder_MalformedSkipToken_CustomName_UsesConfiguredKey()
    {
        var settings = new ApiQueryOptionsSettings { SkipTokenParameterNames = ["cursor"] };
        string notJson = Convert.ToBase64String("not json"u8.ToArray()).TrimEnd('=');
        DefaultHttpContext httpContext = BuildHttpContext(("cursor", notJson));
        var binder = new ApiQueryOptionsModelBinder(typeof(ApiQueryOptions<BinderEntity>), settings);
        DefaultModelBindingContext ctx = BuildBindingContext(httpContext, typeof(ApiQueryOptions<BinderEntity>));

        await binder.BindModelAsync(ctx);

        ctx.Result.IsModelSet.Should().BeFalse();
        ctx.ModelState.Should().ContainKey("cursor");
    }

    [TestMethod]
    public async Task ModelBinder_SameAction_ReusesResolvedSettings()
    {
        ControllerActionDescriptor descriptor = BuildControllerDescriptor(
            typeof(ClassAttributeController).GetMethod(nameof(ClassAttributeController.SomeAction))!);
        var binder = new ApiQueryOptionsModelBinder(typeof(ApiQueryOptions<BinderEntity>), new ApiQueryOptionsSettings());
        DefaultModelBindingContext first = BuildBindingContext(BuildHttpContext(), typeof(ApiQueryOptions<BinderEntity>), descriptor);
        DefaultModelBindingContext second = BuildBindingContext(BuildHttpContext(), typeof(ApiQueryOptions<BinderEntity>), descriptor);

        await binder.BindModelAsync(first);
        await binder.BindModelAsync(second);

        var firstResult = (ApiQueryOptions<BinderEntity>)first.Result.Model!;
        var secondResult = (ApiQueryOptions<BinderEntity>)second.Result.Model!;
        firstResult.Settings.Should().BeSameAs(secondResult.Settings);
        firstResult.Settings.DefaultPageSize.Should().Be(30);
    }

    [TestMethod]
    public async Task ModelBinder_ControllerActionWithoutAttribute_UsesRegisteredSettingsInstance()
    {
        ControllerActionDescriptor descriptor = BuildControllerDescriptor(
            typeof(MethodAttributeController).GetMethod(nameof(MethodAttributeController.NoAttributeAction))!);
        var settings = new ApiQueryOptionsSettings();
        var binder = new ApiQueryOptionsModelBinder(typeof(ApiQueryOptions<BinderEntity>), settings);
        DefaultModelBindingContext ctx = BuildBindingContext(BuildHttpContext(), typeof(ApiQueryOptions<BinderEntity>), descriptor);

        await binder.BindModelAsync(ctx);

        ((ApiQueryOptions<BinderEntity>)ctx.Result.Model!).Settings.Should().BeSameAs(settings);
    }

    [TestMethod]
    public async Task ModelBinder_AttributeOverride_KeepsCustomParameterNames()
    {
        var settings = new ApiQueryOptionsSettings { FilterParameterNames = ["q"], TopParameterNames = ["limit"] };
        DefaultHttpContext httpContext = BuildHttpContext(("q", "Name eq 'x'"), ("limit", "500"));
        ControllerActionDescriptor descriptor = BuildControllerDescriptor(
            typeof(MethodAttributeController).GetMethod(nameof(MethodAttributeController.MaxPageSizeAction))!);
        var binder = new ApiQueryOptionsModelBinder(typeof(ApiQueryOptions<BinderEntity>), settings);
        DefaultModelBindingContext ctx = BuildBindingContext(httpContext, typeof(ApiQueryOptions<BinderEntity>), descriptor);

        await binder.BindModelAsync(ctx);

        ApiQueryOptions<BinderEntity> result = ctx.Result.Model.Should().BeOfType<ApiQueryOptions<BinderEntity>>().Subject;
        result.Filter!.RawValue.Should().Be("Name eq 'x'");
        result.Top!.Value.Should().Be(50);
    }

    [TestMethod]
    [DataRow(typeof(string))]
    [DataRow(typeof(List<int>))]
    [DataRow(typeof(ApiQueryOptions<>))]
    public void ModelBinder_Ctor_NotClosedApiQueryOptionsType_ThrowsArgumentException(Type type)
    {
        Action act = () => _ = new ApiQueryOptionsModelBinder(type, new ApiQueryOptionsSettings());

        act.Should().Throw<ArgumentException>().WithParameterName("optionsType");
    }

    [TestMethod]
    public void ModelBinder_Ctor_ConstructedButOpenType_ThrowsArgumentException()
    {
        Type openArgument = typeof(ApiQueryOptions<>).MakeGenericType(typeof(List<>));

        Action act = () => _ = new ApiQueryOptionsModelBinder(openArgument, new ApiQueryOptionsSettings());

        act.Should().Throw<ArgumentException>().WithParameterName("optionsType");
    }

    [TestMethod]
    public void ModelBinder_Ctor_NullArguments_ThrowArgumentNull()
    {
        Action nullType = () => _ = new ApiQueryOptionsModelBinder(null!, new ApiQueryOptionsSettings());
        Action nullSettings = () => _ = new ApiQueryOptionsModelBinder(typeof(ApiQueryOptions<BinderEntity>), null!);

        nullType.Should().Throw<ArgumentNullException>().WithParameterName("optionsType");
        nullSettings.Should().Throw<ArgumentNullException>().WithParameterName("settings");
    }

    [TestMethod]
    public async Task ModelBinder_BindModelAsync_NullContext_ThrowsArgumentNull()
    {
        var binder = new ApiQueryOptionsModelBinder(typeof(ApiQueryOptions<BinderEntity>), new ApiQueryOptionsSettings());

        Func<Task> act = () => binder.BindModelAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
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