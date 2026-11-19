using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Jellyfin.Extensions.Json;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Querying;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Jellyfin.Server.Integration.Tests.Controllers;

public sealed class ItemsControllerTests : IClassFixture<JellyfinApplicationFactory>, IAsyncLifetime
{
    private readonly JellyfinApplicationFactory _factory;
    private readonly JsonSerializerOptions _jsonOptions = JsonDefaults.Options;
    private readonly List<(string Name, string Path)> _libraries = [];
    private static string? _accessToken;

    public ItemsControllerTests(JellyfinApplicationFactory factory)
    {
        _factory = factory;
    }

    public ValueTask InitializeAsync()
    {
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        var libraryManager = _factory.Services.GetRequiredService<ILibraryManager>();
        foreach (var (name, path) in _libraries)
        {
            await libraryManager.RemoveVirtualFolder(name, false).ConfigureAwait(false);
            Directory.Delete(path, true);
        }
    }

    [Fact]
    public async Task GetItems_NoApiKeyOrUserId_Success()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.AddAuthHeader(_accessToken ??= await AuthHelper.CompleteStartupAsync(client));

        var response = await client.GetAsync("Items", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("Users/{0}/Items")]
    [InlineData("Users/{0}/Items/Resume")]
    public async Task GetUserItems_NonexistentUserId_NotFound(string format)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.AddAuthHeader(_accessToken ??= await AuthHelper.CompleteStartupAsync(client));

        var response = await client.GetAsync(string.Format(CultureInfo.InvariantCulture, format, Guid.NewGuid()), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("Items?userId={0}")]
    [InlineData("Users/{0}/Items")]
    [InlineData("Users/{0}/Items/Resume")]
    public async Task GetItems_UserId_Ok(string format)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.AddAuthHeader(_accessToken ??= await AuthHelper.CompleteStartupAsync(client));

        var userDto = await AuthHelper.GetUserDtoAsync(client);

        var response = await client.GetAsync(string.Format(CultureInfo.InvariantCulture, format, userDto.Id), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = await response.Content.ReadFromJsonAsync<QueryResult<BaseItemDto>>(_jsonOptions, TestContext.Current.CancellationToken);
        Assert.NotNull(items);
    }

    [Fact]
    public async Task GetItems_TmdbIdMatch_ReturnsOnlyTheItemWithThatTmdbId()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.AddAuthHeader(_accessToken ??= await AuthHelper.CompleteStartupAsync(client));

        var libraryId = await CreateLibraryAsync();
        var tmdbId = Guid.NewGuid().ToString("N");
        var matching = CreateMovie(libraryId, "Matching TMDb id", tmdbId);
        var other = CreateMovie(libraryId, "Other TMDb id", Guid.NewGuid().ToString("N"));

        // Other query criteria are supplied alongside the TMDb id; the match must still be exact.
        var response = await client.GetAsync($"Items?parentId={libraryId:N}&tmdbId={tmdbId}&includeItemTypes=Movie", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<QueryResult<BaseItemDto>>(_jsonOptions, TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal([matching.Id], result.Items.Select(i => i.Id));
        Assert.DoesNotContain(other.Id, result.Items.Select(i => i.Id));
    }

    [Fact]
    public async Task GetItems_TmdbIdWithNoMatch_ReturnsNoItems()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.AddAuthHeader(_accessToken ??= await AuthHelper.CompleteStartupAsync(client));

        var libraryId = await CreateLibraryAsync();
        CreateMovie(libraryId, "Present TMDb id", Guid.NewGuid().ToString("N"));
        var unknownTmdbId = Guid.NewGuid().ToString("N");

        var response = await client.GetAsync($"Items?parentId={libraryId:N}&tmdbId={unknownTmdbId}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<QueryResult<BaseItemDto>>(_jsonOptions, TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalRecordCount);
    }

    [Fact]
    public async Task GetItems_WithoutTmdbId_ReturnsTheItemEvenWhenItsTmdbIdDiffersFromAnotherRequest()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.AddAuthHeader(_accessToken ??= await AuthHelper.CompleteStartupAsync(client));

        var libraryId = await CreateLibraryAsync();
        var movie = CreateMovie(libraryId, "Unfiltered movie", Guid.NewGuid().ToString("N"));

        var response = await client.GetAsync($"Items?parentId={libraryId:N}&recursive=true&includeItemTypes=Movie", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<QueryResult<BaseItemDto>>(_jsonOptions, TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal([movie.Id], result.Items.Select(i => i.Id));
    }

    private async Task<Guid> CreateLibraryAsync()
    {
        var libraryManager = _factory.Services.GetRequiredService<ILibraryManager>();

        var libraryName = "TmdbLookup " + Guid.NewGuid().ToString("N");
        var libraryPath = Path.Combine(Path.GetTempPath(), libraryName);
        Directory.CreateDirectory(libraryPath);
        _libraries.Add((libraryName, libraryPath));

        await libraryManager.AddVirtualFolder(
            libraryName,
            CollectionTypeOptions.movies,
            new LibraryOptions { PathInfos = [new MediaPathInfo(libraryPath)] },
            false).ConfigureAwait(false);

        var libraryInfo = libraryManager.GetVirtualFolders().Single(f => f.Name == libraryName);
        return Guid.Parse(libraryInfo.ItemId);
    }

    private Movie CreateMovie(Guid libraryId, string name, string tmdbId)
    {
        var libraryManager = _factory.Services.GetRequiredService<ILibraryManager>();
        var library = libraryManager.GetItemById(libraryId)!;
        var movie = new Movie
        {
            Id = Guid.NewGuid(),
            Name = name,
            Path = Path.Combine(library.Path, name + ".mkv"),
            ParentId = libraryId
        };
        movie.ProviderIds[MetadataProvider.Tmdb.ToString()] = tmdbId;

        libraryManager.CreateItems([movie], library, TestContext.Current.CancellationToken);

        return movie;
    }
}
