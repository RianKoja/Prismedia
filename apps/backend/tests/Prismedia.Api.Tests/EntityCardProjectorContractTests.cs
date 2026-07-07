using Prismedia.Application.Entities;
using Prismedia.Contracts.Entities;
using Prismedia.Domain.Entities;
using Prismedia.Domain.Taxonomy;
using ContractEntityCapability = Prismedia.Contracts.Entities.EntityCapability;

namespace Prismedia.Api.Tests;

public sealed class EntityCardProjectorContractTests {
    [Fact]
    public void ProjectsLogoBeforeBackdropForThumbnailCoverUrls() {
        var studio = new Studio(Guid.NewGuid(), "GameChops");
        studio.AttachFile(EntityFileRole.Backdrop, "/assets/plugins/artwork/gamechops/banner.webp", "image/webp");
        studio.AttachFile(EntityFileRole.Logo, "/assets/plugins/artwork/gamechops/logo.webp", "image/webp");

        var card = EntityCardProjector.ToCard(studio);
        var images = AssertCapability<ImagesCapability>(card);

        Assert.Equal("/assets/plugins/artwork/gamechops/logo.webp", images.CoverUrl);
        Assert.Equal("/assets/plugins/artwork/gamechops/logo.webp", images.ThumbnailUrl);
        Assert.Equal(["logo", "backdrop"], images.Items.Select(item => item.Kind));
    }

    private static TCapability AssertCapability<TCapability>(EntityCard card)
        where TCapability : ContractEntityCapability =>
        Assert.IsType<TCapability>(Assert.Single(card.Capabilities.OfType<TCapability>()));
}
