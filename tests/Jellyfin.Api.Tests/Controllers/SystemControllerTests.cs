using System.Net;
using Jellyfin.Api.Controllers;
using Jellyfin.Server.Implementations.SystemBackupService;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Jellyfin.Api.Tests.Controllers
{
    public class SystemControllerTests
    {
        [Fact]
        public void GetNetworkAddresses_MixedAddresses_SplitsIntoIpv4AndIpv6Lists()
        {
            var networkManager = new Mock<INetworkManager>();
            networkManager
                .Setup(nm => nm.GetNetworkAddresses())
                .Returns(
                [
                    new IPData(IPAddress.Parse("192.168.1.20"), null, "eth0") { Index = 1 },
                    new IPData(IPAddress.Parse("2001:db8::5"), null, "wlan0") { Index = 2 }
                ]);

            var controller = new SystemController(
                Mock.Of<ILogger<SystemController>>(),
                Mock.Of<IServerApplicationHost>(),
                Mock.Of<IServerApplicationPaths>(),
                Mock.Of<IFileSystem>(),
                networkManager.Object,
                Mock.Of<ISystemManager>());

            var result = controller.GetNetworkAddresses().Value;

            Assert.NotNull(result);
            var lanIpv4 = Assert.Single(result.LanIpv4Addresses);
            Assert.Equal("192.168.1.20", lanIpv4.Address);
            Assert.Equal("eth0", lanIpv4.InterfaceName);
            var globalIpv6 = Assert.Single(result.GlobalIpv6Addresses);
            Assert.Equal("2001:db8::5", globalIpv6.Address);
            Assert.Equal("wlan0", globalIpv6.InterfaceName);
        }

        [Fact]
        public void GetLogFile_FileDoesNotExist_ReturnsNotFound()
        {
            var mockFileSystem = new Mock<IFileSystem>();
            mockFileSystem
                .Setup(fs => fs.GetFiles(It.IsAny<string>(), It.IsAny<bool>()))
                .Returns([new() { Name = "file1.txt" }, new() { Name = "file2.txt" }]);

            var controller = new SystemController(
                Mock.Of<ILogger<SystemController>>(),
                Mock.Of<IServerApplicationHost>(),
                Mock.Of<IServerApplicationPaths>(),
                mockFileSystem.Object,
                Mock.Of<INetworkManager>(),
                Mock.Of<ISystemManager>());

            var result = controller.GetLogFile("DOES_NOT_EXIST.txt");

            Assert.IsType<NotFoundObjectResult>(result);
        }
    }
}
