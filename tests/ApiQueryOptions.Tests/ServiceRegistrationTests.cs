using ApiQueryOptions.Binding;
using ApiQueryOptions.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace ApiQueryOptions.Tests;

[TestClass]
public class ServiceRegistrationTests
{
    private static int ProviderCount(IServiceProvider provider) =>
        provider.GetRequiredService<IOptions<MvcOptions>>().Value.ModelBinderProviders
            .Count(p => p is ApiQueryOptionsModelBinderProvider);

    [TestMethod]
    public void AddApiQueryOptions_CalledTwice_RegistersProviderOnceAtFront()
    {
        var services = new ServiceCollection();
        services.AddControllers();

        services.AddApiQueryOptions();
        services.AddApiQueryOptions(o => o.MaxPageSize = 10);

        ServiceProvider provider = services.BuildServiceProvider();
        ProviderCount(provider).Should().Be(1);
        provider.GetRequiredService<IOptions<MvcOptions>>().Value.ModelBinderProviders[0]
            .Should().BeOfType<ApiQueryOptionsModelBinderProvider>();
    }

    [TestMethod]
    public void AddApiQueryOptions_CalledTwice_LastSettingsWin()
    {
        var services = new ServiceCollection();
        var first = new ApiQueryOptionsSettings { MaxPageSize = 5 };
        var second = new ApiQueryOptionsSettings { MaxPageSize = 50 };

        services.AddApiQueryOptions(first);
        services.AddApiQueryOptions(second);

        ServiceProvider provider = services.BuildServiceProvider();
        provider.GetServices<ApiQueryOptionsSettings>().Should().ContainSingle().Which.Should().BeSameAs(second);
    }

    [TestMethod]
    public void AddApiQueryOptions_ProviderAlreadyAdded_DoesNotAddAnother()
    {
        var services = new ServiceCollection();
        services.AddControllers(o => o.ModelBinderProviders.Add(new ApiQueryOptionsModelBinderProvider()));

        services.AddApiQueryOptions();

        ProviderCount(services.BuildServiceProvider()).Should().Be(1);
    }

    [TestMethod]
    public void AddApiQueryOptions_NullConfigure_ThrowsArgumentNull()
    {
        Action act = () => new ServiceCollection().AddApiQueryOptions((Action<ApiQueryOptionsBuilder>)null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [TestMethod]
    public void GetOwnedParameterNames_SameSettings_ReturnsCachedSet()
    {
        var settings = new ApiQueryOptionsSettings { FilterParameterNames = ["q", "$filter"] };

        IReadOnlySet<string> first = settings.GetOwnedParameterNames("settings");
        IReadOnlySet<string> second = settings.GetOwnedParameterNames("settings");

        first.Should().BeSameAs(second);
        first.Should().Contain("Q").And.Contain("$SKIPTOKEN").And.HaveCount(12);
    }

    [TestMethod]
    [DataRow(nameof(ApiQueryOptionsSettings.SkipTokenParameterNames))]
    [DataRow(nameof(ApiQueryOptionsSettings.ExpandParameterNames))]
    [DataRow(nameof(ApiQueryOptionsSettings.FilterParameterNames))]
    [DataRow(nameof(ApiQueryOptionsSettings.OrderByParameterNames))]
    [DataRow(nameof(ApiQueryOptionsSettings.TopParameterNames))]
    [DataRow(nameof(ApiQueryOptionsSettings.SkipParameterNames))]
    public void Constructor_EmptyParameterNames_ThrowsEveryTime(string listName)
    {
        ApiQueryOptionsSettings settings = listName switch
        {
            nameof(ApiQueryOptionsSettings.SkipTokenParameterNames) => new() { SkipTokenParameterNames = [] },
            nameof(ApiQueryOptionsSettings.ExpandParameterNames) => new() { ExpandParameterNames = [] },
            nameof(ApiQueryOptionsSettings.FilterParameterNames) => new() { FilterParameterNames = [] },
            nameof(ApiQueryOptionsSettings.OrderByParameterNames) => new() { OrderByParameterNames = null! },
            nameof(ApiQueryOptionsSettings.TopParameterNames) => new() { TopParameterNames = [] },
            _ => new() { SkipParameterNames = [] },
        };

        Action act = () => _ = new ApiQueryOptions<object>(null, settings);

        act.Should().Throw<ArgumentException>().WithMessage($"{listName} cannot be null or empty.*").WithParameterName("settings");
        act.Should().Throw<ArgumentException>().WithMessage($"{listName} cannot be null or empty.*");
    }

    [TestMethod]
    public void Constructor_RequestQuery_MatchesKeysCaseInsensitively()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.QueryString = new QueryString("?$TOP=3&$Filter=Id%20eq%201");

        var options = ApiQueryOptions<object>.FromRequest(ctx.Request);

        options.Top!.Value.Should().Be(3);
        options.Filter!.RawValue.Should().Be("Id eq 1");
    }

    [TestMethod]
    public void Constructor_KeyWithNoValues_IsIgnored()
    {
        var query = new QueryCollection(new Dictionary<string, StringValues> { ["$top"] = StringValues.Empty });

        new ApiQueryOptions<object>(query).Top.Should().BeNull();
    }
}
