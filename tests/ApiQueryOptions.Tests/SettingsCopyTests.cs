using ApiQueryOptions.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace ApiQueryOptions.Tests;

/// <summary>
/// Every code path that copies <see cref="ApiQueryOptionsSettings"/> must carry over every
/// property. These tests compare all public properties, so a new setting that is not copied
/// fails here instead of silently reverting to its default.
/// </summary>
[TestClass]
public class SettingsCopyTests
{
    private static ApiQueryOptionsSettings NonDefaultSettings() => new()
    {
        DefaultPageSize = 7,
        ExpandEnabled = false,
        ExpandParameterNames = ["include"],
        FilterEnabled = false,
        FilterParameterNames = ["q"],
        MaxFilterDepth = 3,
        MaxFilterNodeCount = 9,
        MaxPageSize = 11,
        OrderByEnabled = false,
        OrderByParameterNames = ["sort"],
        SkipEnabled = false,
        SkipParameterNames = ["offset"],
        SkipTokenEnabled = false,
        SkipTokenParameterNames = ["cursor"],
        StringMatching = StringMatching.Database,
        TieBreakerProperty = "Id",
        TopEnabled = false,
        TopParameterNames = ["limit"],
    };

    [TestMethod]
    public void NonDefaultSettings_DiffersFromDefaultsOnEveryProperty()
    {
        ApiQueryOptionsSettings custom = NonDefaultSettings();
        var defaults = new ApiQueryOptionsSettings();

        foreach (System.Reflection.PropertyInfo property in typeof(ApiQueryOptionsSettings).GetProperties())
        {
            property.GetValue(custom).Should().NotBeEquivalentTo(property.GetValue(defaults), because: $"{property.Name} must be exercised by the copy tests");
        }
    }

    [TestMethod]
    public void ApplyTo_EmptyAttribute_CopiesEverySetting()
    {
        ApiQueryOptionsSettings source = NonDefaultSettings();

        ApiQueryOptionsSettings copy = new ApiQueryOptionsAttribute().ApplyTo(source);

        copy.Should().NotBeSameAs(source);
        copy.Should().BeEquivalentTo(source);
    }

    [TestMethod]
    public void ApplyTo_KeepsCustomParameterNames_WhileApplyingOverrides()
    {
        ApiQueryOptionsSettings source = NonDefaultSettings();
        var attribute = new ApiQueryOptionsAttribute
        {
            DefaultPageSize = 5,
            FilterEnabled = QueryOptionState.Enabled,
            MaxPageSize = 50,
            TopEnabled = QueryOptionState.Enabled,
        };

        ApiQueryOptionsSettings copy = attribute.ApplyTo(source);

        copy.DefaultPageSize.Should().Be(5);
        copy.MaxPageSize.Should().Be(50);
        copy.FilterEnabled.Should().BeTrue();
        copy.TopEnabled.Should().BeTrue();
        copy.FilterParameterNames.Should().Equal("q");
        copy.TopParameterNames.Should().Equal("limit");
        copy.SkipTokenParameterNames.Should().Equal("cursor");
        copy.MaxFilterDepth.Should().Be(3);
    }

    [TestMethod]
    public void ApplyTo_NullSettings_ThrowsArgumentNull()
    {
        Action act = () => new ApiQueryOptionsAttribute().ApplyTo(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [TestMethod]
    public void WithPageSizeOverrides_NullOverrides_CopiesEverySetting()
    {
        ApiQueryOptionsSettings source = NonDefaultSettings();

        ApiQueryOptionsSettings copy = source.WithPageSizeOverrides(maxPageSize: null, defaultPageSize: null);

        copy.Should().NotBeSameAs(source);
        copy.Should().BeEquivalentTo(source);
    }

    [TestMethod]
    public void Builder_CopiesEverySetting()
    {
        ApiQueryOptionsSettings expected = NonDefaultSettings();
        var services = new ServiceCollection();

        services.AddApiQueryOptions(o =>
        {
            o.DefaultPageSize = expected.DefaultPageSize;
            o.ExpandEnabled = expected.ExpandEnabled;
            o.ExpandParameterNames = expected.ExpandParameterNames;
            o.FilterEnabled = expected.FilterEnabled;
            o.FilterParameterNames = expected.FilterParameterNames;
            o.MaxFilterDepth = expected.MaxFilterDepth;
            o.MaxFilterNodeCount = expected.MaxFilterNodeCount;
            o.MaxPageSize = expected.MaxPageSize;
            o.OrderByEnabled = expected.OrderByEnabled;
            o.OrderByParameterNames = expected.OrderByParameterNames;
            o.SkipEnabled = expected.SkipEnabled;
            o.SkipParameterNames = expected.SkipParameterNames;
            o.SkipTokenEnabled = expected.SkipTokenEnabled;
            o.SkipTokenParameterNames = expected.SkipTokenParameterNames;
            o.StringMatching = expected.StringMatching;
            o.TieBreakerProperty = expected.TieBreakerProperty;
            o.TopEnabled = expected.TopEnabled;
            o.TopParameterNames = expected.TopParameterNames;
        });

        ApiQueryOptionsSettings built = services.BuildServiceProvider().GetRequiredService<ApiQueryOptionsSettings>();
        built.Should().BeEquivalentTo(expected);
    }

    [TestMethod]
    public void Builder_FilterLimitDefaults_MatchSettingsDefaults()
    {
        var builder = new ApiQueryOptionsBuilder();

        builder.MaxFilterDepth.Should().Be(ApiQueryOptionsSettings.DefaultMaxFilterDepth);
        builder.MaxFilterNodeCount.Should().Be(ApiQueryOptionsSettings.DefaultMaxFilterNodeCount);
    }

    [TestMethod]
    public void Builder_InvalidLimit_ThrowsAtRegistration()
    {
        var services = new ServiceCollection();

        Action act = () => services.AddApiQueryOptions(o => o.MaxFilterDepth = 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
