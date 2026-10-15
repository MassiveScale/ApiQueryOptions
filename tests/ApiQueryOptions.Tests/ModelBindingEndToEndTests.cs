using System.Net;
using System.Text.Json;
using ApiQueryOptions.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ApiQueryOptions.Tests;

/// <summary>
/// Runs requests through the real MVC pipeline so the binder provider, model binder,
/// and <see cref="ApiControllerAttribute"/> behaviour are exercised together.
/// </summary>
[TestClass]
public class ModelBindingEndToEndTests
{
    private static async Task<IHost> StartHostAsync(ApiQueryOptionsSettings settings)
    {
        return await new HostBuilder()
            .ConfigureWebHost(web => web
                .UseTestServer()
                .ConfigureServices(services =>
                {
                    services.AddControllers().AddApplicationPart(typeof(EndToEndPetsController).Assembly);
                    services.AddApiQueryOptions(settings);
                })
                .Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapControllers());
                }))
            .StartAsync();
    }

    [TestMethod]
    public async Task Get_BoundOptions_NextLinkIsAbsoluteUrl()
    {
        using IHost host = await StartHostAsync(new ApiQueryOptionsSettings());
        using HttpClient client = host.GetTestClient();

        HttpResponseMessage response = await client.GetAsync(new Uri("/e2e/pets?$top=2&tenant=a", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("nextLink").GetString()
            .Should().StartWith("http://localhost/e2e/pets?tenant=a&$skiptoken=");
    }

    [TestMethod]
    public async Task Get_FollowingNextLink_WithCustomNames_KeepsFilterAndAdvances()
    {
        var settings = new ApiQueryOptionsSettings
        {
            FilterParameterNames = ["q"],
            SkipTokenParameterNames = ["cursor"],
            TopParameterNames = ["limit"],
        };
        using IHost host = await StartHostAsync(settings);
        using HttpClient client = host.GetTestClient();

        using JsonDocument page1 = JsonDocument.Parse(
            await client.GetStringAsync(new Uri("/e2e/pets?q=Id%20gt%200&limit=2", UriKind.Relative)));
        string nextLink = page1.RootElement.GetProperty("nextLink").GetString()!;
        using JsonDocument page2 = JsonDocument.Parse(await client.GetStringAsync(new Uri(nextLink)));

        page2.RootElement.GetProperty("filter").GetString().Should().Be("Id gt 0");
        page2.RootElement.GetProperty("top").GetInt32().Should().Be(2);
        page2.RootElement.GetProperty("skip").GetInt32().Should().Be(2);
    }

    [TestMethod]
    public async Task Get_MalformedSkipToken_ReturnsBadRequest()
    {
        using IHost host = await StartHostAsync(new ApiQueryOptionsSettings());
        using HttpClient client = host.GetTestClient();

        HttpResponseMessage response = await client.GetAsync(new Uri("/e2e/pets?$skiptoken=not-a-token!", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("$skiptoken");
    }
}

/// <summary>
/// Minimal controller used by <see cref="ModelBindingEndToEndTests"/>.
/// </summary>
[ApiController]
[Route("e2e/pets")]
public sealed class EndToEndPetsController : ControllerBase
{
    /// <summary>
    /// Echoes the bound options and the next-page link for a full page of results.
    /// </summary>
    [HttpGet]
    public IActionResult Get(ApiQueryOptions<EndToEndPet> options)
    {
        int pageSize = options.Top?.Value ?? 0;
        return Ok(new
        {
            filter = options.Filter?.RawValue,
            nextLink = options.NextLink(resultCount: pageSize),
            skip = options.Skip?.Value,
            top = options.Top?.Value,
        });
    }
}

/// <summary>
/// Entity type for <see cref="EndToEndPetsController"/>.
/// </summary>
public sealed class EndToEndPet
{
    /// <summary>
    /// Identifier.
    /// </summary>
    public int Id { get; set; }
}
