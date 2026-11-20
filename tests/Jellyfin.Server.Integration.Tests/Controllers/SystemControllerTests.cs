using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Jellyfin.Api.Models.SystemInfoDtos;
using Jellyfin.Api.Models.UserDtos;
using Jellyfin.Extensions.Json;
using MediaBrowser.Model.System;
using Xunit;

namespace Jellyfin.Server.Integration.Tests.Controllers
{
    public sealed class SystemControllerTests : IClassFixture<SystemControllerTests.ServerFixture>
    {
        private const string NonAdminUsername = "networkAddressesUser";

        private readonly JellyfinApplicationFactory _factory;
        private readonly string _adminAccessToken;

        public SystemControllerTests(ServerFixture fixture)
        {
            _factory = fixture.Factory;
            _adminAccessToken = fixture.AdminAccessToken;
        }

        [Fact]
        public async Task GetNetworkAddresses_Administrator_ReturnsOk()
        {
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.AddAuthHeader(_adminAccessToken);

            using var response = await client.GetAsync("System/NetworkAddresses", TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var addresses = await response.Content.ReadFromJsonAsync<NetworkAddressesDto>(JsonDefaults.Options, TestContext.Current.CancellationToken);
            Assert.NotNull(addresses);
            Assert.NotNull(addresses.LanIpv4Addresses);
            Assert.NotNull(addresses.GlobalIpv6Addresses);
            foreach (var entry in addresses.LanIpv4Addresses)
            {
                Assert.False(string.IsNullOrEmpty(entry.Address));
                Assert.False(string.IsNullOrEmpty(entry.InterfaceName));
                Assert.True(IsRfc1918Ipv4(IPAddress.Parse(entry.Address)), $"{entry.Address} is not an RFC 1918 IPv4 address.");
            }

            foreach (var entry in addresses.GlobalIpv6Addresses)
            {
                Assert.False(string.IsNullOrEmpty(entry.Address));
                Assert.False(string.IsNullOrEmpty(entry.InterfaceName));
                Assert.True(IsGlobalUnicastIpv6(IPAddress.Parse(entry.Address)), $"{entry.Address} is not a global-unicast IPv6 address.");
            }
        }

        [Fact]
        public async Task GetNetworkAddresses_NonAdministrator_ReturnsForbidden()
        {
            var adminClient = _factory.CreateClient();
            adminClient.DefaultRequestHeaders.AddAuthHeader(_adminAccessToken);

            using var createResponse = await adminClient.PostAsJsonAsync(
                "Users/New",
                new CreateUserByName { Name = NonAdminUsername },
                JsonDefaults.Options,
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

            var userClient = _factory.CreateClient();
            using var authRequest = new HttpRequestMessage(HttpMethod.Post, "Users/AuthenticateByName")
            {
                Content = JsonContent.Create(
                    new AuthenticateUserByName { Username = NonAdminUsername, Pw = string.Empty },
                    options: JsonDefaults.Options)
            };
            authRequest.Headers.TryAddWithoutValidation(AuthHelper.AuthHeaderName, AuthHelper.DummyAuthHeader);
            using var authResponse = await userClient.SendAsync(authRequest, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, authResponse.StatusCode);
            var auth = await authResponse.Content.ReadFromJsonAsync<AuthenticationResult>(JsonDefaults.Options, TestContext.Current.CancellationToken);
            Assert.NotNull(auth);

            using var request = new HttpRequestMessage(HttpMethod.Get, "System/NetworkAddresses");
            request.Headers.TryAddWithoutValidation(AuthHelper.AuthHeaderName, AuthHelper.DummyAuthHeader + ", Token=" + auth.AccessToken);
            using var response = await userClient.SendAsync(request, TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task GetPublicSystemInfo_Anonymous_ReturnsOkWithLocalAddress()
        {
            var client = _factory.CreateClient();

            using var response = await client.GetAsync("System/Info/Public", TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var info = await response.Content.ReadFromJsonAsync<PublicSystemInfo>(JsonDefaults.Options, TestContext.Current.CancellationToken);
            Assert.NotNull(info);
            Assert.False(string.IsNullOrEmpty(info.LocalAddress));
        }

        private static bool IsRfc1918Ipv4(IPAddress address)
        {
            var bytes = address.GetAddressBytes();
            return bytes[0] == 10
                || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                || (bytes[0] == 192 && bytes[1] == 168);
        }

        private static bool IsGlobalUnicastIpv6(IPAddress address)
        {
            // 2000::/3
            return (address.GetAddressBytes()[0] & 0xE0) == 0x20;
        }

        private sealed class AuthenticationResult
        {
            public string AccessToken { get; set; } = string.Empty;
        }

        /// <summary>
        /// Starts one server for the test class and completes its startup once. Tests only read the token afterwards.
        /// </summary>
        public sealed class ServerFixture : IAsyncLifetime
        {
            public JellyfinApplicationFactory Factory { get; } = new JellyfinApplicationFactory();

            public string AdminAccessToken { get; private set; } = string.Empty;

            public async ValueTask InitializeAsync()
            {
                using var client = Factory.CreateClient();
                AdminAccessToken = await AuthHelper.CompleteStartupAsync(client);
            }

            public ValueTask DisposeAsync()
            {
                Factory.Dispose();
                return ValueTask.CompletedTask;
            }
        }
    }
}
