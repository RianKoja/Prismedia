using Prismedia.Domain.Entities;
using Prismedia.Domain.Media;

namespace Prismedia.Domain.Tests;

public sealed class CollectionModelTests {
    [Fact]
    public void ConfigureRulesNormalizesManualCollectionsAndKeepsCoverSettings() {
        var collection = new Collection(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            "Watch Later",
            CollectionMode.Dynamic,
            "{\"type\":\"group\"}",
            CollectionCoverMode.Mosaic);

        collection.ConfigureRules(CollectionMode.Manual, "{\"ignored\":true}");
        collection.SetCover(CollectionCoverMode.Item, Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));

        Assert.Equal(CollectionMode.Manual, collection.Mode);
        Assert.Null(collection.RuleTreeJson);
        Assert.Equal(CollectionCoverMode.Item, collection.CoverMode);
        Assert.Equal(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), collection.CoverItemId);
    }

    [Fact]
    public void ConfigureRulesRequiresRulesForRuleDrivenCollections() {
        var collection = new Collection(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"), "Smart Picks");

        var ex = Assert.Throws<ArgumentException>(() =>
            collection.ConfigureRules(CollectionMode.Dynamic, null));

        Assert.Contains("Rule-driven collections require", ex.Message);
    }

    [Fact]
    public void ManualMembershipIsDomainGuardedByModeAndItemKind() {
        var manual = new Collection(Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"), "Manual");
        var dynamic = new Collection(
            Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
            "Dynamic",
            CollectionMode.Dynamic,
            "{\"type\":\"group\"}");

        Assert.True(manual.CanEditManualMembership);
        Assert.False(dynamic.CanEditManualMembership);
        Assert.True(Collection.CanContain(EntityKind.VideoSeries));
        Assert.True(Collection.CanContain(EntityKind.Movie));
        Assert.True(Collection.CanContain(EntityKind.MusicArtist));
        Assert.True(Collection.CanContain(EntityKind.AudioLibrary));
        Assert.True(Collection.CanContain(EntityKind.AudioTrack));
        Assert.False(Collection.CanContain(EntityKind.Collection));
    }
}
