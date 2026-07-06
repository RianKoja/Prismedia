using Microsoft.EntityFrameworkCore;
using Prismedia.Application.Security;
using Prismedia.Domain.Entities;
using Prismedia.Infrastructure.Entities;
using Prismedia.Infrastructure.Entities.Mappers;
using Prismedia.Infrastructure.Entities.Thumbnails;
using Prismedia.Infrastructure.Persistence;
using Prismedia.Infrastructure.Persistence.Entities;

namespace Prismedia.Infrastructure.Tests;

/// <summary>
/// Member library-access enforcement: a member sees only entities in granted roots,
/// hidden entities behave as missing, and per-user engagement never leaks across users.
/// </summary>
public sealed class UserLibraryVisibilityTests {
    private static readonly Guid GrantedRootId = Guid.Parse("aaaa0000-0000-0000-0000-000000000001");
    private static readonly Guid RestrictedRootId = Guid.Parse("aaaa0000-0000-0000-0000-000000000002");
    private static readonly Guid GrantedVideoId = Guid.Parse("bbbb0000-0000-0000-0000-000000000001");
    private static readonly Guid RestrictedVideoId = Guid.Parse("bbbb0000-0000-0000-0000-000000000002");

    [Fact]
    public async Task MemberSeesOnlyGrantedRootsInListsDetailsAndVisibilityChecks() {
        await using var db = CreateContext();
        await SeedTwoRootedVideosAsync(db);
        var member = TestUserContext.Member(GrantedRootId);
        var service = CreateService(db, member);

        var list = await service.ListAsync(EntityKindRegistry.Video.Code, null, null, null, null, CancellationToken.None);
        Assert.Equal(GrantedVideoId, Assert.Single(list.Items).Id);

        Assert.NotNull(await service.GetAsync(GrantedVideoId, hideNsfw: false, CancellationToken.None));
        Assert.Null(await service.GetAsync(RestrictedVideoId, hideNsfw: false, CancellationToken.None));

        var checker = new EfEntityVisibilityChecker(service);
        Assert.True(await checker.IsVisibleAsync(GrantedVideoId, CancellationToken.None));
        Assert.False(await checker.IsVisibleAsync(RestrictedVideoId, CancellationToken.None));
    }

    [Fact]
    public async Task AdminSeesEveryRootWithoutAccessRows() {
        await using var db = CreateContext();
        await SeedTwoRootedVideosAsync(db);
        var service = CreateService(db, TestUserContext.Admin());

        var list = await service.ListAsync(EntityKindRegistry.Video.Code, null, null, null, null, CancellationToken.None);

        Assert.Equal(2, list.TotalCount);
    }

    [Fact]
    public async Task EngagementStateIsIsolatedPerUser() {
        await using var db = CreateContext();
        await SeedTwoRootedVideosAsync(db);
        var otherUserId = Guid.Parse("cccc0000-0000-0000-0000-000000000009");
        var now = DateTimeOffset.UtcNow;
        db.UserEntityStates.Add(new UserEntityStateRow {
            UserId = otherUserId,
            EntityId = GrantedVideoId,
            IsFavorite = true,
            PlayCount = 5,
            LastPlayedAt = now,
            UpdatedAt = now
        });
        await db.SaveChangesAsync();

        // The test user has no state of their own: the other user's favorites and
        // playback must not surface.
        var service = CreateService(db, TestUserContext.Admin());
        var favorites = await service.ListAsync(
            EntityKindRegistry.Video.Code, null, null, null, null, CancellationToken.None, favorite: true);
        var thumbnails = await service.ListAsync(
            EntityKindRegistry.Video.Code, null, null, null, null, CancellationToken.None);

        Assert.Empty(favorites.Items);
        Assert.All(thumbnails.Items, item => Assert.False(item.IsFavorite));
        Assert.All(thumbnails.Items, item => Assert.Null(item.PlayCount));
    }

    private static EfEntityReadService CreateService(PrismediaDbContext db, ICurrentUserContext user) {
        var repository = new EfEntityRepository(db, user, EntityMappers.Kinds(db), EntityMappers.Capabilities(db, user));
        return new EfEntityReadService(db, user, repository, EntityMappers.Kinds(db), ThumbnailContributors.For(db));
    }

    private static async Task SeedTwoRootedVideosAsync(PrismediaDbContext db) {
        var now = DateTimeOffset.UtcNow;
        db.LibraryRoots.AddRange(
            new LibraryRootRow { Id = GrantedRootId, Path = "/media/a", Label = "A", Enabled = true, CreatedAt = now, UpdatedAt = now },
            new LibraryRootRow { Id = RestrictedRootId, Path = "/media/b", Label = "B", Enabled = true, CreatedAt = now, UpdatedAt = now });
        db.Entities.AddRange(
            new EntityRow { Id = GrantedVideoId, KindCode = EntityKindRegistry.Video.Code, Title = "Granted", CreatedAt = now, UpdatedAt = now },
            new EntityRow { Id = RestrictedVideoId, KindCode = EntityKindRegistry.Video.Code, Title = "Restricted", CreatedAt = now, UpdatedAt = now });
        db.VideoDetails.AddRange(
            new VideoDetailRow { EntityId = GrantedVideoId, LibraryRootId = GrantedRootId },
            new VideoDetailRow { EntityId = RestrictedVideoId, LibraryRootId = RestrictedRootId });
        await db.SaveChangesAsync();
    }

    private static PrismediaDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<PrismediaDbContext>()
            .UseInMemoryDatabase($"user-visibility-{Guid.NewGuid():N}")
            .Options);
}
