using System;
using System.IO;
using AutoFixture;
using AutoFixture.AutoMoq;
using Emby.Naming.Common;
using Jellyfin.Database.Implementations.Enums;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.MediaInfo;
using Moq;
using Xunit;
using BaseItem = MediaBrowser.Controller.Entities.BaseItem;
using Person = MediaBrowser.Controller.Entities.Person;

namespace Jellyfin.Server.Implementations.Tests.Library;

public sealed class LibraryManagerDeleteTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "jellyfin-delete-tests-" + Guid.NewGuid().ToString("N"));
    private readonly string _peoplePath;
    private readonly IFileSystem _previousFileSystem = BaseItem.FileSystem;
    private readonly IMediaSourceManager _previousMediaSourceManager = BaseItem.MediaSourceManager;
    private readonly IServerConfigurationManager _previousConfigurationManager = BaseItem.ConfigurationManager;

    public LibraryManagerDeleteTests()
    {
        _peoplePath = Path.Combine(_root, "metadata", "people");
        Directory.CreateDirectory(_peoplePath);
    }

    public void Dispose()
    {
        BaseItem.FileSystem = _previousFileSystem;
        BaseItem.MediaSourceManager = _previousMediaSourceManager;
        BaseItem.ConfigurationManager = _previousConfigurationManager;
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    [Fact]
    public void DeleteItem_PersonStoredAtPeopleRoot_KeepsRootAndOtherPeople()
    {
        var otherPersonFile = CreateFile(Path.Combine(_peoplePath, "o", "Other Person", "folder.jpg"));
        var person = new Person { Id = Guid.NewGuid(), Name = "???", Path = _peoplePath };
        var libraryManager = CreateLibraryManager();

        libraryManager.DeleteItem(person, new DeleteOptions { DeleteFileLocation = true });

        Assert.True(Directory.Exists(_peoplePath));
        Assert.True(File.Exists(otherPersonFile));
    }

    [Fact]
    public void DeleteItem_PersonBelowPeopleRoot_DeletesOnlyThatPersonDirectory()
    {
        var personDirectory = Path.Combine(_peoplePath, "a", "Alice");
        var personFile = CreateFile(Path.Combine(personDirectory, "folder.jpg"));
        var otherPersonFile = CreateFile(Path.Combine(_peoplePath, "b", "Bob", "folder.jpg"));
        var person = new Person { Id = Guid.NewGuid(), Name = "Alice", Path = personDirectory };
        var libraryManager = CreateLibraryManager();

        libraryManager.DeleteItem(person, new DeleteOptions { DeleteFileLocation = true });

        Assert.False(Directory.Exists(personDirectory));
        Assert.False(File.Exists(personFile));
        Assert.True(Directory.Exists(_peoplePath));
        Assert.True(File.Exists(otherPersonFile));
    }

    private static string CreateFile(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "test");
        return path;
    }

    private Emby.Server.Implementations.Library.LibraryManager CreateLibraryManager()
    {
        var fixture = new Fixture().Customize(new AutoMoqCustomization());
        fixture.Register(() => new NamingOptions());
        var configMock = fixture.Freeze<Mock<IServerConfigurationManager>>();
        configMock.Setup(c => c.ApplicationPaths.ProgramDataPath).Returns(_root);
        configMock.Setup(c => c.ApplicationPaths.PeoplePath).Returns(_peoplePath);
        configMock.Setup(c => c.ApplicationPaths.InternalMetadataPath).Returns(Path.Combine(_root, "metadata", "internal"));
        BaseItem.ConfigurationManager = configMock.Object;

        var fileSystemMock = new Mock<IFileSystem>();
        fileSystemMock.Setup(f => f.GetFileSystemInfo(It.IsAny<string>()))
            .Returns((string path) => new FileSystemMetadata
            {
                FullName = path,
                IsDirectory = Directory.Exists(path)
            });
        BaseItem.FileSystem = fileSystemMock.Object;

        var mediaSourceManager = new Mock<IMediaSourceManager>();
        mediaSourceManager.Setup(m => m.GetPathProtocol(It.IsAny<string>())).Returns(MediaProtocol.File);
        BaseItem.MediaSourceManager = mediaSourceManager.Object;

        fixture.Freeze<Mock<IItemPersistenceService>>();
        return fixture.Build<Emby.Server.Implementations.Library.LibraryManager>().Create();
    }
}
