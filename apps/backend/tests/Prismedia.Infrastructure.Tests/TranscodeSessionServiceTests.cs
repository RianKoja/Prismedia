using Prismedia.Infrastructure.Videos;

namespace Prismedia.Infrastructure.Tests;

/// <summary>
/// Covers the liveness window and stale-session reaping that the transcode reaper relies on to
/// cancel abandoned ffmpeg jobs.
/// </summary>
public sealed class TranscodeSessionServiceTests {
    [Fact]
    public void LiveItemIdsIncludesARecentlyRegisteredSession() {
        var service = new TranscodeSessionService();
        var item = Guid.NewGuid();
        service.Register("s1", item);

        Assert.Contains(item, service.LiveItemIds(TimeSpan.FromHours(1)));
    }

    [Fact]
    public async Task LiveItemIdsExcludesSessionsOlderThanTheWindow() {
        var service = new TranscodeSessionService();
        var item = Guid.NewGuid();
        service.Register("s1", item);
        await Task.Delay(100);

        Assert.DoesNotContain(item, service.LiveItemIds(TimeSpan.FromMilliseconds(30)));
    }

    [Fact]
    public void LiveItemIdsIgnoresHeartbeatsWithNoAssociatedItem() {
        var service = new TranscodeSessionService();
        service.Ping("anonymous"); // a ping before any item is registered

        Assert.Empty(service.LiveItemIds(TimeSpan.FromHours(1)));
    }

    [Fact]
    public async Task ReapStaleSessionsRemovesOnlyAbandonedSessions() {
        var service = new TranscodeSessionService();
        service.Register("stale", Guid.NewGuid());
        await Task.Delay(100);
        var freshItem = Guid.NewGuid();
        service.Register("fresh", freshItem);

        var removed = service.ReapStaleSessions(TimeSpan.FromMilliseconds(30));

        Assert.Equal(1, removed);
        Assert.Contains(freshItem, service.LiveItemIds(TimeSpan.FromHours(1)));
    }
}
