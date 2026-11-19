using System.Linq;
using System.Net;
using Jellyfin.Networking.Manager;
using MediaBrowser.Common.Net;
using MediaBrowser.Model.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Jellyfin.Networking.Tests
{
    public class NetworkManagerTests
    {
        /// <summary>
        /// Checks that the given IP address is in the specified network(s).
        /// </summary>
        /// <param name="network">Network address(es).</param>
        /// <param name="value">The IP to check.</param>
        [Theory]
        [InlineData("192.168.2.1/24", "192.168.2.123")]
        [InlineData("192.168.2.1/24, !192.168.2.122/32", "192.168.2.123")]
        [InlineData("fd23:184f:2029:0::/56", "fd23:184f:2029:0:3139:7386:67d7:d517")]
        [InlineData("fd23:184f:2029:0::/56, !fd23:184f:2029:0:3139:7386:67d7:d518/128", "fd23:184f:2029:0:3139:7386:67d7:d517")]
        public void InNetwork_True_Success(string network, string value)
        {
            var ip = IPAddress.Parse(value);
            var conf = new NetworkConfiguration()
            {
                EnableIPv6 = true,
                EnableIPv4 = true,
                LocalNetworkSubnets = network.Split(',')
            };

            var startupConf = new Mock<IConfiguration>();
            using var networkManager = new NetworkManager(NetworkParseTests.GetMockConfig(conf), startupConf.Object, new NullLogger<NetworkManager>());

            Assert.True(networkManager.IsInLocalNetwork(ip));
        }

        /// <summary>
        /// Checks that the given IP address is not in the network provided.
        /// </summary>
        /// <param name="network">Network address(es).</param>
        /// <param name="value">The IP to check.</param>
        [Theory]
        [InlineData("192.168.10.0/24", "192.168.11.1")]
        [InlineData("192.168.10.0/24, !192.168.10.60/32", "192.168.10.60")]
        [InlineData("192.168.10.0/24", "fd23:184f:2029:0:3139:7386:67d7:d517")]
        [InlineData("fd23:184f:2029:0::/56", "fd24:184f:2029:0:3139:7386:67d7:d517")]
        [InlineData("fd23:184f:2029:0::/56, !fd23:184f:2029:0:3139:7386:67d7:d500/120", "fd23:184f:2029:0:3139:7386:67d7:d517")]
        [InlineData("fd23:184f:2029:0::/56", "192.168.10.60")]
        [InlineData("2001:abcd:abcd:6b40::0/60", "192.168.10.60")]
        public void InNetwork_False_Success(string network, string value)
        {
            var ip = IPAddress.Parse(value);
            var conf = new NetworkConfiguration()
            {
                EnableIPv6 = true,
                EnableIPv4 = true,
                LocalNetworkSubnets = network.Split(',')
            };

            var startupConf = new Mock<IConfiguration>();
            using var networkManager = new NetworkManager(NetworkParseTests.GetMockConfig(conf), startupConf.Object, new NullLogger<NetworkManager>());

            Assert.False(networkManager.IsInLocalNetwork(ip));
        }

        /// <summary>
        /// Checks that private IPv4 (RFC 1918) addresses are returned.
        /// </summary>
        /// <param name="address">The interface address.</param>
        [Theory]
        [InlineData("10.0.0.5")]
        [InlineData("172.16.0.1")]
        [InlineData("172.31.255.254")]
        [InlineData("192.168.1.20")]
        public void FilterPrivateAndGlobalAddresses_PrivateIPv4_Included(string address)
        {
            var result = NetworkManager.FilterPrivateAndGlobalAddresses(new[] { CreateInterface(address, "eth0") });

            Assert.Single(result);
            Assert.Equal(address, result[0].Address.ToString());
        }

        /// <summary>
        /// Checks that public IPv4 addresses and IPv4 addresses just outside RFC 1918 are not returned.
        /// </summary>
        /// <param name="address">The interface address.</param>
        [Theory]
        [InlineData("8.8.8.8")]
        [InlineData("172.15.255.255")]
        [InlineData("172.32.0.1")]
        [InlineData("169.254.10.10")]
        [InlineData("127.0.0.1")]
        public void FilterPrivateAndGlobalAddresses_PublicOrNonRfc1918IPv4_Excluded(string address)
        {
            var result = NetworkManager.FilterPrivateAndGlobalAddresses(new[] { CreateInterface(address, "eth0") });

            Assert.Empty(result);
        }

        /// <summary>
        /// Checks that global-unicast IPv6 addresses are returned.
        /// </summary>
        /// <param name="address">The interface address.</param>
        [Theory]
        [InlineData("2001:db8::1")]
        [InlineData("2a00:1450:4001::200e")]
        public void FilterPrivateAndGlobalAddresses_GlobalIPv6_Included(string address)
        {
            var result = NetworkManager.FilterPrivateAndGlobalAddresses(new[] { CreateInterface(address, "eth0") });

            Assert.Single(result);
            Assert.Equal(address, result[0].Address.ToString());
        }

        /// <summary>
        /// Checks that link-local, unique-local, loopback and multicast IPv6 addresses are not returned.
        /// </summary>
        /// <param name="address">The interface address.</param>
        [Theory]
        [InlineData("fe80::1")]
        [InlineData("febf::1")]
        [InlineData("fd12:3456:789a::1")]
        [InlineData("::1")]
        [InlineData("ff02::1")]
        public void FilterPrivateAndGlobalAddresses_NonGlobalIPv6_Excluded(string address)
        {
            var result = NetworkManager.FilterPrivateAndGlobalAddresses(new[] { CreateInterface(address, "eth0") });

            Assert.Empty(result);
        }

        /// <summary>
        /// Checks that each returned address keeps the name of the interface it was found on.
        /// </summary>
        [Fact]
        public void FilterPrivateAndGlobalAddresses_MixedInterfaces_KeepsInterfaceNames()
        {
            var interfaces = new[]
            {
                CreateInterface("192.168.0.10", "eth0"),
                CreateInterface("203.0.113.7", "eth0"),
                CreateInterface("fe80::1234", "eth0"),
                CreateInterface("2001:db8:1::5", "wlan0"),
                CreateInterface("10.20.30.40", "wlan0")
            };

            var result = NetworkManager.FilterPrivateAndGlobalAddresses(interfaces);

            Assert.Equal(
                new[] { "192.168.0.10 eth0", "2001:db8:1::5 wlan0", "10.20.30.40 wlan0" },
                result.Select(i => i.Address + " " + i.Name).ToArray());
        }

        private static IPData CreateInterface(string address, string name)
        {
            return new IPData(IPAddress.Parse(address), null, name);
        }
    }
}
