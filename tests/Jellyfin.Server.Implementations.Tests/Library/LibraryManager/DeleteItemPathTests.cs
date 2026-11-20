using System;
using System.IO;
using AutoFixture;
using AutoFixture.AutoMoq;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.IO;
using Moq;
using Xunit;

namespace Jellyfin.Server.Implementations.Tests.Library.LibraryManager;

[Collection("LibraryManagerTests")]
public sealed class DeleteItemPathTests : IDisposable
{
    private readonly string _workRoot;
    private readonly string _peopleRoot;
    private readonly IFileSystem _previousFileSystem;
    private readonly IServerConfigurationManager _previousConfigurationManager;
    private readonly Emby.Server.Implementations.Library.LibraryManager _libraryManager;

    public DeleteItemPathTests()
    {
        _workRoot = Path.Combine(Path.GetTempPath(), "jellyfin-delete-tests-" + Guid.NewGuid().ToString("N"));
        _peopleRoot = Path.Combine(_workRoot, "People");
        Directory.CreateDirectory(_peopleRoot);

        _previousFileSystem = BaseItem.FileSystem;
        _previousConfigurationManager = BaseItem.ConfigurationManager;

        // Item paths are resolved through the static file system, so resolve them against the real disk.
        var fileSystemMock = new Mock<IFileSystem>();
        fileSystemMock.Setup(f => f.GetFileSystemInfo(It.IsAny<string>()))
            .Returns((string path) => new FileSystemMetadata
            {
                FullName = path,
                IsDirectory = Directory.Exists(path),
                Exists = Directory.Exists(path) || File.Exists(path)
            });
        BaseItem.FileSystem = fileSystemMock.Object;

        var fixture = new Fixture().Customize(new AutoMoqCustomization());
        var configurationManagerMock = fixture.Freeze<Mock<IServerConfigurationManager>>();
        configurationManagerMock.Setup(c => c.ApplicationPaths.PeoplePath).Returns(_peopleRoot);
        configurationManagerMock.Setup(c => c.ApplicationPaths.ProgramDataPath).Returns(_workRoot);
        configurationManagerMock.Setup(c => c.ApplicationPaths.InternalMetadataPath).Returns(Path.Combine(_workRoot, "metadata"));
        BaseItem.ConfigurationManager = configurationManagerMock.Object;

        _libraryManager = fixture.Create<Emby.Server.Implementations.Library.LibraryManager>();
    }

    [Fact]
    public void DeleteItemsUnsafeFast_PeopleRootPath_RootAndContentsRemain()
    {
        var joeImage = CreateFile(Path.Combine("J", "Joe Smith"), "folder.jpg");
        var person = new Person { Id = Guid.NewGuid(), Name = "???", Path = _peopleRoot };

        _libraryManager.DeleteItemsUnsafeFast([person], deleteSourceFiles: true);

        Assert.True(Directory.Exists(_peopleRoot));
        Assert.True(File.Exists(joeImage));
    }

    [Fact]
    public void DeleteItemsUnsafeFast_PeopleRootPathWithTrailingSeparator_RootAndContentsRemain()
    {
        var joeImage = CreateFile(Path.Combine("J", "Joe Smith"), "folder.jpg");
        var person = new Person { Id = Guid.NewGuid(), Name = "???", Path = _peopleRoot + Path.DirectorySeparatorChar };

        _libraryManager.DeleteItemsUnsafeFast([person], deleteSourceFiles: true);

        Assert.True(Directory.Exists(_peopleRoot));
        Assert.True(File.Exists(joeImage));
    }

    [Fact]
    public void DeleteItemsUnsafeFast_NormalPersonDirectory_RemovesOnlyThatDirectory()
    {
        var joeDirectory = Path.Combine(_peopleRoot, "J", "Joe Smith");
        CreateFile(Path.Combine("J", "Joe Smith"), "folder.jpg");
        var bobImage = CreateFile(Path.Combine("B", "Bob Kane"), "folder.jpg");
        var person = new Person { Id = Guid.NewGuid(), Name = "Joe Smith", Path = joeDirectory };

        _libraryManager.DeleteItemsUnsafeFast([person], deleteSourceFiles: true);

        Assert.False(Directory.Exists(joeDirectory));
        Assert.True(Directory.Exists(_peopleRoot));
        Assert.True(File.Exists(bobImage));
    }

    [Fact]
    public void DeleteItemsUnsafeFast_MalformedPersonChildDirectory_RemovesOnlyThatDirectory()
    {
        var malformedDirectory = Path.Combine(_peopleRoot, "3", "3F2A9C");
        CreateFile(Path.Combine("3", "3F2A9C"), "folder.jpg");
        var joeImage = CreateFile(Path.Combine("J", "Joe Smith"), "folder.jpg");
        var person = new Person { Id = Guid.NewGuid(), Name = "???", Path = malformedDirectory };

        _libraryManager.DeleteItemsUnsafeFast([person], deleteSourceFiles: true);

        Assert.False(Directory.Exists(malformedDirectory));
        Assert.True(Directory.Exists(_peopleRoot));
        Assert.True(File.Exists(joeImage));
    }

    public void Dispose()
    {
        BaseItem.FileSystem = _previousFileSystem;
        BaseItem.ConfigurationManager = _previousConfigurationManager;

        if (Directory.Exists(_workRoot))
        {
            Directory.Delete(_workRoot, true);
        }
    }

    private string CreateFile(string relativeDirectory, string fileName)
    {
        var directory = Path.Combine(_peopleRoot, relativeDirectory);
        Directory.CreateDirectory(directory);
        var file = Path.Combine(directory, fileName);
        File.WriteAllText(file, "image");
        return file;
    }
}
