using System.Collections.Generic;
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
        /// Checks that RFC 1918 private IPv4 addresses are returned by the network address filter.
        /// </summary>
        /// <param name="value">The interface IPv4 address.</param>
        [Theory]
        [InlineData("10.0.0.5")]
        [InlineData("172.16.0.1")]
        [InlineData("172.31.255.254")]
        [InlineData("192.168.1.20")]
        public void FilterNetworkAddresses_Rfc1918Ipv4_Included(string value)
        {
            var result = NetworkManager.FilterNetworkAddresses(new[] { CreateInterface(value, "eth0") });

            Assert.Single(result);
            Assert.Equal(value, result[0].Address.ToString());
        }

        /// <summary>
        /// Checks that public IPv4 addresses, including ones next to the RFC 1918 ranges, are excluded by the network address filter.
        /// </summary>
        /// <param name="value">The interface IPv4 address.</param>
        [Theory]
        [InlineData("8.8.8.8")]
        [InlineData("172.32.0.1")]
        [InlineData("192.169.0.1")]
        [InlineData("100.64.0.1")]
        [InlineData("127.0.0.1")]
        public void FilterNetworkAddresses_PublicIpv4_Excluded(string value)
        {
            var result = NetworkManager.FilterNetworkAddresses(new[] { CreateInterface(value, "eth0") });

            Assert.Empty(result);
        }

        /// <summary>
        /// Checks that global-unicast IPv6 addresses are returned by the network address filter.
        /// </summary>
        /// <param name="value">The interface IPv6 address.</param>
        [Theory]
        [InlineData("2001:db8::5")]
        [InlineData("2a00:1450:4001::200e")]
        public void FilterNetworkAddresses_GlobalIpv6_Included(string value)
        {
            var result = NetworkManager.FilterNetworkAddresses(new[] { CreateInterface(value, "eth0") });

            Assert.Single(result);
            Assert.Equal(value, result[0].Address.ToString());
        }

        /// <summary>
        /// Checks that link-local IPv6 addresses are excluded by the network address filter.
        /// </summary>
        /// <param name="value">The interface IPv6 address.</param>
        [Theory]
        [InlineData("fe80::1")]
        [InlineData("fe80::7add:12ff:febb:c67b")]
        public void FilterNetworkAddresses_LinkLocalIpv6_Excluded(string value)
        {
            var result = NetworkManager.FilterNetworkAddresses(new[] { CreateInterface(value, "eth0") });

            Assert.Empty(result);
        }

        /// <summary>
        /// Checks that the network address filter keeps each address's interface name and drops the excluded addresses.
        /// </summary>
        [Fact]
        public void FilterNetworkAddresses_MixedInterfaces_KeepsInterfaceNames()
        {
            var interfaces = new[]
            {
                CreateInterface("203.0.113.7", "eth1", 1),
                CreateInterface("fe80::1", "eth2", 2),
                CreateInterface("2001:db8::5", "wlan0", 3),
                CreateInterface("192.168.1.20", "eth0", 4)
            };

            var result = NetworkManager.FilterNetworkAddresses(interfaces);

            Assert.Equal(
                new[] { ("2001:db8::5", "wlan0"), ("192.168.1.20", "eth0") },
                result.Select(x => (x.Address.ToString(), x.Name)).ToArray());
        }

        private static IPData CreateInterface(string address, string name, int index = 0)
        {
            return new IPData(IPAddress.Parse(address), null, name) { Index = index };
        }
    }
}
