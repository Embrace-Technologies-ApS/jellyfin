using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Mime;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Jellyfin.Api.Models.SystemInfoDtos;
using Jellyfin.Api.Models.UserDtos;
using Jellyfin.Extensions.Json;
using Xunit;

namespace Jellyfin.Server.Integration.Tests.Controllers;

public sealed class SystemControllerTests : IClassFixture<JellyfinApplicationFactory>
{
    private const string NonAdminUsername = "networkAddressesNonAdmin";

    private readonly JellyfinApplicationFactory _factory;
    private static string? _accessToken;

    public SystemControllerTests(JellyfinApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetNetworkAddresses_Unauthenticated_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        using var response = await client.GetAsync("System/NetworkAddresses", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetNetworkAddresses_NonAdministrator_ReturnsForbidden()
    {
        var adminClient = _factory.CreateClient();
        var adminToken = _accessToken ??= await AuthHelper.CompleteStartupAsync(adminClient);
        adminClient.DefaultRequestHeaders.AddAuthHeader(adminToken);

        using var createResponse = await adminClient.PostAsJsonAsync(
            "Users/New",
            new CreateUserByName { Name = NonAdminUsername },
            JsonDefaults.Options,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        var userToken = await AuthenticateWithoutPasswordAsync(NonAdminUsername);
        var userClient = _factory.CreateClient();
        userClient.DefaultRequestHeaders.AddAuthHeader(userToken);

        using var response = await userClient.GetAsync("System/NetworkAddresses", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetNetworkAddresses_Administrator_ReturnsAddressesWithInterfaceNames()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.AddAuthHeader(_accessToken ??= await AuthHelper.CompleteStartupAsync(client));

        using var response = await client.GetAsync("System/NetworkAddresses", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(MediaTypeNames.Application.Json, response.Content.Headers.ContentType?.MediaType);
        var addresses = await response.Content.ReadFromJsonAsync<NetworkAddressDto[]>(JsonDefaults.Options, TestContext.Current.CancellationToken);
        Assert.NotNull(addresses);
        Assert.All(addresses, address => Assert.False(string.IsNullOrEmpty(address.InterfaceName)));
    }

    private async Task<string> AuthenticateWithoutPasswordAsync(string username)
    {
        var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/Users/AuthenticateByName");
        request.Headers.TryAddWithoutValidation(AuthHelper.AuthHeaderName, AuthHelper.DummyAuthHeader);
        request.Content = JsonContent.Create(
            new AuthenticateUserByName { Username = username, Pw = string.Empty },
            options: JsonDefaults.Options);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<AuthenticationResult>(JsonDefaults.Options, TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        return result.AccessToken;
    }

    private sealed class AuthenticationResult
    {
        [JsonPropertyName("AccessToken")]
        public string AccessToken { get; set; } = string.Empty;
    }
}
