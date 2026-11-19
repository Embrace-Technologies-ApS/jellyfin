using System;
using AutoFixture.Xunit3;
using Jellyfin.Api.Controllers;
using Jellyfin.Database.Implementations.Entities;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Querying;
using Moq;
using Xunit;

namespace Jellyfin.Api.Tests.Controllers;

public class UserLibraryControllerTests
{
    private readonly UserLibraryController _subject;
    private readonly Mock<IUserManager> _mockUserManager;
    private readonly Mock<ILibraryManager> _mockLibraryManager;

    public UserLibraryControllerTests()
    {
        _mockUserManager = new Mock<IUserManager>();
        _mockLibraryManager = new Mock<ILibraryManager>();

        _subject = new UserLibraryController(
            _mockUserManager.Object,
            new Mock<IUserDataManager>().Object,
            _mockLibraryManager.Object,
            new Mock<IDtoService>().Object,
            new Mock<IUserViewManager>().Object,
            new Mock<IFileSystem>().Object,
            new Mock<IProviderManager>().Object);
    }

    [Theory]
    [AutoData]
    public void GetRecentlyPlayed_ReturnsRecentlyPlayedItems(Guid userId)
    {
        _mockUserManager
            .Setup(m => m.GetUserById(userId))
            .Returns(new User("test", "provider", "reset"));
        _mockLibraryManager
            .Setup(m => m.GetItemsResult(It.IsAny<InternalItemsQuery>()))
            .Returns(new QueryResult<BaseItem>());

        _subject.GetRecentlyPlayed(userId, 0, 10);
    }
}
