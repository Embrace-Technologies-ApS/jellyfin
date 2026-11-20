using System;
using System.IO;
using System.Linq;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.IO;
using Moq;
using Xunit;

namespace Jellyfin.Controller.Tests.Entities;

[Collection("LibraryManagerTests")]
public class PersonPathTests
{
    private const string PeopleRoot = "/metadata/library/People";

    [Theory]
    [InlineData("???")]
    [InlineData("*")]
    [InlineData(" ? : * ")]
    [InlineData("...")]
    public void GetPath_NameWithOnlyInvalidCharacters_ResolvesBelowPeopleRoot(string name)
    {
        var previousConfiguration = BaseItem.ConfigurationManager;
        var previousFileSystem = BaseItem.FileSystem;
        try
        {
            SetupPaths();

            var path = Person.GetPath(name);

            Assert.NotEqual(PeopleRoot, path);
            Assert.StartsWith(PeopleRoot + Path.DirectorySeparatorChar, path, StringComparison.Ordinal);
            Assert.False(string.IsNullOrEmpty(Path.GetFileName(path)));
        }
        finally
        {
            BaseItem.ConfigurationManager = previousConfiguration;
            BaseItem.FileSystem = previousFileSystem;
        }
    }

    [Fact]
    public void GetPath_DifferentNamesWithOnlyInvalidCharacters_ResolveToDifferentPaths()
    {
        var previousConfiguration = BaseItem.ConfigurationManager;
        var previousFileSystem = BaseItem.FileSystem;
        try
        {
            SetupPaths();

            Assert.NotEqual(Person.GetPath("???"), Person.GetPath("*:*"));
        }
        finally
        {
            BaseItem.ConfigurationManager = previousConfiguration;
            BaseItem.FileSystem = previousFileSystem;
        }
    }

    [Fact]
    public void GetPath_NameWithOnlyInvalidCharacters_IsStableWhenRebasedFromItsFileName()
    {
        var previousConfiguration = BaseItem.ConfigurationManager;
        var previousFileSystem = BaseItem.FileSystem;
        try
        {
            SetupPaths();

            var path = Person.GetPath("???");

            Assert.Equal(path, Person.GetPath(Path.GetFileName(path), false));
        }
        finally
        {
            BaseItem.ConfigurationManager = previousConfiguration;
            BaseItem.FileSystem = previousFileSystem;
        }
    }

    [Fact]
    public void GetPath_ValidName_UsesAlphabeticSubdirectory()
    {
        var previousConfiguration = BaseItem.ConfigurationManager;
        var previousFileSystem = BaseItem.FileSystem;
        try
        {
            SetupPaths();

            Assert.Equal(Path.Combine(PeopleRoot, "J", "Joe Smith"), Person.GetPath("Joe Smith"));
        }
        finally
        {
            BaseItem.ConfigurationManager = previousConfiguration;
            BaseItem.FileSystem = previousFileSystem;
        }
    }

    private static void SetupPaths()
    {
        var applicationPaths = new Mock<IServerApplicationPaths>();
        applicationPaths.SetupGet(p => p.PeoplePath).Returns(PeopleRoot);

        var configurationManager = new Mock<IServerConfigurationManager>();
        configurationManager.SetupGet(c => c.ApplicationPaths).Returns(applicationPaths.Object);
        BaseItem.ConfigurationManager = configurationManager.Object;

        // Mirrors ManagedFileSystem.GetValidFilename: invalid filename characters become spaces.
        var invalid = Path.GetInvalidFileNameChars().Concat(new[] { '?', '*', ':', '"', '<', '>', '|' }).ToArray();
        var fileSystem = new Mock<IFileSystem>();
        fileSystem.Setup(x => x.GetValidFilename(It.IsAny<string>()))
            .Returns((string name) => new string(name.Select(c => Array.IndexOf(invalid, c) >= 0 ? ' ' : c).ToArray()));
        BaseItem.FileSystem = fileSystem.Object;
    }
}
