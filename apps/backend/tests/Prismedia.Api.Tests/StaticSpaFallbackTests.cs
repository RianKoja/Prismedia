using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Prismedia.Api.Tests;

public sealed class StaticSpaFallbackTests : IDisposable {
    private readonly string _cacheRoot = Path.Combine(Path.GetTempPath(), $"prismedia-cache-{Guid.NewGuid():N}");

    [Fact]
    public async Task ServesGeneratedAssetsWhenCacheDirectoryIsCreatedByStartup() {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseSetting("Prismedia:CacheDir", _cacheRoot))
            .WithTestAuth();
        using var client = factory.CreateAuthenticatedClient();

        using var health = await client.GetAsync("/api/health");
        health.EnsureSuccessStatusCode();

        var thumbnailPath = Path.Combine(_cacheRoot, "videos", "example", "thumb.jpg");
        Directory.CreateDirectory(Path.GetDirectoryName(thumbnailPath)!);
        await File.WriteAllBytesAsync(thumbnailPath, [(byte)0xff, (byte)0xd8, (byte)0xff, (byte)0xd9]);

        using var response = await client.GetAsync("/assets/videos/example/thumb.jpg");
        var bytes = await response.Content.ReadAsByteArrayAsync();

        response.EnsureSuccessStatusCode();
        Assert.Equal("image/jpeg", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal([(byte)0xff, (byte)0xd8, (byte)0xff, (byte)0xd9], bytes);
    }

    public void Dispose() {
        if (Directory.Exists(_cacheRoot)) {
            Directory.Delete(_cacheRoot, recursive: true);
        }
    }
}
