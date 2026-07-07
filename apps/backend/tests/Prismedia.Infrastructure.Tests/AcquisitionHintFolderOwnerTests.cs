using Microsoft.EntityFrameworkCore;
using Prismedia.Domain.Entities;
using Prismedia.Infrastructure.Acquisition;
using Prismedia.Infrastructure.Persistence;
using Prismedia.Infrastructure.Persistence.Entities;

namespace Prismedia.Infrastructure.Tests;

/// <summary>
/// Covers the post-scan hint pass for video/audio imports: external ids stamp onto the entity owning
/// the imported path, the hint is consumed, and the owner's TOP-LEVEL ancestor is reported for the
/// identify kick. Book hints and not-yet-owned paths are left alone.
/// </summary>
public sealed class AcquisitionHintFolderOwnerTests {
    [Fact]
    public async Task StampsIdsOnTheSeasonOwnerAndReportsTheSeries() {
        await using var db = CreateContext();
        var seriesId = AddEntity(db, EntityKindRegistry.VideoSeries.Code, null, "/media/tv/Show (2008)", title: "Show");
        var seasonId = AddEntity(db, EntityKindRegistry.VideoSeason.Code, seriesId, "/media/tv/Show (2008)/S01");
        AddHint(db, "/media/tv/Show (2008)/S01", """{"tmdb":"4242"}""");
        await db.SaveChangesAsync();

        var owners = await new AcquisitionHintApplier(db).ApplyToFolderOwnersAsync(CancellationToken.None);

        var owner = Assert.Single(owners);
        Assert.Equal(seriesId, owner.TopLevelEntityId);
        Assert.Equal(EntityKindRegistry.VideoSeries.Code, owner.TopLevelKindCode);
        Assert.Equal("Show", owner.TopLevelTitle);
        var stamped = Assert.Single(await db.EntityExternalIds.AsNoTracking().Where(row => row.EntityId == seasonId).ToArrayAsync());
        Assert.Equal("tmdb", stamped.Provider);
        Assert.Equal("4242", stamped.Value);
        Assert.True(Assert.Single(await db.AcquisitionImportHints.AsNoTracking().ToArrayAsync()).Consumed);
    }

    [Fact]
    public async Task FileHintsResolveThroughTheParentFolderWalk() {
        await using var db = CreateContext();
        var movieId = AddEntity(db, EntityKindRegistry.Movie.Code, null, "/media/movies/Film (2020)", title: "Film");
        AddHint(db, "/media/movies/Film (2020)/Film.mkv", """{"tmdb":"77"}""");
        await db.SaveChangesAsync();

        var owners = await new AcquisitionHintApplier(db).ApplyToFolderOwnersAsync(CancellationToken.None);

        Assert.Equal(movieId, Assert.Single(owners).TopLevelEntityId);
        Assert.Single(await db.EntityExternalIds.AsNoTracking().Where(row => row.EntityId == movieId).ToArrayAsync());
    }

    [Fact]
    public async Task BookOwnersAndUnownedPathsAreLeftForTheirOwnPasses() {
        await using var db = CreateContext();
        AddEntity(db, EntityKindRegistry.Book.Code, null, "/media/books/Novel.epub");
        AddHint(db, "/media/books/Novel.epub", """{"openlibrary":"OL1"}""");
        AddHint(db, "/media/tv/Not Scanned Yet/S01", """{"tmdb":"1"}""");
        await db.SaveChangesAsync();

        var owners = await new AcquisitionHintApplier(db).ApplyToFolderOwnersAsync(CancellationToken.None);

        Assert.Empty(owners);
        Assert.All(await db.AcquisitionImportHints.AsNoTracking().ToArrayAsync(), hint => Assert.False(hint.Consumed));
        Assert.Empty(await db.EntityExternalIds.AsNoTracking().ToArrayAsync());
    }

    private static Guid AddEntity(PrismediaDbContext db, string kindCode, Guid? parent, string sourcePath, string? title = null) {
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        db.Entities.Add(new EntityRow {
            Id = id, KindCode = kindCode, Title = title ?? kindCode, ParentEntityId = parent, CreatedAt = now, UpdatedAt = now
        });
        db.EntityFiles.Add(new EntityFileRow {
            Id = Guid.NewGuid(), EntityId = id, Role = EntityFileRole.Source, Path = sourcePath, CreatedAt = now, UpdatedAt = now
        });
        return id;
    }

    private static void AddHint(PrismediaDbContext db, string sourcePath, string externalIdsJson) {
        var now = DateTimeOffset.UtcNow;
        var acquisitionId = Guid.NewGuid();
        db.Acquisitions.Add(new AcquisitionRow {
            Id = acquisitionId, Status = AcquisitionStatus.Imported, Title = "T",
            ExternalIdsJson = "{}", SourceUrlsJson = "[]", CreatedAt = now, UpdatedAt = now
        });
        db.AcquisitionImportHints.Add(new AcquisitionImportHintRow {
            Id = Guid.NewGuid(), AcquisitionId = acquisitionId, SourcePath = sourcePath,
            ExternalIdsJson = externalIdsJson, SourceUrlsJson = "[]", Consumed = false, CreatedAt = now, UpdatedAt = now
        });
    }

    private static PrismediaDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<PrismediaDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}
